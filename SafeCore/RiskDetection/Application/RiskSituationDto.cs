using System;
using System.ComponentModel.DataAnnotations;

namespace SafeCore.RiskDetection.Application;

public class RiskSituationDto
{
    [Required, RegularExpression("^(Fire|GasOrSmoke|Earthquake|SensorFault)$")] public string RiskType { get; set; } = string.Empty;
    [Required, RegularExpression("^(Open|Acknowledged|Resolved)$")] public string Status { get; set; } = "Open";
    [Required, RegularExpression("^(Low|Medium|High|Critical)$")] public string Severity { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
}
