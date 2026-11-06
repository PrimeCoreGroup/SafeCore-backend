using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SafeCore.Shared.Contracts;
using SafeCore.IdentityAccess.Domain;
using SafeCore.Notifications.Application;
using SafeCore.Notifications.Domain;

namespace SafeCore.Notifications.API;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IRepository<Notification> _notifications;
    private readonly UserManager<User> _users;
    public NotificationsController(IRepository<Notification> notifications, UserManager<User> users)
    {
        _notifications = notifications;
        _users = users;
    }

    [HttpGet]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<IActionResult> GetAll(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var notifications = await _notifications.ListAsync(page, pageSize, cancellationToken);
        return Ok(notifications);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var notification = await _notifications.GetByIdAsync(id);
        if (notification == null) return NotFound();
        return Ok(notification);
    }

    [HttpGet("mine")]
    [Authorize(Roles = "Resident")]
    public async Task<IActionResult> GetMine(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();
        return Ok(await _notifications.ListWhereAsync(item => item.RecipientUserId == userId, page, pageSize, cancellationToken));
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<IActionResult> Create(NotificationDto dto, CancellationToken cancellationToken)
    {
        if (await _users.FindByIdAsync(dto.RecipientUserId.ToString()) is null)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.RecipientUserId)] = ["RecipientUserId must identify a registered user."] }));
        var notification = new Notification
        {
            RecipientUserId = dto.RecipientUserId,
            Title = dto.Title,
            Body = dto.Body,
            Channel = dto.Channel,
            Status = dto.Status
        };

        _notifications.Add(notification);
        await _notifications.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = notification.Id }, notification);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<IActionResult> Update(Guid id, NotificationDto dto)
    {
        var notification = await _notifications.GetByIdAsync(id);
        if (notification == null) return NotFound();

        notification.RecipientUserId = dto.RecipientUserId;
        notification.Title = dto.Title;
        notification.Body = dto.Body;
        notification.Channel = dto.Channel;
        notification.Status = dto.Status;

        await _notifications.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var notification = await _notifications.GetByIdAsync(id);
        if (notification == null) return NotFound();

        _notifications.Remove(notification);
        await _notifications.SaveChangesAsync();
        return NoContent();
    }
}
