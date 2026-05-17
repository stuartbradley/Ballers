using System.Net.Http.Json;
using System.Text.Json;

namespace Ballers.API.Services
{
    public class ImageModerationService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly string? _apiKey;
        private readonly ILogger<ImageModerationService> _logger;

        public ImageModerationService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<ImageModerationService> logger)
        {
            _httpFactory = httpFactory;
            _apiKey = config["Anthropic:ApiKey"];
            _logger = logger;
        }

        // Returns true if the image contains explicit content.
        // Returns false (safe) if the API key is not configured or the check fails.
        public async Task<bool> IsExplicitAsync(byte[] imageBytes, string mediaType)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("Anthropic:ApiKey not configured — skipping image moderation.");
                return false;
            }

            try
            {
                var body = new
                {
                    model = "claude-haiku-4-5-20251001",
                    max_tokens = 5,
                    messages = new[]
                    {
                        new
                        {
                            role = "user",
                            content = new object[]
                            {
                                new
                                {
                                    type = "image",
                                    source = new
                                    {
                                        type = "base64",
                                        media_type = mediaType,
                                        data = Convert.ToBase64String(imageBytes)
                                    }
                                },
                                new
                                {
                                    type = "text",
                                    text = "Does this image contain explicit, adult, violent, or otherwise inappropriate content? Reply with only YES or NO."
                                }
                            }
                        }
                    }
                };

                var http = _httpFactory.CreateClient("Anthropic");
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = JsonContent.Create(body);

                var response = await http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Anthropic moderation API returned {StatusCode}", response.StatusCode);
                    return false;
                }

                using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                var text = json.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString() ?? "";

                return text.Trim().StartsWith("YES", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Image moderation check failed — treating image as safe.");
                return false;
            }
        }
    }
}
