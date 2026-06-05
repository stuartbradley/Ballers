// Renders a DOM node to a PNG and triggers a download (snapdom).
// snapdom reproduces gradients, clip-paths, filters, transforms and — with
// embedFonts + fonts.ready + preCache — the custom web fonts, so the exported
// PNG matches the on-screen poster.
window.teamSheetExport = {
    download: async function (elementId, filename) {
        const node = document.getElementById(elementId);
        if (!node || !window.snapdom) {
            console.error('teamSheetExport: node or snapdom not available');
            return false;
        }
        try {
            const target = node.firstElementChild || node; // the poster itself

            // make sure web fonts are loaded and cached before capture
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

            const link = document.createElement('a');
            link.download = filename || 'team-sheet.png';
            link.href = img.src;
            link.click();
            return true;
        } catch (e) {
            console.error('teamSheetExport failed', e);
            return false;
        }
    }
};
