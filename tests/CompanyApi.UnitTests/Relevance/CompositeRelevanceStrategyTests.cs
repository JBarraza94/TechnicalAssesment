using CompanyApi.Application.Abstractions;
using CompanyApi.Application.Relevance;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CompanyApi.UnitTests.Relevance;

public sealed class CompositeRelevanceStrategyTests
{
    private const double Threshold = 0.5;

    private static CompositeRelevanceStrategy CreateSut(
        IWebsiteContentFetcher? fetcher = null,
        bool enableContent = false,
        ILogger<CompositeRelevanceStrategy>? logger = null) =>
        new(
            fetcher ?? Mock.Of<IWebsiteContentFetcher>(),
            Options.Create(new RelevanceOptions { Threshold = Threshold, EnableContentInspection = enableContent }),
            logger ?? NullLogger<CompositeRelevanceStrategy>.Instance);

    [Theory]
    [InlineData("MyCompany", "https://www.mycompany.com")]
    [InlineData("Acme Tools Inc.", "https://acmetools.com")]
    [InlineData("Café Niño S.A. de C.V.", "https://www.cafenino.com.mx")]
    [InlineData("Señor Pañuelo, Ltd.", "https://senor-panuelo.co.uk")]
    [InlineData("International Business Machines", "https://www.ibm.com")]
    public async Task EvaluateAsync_RelevantPair_ScoresAboveThreshold(string name, string website)
    {
        var result = await CreateSut().EvaluateAsync(name, new Uri(website));

        result.Score.Should().BeGreaterThanOrEqualTo(Threshold);
        result.Explanation.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task EvaluateAsync_ExactDomainMatch_ScoresOne()
    {
        var result = await CreateSut().EvaluateAsync("MyCompany", new Uri("https://www.mycompany.com"));

        result.Score.Should().Be(1.0);
        result.Explanation.Should().Contain("domain match 1.00");
    }

    [Theory]
    [InlineData("MyCompany", "https://www.google.com")]
    [InlineData("Banana Co", "https://www.microsoft.com")]
    [InlineData("Globex Corporation", "https://initech.com")]
    public async Task EvaluateAsync_IrrelevantPair_ScoresBelowThresholdWithExplanation(string name, string website)
    {
        var result = await CreateSut().EvaluateAsync(name, new Uri(website));

        result.Score.Should().BeLessThan(Threshold);
        result.Explanation.Should().Contain("token overlap").And.Contain("fuzzy similarity");
    }

    [Theory]
    [InlineData("Microsft", "https://microsoft.com")]
    [InlineData("Gogle", "https://www.google.com")]
    public async Task EvaluateAsync_Typo_PassesThroughFuzzyMatch(string name, string website)
    {
        var result = await CreateSut().EvaluateAsync(name, new Uri(website));

        result.Score.Should().BeGreaterThanOrEqualTo(Threshold);
    }

    [Theory]
    [InlineData("MyCompany", "https://www.mycompany.com")]
    [InlineData("MyCompany", "https://www.google.com")]
    [InlineData("!!", "https://www.example.com")]
    [InlineData("Co", "https://co.com")]
    [InlineData("a b c d e f g", "https://abcdefg.com")]
    [InlineData("Acme Acme Acme Acme", "https://acme.com")]
    public async Task EvaluateAsync_AnyInput_ScoreIsBetweenZeroAndOne(string name, string website)
    {
        var result = await CreateSut().EvaluateAsync(name, new Uri(website));

        result.Score.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public async Task EvaluateAsync_ContentTitleContainsName_RaisesScore()
    {
        var fetcher = new Mock<IWebsiteContentFetcher>();
        fetcher.Setup(f => f.FetchAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebsiteContent("Contoso Pharmaceuticals - Home", null, null));
        var website = new Uri("https://www.cpharma.com");

        var offline = await CreateSut().EvaluateAsync("Contoso Pharmaceuticals", website);
        var boosted = await CreateSut(fetcher.Object, enableContent: true).EvaluateAsync("Contoso Pharmaceuticals", website);

        boosted.Score.Should().BeGreaterThan(offline.Score);
        boosted.Score.Should().BeLessThanOrEqualTo(1.0);
        boosted.Explanation.Should().Contain("2/2 name tokens found");
    }

    [Fact]
    public async Task EvaluateAsync_ContentInspectionDisabled_NeverCallsFetcher()
    {
        var fetcher = new Mock<IWebsiteContentFetcher>(MockBehavior.Strict);

        await CreateSut(fetcher.Object, enableContent: false).EvaluateAsync("Contoso", new Uri("https://x-corp.com"));

        fetcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EvaluateAsync_FetcherThrows_FallsBackToOfflineScoreAndLogsWarning()
    {
        var fetcher = new Mock<IWebsiteContentFetcher>();
        fetcher.Setup(f => f.FetchAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("DNS failure"));
        var logger = new Mock<ILogger<CompositeRelevanceStrategy>>();
        var website = new Uri("https://www.google.com");

        var offline = await CreateSut().EvaluateAsync("MyCompany", website);
        var act = () => CreateSut(fetcher.Object, enableContent: true, logger.Object).EvaluateAsync("MyCompany", website);

        var result = (await act.Should().NotThrowAsync()).Subject;
        result.Score.Should().Be(offline.Score);
        result.Explanation.Should().Contain("Content inspection unavailable");
        logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<HttpRequestException>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task EvaluateAsync_FetcherTimesOut_FallsBackToOfflineScore()
    {
        var fetcher = new Mock<IWebsiteContentFetcher>();
        fetcher.Setup(f => f.FetchAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("The request timed out."));
        var website = new Uri("https://acmetools.com");

        var offline = await CreateSut().EvaluateAsync("Acme", website);
        var result = await CreateSut(fetcher.Object, enableContent: true).EvaluateAsync("Acme", website);

        result.Score.Should().Be(offline.Score);
    }

    [Fact]
    public void ScoreQuery_PartialName_ScoresHigherThanUnrelatedCompany()
    {
        var sut = CreateSut();

        var mycompany = sut.ScoreQuery("mycompany", "MyCompany", new Uri("https://www.mycompany.com"));
        var acme = sut.ScoreQuery("mycompany", "Acme Tools Inc.", new Uri("https://acmetools.com"));

        mycompany.Score.Should().Be(1.0);
        acme.Score.Should().BeLessThan(mycompany.Score);
        acme.Score.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void ScoreQuery_MatchesDomainWhenNameDiffers_UsesDomainScore()
    {
        var result = CreateSut().ScoreQuery("contoso", "C Corp Holdings", new Uri("https://contoso.com"));

        result.Score.Should().Be(1.0);
        result.Explanation.Should().Contain("domain 'contoso'");
    }
}
