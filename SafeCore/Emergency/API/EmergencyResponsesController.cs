using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SafeCore.Shared.Contracts;
using SafeCore.Emergency.Application;
using SafeCore.Emergency.Domain;
using SafeCore.RiskDetection.Domain;

namespace SafeCore.Emergency.API;

[ApiController]
[Authorize(Roles = "Administrator,Operator,Responder")]
[Route("api/[controller]")]
public class EmergencyResponsesController : ControllerBase
{
    private readonly IRepository<EmergencyResponse> _responses;
    private readonly IRepository<EmergencyProtocol> _protocols;
    private readonly IRepository<RiskSituation> _risks;
    private readonly IRepository<ResponseAction> _actions;
    public EmergencyResponsesController(IRepository<EmergencyResponse> responses, IRepository<EmergencyProtocol> protocols, IRepository<RiskSituation> risks, IRepository<ResponseAction> actions)
    {
        _responses = responses;
        _protocols = protocols;
        _risks = risks;
        _actions = actions;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var responses = await _responses.ListAsync(page, pageSize, cancellationToken);
        return Ok(responses);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _responses.GetByIdAsync(id);
        if (response == null) return NotFound();
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create(EmergencyResponseDto dto, CancellationToken cancellationToken)
    {
        var protocol = await _protocols.GetByIdAsync(dto.ProtocolId, cancellationToken);
        if (protocol is null || !protocol.IsActive)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.ProtocolId)] = ["ProtocolId must identify an active protocol."] }));
        if (!await _risks.AnyAsync(item => item.Id == dto.RiskSituationId, cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.RiskSituationId)] = ["RiskSituationId must identify a recorded risk."] }));
        var response = new EmergencyResponse
        {
            ProtocolId = dto.ProtocolId,
            EmergencyType = dto.EmergencyType,
            RiskSituationId = dto.RiskSituationId,
            Status = dto.Status
        };

        _responses.Add(response);
        await _responses.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Administrator,Operator")]
    public async Task<IActionResult> Update(Guid id, EmergencyResponseDto dto, CancellationToken cancellationToken)
    {
        var response = await _responses.GetByIdAsync(id, cancellationToken);
        if (response == null) return NotFound();
        var protocol = await _protocols.GetByIdAsync(dto.ProtocolId, cancellationToken);
        if (protocol is null || !protocol.IsActive)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.ProtocolId)] = ["ProtocolId must identify an active protocol."] }));
        if (!await _risks.AnyAsync(item => item.Id == dto.RiskSituationId, cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.RiskSituationId)] = ["RiskSituationId must identify a recorded risk."] }));

        response.ProtocolId = dto.ProtocolId;
        response.EmergencyType = dto.EmergencyType;
        response.RiskSituationId = dto.RiskSituationId;
        response.Status = dto.Status;

        await _responses.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var response = await _responses.GetByIdAsync(id);
        if (response == null) return NotFound();
        if (await _actions.AnyAsync(action => action.EmergencyResponseId == id))
            return Conflict(new ProblemDetails { Title = "An emergency response with recorded actions cannot be deleted." });

        _responses.Remove(response);
        await _responses.SaveChangesAsync();
        return NoContent();
    }
}
