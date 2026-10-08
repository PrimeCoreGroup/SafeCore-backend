using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeCore.Notifications.Application;
using SafeCore.Notifications.Domain;
using SafeCore.Shared.Data;

namespace SafeCore.Notifications.API;

[ApiController]
[Route("api/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly SafeCoreDbContext _context;

    public NotificationsController(SafeCoreDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var notifications = await _context.Notifications.ToListAsync();
        return Ok(notifications);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null) return NotFound();
        return Ok(notification);
    }

    [HttpPost]
    public async Task<IActionResult> Create(NotificationDto dto)
    {
        var notification = new Notification
        {
            RecipientUserId = dto.RecipientUserId,
            Title = dto.Title,
            Body = dto.Body,
            Channel = dto.Channel,
            Status = dto.Status
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = notification.Id }, notification);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, NotificationDto dto)
    {
        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null) return NotFound();

        notification.RecipientUserId = dto.RecipientUserId;
        notification.Title = dto.Title;
        notification.Body = dto.Body;
        notification.Channel = dto.Channel;
        notification.Status = dto.Status;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null) return NotFound();

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}