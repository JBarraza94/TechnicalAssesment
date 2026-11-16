using System.Net.Http.Headers;
using System.Net.Http.Json;
using CompanyApi.Api.Auth;
using CompanyApi.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyApi.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API in memory. Create one per test so the in-memory store starts empty.
/// Authentication is enabled unless <paramref name="settings"/> says otherwise, so the auth path is always exercised.
/// </summary>
public sealed class ApiFactory(
    Action<IServiceCollection>? configureServices = null,
    IReadOnlyDictionary<string, string?>? settings = null) : WebApplicationFactory<Program>
{
    public const string Username = "tester";
    public const string Password = "Correct-Horse-9";

    private static readonly string PasswordHash = PasswordHasher.Hash(Password, iterations: 1_000);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Make sure tests stay offline and deterministic regardless of local config.
        builder.UseSetting("Relevance:EnableContentInspection", "false");
        builder.UseSetting("Auth:SigningKey", "integration-tests-signing-key-0123456789abcdef");
        builder.UseSetting("Auth:Users:0:Username", Username);
        builder.UseSetting("Auth:Users:0:PasswordHash", PasswordHash);
        builder.UseSetting("Auth:Enabled", "true");
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(key, value);
        }
        if (configureServices is not null)
        {
            builder.ConfigureTestServices(configureServices);
        }
    }

    /// <summary>A client that already sends a valid bearer token.</summary>
    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        var token = Services.GetRequiredService<IAuthService>().CreateToken(Username).AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

public static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> CreateCompanyAsync(this HttpClient client, string? name, string? website) =>
        client.PostAsJsonAsync("/api/companies", new CreateCompanyRequest { Name = name, Website = website });
}
