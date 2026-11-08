using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SafeCore.Shared.Contracts;
using SafeCore.Emergency.Application;
using SafeCore.Emergency.Domain;
using SafeCore.RiskDetection.Domain;
using SafeCore.MonitoringConfiguration.Application;
using SafeCore.Shared.Data;
using SafeCore.Locations.Application;
using Microsoft.EntityFrameworkCore;

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
    private readonly SafetyConfigurationStore _configuration;
    private readonly SafeCoreDbContext _db;
    private readonly LocationAccessService _access;
    public EmergencyResponsesController(IRepository<EmergencyResponse> responses, IRepository<EmergencyProtocol> protocols, IRepository<RiskSituation> risks, IRepository<ResponseAction> actions, SafetyConfigurationStore configuration, SafeCoreDbContext db, LocationAccessService access)
    {
        _responses = responses;
        _protocols = protocols;
        _risks = risks;
        _actions = actions;
        _configuration = configuration;
        _db = db; _access = access;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
    {
        var deviceIds = await _access.DeviceIdsAsync(User, cancellationToken);
        var safePage = Math.Max(page, 1); var safeSize = Math.Clamp(pageSize, 1, 250);
        var responses = await _db.EmergencyResponses.AsNoTracking()
            .Where(response => _db.RiskSituations.Any(risk => risk.Id == response.RiskSituationId && (risk.DeviceId == null || deviceIds.Contains(risk.DeviceId.Value))))
            .OrderByDescending(item => item.CreatedAt).Skip((int)Math.Min((long)(safePage - 1) * safeSize, int.MaxValue)).Take(safeSize).ToListAsync(cancellationToken);
        return Ok(responses);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var response = await _responses.GetByIdAsync(id);
        if (response == null) return NotFound();
        if (!await CanViewResponseAsync(response, HttpContext.RequestAborted)) return Forbid();
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create(EmergencyResponseDto dto, CancellationToken cancellationToken)
    {
        var config = await _configuration.GetAsync(cancellationToken);
        if (config.ManualConfirmation && dto.Status != "Open")
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.Status)] = ["Manual confirmation is enabled; a new response must start with Open. An operator can then move it to InProgress to confirm."] }));
        var protocol = await _protocols.GetByIdAsync(dto.ProtocolId, cancellationToken);
        if (protocol is null || !protocol.IsActive)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.ProtocolId)] = ["ProtocolId must identify an active protocol."] }));
        if (!await _risks.AnyAsync(item => item.Id == dto.RiskSituationId, cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.RiskSituationId)] = ["RiskSituationId must identify a recorded risk."] }));
        if (!await CanViewRiskAsync(dto.RiskSituationId, cancellationToken)) return Forbid();
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
        var config = await _configuration.GetAsync(cancellationToken);
        var response = await _responses.GetByIdAsync(id, cancellationToken);
        if (response == null) return NotFound();
        if (!await CanViewResponseAsync(response, cancellationToken)) return Forbid();
        if (config.ManualConfirmation && response.Status == "Open" && dto.Status == "Resolved")
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.Status)] = ["Manual confirmation is enabled; move the response to InProgress before resolving it."] }));
        var protocol = await _protocols.GetByIdAsync(dto.ProtocolId, cancellationToken);
        if (protocol is null || !protocol.IsActive)
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.ProtocolId)] = ["ProtocolId must identify an active protocol."] }));
        if (!await _risks.AnyAsync(item => item.Id == dto.RiskSituationId, cancellationToken))
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { [nameof(dto.RiskSituationId)] = ["RiskSituationId must identify a recorded risk."] }));
        if (!await CanViewRiskAsync(dto.RiskSituationId, cancellationToken)) return Forbid();

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

    private async Task<bool> CanViewResponseAsync(EmergencyResponse response, CancellationToken ct) => await CanViewRiskAsync(response.RiskSituationId, ct);

    private async Task<bool> CanViewRiskAsync(Guid riskId, CancellationToken ct)
    {
        if (User.IsInRole("Administrator")) return true;
        var deviceId = await _db.RiskSituations.Where(item => item.Id == riskId).Select(item => item.DeviceId).SingleOrDefaultAsync(ct);
        return deviceId is null || (await _access.DeviceIdsAsync(User, ct)).Contains(deviceId.Value);
    }
}
