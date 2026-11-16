using System.IdentityModel.Tokens.Jwt;
using CompanyApi.Api.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CompanyApi.UnitTests.Auth;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = PasswordHasher.Hash("Admin123!", iterations: 1_000);

        PasswordHasher.Verify("Admin123!", hash).Should().BeTrue();
    }

    [Fact]
    public void Hash_SamePasswordTwice_UsesDifferentSalts()
    {
        PasswordHasher.Hash("Admin123!", 1_000).Should().NotBe(PasswordHasher.Hash("Admin123!", 1_000));
    }

    [Theory]
    [InlineData("admin123!")]
    [InlineData("")]
    [InlineData("Admin123! ")]
    public void Verify_WrongPassword_ReturnsFalse(string attempt)
    {
        var hash = PasswordHasher.Hash("Admin123!", iterations: 1_000);

        PasswordHasher.Verify(attempt, hash).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-text-password")]
    [InlineData("PBKDF2-SHA256$abc$salt$hash")]
    [InlineData("PBKDF2-SHA256$1000$not-base64!$not-base64!")]
    [InlineData("MD5$1000$c2FsdA==$aGFzaA==")]
    public void Verify_MalformedHash_ReturnsFalseWithoutThrowing(string encoded)
    {
        PasswordHasher.Verify("anything", encoded).Should().BeFalse();
    }
}

public sealed class AuthServiceTests
{
    private const string SigningKey = "unit-test-signing-key-0123456789abcdefghij";

    private static AuthService CreateSut() => new(
        Options.Create(new AuthOptions
        {
            SigningKey = SigningKey,
            TokenLifetimeMinutes = 30,
            Users = [new AuthUser { Username = "admin", PasswordHash = PasswordHasher.Hash("Admin123!", 1_000) }],
        }),
        TimeProvider.System,
        NullLogger<AuthService>.Instance);

    [Theory]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("  admin ")]
    public void Login_ValidCredentials_ReturnsSignedTokenForUser(string username)
    {
        var result = CreateSut().Login(username, "Admin123!");

        result.Should().NotBeNull();
        result!.TokenType.Should().Be("Bearer");
        result.Username.Should().Be("admin");
        result.ExpiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(30), TimeSpan.FromSeconds(10));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        jwt.Subject.Should().Be("admin");
        jwt.Issuer.Should().Be("CompanyApi");
        jwt.Audiences.Should().ContainSingle("CompanyApp");
        jwt.SignatureAlgorithm.Should().Be("HS256");
    }

    [Theory]
    [InlineData("admin", "wrong")]
    [InlineData("admin", "")]
    [InlineData("ghost", "Admin123!")]
    public void Login_InvalidCredentials_ReturnsNull(string username, string password)
    {
        CreateSut().Login(username, password).Should().BeNull();
    }
}
