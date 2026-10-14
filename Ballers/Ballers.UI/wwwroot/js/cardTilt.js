// Leans a card towards the pointer and moves a highlight across it.
//
// One listener on a container serves every card inside it, including cards
// rendered after binding. The script only sets CSS variables on the card under
// the pointer (--rx, --ry: the lean in degrees; --gx, --gy: where the light
// sits, in %); the page's stylesheet decides what they do. Nothing happens on
// touch screens, or for anyone who has asked for less motion.
window.cardTilt = {
    _bound: {},

    bind: function (containerId, selector, maxDeg) {
        const root = document.getElementById(containerId);
        if (!root || this._bound[containerId] === root) return;

        const reduced = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        const hover = window.matchMedia && window.matchMedia('(hover: hover)').matches;
        if (reduced || !hover) return;

        const max = maxDeg || 8;
        let active = null;

        const reset = card => {
            if (!card) return;
            card.style.setProperty('--rx', '0deg');
            card.style.setProperty('--ry', '0deg');
            card.style.setProperty('--glow', '0');
        };

        root.addEventListener('pointermove', e => {
            const card = e.target.closest(selector);
            if (card !== active) { reset(active); active = card; }
            if (!card || !root.contains(card)) return;

            const r = card.getBoundingClientRect();
            const x = (e.clientX - r.left) / r.width;   // 0..1
            const y = (e.clientY - r.top) / r.height;
            card.style.setProperty('--ry', ((x - 0.5) * 2 * max).toFixed(2) + 'deg');
            card.style.setProperty('--rx', ((0.5 - y) * 2 * max).toFixed(2) + 'deg');
            card.style.setProperty('--gx', (x * 100).toFixed(1) + '%');
            card.style.setProperty('--gy', (y * 100).toFixed(1) + '%');
            card.style.setProperty('--glow', '1');
        });

        root.addEventListener('pointerleave', () => { reset(active); active = null; });

        this._bound[containerId] = root;
    }
};
