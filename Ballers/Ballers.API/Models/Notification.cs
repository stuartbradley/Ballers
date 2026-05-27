using Ballers.Shared;

namespace Ballers.API.Models;

public class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public NotificationType Type { get; set; }
    public string Message { get; set; } = "";
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? ReferenceId { get; set; }
}

public class NotificationSetting
{
    public NotificationType Type { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
}
