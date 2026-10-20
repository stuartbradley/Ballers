using Ballers.Shared;
using Ballers.UI.Infrastructure;

namespace Ballers.Services
{
    public class ManagerService
    {
        private readonly HttpClient _http;
        public ManagerService(HttpClient http) => _http = http;

        // Null when it can't be loaded, so the page can say so rather than
        // show an empty dashboard.
        public Task<ManagerDashboardDto?> GetDashboard() =>
            _http.GetOrDefault<ManagerDashboardDto>("api/manager/dashboard");
    }
}
