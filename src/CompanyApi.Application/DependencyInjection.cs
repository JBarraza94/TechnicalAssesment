using CompanyApi.Application.Abstractions;
using CompanyApi.Application.Relevance;
using CompanyApi.Application.Services;
using CompanyApi.Application.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyApi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ICompanyInputValidator, CompanyInputValidator>();
        services.AddScoped<IRelevanceStrategy, CompositeRelevanceStrategy>();
        services.AddScoped<ICompanyService, CompanyService>();
        return services;
    }
}
