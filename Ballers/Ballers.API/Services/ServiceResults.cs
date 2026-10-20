namespace Ballers.API.Services
{
    public record PlayerSummary(int Id, string Name);
    public record WinLossDrawResult(int Wins, int Losses, int Draws);

    // Everything the API sends to the UI is a shared class from Ballers.Shared
    // (FixtureDetailsDto, FixtureListItemDto, PlayerLeaderboardDto and so on),
    // so the API and UI use one definition. Only types that never leave the API
    // belong here.
}
