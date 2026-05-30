namespace Ballers.API.Models
{
    public class TeamOfTheWeek
    {
        public int Id { get; set; }
        public int SeasonId { get; set; }
        public Season? Season { get; set; }
        public int MatchNumber { get; set; }
        public DateTime GeneratedAtUtc { get; set; }

        public ICollection<TeamOfTheWeekPlayer> Players { get; set; } = new List<TeamOfTheWeekPlayer>();
    }

    public class TeamOfTheWeekPlayer
    {
        public int Id { get; set; }
        public int TeamOfTheWeekId { get; set; }
        public TeamOfTheWeek? TeamOfTheWeek { get; set; }

        public int PlayerId { get; set; }
        public Player? Player { get; set; }

        public bool IsGoalkeeper { get; set; }
        public int Goals { get; set; }
        public int Assists { get; set; }
        public int? GoalsConceded { get; set; }
        public int? CleanSheets { get; set; }
    }
}
