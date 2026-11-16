using CompanyApi.Api.Contracts;
using CompanyApi.Application.Domain;
using CompanyApi.Application.Services;

namespace CompanyApi.Api.Mapping;

/// <summary>The only place where internal models are turned into API contracts.</summary>
public static class CompanyMappings
{
    public static CompanyResponse ToResponse(this Company company) =>
        new(company.Id, company.Name, company.Website);

    public static CompanySearchResult ToSearchResult(this CompanySearchHit hit) =>
        new(hit.Company.Id, hit.Company.Name, hit.Company.Website, hit.Score, hit.Explanation);

    public static CreateCompanyCommand ToCommand(this CreateCompanyRequest request) =>
        new(request.Name, request.Website);

    public static CompanySearchQuery ToQuery(this CompanySearchRequest request) =>
        new(request.Name, request.Domain, request.Q, request.Page, request.PageSize);
}
