using System.Net;
using System.Net.Http.Json;
using CompanyApi.Api.Contracts;
using CompanyApi.Api.Controllers;
using CompanyApi.IntegrationTests.Infrastructure;

namespace CompanyApi.IntegrationTests;

/// <summary>Default configuration: Auth:Enabled = false, so reviewers can use curl/Postman without a token.</summary>
public sealed class AuthDisabledTests : IDisposable
{
    private readonly ApiFactory _factory = new(settings: new Dictionary<string, string?> { ["Auth:Enabled"] = "false" });
    private readonly HttpClient _client;

    public AuthDisabledTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Config_AuthDisabled_ReportsDisabled()
    {
        var config = await _client.GetFromJsonAsync<AuthConfigResponse>("/api/auth/config");

        config!.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task CompanyEndpoints_WithoutToken_Work()
    {
        var created = await _client.CreateCompanyAsync("MyCompany", "https://www.mycompany.com");
        var id = (await created.Content.ReadFromJsonAsync<CompanyResponse>())!.Id;

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        (await _client.GetAsync("/api/companies")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.GetAsync($"/api/companies/{id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Validation_StillApplies_WhenAuthDisabled()
    {
        var response = await _client.CreateCompanyAsync("", "https://www.mycompany.com");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_AuthDisabled_Returns404WithMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = ApiFactory.Username, Password = ApiFactory.Password });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Message.Should().Be(AuthController.AuthDisabledMessage);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401EvenWhenAuthDisabled()
    {
        (await _client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void Startup_AuthDisabledWithoutSigningKey_Succeeds()
    {
        using var factory = new ApiFactory(settings: new Dictionary<string, string?>
        {
            ["Auth:Enabled"] = "false",
            ["Auth:SigningKey"] = "",
        });

        var act = () => factory.CreateClient();

        act.Should().NotThrow();
    }

    [Fact]
    public void Startup_AuthEnabledWithoutSigningKey_FailsWithClearMessage()
    {
        using var factory = new ApiFactory(settings: new Dictionary<string, string?> { ["Auth:SigningKey"] = "" });

        var act = () => factory.CreateClient();

        act.Should().Throw<Exception>().Where(e => e.ToString().Contains("Auth:SigningKey must be at least 32 characters"));
    }
}
