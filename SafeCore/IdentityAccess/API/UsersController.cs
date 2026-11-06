using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeCore.IdentityAccess.Application;
using SafeCore.IdentityAccess.Domain;
using SafeCore.Notifications.Domain;
using SafeCore.Shared.Contracts;

namespace SafeCore.IdentityAccess.API;

[ApiController]
[Authorize(Roles = "Administrator")]
[Route("api/users")]
public sealed class UsersController(UserManager<User> userManager, IRepository<Notification> notifications) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserResponse>>> GetAll(int page = 1, int pageSize = 100)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 250);
        var users = await userManager.Users.OrderBy(user => user.Email)
            .Skip((int)Math.Min((long)(safePage - 1) * safePageSize, int.MaxValue))
            .Take(safePageSize)
            .ToListAsync();
        var results = new List<UserResponse>(users.Count);
        foreach (var user in users)
            results.Add(await ToResponseAsync(user));
        return Ok(results);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        return user is null ? NotFound() : Ok(await ToResponseAsync(user));
    }

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Status = UserStatuses.Active,
            EmailConfirmed = true
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            return BadRequest(new { errors = createResult.Errors.Select(error => error.Description) });

        var roleResult = await userManager.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(user);
            return BadRequest(new { errors = roleResult.Errors.Select(error => error.Description) });
        }

        var response = await ToResponseAsync(user);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest request)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound();

        if (user.Id.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier) && request.Status == UserStatuses.Disabled)
            return Conflict(new ProblemDetails { Title = "An administrator cannot disable their own account." });

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Status = request.Status;
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors.Select(error => error.Description) });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound();
        if (user.Id.ToString() == User.FindFirstValue(ClaimTypes.NameIdentifier))
            return Conflict(new ProblemDetails { Title = "An administrator cannot delete their own account." });

        if (await userManager.IsInRoleAsync(user, "Administrator"))
        {
            var otherAdministrators = await userManager.GetUsersInRoleAsync("Administrator");
            if (otherAdministrators.Count <= 1)
                return Conflict(new ProblemDetails { Title = "The last administrator account cannot be deleted." });
        }
        if (await notifications.AnyAsync(notification => notification.RecipientUserId == id))
            return Conflict(new ProblemDetails { Title = "A user with notification records cannot be deleted; disable the account instead." });

        var result = await userManager.DeleteAsync(user);
        return result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors.Select(error => error.Description) });
    }

    private async Task<UserResponse> ToResponseAsync(User user) => new(
        user.Id,
        user.Email ?? string.Empty,
        user.FirstName,
        user.LastName,
        user.Status,
        (await userManager.GetRolesAsync(user)).ToArray(),
        user.CreatedAt);
}
