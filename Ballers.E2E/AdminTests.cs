using Microsoft.Playwright;

namespace Ballers.E2E;

[TestFixture]
public class AdminTests : BallersTestBase
{
    // DB is reset before each run so dates just need to not overlap each other.
    private static readonly int RunYear = DateTime.Today.Year + 1;

    [SetUp]
    public async Task SetUp() => await LoginAsAdmin();

    [Test]
    public async Task AdminPage_LoadsWithCorrectHeading()
    {
        await Page.GotoAsync($"{BaseUrl}/admin");
        await Expect(Page.Locator("h1")).ToContainTextAsync("League Control");
    }

    [Test]
    public async Task AdminPage_ShowsTeamListForFixtureGeneration()
    {
        await Page.GotoAsync($"{BaseUrl}/admin");
        await Page.WaitForSelectorAsync(".team-check-label");

        var teamCount = await Page.Locator(".team-check-label").CountAsync();
        Assert.That(teamCount, Is.GreaterThan(0), "At least one team should be listed");
    }

    [Test]
    public async Task GenerateFixtures_WithFourTeams_CreatesSeasonAndShowsSuccess()
    {
        await Page.GotoAsync($"{BaseUrl}/admin");
        await Page.WaitForSelectorAsync(".team-check-label");

        var startDate = new DateTime(RunYear, 1, 1).ToString("yyyy-MM-dd");
        await Page.Locator("input[type='date']").FillAsync(startDate);

        // Select first 4 teams
        var checkboxes = Page.Locator(".team-check-label input[type='checkbox']");
        var available = await checkboxes.CountAsync();
        for (int i = 0; i < Math.Min(4, available); i++)
            await checkboxes.Nth(i).CheckAsync();

        // Record season count before generating
        var seasonsBefore = await Page.Locator(".season-row").CountAsync();

        // Generate
        await Page.GetByRole(AriaRole.Button, new() { Name = "Generate Fixtures" }).ClickAsync();

        // Wait for either success or error to appear
        await Page.WaitForSelectorAsync(".success-msg, .error-msg", new() { Timeout = 10_000 });

        var errorMsg = Page.Locator(".error-msg");
        if (await errorMsg.IsVisibleAsync())
        {
            var text = await errorMsg.InnerTextAsync();
            Assert.Fail($"Fixture generation returned an error: {text}");
        }

        await Expect(Page.Locator(".success-msg")).ToContainTextAsync("Fixtures generated.");
        await Expect(Page.Locator(".season-row")).ToHaveCountAsync(seasonsBefore + 1, new() { Timeout = 10_000 });
    }

    [Test]
    public async Task GenerateFixtures_WithAllTeams_SeasonAppearsInHistory()
    {
        await Page.GotoAsync($"{BaseUrl}/admin");
        await Page.WaitForSelectorAsync(".team-check-label");

        var startDate = new DateTime(RunYear + 20, 1, 1).ToString("yyyy-MM-dd");
        await Page.Locator("input[type='date']").FillAsync(startDate);

        // Select all teams
        var checkboxes = Page.Locator(".team-check-label input[type='checkbox']");
        var available = await checkboxes.CountAsync();
        for (int i = 0; i < available; i++)
            await checkboxes.Nth(i).CheckAsync();

        var seasonsBefore = await Page.Locator(".season-row").CountAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Generate Fixtures" }).ClickAsync();

        await Expect(Page.Locator(".success-msg"))
            .ToContainTextAsync("Fixtures generated.", new() { Timeout = 10_000 });

        await Expect(Page.Locator(".season-row"))
            .ToHaveCountAsync(seasonsBefore + 1, new() { Timeout = 10_000 });

        // New season row should show the start date year
        var newSeasonRow = Page.Locator(".season-row").First;
        await Expect(newSeasonRow).ToContainTextAsync((RunYear + 20).ToString());
    }

    [Test]
    public async Task FixturesPage_AfterGeneration_ShowsNewWeeks()
    {
        // Generate a season first
        await Page.GotoAsync($"{BaseUrl}/admin");
        await Page.WaitForSelectorAsync(".team-check-label");

        var startDate = new DateTime(RunYear + 40, 1, 1).ToString("yyyy-MM-dd");
        await Page.Locator("input[type='date']").FillAsync(startDate);

        var checkboxes = Page.Locator(".team-check-label input[type='checkbox']");
        var available = await checkboxes.CountAsync();
        for (int i = 0; i < Math.Min(4, available); i++)
            await checkboxes.Nth(i).CheckAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Generate Fixtures" }).ClickAsync();
        await Expect(Page.Locator(".success-msg")).ToContainTextAsync("Fixtures generated.");

        // Navigate to fixtures page and verify content
        await Page.GotoAsync($"{BaseUrl}/fixtures");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var weekCards = Page.Locator(".fixture-week-card, .week-card, [class*='week']");
        var count = await weekCards.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Fixtures page should show at least one week");
    }
}
