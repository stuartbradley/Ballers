namespace Ballers.Models
{
    public class FixtureDetailsDto
    {
        public int Id { get; set; }

        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";

        public int HomeScore { get; set; }
        public int AwayScore { get; set; }

        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }

        public DateTime? Kickoff { get; set; }

        public string? Location { get; set; }
        public string? Postcode { get; set; }

        public int Week { get; set; }

        public bool IsPlayed { get; set; }
        public int? RefereeId { get; set; }
        public string? RefereeName { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
        public bool IsEditLocked { get; set; }

        public int? HomeCaptainId { get; set; }
        public string? HomeCaptainName { get; set; }
        public int? HomeViceCaptainId { get; set; }
        public string? HomeViceCaptainName { get; set; }
        public int? AwayCaptainId { get; set; }
        public string? AwayCaptainName { get; set; }
        public int? AwayViceCaptainId { get; set; }
        public string? AwayViceCaptainName { get; set; }
    }
}
