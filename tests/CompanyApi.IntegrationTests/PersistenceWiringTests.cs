using System.Net.Http.Json;
using CompanyApi.Api.Contracts;
using CompanyApi.Application.Abstractions;
using CompanyApi.Infrastructure.Persistence;
using CompanyApi.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using static CompanyApi.Infrastructure.DependencyInjection;

namespace CompanyApi.IntegrationTests;

public sealed class PersistenceWiringTests
{
    private static ApiFactory CreateFactory(string? provider) => new(settings: provider is null
        ? null
        : new Dictionary<string, string?> { [PersistenceProviderKey] = provider });

    [Fact]
    public void Default_UsesDictionaryRepositoryAsSingleton()
    {
        using var factory = CreateFactory(provider: null);
        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();

        var repoA = scopeA.ServiceProvider.GetRequiredService<ICompanyRepository>();

        repoA.Should().BeOfType<InMemoryCompanyRepository>();
        repoA.Should().BeSameAs(scopeB.ServiceProvider.GetRequiredService<ICompanyRepository>());
    }

    [Fact]
    public void EfInMemoryProvider_UsesEfRepositoryPerScope()
    {
        using var factory = CreateFactory(PersistenceProviders.EfInMemory);
        using var scopeA = factory.Services.CreateScope();
        using var scopeB = factory.Services.CreateScope();

        var repoA = scopeA.ServiceProvider.GetRequiredService<ICompanyRepository>();

        repoA.Should().BeOfType<EfCompanyRepository>();
        repoA.Should().NotBeSameAs(scopeB.ServiceProvider.GetRequiredService<ICompanyRepository>(),
            "the repository and its DbContext are scoped");
    }

    [Fact]
    public void UnknownProvider_FailsAtStartupWithClearMessage()
    {
        using var factory = CreateFactory("Oracle");

        var act = () => factory.CreateClient();

        act.Should().Throw<InvalidOperationException>().WithMessage("*Unknown Persistence:Provider 'Oracle'*");
    }

    [Theory]
    [InlineData(PersistenceProviders.InMemory)]
    [InlineData(PersistenceProviders.EfInMemory)]
    public async Task DataCreatedInOneRequest_IsVisibleInTheNext(string provider)
    {
        using var factory = CreateFactory(provider);
        using var client = factory.CreateAuthenticatedClient();

        (await client.CreateCompanyAsync("MyCompany", "https://www.mycompany.com")).EnsureSuccessStatusCode();
        var list = await client.GetFromJsonAsync<List<CompanySearchResult>>("/api/companies");

        list.Should().ContainSingle().Which.Name.Should().Be("MyCompany");
    }

    [Theory]
    [InlineData(PersistenceProviders.InMemory)]
    [InlineData(PersistenceProviders.EfInMemory)]
    public async Task SeparateAppInstances_DoNotShareData(string provider)
    {
        using var first = CreateFactory(provider);
        using var second = CreateFactory(provider);
        using var firstClient = first.CreateAuthenticatedClient();
        using var secondClient = second.CreateAuthenticatedClient();

        (await firstClient.CreateCompanyAsync("MyCompany", "https://www.mycompany.com")).EnsureSuccessStatusCode();

        (await secondClient.GetFromJsonAsync<List<CompanySearchResult>>("/api/companies")).Should().BeEmpty();
    }
}
