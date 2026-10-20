using System.Net.Http.Json;

namespace Ballers.UI.Infrastructure
{
    /// <summary>
    /// The API calls every UI service makes, with one rule for failures:
    /// reads that fail come back empty (null, or an empty list) so a page can
    /// show its empty state; writes that fail throw an <see cref="ApiException"/>
    /// carrying the API's reason, for the page to show in a toast.
    /// Cookies are added to every request by CredentialsHandler.
    /// </summary>
    public static class HttpJson
    {
        /// <summary>
        /// The body as <typeparamref name="T"/>, or null on a failure status, 204
        /// No Content, an unreachable API or a reply that isn't the expected JSON.
        /// </summary>
        public static async Task<T?> GetOrDefault<T>(this HttpClient http, string url)
        {
            try
            {
                var response = await http.GetAsync(url);
                if (!response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent) return default;
                return await response.Content.ReadFromJsonAsync<T>();
            }
            catch (HttpRequestException) { return default; }
            catch (System.Text.Json.JsonException) { return default; }
        }

        /// <summary>The list, or an empty one on any failure.</summary>
        public static async Task<List<T>> GetListOrEmpty<T>(this HttpClient http, string url) =>
            await http.GetOrDefault<List<T>>(url) ?? [];

        // ── Writes ────────────────────────────────────────────────────────
        // The body is sent as JSON, or as-is when it is already HttpContent (a
        // file upload); null sends no body. Each throws an ApiException with the
        // API's reason, or the fallback, unless the call succeeded.

        public static async Task PostOk(this HttpClient http, string url, object? body, string fallback) =>
            await (await http.SendAsync(HttpMethod.Post, url, body, fallback)).EnsureOk(fallback);

        public static async Task<T?> PostOk<T>(this HttpClient http, string url, object? body, string fallback) =>
            await (await http.SendAsync(HttpMethod.Post, url, body, fallback)).ReadOk<T>(fallback);

        public static async Task PutOk(this HttpClient http, string url, object? body, string fallback) =>
            await (await http.SendAsync(HttpMethod.Put, url, body, fallback)).EnsureOk(fallback);

        public static async Task<T?> PutOk<T>(this HttpClient http, string url, object? body, string fallback) =>
            await (await http.SendAsync(HttpMethod.Put, url, body, fallback)).ReadOk<T>(fallback);

        public static async Task DeleteOk(this HttpClient http, string url, string fallback) =>
            await (await http.SendAsync(HttpMethod.Delete, url, null, fallback)).EnsureOk(fallback);

        // An unreachable API is a failed write like any other, so it gets the same
        // exception and the page shows the fallback.
        private static async Task<HttpResponseMessage> SendAsync(
            this HttpClient http, HttpMethod method, string url, object? body, string fallback)
        {
            var request = new HttpRequestMessage(method, url)
            {
                Content = body switch
                {
                    null => null,
                    HttpContent content => content,
                    _ => JsonContent.Create(body, body.GetType())
                }
            };

            try { return await http.SendAsync(request); }
            catch (HttpRequestException) { throw new ApiException(fallback, 0); }
        }

        /// <summary>Throws with the API's reason, or <paramref name="fallback"/>, unless the call succeeded.</summary>
        public static async Task EnsureOk(this HttpResponseMessage response, string fallback)
        {
            if (response.IsSuccessStatusCode) return;
            throw new ApiException(await ReasonAsync(response) ?? fallback, (int)response.StatusCode);
        }

        /// <summary>The body as <typeparamref name="T"/>; throws like <see cref="EnsureOk"/> on failure.</summary>
        public static async Task<T?> ReadOk<T>(this HttpResponseMessage response, string fallback)
        {
            await response.EnsureOk(fallback);
            return await response.Content.ReadFromJsonAsync<T>();
        }

        // Controllers return their reason as a plain string, e.g.
        // BadRequest("Stats are already in"). Anything else (a problem-details
        // object, an HTML error page) isn't fit to show, so it's ignored.
        private static async Task<string?> ReasonAsync(HttpResponseMessage response)
        {
            string body;
            try { body = (await response.Content.ReadAsStringAsync()).Trim(); }
            catch { return null; }

            body = body.Trim('"');
            return string.IsNullOrWhiteSpace(body) || body.StartsWith('{') || body.StartsWith('<') || body.Length > 300
                ? null
                : body;
        }
    }

    /// <summary>An API call that failed, with a message fit to show the user.</summary>
    public class ApiException(string message, int statusCode) : Exception(message)
    {
        public int StatusCode { get; } = statusCode;
    }
}
