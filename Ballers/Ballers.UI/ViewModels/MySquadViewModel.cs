using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;
using Microsoft.AspNetCore.Components.Authorization;

namespace Ballers.ViewModels
{
    /// <summary>
    /// A manager's squad: add, edit and remove players, set their photos, and
    /// approve or turn down players asking to join.
    /// </summary>
    public class MySquadViewModel : ViewModelBase
    {
        public static readonly string[] PositionOrder = ["GK", "DEF", "MID", "FWD"];

        private readonly PlayerService _players;
        private readonly LeagueSettingsService _settings;
        private readonly PlayerAccountService _accounts;
        private readonly AuthenticationStateProvider _auth;

        public MySquadViewModel(PlayerService players, LeagueSettingsService settings,
            PlayerAccountService accounts, AuthenticationStateProvider auth)
        {
            _players = players;
            _settings = settings;
            _accounts = accounts;
            _auth = auth;
        }

        public List<PlayerDto> Players { get; private set; } = new();
        public bool IsLoading { get; private set; } = true;
        public bool NoTeamAssigned { get; private set; }
        public bool PlayersLocked { get; private set; }
        public bool IsAdmin { get; private set; }
        public string NewName { get; set; } = "";
        public int NewNumber { get; set; }
        public string NewPosition { get; set; } = "MID";
        public string FormError { get; private set; } = "";

        public bool ShowCropModal { get; set; }
        public PlayerDto? CropPlayer { get; private set; }

        public List<PlayerAccountRequestDto> AccountRequests { get; private set; } = new();
        public int? ResolvingRequestId { get; private set; }
        public string RequestError { get; private set; } = "";

        public PlayerDto? EditPlayer { get; private set; }
        public string EditName { get; set; } = "";
        public int EditNumber { get; set; }
        public string EditPosition { get; set; } = "MID";
        public string EditError { get; private set; } = "";

        public IEnumerable<IGrouping<string, PlayerDto>> GroupedPlayers =>
            Players.GroupBy(p => p.Position)
                   .OrderBy(g => Array.IndexOf(PositionOrder, g.Key) is int i && i >= 0 ? i : 99);

        public async Task InitializeAsync()
        {
            var authState = await _auth.GetAuthenticationStateAsync();
            IsAdmin = authState.User.IsInRole("Admin");

            try
            {
                PlayersLocked = (await _settings.Get()).PlayersLocked;
            }
            catch { /* non-fatal: default to unlocked */ }

            await LoadPlayers();
            await LoadAccountRequests();
        }

        private async Task LoadAccountRequests()
        {
            try
            {
                AccountRequests = await _accounts.GetRequests();
            }
            catch
            {
                // A manager with no team, or an endpoint that refused: the panel just
                // stays hidden rather than breaking the squad page.
                AccountRequests = new();
            }
        }

        public Task ApproveRequest(int requestId) => ResolveRequest(requestId, approve: true);

        public Task RejectRequest(int requestId) => ResolveRequest(requestId, approve: false);

        private async Task ResolveRequest(int requestId, bool approve)
        {
            ResolvingRequestId = requestId;
            RequestError = "";

            try
            {
                if (approve)
                {
                    await _accounts.Approve(requestId);
                    // Approving can create a squad row, so the list is reloaded too.
                    await LoadPlayers();
                }
                else
                {
                    await _accounts.Reject(requestId);
                }

                await LoadAccountRequests();
            }
            catch (Exception ex)
            {
                RequestError = ex.Message;
            }
            finally
            {
                ResolvingRequestId = null;
            }
        }

        private async Task LoadPlayers()
        {
            try
            {
                Players = await _players.GetMyPlayers();
            }
            catch
            {
                NoTeamAssigned = true;
            }
            IsLoading = false;
        }

        public async Task AddPlayer()
        {
            FormError = "";
            if (string.IsNullOrWhiteSpace(NewName))
            {
                FormError = "Please enter a player name.";
                return;
            }
            if (NewNumber <= 0)
            {
                FormError = "Please enter a valid squad number.";
                return;
            }

            try
            {
                await _players.AddPlayer(NewName, NewNumber, NewPosition);
                NewName = "";
                NewNumber = 0;
                NewPosition = "MID";
                await LoadPlayers();
            }
            catch
            {
                FormError = "Failed to add player. Please try again.";
            }
        }

        public async Task RemovePlayer(int id)
        {
            FormError = "";
            try
            {
                await _players.RemovePlayer(id);
                await LoadPlayers();
            }
            catch
            {
                FormError = "Failed to remove player. Please try again.";
            }
        }

        public void OpenCropModal(PlayerDto player)
        {
            CropPlayer = player;
            ShowCropModal = true;
        }

        public void OpenEditModal(PlayerDto player)
        {
            EditPlayer = player;
            EditName = player.Name;
            EditNumber = player.Number;
            EditPosition = player.Position;
            EditError = "";
        }

        public void CloseEditModal()
        {
            EditPlayer = null;
            EditError = "";
        }

        public async Task SaveEdit()
        {
            EditError = "";
            if (string.IsNullOrWhiteSpace(EditName)) { EditError = "Name is required."; return; }
            if (EditNumber <= 0) { EditError = "Valid squad number required."; return; }

            try
            {
                await _players.UpdatePlayer(EditPlayer!.Id, EditName, EditNumber, EditPosition);
                EditPlayer = null;
                await LoadPlayers();
            }
            catch
            {
                EditError = "Failed to save. Please try again.";
            }
        }

        public async Task OnPhotoSaved()
        {
            ShowCropModal = false;
            await LoadPlayers();
        }
    }
}
