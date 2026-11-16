using System.Net;
using CompanyApi.Application.Relevance;
using CompanyApi.Infrastructure.Content;
using Microsoft.Extensions.Options;

namespace CompanyApi.UnitTests.Content;

public sealed class HttpWebsiteContentFetcherTests
{
    private const string Html = """
        <html><head>
          <title> Verus &amp; Co | Home </title>
          <meta property="og:site_name" content="Verus">
          <meta name="description" content="Verus builds tools.">
        </head><body>...</body></html>
        """;

    [Fact]
    public void Parse_HtmlWithMetadata_ExtractsTitleSiteNameAndDescription()
    {
        var content = HttpWebsiteContentFetcher.Parse(Html);

        content.Should().Be(new CompanyApi.Application.Abstractions.WebsiteContent("Verus & Co | Home", "Verus", "Verus builds tools."));
    }

    [Fact]
    public void Parse_HtmlWithoutMetadata_ReturnsNull()
    {
        HttpWebsiteContentFetcher.Parse("<html><body>hi</body></html>").Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_HtmlResponse_ReturnsParsedContent()
    {
        var sut = CreateSut(new StubHandler(HttpStatusCode.OK, Html, "text/html"));

        var content = await sut.FetchAsync(new Uri("https://www.verus.com"));

        content!.SiteName.Should().Be("Verus");
    }

    [Fact]
    public async Task FetchAsync_NonSuccessStatus_ReturnsNull()
    {
        var sut = CreateSut(new StubHandler(HttpStatusCode.InternalServerError, "oops", "text/html"));

        (await sut.FetchAsync(new Uri("https://www.verus.com"))).Should().BeNull();
    }

    private static HttpWebsiteContentFetcher CreateSut(HttpMessageHandler handler) =>
        new(new HttpClient(handler), Options.Create(new RelevanceOptions()));

    private sealed class StubHandler(HttpStatusCode status, string body, string mediaType) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType) });
    }
}
