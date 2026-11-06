using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SafeCore.DeviceManagement.Domain;
using SafeCore.IAM.Application;
using SafeCore.IdentityAccess.Domain;
using SafeCore.Shared.Contracts;
using System.Security.Cryptography;
using System.Text;

namespace SafeCore.IAM.API;

[ApiController]
[Route("api/iam/auth")]
[EnableRateLimiting("authentication")]
public sealed class AuthController(
    SignInManager<User> signInManager,
    UserManager<User> userManager,
    IRepository<Device> devices,
    ITokenService tokenService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || user.Status != UserStatuses.Active)
            return Unauthorized(new ProblemDetails { Title = "Invalid credentials." });

        var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(new ProblemDetails { Title = "Invalid credentials." });

        var roles = (await userManager.GetRolesAsync(user)).ToArray();
        return Ok(new LoginResponse(
            await tokenService.CreateUserTokenAsync(user),
            new AuthenticatedUserResponse(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName, roles)));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<AuthenticatedUserResponse>> GetCurrentUser()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userId, out var id))
            return Unauthorized();
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null || user.Status != UserStatuses.Active)
            return Unauthorized();
        return Ok(new AuthenticatedUserResponse(user.Id, user.Email ?? string.Empty, user.FirstName, user.LastName,
            (await userManager.GetRolesAsync(user)).ToArray()));
    }

    [AllowAnonymous]
    [HttpPost("device-token")]
    public async Task<ActionResult<TokenResponse>> CreateDeviceToken(DeviceTokenRequest request)
    {
        var device = await devices.GetByIdAsync(request.DeviceId);
        if (device is null || string.IsNullOrEmpty(device.CredentialHash))
            return Unauthorized(new ProblemDetails { Title = "Invalid device credentials." });

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.Secret));
        var expectedHash = Convert.FromHexString(device.CredentialHash);
        if (expectedHash.Length != providedHash.Length || !CryptographicOperations.FixedTimeEquals(providedHash, expectedHash))
            return Unauthorized(new ProblemDetails { Title = "Invalid device credentials." });

        return Ok(tokenService.CreateDeviceToken(device));
    }
}
