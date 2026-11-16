using CompanyApi.Application.Abstractions;
using CompanyApi.Infrastructure.Content;
using CompanyApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyApi.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Configuration key that selects the persistence implementation.</summary>
    public const string PersistenceProviderKey = "Persistence:Provider";

    public static class PersistenceProviders
    {
        /// <summary>Default: a private ConcurrentDictionary. No database involved.</summary>
        public const string InMemory = "InMemory";

        /// <summary>EF Core with its InMemory provider: the same code path a real database would use.</summary>
        public const string EfInMemory = "EfInMemory";
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration[PersistenceProviderKey] ?? PersistenceProviders.InMemory;

        if (string.Equals(provider, PersistenceProviders.EfInMemory, StringComparison.OrdinalIgnoreCase))
        {
            // For a real database, replace UseInMemoryDatabase with UseSqlServer/UseNpgsql(connectionString)
            // and add migrations; nothing outside this block changes.
            // Each app instance gets its own InMemoryDatabaseRoot, so separate hosts (e.g. tests) never share data.
            services.AddSingleton<InMemoryDatabaseRoot>();
            services.AddDbContext<CompanyDbContext>((sp, options) =>
                options.UseInMemoryDatabase("Companies", sp.GetRequiredService<InMemoryDatabaseRoot>()));
            services.AddScoped<ICompanyRepository, EfCompanyRepository>();
        }
        else if (string.Equals(provider, PersistenceProviders.InMemory, StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ICompanyRepository, InMemoryCompanyRepository>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown {PersistenceProviderKey} '{provider}'. Use '{PersistenceProviders.InMemory}' or '{PersistenceProviders.EfInMemory}'.");
        }

        services.AddHttpClient<IWebsiteContentFetcher, HttpWebsiteContentFetcher>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("CompanyApi-RelevanceCheck/1.0");
        });

        return services;
    }
}
