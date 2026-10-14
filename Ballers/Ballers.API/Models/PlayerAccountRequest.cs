namespace Ballers.API.Models
{
    public enum PlayerAccountRequestStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    /// <summary>
    /// A player's request to be given a login tied to a squad place. Players sign
    /// themselves up, but nothing happens until their manager approves: until then
    /// the account has no role and can do nothing but see that it is waiting.
    /// </summary>
    public class PlayerAccountRequest
    {
        public int Id { get; set; }

        public string UserId { get; set; } = "";
        public ApplicationUser? User { get; set; }

        public int RequestedTeamId { get; set; }
        public Team? RequestedTeam { get; set; }

        // Null when the player says they are not on the roster yet; approving such a
        // request creates the Player row from RequestedName.
        public int? RequestedPlayerId { get; set; }
        public Player? RequestedPlayer { get; set; }

        public string RequestedName { get; set; } = "";

        public PlayerAccountRequestStatus Status { get; set; } = PlayerAccountRequestStatus.Pending;

        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedByUserId { get; set; }
    }
}
