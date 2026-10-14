namespace Ballers.Shared
{
    /// <summary>
    /// One club as the teams page and the club page show it: its place in the
    /// current season's league table, its recent form, and how its squad rates
    /// on the player cards. Everything here is public.
    /// </summary>
    public class TeamOverviewDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? ProfileImageUrl { get; set; }
        public string? ManagerName { get; set; }
        public string? HomeKitColour { get; set; }

        // Null when the team has no fixtures in the current season.
        public int? Position { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Drawn { get; set; }
        public int Lost { get; set; }
        public int Points { get; set; }
        public int GoalDifference { get; set; }

        // The last five results, oldest first: "W", "D" or "L".
        public List<string> Form { get; set; } = new();

        // The average overall of the active players who have a card, and the tier
        // that average falls in. Null until anyone in the squad has a card.
        public int? SquadOverall { get; set; }
        public string? SquadTier { get; set; }
        public int CardCount { get; set; }
        public int SquadSize { get; set; }

        public TeamTopPlayerDto? TopPlayer { get; set; }
    }

    /// <summary>The highest-rated card in a squad.</summary>
    public class TeamTopPlayerDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Number { get; set; }
        public string Position { get; set; } = "";
        public int Overall { get; set; }
        public string Tier { get; set; } = "bronze";
    }
}
