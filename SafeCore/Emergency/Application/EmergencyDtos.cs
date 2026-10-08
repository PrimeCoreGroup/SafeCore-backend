using System;

namespace SafeCore.Emergency.Application;

public class EmergencyProtocolDto
{
    public string Name { get; set; } = string.Empty;
    public string EmergencyType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class EmergencyResponseDto
{
    public Guid ProtocolId { get; set; }
    public string EmergencyType { get; set; } = string.Empty;
    public Guid RiskSituationId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class ResponseActionDto
{
    public Guid EmergencyResponseId { get; set; }
    public string ActuatorType { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}