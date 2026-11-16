using System.Globalization;

namespace CompanyApi.Application.Relevance;

/// <summary>Per-signal breakdown of an offline relevance score; every value is in [0, 1].</summary>
public sealed record RelevanceBreakdown(double DomainMatch, double TokenOverlap, double Fuzzy, double Score)
{
    public string Describe() => string.Create(CultureInfo.InvariantCulture,
        $"domain match {DomainMatch:0.00}, token overlap {TokenOverlap:0.00}, fuzzy similarity {Fuzzy:0.00} -> offline score {Score:0.00}");
}

/// <summary>
/// Pure, deterministic scorer comparing a list of tokens against a target string
/// (a domain label such as "acmetools", or a concatenated company name).
/// </summary>
public static class OfflineRelevanceScorer
{
    // Weights (sum = 1). An exact domain match short-circuits to 1.0; otherwise:
    //   score = 0.6 * tokenOverlap + 0.4 * fuzzy
    // Token overlap dominates because it is the strongest evidence that the name *is* the domain;
    // fuzzy similarity covers typos/abbreviations that overlap misses (Microsft vs microsoft).
    public const double TokenOverlapWeight = 0.6;
    public const double FuzzyWeight = 0.4;

    /// <summary>Jaro-Winkler similarity at which a token counts as a (typo-tolerant) match.</summary>
    public const double FuzzyTokenMatchThreshold = 0.88;

    private const int MinSubstringTokenLength = 3;
    private const int MinFuzzyTokenLength = 4;

    public static RelevanceBreakdown Score(IReadOnlyList<string> tokens, string target, IReadOnlyList<string> targetTokens)
    {
        var compactTarget = target.Replace("-", string.Empty, StringComparison.Ordinal);
        if (tokens.Count == 0 || compactTarget.Length == 0)
        {
            return new RelevanceBreakdown(0, 0, 0, 0);
        }

        var concatenated = string.Concat(tokens);
        var domainMatch = IsExactMatch(tokens, concatenated, compactTarget) ? 1.0 : 0.0;
        var overlap = TokenOverlap(tokens, compactTarget, targetTokens);
        var fuzzy = StringSimilarity.JaroWinkler(concatenated, compactTarget);

        var score = domainMatch >= 1.0
            ? 1.0
            : TokenOverlapWeight * overlap + FuzzyWeight * fuzzy;

        return new RelevanceBreakdown(domainMatch, overlap, fuzzy, Math.Clamp(score, 0.0, 1.0));
    }

    // "mycompany" == "mycompany", or an acronym of a multi-word name: "International Business Machines" -> "ibm".
    private static bool IsExactMatch(IReadOnlyList<string> tokens, string concatenated, string target) =>
        concatenated == target ||
        (tokens.Count >= 2 && string.Concat(tokens.Select(t => t[0])) == target);

    /// <summary>
    /// Average of containment (share of tokens found in the target) and coverage (share of the target
    /// explained by matched tokens). Substring matches count: "acme" is found in "acmetools".
    /// </summary>
    private static double TokenOverlap(IReadOnlyList<string> tokens, string target, IReadOnlyList<string> targetTokens)
    {
        var matchedTokens = 0;
        var matchedChars = 0;

        foreach (var token in tokens)
        {
            var exactOrSubstring =
                targetTokens.Contains(token) ||
                (token.Length >= MinSubstringTokenLength && target.Contains(token, StringComparison.Ordinal));

            var fuzzy = !exactOrSubstring && token.Length >= MinFuzzyTokenLength &&
                        (StringSimilarity.JaroWinkler(token, target) >= FuzzyTokenMatchThreshold ||
                         targetTokens.Any(t => StringSimilarity.JaroWinkler(token, t) >= FuzzyTokenMatchThreshold));

            if (!exactOrSubstring && !fuzzy) continue;

            matchedTokens++;
            matchedChars += Math.Min(token.Length, target.Length);
        }

        var containment = (double)matchedTokens / tokens.Count;
        var coverage = Math.Min(1.0, (double)matchedChars / target.Length);
        return (containment + coverage) / 2.0;
    }
}
