using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeCore.IdentityAccess.Domain;

namespace SafeCore.IAM.Infrastructure;

public sealed class IdentitySeeder(
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<User> userManager,
    IConfiguration configuration,
    IHostEnvironment environment)
{
    private static readonly string[] Roles = ["Administrator", "Operator", "Responder", "Resident"];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                EnsureSucceeded(result, $"Could not create role {role}.");
            }
        }

        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            if (environment.IsProduction() && !await userManager.Users.AnyAsync(u => u.Status == UserStatuses.Active))
                throw new InvalidOperationException("Set BootstrapAdmin__Email and BootstrapAdmin__Password for the initial production deployment.");
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
            return;

        user = new User
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "System",
            LastName = "Administrator",
            Status = UserStatuses.Active
        };
        EnsureSucceeded(await userManager.CreateAsync(user, password), "Could not create the bootstrap administrator.");
        EnsureSucceeded(await userManager.AddToRoleAsync(user, "Administrator"), "Could not assign the administrator role.");
    }

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"{message} {string.Join("; ", result.Errors.Select(error => error.Description))}");
    }
}
