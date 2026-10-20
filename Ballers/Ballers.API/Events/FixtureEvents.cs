namespace Ballers.API.Events
{
    // A side sent its stats; a null team means both were sent at once.
    public sealed record FixtureStatsSubmitted(int FixtureId, int HomeTeamId, int AwayTeamId, int? TeamId) : IDomainEvent;

    public sealed record FixtureRescheduled(int FixtureId, int HomeTeamId, int AwayTeamId, DateTime? Kickoff, string? Location) : IDomainEvent;

    // Team ids are after the swap: the new home side used to be away.
    public sealed record FixtureSidesSwapped(int FixtureId, int HomeTeamId, int AwayTeamId, bool ByAdmin) : IDomainEvent;
}
