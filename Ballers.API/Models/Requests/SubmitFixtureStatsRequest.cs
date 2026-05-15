namespace Ballers.API.Models.Requests
{
    public class SubmitFixtureStatsRequest
    {
        public List<PlayerStatDto> PlayerStats { get; set; } = new();
    }
    public class PlayerStatDto
    {
        public int PlayerId { get; set; }   
        public int Goals { get; set; }
        public int Assists { get; set; }
        public bool IsManOfTheMatch { get; set; }
        public bool HadYellowCard { get; set; }
        public bool HadRedCard { get; set; }

    }
}
