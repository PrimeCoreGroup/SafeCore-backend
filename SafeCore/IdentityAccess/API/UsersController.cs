using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SafeCore.IdentityAccess.Application;
using SafeCore.IdentityAccess.Domain;
using SafeCore.Shared.Data;

namespace SafeCore.IdentityAccess.API;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly SafeCoreDbContext _context;

    public UsersController(SafeCoreDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await _context.Users.ToListAsync();
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        return Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create(UserDto dto)
    {
        var user = new User
        {
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            PasswordHash = dto.PasswordHash,
            Status = dto.Status
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, UserDto dto)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        user.Email = dto.Email;
        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.PasswordHash = dto.PasswordHash;
        user.Status = dto.Status;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}