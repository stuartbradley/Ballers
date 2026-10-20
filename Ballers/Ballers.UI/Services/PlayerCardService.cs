using Ballers.Shared;
using Ballers.UI.Infrastructure;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class PlayerCardService
    {
        private readonly HttpClient _http;

        public PlayerCardService(HttpClient http) => _http = http;

        public Task<MyPlayerCardDto?> GetMyCard() =>
            _http.GetOrDefault<MyPlayerCardDto>("api/playercard/me");

        /// <summary>Null when the thresholds could not be fetched.</summary>
        public Task<CardTierThresholdsDto?> GetTierThresholds() =>
            _http.GetOrDefault<CardTierThresholdsDto>("api/playercard/tiers");

        /// <summary>Null when there is no card of the week yet, or it could not be fetched.</summary>
        public Task<FeaturedCardDto?> GetFeaturedCard() =>
            _http.GetOrDefault<FeaturedCardDto>("api/playercard/featured");

        public Task<List<PlayerPointsAwardDto>> GetMyAwards() =>
            _http.GetListOrEmpty<PlayerPointsAwardDto>("api/playercard/me/awards");

        /// <summary>
        /// Spends points on attributes. Throws on failure rather than returning
        /// null: a spend that silently did nothing would look to the player like
        /// points had gone missing.
        /// </summary>
        public async Task<MyPlayerCardDto> Allocate(AllocatePointsRequest request)
        {
            return await _http.PostOk<MyPlayerCardDto>("api/playercard/me/allocate", request, "Unable to save your points.")
                ?? throw new ApiException("Unable to read your card back after saving.", 200);
        }
    }
}
