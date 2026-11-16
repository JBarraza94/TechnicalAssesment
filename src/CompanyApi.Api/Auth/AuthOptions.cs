using System.ComponentModel.DataAnnotations;

namespace CompanyApi.Api.Auth;

/// <summary>Bound from the "Auth" configuration section.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// When false (the default), the company endpoints are open so the API can be tried with curl/Postman
    /// right away. When true, they require a bearer token from POST /api/auth/login.
    /// </summary>
    public bool Enabled { get; set; }

    [Required]
    public string Issuer { get; set; } = "CompanyApi";

    [Required]
    public string Audience { get; set; } = "CompanyApp";

    /// <summary>
    /// HMAC-SHA256 key (at least 32 characters). Required only when <see cref="Enabled"/> is true; outside
    /// Development, supply it via user secrets or the Auth__SigningKey environment variable.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    public const int MinSigningKeyLength = 32;

    public bool HasValidSigningKey => SigningKey.Length >= MinSigningKeyLength;

    [Range(1, 24 * 60)]
    public int TokenLifetimeMinutes { get; set; } = 60;

    /// <summary>Users allowed to sign in. Passwords are stored as PBKDF2 hashes (see <see cref="PasswordHasher"/>).</summary>
    public List<AuthUser> Users { get; set; } = [];
}

public sealed class AuthUser
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}
