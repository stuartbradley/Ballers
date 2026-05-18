using Microsoft.Playwright;

namespace Ballers.E2E;

/// <summary>
/// Verifies that changes made on a fixture detail page propagate back to
/// the manager fixtures list (/manager-fixtures) and the public fixtures
/// page (/fixtures).
/// </summary>
[TestFixture]
public class FixtureUpdateTests : BallersTestBase
{
    private string _fixtureUrl = "";
    private string _homeTeam = "";

    [SetUp]
    public async Task SetUp()
    {
        // Navigate to the first available fixture as manager to capture its URL
        // and team names for later assertions on list/public pages.
        await LoginAs(ManagerEmail, ManagerPassword);
        await Page.GotoAsync($"{BaseUrl}/manager-fixtures");
        await Page.WaitForSelectorAsync(".view-btn, .edit-btn");

        await Page.Locator(".view-btn, .edit-btn").First.ClickAsync();
        await Page.WaitForSelectorAsync(".fixture-teams");

        _fixtureUrl = Page.Url;
        _homeTeam = await Page.Locator(".fixture-teams .team").First.InnerTextAsync();
    }

    // ── Schedule update reflected in manager fixtures list ────────────────────

    [Test]
    public async Task ScheduleUpdate_Location_AppearsInManagerFixturesList()
    {
        var testLocation = $"UpdateTest {Guid.NewGuid().ToString()[..8]}";

        await ReLoginAsAdmin();
        await SetLocation(testLocation);

        // Manager re-checks their fixtures list
        await LoginAs(ManagerEmail, ManagerPassword);
        await Page.GotoAsync($"{BaseUrl}/manager-fixtures");
        await Page.WaitForSelectorAsync(".fixture-row");

        await Expect(Page.Locator(".fixture-detail", new() { HasTextString = testLocation }))
            .ToBeVisibleAsync();
    }

    [Test]
    public async Task ScheduleUpdate_Kickoff_AppearsInManagerFixturesList()
    {
        var kickoff = DateTime.Now.AddDays(30);
        var expectedDisplay = kickoff.ToString("dd MMM HH:mm"); // matches format in ManagerFixtures.razor

        await ReLoginAsAdmin();
        await SetKickoff(kickoff);

        await LoginAs(ManagerEmail, ManagerPassword);
        await Page.GotoAsync($"{BaseUrl}/manager-fixtures");
        await Page.WaitForSelectorAsync(".fixture-row");

        await Expect(Page.Locator(".fixture-detail", new() { HasTextString = expectedDisplay }))
            .ToBeVisibleAsync();
    }

    // ── Schedule update reflected on public fixtures page ─────────────────────

    [Test]
    public async Task ScheduleUpdate_Location_AppearsOnPublicFixturesPage()
    {
        var testLocation = $"PubTest {Guid.NewGuid().ToString()[..8]}";

        await ReLoginAsAdmin();
        await SetLocation(testLocation);

        // Public page — no auth required
        await Page.GotoAsync($"{BaseUrl}/fixtures");
        await Page.WaitForSelectorAsync(".fixture-card");

        // Find this fixture's card by home team name and verify the location link
        var card = Page.Locator(".fixture-card")
            .Filter(new() { Has = Page.Locator(".home-team").Filter(new() { HasTextString = _homeTeam }) });

        await Expect(card.Locator(".meta-link", new() { HasTextString = testLocation }))
            .ToBeVisibleAsync(new() { Timeout = 8_000 });
    }

    [Test]
    public async Task ScheduleUpdate_Kickoff_AppearsOnPublicFixturesPage()
    {
        var kickoff = DateTime.Now.AddDays(45);
        var expectedTime = kickoff.ToString("HH:mm"); // FixtureWeekCard shows "Day · HH:mm"

        await ReLoginAsAdmin();
        await SetKickoff(kickoff);

        await Page.GotoAsync($"{BaseUrl}/fixtures");
        await Page.WaitForSelectorAsync(".fixture-card");

        var card = Page.Locator(".fixture-card")
            .Filter(new() { Has = Page.Locator(".home-team").Filter(new() { HasTextString = _homeTeam }) });

        // Meta item should now show a time rather than "Time TBD"
        var metaItem = card.Locator(".meta-item").First;
        await Expect(metaItem).Not.ToContainTextAsync("TBD", new() { Timeout = 8_000 });
        await Expect(metaItem).ToContainTextAsync(expectedTime);
    }

    // ── Stats submission reflected in manager fixtures list ───────────────────

