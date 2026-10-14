using Ballers.Shared;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class ManagerService
    {
        private readonly HttpClient _http;
        public ManagerService(HttpClient http) => _http = http;

        // Null when it can't be loaded, so the page can say so rather than
        // show an empty dashboard.
        public async Task<ManagerDashboardDto?> GetDashboard()
        {
            try
            {
                var response = await _http.GetAsync("api/manager/dashboard");
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadFromJsonAsync<ManagerDashboardDto>();
            }
            catch (HttpRequestException)
            {
                return null;
            }
        }
    }
}
