namespace Ballers.Models
{
    public class PlayerStatsDto
    {
        public int PlayerId { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public bool IsManOfTheMatch { get; set; }
        public bool HadYellowCard { get; set; }
        public bool HadRedCard { get; set; }
    }
}
