namespace Ballers.Shared
{
    public record TotwWeekRefDto(int MatchNumber, DateTime Date);

    public record TeamOfTheWeekDto(
        int Id,
        int SeasonId,
        int MatchNumber,
        DateTime GeneratedAtUtc,
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
