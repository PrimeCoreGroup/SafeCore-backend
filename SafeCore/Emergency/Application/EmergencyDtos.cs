using System;
using System.ComponentModel.DataAnnotations;

namespace SafeCore.Emergency.Application;

public class EmergencyProtocolDto
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;
    [Required, StringLength(50)]
    public string EmergencyType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class EmergencyResponseDto
{
    [Required]
    public Guid ProtocolId { get; set; }
    [Required, StringLength(50)]
    public string EmergencyType { get; set; } = string.Empty;
    [Required]
    public Guid RiskSituationId { get; set; }
    [Required, RegularExpression("^(Open|InProgress|Resolved|Cancelled)$")]
    public string Status { get; set; } = string.Empty;
}

public class ResponseActionDto
{
    [Required]
    public Guid EmergencyResponseId { get; set; }
    [Required, StringLength(50)]
    public string ActuatorType { get; set; } = string.Empty;
    [Required, StringLength(1000)]
    public string Instruction { get; set; } = string.Empty;
    [Required, RegularExpression("^(Pending|Dispatched|Completed|Failed|Cancelled)$")]
    public string Status { get; set; } = string.Empty;
}
