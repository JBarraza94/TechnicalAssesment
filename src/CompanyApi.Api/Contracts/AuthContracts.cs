using System.ComponentModel.DataAnnotations;

namespace CompanyApi.Api.Contracts;

/// <summary>Credentials for POST /api/auth/login.</summary>
public sealed record LoginRequest
{
    /// <example>admin</example>
    [Required(ErrorMessage = "Username is required.")]
    [MaxLength(100)]
    public string? Username { get; init; }

    /// <example>Admin123!</example>
    [Required(ErrorMessage = "Password is required.")]
    [MaxLength(200)]
    public string? Password { get; init; }
}

/// <summary>Issued access token. Send it as "Authorization: Bearer {accessToken}".</summary>
/// <param name="AccessToken">Signed JWT.</param>
/// <param name="TokenType">Always "Bearer".</param>
/// <param name="ExpiresAt">When the token stops being accepted (UTC).</param>
/// <param name="Username">The signed-in user.</param>
public sealed record LoginResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt, string Username);

/// <summary>Whether the API requires authentication.</summary>
/// <param name="Enabled">True when the company endpoints require a bearer token.</param>
public sealed record AuthConfigResponse(bool Enabled);

/// <summary>The current user.</summary>
/// <param name="Username">Name from the token.</param>
public sealed record CurrentUserResponse(string Username);
