using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CompanyApi.Api.Auth;
using CompanyApi.Api.Contracts;
using CompanyApi.IntegrationTests.Infrastructure;

namespace CompanyApi.IntegrationTests;

public sealed class AuthApiTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public AuthApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> LoginAsync(string? username, string? password) =>
        _client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Username = username, Password = password });

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithBearerToken()
    {
        var response = await LoginAsync(ApiFactory.Username, ApiFactory.Password);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.TokenType.Should().Be("Bearer");
        body.Username.Should().Be(ApiFactory.Username);
        body.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(ApiFactory.Username, "wrong-password")]
    [InlineData("nobody", ApiFactory.Password)]
    public async Task Login_InvalidCredentials_Returns401WithGenericMessage(string username, string password)
    {
        var response = await LoginAsync(username, password);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Message.Should().Be("Invalid username or password.");
    }

    [Fact]
    public async Task Login_MissingFields_Returns400()
    {
        var response = await LoginAsync("", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Errors
            .Should().Contain(["Username is required.", "Password is required."]);
    }

    [Fact]
    public async Task Login_TooManyAttempts_Returns429()
    {
        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
        {
            last = await LoginAsync(ApiFactory.Username, "wrong-password");
        }

        last!.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await last.Content.ReadFromJsonAsync<ErrorResponse>())!.Message.Should().Be(AuthSetup.TooManyAttemptsMessage);
    }

    [Theory]
    [InlineData("GET", "/api/companies")]
    [InlineData("GET", "/api/companies/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/companies")]
    [InlineData("GET", "/api/auth/me")]
    public async Task ProtectedEndpoint_WithoutToken_Returns401WithErrorResponse(string method, string url)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST") request.Content = JsonContent.Create(new { name = "MyCompany", website = "https://www.mycompany.com" });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Message.Should().Be(AuthSetup.AuthenticationRequiredMessage);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithTamperedToken_Returns401()
    {
        var token = (await (await LoginAsync(ApiFactory.Username, ApiFactory.Password)).Content.ReadFromJsonAsync<LoginResponse>())!.AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token[..^4] + "AAAA");

        var response = await _client.GetAsync("/api/companies");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginThenCallApi_WithIssuedToken_Succeeds()
    {
        var login = await (await LoginAsync(ApiFactory.Username, ApiFactory.Password)).Content.ReadFromJsonAsync<LoginResponse>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);

        var list = await _client.GetAsync("/api/companies");
        var me = await _client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        list.StatusCode.Should().Be(HttpStatusCode.OK);
        me!.Username.Should().Be(ApiFactory.Username);
    }
}
