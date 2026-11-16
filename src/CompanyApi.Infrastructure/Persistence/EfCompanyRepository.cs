using CompanyApi.Application.Abstractions;
using CompanyApi.Application.Domain;
using Microsoft.EntityFrameworkCore;

namespace CompanyApi.Infrastructure.Persistence;

/// <summary>
/// <see cref="ICompanyRepository"/> backed by EF Core. Register as scoped (one DbContext per request).
/// Reads use AsNoTracking, so returned records are detached snapshots.
/// </summary>
public sealed class EfCompanyRepository(CompanyDbContext db) : ICompanyRepository
{
    public async Task AddAsync(Company company, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(company);

        if (await db.Companies.AnyAsync(c => c.Id == company.Id, cancellationToken))
        {
            throw new InvalidOperationException($"A company with id '{company.Id}' already exists.");
        }

        db.Companies.Add(company);
        await db.SaveChangesAsync(cancellationToken);
        db.Entry(company).State = EntityState.Detached;
    }

    public Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Companies.AsNoTracking().ToListAsync(cancellationToken);

    public async Task<bool> UpdateAsync(Company company, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(company);

        var existing = await db.Companies.FirstOrDefaultAsync(c => c.Id == company.Id, cancellationToken);
        if (existing is null) return false;

        // Company is an immutable record: copy the new values onto the tracked instance.
        db.Entry(existing).CurrentValues.SetValues(company);
        await db.SaveChangesAsync(cancellationToken);
        db.Entry(existing).State = EntityState.Detached;
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var existing = await db.Companies.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (existing is null) return false;

        db.Companies.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
