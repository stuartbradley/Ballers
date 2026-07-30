// Renders the poster to a PNG and triggers a download (snapdom).
// The poster's responsive rules are container-queries keyed off its own width,
// so we temporarily force it to the full 640px design width before capture.
// That means the downloaded image always looks like the desktop poster, even
// when exporting from a narrow phone screen.
window.teamSheetExport = {
    // Shrink any player name that overflows its column so it reads in full.
    // Called by the page after each render and again before capture, since the
    // poster is forced to 640px for the export and the fit differs at that width.
    fitNames: async function (elementId) {
        const node = document.getElementById(elementId);
        if (!node) return false;
        // Measurement is meaningless until the poster's webfonts have loaded.
        if (document.fonts && document.fonts.ready) {
            try { await document.fonts.ready; } catch (e) { }
        }
        fitPlayerNames(node);
        return true;
    },

    download: async function (elementId, filename) {
        const node = document.getElementById(elementId);
        if (!node || !window.snapdom) {
            console.error('teamSheetExport: node or snapdom not available');
            return false;
        }

        const target = node.firstElementChild || node; // the poster itself
        const prev = {
            width: target.style.width, maxWidth: target.style.maxWidth,
            minWidth: target.style.minWidth, flexShrink: target.style.flexShrink, margin: target.style.margin
        };

        let restoreAssets = () => { };

        try {
            // force the full design width so container queries render the desktop layout.
            // minWidth + flexShrink:0 stop the flex parent from shrinking it back on mobile.
            target.style.width = '640px';
            target.style.minWidth = '640px';
            target.style.maxWidth = 'none';
            target.style.flexShrink = '0';
            target.style.margin = '0';

            // Bake every image (img src + background-image) into a base64 data URI first.
            // Some mobile browsers (iOS Safari) fail to embed network images during capture
            // and render the alt text "img" instead — inlining removes the fetch entirely.
            restoreAssets = await inlineAssets(target);

            // let layout + container-query styles settle
            await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));

            if (document.fonts && document.fonts.ready) {
                try { await document.fonts.ready; } catch (e) { }
            }

            // Re-fit names at the forced 640px width, not whatever the screen was.
            // The slack matters: snapdom re-rasterizes the text, and slightly wider
            // font metrics there re-trigger the ellipsis on names that fit exactly
            // on screen. Leaving a few px spare absorbs that difference.
            fitPlayerNames(target, CAPTURE_SLACK_PX);
            if (window.snapdom.preCache) {
                try { await window.snapdom.preCache(target); } catch (e) { }
            }

            const img = await window.snapdom.toPng(target, {
                scale: 2,
                embedFonts: true,
                // Measure the real laid-out text instead of letting it re-flow at
                // its natural width under font fallback, which was clipping names.
                reconcile: true,
                backgroundColor: null
            });

            // Convert the data URL to a Blob + object URL. Mobile browsers ignore the
            // download filename on data: URLs (saving as "img"), but honour it on blob: URLs.
            const blob = await (await fetch(img.src)).blob();
            const url = URL.createObjectURL(blob);

            const link = document.createElement('a');
            link.href = url;
            link.download = filename || 'team-sheet.png';
            link.rel = 'noopener';
            document.body.appendChild(link);
            link.click();
            link.remove();
            setTimeout(() => URL.revokeObjectURL(url), 10000);
            return true;
        } catch (e) {
            console.error('teamSheetExport failed', e);
            return false;
        } finally {
            restoreAssets();
            target.style.width = prev.width;
            target.style.maxWidth = prev.maxWidth;
            target.style.minWidth = prev.minWidth;
            target.style.flexShrink = prev.flexShrink;
            target.style.margin = prev.margin;
            // Back to the on-screen width, so re-fit against it.
            fitPlayerNames(target);
        }
    }
};

// Player name spans, one class per poster variant.
const PLAYER_NAME_SELECTOR = '.ts-name, .sp-pname, .gf-pname, .cm-pname, .vt-pname';

// Spare width left when fitting for the capture, to absorb the font-metric
// difference between the live page and snapdom's rasterization.
const CAPTURE_SLACK_PX = 8;

