namespace Ballers.Models
{
    // One of the referee's per-team men of the match, offered as a choice for the
    // true man of the match.
    public class TrueMotmCandidateDto
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public int TeamId { get; set; }
        public string TeamName { get; set; } = "";
    }

    // Admin-only picker state for one fixture.
    public class TrueMotmDto
    {
        public int FixtureId { get; set; }
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public int? SelectedPlayerId { get; set; }
        public List<TrueMotmCandidateDto> Candidates { get; set; } = new();
    }

    // A settled pick, for the admin listing.
    public class TrueMotmRowDto
    {
        public int FixtureId { get; set; }
        public DateTime? Kickoff { get; set; }
        public int Week { get; set; }
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public int? PlayerId { get; set; }
        public string? PlayerName { get; set; }
        public string? TeamName { get; set; }
    }
}
