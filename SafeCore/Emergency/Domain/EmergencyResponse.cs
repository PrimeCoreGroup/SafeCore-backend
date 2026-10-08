using System;
using SafeCore.Shared.Kernel;

namespace SafeCore.Emergency.Domain;

public class EmergencyResponse : Entity
{
    public Guid ProtocolId { get; set; }
    public string EmergencyType { get; set; } = string.Empty;
    public Guid RiskSituationId { get; set; }
    public string Status { get; set; } = string.Empty;
}