namespace Ballers.API.Services
{
    public record PenaltyKickResult(int Order, int PlayerId, string PlayerName, bool Scored);
    public record PlayerSummary(int Id, string Name);
    public record SquadEntry(int PlayerId, string Name);
    public record FixtureDetail(
        int Id, string HomeTeam, string AwayTeam,
        int HomeTeamId, int AwayTeamId,
        DateTime? Kickoff, string? Location, int Week, bool IsPlayed,
        int HomeScore, int AwayScore);
    public record FixtureSummary(
        int Id, string HomeTeam, string AwayTeam, int Week,
        DateTime? Kickoff, string? Location, bool IsPlayed, bool IsHomeTeamManager);
    public record PlayerGoalsStat(int PlayerId, string Name, int Goals);
    public record PlayerAssistsStat(int PlayerId, string Name, int Assists);
    public record PlayerMotmStat(int PlayerId, string Name, int Motm);
    public record WinLossDrawResult(int Wins, int Losses, int Draws);
}
