using CompanyApi.Application.Abstractions;
using CompanyApi.Application.Domain;
using CompanyApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CompanyApi.UnitTests.Persistence;

/// <summary>
/// Behaviour every <see cref="ICompanyRepository"/> must have. Each implementation runs the same tests.
/// </summary>
public abstract class CompanyRepositoryContractTests
{
    private readonly ICompanyRepository _sut;

    protected CompanyRepositoryContractTests() => _sut = CreateRepository();

    /// <summary>A repository over this test's store. Every call must see the same data.</summary>
    protected abstract ICompanyRepository CreateRepository();

    private static Company NewCompany(string name = "MyCompany") =>
        new(Guid.NewGuid(), name, "https://www.mycompany.com", "mycompany.com", DateTimeOffset.UtcNow);

    [Fact]
    public async Task AddAsync_ThenGetById_ReturnsSameCompany()
    {
        var company = NewCompany();

        await _sut.AddAsync(company);
        var found = await _sut.GetByIdAsync(company.Id);

        found.Should().Be(company);
    }

    [Fact]
    public async Task GetAllAsync_AfterAdds_ReturnsEverything()
    {
        var companies = new[] { NewCompany("A1"), NewCompany("B2"), NewCompany("C3") };
        foreach (var c in companies) await _sut.AddAsync(c);

        var all = await _sut.GetAllAsync();

        all.Should().BeEquivalentTo(companies);
    }

    [Fact]
    public async Task AddAsync_ConcurrentAdds_LosesNone()
    {
        var companies = Enumerable.Range(0, 100).Select(i => NewCompany($"Company {i}")).ToList();

        // One repository per add: a DbContext is not thread-safe, but the store behind it must be.
        await Parallel.ForEachAsync(companies, async (c, ct) => await CreateRepository().AddAsync(c, ct));

        var all = await _sut.GetAllAsync();
        all.Should().HaveCount(100);
        all.Select(c => c.Id).Should().BeEquivalentTo(companies.Select(c => c.Id));
    }

    [Fact]
    public async Task AddAsync_DuplicateId_Throws()
    {
        var company = NewCompany();
        await _sut.AddAsync(company);

        var act = () => _sut.AddAsync(company with { Name = "Other" });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        await _sut.AddAsync(NewCompany());

        var found = await _sut.GetByIdAsync(Guid.NewGuid());

        found.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_EmptyRepository_ReturnsEmptyList()
    {
        var all = await _sut.GetAllAsync();

        all.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_MutatingReturnedCollection_DoesNotChangeStore()
    {
        var original = NewCompany();
        await _sut.AddAsync(original);

        var snapshot = await _sut.GetAllAsync();
        // Whatever concrete type comes back, writing through it must not reach the store.
        if (snapshot is IList<Company> { IsReadOnly: false } writable)
        {
            writable[0] = NewCompany("Hacked");
        }

        var later = await _sut.GetAllAsync();
        later.Should().ContainSingle().Which.Should().Be(original);
    }

    [Fact]
    public async Task GetAllAsync_SnapshotTakenBeforeAdd_IsNotAffectedByLaterAdds()
    {
        await _sut.AddAsync(NewCompany("First"));
        var snapshot = await _sut.GetAllAsync();

        await _sut.AddAsync(NewCompany("Second"));

        snapshot.Should().ContainSingle();
    }

    [Fact]
    public async Task UpdateAsync_ExistingAndMissing_ReturnsExpectedFlags()
    {
        var company = NewCompany();
        await _sut.AddAsync(company);

        (await _sut.UpdateAsync(company with { Name = "MyCompany Inc" })).Should().BeTrue();
        (await _sut.GetByIdAsync(company.Id))!.Name.Should().Be("MyCompany Inc");
        (await _sut.UpdateAsync(NewCompany())).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ExistingAndMissing_ReturnsExpectedFlags()
    {
        var company = NewCompany();
        await _sut.AddAsync(company);

        (await _sut.DeleteAsync(company.Id)).Should().BeTrue();
        (await _sut.GetByIdAsync(company.Id)).Should().BeNull();
        (await _sut.DeleteAsync(company.Id)).Should().BeFalse();
    }
}

public sealed class InMemoryCompanyRepositoryTests : CompanyRepositoryContractTests
{
    private InMemoryCompanyRepository? _repository;

    protected override ICompanyRepository CreateRepository() => _repository ??= new InMemoryCompanyRepository();
}

public sealed class EfCompanyRepositoryTests : CompanyRepositoryContractTests
{
    // Unique store per test instance; a new DbContext per repository, like one per HTTP request.
    private readonly InMemoryDatabaseRoot _root = new();

    protected override ICompanyRepository CreateRepository() =>
        new EfCompanyRepository(new CompanyDbContext(
            new DbContextOptionsBuilder<CompanyDbContext>().UseInMemoryDatabase("Companies", _root).Options));
}
