using Ballers.API.Data;
using Ballers.API.Models;
using Ballers.Shared;
using Microsoft.EntityFrameworkCore;

namespace Ballers.API.Services;

public interface INotificationService
{
    Task CreateAsync(string userId, NotificationType type, string message, string? link = null, int? referenceId = null);
    Task CreateForTeamAsync(int teamId, NotificationType type, string message, string? link = null, int? referenceId = null);
    Task<bool> IsEnabledAsync(NotificationType type);
    Task<NotificationsResponse> GetForUserAsync(string userId, int count = 20);
    Task MarkReadAsync(int id, string userId);
    Task MarkAllReadAsync(string userId);
    Task<List<NotificationSettingDto>> GetSettingsAsync();
    Task UpdateSettingAsync(NotificationType type, bool isEnabled);
}

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;

    public NotificationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsEnabledAsync(NotificationType type)
    {
        var setting = await _db.NotificationSettings.FindAsync(type);
        return setting?.IsEnabled ?? true;
    }

    public async Task CreateAsync(string userId, NotificationType type, string message, string? link = null, int? referenceId = null)
    {
        if (!await IsEnabledAsync(type)) return;

        if (referenceId.HasValue)
        {
            var exists = await _db.Notifications.AnyAsync(n =>
                n.UserId == userId && n.Type == type && n.ReferenceId == referenceId);
            if (exists) return;
        }

        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            Message = message,
            Link = link,
            ReferenceId = referenceId,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    public async Task CreateForTeamAsync(int teamId, NotificationType type, string message, string? link = null, int? referenceId = null)
    {
        if (!await IsEnabledAsync(type)) return;

        var users = await _db.Users.Where(u => u.TeamId == teamId).ToListAsync();
        foreach (var user in users)
        {
            if (referenceId.HasValue)
            {
                var exists = await _db.Notifications.AnyAsync(n =>
                    n.UserId == user.Id && n.Type == type && n.ReferenceId == referenceId);
                if (exists) continue;
            }

            _db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Type = type,
                Message = message,
                Link = link,
                ReferenceId = referenceId,
                CreatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
    }

    public async Task<NotificationsResponse> GetForUserAsync(string userId, int count = 20)
    {
        var unread = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        var notifications = await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(count)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Message = n.Message,
                Link = n.Link,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync();

        return new NotificationsResponse { UnreadCount = unread, Notifications = notifications };
    }

    public async Task MarkReadAsync(int id, string userId)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (n == null) return;
        n.IsRead = true;
        await _db.SaveChangesAsync();
    }

    public async Task MarkAllReadAsync(string userId)
    {
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task<List<NotificationSettingDto>> GetSettingsAsync()
    {
        return await _db.NotificationSettings
            .Select(s => new NotificationSettingDto
            {
                Type = s.Type,
                IsEnabled = s.IsEnabled,
                DisplayName = s.DisplayName,
                Description = s.Description
            })
            .ToListAsync();
    }

    public async Task UpdateSettingAsync(NotificationType type, bool isEnabled)
    {
        var setting = await _db.NotificationSettings.FindAsync(type);
        if (setting == null) return;
        setting.IsEnabled = isEnabled;
        await _db.SaveChangesAsync();
    }
}
