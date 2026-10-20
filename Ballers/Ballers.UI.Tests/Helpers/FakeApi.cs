using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ballers.UI.Tests.Helpers
{
    /// <summary>
    /// Stands in for the API behind the UI's real services. Register a response
    /// per method and path; any GET not registered answers 404 (so reads come
    /// back empty, as they do when the API has nothing), and any write not
    /// registered answers 200. Every request is recorded for asserting on.
    /// </summary>
    public class FakeApi : HttpMessageHandler
    {
        private readonly Dictionary<(HttpMethod, string), Func<HttpResponseMessage>> _routes = new();

        public List<(HttpMethod Method, string Path, string? Body)> Requests { get; } = new();

        public FakeApi()
        {
            // Every page that reads the league settings expects them to load.
            Get("api/league-settings", new { });
        }

        public HttpClient Client => new(this) { BaseAddress = new Uri("https://api.test/") };

        public FakeApi Get(string path, object body) => On(HttpMethod.Get, path, () => Json(body));
        public FakeApi On(HttpMethod method, string path, HttpStatusCode status, string? reason = null)
            => On(method, path, () => new HttpResponseMessage(status) { Content = new StringContent(reason ?? "") });
        public FakeApi On(HttpMethod method, string path, Func<HttpResponseMessage> respond)
        {
            _routes[(method, path)] = respond;
            return this;
        }

        public IEnumerable<string?> BodiesSentTo(HttpMethod method, string path)
            => Requests.Where(r => r.Method == method && r.Path == path).Select(r => r.Body);

        public bool WasCalled(HttpMethod method, string path)
            => Requests.Any(r => r.Method == method && r.Path == path);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery.TrimStart('/');
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            lock (Requests) Requests.Add((request.Method, path, body));

            if (_routes.TryGetValue((request.Method, path), out var respond)) return respond();
            return new HttpResponseMessage(request.Method == HttpMethod.Get ? HttpStatusCode.NotFound : HttpStatusCode.OK);
        }

        private static HttpResponseMessage Json(object body)
            => new(HttpStatusCode.OK) { Content = JsonContent.Create(body, body.GetType(), options: new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
    }
}
