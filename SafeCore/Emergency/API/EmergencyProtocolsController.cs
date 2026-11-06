using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeCore.Emergency.Application;
using SafeCore.Emergency.Domain;
using SafeCore.Shared.Contracts;

namespace SafeCore.Emergency.API;

[ApiController]
[Authorize(Roles = "Administrator,Operator,Responder")]
[Route("api/emergency/protocols")]
public sealed class EmergencyProtocolsController(IRepository<EmergencyProtocol> protocols, IRepository<EmergencyResponse> responses) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<EmergencyProtocol>>> GetAll(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) =>
        Ok((await protocols.ListAsync(page, pageSize, cancellationToken)).OrderBy(item => item.Name));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmergencyProtocol>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var protocol = await protocols.GetByIdAsync(id, cancellationToken);
        return protocol is null ? NotFound() : Ok(protocol);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<ActionResult<EmergencyProtocol>> Create(EmergencyProtocolDto dto, CancellationToken cancellationToken)
    {
        var protocol = new EmergencyProtocol { Name = dto.Name.Trim(), EmergencyType = dto.EmergencyType.Trim(), IsActive = dto.IsActive };
        protocols.Add(protocol);
        await protocols.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = protocol.Id }, protocol);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<IActionResult> Update(Guid id, EmergencyProtocolDto dto, CancellationToken cancellationToken)
    {
        var protocol = await protocols.GetByIdAsync(id, cancellationToken);
        if (protocol is null) return NotFound();
        protocol.Name = dto.Name.Trim();
        protocol.EmergencyType = dto.EmergencyType.Trim();
        protocol.IsActive = dto.IsActive;
        await protocols.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var protocol = await protocols.GetByIdAsync(id, cancellationToken);
        if (protocol is null) return NotFound();
        if (await responses.AnyAsync(item => item.ProtocolId == id, cancellationToken))
            return Conflict(new ProblemDetails { Title = "A protocol used by an emergency response cannot be deleted." });
        protocols.Remove(protocol);
        await protocols.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
