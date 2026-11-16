using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using CompanyApi.Api.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CompanyApi.Api.Auth;

public static class AuthSetup
{
    public const string LoginRateLimitPolicy = "login";
    public const string AuthenticatedUserPolicy = "authenticated-user";
    public const string AuthenticationRequiredMessage = "Authentication is required. Sign in and send the token as 'Authorization: Bearer {token}'.";
    public const string TooManyAttemptsMessage = "Too many sign-in attempts. Please wait a minute and try again.";

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => !o.Enabled || o.HasValidSigningKey,
                $"Auth:SigningKey must be at least {AuthOptions.MinSigningKeyLength} characters when Auth:Enabled is true. " +
                "Set it with user secrets or the Auth__SigningKey environment variable.")
            .ValidateOnStart();

        services.AddSingleton<IAuthService, AuthService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Configured from AuthOptions at runtime so tests and environments can override the key/issuer.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((jwt, auth) =>
            {
                var o = auth.Value;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = o.Issuer,
                    ValidateAudience = true,
                    ValidAudience = o.Audience,
                    ValidateIssuerSigningKey = true,
                    // With auth disabled there may be no key; a random one keeps the handler valid (no token can match it).
                    IssuerSigningKey = o.HasValidSigningKey
                        ? AuthService.CreateSigningKey(o.SigningKey)
                        : new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.UniqueName,
                };
                jwt.Events = new JwtBearerEvents
                {
                    // Uniform ErrorResponse body instead of an empty 401.
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(ErrorResponse.From(AuthenticationRequiredMessage));
                    },
                };
            });

        // Endpoints that are about "who am I" always need a real user, whatever Auth:Enabled says.
        services.AddAuthorization(o => o.AddPolicy(AuthenticatedUserPolicy, p => p.RequireAuthenticatedUser()));

        // Auth:Enabled = false: [Authorize] endpoints accept anonymous callers (the attributes stay in place,
        // so turning authentication on is purely a configuration change).
        services.AddOptions<AuthorizationOptions>()
            .Configure<IOptions<AuthOptions>>((authorization, auth) =>
            {
                if (!auth.Value.Enabled)
                {
                    authorization.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build();
                }
            });

        services.AddRateLimiter(o =>
        {
            o.AddPolicy(LoginRateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            o.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(ErrorResponse.From(TooManyAttemptsMessage), ct);
            };
        });

        return services;
    }
}
