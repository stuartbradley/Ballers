using Ballers.Models;
using Ballers.Services;
using Ballers.Shared;
using Microsoft.AspNetCore.Components.Forms;

namespace Ballers.ViewModels
{
    /// <summary>
    /// Admin for logins: list and filter them, create team, referee and admin
    /// accounts, reset a password, remove a login. The page confirms a removal
    /// before calling <see cref="RemoveAccount"/>.
    /// </summary>
    public class AdminAccountsViewModel : ViewModelBase
    {
        public static readonly string[] RoleFilters = { "All", "Admin", "Manager", "Referee", "Player", "Pending" };

        public static readonly (AccountType Type, string Label, string Help)[] AccountTypes =
        {
            (AccountType.Team, "Team", "A new club and its manager's login."),
            (AccountType.Referee, "Referee", "Sees every fixture and can record results."),
            (AccountType.Admin, "Admin", "Runs the league: everything on these pages."),
        };

        private readonly AdminService _admin;

        public AdminAccountsViewModel(AdminService admin)
        {
            _admin = admin;
            EditContext = new EditContext(Model);
        }

        public List<AdminAccountDto>? Accounts { get; private set; }
        public bool Loading { get; private set; } = true;
        public string Role { get; set; } = "All";
        public string Search { get; set; } = "";
        public string? ListMessage { get; private set; }
        public bool ListIsError { get; private set; }

        public bool ShowCreate { get; set; }
        public CreateTeamViewModel Model { get; private set; } = new();
        public EditContext EditContext { get; private set; }
        public bool Creating { get; private set; }
        public string? CreateMessage { get; private set; }
        public bool CreateIsError { get; private set; }

        public AdminAccountDto? Resetting { get; set; }
        public string NewPassword { get; set; } = "";
        public string? ResetError { get; private set; }
        public bool SavingReset { get; private set; }

        public IEnumerable<AdminAccountDto> Filtered => (Accounts ?? new())
            .Where(a => Role == "All" || a.Role == Role)
            .Where(a => string.IsNullOrWhiteSpace(Search)
                || a.Email.Contains(Search, StringComparison.OrdinalIgnoreCase)
                || (a.TeamName?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false)
                || (a.PlayerName?.Contains(Search, StringComparison.OrdinalIgnoreCase) ?? false));

        /// <param name="roleQuery">The overview links here with ?role=Pending.</param>
        public async Task InitializeAsync(string? roleQuery)
        {
            if (roleQuery != null && RoleFilters.Contains(roleQuery)) Role = roleQuery;
            await Load();
        }

        private async Task Load()
        {
            Loading = true;
            Accounts = await _admin.GetAccounts();
            Loading = false;
        }

        public void SetType(AccountType type)
        {
            Model.AccountType = type;
            if (!Model.IsTeam) Model.TeamName = "";
            EditContext.NotifyValidationStateChanged();
        }

        public async Task Create()
        {
            Creating = true;
            CreateMessage = null;
            try
            {
                switch (Model.AccountType)
                {
                    case AccountType.Referee: await _admin.CreateReferee(Model.Email, Model.Password); break;
                    case AccountType.Admin: await _admin.CreateAdmin(Model.Email, Model.Password); break;
                    default: await _admin.CreateTeam(Model.TeamName, Model.Email, Model.Password); break;
                }
                CreateIsError = false;
                CreateMessage = Model.IsTeam
                    ? $"{Model.TeamName} created, with {Model.Email} as manager."
                    : $"{TypeLabel(Model.AccountType)} account created for {Model.Email}.";

                var type = Model.AccountType;
                Model = new CreateTeamViewModel { AccountType = type };
                EditContext = new EditContext(Model);
                await Load();
            }
            catch (Exception ex)
            {
                CreateIsError = true;
                CreateMessage = ex.Message;
            }
            finally
            {
                Creating = false;
            }
        }

        public void StartReset(AdminAccountDto a)
        {
            Resetting = a;
            NewPassword = "";
            ResetError = null;
        }

        public async Task SaveReset()
        {
            if (Resetting == null) return;
            SavingReset = true;
            ResetError = null;
            try
            {
                await _admin.ResetAccountPassword(Resetting.Id, NewPassword);
                ListIsError = false;
                ListMessage = $"Password reset for {Resetting.Email}.";
                Resetting = null;
                await Load();
            }
            catch (Exception ex)
            {
                ResetError = ex.Message;
            }
            finally
            {
                SavingReset = false;
            }
        }

        // What removing this login leaves behind, for the confirmation.
        public static string RemovalConsequence(AdminAccountDto a) => a.Role switch
        {
            "Manager" => $"{a.TeamName ?? "Their team"} keeps its squad and results, but has no manager login until you create one.",
            "Player" => $"{a.PlayerName ?? "They"} stay in the squad and keep their card; they just can't log in.",
            "Pending" => "Their sign-up request goes with it.",
            _ => "They won't be able to log in."
        };

        public async Task RemoveAccount(AdminAccountDto a)
        {
            try
            {
                await _admin.DeleteAccount(a.Id);
                ListIsError = false;
                ListMessage = $"{a.Email} removed.";
                await Load();
            }
            catch (Exception ex)
            {
                ListIsError = true;
                ListMessage = ex.Message;
            }
        }

        public static string TypeLabel(AccountType t) => t switch
        {
            AccountType.Referee => "Referee",
            AccountType.Admin => "Admin",
            _ => "Team"
        };

        public static string RoleLabel(string r) => r == "Pending" ? "Awaiting approval" : r;

        public static string RoleTone(string r) => r switch
        {
            "Admin" => "is-red",
            "Manager" => "is-amber",
            "Referee" => "is-blue",
            "Player" => "is-green",
            _ => "is-violet"
        };
    }
}
