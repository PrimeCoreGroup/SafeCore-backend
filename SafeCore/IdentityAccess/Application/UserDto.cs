using System.ComponentModel.DataAnnotations;

namespace SafeCore.IdentityAccess.Application;

public sealed class CreateUserRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(12), MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required, RegularExpression("^(Administrator|Operator|Responder|Resident)$")]
    public string Role { get; init; } = string.Empty;
}

public sealed class UpdateUserRequest
{
    [Required, MaxLength(100)]
    public string FirstName { get; init; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; init; } = string.Empty;

    [Required, RegularExpression("^(Active|Disabled)$")]
    public string Status { get; init; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required, MinLength(12), MaxLength(128)] public string NewPassword { get; init; } = string.Empty;
}

public sealed record UserResponse(Guid Id, string Email, string FirstName, string LastName, string Status, IReadOnlyCollection<string> Roles, DateTime CreatedAt, bool MustChangePassword);
