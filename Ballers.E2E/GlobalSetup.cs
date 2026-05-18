namespace Ballers.E2E;

[SetUpFixture]
public class GlobalSetup
{
    [OneTimeSetUp]
    public async Task ResetTestDatabase()
    {
        var apiBaseUrl = TestContext.Parameters.Get("ApiBaseUrl", "https://localhost:7075");

        using var client = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync($"{apiBaseUrl}/api/test/reset", null);
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Could not reach API at {apiBaseUrl}. Make sure it is running with the Testing profile. ({ex.Message})");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception(
                $"DB reset failed ({(int)response.StatusCode}). " +
                $"Is the API running with ASPNETCORE_ENVIRONMENT=Testing? Response: {body}");
        }

        TestContext.Progress.WriteLine("BallersAutoTest database reset and seeded.");
    }
}
