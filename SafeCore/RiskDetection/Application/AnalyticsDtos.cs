namespace SafeCore.RiskDetection.Application;

public sealed record AnalyticsSummaryResponse(
    int TotalAlerts,
    IReadOnlyDictionary<string, int> AlertsByType,
    IReadOnlyDictionary<string, int> AlertsBySeverity,
    decimal ResolvedPercentage,
    double? AverageResponseMinutes,
    decimal DeviceAvailabilityPercentage,
    DateTime From,
    DateTime To,
    string TimeZone);

public sealed record AnalyticsTrendPoint(DateTime Date, int RegisteredAlerts, int AttendedAlerts);
public sealed record AnalyticsReportDescriptor(string Id, string Name, string Description, string Format);
