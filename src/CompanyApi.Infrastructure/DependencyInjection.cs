using CompanyApi.Application.Abstractions;
using CompanyApi.Infrastructure.Content;
using CompanyApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // EF Core with the InMemory provider. For a real database, replace UseInMemoryDatabase with
        // UseSqlServer/UseNpgsql(connectionString) and add migrations; nothing else changes.
        // Each app instance gets its own InMemoryDatabaseRoot, so separate hosts (e.g. tests) never share data.
        services.AddSingleton<InMemoryDatabaseRoot>();
        services.AddDbContext<CompanyDbContext>((sp, options) =>
            options.UseInMemoryDatabase("Companies", sp.GetRequiredService<InMemoryDatabaseRoot>()));
        services.AddScoped<ICompanyRepository, EfCompanyRepository>();

        services.AddHttpClient<IWebsiteContentFetcher, HttpWebsiteContentFetcher>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CompanyApi-RelevanceCheck/1.0");
        });

        return services;
    }
}