    [Test]
    public async Task StatsSubmission_ChangesStatusToPlayed_InManagerFixturesList()
    {
        await LoginAs(ManagerEmail, ManagerPassword);
        await Page.GotoAsync($"{BaseUrl}/manager-fixtures");
        await Page.WaitForSelectorAsync(".view-btn, .edit-btn");

        var editCountBefore = await Page.Locator(".edit-btn").CountAsync();
        if (editCountBefore == 0)
            Assert.Ignore("No unplayed fixtures in seeded data — skipping stats-submission test.");

        await SubmitStatsForFirstUnplayedFixture();

        await Page.GotoAsync($"{BaseUrl}/manager-fixtures");
        await Page.WaitForSelectorAsync(".fixture-row");

        var editCountAfter = await Page.Locator(".edit-btn").CountAsync();
        Assert.That(editCountAfter, Is.EqualTo(editCountBefore - 1),
            "One fewer unplayed fixture expected after stats submission");
    }

    [Test]
    public async Task StatsSubmission_ChangesEditButtonToView_InManagerFixturesList()
    {
        await LoginAs(ManagerEmail, ManagerPassword);
        await Page.GotoAsync($"{BaseUrl}/manager-fixtures");
        await Page.WaitForSelectorAsync(".view-btn, .edit-btn");

        var editCountBefore = await Page.Locator(".edit-btn").CountAsync();
        var viewCountBefore = await Page.Locator(".view-btn").CountAsync();
        if (editCountBefore == 0)
            Assert.Ignore("No unplayed fixtures in seeded data — skipping button-change test.");

        await SubmitStatsForFirstUnplayedFixture();

        await Page.GotoAsync($"{BaseUrl}/manager-fixtures");
        await Page.WaitForSelectorAsync(".fixture-row");

        Assert.That(await Page.Locator(".edit-btn").CountAsync(), Is.EqualTo(editCountBefore - 1),
            "Edit button count should decrease by one");
        Assert.That(await Page.Locator(".view-btn").CountAsync(), Is.EqualTo(viewCountBefore + 1),
            "View button count should increase by one");
    }

    private async Task SubmitStatsForFirstUnplayedFixture()
    {
        var firstEdit = Page.Locator(".fixture-row").Filter(new() { Has = Page.Locator(".edit-btn") }).First;
        await firstEdit.Locator(".edit-btn").ClickAsync();
        await Page.WaitForSelectorAsync(".fixture-card");

        await Page.Locator(".tab", new() { HasTextString = "Squad" }).ClickAsync();
        await Page.WaitForSelectorAsync(".squad-row");

        var checkboxes = Page.Locator(".squad-row input[type='checkbox']");
        var playerCount = await checkboxes.CountAsync();
        if (playerCount <= 10)
            Assert.Ignore($"Only {playerCount} seeded players — need >10 to unlock stats tab.");

        for (int i = 0; i < playerCount; i++)
            await checkboxes.Nth(i).CheckAsync();

        await Page.Locator(".squad-save-btn").ClickAsync();
        await Expect(Page.Locator(".toast-success")).ToBeVisibleAsync(new() { Timeout = 8_000 });

        await Page.Locator(".tab", new() { HasTextString = "Stats" }).ClickAsync();
        await Page.WaitForSelectorAsync(".stats-row");

        await Page.Locator(".motm-checkbox").First.CheckAsync();
        await Page.Locator(".submit-stats-btn").ClickAsync();
        await Expect(Page.Locator(".toast-success")).ToBeVisibleAsync(new() { Timeout = 8_000 });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task SetLocation(string location)
    {
        await Page.GotoAsync(_fixtureUrl);
        await Page.WaitForSelectorAsync(".schedule-section");

        var locationInput = Page.Locator(".schedule-input").First;
        await locationInput.ClearAsync();
        await locationInput.FillAsync(location);

        await Page.Locator(".primary-btn", new() { HasTextString = "Save Schedule" }).ClickAsync();
        await Expect(Page.Locator(".toast-success")).ToBeVisibleAsync(new() { Timeout = 8_000 });
    }

    private async Task SetKickoff(DateTime kickoff)
    {
        await Page.GotoAsync(_fixtureUrl);
        await Page.WaitForSelectorAsync(".schedule-section");

        await Page.Locator("input[type='datetime-local']").FillAsync(
            kickoff.ToString("yyyy-MM-ddTHH:mm"));

        await Page.Locator(".primary-btn", new() { HasTextString = "Save Schedule" }).ClickAsync();
        await Expect(Page.Locator(".toast-success")).ToBeVisibleAsync(new() { Timeout = 8_000 });
    }

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
