using System.Net.Http.Json;
using CompanyApi.Api.Contracts;
using CompanyApi.Application.Abstractions;
using CompanyApi.Infrastructure.Persistence;
using CompanyApi.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyApi.IntegrationTests;

public sealed class PersistenceWiringTests
{
    [Fact]
    public void Container_ResolvesEfRepositoryPerScope()
    {
        using var factory = new ApiFactory();
        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();

        var repoA = scopeA.ServiceProvider.GetRequiredService<ICompanyRepository>();
        var repoB = scopeB.ServiceProvider.GetRequiredService<ICompanyRepository>();

        repoA.Should().BeOfType<EfCompanyRepository>();
        repoA.Should().NotBeSameAs(repoB, "the repository and its DbContext are scoped");
    }

    [Fact]
    public async Task DataCreatedInOneRequest_IsVisibleInTheNext()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateAuthenticatedClient();

        (await client.CreateCompanyAsync("MyCompany", "https://www.mycompany.com")).EnsureSuccessStatusCode();
        var list = await client.GetFromJsonAsync<List<CompanySearchResult>>("/api/companies");

        list.Should().ContainSingle().Which.Name.Should().Be("MyCompany");
    }

    [Fact]
    public async Task SeparateAppInstances_DoNotShareData()
    {
        using var first = new ApiFactory();
        using var second = new ApiFactory();
        using var firstClient = first.CreateAuthenticatedClient();
        using var secondClient = second.CreateAuthenticatedClient();

        (await firstClient.CreateCompanyAsync("MyCompany", "https://www.mycompany.com")).EnsureSuccessStatusCode();

        (await secondClient.GetFromJsonAsync<List<CompanySearchResult>>("/api/companies")).Should().BeEmpty();
    }
}
