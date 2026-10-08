using System;

namespace SafeCore.RiskDetection.Application;

public class RiskSituationDto
{
    public string RiskType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
}