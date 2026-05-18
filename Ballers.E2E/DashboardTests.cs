using Microsoft.Playwright;

namespace Ballers.E2E;

[TestFixture]
public class DashboardTests : BallersTestBase
{
    [SetUp]
    public async Task SetUp() => await LoginAs(ManagerEmail, ManagerPassword);

    // ── Stats ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Dashboard_ShowsWinDrawLossRecord()
    {
        await Page.GotoAsync($"{BaseUrl}/dashboard");
        // Stats load in OnAfterRenderAsync — wait for the record block to appear
        await Page.WaitForSelectorAsync(".dash-record", new() { Timeout = 15_000 });

        await Expect(Page.Locator(".dash-win  .dash-stat-num")).ToBeVisibleAsync();
        await Expect(Page.Locator(".dash-draw .dash-stat-num")).ToBeVisibleAsync();
        await Expect(Page.Locator(".dash-loss .dash-stat-num")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Dashboard_StatLabelsAreCorrect()
    {
        await Page.GotoAsync($"{BaseUrl}/dashboard");
        await Page.WaitForSelectorAsync(".dash-record", new() { Timeout = 15_000 });

        await Expect(Page.Locator(".dash-win  .dash-stat-lbl")).ToContainTextAsync("Wins");
        await Expect(Page.Locator(".dash-draw .dash-stat-lbl")).ToContainTextAsync("Draws");
        await Expect(Page.Locator(".dash-loss .dash-stat-lbl")).ToContainTextAsync("Losses");
    }

    // ── Action tiles ──────────────────────────────────────────────────────────

    [Test]
    public async Task Dashboard_AllActionTilesAreVisible()
    {
        await Page.GotoAsync($"{BaseUrl}/dashboard");
        await Page.WaitForSelectorAsync(".action-tiles-row");

        await Expect(Page.Locator(".myteam-action")).ToBeVisibleAsync();
        await Expect(Page.Locator(".fixtures-action")).ToBeVisibleAsync();
        await Expect(Page.Locator(".squad-action")).ToBeVisibleAsync();
        await Expect(Page.Locator(".referees-action")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Dashboard_MyTeamTile_NavigatesToMyTeam()
    {
        await Page.GotoAsync($"{BaseUrl}/dashboard");
        await Page.WaitForSelectorAsync(".myteam-action");
        await Page.Locator(".myteam-action").ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(".*/my-team"));
    }

    [Test]
    public async Task Dashboard_FixturesTile_NavigatesToManagerFixtures()
    {
        await Page.GotoAsync($"{BaseUrl}/dashboard");
        await Page.WaitForSelectorAsync(".fixtures-action");
        await Page.Locator(".fixtures-action").ClickAsync();

        await Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(".*/manager-fixtures"));
    }

    [Test]
    public async Task Dashboard_MySquadTile_NavigatesToMySquad()
    {
        await Page.GotoAsync($"{BaseUrl}/dashboard");
        await Page.WaitForSelectorAsync(".squad-action");
        await Page.Locator(".squad-action").ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(".*/my-squad"));
    }

    [Test]
    public async Task Dashboard_RefereesTile_NavigatesToReferees()
    {
        await Page.GotoAsync($"{BaseUrl}/dashboard");
        await Page.WaitForSelectorAsync(".referees-action");
        await Page.Locator(".referees-action").ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(".*/referees"));
    }
}
