using CompanyApi.Api.Auth;
using CompanyApi.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CompanyApi.Api.Controllers;

/// <summary>Sign-in and current-user endpoints.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    public const string InvalidCredentialsMessage = "Invalid username or password.";

    /// <summary>Exchanges a username and password for a bearer token.</summary>
    /// <response code="200">Signed in; use the returned access token as a Bearer token.</response>
    /// <response code="400">Username or password missing.</response>
    /// <response code="401">Wrong username or password.</response>
    /// <response code="429">Too many sign-in attempts; try again later.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthSetup.LoginRateLimitPolicy)]
    [Consumes("application/json")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status429TooManyRequests)]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var result = authService.Login(request.Username!, request.Password!);
        return result is null
            ? Unauthorized(ErrorResponse.From(InvalidCredentialsMessage))
            : Ok(result);
    }

    /// <summary>Returns the user the bearer token belongs to.</summary>
    /// <response code="200">The current user.</response>
    /// <response code="401">Missing, invalid or expired token.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
    public ActionResult<CurrentUserResponse> Me() =>
        new CurrentUserResponse(User.Identity?.Name ?? "unknown");
}
