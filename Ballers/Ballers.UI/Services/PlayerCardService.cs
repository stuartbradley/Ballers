using Ballers.Shared;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class PlayerCardService
    {
        private readonly HttpClient _http;

        public PlayerCardService(HttpClient http) => _http = http;

        public async Task<MyPlayerCardDto?> GetMyCard()
        {
            var response = await _http.GetAsync("api/playercard/me");
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<MyPlayerCardDto>();
        }

        /// <summary>Null when the thresholds could not be fetched.</summary>
        public async Task<CardTierThresholdsDto?> GetTierThresholds()
        {
            try
            {
                var response = await _http.GetAsync("api/playercard/tiers");
                if (response.StatusCode != System.Net.HttpStatusCode.OK) return null;
                return await response.Content.ReadFromJsonAsync<CardTierThresholdsDto>();
            }
            // The home page carries on without the numbers rather than failing
            // to render over a missing or malformed reply.
            catch (HttpRequestException) { return null; }
            catch (System.Text.Json.JsonException) { return null; }
        }

        /// <summary>Null when there is no card of the week yet, or it could not be fetched.</summary>
        public async Task<FeaturedCardDto?> GetFeaturedCard()
        {
            try
            {
                var response = await _http.GetAsync("api/playercard/featured");
                if (response.StatusCode != System.Net.HttpStatusCode.OK) return null;
                return await response.Content.ReadFromJsonAsync<FeaturedCardDto>();
            }
            catch (HttpRequestException) { return null; }
            catch (System.Text.Json.JsonException) { return null; }
        }

        public async Task<List<PlayerPointsAwardDto>> GetMyAwards()
        {
            var response = await _http.GetAsync("api/playercard/me/awards");
            if (!response.IsSuccessStatusCode) return new();
            return await response.Content.ReadFromJsonAsync<List<PlayerPointsAwardDto>>() ?? new();
        }

        /// <summary>
        /// Spends points on attributes. Throws on failure rather than returning
        /// null: a spend that silently did nothing would look to the player like
        /// points had gone missing.
        /// </summary>
        public async Task<MyPlayerCardDto> Allocate(AllocatePointsRequest request)
        {
            var response = await _http.PostAsJsonAsync("api/playercard/me/allocate", request);

            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();
                throw new Exception(string.IsNullOrWhiteSpace(message)
                    ? "Unable to save your points."
                    : message);
            }

            return await response.Content.ReadFromJsonAsync<MyPlayerCardDto>()
                ?? throw new Exception("Unable to read your card back after saving.");
        }
    }
}
