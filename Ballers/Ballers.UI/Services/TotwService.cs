using Ballers.Shared;
using Ballers.UI.Infrastructure;

namespace Ballers.Services
{
    public class TotwService
    {
        private readonly HttpClient _http;
        public TotwService(HttpClient http) => _http = http;

        public Task<TeamOfTheWeekDto?> GetCurrent() =>
            _http.GetOrDefault<TeamOfTheWeekDto>("api/totw/current");

        public Task<List<TeamOfTheWeekDto>> GetHistory() =>
            _http.GetListOrEmpty<TeamOfTheWeekDto>("api/totw/history");

        public Task<List<TotwWeekRefDto>> GetWeeks() =>
            _http.GetListOrEmpty<TotwWeekRefDto>("api/totw/weeks");

        // Empty on failure: the table is an extra, not worth an error.
        public Task<List<TotwAppearanceDto>> GetAppearances() =>
            _http.GetListOrEmpty<TotwAppearanceDto>("api/totw/appearances");

        public Task<TeamOfTheWeekDto?> GetWeek(int matchNumber) =>
            _http.GetOrDefault<TeamOfTheWeekDto>($"api/totw/{matchNumber}");
    }
}
