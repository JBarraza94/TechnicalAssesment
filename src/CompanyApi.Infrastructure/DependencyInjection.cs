using CompanyApi.Application.Abstractions;
using CompanyApi.Infrastructure.Content;
using CompanyApi.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Swap this single line for an EF Core repository to use a real database.
        services.AddSingleton<ICompanyRepository, InMemoryCompanyRepository>();

        services.AddHttpClient<IWebsiteContentFetcher, HttpWebsiteContentFetcher>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CompanyApi-RelevanceCheck/1.0");
        });

        return services;
    }
}
