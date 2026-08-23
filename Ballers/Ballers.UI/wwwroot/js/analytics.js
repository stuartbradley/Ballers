// Consent-gated Google Analytics 4 loader.
// Nothing is requested from Google and no cookies are set until grant() is called.
window.bblAnalytics = (function () {
    var measurementId = null;
    var scriptLoaded = false;

    function disableFlag() {
        return 'ga-disable-' + measurementId;
    }

    function clearGaCookies() {
        var host = window.location.hostname;
        var parts = document.cookie ? document.cookie.split(';') : [];

        for (var i = 0; i < parts.length; i++) {
            var name = parts[i].split('=')[0].trim();
            if (name.indexOf('_ga') !== 0 && name.indexOf('_gid') !== 0 && name.indexOf('_gat') !== 0) continue;

            document.cookie = name + '=; Max-Age=0; path=/';
            document.cookie = name + '=; Max-Age=0; path=/; domain=' + host;
            document.cookie = name + '=; Max-Age=0; path=/; domain=.' + host;
        }
    }

    function loadScript() {
        if (scriptLoaded) return;
        scriptLoaded = true;

        window.dataLayer = window.dataLayer || [];
        window.gtag = function () { window.dataLayer.push(arguments); };

        window.gtag('js', new Date());
        // Page views are sent manually so SPA navigations are counted correctly.
        window.gtag('config', measurementId, { anonymize_ip: true, send_page_view: false });

        var s = document.createElement('script');
        s.async = true;
        s.src = 'https://www.googletagmanager.com/gtag/js?id=' + encodeURIComponent(measurementId);
        document.head.appendChild(s);
    }

    return {
        configure: function (id) {
            measurementId = id;
        },

        grant: function () {
            if (!measurementId) return;
            window[disableFlag()] = false;
            loadScript();
        },

        revoke: function () {
            if (measurementId) window[disableFlag()] = true;
            clearGaCookies();
        },

        pageView: function (path, title) {
            if (!scriptLoaded || !window.gtag) return;
            window.gtag('event', 'page_view', {
                page_path: path,
                page_location: window.location.href,
                page_title: title || document.title
            });
        }
    };
})();
