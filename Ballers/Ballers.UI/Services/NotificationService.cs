using Ballers.Shared;
using Ballers.UI.Infrastructure;

namespace Ballers.Client.Services;

public class NotificationService
{
    private readonly HttpClient _http;

    public NotificationService(HttpClient http)
    {
        _http = http;
    }

    public Task<NotificationsResponse?> GetAsync() =>
        _http.GetOrDefault<NotificationsResponse>("api/notifications");

    // Marking as read is best-effort: the bell refreshes on its next poll, so
    // a failure here is not worth telling anyone about.
    public async Task MarkReadAsync(int id)
    {
        try { await _http.PostOk($"api/notifications/{id}/read", null, ""); }
        catch (ApiException) { }
    }

    public async Task MarkAllReadAsync()
    {
        try { await _http.PostOk("api/notifications/read-all", null, ""); }
        catch (ApiException) { }
    }

    public Task<List<NotificationSettingDto>> GetSettingsAsync() =>
        _http.GetListOrEmpty<NotificationSettingDto>("api/notifications/settings");

    public Task UpdateSettingAsync(NotificationType type, bool isEnabled) =>
        _http.PutOk($"api/notifications/settings/{(int)type}", isEnabled, "Couldn't save that notification setting.");
}
