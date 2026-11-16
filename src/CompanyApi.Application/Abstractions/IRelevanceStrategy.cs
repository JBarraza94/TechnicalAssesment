namespace CompanyApi.Application.Abstractions;

/// <summary>A relevance score in [0, 1] plus a human-readable explanation of how it was computed.</summary>
public sealed record RelevanceResult(double Score, string Explanation);

/// <summary>
/// Single relevance model used both to validate name/website pairs and to rank free-text searches.
/// Swap in an embedding/ML-based implementation by registering a different class.
/// </summary>
public interface IRelevanceStrategy
{
    /// <summary>How relevant is <paramref name="companyName"/> to <paramref name="website"/>? May inspect content (async).</summary>
    Task<RelevanceResult> EvaluateAsync(string companyName, Uri website, CancellationToken cancellationToken = default);

    /// <summary>How well does a free-text <paramref name="query"/> match a company's name or domain? Offline and deterministic.</summary>
    RelevanceResult ScoreQuery(string query, string companyName, Uri website);
}
