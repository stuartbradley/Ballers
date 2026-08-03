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
    }
};
