using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SafeCore.Emergency.Application;
using SafeCore.Emergency.Domain;
using SafeCore.Shared.Contracts;

namespace SafeCore.Emergency.API;

[ApiController]
[Authorize(Roles = "Administrator,Operator,Responder")]
[Route("api/emergency/actions")]
public sealed class ResponseActionsController(IRepository<ResponseAction> actions, IRepository<EmergencyResponse> responses) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ResponseAction>>> GetAll(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) =>
        Ok(await actions.ListAsync(page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ResponseAction>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var action = await actions.GetByIdAsync(id, cancellationToken);
        return action is null ? NotFound() : Ok(action);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator,Operator,Responder")]
    public async Task<ActionResult<ResponseAction>> Create(ResponseActionDto dto, CancellationToken cancellationToken)
    {
        if (!await responses.AnyAsync(item => item.Id == dto.EmergencyResponseId, cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.EmergencyResponseId)] = ["EmergencyResponseId must identify an active response."] }));
        var action = new ResponseAction
        {
            EmergencyResponseId = dto.EmergencyResponseId,
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
        if (!await responses.AnyAsync(item => item.Id == dto.EmergencyResponseId, cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.EmergencyResponseId)] = ["EmergencyResponseId must identify a recorded response."] }));
        action.EmergencyResponseId = dto.EmergencyResponseId;
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
}
