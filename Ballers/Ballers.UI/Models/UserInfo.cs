namespace Ballers.Models
{
    public class UserInfo
    {
        public string Email { get; set; } = "";
        public int? TeamId { get; set; }
        public bool IsAdmin { get; set; }
        public string? TeamName { get; set; }
        public bool IsReferee { get; set; }

        // Set once a manager has approved this player's account. TeamId stays null
        // for players; TeamName above is the display-only team.
        public int? PlayerId { get; set; }
        public string? PlayerName { get; set; }

        // Signed up, but still waiting on their manager. Can see nothing yet.
        public bool IsPendingPlayer { get; set; }
    }
}
