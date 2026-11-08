using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeCore.IdentityAccess.Application;
using SafeCore.IdentityAccess.Domain;
using SafeCore.Notifications.Domain;
using SafeCore.Shared.Contracts;
using SafeCore.Locations.Application;
using SafeCore.Locations.Domain;
using SafeCore.Shared.Data;
using System.Data;

namespace SafeCore.IdentityAccess.API;

[ApiController]
[Authorize(Roles = "Administrator")]
[Route("api/users")]
public sealed class UsersController(UserManager<User> userManager, IRepository<Notification> notifications, SafeCoreDbContext db) : ControllerBase
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
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (request.Role == "Administrator" && await userManager.GetUsersInRoleAsync("Administrator") is { Count: >= 3 })
            return Conflict(new ProblemDetails { Title = "The system allows at most three administrator accounts." });
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

        await transaction.CommitAsync(cancellationToken);
        var response = await ToResponseAsync(user);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, response);
    }

    [HttpGet("{id:guid}/assignments")]
    public async Task<IActionResult> GetAssignments(Guid id, CancellationToken ct)
    {
        if (await userManager.FindByIdAsync(id.ToString()) is null) return NotFound();
        var assignments = await db.UserAssignments.Where(item => item.UserId == id).ToListAsync(ct);
        var propertyIds = assignments.Select(item => item.PropertyId).Distinct().ToArray();
        var zoneIds = assignments.Where(item => item.ZoneId != null).Select(item => item.ZoneId!.Value).Distinct().ToArray();
        var properties = await db.Properties.Where(item => propertyIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, ct);
        var zones = await db.Zones.Where(item => zoneIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, ct);
        return Ok(assignments.Select(item => new UserAssignmentResponse(item.Id, id, item.PropertyId,
            properties.GetValueOrDefault(item.PropertyId)?.Name ?? string.Empty, item.ZoneId,
            item.ZoneId is { } zoneId ? zones.GetValueOrDefault(zoneId)?.Name : null)).ToArray());
    }

    [HttpPut("{id:guid}/assignments")]
    public async Task<IActionResult> ReplaceAssignments(Guid id, UserAssignmentsDto dto, CancellationToken ct)
    {
        var target = await userManager.FindByIdAsync(id.ToString());
        if (target is null) return NotFound();
        var roleList = await userManager.GetRolesAsync(target);
        var isResident = roleList.Contains("Resident", StringComparer.Ordinal);
        var requested = dto.Assignments.DistinctBy(item => (item.PropertyId, item.ZoneId)).ToArray();
        foreach (var assignment in requested)
        {
            if (!await db.Properties.AnyAsync(item => item.Id == assignment.PropertyId, ct))
                return BadRequest(new { errors = new[] { $"PropertyId {assignment.PropertyId} does not exist." } });
            if (isResident && assignment.ZoneId is null)
                return BadRequest(new { errors = new[] { "Resident assignments must include a ZoneId." } });
            if (assignment.ZoneId is { } zoneId && !await db.Zones.AnyAsync(zone => zone.Id == zoneId && zone.PropertyId == assignment.PropertyId, ct))
                return BadRequest(new { errors = new[] { $"ZoneId {zoneId} does not belong to PropertyId {assignment.PropertyId}." } });
        }
        var existing = await db.UserAssignments.Where(item => item.UserId == id).ToListAsync(ct);
        db.UserAssignments.RemoveRange(existing);
        db.UserAssignments.AddRange(requested.Select(item => new UserAssignment { UserId = id, PropertyId = item.PropertyId, ZoneId = item.ZoneId }));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid id, ResetPasswordRequest request)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var result = await userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(error => error.Description) });
        user.MustChangePassword = true;
        var update = await userManager.UpdateAsync(user);
        return update.Succeeded ? NoContent() : BadRequest(new { errors = update.Errors.Select(error => error.Description) });
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
        if (await db.UserAssignments.AnyAsync(item => item.UserId == id))
            return Conflict(new ProblemDetails { Title = "A user with property assignments cannot be deleted; remove assignments or disable the account." });
        if (await db.Devices.AnyAsync(item => item.CreatedByUserId == id))
            return Conflict(new ProblemDetails { Title = "A user who registered devices cannot be deleted; disable the account instead." });

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
        user.CreatedAt,
        user.MustChangePassword);
}
