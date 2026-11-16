using System.Collections.Concurrent;
using CompanyApi.Application.Abstractions;
using CompanyApi.Application.Domain;

namespace CompanyApi.Infrastructure.Persistence;

/// <summary>Thread-safe in-memory store. Register as a singleton; data is lost on restart.</summary>
public sealed class InMemoryCompanyRepository : ICompanyRepository
{
    private readonly ConcurrentDictionary<Guid, Company> _companies = new();

    public Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(company);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_companies.TryAdd(company.Id, company))
        {
            throw new InvalidOperationException($"A company with id '{company.Id}' already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_companies.TryGetValue(id, out var company) ? company : null);
    }

    public Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Snapshot copy of immutable records: callers cannot change what is stored.
        IReadOnlyList<Company> snapshot = _companies.Values.ToArray();
        return Task.FromResult(snapshot);
    }

    public Task<bool> UpdateAsync(Company company, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(company);
        cancellationToken.ThrowIfCancellationRequested();

        while (_companies.TryGetValue(company.Id, out var existing))
        {
            if (_companies.TryUpdate(company.Id, company, existing)) return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_companies.TryRemove(id, out _));
    }
}
