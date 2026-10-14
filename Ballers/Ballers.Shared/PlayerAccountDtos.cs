namespace Ballers.Shared
{
    public enum PlayerAccountRequestStatusDto
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    /// <summary>
    /// A player signing themselves up. The account is created immediately but has
    /// no role and can do nothing until their manager approves the request.
    /// </summary>
    public class PlayerSignupRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public int TeamId { get; set; }

        // The squad place they say is theirs. Null means "I'm not on the roster
        // yet" — approving creates the player from Name.
        public int? PlayerId { get; set; }
        public string Name { get; set; } = "";
    }

    /// <summary>A pending request as the manager sees it in their approval queue.</summary>
    public class PlayerAccountRequestDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = "";
        public int TeamId { get; set; }
        public string? TeamName { get; set; }
        public int? PlayerId { get; set; }
        public string? PlayerName { get; set; }
        public string RequestedName { get; set; } = "";
        public PlayerAccountRequestStatusDto Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// What a logged-in player account needs to know about its own standing: an
    /// account whose request is still pending can see nothing else.
    /// </summary>
    public class PlayerAccountStatusDto
    {
        public bool HasPendingRequest { get; set; }
        public bool IsApproved { get; set; }
        public bool WasRejected { get; set; }
        public string? TeamName { get; set; }
        public string? RequestedName { get; set; }
    }

    /// <summary>A squad place a signing-up player can claim, for the signup picker.</summary>
    public class ClaimablePlayerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Number { get; set; }
        public string Position { get; set; } = "MID";

        // False when someone already holds this squad place, so the picker can
        // show it as taken rather than letting two people claim one shirt.
        public bool IsClaimable { get; set; }
    }
}
