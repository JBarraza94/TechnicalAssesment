using CompanyApi.Application.Common;
using CompanyApi.Application.Domain;

namespace CompanyApi.Application.Services;

public sealed record CreateCompanyCommand(string? Name, string? Website);

/// <summary>
/// Search criteria. All filters are optional and combined with AND.
/// Sorting (SortBy/SortDir) can be added here later without changing existing callers.
/// </summary>
public sealed record CompanySearchQuery(
    string? Name = null,
    string? Domain = null,
    string? Q = null,
    int? Page = null,
    int? PageSize = null);

/// <summary>A search hit; Score/Explanation are only set for relevance ("q") searches.</summary>
public sealed record CompanySearchHit(Company Company, double? Score = null, string? Explanation = null);

public sealed record CompanySearchResults(IReadOnlyList<CompanySearchHit> Items, int TotalCount);

public interface ICompanyService
{
    Task<Result<Company>> CreateAsync(CreateCompanyCommand command, CancellationToken cancellationToken = default);

    Task<Result<Company>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CompanySearchResults> SearchAsync(CompanySearchQuery query, CancellationToken cancellationToken = default);
}
