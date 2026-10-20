using Ballers.Models;
using Ballers.Shared;
using Ballers.UI.Infrastructure;
using System.Net.Http.Json;

namespace Ballers.Services
{
    public class FixtureService
    {
        private readonly HttpClient _httpClient;

        public FixtureService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Every season unless one is named.
        public async Task<List<FixtureListItemDto>> GetFixtures(int? seasonId = null)
        {
            var response = await _httpClient.GetAsync(
                seasonId.HasValue ? $"api/fixtures?seasonId={seasonId}" : "api/fixtures");
            return await response.ReadOk<List<FixtureListItemDto>>("Couldn't load the fixtures.") ?? [];
        }

        public Task<FixtureWeekDto?> GetCurrentWeek() =>
            _httpClient.GetOrDefault<FixtureWeekDto>("api/fixtures/current-week");

        public Task<List<FixtureWeekDto>> GetFixtureWeeks(int? seasonId = null) =>
            _httpClient.GetListOrEmpty<FixtureWeekDto>(
                seasonId.HasValue ? $"api/fixtures/weeks?seasonId={seasonId}" : "api/fixtures/weeks");

        public Task<FixtureDetailsDto?> GetFixture(int id) =>
            _httpClient.GetOrDefault<FixtureDetailsDto>($"api/fixtures/{id}");

        // teamId is only honoured for admins and referees, who use it to pick the
        // home or away side; a manager's team always comes from their account.
        public async Task SaveSquad(int fixtureId, List<int> playerIds, int? teamId = null)
        {
            await _httpClient.PostOk($"api/fixtures/{fixtureId}/squad",
                new UpdateFixtureSquadRequest { PlayerIds = playerIds, TeamId = teamId }, "Unable to save squad.");
        }

        public async Task UpdateFixtureSchedule(int fixtureId, string? location, string? postcode, DateTime? kickoffTime)
        {
            await _httpClient.PutOk($"api/fixtures/{fixtureId}/schedule",
                new UpdateFixtureScheduleRequest { Location = location, Postcode = postcode, KickOffTime = kickoffTime },
                "Unable to update the fixture.");
        }

        // Hands hosting to the other team. Throws with the API's reason when it
        // can't be done (kicked off, stats in, locked…).
        public async Task SwapHomeAway(int fixtureId)
        {
            await _httpClient.PostOk($"api/fixtures/{fixtureId}/swap-home-away", null, "Couldn't swap home and away.");
        }

        public async Task SubmitStats(int fixtureId, List<FixturePlayerStatInput> stats, int? teamId = null)
        {
            var request = new SubmitFixtureStatsRequest
            {
                TeamId = teamId,
                PlayerStats = stats.Select(s => new PlayerStatDto
                {
                    PlayerId = s.PlayerId,
                    Goals = s.Goals,
                    OwnGoals = s.OwnGoals,
                    Assists = s.Assists,
                    IsManOfTheMatch = s.IsManOfTheMatch,
                    HadYellowCard = s.HadYellowCard,
                    HadRedCard = s.HadRedCard
                }).ToList()
            };
            await _httpClient.PostOk($"api/fixtures/{fixtureId}/stats", request, "Unable to submit stats.");
        }

        public Task<List<PlayerStatDto>> GetFixtureStats(int fixtureId) =>
            _httpClient.GetListOrEmpty<PlayerStatDto>($"api/fixtures/{fixtureId}/stats");

        public Task<List<PlayerDto>> GetFixturePlayers(int fixtureId, int? teamId = null) =>
            _httpClient.GetListOrEmpty<PlayerDto>(teamId.HasValue
                ? $"api/fixtures/{fixtureId}/players?teamId={teamId}"
                : $"api/fixtures/{fixtureId}/players");

        public Task<List<HeadToHeadResultDto>> GetHeadToHead(int fixtureId) =>
            _httpClient.GetListOrEmpty<HeadToHeadResultDto>($"api/fixtures/{fixtureId}/head-to-head");

        public async Task SaveCaptaincy(int fixtureId, int? captainId, int? viceId, int? teamId = null)
        {
            await _httpClient.PutOk($"api/fixtures/{fixtureId}/captaincy",
                new SaveCaptaincyRequest { CaptainPlayerId = captainId, ViceCaptainPlayerId = viceId, TeamId = teamId },
                "Unable to save the captains.");
        }

        public Task<List<OpponentPlayerStatDto>> GetOpponentStats(int fixtureId, int? teamId = null) =>
            _httpClient.GetListOrEmpty<OpponentPlayerStatDto>(teamId.HasValue
                ? $"api/fixtures/{fixtureId}/opponent-stats?teamId={teamId}"
                : $"api/fixtures/{fixtureId}/opponent-stats");

        // ── True man of the match (admin only) ─────────────────────────────
        // The API forbids these to anyone who is not a site admin, including
        // referees, so a non-admin simply gets nothing back rather than an error.

        public Task<TrueMotmDto?> GetTrueMotm(int fixtureId) =>
            _httpClient.GetOrDefault<TrueMotmDto>($"api/fixtures/{fixtureId}/true-motm");

        public Task SaveTrueMotm(int fixtureId, int? playerId) =>
            _httpClient.PutOk($"api/fixtures/{fixtureId}/true-motm",
                new SetTrueMotmRequest { PlayerId = playerId }, "Couldn't save the true man of the match.");

        public Task<List<TrueMotmRowDto>> GetTrueMotmLog() =>
            _httpClient.GetListOrEmpty<TrueMotmRowDto>("api/fixtures/true-motm");

        public Task<List<FixtureSquadPlayerDto>> GetFixtureSquad(int fixtureId) =>
            _httpClient.GetListOrEmpty<FixtureSquadPlayerDto>($"api/fixtures/{fixtureId}/squad");
    }
}
