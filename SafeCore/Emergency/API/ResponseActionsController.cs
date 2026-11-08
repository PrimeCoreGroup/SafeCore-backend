using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeCore.Emergency.Application;
using SafeCore.Emergency.Domain;
using SafeCore.Shared.Contracts;
using SafeCore.Shared.Data;
using Microsoft.EntityFrameworkCore;
using SafeCore.Locations.Application;

namespace SafeCore.Emergency.API;

[ApiController]
[Authorize(Roles = "Administrator,Operator,Responder")]
[Route("api/emergency/actions")]
public sealed class ResponseActionsController(IRepository<ResponseAction> actions, IRepository<EmergencyResponse> responses, SafeCoreDbContext db, LocationAccessService access) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ResponseAction>>> GetAll(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var deviceIds = await access.DeviceIdsAsync(User, cancellationToken);
        var safePage = Math.Max(page, 1); var safeSize = Math.Clamp(pageSize, 1, 250);
        return Ok(await db.ResponseActions.AsNoTracking()
            .Where(action => db.EmergencyResponses.Any(response => response.Id == action.EmergencyResponseId
                && db.RiskSituations.Any(risk => risk.Id == response.RiskSituationId && (risk.DeviceId == null || deviceIds.Contains(risk.DeviceId.Value)))))
            .OrderByDescending(item => item.CreatedAt).Skip((int)Math.Min((long)(safePage - 1) * safeSize, int.MaxValue)).Take(safeSize).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ResponseAction>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var action = await actions.GetByIdAsync(id, cancellationToken);
        if (action is null) return NotFound();
        return await CanViewActionAsync(action.Id, cancellationToken) ? Ok(action) : Forbid();
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<ActionResult<ResponseAction>> Create(ResponseActionDto dto, CancellationToken cancellationToken)
    {
        if (!await responses.AnyAsync(item => item.Id == dto.EmergencyResponseId, cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.EmergencyResponseId)] = ["EmergencyResponseId must identify an active response."] }));
        if (!await CanViewResponseAsync(dto.EmergencyResponseId, cancellationToken)) return Forbid();
        if (!await ComponentMatchesResponseAsync(dto.ComponentId, dto.EmergencyResponseId, cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.ComponentId)] = ["ComponentId must belong to the device associated with the emergency risk."] }));
        var action = new ResponseAction
        {
            EmergencyResponseId = dto.EmergencyResponseId,
            ComponentId = dto.ComponentId,
            ActuatorType = dto.ActuatorType.Trim(),
            Instruction = dto.Instruction.Trim(),
            Status = dto.Status
        };
        actions.Add(action);
        await actions.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = action.Id }, action);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<IActionResult> Update(Guid id, ResponseActionDto dto, CancellationToken cancellationToken)
    {
        var action = await actions.GetByIdAsync(id, cancellationToken);
        if (action is null) return NotFound();
        if (!await CanViewActionAsync(id, cancellationToken)) return Forbid();
        if (!await responses.AnyAsync(item => item.Id == dto.EmergencyResponseId, cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.EmergencyResponseId)] = ["EmergencyResponseId must identify a recorded response."] }));
        if (!await CanViewResponseAsync(dto.EmergencyResponseId, cancellationToken)) return Forbid();
        if (!await ComponentMatchesResponseAsync(dto.ComponentId, dto.EmergencyResponseId, cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.ComponentId)] = ["ComponentId must belong to the device associated with the emergency risk."] }));
        action.EmergencyResponseId = dto.EmergencyResponseId;
        action.ComponentId = dto.ComponentId;
        action.ActuatorType = dto.ActuatorType.Trim();
        action.Instruction = dto.Instruction.Trim();
        action.Status = dto.Status;
        await actions.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var action = await actions.GetByIdAsync(id, cancellationToken);
        if (action is null) return NotFound();
        actions.Remove(action);
        await actions.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task<bool> ComponentMatchesResponseAsync(Guid? componentId, Guid responseId, CancellationToken ct)
    {
        if (componentId is null) return true;
        var deviceId = await (from response in db.EmergencyResponses
                              join risk in db.RiskSituations on response.RiskSituationId equals risk.Id
                              where response.Id == responseId
                              select risk.DeviceId).SingleOrDefaultAsync(ct);
        return deviceId is { } id && await db.DeviceComponents.AnyAsync(component => component.Id == componentId && component.DeviceId == id && component.Kind == "Actuator", ct);
    }

    private async Task<bool> CanViewActionAsync(Guid actionId, CancellationToken ct)
    {
        if (User.IsInRole("Administrator")) return true;
        var responseId = await db.ResponseActions.Where(item => item.Id == actionId).Select(item => item.EmergencyResponseId).SingleOrDefaultAsync(ct);
        return await CanViewResponseAsync(responseId, ct);
    }

    private async Task<bool> CanViewResponseAsync(Guid responseId, CancellationToken ct)
    {
        if (User.IsInRole("Administrator")) return true;
        var deviceId = await (from response in db.EmergencyResponses join risk in db.RiskSituations on response.RiskSituationId equals risk.Id
                              where response.Id == responseId select risk.DeviceId).SingleOrDefaultAsync(ct);
        return deviceId is null || (await access.DeviceIdsAsync(User, ct)).Contains(deviceId.Value);
    }
}
