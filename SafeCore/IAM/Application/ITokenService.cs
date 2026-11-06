using SafeCore.IdentityAccess.Domain;
using SafeCore.DeviceManagement.Domain;

namespace SafeCore.IAM.Application;

public interface ITokenService
{
    Task<TokenResponse> CreateUserTokenAsync(User user, CancellationToken cancellationToken = default);
    TokenResponse CreateDeviceToken(Device device);
}
