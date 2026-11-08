using Microsoft.EntityFrameworkCore;
using SafeCore.Shared.Data;

namespace SafeCore.DeviceManagement.Infrastructure;

/// <summary>Marks silent nodes as offline and records the transition for availability reporting.</summary>
public sealed class DeviceHeartbeatMonitor(IServiceScopeFactory scopeFactory, ILogger<DeviceHeartbeatMonitor> logger) : BackgroundService
{
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await MarkSilentDevicesOfflineAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Failed to process device heartbeat timeouts."); }
            try { await timer.WaitForNextTickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task MarkSilentDevicesOfflineAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SafeCoreDbContext>();
        var cutoff = DateTime.UtcNow - HeartbeatTimeout;
        var stale = await db.Devices.Where(device => device.ConnectivityStatus == "Online" && (device.LastSeenAt ?? device.CreatedAt) < cutoff).ToListAsync(ct);
        foreach (var device in stale)
        {
            device.ConnectivityStatus = "Offline";
            db.DeviceConnectivityEvents.Add(new SafeCore.DeviceManagement.Domain.DeviceConnectivityEvent { DeviceId = device.Id, ConnectivityStatus = "Offline" });
        }
        var commandTimeout = DateTime.UtcNow - TimeSpan.FromMinutes(2);
        var expiredCommands = await db.DeviceCommands.Where(item => item.Status == "Pending" && item.RequestedAt < commandTimeout).ToListAsync(ct);
        foreach (var command in expiredCommands)
        {
            command.Status = "Failed";
            command.Result = "Timed out waiting for the device to report the test result.";
            command.CompletedAt = DateTime.UtcNow;
            var component = await db.DeviceComponents.SingleOrDefaultAsync(item => item.Id == command.ComponentId, ct);
            if (component is not null) component.Status = "Fault";
        }
        if (stale.Count > 0 || expiredCommands.Count > 0) await db.SaveChangesAsync(ct);
    }
}
