namespace Ballers.Shared
{
    /// <summary>
    /// Everything a manager's dashboard shows: their club's season, the next
    /// game and how ready they are for it, recent results, and the jobs still
    /// to do.
    /// </summary>
    public class ManagerDashboardDto
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = "";
        public string? ProfileImageUrl { get; set; }
        public string? HomeKitColour { get; set; }
        public bool HasProfile { get; set; }

        // Null when there's no season yet.
        public int? SeasonNumber { get; set; }

        // From the same overview the Teams page uses.
        public TeamOverviewDto? Standing { get; set; }
        public int TeamsInLeague { get; set; }

        public string? TopScorerName { get; set; }
        public int TopScorerGoals { get; set; }
        public string? TopAssisterName { get; set; }
        public int TopAssisterAssists { get; set; }

        public ManagerMatchDto? NextMatch { get; set; }
        public List<ManagerResultDto> RecentResults { get; set; } = new();
        public List<ManagerTodoDto> Todos { get; set; } = new();

        public int PendingSignups { get; set; }
        public bool FixturesLocked { get; set; }
        public bool PlayersLocked { get; set; }
    }

    public class ManagerMatchDto
    {
        public int FixtureId { get; set; }
        public string Opponent { get; set; } = "";
        public string? OpponentImageUrl { get; set; }
        public bool IsHome { get; set; }
        public DateTime? Kickoff { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
        public string? Location { get; set; }
        public string? Postcode { get; set; }
        public string? RefereeName { get; set; }
        public string? Competition { get; set; }

        // How ready the manager is: squad picked (11 or more) and captain named.
        public int SquadCount { get; set; }
        public bool SquadPicked { get; set; }
        public string? CaptainName { get; set; }
    }

    public class ManagerResultDto
    {
        public int FixtureId { get; set; }
        public string Opponent { get; set; } = "";
        public bool IsHome { get; set; }
        public DateTime When { get; set; }

        // Counted once both sides have sent their stats; until then the score
        // isn't final.
        public bool IsPlayed { get; set; }
        public int TeamScore { get; set; }
        public int OpponentScore { get; set; }
        public bool OurStatsIn { get; set; }
        public bool TheirStatsIn { get; set; }
    }

    public class ManagerTodoDto
    {
        // "squad", "captain", "details", "stats", "signups", "profile", "players".
        public string Kind { get; set; } = "";
        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";
        public string? Link { get; set; }
        public bool Done { get; set; }
        public bool Urgent { get; set; }
    }
}
