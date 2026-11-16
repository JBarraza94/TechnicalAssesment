namespace CompanyApi.Application.Validation;

/// <summary>Result of validating raw company input.</summary>
public sealed record CompanyValidationResult(
    IReadOnlyList<string> Errors,
    string? NormalizedName,
    Uri? WebsiteUri)
{
    public bool IsValid => Errors.Count == 0;
}

public interface ICompanyInputValidator
{
    CompanyValidationResult Validate(string? name, string? website);
}

/// <summary>
/// Authoritative validation rules for company input (the API's data annotations are a first line of defense only).
/// </summary>
public sealed class CompanyInputValidator : ICompanyInputValidator
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 200;
    public const int WebsiteMaxLength = 2048;

    public static class Messages
    {
        public const string NameRequired = "Company name is required.";
        public static readonly string NameTooShort = $"Company name must be at least {NameMinLength} characters.";
        public static readonly string NameTooLong = $"Company name must be at most {NameMaxLength} characters.";
        public const string WebsiteRequired = "Website URL is required.";
        public const string WebsiteMalformed = "Website URL must be an absolute, well-formed URL (e.g. https://www.example.com).";
        public const string WebsiteScheme = "Website URL scheme must be http or https.";
        public const string WebsiteHost = "Website URL host must contain a dot (e.g. example.com).";
        public static readonly string WebsiteTooLong = $"Website URL must be at most {WebsiteMaxLength} characters.";
    }

    public CompanyValidationResult Validate(string? name, string? website)
    {
        var errors = new List<string>();
        var normalizedName = ValidateName(name, errors);
        var uri = ValidateWebsite(website, errors);
        return new CompanyValidationResult(errors, normalizedName, uri);
    }

    private static string? ValidateName(string? name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add(Messages.NameRequired);
            return null;
        }

        var trimmed = name.Trim();
        if (trimmed.Length < NameMinLength)
        {
            errors.Add(Messages.NameTooShort);
            return null;
        }

        if (trimmed.Length > NameMaxLength)
        {
            errors.Add(Messages.NameTooLong);
            return null;
        }

        return trimmed;
    }

    private static Uri? ValidateWebsite(string? website, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(website))
        {
            errors.Add(Messages.WebsiteRequired);
            return null;
        }

        var trimmed = website.Trim();
        if (trimmed.Length > WebsiteMaxLength)
        {
            errors.Add(Messages.WebsiteTooLong);
            return null;
        }

        // "example.com" is not absolute; on Unix, "/path" parses as file:// so the scheme check below catches it.
        if (!Uri.IsWellFormedUriString(trimmed, UriKind.Absolute) ||
            !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            errors.Add(Messages.WebsiteMalformed);
            return null;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add(Messages.WebsiteScheme);
            return null;
        }

        var host = uri.IdnHost;
        if (string.IsNullOrEmpty(host) || !host.Contains('.') || host.StartsWith('.') || host.EndsWith('.'))
        {
            errors.Add(Messages.WebsiteHost);
            return null;
        }

        return uri;
    }
}
