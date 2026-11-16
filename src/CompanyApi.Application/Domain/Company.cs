namespace CompanyApi.Application.Domain;

/// <summary>
/// Internal company entity. Never serialized directly; the API maps it to DTOs.
/// </summary>
/// <param name="Id">Server-generated identifier.</param>
/// <param name="Name">Trimmed company name.</param>
/// <param name="Website">Website URL as supplied (trimmed).</param>
/// <param name="Host">Normalized host (lowercase, without "www."), used for domain filtering.</param>
/// <param name="CreatedAtUtc">Creation timestamp (internal bookkeeping).</param>
public sealed record Company(
    Guid Id,
    string Name,
    string Website,
    string Host,
    DateTimeOffset CreatedAtUtc);
