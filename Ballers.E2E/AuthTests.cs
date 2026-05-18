namespace Ballers.E2E;

[TestFixture]
public class AuthTests : BallersTestBase
{
    [Test]
    public async Task Login_WithValidAdminCredentials_RedirectsToDashboard()
    {
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.GetByPlaceholder("manager@team.com").FillAsync(AdminEmail);
        await Page.Locator("input[type='password']").FillAsync(AdminPassword);
        await Page.Locator(".login-btn").ClickAsync();

        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(".*/dashboard"));
        await Expect(Page.Locator(".dash-greeting")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Login_WithWrongPassword_ShowsError()
    {
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.GetByPlaceholder("manager@team.com").FillAsync(AdminEmail);
        await Page.Locator("input[type='password']").FillAsync("definitelywrong!");
        await Page.Locator(".login-btn").ClickAsync();

        await Expect(Page.Locator(".login-error")).ToBeVisibleAsync();
        await Expect(Page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex(".*/login"));
    }

    [Test]
    public async Task UnauthenticatedUser_AccessingDashboard_DoesNotSeeDashboardContent()
    {
        await Page.GotoAsync($"{BaseUrl}/dashboard");
        await Page.WaitForLoadStateAsync(Microsoft.Playwright.LoadState.NetworkIdle);

        // Blazor WASM keeps the URL but hides the Authorized content
        await Expect(Page.Locator(".dash-greeting")).Not.ToBeVisibleAsync();
    }
}
