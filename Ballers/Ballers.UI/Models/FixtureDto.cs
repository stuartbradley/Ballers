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
    }
}
