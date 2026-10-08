using System;
using SafeCore.Shared.Kernel;

namespace SafeCore.Notifications.Domain;

public class Notification : Entity
{
    public Guid RecipientUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}