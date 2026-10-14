// Team of the Week page: saving the lineup as an image, and keeping the
// chosen week's chip in view in the week strip.
//
// The image is the poster element as it looks on a wide screen: it is held at
// a fixed width while snapdom captures it, so a download from a phone comes
// out the same as one from a desktop.
window.totwPage = {
    download: async function (elementId, filename) {
        const node = document.getElementById(elementId);
        if (!node || !window.snapdom) return false;

        const prev = { width: node.style.width, minWidth: node.style.minWidth, maxWidth: node.style.maxWidth };
        node.classList.add('is-exporting');
        try {
            node.style.width = '1080px';
            node.style.minWidth = '1080px';
            node.style.maxWidth = 'none';

            // Let the forced width lay out, and the fonts arrive, before capture.
            await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
            if (document.fonts && document.fonts.ready) {
                try { await document.fonts.ready; } catch (e) { }
            }

            const img = await window.snapdom.toPng(node, {
                scale: 2,
                embedFonts: true,
                backgroundColor: '#07101E'
            });

            // A blob: URL, because mobile browsers ignore the filename on data: URLs.
            const blob = await (await fetch(img.src)).blob();
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = filename || 'team-of-the-week.png';
            link.rel = 'noopener';
            document.body.appendChild(link);
            link.click();
            link.remove();
            setTimeout(() => URL.revokeObjectURL(url), 10000);
            return true;
        } catch (e) {
            console.error('totwPage.download failed', e);
            return false;
        } finally {
            node.classList.remove('is-exporting');
            node.style.width = prev.width;
            node.style.minWidth = prev.minWidth;
            node.style.maxWidth = prev.maxWidth;
        }
    },

    // Scrolls the strip itself, not the window (scrollIntoView would move both).
    centreChip: function (stripId) {
        const strip = document.getElementById(stripId);
        const chip = strip && strip.querySelector('.is-active');
        if (!chip) return;
        const left = chip.offsetLeft - (strip.clientWidth - chip.offsetWidth) / 2;
        strip.scrollTo({ left: Math.max(0, left), behavior: 'smooth' });
    }
};
