using Microsoft.Playwright;

namespace Ballers.E2E;

[TestFixture]
public class PublicPageTests : BallersTestBase
{
    // ── Home ────────────────────────────────────────────────────────────────

    [Test]
    public async Task HomePage_DisplaysCorrectHeading()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForSelectorAsync("h1");

        await Expect(Page.Locator("h1")).ToContainTextAsync("Blackpool");
        await Expect(Page.Locator("h1")).ToContainTextAsync("Ballers");
        await Expect(Page.Locator("h1")).ToContainTextAsync("League");
    }

    [Test]
    public async Task HomePage_ShowsLeagueTableSection()
    {
        await Page.GotoAsync(BaseUrl);
        await Expect(Page.Locator("#league-table")).ToBeVisibleAsync();
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────

    [Test]
    public async Task FixturesPage_LoadsAndShowsWeekCards()
    {
        await Page.GotoAsync($"{BaseUrl}/fixtures");
        await Page.WaitForSelectorAsync(".week-card");

        var count = await Page.Locator(".week-card").CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Expected at least one fixture week card");
    }

    [Test]
    public async Task FixturesPage_EachWeekCardShowsMatchups()
    {
        await Page.GotoAsync($"{BaseUrl}/fixtures");
        await Page.WaitForSelectorAsync(".fixture-card");

        var firstCard = Page.Locator(".fixture-card").First;
        await Expect(firstCard.Locator(".home-team")).ToBeVisibleAsync();
        await Expect(firstCard.Locator(".away-team")).ToBeVisibleAsync();
        await Expect(firstCard.Locator(".vs-ring")).ToContainTextAsync("VS");
    }

    // ── Teams ────────────────────────────────────────────────────────────────

    [Test]
    public async Task TeamsPage_LoadsAndShowsTeamCards()
    {
        await Page.GotoAsync($"{BaseUrl}/teams");
        await Page.WaitForSelectorAsync(".team-card");

        var count = await Page.Locator(".team-card").CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Expected at least one team card");
    }

    [Test]
    public async Task TeamsPage_TeamCardsShowNameAndManager()
    {
        await Page.GotoAsync($"{BaseUrl}/teams");
        await Page.WaitForSelectorAsync(".team-card");

        var firstCard = Page.Locator(".team-card").First;
        await Expect(firstCard.Locator(".team-card-name")).ToBeVisibleAsync();
        await Expect(firstCard.Locator(".team-card-manager")).ToBeVisibleAsync();
    }

    [Test]
    public async Task TeamsPage_ClickingTeamCard_NavigatesToTeamProfile()
    {
        await Page.GotoAsync($"{BaseUrl}/teams");
        await Page.WaitForSelectorAsync(".team-card");

        await Page.Locator(".team-card").First.ClickAsync();

        await Expect(Page).ToHaveURLAsync(
            new System.Text.RegularExpressions.Regex(".*/team/\\d+"));
    }

    // ── Team Profile ──────────────────────────────────────────────────────────

    [Test]
    public async Task TeamProfilePage_ShowsTeamNameAndMeta()
    {
        await Page.GotoAsync($"{BaseUrl}/teams");
        await Page.WaitForSelectorAsync(".team-card");
        await Page.Locator(".team-card").First.ClickAsync();
        await Page.WaitForSelectorAsync(".profile-team-name");

        await Expect(Page.Locator(".profile-team-name")).ToBeVisibleAsync();
        // Meta pills show Est. year and manager name
        await Expect(Page.Locator(".meta-pill").First).ToBeVisibleAsync();
    }

    [Test]
    public async Task TeamProfilePage_ShowsEstablishedYear()
    {
        await Page.GotoAsync($"{BaseUrl}/teams");
        await Page.WaitForSelectorAsync(".team-card");
        await Page.Locator(".team-card").First.ClickAsync();
        await Page.WaitForSelectorAsync(".profile-team-name");

        // Use Playwright auto-retry so we don't race Blazor's async render
        await Expect(Page.Locator(".profile-meta-pills")).ToContainTextAsync("Est.");
    }
}
