using CompanyApi.Application.Abstractions;
using CompanyApi.Application.Common;
using CompanyApi.Application.Domain;
using CompanyApi.Application.Relevance;
using CompanyApi.Application.Services;
using CompanyApi.Application.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CompanyApi.UnitTests.Services;

public sealed class CompanyServiceTests
{
    private readonly Mock<ICompanyRepository> _repository = new();
    private readonly Mock<IRelevanceStrategy> _relevance = new();
    private readonly Mock<ILogger<CompanyService>> _logger = new();
    private readonly CompanyService _sut;

    public CompanyServiceTests()
    {
        _relevance
            .Setup(r => r.EvaluateAsync(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevanceResult(0.9, "relevant"));

        _sut = new CompanyService(
            _repository.Object,
            new CompanyInputValidator(),
            _relevance.Object,
            Options.Create(new RelevanceOptions { Threshold = 0.5, SearchMinScore = 0.3 }),
            TimeProvider.System,
            _logger.Object);
    }

    // ---------- Create ----------

    [Fact]
    public async Task CreateAsync_ValidInput_PersistsOnceAndReturnsCompanyWithNewId()
    {
        var result = await _sut.CreateAsync(new CreateCompanyCommand("  Verus LLC ", " https://www.verus.com "));

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.Id.Should().NotBe(Guid.Empty);
        result.Value.Name.Should().Be("Verus LLC");
        result.Value.Website.Should().Be("https://www.verus.com");
        result.Value.Host.Should().Be("verus.com");
        _repository.Verify(r => r.AddAsync(
            It.Is<Company>(c => c.Id == result.Value.Id && c.Name == "Verus LLC"), It.IsAny<CancellationToken>()), Times.Once);
        VerifyLogged(LogLevel.Information);
    }

    [Theory]
    [InlineData("", "https://www.verus.com", "Company name is required.")]
    [InlineData("V", "https://www.verus.com", "Company name must be at least 2 characters.")]
    [InlineData("Verus", "verus.com", CompanyInputValidator.Messages.WebsiteMalformed)]
    [InlineData("Verus", "ftp://verus.com", CompanyInputValidator.Messages.WebsiteScheme)]
    public async Task CreateAsync_InvalidInput_ReturnsInvalidAndNeverPersists(string name, string website, string message)
    {
        var result = await _sut.CreateAsync(new CreateCompanyCommand(name, website));

        result.Status.Should().Be(ResultStatus.Invalid);
        result.Message.Should().Be(message);
        _repository.Verify(r => r.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()), Times.Never);
        _relevance.Verify(r => r.EvaluateAsync(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyLogged(LogLevel.Warning);
    }

    [Fact]
    public async Task CreateAsync_RelevanceBelowThreshold_ReturnsRelevanceMessageAndNeverPersists()
    {
        _relevance
            .Setup(r => r.EvaluateAsync("Verus LLC", It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevanceResult(0.1, "no overlap"));

        var result = await _sut.CreateAsync(new CreateCompanyCommand("Verus LLC", "https://www.google.com"));

        result.Status.Should().Be(ResultStatus.Invalid);
        result.Message.Should().Be("Company name 'Verus LLC' does not appear to be relevant to website 'https://www.google.com'.");
        result.Errors.Should().Contain("no overlap");
        _repository.Verify(r => r.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()), Times.Never);
        VerifyLogged(LogLevel.Warning);
    }

    [Fact]
    public async Task CreateAsync_RelevanceExactlyAtThreshold_IsAccepted()
    {
        _relevance
            .Setup(r => r.EvaluateAsync(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevanceResult(0.5, "borderline"));

        var result = await _sut.CreateAsync(new CreateCompanyCommand("Verus", "https://verus.com"));

        result.IsSuccess.Should().BeTrue();
    }

    // ---------- Get by id ----------

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsCompany()
    {
        var company = NewCompany("Verus LLC", "https://www.verus.com");
        _repository.Setup(r => r.GetByIdAsync(company.Id, It.IsAny<CancellationToken>())).ReturnsAsync(company);

        var result = await _sut.GetByIdAsync(company.Id);

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value.Should().Be(company);
    }

    [Fact]
    public async Task GetByIdAsync_MissingId_ReturnsNotFoundWithoutThrowing()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Company?)null);

        var act = () => _sut.GetByIdAsync(Guid.NewGuid());

        var result = (await act.Should().NotThrowAsync()).Subject;
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Value.Should().BeNull();
        result.Message.Should().Contain("was not found");
    }

