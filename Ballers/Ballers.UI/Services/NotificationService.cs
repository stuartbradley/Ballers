using System.Net.Http.Json;
using Ballers.Shared;

namespace Ballers.Client.Services;

public class NotificationService
{
    private readonly HttpClient _http;

    public NotificationService(HttpClient http)
    {
        _http = http;
    }

    public async Task<NotificationsResponse?> GetAsync()
    {
        try { return await _http.GetFromJsonAsync<NotificationsResponse>("api/notifications"); }
        catch { return null; }
    }

    public async Task MarkReadAsync(int id)
    {
        try { await _http.PostAsync($"api/notifications/{id}/read", null); }
        catch { }
    }

    public async Task MarkAllReadAsync()
    {
        try { await _http.PostAsync("api/notifications/read-all", null); }
        catch { }
    }

    public async Task<List<NotificationSettingDto>> GetSettingsAsync()
    {
        try { return await _http.GetFromJsonAsync<List<NotificationSettingDto>>("api/notifications/settings") ?? new(); }
        catch { return new(); }
    }

    public async Task UpdateSettingAsync(NotificationType type, bool isEnabled)
    {
        await _http.PutAsJsonAsync($"api/notifications/settings/{(int)type}", isEnabled);
    }
}
