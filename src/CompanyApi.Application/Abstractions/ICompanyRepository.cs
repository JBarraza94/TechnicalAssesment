using CompanyApi.Application.Domain;

namespace CompanyApi.Application.Abstractions;

/// <summary>
/// Persistence boundary. Swapping the in-memory store for EF Core only requires a new implementation + DI registration.
/// </summary>
public interface ICompanyRepository
{
    Task AddAsync(Company company, CancellationToken cancellationToken = default);

    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns a snapshot; mutating it never affects the store.</summary>
    Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Replaces an existing company. Returns false when the id does not exist.</summary>
    Task<bool> UpdateAsync(Company company, CancellationToken cancellationToken = default);

    /// <summary>Removes a company. Returns false when the id does not exist.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
