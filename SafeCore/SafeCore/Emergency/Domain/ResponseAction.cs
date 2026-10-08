using System;
using SafeCore.Shared.Kernel;

namespace SafeCore.Emergency.Domain;

public class ResponseAction : Entity
{
    public Guid EmergencyResponseId { get; set; }
    public string ActuatorType { get; set; } = string.Empty;
    public string Instruction { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}