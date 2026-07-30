// Renders the poster to a PNG and triggers a download (snapdom).
// The poster's responsive rules are container-queries keyed off its own width,
// so we temporarily force it to the full 640px design width before capture.
// That means the downloaded image always looks like the desktop poster, even
// when exporting from a narrow phone screen.
window.teamSheetExport = {
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
            if (window.snapdom.preCache) {
                try { await window.snapdom.preCache(target); } catch (e) { }
            }

            const img = await window.snapdom.toPng(target, {
                scale: 2,
                embedFonts: true,
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
        }
    }
};

// Fetch a URL and return a base64 data URI.
async function toDataUri(url) {
    const resp = await fetch(url, { cache: 'force-cache' });
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
