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
    }
}
