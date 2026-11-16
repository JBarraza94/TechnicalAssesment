using System.Globalization;
using CompanyApi.Application.Abstractions;
using CompanyApi.Application.Common;
using CompanyApi.Application.Domain;
using CompanyApi.Application.Relevance;
using CompanyApi.Application.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompanyApi.Application.Services;

public sealed class CompanyService(
    ICompanyRepository repository,
    ICompanyInputValidator validator,
    IRelevanceStrategy relevanceStrategy,
    IOptions<RelevanceOptions> relevanceOptions,
    TimeProvider timeProvider,
    ILogger<CompanyService> logger) : ICompanyService
{
    private readonly RelevanceOptions _relevance = relevanceOptions.Value;

    public async Task<Result<Company>> CreateAsync(CreateCompanyCommand command, CancellationToken cancellationToken = default)
    {
        var validation = validator.Validate(command.Name, command.Website);
        if (!validation.IsValid)
        {
            logger.LogWarning("Company creation rejected by validation: {Reasons}", string.Join(" ", validation.Errors));
            return Result<Company>.Invalid(validation.Errors[0], validation.Errors);
        }

        var name = validation.NormalizedName!;
        var website = command.Website!.Trim();
        var uri = validation.WebsiteUri!;

        var relevance = await relevanceStrategy.EvaluateAsync(name, uri, cancellationToken);
        if (relevance.Score < _relevance.Threshold)
        {
            var message = $"Company name '{name}' does not appear to be relevant to website '{website}'.";
            logger.LogWarning(
                "Company creation rejected by relevance check: {Name} / {Website} scored {Score:0.00} (threshold {Threshold:0.00}). {Explanation}",
                name, website, relevance.Score, _relevance.Threshold, relevance.Explanation);
            return Result<Company>.Invalid(message, [message, relevance.Explanation]);
        }

        var company = new Company(
            Guid.NewGuid(),
            name,
            website,
            TextNormalizer.NormalizeHost(uri.Host),
            timeProvider.GetUtcNow());

        await repository.AddAsync(company, cancellationToken);
        logger.LogInformation("Created company {CompanyId} '{Name}' ({Website}) with relevance {Score:0.00}",
            company.Id, company.Name, company.Website, relevance.Score);

        return Result<Company>.Ok(company);
    }

    public async Task<Result<Company>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var company = await repository.GetByIdAsync(id, cancellationToken);
        if (company is null)
        {
            logger.LogInformation("Company {CompanyId} not found", id);
            return Result<Company>.NotFound($"Company '{id}' was not found.");
        }

        logger.LogInformation("Retrieved company {CompanyId}", id);
        return Result<Company>.Ok(company);
    }

    public async Task<CompanySearchResults> SearchAsync(CompanySearchQuery query, CancellationToken cancellationToken = default)
    {
        // In-memory filtering is fine here; with EF Core the name/domain filters would move into the repository query.
        var companies = await repository.GetAllAsync(cancellationToken);
        IEnumerable<Company> filtered = companies;

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            var term = query.Name.Trim();
            filtered = filtered.Where(c => ContainsIgnoreCaseAndAccents(c.Name, term));
        }

        if (!string.IsNullOrWhiteSpace(query.Domain))
        {
            var domain = NormalizeDomainFilter(query.Domain);
            filtered = filtered.Where(c => c.Host == domain || c.Host.EndsWith("." + domain, StringComparison.Ordinal));
        }

        IReadOnlyList<CompanySearchHit> hits;
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var q = query.Q.Trim();
            hits = filtered
                .Select(c => (Company: c, Relevance: relevanceStrategy.ScoreQuery(q, c.Name, new Uri(c.Website))))
                .Where(x => x.Relevance.Score >= _relevance.SearchMinScore)
                .OrderByDescending(x => x.Relevance.Score)
                .ThenBy(x => x.Company.Name, StringComparer.OrdinalIgnoreCase)
                .Select(x => new CompanySearchHit(x.Company, Math.Round(x.Relevance.Score, 4), x.Relevance.Explanation))
                .ToList();
        }
        else
        {
            hits = filtered
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(c => c.CreatedAtUtc)
                .Select(c => new CompanySearchHit(c))
                .ToList();
        }

        var total = hits.Count;
        if (query.PageSize is { } size)
        {
            var page = query.Page ?? 1;
            hits = hits.Skip((page - 1) * size).Take(size).ToList();
        }

        logger.LogInformation(
            "Company search (name: {Name}, domain: {Domain}, q: {Query}) returned {Count} of {Total} results",
            query.Name, query.Domain, query.Q, hits.Count, total);

        return new CompanySearchResults(hits, total);
    }

    private static bool ContainsIgnoreCaseAndAccents(string source, string term) =>
        CultureInfo.InvariantCulture.CompareInfo.IndexOf(
            source, term, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    // Accepts "verus.com", "www.verus.com" or a full URL such as "https://www.verus.com/about".
    private static string NormalizeDomainFilter(string domain)
    {
        var value = domain.Trim();
        var host = Uri.TryCreate(value, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
            ? uri.Host
            : value.Split('/', 2)[0];
        return TextNormalizer.NormalizeHost(host);
    }
}
