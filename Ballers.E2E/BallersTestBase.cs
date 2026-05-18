using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace Ballers.E2E;

public abstract class BallersTestBase : PageTest
{
    protected string BaseUrl =>
        TestContext.Parameters.Get("BaseUrl", "https://localhost:57420");

    protected string ApiBaseUrl =>
        TestContext.Parameters.Get("ApiBaseUrl", "https://localhost:7075");

    protected string AdminEmail =>
        TestContext.Parameters.Get("AdminEmail", "Admin@ballers.com");

    protected string AdminPassword =>
        TestContext.Parameters.Get("AdminPassword", "Admin123!");

    protected string ManagerEmail =>
        TestContext.Parameters.Get("ManagerEmail", "manager1@ballers.com");

    protected string ManagerPassword =>
        TestContext.Parameters.Get("ManagerPassword", "Manager123!");

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        IgnoreHTTPSErrors = true
    };

    protected async Task LoginAsAdmin()
    {
        await Page.Context.ClearCookiesAsync();
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.GetByPlaceholder("manager@team.com").FillAsync(AdminEmail);
        await Page.Locator("input[type='password']").FillAsync(AdminPassword);
        await Page.Locator(".login-btn").ClickAsync();
        await Page.WaitForURLAsync("**/dashboard");
    }

    protected async Task LoginAs(string email, string password)
    {
        await Page.Context.ClearCookiesAsync();
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.GetByPlaceholder("manager@team.com").FillAsync(email);
        await Page.Locator("input[type='password']").FillAsync(password);
        await Page.Locator(".login-btn").ClickAsync();
        await Page.WaitForURLAsync("**/dashboard");
    }
}
