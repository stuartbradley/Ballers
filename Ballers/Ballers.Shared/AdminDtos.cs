namespace Ballers.Shared
{
    /// <summary>
    /// The state of the league at a glance, for the admin overview: where the
    /// active season is, what needs sorting out, and what has happened lately.
    /// </summary>
    public class AdminOverviewDto
    {
        // Null when no season is active.
        public int? SeasonNumber { get; set; }
        public DateTime? SeasonStart { get; set; }
        public DateTime? SeasonEnd { get; set; }

        // The match week whose window is open now (or the next to open); 0 when
        // the season has no fixtures.
        public int CurrentWeek { get; set; }
        public int TotalWeeks { get; set; }
        public int FixturesPlayed { get; set; }
        public int FixturesTotal { get; set; }

        public int Teams { get; set; }
        public int Players { get; set; }
        public int PlayersWithCards { get; set; }
        public int PendingSignups { get; set; }
        public DateTime? LastPointsRunAt { get; set; }

        public bool PlayersLocked { get; set; }
        public bool FixturesLocked { get; set; }
        public bool FixturesHidden { get; set; }

        public List<AdminAttentionDto> Attention { get; set; } = new();
        public List<AdminActivityDto> Activity { get; set; } = new();
    }

    /// <summary>Something in the active season that an admin should look at.</summary>
    public class AdminAttentionDto
    {
        // "stats", "result", "referee", "kickoff" or "signup".
        public string Kind { get; set; } = "";
        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";
        public string? Link { get; set; }
        public DateTime? When { get; set; }
    }

    /// <summary>Something that happened recently, newest first.</summary>
    public class AdminActivityDto
    {
        // "result", "signup", "gallery", "motd" or "payout".
        public string Kind { get; set; } = "";
        public string Text { get; set; } = "";
        public string? Link { get; set; }
        public DateTime When { get; set; }
    }

    /// <summary>One login, for the admin accounts list.</summary>
    public class AdminAccountDto
    {
        public string Id { get; set; } = "";
        public string Email { get; set; } = "";

        // "Admin", "Manager", "Referee", "Player" or "Pending" (a player
        // waiting for their manager's approval, or turned down).
        public string Role { get; set; } = "";

        // The manager's team, or the team a player plays for or asked to join.
        public string? TeamName { get; set; }
        public string? PlayerName { get; set; }
        public bool IsYou { get; set; }
        public bool EmailConfirmed { get; set; }
        public bool LockedOut { get; set; }
    }

    public class AdminResetPasswordRequest
    {
        public string NewPassword { get; set; } = "";
    }
}
