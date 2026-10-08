using SafeCore.Shared.Kernel;

namespace SafeCore.IdentityAccess.Domain;

public class User : Entity
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}