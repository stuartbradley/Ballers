namespace Ballers.API.Models
{
    public class Fixture
    {
        public int Id { get; set; }
        public int HomeTeamId { get; set; }
        public int AwayTeamId {  get; set; }
        public DateTime? Kickoff { get; set; }
        public string? Location { get; set; } = "";
        public string? Postcode { get; set; }
        public bool IsPlayed { get; set; } = false;
        public int SeasonId {  get; set; }
        public Season? Season { get; set; }
        public Team? HomeTeam { get; set; }
        public Team? AwayTeam { get; set; }
        public int MatchNumber { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public int? RefereeId { get; set; }
        public Referee? Referee { get; set; }

        public int? HomeCaptainId { get; set; }
        public Player? HomeCaptain { get; set; }
        public int? HomeViceCaptainId { get; set; }
        public Player? HomeViceCaptain { get; set; }
        public int? AwayCaptainId { get; set; }
        public Player? AwayCaptain { get; set; }
        public int? AwayViceCaptainId { get; set; }
        public Player? AwayViceCaptain { get; set; }
    }
}
