namespace Ballers.API.Services
{
    // One of the two referee-nominated men of the match, offered as a choice for
    // the true man of the match.
    public record TrueMotmCandidate(int PlayerId, string PlayerName, int TeamId, string TeamName);

    // The admin-only picker state for a single fixture.
    public record TrueMotmDetail(
        int FixtureId,
        string HomeTeam,
        string AwayTeam,
        int? SelectedPlayerId,
        List<TrueMotmCandidate> Candidates);

    // A settled pick, for the admin listing.
    public record TrueMotmRow(
        int FixtureId,
        DateTime? Kickoff,
        int Week,
        string HomeTeam,
        string AwayTeam,
        int HomeScore,
        int AwayScore,
        int? PlayerId,
        string? PlayerName,
        string? TeamName);
}
