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
        List<TotwPlayerDto> Players);

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
        string? ProfileImageBase64);
}
