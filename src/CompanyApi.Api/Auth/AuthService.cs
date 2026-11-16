using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CompanyApi.Api.Contracts;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CompanyApi.Api.Auth;

public interface IAuthService
{
    /// <summary>Returns a token for valid credentials, or null when the username/password pair is wrong.</summary>
    LoginResponse? Login(string username, string password);

    /// <summary>Issues a signed JWT for <paramref name="username"/> (also used by tests).</summary>
    LoginResponse CreateToken(string username);
}

/// <summary>Validates credentials against the configured users and issues HMAC-signed JWTs.</summary>
public sealed class AuthService(IOptions<AuthOptions> options, TimeProvider timeProvider, ILogger<AuthService> logger) : IAuthService
{
    // Verified against when the user does not exist, so unknown users take as long as wrong passwords.
    private static readonly string DummyHash = PasswordHasher.Hash("not-a-real-password");

    private readonly AuthOptions _options = options.Value;

    public LoginResponse? Login(string username, string password)
    {
        var user = _options.Users.FirstOrDefault(u =>
            string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));

        var valid = PasswordHasher.Verify(password, user?.PasswordHash ?? DummyHash) && user is not null;
        if (!valid)
        {
            logger.LogWarning("Failed sign-in attempt for {Username}", username);
            return null;
        }

        logger.LogInformation("User {Username} signed in", user!.Username);
        return CreateToken(user.Username);
    }

    public LoginResponse CreateToken(string username)
    {
        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(_options.TokenLifetimeMinutes);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, username),
                new Claim(JwtRegisteredClaimNames.UniqueName, username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ],
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(CreateSigningKey(_options.SigningKey), SecurityAlgorithms.HmacSha256));

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", expires, username);
    }

    public static SymmetricSecurityKey CreateSigningKey(string key) => new(Encoding.UTF8.GetBytes(key));
}
