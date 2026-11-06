using System;
using System.ComponentModel.DataAnnotations;

namespace SafeCore.Notifications.Application;

public class NotificationDto
{
    [Required] public Guid RecipientUserId { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Body { get; set; } = string.Empty;
    [Required, RegularExpression("^(Email|Sms|Push)$")] public string Channel { get; set; } = string.Empty;
    [Required, RegularExpression("^(Pending|Delivered|Failed)$")] public string Status { get; set; } = "Pending";
}