    // ---------- Search ----------

    private static readonly Company Verus = NewCompany("Verus LLC", "https://www.verus.com");
    private static readonly Company Acme = NewCompany("Acme Tools Inc.", "https://acmetools.com");
    private static readonly Company Severus = NewCompany("Severus Labs", "https://severuslabs.io");

    private void SeedRepository(params Company[] companies) =>
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(companies);

    [Theory]
    [InlineData("ver")]
    [InlineData("VERUS")]
    [InlineData("Vérus")]
    public async Task SearchAsync_PartialNameCaseInsensitive_ReturnsMatchesOnly(string name)
    {
        SeedRepository(Verus, Acme, Severus);

        var result = await _sut.SearchAsync(new CompanySearchQuery(Name: name));

        result.Items.Select(h => h.Company.Name).Should().BeEquivalentTo("Verus LLC", "Severus Labs");
        result.Items.Should().OnlyContain(h => h.Score == null);
    }

    [Theory]
    [InlineData("verus.com")]
    [InlineData("www.verus.com")]
    [InlineData("https://verus.com/about")]
    public async Task SearchAsync_DomainFilter_MatchesHostIgnoringWww(string domain)
    {
        SeedRepository(Verus, Acme, Severus);

        var result = await _sut.SearchAsync(new CompanySearchQuery(Domain: domain));

        result.Items.Should().ContainSingle().Which.Company.Should().Be(Verus);
    }

    [Fact]
    public async Task SearchAsync_RelevanceQuery_OrdersByScoreDescendingAndDropsLowScores()
    {
        SeedRepository(Acme, Severus, Verus);
        _relevance.Setup(r => r.ScoreQuery("verus", It.IsAny<string>(), It.IsAny<Uri>()))
            .Returns((string _, string name, Uri _) => name switch
            {
                "Verus LLC" => new RelevanceResult(1.0, "exact"),
                "Severus Labs" => new RelevanceResult(0.7, "partial"),
                _ => new RelevanceResult(0.1, "none"),
            });

        var result = await _sut.SearchAsync(new CompanySearchQuery(Q: "verus"));

        result.Items.Select(h => h.Company.Name).Should().Equal("Verus LLC", "Severus Labs");
        result.Items.Select(h => h.Score).Should().BeInDescendingOrder();
        result.Items[0].Explanation.Should().Be("exact");
    }

    [Fact]
    public async Task SearchAsync_NoMatches_ReturnsEmptyListNotNull()
    {
        SeedRepository(Verus, Acme);

        var result = await _sut.SearchAsync(new CompanySearchQuery(Name: "zzz"));

        result.Items.Should().NotBeNull().And.BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_EmptyRepository_ReturnsEmptyList()
    {
        SeedRepository();

        var result = await _sut.SearchAsync(new CompanySearchQuery());

        result.Items.Should().BeEmpty();
        VerifyLogged(LogLevel.Information);
    }

    [Fact]
    public async Task SearchAsync_WithPageSize_ReturnsRequestedPageAndTotal()
    {
        SeedRepository(Verus, Acme, Severus);

        var result = await _sut.SearchAsync(new CompanySearchQuery(Page: 2, PageSize: 2));

        result.TotalCount.Should().Be(3);
        result.Items.Should().ContainSingle().Which.Company.Should().Be(Verus); // sorted by name: Acme, Severus, Verus
    }

    private static Company NewCompany(string name, string website) =>
        new(Guid.NewGuid(), name, website, TextNormalizer.NormalizeHost(new Uri(website).Host), DateTimeOffset.UtcNow);

    private void VerifyLogged(LogLevel level) =>
        _logger.Verify(l => l.Log(level, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
}
