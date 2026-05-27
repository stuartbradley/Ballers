using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Ballers.Infrastructure
{
    public class CredentialsHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Automatically include cookies on every request
            request.SetBrowserRequestCredentials(
                BrowserRequestCredentials.Include);

            return base.SendAsync(request, cancellationToken);
        }
    }
}
