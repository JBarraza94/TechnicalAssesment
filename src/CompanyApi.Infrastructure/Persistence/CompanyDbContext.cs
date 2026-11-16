using CompanyApi.Application.Domain;
using CompanyApi.Application.Validation;
using Microsoft.EntityFrameworkCore;

namespace CompanyApi.Infrastructure.Persistence;

/// <summary>
/// EF Core context for companies. Uses the InMemory provider today; switching to a real database
/// only means changing the provider in <see cref="DependencyInjection"/> and adding migrations.
/// </summary>
public sealed class CompanyDbContext(DbContextOptions<CompanyDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(entity =>
        {
            // With a relational provider, add: entity.ToTable("Companies");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedNever(); // the service generates the Guid
            entity.Property(c => c.Name).HasMaxLength(CompanyInputValidator.NameMaxLength).IsRequired();
            entity.Property(c => c.Website).HasMaxLength(CompanyInputValidator.WebsiteMaxLength).IsRequired();
            entity.Property(c => c.Host).HasMaxLength(253).IsRequired();
            entity.Property(c => c.CreatedAtUtc).IsRequired();
            entity.HasIndex(c => c.Host);
            entity.HasIndex(c => c.Name);
        });
    }
}
