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
        var result = await _sut.CreateAsync(new CreateCompanyCommand("  MyCompany ", " https://www.mycompany.com "));

        result.Status.Should().Be(ResultStatus.Ok);
        result.Value!.Id.Should().NotBe(Guid.Empty);
        result.Value.Name.Should().Be("MyCompany");
        result.Value.Website.Should().Be("https://www.mycompany.com");
        result.Value.Host.Should().Be("mycompany.com");
        _repository.Verify(r => r.AddAsync(
            It.Is<Company>(c => c.Id == result.Value.Id && c.Name == "MyCompany"), It.IsAny<CancellationToken>()), Times.Once);
        VerifyLogged(LogLevel.Information);
    }

    [Theory]
    [InlineData("", "https://www.mycompany.com", "Company name is required.")]
    [InlineData("V", "https://www.mycompany.com", "Company name must be at least 2 characters.")]
    [InlineData("MyCompany", "mycompany.com", CompanyInputValidator.Messages.WebsiteMalformed)]
    [InlineData("MyCompany", "ftp://mycompany.com", CompanyInputValidator.Messages.WebsiteScheme)]
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
            .Setup(r => r.EvaluateAsync("MyCompany", It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelevanceResult(0.1, "no overlap"));

        var result = await _sut.CreateAsync(new CreateCompanyCommand("MyCompany", "https://www.google.com"));

        result.Status.Should().Be(ResultStatus.Invalid);
        result.Message.Should().Be("Company name 'MyCompany' does not appear to be relevant to website 'https://www.google.com'.");
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

        var result = await _sut.CreateAsync(new CreateCompanyCommand("MyCompany", "https://mycompany.com"));

        result.IsSuccess.Should().BeTrue();
    }

    // ---------- Get by id ----------

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsCompany()
    {
        var company = NewCompany("MyCompany", "https://www.mycompany.com");
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

    private static readonly Company MyCompany = NewCompany("MyCompany", "https://www.mycompany.com");
    private static readonly Company Acme = NewCompany("Acme Tools Inc.", "https://acmetools.com");
    private static readonly Company Compass = NewCompany("Compass Labs", "https://compasslabs.io");

    private void SeedRepository(params Company[] companies) =>
        _repository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(companies);

    [Theory]
    [InlineData("comp")]
    [InlineData("COMP")]
    [InlineData("Cómp")]
    public async Task SearchAsync_PartialNameCaseInsensitive_ReturnsMatchesOnly(string name)
    {
        SeedRepository(MyCompany, Acme, Compass);

        var result = await _sut.SearchAsync(new CompanySearchQuery(Name: name));

        result.Items.Select(h => h.Company.Name).Should().BeEquivalentTo("MyCompany", "Compass Labs");
        result.Items.Should().OnlyContain(h => h.Score == null);
    }

    [Theory]
    [InlineData("mycompany.com")]
    [InlineData("www.mycompany.com")]
    [InlineData("https://mycompany.com/about")]
    public async Task SearchAsync_DomainFilter_MatchesHostIgnoringWww(string domain)
    {
        SeedRepository(MyCompany, Acme, Compass);

        var result = await _sut.SearchAsync(new CompanySearchQuery(Domain: domain));

        result.Items.Should().ContainSingle().Which.Company.Should().Be(MyCompany);
    }

    [Fact]
    public async Task SearchAsync_RelevanceQuery_OrdersByScoreDescendingAndDropsLowScores()
    {
        SeedRepository(Acme, Compass, MyCompany);
        _relevance.Setup(r => r.ScoreQuery("mycompany", It.IsAny<string>(), It.IsAny<Uri>()))
            .Returns((string _, string name, Uri _) => name switch
            {
                "MyCompany" => new RelevanceResult(1.0, "exact"),
                "Compass Labs" => new RelevanceResult(0.7, "partial"),
                _ => new RelevanceResult(0.1, "none"),
            });

        var result = await _sut.SearchAsync(new CompanySearchQuery(Q: "mycompany"));

        result.Items.Select(h => h.Company.Name).Should().Equal("MyCompany", "Compass Labs");
        result.Items.Select(h => h.Score).Should().BeInDescendingOrder();
        result.Items[0].Explanation.Should().Be("exact");
    }

    [Fact]
    public async Task SearchAsync_NoMatches_ReturnsEmptyListNotNull()
    {
        SeedRepository(MyCompany, Acme);

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
        SeedRepository(MyCompany, Acme, Compass);

        var result = await _sut.SearchAsync(new CompanySearchQuery(Page: 2, PageSize: 2));

        result.TotalCount.Should().Be(3);
        result.Items.Should().ContainSingle().Which.Company.Should().Be(MyCompany); // sorted by name: Acme, Compass, MyCompany
    }

    private static Company NewCompany(string name, string website) =>
        new(Guid.NewGuid(), name, website, TextNormalizer.NormalizeHost(new Uri(website).Host), DateTimeOffset.UtcNow);

    private void VerifyLogged(LogLevel level) =>
        _logger.Verify(l => l.Log(level, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
}
