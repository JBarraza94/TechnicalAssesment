using System.ComponentModel.DataAnnotations;

namespace CompanyApi.Api.Auth;

/// <summary>Bound from the "Auth" configuration section.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    [Required]
    public string Issuer { get; set; } = "CompanyApi";

    [Required]
    public string Audience { get; set; } = "CompanyApp";

    /// <summary>HMAC-SHA256 key (at least 32 characters). Supply it via user secrets or environment variables outside Development.</summary>
    [Required(ErrorMessage = "Auth:SigningKey is required. Set it with user secrets or the Auth__SigningKey environment variable.")]
    [MinLength(32, ErrorMessage = "Auth:SigningKey must be at least 32 characters.")]
    public string SigningKey { get; set; } = string.Empty;

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
