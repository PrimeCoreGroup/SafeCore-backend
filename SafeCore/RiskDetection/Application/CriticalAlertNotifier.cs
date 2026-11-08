using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SafeCore.IdentityAccess.Domain;
using SafeCore.MonitoringConfiguration.Application;
using SafeCore.Notifications.Domain;
using SafeCore.RiskDetection.Domain;
using SafeCore.Shared.Data;

namespace SafeCore.RiskDetection.Application;

public sealed class CriticalAlertNotifier(SafetyConfigurationStore settings, UserManager<User> users, SafeCoreDbContext db)
{
    public async Task NotifyRespondersAsync(RiskSituation risk, CancellationToken ct = default)
    {
        if (risk.Severity != "Critical") return;
        var configuration = await settings.GetAsync(ct);
        if (!configuration.CriticalAlertsEnabled || !configuration.NotifyResponders) return;
        var responders = await users.GetUsersInRoleAsync("Responder");
        var at = risk.DetectedAt.ToString("O");
        foreach (var responder in responders.Where(user => user.Status == UserStatuses.Active))
        {
            if (risk.DeviceId is { } deviceId)
            {
                var deviceZoneId = await db.Devices.Where(device => device.Id == deviceId).Select(device => device.ZoneId).SingleOrDefaultAsync(ct);
                if (deviceZoneId is { } zoneId)
                {
                    var propertyId = await db.Zones.Where(zone => zone.Id == zoneId).Select(zone => (Guid?)zone.PropertyId).SingleOrDefaultAsync(ct);
                    if (propertyId is { } assignedProperty && !await db.UserAssignments.AnyAsync(a => a.UserId == responder.Id && a.PropertyId == assignedProperty, ct))
                        continue;
                }
            }
            db.Notifications.Add(new Notification
            {
                RecipientUserId = responder.Id,
                Title = $"Alerta crítica: {risk.RiskType}",
                Body = risk.DeviceId is { } device ? $"Riesgo {risk.RiskType} detectado por el dispositivo {device} a las {at}." : $"Riesgo {risk.RiskType} registrado a las {at}.",
                Channel = "Push",
                Status = "Pending"
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
