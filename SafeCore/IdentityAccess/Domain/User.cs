using Microsoft.AspNetCore.Identity;

namespace SafeCore.IdentityAccess.Domain;

public sealed class User : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Status { get; set; } = UserStatuses.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool MustChangePassword { get; set; }
}

public static class UserStatuses
{
    public const string Active = "Active";
    public const string Disabled = "Disabled";
}
