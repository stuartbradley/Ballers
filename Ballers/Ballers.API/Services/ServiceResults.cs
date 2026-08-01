namespace Ballers.API.Services
{
    public record PenaltyKickResult(int Order, int PlayerId, string PlayerName, bool Scored);
    public record PlayerSummary(int Id, string Name);
    public record PlayerDetail(int Id, string Name, int Number, string Position, int TeamId, string? ImageUrl);
    public record SquadEntry(int PlayerId, string Name);
    public record FixtureDetail(
        int Id, string HomeTeam, string AwayTeam,
        int HomeTeamId, int AwayTeamId,
        DateTime? Kickoff, string? Location, string? Postcode, int Week, bool IsPlayed,
        int HomeScore, int AwayScore,
        int? RefereeId = null, string? RefereeName = null,
        DateTime WindowStart = default, DateTime WindowEnd = default)
    {
        // No time-based grace period — fixtures stay editable until the admin
        // locks them via the global "Lock all fixtures" switch.
        public bool IsEditLocked => false;
        public bool IsKnockout { get; init; }
        public string KnockoutTournament { get; init; } = "";
        public string KnockoutRound { get; init; } = "";
        public int? HomeCaptainId { get; init; }
        public string? HomeCaptainName { get; init; }
        public int? HomeViceCaptainId { get; init; }
        public string? HomeViceCaptainName { get; init; }
        public int? AwayCaptainId { get; init; }
        public string? AwayCaptainName { get; init; }
        public int? AwayViceCaptainId { get; init; }
        public string? AwayViceCaptainName { get; init; }
        // A result is only counted once both are true; see Fixture.IsPlayed.
        public bool HomeStatsSubmitted { get; init; }
        public bool AwayStatsSubmitted { get; init; }
    }
    public record HeadToHeadResult(
        int FixtureId, string HomeTeam, string AwayTeam,
        int HomeScore, int AwayScore, DateTime? Kickoff, DateTime WindowEnd);
    public record FixtureSummary(
        int Id, string HomeTeam, string AwayTeam, int Week,
        DateTime? Kickoff, string? Location, bool IsPlayed, bool IsHomeTeamManager)
    {
        public bool ManagerStatsSubmitted { get; init; }
        public bool ManagerSquadSubmitted { get; init; }
        public bool IsKnockout { get; init; }
        public string KnockoutTournament { get; init; } = "";
        public string KnockoutRound { get; init; } = "";
    }
    public record PlayerGoalsStat(int PlayerId, string Name, int Goals);
    public record PlayerAssistsStat(int PlayerId, string Name, int Assists);
    public record PlayerMotmStat(int PlayerId, string Name, int Motm);
    public record WinLossDrawResult(int Wins, int Losses, int Draws);
    public record OpponentPlayerStat(string Name, int Goals, int Assists, bool IsMotm, bool YellowCard, bool RedCard);
    public record PlayerLeaderboardEntry(
        int PlayerId, string Name, string Team, string Position,
        int Appearances, int Goals, int Assists, int CleanSheets,
        int YellowCards, int RedCards);
}
