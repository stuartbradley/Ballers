namespace Ballers.Models
{
    public class FixtureWeekDto
    {
        public int WeekNumber { get; set; }
        public string DateRange { get; set; } = "";
        public List<FixtureMatchDto> Matches { get; set; } = new();
        public List<string> Byes { get; set; } = new();
    }

    public class FixtureMatchDto
    {
        /// <summary>Needed so the schedule can link a match to its fixture page and gallery.</summary>
        public int Id { get; set; }

        public string Home { get; set; } = "";
        public string Away { get; set; } = "";
        public string? HomeLogo { get; set; }
        public string? AwayLogo { get; set; }
        public string Day { get; set; } = "";
        public string Time { get; set; } = "";
        public string Location { get; set; } = "";
        public string Postcode { get; set; } = "";
        public bool IsPlayed { get; set; }
        // Awarded 3-0 by the admin; there are no scorers to list.
        public bool IsWalkover { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public List<GoalScorerDto> HomeScorers { get; set; } = new();
        public List<GoalScorerDto> AwayScorers { get; set; } = new();
    }

    public class GoalScorerDto
    {
        public string Name { get; set; } = "";
        public int Goals { get; set; }

        // Own goals are listed under the team that benefited, so the name needs
        // marking to make clear it belongs to the other side's squad.
        public bool IsOwnGoal { get; set; }
    }
}
