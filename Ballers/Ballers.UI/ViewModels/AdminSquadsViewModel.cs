using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;

namespace Ballers.ViewModels
{
    /// <summary>
    /// Admin for any team's squad: the list of teams, then one team's players
    /// to add, edit and photograph.
    /// </summary>
    public class AdminSquadsViewModel : ViewModelBase
    {
        public static readonly string[] PositionOrder = ["GK", "DEF", "MID", "FWD"];

        private readonly TeamService _teams;
        private readonly PlayerService _players;

        public AdminSquadsViewModel(TeamService teams, PlayerService players)
        {
            _teams = teams;
            _players = players;
        }

        // The team being looked at; null shows the list of teams.
        public int? TeamId { get; private set; }

        public bool Loading { get; private set; } = true;
        public List<TeamProfileDto> Teams { get; private set; } = new();
        public TeamProfileDto? CurrentTeam { get; private set; }
        public List<PlayerCardDto> Players { get; private set; } = new();

        public bool ShowCropModal { get; set; }
        public PlayerCardDto? CropPlayer { get; private set; }

        public PlayerCardDto? EditPlayer { get; private set; }
        public string EditName { get; set; } = "";
        public int EditNumber { get; set; }
        public string EditPosition { get; set; } = "MID";
        public string EditError { get; private set; } = "";

        public string NewName { get; set; } = "";
        public int NewNumber { get; set; }
        public string NewPosition { get; set; } = "MID";
        public string FormError { get; private set; } = "";

        public IEnumerable<IGrouping<string, PlayerCardDto>> GroupedPlayers =>
            Players.GroupBy(p => p.Position)
                   .OrderBy(g => Array.IndexOf(PositionOrder, g.Key) is int i && i >= 0 ? i : 99);

        public async Task LoadAsync(int? teamId)
        {
            TeamId = teamId;
            Loading = true;
            if (TeamId.HasValue)
            {
                var profile = _teams.GetProfile(TeamId.Value);
                var squad = _teams.GetSquad(TeamId.Value);
                await Task.WhenAll(profile, squad);
                CurrentTeam = profile.Result;
                Players = squad.Result;
            }
            else
            {
                Teams = (await _teams.GetAllTeams())
                    .OrderBy(t => t.Name)
                    .ToList();
            }
            Loading = false;
        }

        public async Task AddPlayer()
        {
            FormError = "";
            if (!TeamId.HasValue) return;
            if (string.IsNullOrWhiteSpace(NewName)) { FormError = "Please enter a player name."; return; }
            if (NewNumber <= 0) { FormError = "Please enter a valid squad number."; return; }

            try
            {
                await _players.AddPlayerForTeam(TeamId.Value, NewName, NewNumber, NewPosition);
                NewName = "";
                NewNumber = 0;
                NewPosition = "MID";
                await ReloadSquad();
            }
            catch
            {
                FormError = "Failed to add player. Please try again.";
            }
        }

        public static string TeamInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpper();
            return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        }

        public void OpenCropModal(PlayerCardDto player)
        {
            CropPlayer = player;
            ShowCropModal = true;
        }

        public void OpenEditModal(PlayerCardDto player)
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
                await ReloadSquad();
            }
            catch
            {
                EditError = "Failed to save. Please try again.";
            }
        }

        public async Task OnPhotoSaved()
        {
            ShowCropModal = false;
            await ReloadSquad();
        }

        private async Task ReloadSquad()
        {
            if (!TeamId.HasValue) return;
            Players = await _teams.GetSquad(TeamId.Value);
        }
    }
}
