using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;

namespace Ballers.ViewModels
{
    /// <summary>
    /// A player asking for an account: pick a team, optionally claim a place in
    /// its squad, and send the request to that team's manager.
    /// </summary>
    public class PlayerSignupViewModel : ViewModelBase
    {
        private readonly PlayerAccountService _accounts;
        private readonly TeamService _teams;

        public PlayerSignupViewModel(PlayerAccountService accounts, TeamService teams)
        {
            _accounts = accounts;
            _teams = teams;
        }

        public List<TeamProfileDto> Teams { get; private set; } = new();
        public List<ClaimablePlayerDto> Squad { get; private set; } = new();

        public int? TeamId { get; private set; }
        public int? PlayerId { get; private set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
        public string? TeamName { get; private set; }

        public bool LoadingSquad { get; private set; }
        public bool Saving { get; private set; }
        public bool Submitted { get; private set; }
        public string? Error { get; private set; }

        public async Task InitializeAsync() => Teams = await _teams.GetAllTeams();

        public async Task SelectTeam(int? teamId)
        {
            Error = null;
            PlayerId = null;
            Squad = new();

            TeamId = teamId;
            TeamName = Teams.FirstOrDefault(t => t.Id == TeamId)?.Name;

            if (TeamId == null) return;

            LoadingSquad = true;
            NotifyChanged();
            Squad = await _accounts.GetClaimablePlayers(TeamId.Value);
            LoadingSquad = false;
        }

        public void SelectPlayer(int? playerId)
        {
            Error = null;
            PlayerId = playerId;

            // Claiming a squad place fills the name in from it, so the manager sees a
            // request that matches a row they recognise.
            var claimed = Squad.FirstOrDefault(p => p.Id == PlayerId);
            if (claimed != null) Name = claimed.Name;
        }

        public async Task Submit()
        {
            Error = Validate();
            if (Error != null) return;

            Saving = true;

            try
            {
                await _accounts.Register(new PlayerSignupRequest
                {
                    Email = Email!.Trim(),
                    Password = Password!,
                    TeamId = TeamId!.Value,
                    PlayerId = PlayerId,
                    Name = Name!.Trim()
                });

                Submitted = true;
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
            finally
            {
                Saving = false;
            }
        }

        private string? Validate()
        {
            if (TeamId == null) return "Choose your team.";
            if (string.IsNullOrWhiteSpace(Name)) return "Enter your name.";
            if (string.IsNullOrWhiteSpace(Email)) return "Enter your email address.";
            if (string.IsNullOrWhiteSpace(Password)) return "Enter a password.";
            if (Password != ConfirmPassword) return "The two passwords don't match.";
            return null;
        }
    }
}
