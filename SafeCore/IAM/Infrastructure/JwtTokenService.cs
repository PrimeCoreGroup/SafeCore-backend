using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SafeCore.DeviceManagement.Domain;
using SafeCore.IdentityAccess.Domain;
using SafeCore.IAM.Application;

namespace SafeCore.IAM.Infrastructure;

public sealed class JwtTokenService : ITokenService
{
    private readonly UserManager<User> _userManager;
    private readonly JwtOptions _options;

    public JwtTokenService(UserManager<User> userManager, IOptions<JwtOptions> options)
    {
        _userManager = userManager;
        _options = options.Value;
    }

    public async Task<TokenResponse> CreateUserTokenAsync(User user, CancellationToken cancellationToken = default)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty),
            new("security_stamp", user.SecurityStamp ?? string.Empty),
            new("must_change_password", user.MustChangePassword.ToString().ToLowerInvariant())
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return CreateToken(claims);
    }

    public TokenResponse CreateDeviceToken(Device device)
    {
        return CreateToken(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, $"device:{device.Id}"),
            new Claim("device_id", device.Id.ToString()),
            new Claim(ClaimTypes.Role, "Device")
        });
    }

    private TokenResponse CreateToken(IEnumerable<Claim> claims)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);
        var token = new JwtSecurityToken(_options.Issuer, _options.Audience, claims,
            notBefore: DateTime.UtcNow, expires: expires, signingCredentials: credentials);
        return new TokenResponse(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", _options.AccessTokenMinutes * 60);
    }
}
