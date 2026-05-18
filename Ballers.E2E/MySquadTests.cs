using Microsoft.Playwright;

namespace Ballers.E2E;

[TestFixture]
public class MySquadTests : BallersTestBase
{
    // ── Manager squad ─────────────────────────────────────────────────────────

    [Test]
    public async Task MySquad_Manager_LoadsPlayerList()
    {
        await LoginAs(ManagerEmail, ManagerPassword);
        await Page.GotoAsync($"{BaseUrl}/my-squad");
        await Page.WaitForSelectorAsync(".player-card");

        var count = await Page.Locator(".player-card").CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Manager should see their seeded players");
    }

    [Test]
    public async Task MySquad_Manager_PlayerCardsShowNumberAndName()
    {
        await LoginAs(ManagerEmail, ManagerPassword);
        await Page.GotoAsync($"{BaseUrl}/my-squad");
        await Page.WaitForSelectorAsync(".player-card");

        var first = Page.Locator(".player-card").First;
        await Expect(first.Locator(".shirt-circle")).ToBeVisibleAsync();
        await Expect(first.Locator(".player-name")).ToBeVisibleAsync();
    }

    [Test]
    public async Task MySquad_Manager_SummaryStripShowsCorrectTotals()
    {
        await LoginAs(ManagerEmail, ManagerPassword);
        await Page.GotoAsync($"{BaseUrl}/my-squad");
        // Wait for players to load before reading the summary strip
        await Page.WaitForSelectorAsync(".player-card");

        var total = await Page.Locator(".summary-item.total .summary-value").InnerTextAsync();
        Assert.That(int.Parse(total), Is.GreaterThan(0), "Total should reflect seeded players");
    }

    [Test]
    public async Task MySquad_Manager_AddPlayer_AppearsInList()
    {
        await LoginAs(ManagerEmail, ManagerPassword);
        await Page.GotoAsync($"{BaseUrl}/my-squad");
        await Page.WaitForSelectorAsync(".player-card");

        var countBefore = await Page.Locator(".player-card").CountAsync();

        // Select FWD position
        await Page.Locator(".pos-btn", new() { HasTextString = "FWD" }).ClickAsync();

        // Fill name and shirt number
        await Page.Locator(".name-input").FillAsync("Test Forward");
        await Page.Locator(".number-input").FillAsync("77");

        await Page.Locator(".add-btn").ClickAsync();

        // Wait for the new card
        await Expect(Page.Locator(".player-card")).ToHaveCountAsync(
            countBefore + 1, new() { Timeout = 8_000 });
        await Expect(Page.Locator(".player-name", new() { HasTextString = "Test Forward" }))
            .ToBeVisibleAsync();
    }

    [Test]
    public async Task MySquad_Manager_RemovePlayer_DisappearsFromList()
    {
        await LoginAs(ManagerEmail, ManagerPassword);
        await Page.GotoAsync($"{BaseUrl}/my-squad");
        await Page.WaitForSelectorAsync(".player-card");

        var countBefore = await Page.Locator(".player-card").CountAsync();
        Assert.That(countBefore, Is.GreaterThan(0), "Need at least one player to remove");

        await Page.Locator(".remove-btn").First.ClickAsync();

        await Expect(Page.Locator(".player-card"))
            .ToHaveCountAsync(countBefore - 1, new() { Timeout = 8_000 });
    }

    // ── Admin ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task MySquad_Admin_ShowsNoTeamMessage()
    {
        await LoginAsAdmin();
        await Page.GotoAsync($"{BaseUrl}/my-squad");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        await Expect(Page.Locator(".empty-state"))
            .ToContainTextAsync("doesn't have a team assigned");
    }
}
