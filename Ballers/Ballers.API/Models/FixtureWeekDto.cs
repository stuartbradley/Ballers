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
        public string Home { get; set; } = "";
        public string Away { get; set; } = "";
        public string? HomeLogo { get; set; }
        public string? AwayLogo { get; set; }
        public string Day { get; set; } = "";
        public string Time { get; set; } = "";
        public string Location { get; set; } = "";
        public string Postcode { get; set; } = "";
        public bool IsPlayed { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public List<GoalScorerDto> HomeScorers { get; set; } = new();
        public List<GoalScorerDto> AwayScorers { get; set; } = new();
    }

    public class GoalScorerDto
    {
        public string Name { get; set; } = "";
        public int Goals { get; set; }
    }
}
