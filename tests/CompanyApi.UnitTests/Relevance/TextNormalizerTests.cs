using CompanyApi.Application.Relevance;

namespace CompanyApi.UnitTests.Relevance;

public sealed class TextNormalizerTests
{
    [Theory]
    [InlineData("Verus LLC", new[] { "verus" })]
    [InlineData("Acme Tools, Inc.", new[] { "acme", "tools" })]
    [InlineData("Café Niño S.A. de C.V.", new[] { "cafe", "nino" })]
    [InlineData("Müller GmbH", new[] { "muller" })]
    [InlineData("AT&T", new[] { "att" })]
    [InlineData("Co", new[] { "co" })]
    public void NameTokens_VariousNames_NormalizesAndStripsLegalSuffixes(string name, string[] expected)
    {
        TextNormalizer.NameTokens(name).Should().Equal(expected);
    }

    [Theory]
    [InlineData("www.verus.com", "verus")]
    [InlineData("acmetools.com", "acmetools")]
    [InlineData("shop.acme-tools.co.uk", "acme-tools")]
    [InlineData("WWW.CafeNino.com.mx", "cafenino")]
    [InlineData("localhost", "localhost")]
    public void DomainLabel_VariousHosts_ReturnsSecondLevelLabel(string host, string expected)
    {
        TextNormalizer.DomainLabel(host).Should().Be(expected);
    }

    [Fact]
    public void JaroWinkler_IdenticalAndDisjointStrings_ReturnsBounds()
    {
        StringSimilarity.JaroWinkler("verus", "verus").Should().Be(1.0);
        StringSimilarity.JaroWinkler("verus", "google").Should().Be(0.0);
        StringSimilarity.JaroWinkler("microsft", "microsoft").Should().BeGreaterThan(0.9);
    }
}
