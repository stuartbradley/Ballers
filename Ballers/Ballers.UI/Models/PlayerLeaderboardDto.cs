namespace Ballers.Models
{
    public class PlayerLeaderboardDto
    {
        public int PlayerId { get; set; }
        public string Name { get; set; } = "";
        public string Team { get; set; } = "";
        public int TeamId { get; set; }
        public int Number { get; set; }
        public string Position { get; set; } = "";
        public int Appearances { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int CleanSheets { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
        public int Motm { get; set; }
        public int OwnGoals { get; set; }

        // The player's card; null when they don't have one.
        public Ballers.Shared.PlayerAttributesDto? Attributes { get; set; }
        public string? Tier { get; set; }
    }

    public class TeamStatsDto
    {
        public int TeamId { get; set; }
        public string Name { get; set; } = "";
        public string? ProfileImageUrl { get; set; }
        public string? HomeKitColour { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int CleanSheets { get; set; }
        public int YellowCards { get; set; }
        public int RedCards { get; set; }
    }
}
