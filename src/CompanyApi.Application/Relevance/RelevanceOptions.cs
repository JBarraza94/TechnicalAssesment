using System.ComponentModel.DataAnnotations;

namespace CompanyApi.Application.Relevance;

/// <summary>Bound from the "Relevance" section of appsettings.json.</summary>
public sealed class RelevanceOptions
{
    public const string SectionName = "Relevance";

    /// <summary>Minimum score a name/website pair needs to be accepted.</summary>
    [Range(0.0, 1.0)]
    public double Threshold { get; set; } = 0.5;

    /// <summary>Minimum score a company needs to be included in "q" search results.</summary>
    [Range(0.0, 1.0)]
    public double SearchMinScore { get; set; } = 0.3;

    /// <summary>When true, the home page title/og:site_name/meta description may boost the score. Off by default for determinism.</summary>
    public bool EnableContentInspection { get; set; }

    [Range(1, 30)]
    public int ContentTimeoutSeconds { get; set; } = 3;

    [Range(1024, 4 * 1024 * 1024)]
    public int ContentMaxBytes { get; set; } = 256 * 1024;
}
