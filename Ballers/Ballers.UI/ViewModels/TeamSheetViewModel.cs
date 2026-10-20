using Ballers.Client.Auth;
using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;

namespace Ballers.ViewModels
{
    /// <summary>
    /// The team sheet poster for one side of a fixture: its picked squad (or
    /// the whole roster before one is picked) and both teams' badges.
    /// </summary>
    public class TeamSheetViewModel : ViewModelBase
    {
        private readonly FixtureService _fixtures;
        private readonly TeamService _teams;
        private readonly AuthService _auth;

        public TeamSheetViewModel(FixtureService fixtures, TeamService teams, AuthService auth)
        {
            _fixtures = fixtures;
            _teams = teams;
            _auth = auth;
        }

        public FixtureDetailsDto? Fixture { get; private set; }
        public List<PlayerCardDto> Squad { get; private set; } = new();
        // Paths on the API; the page makes them absolute.
        public string? HomeBadgePath { get; private set; }
        public string? AwayBadgePath { get; private set; }
        public string SheetTeamName { get; private set; } = "";
        public bool IsLoading { get; private set; } = true;
        public bool SquadIsProvisional { get; private set; }
        public int Variant { get; set; } = 1;

        public async Task LoadAsync(int fixtureId)
        {
            Fixture = await _fixtures.GetFixture(fixtureId);
            if (Fixture == null) { IsLoading = false; return; }

            var user = await _auth.GetCurrentUser();
            // The sheet is for the logged-in manager's team; fall back to the home team
            // (e.g. admin/referee viewing) so the page always renders something useful.
            var sheetTeamId = user?.TeamId ?? Fixture.HomeTeamId;
            SheetTeamName = sheetTeamId == Fixture.AwayTeamId ? Fixture.AwayTeam : Fixture.HomeTeam;

            var homeTask = _teams.GetProfile(Fixture.HomeTeamId);
            var awayTask = _teams.GetProfile(Fixture.AwayTeamId);
            var rosterTask = _teams.GetSquad(sheetTeamId);
            var pickedTask = _fixtures.GetFixtureSquad(fixtureId);
            await Task.WhenAll(homeTask, awayTask, rosterTask, pickedTask);

            HomeBadgePath = homeTask.Result?.ProfileImageUrl;
            AwayBadgePath = awayTask.Result?.ProfileImageUrl;

            // The fixture squad endpoint returns picks for both teams, so intersecting
            // with this team's roster filters out the opposition as well as unpicked players.
            var pickedIds = pickedTask.Result.Select(p => p.PlayerId).ToHashSet();
            var picked = rosterTask.Result.Where(p => pickedIds.Contains(p.Id)).ToList();

            // No squad saved for this fixture yet — show the full roster rather than a blank sheet.
            SquadIsProvisional = picked.Count == 0;
            Squad = (SquadIsProvisional ? rosterTask.Result : picked)
                .OrderBy(p => p.Number)
                .ToList();

            IsLoading = false;
        }

        public string? DownloadFileName => Fixture == null
            ? null
            : $"{Safe(Fixture.HomeTeam)}-vs-{Safe(Fixture.AwayTeam)}-teamsheet.png";

        private static string Safe(string? s)
        {
            var bad = Path.GetInvalidFileNameChars().Concat(new[] { ' ' }).ToArray();
            return string.Join("-", (s ?? "").Split(bad, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
