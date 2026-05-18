namespace Ballers.Models
{
    public class PlayerLeaderboardDto
    {
        public int PlayerId { get; set; }
        public string Name { get; set; } = "";
        public string Team { get; set; } = "";
        public string Position { get; set; } = "";
        public int Appearances { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int CleanSheets { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
    }
}
