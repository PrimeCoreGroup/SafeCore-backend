using System;
using SafeCore.Shared.Kernel;

namespace SafeCore.RiskDetection.Domain;

public class RiskSituation : Entity
{
    public Guid? DeviceId { get; set; }
    public string Origin { get; set; } = "Manual";
    public string RiskType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime DetectedAt { get; set; }
    public DateTime? AlertUntil { get; set; }
}
