using Ballers.Client.Services;
using Ballers.Services;
using Ballers.Shared;

namespace Ballers.ViewModels
{
    /// <summary>
    /// Admin settings: the league-wide switches (registration, fixture editing,
    /// fixture visibility) and which notifications are sent.
    /// </summary>
    public class AdminSettingsViewModel : ViewModelBase
    {
        private readonly LeagueSettingsService _settings;
        private readonly NotificationService _notifications;

        public AdminSettingsViewModel(LeagueSettingsService settings, NotificationService notifications)
        {
            _settings = settings;
            _notifications = notifications;
        }

        public bool Loaded { get; private set; }
        public bool PlayersLocked { get; private set; }
        public bool FixturesLocked { get; private set; }
        public bool FixturesHidden { get; private set; }
        public List<NotificationSettingDto> Notifications { get; private set; } = new();
        public string? LeagueMessage { get; private set; }
        public bool LeagueIsError { get; private set; }
        public string? NotifMessage { get; private set; }

        public async Task InitializeAsync()
        {
            var leagueTask = _settings.Get();
            var notifTask = _notifications.GetSettingsAsync();
            await Task.WhenAll(leagueTask, notifTask);

            PlayersLocked = leagueTask.Result.PlayersLocked;
            FixturesLocked = leagueTask.Result.FixturesLocked;
            FixturesHidden = leagueTask.Result.FixturesHidden;
            Notifications = notifTask.Result;
            Loaded = true;
        }

        public Task SetPlayersLocked(bool v) => League(async () =>
        {
            await _settings.SetPlayersLocked(v);
            PlayersLocked = v;
            return v ? "Player registration locked." : "Player registration open.";
        });

        public Task SetFixturesLocked(bool v) => League(async () =>
        {
            await _settings.SetFixturesLocked(v);
            FixturesLocked = v;
            return v ? "Fixtures locked for editing." : "Fixtures open for editing.";
        });

        public Task SetFixturesHidden(bool v) => League(async () =>
        {
            await _settings.SetFixturesHidden(v);
            FixturesHidden = v;
            return v ? "Fixtures hidden from the public." : "Fixtures visible to the public.";
        });

        private async Task League(Func<Task<string>> change)
        {
            LeagueMessage = null;
            try
            {
                LeagueMessage = await change();
                LeagueIsError = false;
            }
            catch (Exception ex)
            {
                LeagueIsError = true;
                LeagueMessage = ex.Message;
            }
        }

        public async Task SetNotification(NotificationSettingDto n, bool on)
        {
            NotifMessage = null;
            try
            {
                await _notifications.UpdateSettingAsync(n.Type, on);
                n.IsEnabled = on;
            }
            catch (Exception ex)
            {
                NotifMessage = ex.Message;
            }
        }
    }
}
