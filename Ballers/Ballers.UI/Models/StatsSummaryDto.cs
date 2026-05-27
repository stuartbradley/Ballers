namespace Ballers.Models
{
    public class StatsSummaryDto
    {
        public List<PlayerGoalsDto> Scorers { get; set; } = new();
        public List<PlayerAssistDto> Assists { get; set; } = new();
        public List<PlayerMotmDto> Motm { get; set; } = new();
    }
}
