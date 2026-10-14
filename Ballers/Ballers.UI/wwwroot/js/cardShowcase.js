// Drives the player card showcase on the home page.
//
// The story plays while the pointer is over the section, and eases to a stop
// where it is when the pointer leaves. A touch screen has no pointer to hover,
// so there it plays while the section is on screen. It runs in five acts:
//   0. the pack: the card lies face down, shakes, and turns over in a burst
//      of light (once only: the loop restarts after it);
//   1. the card: callouts explain the rating, photo, name and stats;
//   2. the stats: all six fly off the card, grow, count up and say what they
//      mean, then fly back;
//   3. the tiers: the other cards come out into a row, labelled with what
//      earns each, while a rating chip runs along it upgrading each card;
//   4. the season: the card turns over to show its season tally, and back.
//
// Every piece of the story is an ordinary Motion animation over one shared
// timeline, created paused and driven from a frame loop that owns the clock.
// Owning the clock is what lets playback ease in and out of a stop rather
// than freezing, and lets the act buttons jump anywhere.
//
// The same loop leans the card towards the pointer, drifts the dust, and
// counts the numbers. The loop and Motion never write to the same element:
// the acts live on .showcase-rotor and the fan cards, the lean on
// .showcase-tilt, their parent.
window.cardShowcase = {
    _instances: {},
    // Where an instance was when it was rebuilt, so the rebuild carries on.
    _resume: {},

    // Only a scale for the timeline's keyframes; playback speed is CYCLE_MS.
    DURATION: 10,

    // One full pass, pack opening included, at full speed.
    CYCLE_MS: 32000,

    // The acts, as fractions of the timeline. Everything else is timed off these.
    REVEAL: { shake: [0.015, 0.055], flip: [0.055, 0.10], burst: 0.078 },
    NOTES: [0.11, 0.265],
    STATS_OUT: [0.27, 0.31],
    STATS_IN: [0.43, 0.47],
    COUNT: [0.28, 0.35],
    SPREAD_OUT: [0.49, 0.54],
    SPREAD_IN: [0.69, 0.74],
    UPGRADE: { appear: 0.565, toSilver: [0.575, 0.605], toGold: [0.615, 0.645], leave: [0.66, 0.675] },
    TURN_OUT: [0.76, 0.82],
    TURN_BACK: [0.93, 0.99],

    // Where each act button jumps to (just before the act's first move), and
    // the ranges they light up for.
    ACTS: [
        { from: 0.00, jump: 0.105 },   // Card
        { from: 0.27, jump: 0.265 },   // Stats
        { from: 0.48, jump: 0.485 },   // Tiers
        { from: 0.75, jump: 0.755 }    // Season
    ],

    // How far each card in the row turns in towards the middle, in degrees,
    // left to right: the row curves round the viewer rather than lying flat.
    ROW_CURVE: [14, 6, -5, -14],
    TIER_ORDER: ['bronze', 'silver', 'gold', 'totw'],

    start: function (rootId, keepLayout) {
        this.stop(rootId, keepLayout);

        const root = document.getElementById(rootId);
        if (!root) return false;

        // Motion is a CDN script; if it failed to load, leave the markup
        // alone. Everything still renders, laid out flat.
        if (!window.Motion || typeof window.Motion.animate !== 'function') {
            root.classList.add('is-static');
            return false;
        }

        // Anyone who has asked for less motion gets the card laid out flat with
        // both faces visible and nothing moving at all.
        const reduced = window.matchMedia
            && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        if (reduced) {
            root.classList.add('is-static');
            return false;
        }

        const { animate, inView } = window.Motion;
        const D = this.DURATION;
        const T = this;

        const q = sel => root.querySelector(sel);
        const qa = sel => Array.from(root.querySelectorAll(sel));

        const rotor = q('.showcase-rotor');
        const tilt = q('.showcase-tilt');
        const stage = q('.showcase-stage');
        const card = q('.showcase-card');
        if (!rotor || !tilt || !stage || !card) return false;

        // Shows the act buttons and the play hint, then lets the layout settle
        // before anything is measured.
        const touch = window.matchMedia('(hover: none)').matches;
        root.classList.add('is-live');
        root.classList.toggle('is-touch', touch);
        void root.offsetHeight;

        const narrow = window.matchMedia('(max-width: 1060px)').matches;
        const clamp01 = v => Math.max(0, Math.min(1, v));
        const ease = k => 1 - Math.pow(1 - k, 3);

        const controls = [];
        const add = c => { if (c) { controls.push(c); } return c; };
        // Every animation runs on the same timeline: same duration, no repeat,
        // fractions of it in `times`. Note the option names: this build of
        // Motion takes `times` and `ease`, and silently ignores the older
        // `offset` and `easing`.
        const tl = (el, props, times, easing) => el && add(animate(el, props, {
            duration: D, times, ease: easing || 'linear'
        }));

        const turnEase = [0.65, 0, 0.35, 1];
        const flipEase = [0.34, 1.4, 0.64, 1];     // overshoots a touch and settles
        const outEase = [0.22, 1, 0.36, 1];        // quick away, long settle
        const backEase = [0.65, 0, 0.35, 1];

        // ── Measurements, taken flat-on before anything moves ────────────
        const W = card.offsetWidth || 300;
        const H = card.offsetHeight || 416;
        const centreX = tilt.offsetLeft + tilt.offsetWidth / 2;
        const centreY = tilt.offsetTop + tilt.offsetHeight / 2;
        const room = Math.max(stage.clientWidth - 16, W);

        const cardBox = card.getBoundingClientRect();
        const cardCX = cardBox.left + cardBox.width / 2;
        const cardCY = cardBox.top + cardBox.height / 2;
        const cardStats = qa('.sc-attrs .sc-attr-value');
        const statFrom = cardStats.map(el => {
            const r = el.getBoundingClientRect();
            return { x: r.left + r.width / 2 - cardCX, y: r.top + r.height / 2 - cardCY, h: r.height };
        });

        // Everything anchored to the card's centre in the stage.
        ['.sc-tiers', '.sc-bigstats', '.sc-sparks', '.sc-chip', '.sc-burst'].forEach(sel => {
            const el = q(sel);
            if (!el) return;
            el.style.left = centreX + 'px';
            el.style.top = centreY + 'px';
        });
        stage.style.setProperty('--floor-top', (centreY + 0.43 * H) + 'px');

        // ── The row ──────────────────────────────────────────────────────
        // Bronze, silver, gold, TOTW, left to right, sized from the room there
        // is: each card scaled to `s`, slots a little wider than that, and the
        // row centred.
        const s = Math.min(0.62, (room / W) / (3 * 1.12 + 1));
        const slot = 1.12 * s * W;
        const slotX = i => (i - 1.5) * slot;
        const slotOf = tier => Math.max(0, T.TIER_ORDER.indexOf(tier));
        // Fan cards scale about a point near their base; this lifts one back
        // level with the main card once shrunk.
        const level = -0.38 * H * (1 - s);

        const mainTier = rotor.dataset.tier || 'gold';
        const mainSlot = slotOf(mainTier);
        const mainX = slotX(mainSlot);
        const mainCurve = T.ROW_CURVE[mainSlot];

        const [sh0, sh1] = T.REVEAL.shake;
        const [fl0, fl1] = T.REVEAL.flip;
        const [so0, so1] = T.SPREAD_OUT;
        const [si0, si1] = T.SPREAD_IN;
        const [to0, to1] = T.TURN_OUT;
        const [tb0, tb1] = T.TURN_BACK;
        const flipMid = (fl0 + fl1) / 2;
        const outMid = (to0 + to1) / 2;
        const backMid = (tb0 + tb1) / 2;

        // ── The main card: reveal, row, and turn ─────────────────────────
        // Face down to start, then a shake, then over in one turn that
        // overshoots and settles. Every turn after that keeps going the same
        // way round, so the card never unwinds.
        tl(rotor, {
            rotateY: [180, 180, 180, 180, 180, 180, 360, 360, 360 + mainCurve, 360 + mainCurve, 360, 360, 540, 540, 720, 720],
            rotate:  [0, 0, -3, 3, -2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            x:       [0, 0, 0, 0, 0, 0, 0, 0, mainX, mainX, 0, 0, 0, 0, 0, 0],
            scale:   [1, 1, 1.02, 1.02, 1.02, 1, 1, 1, s, s, 1, 1, 1, 1, 1, 1]
        }, [0, sh0, sh0 + 0.01, sh0 + 0.02, sh0 + 0.03, sh1, fl1, so0, so1, si0, si1, to0, to1, tb0, tb1, 1],
        ['linear', 'easeInOut', 'easeInOut', 'easeInOut', 'easeInOut', flipEase, 'linear', outEase, 'linear', backEase, 'linear', turnEase, 'linear', turnEase, 'linear']);

        // The burst behind the card and the white-out on it as it lands.
        const burstAt = T.REVEAL.burst;
        tl(q('.sc-burst'), {
            opacity: [0, 0, 1, 0],
            scale: [0.3, 0.3, 1, 1.25],
            rotate: [0, 0, 20, 45]
        }, [0, burstAt - 0.02, burstAt, burstAt + 0.05], ['linear', 'easeOut', 'easeIn']);

        // The white-out lands on the main card as it turns face up, and again
        // when the rating chip reaches its slot in the row.
        const U = T.UPGRADE;
        const upgradeAt = { silver: U.toSilver[1], gold: U.toGold[1] };
        const flashTimes = [burstAt + 0.004];
        if (upgradeAt[mainTier]) flashTimes.push(upgradeAt[mainTier]);
        const flash = q('.sc-flash');
        if (flash) {
            const op = [0], times = [0];
            flashTimes.forEach(t => { op.push(0, 0.95, 0); times.push(t - 0.004, t, t + 0.03); });
            op.push(0); times.push(1);
            tl(flash, { opacity: op }, times, 'easeOut');
        }

        // ── The other tiers ──────────────────────────────────────────────
        // Out of the fan into the row, forward to the main card's depth and up
        // to full brightness; then back. The resting values repeat the
        // stylesheet's, so the hand-over to the script is seamless. Each card
        // leaves a beat after the one before, so they peel off rather than
        // move as a block.
        const POSES = {
            left:  { delay: 0.006, rest: { x: -0.2 * W, y: 0.02 * H, z: -200, rotateY: 20, rotate: -9, scale: 0.98, filter: 'brightness(0.5) saturate(0.85)' } },
            right: { delay: 0.012, rest: { x: 0.2 * W, y: 0.02 * H, z: -200, rotateY: -20, rotate: 9, scale: 0.98, filter: 'brightness(0.5) saturate(0.85)' } },
            back:  { delay: 0, rest: { x: 0, y: -0.05 * H, z: -260, rotateY: 0, rotate: 0, scale: 1.2, filter: 'brightness(0.75) saturate(1)' } }
        };

        qa('.sc-fan-card').forEach(el => {
            const pose = el.classList.contains('sc-fan-left') ? POSES.left
                : el.classList.contains('sc-fan-right') ? POSES.right : POSES.back;
            const at = slotOf(el.dataset.tier);
            const r = pose.rest;
            const row = {
                x: slotX(at), y: level, z: 0, rotateY: T.ROW_CURVE[at], rotate: 0,
                scale: s, filter: 'brightness(1) saturate(1)'
            };
            const kf = key => [r[key], r[key], row[key], row[key], r[key], r[key]];
            tl(el, {
                x: kf('x'), y: kf('y'), z: kf('z'), rotateY: kf('rotateY'),
                rotate: kf('rotate'), scale: kf('scale'), filter: kf('filter')
            }, [0, so0 + pose.delay, so1 + pose.delay, si0 + pose.delay, si1 + pose.delay, 1],
            ['linear', outEase, 'linear', backEase, 'linear']);

            const t = upgradeAt[el.dataset.tier];
            const f = el.querySelector('.sc-fan-flash');
            if (t && f) tl(f, { opacity: [0, 0, 0.95, 0, 0] }, [0, t - 0.004, t, t + 0.03, 1], 'easeOut');
        });

        // ── Labels under the row ─────────────────────────────────────────
        const labelTop = 0.43 * H * s + 4;
        qa('.sc-tier').forEach((label, i) => {
            label.style.setProperty('--tx', slotX(i) + 'px');
            label.style.setProperty('--ty', labelTop + 'px');
            label.style.setProperty('--tw', slot + 'px');
            const enter = so1 - 0.01 + (i * 0.008);
            tl(label, {
                opacity: [0, 0, 1, 1, 0, 0],
                y: [12, 12, 0, 0, -4, -4]
            }, [0, enter, enter + 0.03, si0 - 0.02, si0, 1],
            ['linear', 'easeOut', 'linear', 'easeIn', 'linear']);
        });

        // ── The upgrade chip ─────────────────────────────────────────────
        // Above the row, from bronze to silver to gold, counting the rating up
        // past each threshold; each card lights up as the chip reaches it.
        const chip = q('.sc-chip');
        const chipValue = q('.sc-chip-value');
        const silverAt = parseInt(root.dataset.silver, 10);
        const goldAt = parseInt(root.dataset.gold, 10);
        const haveThresholds = isFinite(silverAt) && isFinite(goldAt);
        if (chip) {
            const chipY = -0.5 * H * s - 30;
            tl(chip, {
                opacity: [0, 0, 1, 1, 1, 1, 1, 0, 0],
                x: [slotX(0), slotX(0), slotX(0), slotX(0), slotX(1), slotX(1), slotX(2), slotX(2), slotX(2)],
                y: [chipY + 10, chipY + 10, chipY, chipY, chipY, chipY, chipY, chipY - 8, chipY - 8],
                scale: [0.8, 0.8, 1, 1, 1, 1, 1, 0.9, 0.9]
            }, [0, U.appear, U.toSilver[0], U.toSilver[0], U.toSilver[1], U.toGold[0], U.toGold[1], U.leave[1], 1],
            ['linear', 'easeOut', 'linear', backEase, 'linear', backEase, 'easeIn', 'linear']);
            if (!haveThresholds) {
                const label = chip.querySelector('.sc-chip-label');
                if (label) label.textContent = 'Rating';
                if (chipValue) chipValue.textContent = '↑';
            }
        }

        // ── Sparks ───────────────────────────────────────────────────────
        // Bursts at the reveal and at each upgrade. Each spark is placed once
        // and given its own keyframes, so a burst scrubs backwards cleanly.
        const sparks = q('.sc-sparks');
        if (sparks) {
            sparks.innerHTML = '';
            const scale = W / 330;
            const bursts = [
                { x: 0, y: 0, t: burstAt, n: narrow ? 14 : 22, reach: 1.25 },
                { x: slotX(1), y: 0, t: upgradeAt.silver, n: narrow ? 8 : 14, reach: 0.7 },
                { x: slotX(2), y: 0, t: upgradeAt.gold, n: narrow ? 10 : 18, reach: 0.85 }
            ];
            // A fixed seed, so the sparks fly the same way every visit.
            let seed = 7;
            const rand = () => { seed = (seed * 16807) % 2147483647; return seed / 2147483647; };
            bursts.forEach(b => {
                for (let i = 0; i < b.n; i++) {
                    const el = document.createElement('span');
                    el.className = 'sc-spark';
                    sparks.appendChild(el);
                    const a = (i / b.n) * Math.PI * 2 + rand() * 0.6;
                    const d = (90 + rand() * 120) * scale * b.reach;
                    const dx = Math.cos(a) * d, dy = Math.sin(a) * d;
                    tl(el, {
                        opacity: [0, 0, 1, 0, 0],
                        x: [b.x, b.x, b.x + dx * 0.55, b.x + dx, b.x + dx],
                        y: [b.y, b.y, b.y + dy * 0.55, b.y + dy + 30 * scale, b.y + dy + 30 * scale],
                        scale: [0.4, 0.4, 1.3, 0.3, 0.3]
                    }, [0, b.t, b.t + 0.008, b.t + 0.045, 1], ['linear', 'easeOut', 'easeIn', 'linear']);
                }
            });
        }

        // ── The six stats, blown up ──────────────────────────────────────
        // Each one starts exactly over its stat on the card and flies out to
        // its place around it. Where there is room they land either side, in
        // the card's own two columns; on a narrow screen, in a grid over the
        // card with the card darkened behind them. Their numbers count up and
        // their bars fill as they arrive (in the frame loop, below).
        const [st0, st1] = T.STATS_OUT;
        const [sn0, sn1] = T.STATS_IN;
        const bigStats = q('.sc-bigstats');
        const bigItems = qa('.sc-bigstat');
        const cap = parseInt(root.dataset.cap, 10) || 99;
        const counters = [];
        if (bigStats && bigItems.length === statFrom.length && bigItems.length) {
            const gap = 40;
            const sideWidth = 210;
            const asGrid = room < W + 2 * (gap + sideWidth);
            root.classList.toggle('sc-stats-grid', asGrid);
            const bw = asGrid ? Math.min(165, (room - 12) / 2) : sideWidth;
            bigStats.style.setProperty('--bw', bw + 'px');

            const backdrop = q('.sc-bigstats-backdrop');
            if (backdrop && asGrid) {
                backdrop.style.left = centreX + 'px';
                backdrop.style.top = centreY + 'px';
                backdrop.style.width = Math.min(room, W * 1.25) + 'px';
                backdrop.style.height = (H * 1.02) + 'px';
                tl(backdrop, { opacity: [0, 0, 1, 1, 0, 0] }, [0, st0, st1, sn0, sn1, 1],
                    ['linear', 'easeOut', 'linear', 'easeIn', 'linear']);
            }

            bigItems.forEach((item, i) => {
                const valueEl = item.querySelector('.sc-bigstat-value');
                const from = statFrom[i];
                const fromScale = Math.max(0.15, from.h / ((valueEl && valueEl.offsetHeight) || 50));
                const col = i % 2;
                const row = Math.floor(i / 2);
                const side = col === 0 ? -1 : 1;
                const toX = asGrid
                    ? side * (bw / 2 + 6)
                    : side * (W / 2 + gap + bw / 2) * (row === 1 ? 1.07 : 1);
                const toY = (row - 1) * H * (asGrid ? 0.3 : 0.29);
                const d = i * 0.005;
                tl(item, {
                    opacity: [0, 0, 1, 1, 0, 0],
                    x: [from.x, from.x, toX, toX, from.x, from.x],
                    y: [from.y, from.y, toY, toY, from.y, from.y],
                    scale: [fromScale, fromScale, 1, 1, fromScale, fromScale]
                }, [0, st0 + d, st1 + d, sn0 + d, sn1 + d, 1],
                ['linear', outEase, 'linear', backEase, 'linear']);

                const value = parseInt(item.dataset.value, 10) || 0;
                counters.push({
                    el: valueEl,
                    fill: item.querySelector('.sc-bigstat-fill'),
                    value,
                    share: Math.min(1, value / cap),
                    delay: i * 0.004,
                    shown: -1
                });
            });

            // The stats on the card fade while their big copies are out, so
            // there is only ever one of each.
            cardStats.forEach((el, i) => {
                const d = i * 0.005;
                tl(el, { opacity: [1, 1, 0, 0, 1, 1] },
                    [0, st0 + d, st0 + d + 0.008, sn1 + d - 0.008, sn1 + d, 1]);
            });
        }

        // ── Surface: foil, spectral wash, edge, shadow ───────────────────
        // The highlight crosses once while the front is held, and resets while
        // the back is showing, so the loop point has nothing to jump.
        tl(q('.sc-foil'), { backgroundPosition: ['100% 0', '100% 0', '0% 0', '0% 0', '100% 0', '100% 0'] },
            [0, 0.11, 0.24, 0.84, 0.85, 1], ['linear', 'easeInOut', 'linear', 'linear', 'linear']);

        qa('.sc-holo').forEach(holo => tl(holo, {
            backgroundPosition: ['0% 50%', '0% 50%', '100% 50%', '0% 50%']
        }, [0, fl1, 0.55, 1], ['linear', 'easeInOut', 'easeInOut']));

        // Edge light, peaking at each moment the card is side-on.
        tl(q('.sc-edge'), {
            opacity: [0, 0, 0.9, 0, 0, 0.9, 0, 0, 0.9, 0, 0]
        }, [0, flipMid - 0.012, flipMid, flipMid + 0.012,
            outMid - 0.014, outMid, outMid + 0.014,
            backMid - 0.014, backMid, backMid + 0.014, 1], 'easeInOut');

        // The shadow follows the card into the row and narrows as it turns
        // side-on to the floor.
        tl(q('.sc-shadow'), {
            x: [0, 0, 0, 0, mainX, mainX, 0, 0, 0, 0, 0, 0, 0, 0],
            scaleX: [1, 1, 0.34, 1, s, s, 1, 1, 0.34, 1, 1, 0.34, 1, 1],
            opacity: [1, 1, 0.45, 1, 0.6, 0.6, 1, 1, 0.45, 1, 1, 0.45, 1, 1]
        }, [0, fl0, flipMid, fl1, so1, si0, si1, to0, outMid, to1, tb0, backMid, tb1, 1],
        ['linear', 'easeIn', 'easeOut', outEase, 'linear', backEase, 'linear',
            'easeIn', 'easeOut', 'linear', 'easeIn', 'easeOut', 'linear']);

        // ── Callouts ─────────────────────────────────────────────────────
        // Side by side on a wide screen, staggered in. On a narrow one they
        // share one slot under the card, so they take turns.
        const [n0, n1] = T.NOTES;
        const notes = qa('.sc-notes .sc-note');
        notes.forEach((note, i) => {
            if (narrow) {
                const seg = (n1 - n0) / notes.length;
                const a = n0 + i * seg;
                tl(note, { opacity: [0, 0, 1, 1, 0, 0], y: [8, 8, 0, 0, -6, -6] },
                    [0, a, a + 0.008, a + seg - 0.008, a + seg - 0.001, 1],
                    ['linear', 'easeOut', 'linear', 'easeIn', 'linear']);
            } else {
                const enter = n0 + (i * 0.025);
                tl(note, { opacity: [0, 0, 1, 1, 0, 0], y: [10, 10, 0, 0, 0, 10] },
                    [0, enter, enter + 0.025, n1 - 0.015, n1, 1],
                    ['linear', 'easeOut', 'linear', 'easeIn', 'linear']);
            }
        });

        // ── The numbers ticking up while the front is held ───────────────
        // They tick back while the card is showing its back, so the end of
        // the timeline matches the point the loop restarts from.
        const swap = [0, 0.15, 0.18, 0.84, 0.85, 1];
        qa('.sc-num-before').forEach(el => tl(el, { opacity: [1, 1, 0, 0, 1, 1] }, swap));
        qa('.sc-num-after').forEach(el => tl(el, { opacity: [0, 0, 1, 1, 0, 0] }, swap));
        tl(q('.sc-pop'), {
            opacity: [0, 0, 1, 1, 0, 0],
            y: [8, 8, -2, -8, -16, -16],
            scale: [0.8, 0.8, 1.06, 1, 0.94, 0.94]
        }, [0, 0.15, 0.18, 0.23, 0.255, 1], ['linear', 'easeOut', 'easeInOut', 'easeIn', 'linear']);

        // ── Season tally on the back ─────────────────────────────────────
        qa('.sc-tally li').forEach((row, i) => {
            const enter = to1 + 0.01 + (i * 0.02);
            tl(row, { opacity: [0, 0, 1, 1], x: [-14, -14, 0, 0] },
                [0, enter, enter + 0.025, 1], ['linear', 'easeOut', 'linear']);
        });
        tl(q('.sc-back-foot'), { opacity: [0, 0, 1, 1] }, [0, to1 + 0.075, to1 + 0.1, 1],
            ['linear', 'easeOut', 'linear']);

        // Paused at the start; the frame loop owns the clock from here.
        controls.forEach(c => { try { c.pause(); c.time = 0; } catch { /* nothing to pause */ } });

        // ── Playback ─────────────────────────────────────────────────────
        // A rebuild (after a resize) picks up where the last one was.
        let progress = T._resume[rootId] || 0;
        delete T._resume[rootId];
        let shownProgress = -1;

        // The loop restarts just after the pack opening, which only plays
        // once. The end of the timeline is the same pose as that point.
        const loopFrom = fl1;

        const seek = p => {
            const t = p * D;
            for (const c of controls) {
                try { c.time = t; } catch { /* finished or cancelled */ }
            }
        };

        // Hover (or keyboard focus inside the section) plays; leaving lets it
        // ease to a stop. A touch screen plays while the section is on screen.
        let hovered = false, focused = false, onScreen = false;
        const wantsToPlay = () => touch ? onScreen : (hovered || focused);
        const syncPlaying = () => root.classList.toggle('is-playing', wantsToPlay());
        const onEnter = () => { hovered = true; syncPlaying(); };
        const onExit = () => { hovered = false; syncPlaying(); };
        const onFocusIn = () => { focused = true; syncPlaying(); };
        const onFocusOut = e => { if (!root.contains(e.relatedTarget)) { focused = false; syncPlaying(); } };
        root.addEventListener('pointerenter', onEnter);
        root.addEventListener('pointerleave', onExit);
        root.addEventListener('focusin', onFocusIn);
        root.addEventListener('focusout', onFocusOut);

        // ── Act buttons ──────────────────────────────────────────────────
        const actButtons = qa('.sc-act');
        const onActClick = e => {
            const act = T.ACTS[parseInt(e.currentTarget.dataset.act, 10)];
            if (!act) return;
            progress = act.jump;
            shownProgress = -1;
        };
        actButtons.forEach(b => b.addEventListener('click', onActClick));
        let currentAct = -1;

        // ── The lean ─────────────────────────────────────────────────────
        const REST_X = 7;
        const MAX_X = 11;
        const MAX_Y = 16;
        const LEAN = 0.085;
        // How quickly playback speeds up to full and slows to a stop.
        const SPEED_EASE = 0.06;
        let speed = 0;
        let last = 0;

        let targetX = 0, targetY = 0, targetGlare = 0;
        let curX = 0, curY = 0, curGlare = 0;

        const clamp = v => Math.max(-1, Math.min(1, v));
        const onMove = e => {
            const box = stage.getBoundingClientRect();
            targetX = clamp((e.clientX - (box.left + box.width / 2)) / (W * 0.75));
            targetY = clamp((e.clientY - (box.top + box.height / 2)) / (H * 0.75));
            targetGlare = 1;
        };
        const onLeave = () => { targetX = 0; targetY = 0; targetGlare = 0; };
        stage.addEventListener('pointermove', onMove);
        stage.addEventListener('pointerleave', onLeave);
        stage.addEventListener('pointercancel', onLeave);

        // ── Dust ─────────────────────────────────────────────────────────
        // A few dozen motes drifting up through the floodlights. Drawn on one
        // canvas at no more than 1.5x, so it costs next to nothing.
        const canvas = q('.sc-dust');
        const ctx = canvas && canvas.getContext ? canvas.getContext('2d') : null;
        let motes = [];
        const sizeCanvas = () => {
            if (!canvas || !ctx) return;
            const dpr = Math.min(1.5, window.devicePixelRatio || 1);
            canvas.width = Math.round(canvas.clientWidth * dpr);
            canvas.height = Math.round(canvas.clientHeight * dpr);
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
            const count = narrow ? 18 : 42;
            let seed = 11;
            const rand = () => { seed = (seed * 16807) % 2147483647; return seed / 2147483647; };
            motes = Array.from({ length: count }, () => ({
                x: rand() * canvas.clientWidth,
                y: rand() * canvas.clientHeight,
                r: 0.6 + rand() * 1.5,
                vy: 0.08 + rand() * 0.22,
                phase: rand() * Math.PI * 2,
                a: 0.15 + rand() * 0.45
            }));
        };
        sizeCanvas();
        const drawDust = t => {
            if (!ctx) return;
            const w = canvas.clientWidth, h = canvas.clientHeight;
            ctx.clearRect(0, 0, w, h);
            for (const m of motes) {
                m.y -= m.vy;
                if (m.y < -4) { m.y = h + 4; }
                const x = m.x + Math.sin(t * 0.0004 + m.phase) * 14;
                const twinkle = 0.6 + 0.4 * Math.sin(t * 0.002 + m.phase);
                ctx.globalAlpha = m.a * twinkle;
                ctx.fillStyle = '#FFF2C8';
                ctx.beginPath();
                ctx.arc(x, m.y, m.r, 0, Math.PI * 2);
                ctx.fill();
            }
            ctx.globalAlpha = 1;
        };

        // ── The frame loop ───────────────────────────────────────────────
        let frame = 0;
        const t0 = performance.now();
        const [c0, c1] = T.COUNT;
        const tierValue = p => {
            const [a0, a1] = U.toSilver, [b0, b1] = U.toGold;
            if (p <= a0) return 0;
            if (p <= a1) return Math.round(silverAt * ease((p - a0) / (a1 - a0)));
            if (p <= b0) return silverAt;
            if (p <= b1) return Math.round(silverAt + (goldAt - silverAt) * ease((p - b0) / (b1 - b0)));
            return goldAt;
        };
        let chipShown = -1;

        const tick = now => {
            frame = requestAnimationFrame(tick);

            // Advance the clock, at a speed that eases towards full while
            // playing and towards nothing when not, so it never lurches. A
            // frame gap after a hidden tab is capped rather than skipped over.
            const dt = last ? Math.min(64, now - last) : 16;
            last = now;
            speed += ((wantsToPlay() ? 1 : 0) - speed) * SPEED_EASE;
            if (speed < 0.001) speed = 0;
            progress += speed * dt / T.CYCLE_MS;
            if (progress >= 1) progress = loopFrom + (progress - 1);

            if (Math.abs(progress - shownProgress) > 0.00005) {
                seek(progress);
                shownProgress = progress;

                // Counters: the big stats count up and fill as they land.
                for (const c of counters) {
                    const k = ease(clamp01((progress - c0 - c.delay) / (c1 - c0)));
                    const v = Math.round(c.value * k);
                    if (v !== c.shown && c.el) { c.el.textContent = v; c.shown = v; }
                    if (c.fill) c.fill.style.transform = `scaleX(${(c.share * k).toFixed(4)})`;
                }

                if (haveThresholds && chipValue) {
                    const v = tierValue(progress);
                    if (v !== chipShown) { chipValue.textContent = v; chipShown = v; }
                }

                let act = 0;
                T.ACTS.forEach((a, i) => { if (progress >= a.from - 0.001) act = i; });
                if (act !== currentAct) {
                    actButtons.forEach((b, i) => {
                        b.classList.toggle('is-current', i === act);
                        if (i === act) b.setAttribute('aria-current', 'step');
                        else b.removeAttribute('aria-current');
                    });
                    currentAct = act;
                }
            }

            // The lean, with an idle drift that fades out as the pointer
            // takes over, so the hand-over has no step in it.
            curX += (targetX - curX) * LEAN;
            curY += (targetY - curY) * LEAN;
            curGlare += (targetGlare - curGlare) * LEAN;
            const t = (now - t0) / 1000;
            const idle = 1 - curGlare;
            const driftY = Math.sin(t * 0.55) * 5 * idle;
            const driftX = Math.cos(t * 0.4) * 1.6 * idle;
            const bob = Math.sin(t * 0.8) * 5;
            tilt.style.transform =
                `translateY(${bob.toFixed(2)}px) `
                + `rotateX(${(REST_X - curY * MAX_X + driftX).toFixed(2)}deg) `
                + `rotateY(${(curX * MAX_Y + driftY).toFixed(2)}deg)`;

            const gx = 50 + curX * 42;
            root.style.setProperty('--gx', gx.toFixed(1) + '%');
            root.style.setProperty('--gx-back', (100 - gx).toFixed(1) + '%');
            root.style.setProperty('--gy', (50 + curY * 42).toFixed(1) + '%');
            root.style.setProperty('--ga', curGlare.toFixed(3));

            drawDust(now);
        };

        const startLoop = () => { if (!frame) { last = 0; frame = requestAnimationFrame(tick); } };
        const stopLoop = () => { if (frame) cancelAnimationFrame(frame); frame = 0; };

        // Land on the right frame straight away, before the first tick.
        seek(progress);

        // Rebuilt on a real change of width (not a phone's address bar sliding
        // away), since the row and the stats are laid out from measurements.
        let resizeTimer = 0;
        const startWidth = stage.clientWidth;
        const onResize = () => {
            clearTimeout(resizeTimer);
            resizeTimer = setTimeout(() => {
                if (Math.abs(stage.clientWidth - startWidth) <= 30) { sizeCanvas(); return; }
                T._resume[rootId] = progress;
                T.start(rootId, true);
            }, 250);
        };
        window.addEventListener('resize', onResize);

        // No point burning frames on a section nobody is looking at. On a
        // touch screen, being mostly on screen is also what plays it.
        let stopWatching = null, stopWatchingPlay = null;
        if (typeof inView === 'function') {
            stopWatching = inView(root, () => {
                startLoop();
                return () => stopLoop();
            }, { amount: 0 });
            if (touch) {
                stopWatchingPlay = inView(stage, () => {
                    onScreen = true; syncPlaying();
                    return () => { onScreen = false; syncPlaying(); };
                }, { amount: 0.5 });
            }
        } else {
            onScreen = true;
            syncPlaying();
            startLoop();
        }

        this._instances[rootId] = {
            controls,
            teardown: keep => {
                stopLoop();
                clearTimeout(resizeTimer);
                window.removeEventListener('resize', onResize);
                stage.removeEventListener('pointermove', onMove);
                stage.removeEventListener('pointerleave', onLeave);
                stage.removeEventListener('pointercancel', onLeave);
                actButtons.forEach(b => b.removeEventListener('click', onActClick));
                root.removeEventListener('pointerenter', onEnter);
                root.removeEventListener('pointerleave', onExit);
                root.removeEventListener('focusin', onFocusIn);
                root.removeEventListener('focusout', onFocusOut);
                if (typeof stopWatching === 'function') stopWatching();
                if (typeof stopWatchingPlay === 'function') stopWatchingPlay();
                tilt.style.transform = '';
                ['--gx', '--gx-back', '--gy', '--ga'].forEach(p => root.style.removeProperty(p));
                if (sparks) sparks.innerHTML = '';
                // A rebuild keeps the live layout, so nothing reflows under it.
                if (!keep) root.classList.remove('is-live', 'is-touch', 'is-playing', 'sc-stats-grid');
            }
        };

        return true;
    },

    stop: function (rootId, keepLayout) {
        const instance = this._instances[rootId];
        if (!instance) return;

        for (const c of instance.controls) {
            try { c.cancel(); } catch { /* nothing to cancel */ }
        }
        instance.teardown(!!keepLayout);
        delete this._instances[rootId];
    }
};
