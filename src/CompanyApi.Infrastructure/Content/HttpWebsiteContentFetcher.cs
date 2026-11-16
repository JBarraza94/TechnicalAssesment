using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using CompanyApi.Application.Abstractions;
using CompanyApi.Application.Relevance;
using Microsoft.Extensions.Options;

namespace CompanyApi.Infrastructure.Content;

/// <summary>
/// Reads a bounded prefix of the home page and extracts &lt;title&gt;, og:site_name and meta description.
/// Timeout and size limit come from <see cref="RelevanceOptions"/>. Errors propagate to the caller.
/// </summary>
public sealed partial class HttpWebsiteContentFetcher(HttpClient httpClient, IOptions<RelevanceOptions> options)
    : IWebsiteContentFetcher
{
    private readonly RelevanceOptions _options = options.Value;

    public async Task<WebsiteContent?> FetchAsync(Uri website, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.ContentTimeoutSeconds));

        using var request = new HttpRequestMessage(HttpMethod.Get, website);
        request.Headers.Accept.ParseAdd("text/html");

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode ||
            response.Content.Headers.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) != true)
        {
            return null;
        }

        var html = await ReadPrefixAsync(response.Content, _options.ContentMaxBytes, timeout.Token);
        return Parse(html);
    }

    public static WebsiteContent? Parse(string html)
    {
        var title = Clean(TitleRegex().Match(html).Groups["v"].Value);
        string? siteName = null;
        string? description = null;

        foreach (Match meta in MetaRegex().Matches(html))
        {
            var tag = meta.Value;
            var key = AttrRegex("property").Match(tag) is { Success: true } p ? p.Groups["v"].Value
                : AttrRegex("name").Match(tag) is { Success: true } n ? n.Groups["v"].Value : null;
            var content = AttrRegex("content").Match(tag) is { Success: true } c ? Clean(c.Groups["v"].Value) : null;

            if (string.Equals(key, "og:site_name", StringComparison.OrdinalIgnoreCase)) siteName ??= content;
            else if (string.Equals(key, "description", StringComparison.OrdinalIgnoreCase)) description ??= content;
        }

        return title is null && siteName is null && description is null
            ? null
            : new WebsiteContent(title, siteName, description);
    }

    private static async Task<string> ReadPrefixAsync(HttpContent content, int maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[maxBytes];
        var total = 0;
        int read;
        while (total < maxBytes && (read = await stream.ReadAsync(buffer.AsMemory(total, maxBytes - total), cancellationToken)) > 0)
        {
            total += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, total);
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WebUtility.HtmlDecode(value).Trim();

    private static Regex AttrRegex(string name) =>
        new($"""\b{name}\s*=\s*["'](?<v>[^"']*)["']""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    [GeneratedRegex(@"<title[^>]*>(?<v>[^<]*)</title>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"<meta\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 100)]
    private static partial Regex MetaRegex();
}
