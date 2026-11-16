using System.Globalization;
using CompanyApi.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompanyApi.Application.Relevance;

/// <summary>
/// Offline score (domain match + token overlap + fuzzy similarity), optionally boosted by page metadata.
/// Content inspection can only raise the score and never fails the request.
/// </summary>
public sealed class CompositeRelevanceStrategy(
    IWebsiteContentFetcher contentFetcher,
    IOptions<RelevanceOptions> options,
    ILogger<CompositeRelevanceStrategy> logger) : IRelevanceStrategy
{
    // Content can close at most 40% of the gap between the offline score and 1.0:
    //   final = offline + 0.4 * contentMatchRatio * (1 - offline)
    public const double ContentBoostWeight = 0.4;

    private readonly RelevanceOptions _options = options.Value;

    public async Task<RelevanceResult> EvaluateAsync(string companyName, Uri website, CancellationToken cancellationToken = default)
    {
        var nameTokens = TextNormalizer.NameTokens(companyName);
        var label = TextNormalizer.DomainLabel(website.Host);
        var breakdown = OfflineRelevanceScorer.Score(nameTokens, label, TextNormalizer.DomainTokens(label));

        var explanation = Invariant(
            $"Name tokens [{string.Join(", ", nameTokens)}] vs domain '{label}': {breakdown.Describe()}.");

        if (!_options.EnableContentInspection || breakdown.Score >= 1.0)
        {
            return new RelevanceResult(breakdown.Score, explanation);
        }

        var (ratio, note) = await InspectContentAsync(website, nameTokens, cancellationToken);
        var final = Math.Clamp(breakdown.Score + ContentBoostWeight * ratio * (1 - breakdown.Score), 0.0, 1.0);
        return new RelevanceResult(final, Invariant($"{explanation} {note} Final score {final:0.00}."));
    }

    public RelevanceResult ScoreQuery(string query, string companyName, Uri website)
    {
        var queryTokens = TextNormalizer.NameTokens(query);
        var nameTokens = TextNormalizer.NameTokens(companyName);
        var label = TextNormalizer.DomainLabel(website.Host);

        var byName = OfflineRelevanceScorer.Score(queryTokens, string.Concat(nameTokens), nameTokens);
        var byDomain = OfflineRelevanceScorer.Score(queryTokens, label, TextNormalizer.DomainTokens(label));

        return byName.Score >= byDomain.Score
            ? new RelevanceResult(byName.Score, Invariant($"Query matched name: {byName.Describe()}."))
            : new RelevanceResult(byDomain.Score, Invariant($"Query matched domain '{label}': {byDomain.Describe()}."));
    }

    private async Task<(double Ratio, string Note)> InspectContentAsync(
        Uri website, IReadOnlyList<string> nameTokens, CancellationToken cancellationToken)
    {
        try
        {
            var content = await contentFetcher.FetchAsync(website, cancellationToken);
            if (content is null)
            {
                return (0, "Content inspection: no usable metadata.");
            }

            var text = TextNormalizer.NormalizeText($"{content.Title} {content.SiteName} {content.Description}");
            var compact = text.Replace(" ", string.Empty, StringComparison.Ordinal);
            var found = nameTokens.Count(t => t.Length >= 2 && (text.Contains(t, StringComparison.Ordinal) || compact.Contains(t, StringComparison.Ordinal)));
            var ratio = nameTokens.Count == 0 ? 0 : (double)found / nameTokens.Count;
            return (ratio, Invariant($"Content inspection: {found}/{nameTokens.Count} name tokens found in page metadata (boost weight {ContentBoostWeight})."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // the caller cancelled; not a content failure
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Content inspection failed for {Website}; falling back to the offline score", website);
            return (0, "Content inspection unavailable; offline score used.");
        }
    }

    private static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);
}
