using System.ComponentModel.DataAnnotations;

namespace SafeCore.IAM.Application;

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(12), MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

public sealed class DeviceTokenRequest
{
    [Required]
    public Guid DeviceId { get; init; }

    [Required, MinLength(32), MaxLength(128)]
    public string Secret { get; init; } = string.Empty;
}

public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresInSeconds);
public sealed record LoginResponse(TokenResponse Token, AuthenticatedUserResponse User);
public sealed record AuthenticatedUserResponse(Guid Id, string Email, string FirstName, string LastName, IReadOnlyCollection<string> Roles);

public sealed record JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
}
