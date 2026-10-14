using Microsoft.AspNetCore.Identity;

namespace Ballers.API.Models
{
    public class ApplicationUser:IdentityUser
    {
        // Set for managers only. Deliberately left null on player accounts:
        // NotificationService.CreateForTeamAsync fans a team notification out to
        // every user with that TeamId, so giving players one would mail them all
        // of their manager's notifications.
        public int? TeamId { get; set; }
        public Team? Team { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsReferee { get; set; }

        // Set once a manager approves the player's account request. Null means the
        // account is either not a player's or is still waiting for approval.
        public int? PlayerId { get; set; }
        public Player? Player { get; set; }

    }
}
