using Ballers.Models;
using Ballers.UI.Infrastructure;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class RefereeService
    {
        private readonly HttpClient _http;

        public RefereeService(HttpClient http) => _http = http;

        public Task<List<RefereeDto>> GetAll() => _http.GetListOrEmpty<RefereeDto>("api/referees");

        public async Task<RefereeDto> Create(SaveRefereeRequest request) =>
            await _http.PostOk<RefereeDto>("api/referees", request, "Failed to add referee. Please try again.")
            ?? throw new ApiException("The referee was added but could not be read back.", 200);

        public async Task<RefereeDto> Update(int id, SaveRefereeRequest request) =>
            await _http.PutOk<RefereeDto>($"api/referees/{id}", request, "Failed to update. Please try again.")
            ?? throw new ApiException("The referee was saved but could not be read back.", 200);

        public Task Delete(int id) =>
            _http.DeleteOk($"api/referees/{id}", "Couldn't delete that referee.");

        public Task AssignToFixture(int fixtureId, int? refereeId) =>
            _http.PutOk($"api/fixtures/{fixtureId}/referee",
                new AssignRefereeRequest { RefereeId = refereeId }, "Couldn't assign the referee.");
    }
}
