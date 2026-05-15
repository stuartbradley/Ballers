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
        public string Day { get; set; } = "";
        public string Time { get; set; } = "";
        public string Location { get; set; } = "";
    }
}
