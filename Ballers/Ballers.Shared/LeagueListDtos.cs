namespace Ballers.Shared
{
    /// <summary>
    /// One fixture in a fixtures list, from the viewer's side: the manager's
    /// own team for a manager, the home side for admins and referees.
    /// </summary>
    public class FixtureListItemDto
    {
        public int Id { get; set; }
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }
        public int Week { get; set; }
        public DateTime? Kickoff { get; set; }
        public string? Location { get; set; }
        public string? Postcode { get; set; }
        public string? RefereeName { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }

        public int SeasonId { get; set; }
        public int SeasonNumber { get; set; }
        public bool SeasonIsActive { get; set; }

        public bool IsPlayed { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public bool HomeStatsSubmitted { get; set; }
        public bool AwayStatsSubmitted { get; set; }

        public bool IsKnockout { get; set; }
        public string KnockoutTournament { get; set; } = "";
        public string KnockoutRound { get; set; } = "";

        // True for admins and referees, who act as the home side.
        public bool IsHomeTeamManager { get; set; }

        // The manager's own side only; zero/false for admins and referees.
        public bool ManagerStatsSubmitted { get; set; }
        public bool ManagerSquadSubmitted { get; set; }
        public int ManagerSquadCount { get; set; }
        public bool ManagerCaptainSet { get; set; }
        public bool ManagerPensSubmitted { get; set; }
    }

    /// <summary>One player's season on the stats page's leaderboard.</summary>
    public class PlayerLeaderboardDto
    {
        public int PlayerId { get; set; }
        public string Name { get; set; } = "";
        public string Team { get; set; } = "";
        public int TeamId { get; set; }
        public int Number { get; set; }
        public string Position { get; set; } = "";
        public int Appearances { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int CleanSheets { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
        public int Motm { get; set; }
        public int OwnGoals { get; set; }

        // The player's card, if they have one. No photo: the leaderboard lists
        // every player, and the photos would make it heavy.
        public PlayerAttributesDto? Attributes { get; set; }
        public string? Tier { get; set; }
    }

    /// <summary>A club's record over the same played fixtures as the leaderboard.</summary>
    public class TeamStatsDto
    {
        public int TeamId { get; set; }
        public string Name { get; set; } = "";
        public string? ProfileImageUrl { get; set; }
        public string? HomeKitColour { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int CleanSheets { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
    }
}
