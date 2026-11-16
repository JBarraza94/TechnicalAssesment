using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CompanyApi.Api.Contracts;
using CompanyApi.Application.Abstractions;
using CompanyApi.Application.Domain;
using CompanyApi.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CompanyApi.IntegrationTests;

public sealed class CompaniesApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public CompaniesApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // ---------- POST ----------

    [Fact]
    public async Task Post_ValidCompany_Returns201WithLocationAndBody()
    {
        var response = await _client.CreateCompanyAsync("Verus LLC", "https://www.verus.com");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CompanyResponse>();
        body!.Id.Should().NotBe(Guid.Empty);
        body.Name.Should().Be("Verus LLC");
        body.Website.Should().Be("https://www.verus.com");
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.AbsolutePath.Should().BeEquivalentTo($"/api/companies/{body.Id}");

        var fetched = await _client.GetAsync(response.Headers.Location);
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Post_EmptyName_Returns400MentioningName(string? name)
    {
        var response = await _client.CreateCompanyAsync(name, "https://www.verus.com");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Message.Should().Contain("name");
        error.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Post_OneCharacterName_Returns400WithMinLengthMessage()
    {
        var response = await _client.CreateCompanyAsync("V", "https://www.verus.com");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Message
            .Should().Be("Company name must be at least 2 characters.");
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("verus.com")]
    [InlineData("ftp://verus.com")]
    [InlineData("https://localhost")]
    public async Task Post_InvalidUrl_Returns400MentioningUrl(string website)
    {
        var response = await _client.CreateCompanyAsync("Verus LLC", website);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Message.Should().Contain("Website URL");
    }

    [Fact]
    public async Task Post_IrrelevantNameAndUrl_Returns400WithRelevanceMessage()
    {
        var response = await _client.CreateCompanyAsync("Verus LLC", "https://www.google.com");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error!.Message.Should().Be("Company name 'Verus LLC' does not appear to be relevant to website 'https://www.google.com'.");
        error.Errors.Should().Contain(e => e.Contains("token overlap"));

        var list = await _client.GetFromJsonAsync<List<CompanySearchResult>>("/api/companies");
        list.Should().BeEmpty("nothing should be persisted");
    }

    [Theory]
    [InlineData("{ \"name\": \"Verus\", ")]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("[]")]
    public async Task Post_MalformedOrEmptyBody_Returns400WithFriendlyMessage(string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/companies", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Message
            .Should().Be("The request body is empty or is not valid JSON.");
    }

    // ---------- GET list ----------

    [Fact]
    public async Task GetAll_EmptyStore_Returns200EmptyArray()
    {
        var response = await _client.GetAsync("/api/companies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("[]");
    }

    [Fact]
    public async Task GetAll_AfterCreatingTwo_Returns200WithTwoItems()
    {
        await SeedAsync();

        var list = await _client.GetFromJsonAsync<List<CompanySearchResult>>("/api/companies");

        list.Should().HaveCount(2);
        list!.Should().OnlyContain(c => c.Score == null);
    }

    [Fact]
    public async Task GetAll_NameFilter_ReturnsOnlyMatches()
    {
        await SeedAsync();

        var list = await _client.GetFromJsonAsync<List<CompanySearchResult>>("/api/companies?name=ver");

        list.Should().ContainSingle().Which.Name.Should().Be("Verus LLC");
    }

    [Fact]
    public async Task GetAll_DomainFilter_ReturnsOnlyMatches()
    {
        await SeedAsync();

        var list = await _client.GetFromJsonAsync<List<CompanySearchResult>>("/api/companies?domain=verus.com");

        list.Should().ContainSingle().Which.Website.Should().Be("https://www.verus.com");
    }

    [Fact]
    public async Task GetAll_RelevanceQuery_ReturnsResultsOrderedByScore()
    {
        await SeedAsync();
        (await _client.CreateCompanyAsync("Acme", "https://acme.com")).EnsureSuccessStatusCode();

        var list = await _client.GetFromJsonAsync<List<CompanySearchResult>>("/api/companies?q=acme");

        list.Should().NotBeEmpty();
        list!.Select(c => c.Score).Should().OnlyContain(s => s != null).And.BeInDescendingOrder();
        list[0].Name.Should().Be("Acme");
        list.Should().NotContain(c => c.Name == "Verus LLC");
    }

    [Theory]
    [InlineData("?name=zzz")]
    [InlineData("?domain=nothing.org")]
    [InlineData("?q=zzzzqqqq")]
    public async Task GetAll_FilterWithNoMatches_Returns200EmptyArray(string query)
    {
        await SeedAsync();

        var response = await _client.GetAsync($"/api/companies{query}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("[]");
    }

    [Fact]
    public async Task GetAll_Paginated_ReturnsPageAndTotalCountHeader()
    {
        await SeedAsync();

        var response = await _client.GetAsync("/api/companies?page=1&pageSize=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<CompanySearchResult>>()).Should().ContainSingle();
        response.Headers.GetValues("X-Total-Count").Should().ContainSingle().Which.Should().Be("2");
    }

    [Theory]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=1000")]
    [InlineData("?page=0&pageSize=10")]
    [InlineData("?page=abc")]
    public async Task GetAll_InvalidQuery_Returns400(string query)
    {
        var response = await _client.GetAsync($"/api/companies{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Message.Should().NotBeNullOrWhiteSpace();
    }

    // ---------- GET by id ----------

    [Fact]
    public async Task GetById_Existing_Returns200()
    {
        var created = await (await _client.CreateCompanyAsync("Verus LLC", "https://www.verus.com"))
            .Content.ReadFromJsonAsync<CompanyResponse>();

        var response = await _client.GetAsync($"/api/companies/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<CompanyResponse>()).Should().Be(created);
    }

    [Fact]
    public async Task GetById_RandomGuid_Returns404WithMessage()
    {
        var response = await _client.GetAsync($"/api/companies/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Message.Should().Contain("was not found");
    }

    [Fact]
    public async Task GetById_NotAGuid_Returns400()
    {
        var response = await _client.GetAsync("/api/companies/not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Message.Should().Contain("not a valid company id");
    }

    // ---------- Contract hygiene ----------

    [Fact]
    public async Task Responses_NeverContainInternalOnlyFields()
    {
        var post = await _client.CreateCompanyAsync("Verus LLC", "https://www.verus.com");
        var id = (await post.Content.ReadFromJsonAsync<CompanyResponse>())!.Id;

        var bodies = new[]
        {
            await post.Content.ReadAsStringAsync(),
            await _client.GetStringAsync($"/api/companies/{id}"),
            await _client.GetStringAsync("/api/companies"),
        };

        foreach (var json in bodies)
        {
            using var doc = JsonDocument.Parse(json);
            var obj = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement[0] : doc.RootElement;
            obj.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "name", "website");
        }
    }

    [Fact]
    public async Task UnexpectedException_Returns500WithGenericMessageAndNoStackTrace()
    {
        using var factory = new ApiFactory(services =>
        {
            services.RemoveAll<ICompanyRepository>();
            services.AddSingleton<ICompanyRepository, ThrowingRepository>();
        });
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/companies");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("ThrowingRepository").And.NotContain("   at ").And.NotContain("secret");
        JsonSerializer.Deserialize<ErrorResponse>(raw, JsonSerializerOptions.Web)!.Message
            .Should().Be("An unexpected error occurred. Please try again later.");
    }

    private async Task SeedAsync()
    {
        (await _client.CreateCompanyAsync("Verus LLC", "https://www.verus.com")).EnsureSuccessStatusCode();
        (await _client.CreateCompanyAsync("Acme Tools Inc.", "https://acmetools.com")).EnsureSuccessStatusCode();
    }

    private sealed class ThrowingRepository : ICompanyRepository
    {
        private static Exception Boom() => new InvalidOperationException("secret connection string leaked");

        public Task AddAsync(Company company, CancellationToken cancellationToken = default) => throw Boom();
        public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw Boom();
        public Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default) => throw Boom();
        public Task<bool> UpdateAsync(Company company, CancellationToken cancellationToken = default) => throw Boom();
        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw Boom();
    }
}
