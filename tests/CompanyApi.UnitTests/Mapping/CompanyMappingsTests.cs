using CompanyApi.Api.Mapping;
using CompanyApi.Application.Domain;
using CompanyApi.Application.Services;

namespace CompanyApi.UnitTests.Mapping;

public sealed class CompanyMappingsTests
{
    private static readonly Company Company =
        new(Guid.NewGuid(), "Verus LLC", "https://www.verus.com", "verus.com", DateTimeOffset.UtcNow);

    [Fact]
    public void ToResponse_Company_MapsPublicFieldsOnly()
    {
        var response = Company.ToResponse();

        response.Id.Should().Be(Company.Id);
        response.Name.Should().Be("Verus LLC");
        response.Website.Should().Be("https://www.verus.com");
        response.GetType().GetProperties().Select(p => p.Name).Should().BeEquivalentTo("Id", "Name", "Website");
    }

    [Fact]
    public void ToSearchResult_HitWithoutScore_LeavesScoreNull()
    {
        var result = new CompanySearchHit(Company).ToSearchResult();

        result.Score.Should().BeNull();
        result.Explanation.Should().BeNull();
    }
}
