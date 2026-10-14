// Scroll-in reveals for the home page.
//
// Elements marked data-reveal fade and rise into place the first time they
// come on screen; the children of a data-reveal-stagger element do the same
// one after another; numbers marked data-count count up from nothing.
//
// The stylesheet only hides those elements once this script has added
// html.reveal-ready, and that happens only when Motion has loaded and the
// visitor has not asked for less motion. Otherwise nothing is hidden and
// nothing moves. scan() can be called as often as the page re-renders: it
// skips anything it has already set up.
(function () {
    const motion = window.Motion;
    const reduced = window.matchMedia
        && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const enabled = !!(motion && typeof motion.animate === 'function'
        && typeof motion.inView === 'function' && !reduced);

    // Set as early as possible, so revealable content is hidden from the very
    // first paint rather than flashing up and then vanishing.
    if (enabled) document.documentElement.classList.add('reveal-ready');

    const DONE = 'revealBound';
    const rise = { opacity: [0, 1], y: [18, 0] };
    const ease = [0.22, 1, 0.36, 1];

    function reveal(el, delay) {
        motion.animate(el, rise, { duration: 0.7, delay: delay || 0, ease });
    }

    function countUp(el) {
        const target = parseInt(el.dataset.count, 10);
        if (!isFinite(target)) return;
        motion.animate(0, target, {
            duration: 1.1,
            ease: [0.16, 1, 0.3, 1],
            onUpdate: v => { el.textContent = Math.round(v); }
        });
    }

    function bind(el, onEnter) {
        if (el.dataset[DONE]) return;
        el.dataset[DONE] = '1';
        // Fires once, as soon as a sliver of it is on screen.
        motion.inView(el, () => { onEnter(); }, { amount: 0.15 });
    }

    window.homeReveal = {
        scan: function (rootId) {
            if (!enabled) return;
            const root = document.getElementById(rootId);
            if (!root) return;

            root.querySelectorAll('[data-reveal]').forEach(el =>
                bind(el, () => reveal(el)));

            root.querySelectorAll('[data-reveal-stagger]').forEach(group =>
                bind(group, () => Array.from(group.children)
                    .forEach((child, i) => reveal(child, i * 0.09))));

            // Numbers count up when they come on screen. Only ones that
            // already have their final value: a number still loading would
            // count up to the wrong thing.
            root.querySelectorAll('[data-count]').forEach(el => {
                if (el.dataset[DONE]) return;
                el.textContent = '0';
                bind(el, () => countUp(el));
            });
        }
    };
})();
