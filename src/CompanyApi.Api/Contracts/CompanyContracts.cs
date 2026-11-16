using System.ComponentModel.DataAnnotations;
using CompanyApi.Application.Validation;

namespace CompanyApi.Api.Contracts;

/// <summary>Payload to create a company.</summary>
public sealed record CreateCompanyRequest
{
    /// <summary>Company name (2-200 characters after trimming).</summary>
    /// <example>MyCompany</example>
    [Required(ErrorMessage = CompanyInputValidator.Messages.NameRequired)]
    [MaxLength(CompanyInputValidator.NameMaxLength + 50, ErrorMessage = "Company name is too long.")]
    public string? Name { get; init; }

    /// <summary>Absolute http(s) URL of the company's website.</summary>
    /// <example>https://www.mycompany.com</example>
    [Required(ErrorMessage = CompanyInputValidator.Messages.WebsiteRequired)]
    public string? Website { get; init; }
}

/// <summary>A stored company.</summary>
/// <param name="Id">Server-generated identifier.</param>
/// <param name="Name">Company name.</param>
/// <param name="Website">Website URL.</param>
public sealed record CompanyResponse(Guid Id, string Name, string Website);

/// <summary>An item returned by GET /api/companies. Score/Explanation are only present when "q" is used.</summary>
/// <param name="Id">Server-generated identifier.</param>
/// <param name="Name">Company name.</param>
/// <param name="Website">Website URL.</param>
/// <param name="Score">Relevance score in [0, 1] for "q" searches.</param>
/// <param name="Explanation">How the relevance score was computed.</param>
public sealed record CompanySearchResult(Guid Id, string Name, string Website, double? Score = null, string? Explanation = null);

/// <summary>Query string for GET /api/companies. Every parameter is optional.</summary>
public sealed record CompanySearchRequest
{
    /// <summary>Partial, case-insensitive match on the company name.</summary>
    [MaxLength(CompanyInputValidator.NameMaxLength)]
    public string? Name { get; init; }

    /// <summary>Website host match, ignoring "www." (e.g. example.com).</summary>
    [MaxLength(253)]
    public string? Domain { get; init; }

    /// <summary>Relevance search across name and domain; results ordered by score descending.</summary>
    [MaxLength(CompanyInputValidator.NameMaxLength)]
    public string? Q { get; init; }

    /// <summary>1-based page number (requires pageSize).</summary>
    [Range(1, int.MaxValue, ErrorMessage = "page must be 1 or greater.")]
    public int? Page { get; init; }

    /// <summary>Page size (1-100). When omitted, all results are returned.</summary>
    [Range(1, 100, ErrorMessage = "pageSize must be between 1 and 100.")]
    public int? PageSize { get; init; }
}

/// <summary>Uniform error body for every 4xx/5xx response.</summary>
/// <param name="Message">Short, human-friendly summary.</param>
/// <param name="Errors">All individual error messages.</param>
public sealed record ErrorResponse(string Message, IReadOnlyList<string> Errors)
{
    public static ErrorResponse From(string message) => new(message, [message]);
}
