using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.JSInterop;

namespace Ballers.Services
{
    /// <summary>
    /// Consent-gated Google Analytics. No analytics script is loaded and no
    /// analytics cookies are set until the visitor accepts in the cookie banner.
    /// </summary>
    public class AnalyticsService
    {
        public const string ConsentKey = "bbl.cookieConsent";
        public const string Granted = "granted";
        public const string Denied = "denied";

        private const string PlaceholderId = "G-XXXXXXXXXX";

        private readonly IJSRuntime _js;
        private readonly NavigationManager _nav;
        private readonly string? _measurementId;

        private bool _initialised;
        private string? _consent;

        /// <summary>Raised when the visitor asks to review their cookie choice again.</summary>
        public event Action? BannerRequested;

        public AnalyticsService(IJSRuntime js, NavigationManager nav, IConfiguration config)
        {
            _js = js;
            _nav = nav;

            var id = config["GoogleAnalytics:MeasurementId"];
            _measurementId = string.IsNullOrWhiteSpace(id) || id == PlaceholderId ? null : id;
        }

        /// <summary>True once the visitor has made a choice either way.</summary>
        public bool HasChoice => _consent == Granted || _consent == Denied;

        public bool IsGranted => _consent == Granted;

        /// <summary>
        /// Reads the stored choice, wires up the analytics script if consent was
        /// already given, and starts tracking SPA navigations.
        /// </summary>
        public async Task InitialiseAsync()
        {
            if (_initialised) return;
            _initialised = true;

            _consent = await ReadStoredConsentAsync();

            if (_measurementId is null) return;

            await SafeInvokeAsync("bblAnalytics.configure", _measurementId);

            if (_consent == Granted)
            {
                await SafeInvokeAsync("bblAnalytics.grant");
                await TrackCurrentPageAsync();
            }

            _nav.LocationChanged += OnLocationChanged;
        }

        public async Task SetConsentAsync(bool granted)
        {
            _consent = granted ? Granted : Denied;

            try
            {
                await _js.InvokeVoidAsync("localStorage.setItem", ConsentKey, _consent);
            }
            catch
            {
                // Storage unavailable (private mode) — the choice just won't persist.
            }

            if (_measurementId is null) return;

            if (granted)
            {
                await SafeInvokeAsync("bblAnalytics.grant");
                await TrackCurrentPageAsync();
            }
            else
            {
                await SafeInvokeAsync("bblAnalytics.revoke");
            }
        }

        /// <summary>Re-opens the banner so the visitor can change their mind.</summary>
        public void RequestBanner() => BannerRequested?.Invoke();

        private async Task<string?> ReadStoredConsentAsync()
        {
            try
            {
                return await _js.InvokeAsync<string?>("localStorage.getItem", ConsentKey);
            }
            catch
            {
                return null;
            }
        }

        private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            if (_consent != Granted) return;
            await TrackCurrentPageAsync();
        }

        private Task TrackCurrentPageAsync()
        {
            var path = "/" + _nav.ToBaseRelativePath(_nav.Uri);
            return SafeInvokeAsync("bblAnalytics.pageView", path, null);
        }

        private async Task SafeInvokeAsync(string identifier, params object?[] args)
        {
            try
            {
                await _js.InvokeVoidAsync(identifier, args);
            }
            catch
            {
                // Script blocked or unavailable — analytics is never critical.
            }
        }
    }
}
