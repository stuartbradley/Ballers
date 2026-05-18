using Microsoft.Playwright;

namespace Ballers.E2E;

[TestFixture]
public class RefereeTests : BallersTestBase
{
    [SetUp]
    public async Task SetUp() => await LoginAs(ManagerEmail, ManagerPassword);

    // ── List ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task Referees_PageLoads_ShowsSeededReferees()
    {
        await Page.GotoAsync($"{BaseUrl}/referees");
        await Page.WaitForSelectorAsync(".ref-card");

        var count = await Page.Locator(".ref-card").CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Expected seeded referees to be listed");
    }

    [Test]
    public async Task Referees_Cards_ShowNameAndContactDetails()
    {
        await Page.GotoAsync($"{BaseUrl}/referees");
        await Page.WaitForSelectorAsync(".ref-card");

        var first = Page.Locator(".ref-card").First;
        await Expect(first.Locator(".ref-card-name")).ToBeVisibleAsync();
        // Seeded referees all have phone + email
        await Expect(first.Locator(".ref-contact-chip").First).ToBeVisibleAsync();
    }

    // ── Add ───────────────────────────────────────────────────────────────────

    [Test]
    public async Task Referees_AddReferee_AppearsInList()
    {
        await Page.GotoAsync($"{BaseUrl}/referees");
        await Page.WaitForSelectorAsync(".ref-form-card");

        var countBefore = await Page.Locator(".ref-card").CountAsync();

        await Page.GetByPlaceholder("e.g. Mark Hughes").FillAsync("New Test Ref");
        await Page.GetByPlaceholder("e.g. 07700 900123").FillAsync("07700 123456");

        await Page.Locator(".primary-btn", new() { HasTextString = "Add Referee" }).ClickAsync();

        await Expect(Page.Locator(".ref-card"))
            .ToHaveCountAsync(countBefore + 1, new() { Timeout = 8_000 });
        await Expect(Page.Locator(".ref-card-name", new() { HasTextString = "New Test Ref" }))
            .ToBeVisibleAsync();
    }

    [Test]
    public async Task Referees_AddReferee_WithoutName_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/referees");
        await Page.WaitForSelectorAsync(".ref-form-card");

        // Leave name blank and try to submit
        await Page.Locator(".primary-btn", new() { HasTextString = "Add Referee" }).ClickAsync();

