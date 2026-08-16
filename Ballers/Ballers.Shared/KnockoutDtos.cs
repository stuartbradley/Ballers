namespace Ballers.Shared
{
    public class KnockoutGoalScorerDto
    {
        public string PlayerName { get; set; } = "";
        public int Goals { get; set; }

        // Own goals are listed under the team that benefited, so the name needs
        // marking to make clear it belongs to the other side's squad.
        public bool IsOwnGoal { get; set; }
    }

    public class KnockoutFixtureDto
    {
        public int Id { get; set; }
        public string Tournament { get; set; } = "";
        public string Round { get; set; } = "";
        public int Slot { get; set; }
        public string HomeTeamName { get; set; } = "TBD";
        public string AwayTeamName { get; set; } = "TBD";
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public bool IsPlayed { get; set; }
        public int? LinkedFixtureId { get; set; }
        public int? HomeTeamId { get; set; }
        public int? AwayTeamId { get; set; }
        public List<KnockoutGoalScorerDto> HomeGoalScorers { get; set; } = new();
        public List<KnockoutGoalScorerDto> AwayGoalScorers { get; set; } = new();
    }

    public class KnockoutBracketDto
    {
        public bool Generated { get; set; }
        public int SeasonId { get; set; }
        public int SeasonNumber { get; set; }
        public List<KnockoutFixtureDto> Fixtures { get; set; } = new();
    }

    public class SubmitKnockoutResultRequest
    {
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
    }
}