// Most variants lay the squad out in a two-column grid, so each name is capped
// at roughly half the poster width and longer ones hit the ellipsis while the
// neighbouring column still looks empty. Step the font size down — only for the
// names that actually overflow — until each one fits, with a floor so a very
// long name degrades to the ellipsis rather than becoming unreadable.
// slackPx leaves that many pixels spare rather than fitting flush to the edge.
function fitPlayerNames(root, slackPx = 0) {
    for (const el of root.querySelectorAll(PLAYER_NAME_SELECTOR)) {
        // Reset to the stylesheet size first: this runs repeatedly (re-render,
        // variant switch, export) and must not compound earlier shrinks.
        if (el.dataset.tsBaseSize) {
            el.style.fontSize = '';
        }
        // Not laid out yet (hidden, or measured before the first paint) — every
        // name would look like it overflows and get shrunk to the floor.
        if (el.clientWidth === 0) continue;

        const base = parseFloat(getComputedStyle(el).fontSize);
        if (!base) continue;
        el.dataset.tsBaseSize = String(base);

        const min = base * 0.7;
        let size = base;
        // scrollWidth exceeds clientWidth only while the text is being clipped.
        while (el.scrollWidth > el.clientWidth - slackPx + 0.5 && size > min) {
            size = Math.max(min, size - 0.5);
            el.style.fontSize = size + 'px';
        }
    }
}

// Fetch a URL and return a base64 data URI.
//
// cache: 'reload' is deliberate. The poster's <img> tags load badges as plain
// (non-CORS) requests, which seeds the HTTP cache with a response this CORS
// fetch is not allowed to reuse — the browser reports it as a missing
// Access-Control-Allow-Origin header even when the server sends one. Reusing
// the cache here ('force-cache' or a revalidating 304) keeps hitting that
// stale entry, so bypass it and re-request as a proper CORS fetch.
// credentials: 'omit' keeps the request compatible with an origin-specific
// Access-Control-Allow-Origin.
async function toDataUri(url) {
    const resp = await fetch(url, { mode: 'cors', cache: 'reload', credentials: 'omit' });
    if (!resp.ok) throw new Error(`HTTP ${resp.status} fetching ${url}`);
    const blob = await resp.blob();
    return await new Promise((resolve, reject) => {
        const fr = new FileReader();
        fr.onload = () => resolve(fr.result);
        fr.onerror = reject;
        fr.readAsDataURL(blob);
    });
}

// Replace network images (img[src] and inline background-image url(...)) with
// base64 data URIs. Returns a function that restores the originals.
async function inlineAssets(root) {
    const restores = [];
    const cache = new Map();
    const get = async (u) => {
        if (!cache.has(u)) cache.set(u, await toDataUri(u));
        return cache.get(u);
    };

    // <img> elements
    for (const img of root.querySelectorAll('img')) {
        const src = img.getAttribute('src');
        if (!src || src.startsWith('data:')) continue;
        try {
            const data = await get(src);
            restores.push(() => img.setAttribute('src', src));
            img.setAttribute('src', data);
        } catch (e) {
            // Usually a missing Access-Control-Allow-Origin on a cross-origin
            // image. Blank the alt text so the capture shows an empty slot
            // rather than the alt string rendered as poster copy.
            console.warn('teamSheetExport: could not inline image', src, e);
            const alt = img.getAttribute('alt');
            if (alt) {
                restores.push(() => img.setAttribute('alt', alt));
                img.setAttribute('alt', '');
            }
        }
    }

    // inline background-image url(...)
    const urlRe = /url\((['"]?)([^'")]+)\1\)/i;
    const candidates = [root, ...root.querySelectorAll('[style*="background-image"]')];
    for (const el of candidates) {
        const bg = el.style && el.style.backgroundImage;
        if (!bg || bg.includes('data:')) continue;
        const m = bg.match(urlRe);
        if (!m) continue;
        try {
            const data = await get(m[2]);
            const orig = el.style.backgroundImage;
            restores.push(() => { el.style.backgroundImage = orig; });
            el.style.backgroundImage = `url("${data}")`;
        } catch (e) { /* leave original */ }
    }

    return () => { for (const r of restores) r(); };
}
