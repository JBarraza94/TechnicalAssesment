using CompanyApi.Application.Validation;
using static CompanyApi.Application.Validation.CompanyInputValidator;

namespace CompanyApi.UnitTests.Validation;

public sealed class CompanyInputValidatorTests
{
    private readonly CompanyInputValidator _sut = new();

    [Theory]
    [InlineData("MyCompany", "https://www.mycompany.com")]
    [InlineData("Ab", "http://ab.io")]
    [InlineData("  Acme Tools Inc.  ", "https://acmetools.com/about?x=1")]
    public void Validate_ValidNameAndUrl_Passes(string name, string website)
    {
        var result = _sut.Validate(name, website);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.NormalizedName.Should().Be(name.Trim());
        result.WebsiteUri.Should().NotBeNull();
    }

    [Fact]
    public void Validate_NameAtMaxLength_Passes()
    {
        var result = _sut.Validate(new string('a', NameMaxLength), "https://www.example.com");

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Validate_EmptyOrWhitespaceName_FailsWithRequiredMessage(string? name)
    {
        var result = _sut.Validate(name, "https://www.example.com");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be(Messages.NameRequired);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("  A  ")]
    public void Validate_OneCharacterName_FailsWithMinLengthMessage(string name)
    {
        var result = _sut.Validate(name, "https://www.example.com");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be("Company name must be at least 2 characters.");
    }

    [Fact]
    public void Validate_NameOverMaxLength_FailsWithMaxLengthMessage()
    {
        var result = _sut.Validate(new string('a', NameMaxLength + 1), "https://www.example.com");

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Be(Messages.NameTooLong);
    }

    [Theory]
    [InlineData(null, Messages.WebsiteRequired)]
    [InlineData("", Messages.WebsiteRequired)]
    [InlineData("   ", Messages.WebsiteRequired)]
    [InlineData("not a url", Messages.WebsiteMalformed)]
    [InlineData("example.com", Messages.WebsiteMalformed)]
    [InlineData("www.example.com", Messages.WebsiteMalformed)]
    [InlineData("ftp://example.com", Messages.WebsiteScheme)]
    [InlineData("mailto:info@example.com", Messages.WebsiteScheme)]
    [InlineData("https://localhost", Messages.WebsiteHost)]
    [InlineData("http://example", Messages.WebsiteHost)]
    public void Validate_InvalidUrl_FailsWithSpecificMessage(string? website, string expectedMessage)
    {
        var result = _sut.Validate("Example", website);

        result.IsValid.Should().BeFalse();
        result.WebsiteUri.Should().BeNull();
        result.Errors.Should().ContainSingle().Which.Should().Be(expectedMessage);
    }

    [Fact]
    public void Validate_InvalidNameAndUrl_ReportsBothErrors()
    {
        var result = _sut.Validate("", "ftp://example.com");

        result.Errors.Should().BeEquivalentTo(Messages.NameRequired, Messages.WebsiteScheme);
    }
}
