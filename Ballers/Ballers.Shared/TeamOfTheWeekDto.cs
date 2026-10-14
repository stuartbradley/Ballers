namespace Ballers.Shared
{
    public record TotwWeekRefDto(int MatchNumber, DateTime Date);

    public record TeamOfTheWeekDto(
        int Id,
        int SeasonId,
        int MatchNumber,
        // The date the match week opens. Deliberately not derived from kickoffs:
        // a fixture rearranged outside its window would otherwise relabel the
        // whole week. This is a plain calendar date, not a UTC instant.
        DateTime WeekStart,
        List<TotwPlayerDto> Players)
    {
        // The week as a whole: every played game, not just the winners'.
        public TotwWeekSummaryDto? Summary { get; init; }
    }

    /// <summary>
    /// Totals for a match week. The top scorer is whoever scored most that week
    /// (Man of the Match or not); null when nobody scored.
    /// </summary>
    public record TotwWeekSummaryDto(
        int Games,
        int Goals,
        string? TopScorerName,
        string? TopScorerTeam,
        int TopScorerGoals);

    /// <summary>How many weeks a player has made the Team of the Week this season.</summary>
    public record TotwAppearanceDto(
        int PlayerId,
        string Name,
        string TeamName,
        string Position,
        int Count,
        PlayerAttributesDto? Attributes = null,
        string? Tier = null);

    /// <summary>
    /// One Man of the Match winner in the team. Goals, Assists, GoalsConceded and
    /// CleanSheets are for the winning game that week; the Season* figures are
    /// the active season so far. A clean sheet is the team's: anyone whose side
    /// conceded nothing has one, whatever their position.
    /// </summary>
    public record TotwPlayerDto(
        int PlayerId,
        string Name,
        int TeamId,
        string TeamName,
        string Position,
        bool IsGoalkeeper,
        int Goals,
        int Assists,
        int? GoalsConceded,
        int? CleanSheets,
        string? ProfileImageBase64,
        int SeasonGoals = 0,
        int SeasonAssists = 0,
        int SeasonCleanSheets = 0,
        // The player's public card, when they have signed up for one.
        PlayerAttributesDto? Attributes = null,
        string? Tier = null)
    {
        // The game that won them Man of the Match, from their side's view.
        public string? OpponentName { get; init; }
        public int? TeamScore { get; init; }
        public int? OpponentScore { get; init; }

        // Weeks they had made the team this season by this week, this one
        // included.
        public int TotwAppearances { get; init; }
    }
}
