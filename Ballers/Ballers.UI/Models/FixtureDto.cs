namespace Ballers.Models
{
    public class FixtureDto
    {
        public int Id { get; set; }
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public DateTime? KickOff { get; set; }
        public string Location { get; set; } = "";
        public string? Postcode { get; set; }
        public int Week { get; set; }
        public bool IsPlayed { get; set; }
        public bool IsHomeTeamManager { get; set; }
        public bool ManagerStatsSubmitted { get; set; }
        public bool ManagerSquadSubmitted { get; set; }
        public bool IsKnockout { get; set; }
        public string KnockoutTournament { get; set; } = "";
        public string KnockoutRound { get; set; } = "";

        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public string? RefereeName { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
        public int SeasonId { get; set; }
        public int SeasonNumber { get; set; }
        public bool SeasonIsActive { get; set; }
        public bool HomeStatsSubmitted { get; set; }
        public bool AwayStatsSubmitted { get; set; }

        // The manager's own side only.
        public int ManagerSquadCount { get; set; }
        public bool ManagerCaptainSet { get; set; }
        public bool ManagerPensSubmitted { get; set; }
    }
}
