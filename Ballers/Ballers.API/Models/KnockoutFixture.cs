namespace Ballers.API.Models
{
    public class KnockoutFixture
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public Season? Season { get; set; }
        public string Tournament { get; set; } = ""; // "Cup" or "Plate"
        public string Round { get; set; } = "";      // "Semifinal" or "Final"
        public int Slot { get; set; }                // 1 or 2 (which semi feeds the final)
        public int? HomeTeamId { get; set; }
        public Team? HomeTeam { get; set; }
        public int? AwayTeamId { get; set; }
        public Team? AwayTeam { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public bool IsPlayed { get; set; }
        public int? LinkedFixtureId { get; set; }
        public Fixture? LinkedFixture { get; set; }
    }
}
