using System.Net.Http.Json;
using CompanyApi.Api.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyApi.IntegrationTests.Infrastructure;

/// <summary>Boots the real API in memory. Create one per test so the in-memory store starts empty.</summary>
public sealed class ApiFactory(Action<IServiceCollection>? configureServices = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // Make sure tests stay offline and deterministic regardless of local config.
        builder.UseSetting("Relevance:EnableContentInspection", "false");
        if (configureServices is not null)
        {
            builder.ConfigureTestServices(configureServices);
        }
    }
}

public static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> CreateCompanyAsync(this HttpClient client, string? name, string? website) =>
        client.PostAsJsonAsync("/api/companies", new CreateCompanyRequest { Name = name, Website = website });
}
