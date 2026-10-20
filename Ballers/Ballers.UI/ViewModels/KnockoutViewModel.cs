using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;
using Microsoft.AspNetCore.Components.Authorization;

namespace Ballers.ViewModels
{
    /// <summary>
    /// The season's knockout cups: semi-finals, finals and winners. Admins draw
    /// the bracket and enter results.
    /// </summary>
    public class KnockoutViewModel : ViewModelBase
    {
        private readonly KnockoutService _knockout;
        private readonly SeasonService _seasons;
        private readonly AuthenticationStateProvider _auth;

        public KnockoutViewModel(KnockoutService knockout, SeasonService seasons, AuthenticationStateProvider auth)
        {
            _knockout = knockout;
            _seasons = seasons;
            _auth = auth;
        }

        public KnockoutBracketDto? Bracket { get; private set; }
        public bool IsLoading { get; private set; } = true;
        public bool IsAdmin { get; private set; }
        public int? UserTeamId { get; private set; }
        public bool IsGenerating { get; private set; }
        public string GenerateMessage { get; private set; } = "";
        public bool GenerateIsError { get; private set; }
        public string ResultMessage { get; private set; } = "";
        public bool ResultIsError { get; private set; }

        public async Task InitializeAsync()
        {
            var authState = await _auth.GetAuthenticationStateAsync();
            IsAdmin = authState.User.IsInRole("Admin");
            var teamIdClaim = authState.User.FindFirst("TeamId")?.Value;
            if (teamIdClaim != null && int.TryParse(teamIdClaim, out var tid))
                UserTeamId = tid;

            var season = await _seasons.GetCurrentSeason();
            if (season != null)
                Bracket = await _knockout.GetBracketAsync(season.Id);

            IsLoading = false;
        }

        public List<KnockoutFixtureDto> GetSemifinals(string tournament)
            => Bracket?.Fixtures
                .Where(f => f.Tournament == tournament && f.Round == "Semifinal")
                .OrderBy(f => f.Slot)
                .ToList() ?? new();

        public KnockoutFixtureDto? GetFinal(string tournament)
            => Bracket?.Fixtures.FirstOrDefault(f => f.Tournament == tournament && f.Round == "Final");

        public string? GetWinner(string tournament)
        {
            var final = GetFinal(tournament);
            if (final == null || !final.IsPlayed) return null;
            return final.HomeScore > final.AwayScore ? final.HomeTeamName : final.AwayTeamName;
        }

        public async Task GenerateKnockout()
        {
            IsGenerating = true;
            GenerateMessage = "";
            try
            {
                var season = await _seasons.GetCurrentSeason();
                if (season == null) throw new Exception("No active season found.");
                await _knockout.GenerateKnockoutAsync(season.Id);
                Bracket = await _knockout.GetBracketAsync(season.Id);
                GenerateMessage = "Knockout bracket generated.";
                GenerateIsError = false;
            }
            catch (Exception ex)
            {
                GenerateMessage = ex.Message;
                GenerateIsError = true;
            }
            IsGenerating = false;
        }

        public async Task HandleResultSubmit((int Id, int HomeScore, int AwayScore) result)
        {
            ResultMessage = "";
            try
            {
                await _knockout.SubmitResultAsync(result.Id, result.HomeScore, result.AwayScore);
                var season = await _seasons.GetCurrentSeason();
                if (season != null)
                    Bracket = await _knockout.GetBracketAsync(season.Id);
                ResultMessage = "Result saved.";
                ResultIsError = false;
            }
            catch (Exception ex)
            {
                ResultMessage = ex.Message;
                ResultIsError = true;
            }
        }
    }
}
