using Microsoft.Playwright;

namespace Ballers.E2E;

[TestFixture]
public class FixtureDetailTests : BallersTestBase
{
    private string _fixtureUrl = "";

    [SetUp]
    public async Task SetUp()
    {
        await LoginAs(ManagerEmail, ManagerPassword);
        await Page.GotoAsync($"{BaseUrl}/manager-fixtures");
        await Page.WaitForSelectorAsync(".view-btn, .edit-btn");

        // Prefer a played fixture (view-btn) so overview shows full info
        var playedCount = await Page.Locator(".view-btn").CountAsync();
        if (playedCount > 0)
            await Page.Locator(".view-btn").First.ClickAsync();
        else
            await Page.Locator(".edit-btn").First.ClickAsync();

        await Page.WaitForSelectorAsync(".fixture-card");
        _fixtureUrl = Page.Url;
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    [Test]
    public async Task FixtureDetail_Navigation_OpensCorrectPage()
    {
        await Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(".*/fixture/\\d+"));
        await Expect(Page.Locator(".fixture-teams")).ToBeVisibleAsync();
    }

    // ── Overview tab ──────────────────────────────────────────────────────────

    [Test]
    public async Task FixtureDetail_Overview_IsActiveByDefault()
    {
        var overviewTab = Page.Locator(".tab", new() { HasTextString = "Overview" });
        await Expect(overviewTab).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("active"));
    }

    [Test]
    public async Task FixtureDetail_Overview_ShowsTeamNames()
    {
        var teams = Page.Locator(".fixture-teams .team");
        Assert.That(await teams.CountAsync(), Is.EqualTo(2), "Expected home and away team spans");
        await Expect(teams.First).ToBeVisibleAsync();
        await Expect(teams.Last).ToBeVisibleAsync();
    }

    [Test]
    public async Task FixtureDetail_Overview_ShowsInfoCards()
    {
        var cards = Page.Locator(".info-card");
        // Status, Location, Kickoff, Referee — at least 3 info cards
        Assert.That(await cards.CountAsync(), Is.GreaterThanOrEqualTo(3));
        await Expect(cards.First).ToBeVisibleAsync();
    }

    [Test]
    public async Task FixtureDetail_Overview_StatusCardIsVisible()
    {
        // First info-card shows the match status label
        var statusCard = Page.Locator(".info-card").First;
        await Expect(statusCard.Locator(".label")).ToContainTextAsync("Status");
    }

    // ── Squad tab ─────────────────────────────────────────────────────────────

    [Test]
    public async Task FixtureDetail_SquadTab_ShowsTeamPlayers()
    {
        await Page.Locator(".tab", new() { HasTextString = "Squad" }).ClickAsync();
        await Page.WaitForSelectorAsync(".squad-row");

        var count = await Page.Locator(".squad-row").CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Expected seeded players to appear in squad tab");
    }

    [Test]
    public async Task FixtureDetail_SquadTab_SaveSquad_ShowsToast()
    {
        await Page.Locator(".tab", new() { HasTextString = "Squad" }).ClickAsync();
        await Page.WaitForSelectorAsync(".squad-row");

        // Select all available players
        var checkboxes = Page.Locator(".squad-row input[type='checkbox']");
        for (int i = 0; i < await checkboxes.CountAsync(); i++)
            await checkboxes.Nth(i).CheckAsync();

        await Page.Locator(".squad-save-btn").ClickAsync();

        await Expect(Page.Locator(".toast-success"))
            .ToBeVisibleAsync(new() { Timeout = 8_000 });
    }

    // ── Stats tab ─────────────────────────────────────────────────────────────

    [Test]
    public async Task FixtureDetail_StatsTab_AccessibleAfterSquadSelected()
    {
        // Stats tab is disabled until >10 squad members are saved
        await Page.Locator(".tab", new() { HasTextString = "Squad" }).ClickAsync();
        await Page.WaitForSelectorAsync(".squad-row");

        var checkboxes = Page.Locator(".squad-row input[type='checkbox']");
        var total = await checkboxes.CountAsync();
        Assert.That(total, Is.GreaterThan(10),
            "Need >10 seeded players to enable the Stats tab");

        for (int i = 0; i < total; i++)
            await checkboxes.Nth(i).CheckAsync();

        await Page.Locator(".squad-save-btn").ClickAsync();
        await Expect(Page.Locator(".toast-success")).ToBeVisibleAsync(new() { Timeout = 8_000 });

        // Stats tab should now be enabled
        var statsTab = Page.Locator(".tab", new() { HasTextString = "Stats" });
        await statsTab.ClickAsync();

        await Expect(Page.Locator(".stats-table")).ToBeVisibleAsync();
    }

    [Test]
    public async Task FixtureDetail_StatsTab_ShowsPlayerRows()
    {
        // Set up squad first
        await Page.Locator(".tab", new() { HasTextString = "Squad" }).ClickAsync();
        await Page.WaitForSelectorAsync(".squad-row");

        var checkboxes = Page.Locator(".squad-row input[type='checkbox']");
        for (int i = 0; i < await checkboxes.CountAsync(); i++)
            await checkboxes.Nth(i).CheckAsync();

        await Page.Locator(".squad-save-btn").ClickAsync();
        await Expect(Page.Locator(".toast-success")).ToBeVisibleAsync(new() { Timeout = 8_000 });

        await Page.Locator(".tab", new() { HasTextString = "Stats" }).ClickAsync();

        var statsRows = Page.Locator(".stats-row");
        Assert.That(await statsRows.CountAsync(), Is.GreaterThan(0));
    }

    // ── Admin — referee assignment ────────────────────────────────────────────

    [Test]
    public async Task FixtureDetail_Admin_RefereeSection_IsVisible()
    {
        await ReLoginAsAdmin();

        await Page.GotoAsync(_fixtureUrl);
        await Page.WaitForSelectorAsync(".fixture-card");

        await Expect(Page.Locator(".referee-section")).ToBeVisibleAsync();
        await Expect(Page.Locator(".referee-select")).ToBeVisibleAsync();
    }

    [Test]
    public async Task FixtureDetail_Admin_SaveReferee_ShowsToast()
    {
        await ReLoginAsAdmin();

        await Page.GotoAsync(_fixtureUrl);
        await Page.WaitForSelectorAsync(".referee-select");

        // Select first actual referee (index 1 skips the "— No referee —" placeholder)
        await Page.Locator(".referee-select").SelectOptionAsync(
            new SelectOptionValue { Index = 1 });

        await Page.Locator(".referee-section .primary-btn").ClickAsync();

        await Expect(Page.Locator(".toast-success"))
            .ToBeVisibleAsync(new() { Timeout = 8_000 });
    }

    [Test]
    public async Task FixtureDetail_NonAdmin_RefereeSection_NotVisible()
    {
        // Manager (non-admin) should not see the referee assignment section
        await Expect(Page.Locator(".referee-section")).Not.ToBeVisibleAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task ReLoginAsAdmin()
    {
        await Page.Context.ClearCookiesAsync();
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.GetByPlaceholder("manager@team.com").FillAsync(AdminEmail);
        await Page.Locator("input[type='password']").FillAsync(AdminPassword);
        await Page.Locator(".login-btn").ClickAsync();
        await Page.WaitForURLAsync("**/dashboard");
    }
}
