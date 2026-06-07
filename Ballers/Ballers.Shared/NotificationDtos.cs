namespace Ballers.Shared;

public enum NotificationType
{
    FixtureUpdated = 1,
    NoSquadSet = 2,
    NoRefereeSet = 3,
    NoLocationSet = 4,
    NoStatsEntered = 5,
    NewFixtureScheduled = 6,
    ResultSubmitted = 7,
}

public class NotificationDto
{
    public int Id { get; set; }
    public NotificationType Type { get; set; }
    public string Message { get; set; } = "";
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationsResponse
{
    public int UnreadCount { get; set; }
    public List<NotificationDto> Notifications { get; set; } = new();
}

public class NotificationSettingDto
{
    public NotificationType Type { get; set; }
    public bool IsEnabled { get; set; }
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
}

public class LeagueSettingsDto
{
    public bool PlayersLocked { get; set; }
    public bool FixturesLocked { get; set; }
}
