namespace CompanyApi.Application.Abstractions;

/// <summary>Metadata extracted from a website's home page.</summary>
public sealed record WebsiteContent(string? Title, string? SiteName, string? Description);

public interface IWebsiteContentFetcher
{
    /// <summary>
    /// Fetches page metadata. Implementations may throw on network errors or timeouts; callers must handle that.
    /// Returns null when the response is not usable HTML.
    /// </summary>
    Task<WebsiteContent?> FetchAsync(Uri website, CancellationToken cancellationToken = default);
}
