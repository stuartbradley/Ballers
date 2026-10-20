using Ballers.Services;
using Ballers.Shared;

namespace Ballers.ViewModels
{
    /// <summary>
    /// The admin overview: the season's progress, what needs attention, recent
    /// activity, the league-wide switches and the card points payout.
    /// </summary>
    public class AdminViewModel : ViewModelBase
    {
        private readonly AdminService _admin;
        private readonly LeagueSettingsService _settings;

        public AdminViewModel(AdminService admin, LeagueSettingsService settings)
        {
            _admin = admin;
            _settings = settings;
        }

        public AdminOverviewDto? Overview { get; private set; }
        public bool Loading { get; private set; } = true;
        public bool ShowAllAttention { get; set; }
        public bool RunningPayout { get; private set; }
        public string? SwitchMessage { get; private set; }
        public bool SwitchIsError { get; private set; }

        public async Task Load()
        {
            Loading = true;
            Overview = await _admin.GetOverview();
            Loading = false;
        }

        /// <param name="which">"players", "fixtures" or "hidden".</param>
        public async Task SetSwitch(string which, bool value)
        {
            if (Overview == null) return;
            SwitchMessage = null;
            try
            {
                switch (which)
                {
                    case "players":
                        await _settings.SetPlayersLocked(value);
                        Overview.PlayersLocked = value;
                        SwitchMessage = value ? "Player registration locked." : "Player registration open.";
                        break;
                    case "fixtures":
                        await _settings.SetFixturesLocked(value);
                        Overview.FixturesLocked = value;
                        SwitchMessage = value ? "Fixtures locked for editing." : "Fixtures open for editing.";
                        break;
                    case "hidden":
                        await _settings.SetFixturesHidden(value);
                        Overview.FixturesHidden = value;
                        SwitchMessage = value ? "Fixtures hidden from the public." : "Fixtures visible to the public.";
                        break;
                }
                SwitchIsError = false;
            }
            catch (Exception ex)
            {
                SwitchIsError = true;
                SwitchMessage = ex.Message;
            }
        }

        public async Task RunPayout()
        {
            RunningPayout = true;
            SwitchMessage = null;
            try
            {
                var (fixtures, players, points) = await _admin.RunPlayerPoints();
                SwitchIsError = false;
                SwitchMessage = points == 0
                    ? "Nothing to pay out: every played fixture is already settled."
                    : $"Credited {points} point(s) to {players} player(s) across {fixtures} fixture(s).";
                Overview = await _admin.GetOverview() ?? Overview;
            }
            catch (Exception ex)
            {
                SwitchIsError = true;
                SwitchMessage = ex.Message;
            }
            finally
            {
                RunningPayout = false;
            }
        }
    }
}
