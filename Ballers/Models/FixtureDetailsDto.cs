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
    }
}
