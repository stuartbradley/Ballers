// Watches a sentinel element at the foot of a gallery and tells Blazor to fetch
// the next page when it comes into view. A full match gallery is a few hundred
// photos, so they load a chunk at a time as you scroll rather than all at once.
window.galleryScroll = {
    _observers: {},

    observe: function (sentinelId, dotNetRef) {
        this.disconnect(sentinelId);

        const sentinel = document.getElementById(sentinelId);
        if (!sentinel || !('IntersectionObserver' in window)) return false;

        const observer = new IntersectionObserver(entries => {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    dotNetRef.invokeMethodAsync('LoadMoreAsync');
                }
            }
        }, {
            // Start fetching before the sentinel is actually on screen, so the
            // next photos are usually there by the time you scroll to them.
            rootMargin: '600px 0px'
        });

        observer.observe(sentinel);
        this._observers[sentinelId] = observer;
        return true;
    },

    disconnect: function (sentinelId) {
        const existing = this._observers[sentinelId];
        if (existing) {
            existing.disconnect();
            delete this._observers[sentinelId];
        }
    },

    // Keeps the lightbox's current thumbnail in the middle of its strip.
    // Scrolls the strip only, not the page.
    centreThumb: function (stripId) {
        const strip = document.getElementById(stripId);
        const thumb = strip && strip.querySelector('.is-active');
        if (!thumb) return;
        const left = thumb.offsetLeft - (strip.clientWidth - thumb.offsetWidth) / 2;
        strip.scrollTo({ left: Math.max(0, left), behavior: 'smooth' });
    },

    // Saves a full-size photo. Photos come from the API's origin, where the
    // download attribute is ignored, so it is fetched and saved as a blob. If
    // that fails the photo opens in a new tab to save from there.
    download: async function (url, filename) {
        try {
            const resp = await fetch(url, { mode: 'cors', credentials: 'omit' });
            if (!resp.ok) throw new Error('HTTP ' + resp.status);
            const blobUrl = URL.createObjectURL(await resp.blob());
            const link = document.createElement('a');
            link.href = blobUrl;
            link.download = filename || 'ballers-photo.jpg';
            document.body.appendChild(link);
            link.click();
            link.remove();
            setTimeout(() => URL.revokeObjectURL(blobUrl), 10000);
        } catch (e) {
            window.open(url, '_blank', 'noopener');
        }
    },

    // The phone's share sheet where there is one, otherwise the link is
    // copied. Returns what happened, for the button to say so.
    share: async function (title, url) {
        if (navigator.share) {
            try {
                await navigator.share({ title: title, url: url });
                return 'shared';
            } catch (e) {
                if (e && e.name === 'AbortError') return 'cancelled';
            }
        }
        try {
            await navigator.clipboard.writeText(url);
            return 'copied';
        } catch (e) {
            return 'failed';
        }
    }
};