        await Expect(Page.Locator(".ref-error")).ToBeVisibleAsync();
    }

    // ── Edit ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task Referees_EditReferee_UpdatesName()
    {
        await Page.GotoAsync($"{BaseUrl}/referees");
        await Page.WaitForSelectorAsync(".ref-card");

        // Click Edit on the first card
        await Page.Locator(".ref-edit-btn").First.ClickAsync();

        // Form should now show "Edit Referee" heading
        await Expect(Page.Locator(".ref-form-card h3")).ToContainTextAsync("Edit Referee");

        // Clear name and type updated name
        var nameInput = Page.GetByPlaceholder("e.g. Mark Hughes");
        await nameInput.ClearAsync();
        await nameInput.FillAsync("Updated Referee Name");

        await Page.Locator(".primary-btn", new() { HasTextString = "Update" }).ClickAsync();

        // Card should reflect the updated name
        await Expect(Page.Locator(".ref-card-name", new() { HasTextString = "Updated Referee Name" }))
            .ToBeVisibleAsync(new() { Timeout = 8_000 });
    }

    [Test]
    public async Task Referees_CancelEdit_RestoresAddForm()
    {
        await Page.GotoAsync($"{BaseUrl}/referees");
        await Page.WaitForSelectorAsync(".ref-card");

        await Page.Locator(".ref-edit-btn").First.ClickAsync();
        await Expect(Page.Locator(".ref-form-card h3")).ToContainTextAsync("Edit Referee");

        await Page.Locator(".cancel-btn").ClickAsync();

        await Expect(Page.Locator(".ref-form-card h3")).ToContainTextAsync("Add Referee");
    }

    // ── Delete (admin only) ───────────────────────────────────────────────────

    [Test]
    public async Task Referees_NonAdmin_DoesNotSeeDeleteButton()
    {
        await Page.GotoAsync($"{BaseUrl}/referees");
        await Page.WaitForSelectorAsync(".ref-card");

        var deleteButtons = await Page.Locator(".ref-delete-btn").CountAsync();
        Assert.That(deleteButtons, Is.EqualTo(0), "Non-admin should not see delete buttons");
    }

    [Test]
    public async Task Referees_Admin_DeleteReferee_RemovedFromList()
    {
        // Re-login as admin for this test
        await Page.Context.ClearCookiesAsync();
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.GetByPlaceholder("manager@team.com").FillAsync(AdminEmail);
        await Page.Locator("input[type='password']").FillAsync(AdminPassword);
        await Page.Locator(".login-btn").ClickAsync();
        await Page.WaitForURLAsync("**/dashboard");

        await Page.GotoAsync($"{BaseUrl}/referees");
        await Page.WaitForSelectorAsync(".ref-card");

        var countBefore = await Page.Locator(".ref-card").CountAsync();
        Assert.That(countBefore, Is.GreaterThan(0));

        await Page.Locator(".ref-delete-btn").First.ClickAsync();

        await Expect(Page.Locator(".ref-card"))
            .ToHaveCountAsync(countBefore - 1, new() { Timeout = 8_000 });
    }

    // ── Upcoming fixture display ──────────────────────────────────────────────

    [Test]
    public async Task Referees_UpcomingFixtureChip_ShowsFormattedDateAndKickoff()
    {
        // Assign a referee to an unplayed fixture via admin, then verify the chip format.
        await Page.GotoAsync($"{BaseUrl}/manager-fixtures");
        await Page.WaitForSelectorAsync(".view-btn, .edit-btn");

        var editCount = await Page.Locator(".edit-btn").CountAsync();
        if (editCount == 0)
            Assert.Ignore("No unplayed fixtures available — skipping date-format test.");

        await Page.Locator(".edit-btn").First.ClickAsync();
        await Page.WaitForSelectorAsync(".fixture-card");
        var fixtureUrl = Page.Url;

        // Re-login as admin to access the referee assignment section
        await Page.Context.ClearCookiesAsync();
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.GetByPlaceholder("manager@team.com").FillAsync(AdminEmail);
        await Page.Locator("input[type='password']").FillAsync(AdminPassword);
        await Page.Locator(".login-btn").ClickAsync();
        await Page.WaitForURLAsync("**/dashboard");

        await Page.GotoAsync(fixtureUrl);
        await Page.WaitForSelectorAsync(".referee-select");

        await Page.Locator(".referee-select").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await Page.Locator(".referee-section .primary-btn").ClickAsync();
        await Expect(Page.Locator(".toast-success")).ToBeVisibleAsync(new() { Timeout = 8_000 });

        // Check the referees page (admin can access it too)
        await Page.GotoAsync($"{BaseUrl}/referees");
        await Page.WaitForSelectorAsync(".ref-card");

        var cardWithFixtures = Page.Locator(".ref-card")
            .Filter(new() { Has = Page.Locator(".ref-fixture-chip") }).First;

        var chip = cardWithFixtures.Locator(".ref-fixture-chip").First;

        // Date span: "ddd d MMM" e.g. "Sun 7 Sep"
        var dateText = await chip.Locator(".ref-fixture-date").InnerTextAsync();
        var dayAbbrevs = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        Assert.That(dayAbbrevs.Any(d => dateText.Contains(d)),
            $"Expected a day abbreviation in .ref-fixture-date, got: '{dateText}'");

        // KO span: "HH:mm" e.g. "14:00"
        var koText = await chip.Locator(".ref-fixture-ko").InnerTextAsync();
        Assert.That(koText, Does.Match(@"^\d{2}:\d{2}$"),
            $"Expected HH:mm kickoff time in .ref-fixture-ko, got: '{koText}'");
    }

    [Test]
    public async Task Referees_UpcomingFixtures_LimitedToTen()
    {
        await Page.GotoAsync($"{BaseUrl}/referees");
        await Page.WaitForSelectorAsync(".ref-card");

        var cards = Page.Locator(".ref-card");
        var cardCount = await cards.CountAsync();

        for (int i = 0; i < cardCount; i++)
        {
            var chipCount = await cards.Nth(i).Locator(".ref-fixture-chip").CountAsync();
            Assert.That(chipCount, Is.LessThanOrEqualTo(10),
                $"Referee card {i} shows {chipCount} upcoming fixtures — expected max 10");
        }
    }
}
