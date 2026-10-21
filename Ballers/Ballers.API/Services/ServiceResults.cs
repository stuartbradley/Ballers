namespace Ballers.API.Services
{
    public record PenaltyKickResult(int Order, int PlayerId, string PlayerName, bool Scored);
    public record PlayerSummary(int Id, string Name);
    // A player to pick for a fixture's squad, grouped by position on the page.
    public record FixturePlayerOption(int Id, string Name, int Number, string Position);
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
        public bool IsWalkover { get; init; }
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

        // For the fixtures list's cards.
        public int HomeTeamId { get; init; }
        public int AwayTeamId { get; init; }
        public int HomeScore { get; init; }
        public int AwayScore { get; init; }
        public string? Postcode { get; init; }
        public string? RefereeName { get; init; }
        public DateTime WindowStart { get; init; }
        public DateTime WindowEnd { get; init; }
        public int SeasonId { get; init; }
        public int SeasonNumber { get; init; }
        public bool SeasonIsActive { get; init; }
        public bool HomeStatsSubmitted { get; init; }
        public bool AwayStatsSubmitted { get; init; }
        public bool IsWalkover { get; init; }

        // The manager's own side only; zero/false for admins and referees.
        public int ManagerSquadCount { get; init; }
        public bool ManagerCaptainSet { get; init; }
        public bool ManagerPensSubmitted { get; init; }
    }
    public record PlayerGoalsStat(int PlayerId, string Name, int Goals);
    public record PlayerAssistsStat(int PlayerId, string Name, int Assists);
    public record PlayerMotmStat(int PlayerId, string Name, int Motm);
    public record WinLossDrawResult(int Wins, int Losses, int Draws);
    public record OpponentPlayerStat(string Name, int Goals, int OwnGoals, int Assists, bool IsMotm, bool YellowCard, bool RedCard);
    public record PlayerLeaderboardEntry(
        int PlayerId, string Name, string Team, string Position,
        int Appearances, int Goals, int Assists, int CleanSheets,
        int YellowCards, int RedCards)
    {
        public int TeamId { get; init; }
        public int Number { get; init; }
        public int Motm { get; init; }
        public int OwnGoals { get; init; }

        // The player's card, if they have one. No photo: the leaderboard lists
        // every player, and the photos would make it heavy.
        public Ballers.Shared.PlayerAttributesDto? Attributes { get; init; }
        public string? Tier { get; init; }
    }

    // A club's record over the same played fixtures the player leaderboard uses.
    public record TeamStatsEntry(
        int TeamId, string Name, string? ProfileImageUrl, string? HomeKitColour,
        int Played, int Won, int Drawn, int Lost,
        int GoalsFor, int GoalsAgainst, int CleanSheets,
        int YellowCards, int RedCards);
}
