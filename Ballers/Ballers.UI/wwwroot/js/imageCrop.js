window.imageCrop = {
    _instances: {},

    init: function (containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;

        // Clean up any previous instance for this container
        this.destroy(containerId);

        const img = container.querySelector('img');
        if (!img) return;

        const state = {
            x: 0, y: 0, scale: 1,
            dragging: false, lastX: 0, lastY: 0, lastDist: 0
        };
        this._instances[containerId] = state;

        const applyTransform = () => {
            img.style.transform = `translate(${state.x}px, ${state.y}px) scale(${state.scale})`;
        };

        const initCoverScale = () => {
            const rect = container.getBoundingClientRect();
            if (rect.width === 0 || img.naturalWidth === 0) return;

            img.style.position = 'absolute';
            img.style.left = '0';
            img.style.top = '0';
            img.style.width = img.naturalWidth + 'px';
            img.style.height = img.naturalHeight + 'px';
            img.style.maxWidth = 'none';
            img.style.maxHeight = 'none';
            img.style.transformOrigin = '0 0';
            img.style.userSelect = 'none';
            img.style.pointerEvents = 'none';

            const coverScale = Math.max(rect.width / img.naturalWidth, rect.height / img.naturalHeight);
            state.scale = coverScale;
            state.initialScale = coverScale;
            state.x = (rect.width - img.naturalWidth * coverScale) / 2;
            state.y = (rect.height - img.naturalHeight * coverScale) / 2;
            applyTransform();
        };

        if (img.complete && img.naturalWidth > 0) initCoverScale();
        else img.addEventListener('load', initCoverScale);

        // Mouse drag
        const onMouseDown = e => {
            state.dragging = true;
            state.lastX = e.clientX;
            state.lastY = e.clientY;
            container.style.cursor = 'grabbing';
            e.preventDefault();
        };

        const onMouseMove = e => {
            if (!state.dragging) return;
            state.x += e.clientX - state.lastX;
            state.y += e.clientY - state.lastY;
            state.lastX = e.clientX;
            state.lastY = e.clientY;
            applyTransform();
        };

        const onMouseUp = () => {
            state.dragging = false;
            container.style.cursor = 'grab';
        };

        container.addEventListener('mousedown', onMouseDown);
        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup', onMouseUp);

        // Wheel zoom toward cursor
        const onWheel = e => {
            e.preventDefault();
            const rect = container.getBoundingClientRect();
            const mouseX = e.clientX - rect.left;
            const mouseY = e.clientY - rect.top;
            const prevScale = state.scale;
            state.scale = Math.max(0.2, Math.min(10, state.scale * (1 - e.deltaY * 0.001)));
            const ratio = state.scale / prevScale;
            state.x = mouseX - ratio * (mouseX - state.x);
            state.y = mouseY - ratio * (mouseY - state.y);
            applyTransform();
        };
        container.addEventListener('wheel', onWheel, { passive: false });

        // Touch drag + pinch zoom
        const onTouchStart = e => {
            if (e.touches.length === 1) {
                state.dragging = true;
                state.lastX = e.touches[0].clientX;
                state.lastY = e.touches[0].clientY;
            } else if (e.touches.length === 2) {
                state.dragging = false;
                state.lastDist = Math.hypot(
                    e.touches[0].clientX - e.touches[1].clientX,
                    e.touches[0].clientY - e.touches[1].clientY
                );
            }
            e.preventDefault();
        };

        const onTouchMove = e => {
            if (e.touches.length === 1 && state.dragging) {
                state.x += e.touches[0].clientX - state.lastX;
                state.y += e.touches[0].clientY - state.lastY;
                state.lastX = e.touches[0].clientX;
                state.lastY = e.touches[0].clientY;
                applyTransform();
            } else if (e.touches.length === 2) {
                const dist = Math.hypot(
                    e.touches[0].clientX - e.touches[1].clientX,
                    e.touches[0].clientY - e.touches[1].clientY
                );
                if (state.lastDist > 0) {
                    state.scale = Math.max(0.2, Math.min(10, state.scale * dist / state.lastDist));
                    applyTransform();
                }
                state.lastDist = dist;
            }
            e.preventDefault();
        };

        const onTouchEnd = () => { state.dragging = false; state.lastDist = 0; };

        container.addEventListener('touchstart', onTouchStart, { passive: false });
        container.addEventListener('touchmove', onTouchMove, { passive: false });
        container.addEventListener('touchend', onTouchEnd);

        // Store refs for cleanup
        state._cleanup = () => {
            container.removeEventListener('mousedown', onMouseDown);
            document.removeEventListener('mousemove', onMouseMove);
            document.removeEventListener('mouseup', onMouseUp);
            container.removeEventListener('wheel', onWheel);
            container.removeEventListener('touchstart', onTouchStart);
            container.removeEventListener('touchmove', onTouchMove);
            container.removeEventListener('touchend', onTouchEnd);
        };
    },

    reset: function (containerId) {
        const state = this._instances[containerId];
        if (!state) return;
        const container = document.getElementById(containerId);
        if (!container) return;
        const img = container.querySelector('img');
        if (!img || !img.naturalWidth) return;

        const rect = container.getBoundingClientRect();
        state.scale = Math.max(rect.width / img.naturalWidth, rect.height / img.naturalHeight);
        state.x = (rect.width - img.naturalWidth * state.scale) / 2;
        state.y = (rect.height - img.naturalHeight * state.scale) / 2;
        img.style.transform = `translate(${state.x}px, ${state.y}px) scale(${state.scale})`;
    },

    getBase64: function (containerId) {
        const container = document.getElementById(containerId);
        if (!container) return null;
        const img = container.querySelector('img');
        if (!img || !img.complete || !img.naturalWidth) return null;

        const state = this._instances[containerId];
        if (!state) return null;

        const rect = container.getBoundingClientRect();
        // 2x output for quality
        const canvas = document.createElement('canvas');
        canvas.width = Math.round(rect.width * 2);
        canvas.height = Math.round(rect.height * 2);
        const ctx = canvas.getContext('2d');

        // Replicate the CSS transform: translate(x,y) scale(s) from origin 0 0
        // at 2x resolution
        ctx.scale(2, 2);
        ctx.translate(state.x, state.y);
        ctx.scale(state.scale, state.scale);
        ctx.drawImage(img, 0, 0, img.naturalWidth, img.naturalHeight);

        return canvas.toDataURL('image/png');
    },

    removeBackground: async function (dataUrl) {
        if (!window._imglyRemoveBackground) return dataUrl;

        const res = await fetch(dataUrl);
        const blob = await res.blob();

        const resultBlob = await window._imglyRemoveBackground(blob, {
            output: { format: 'image/png', quality: 1 }
        });

        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(reader.result);
            reader.onerror = reject;
            reader.readAsDataURL(resultBlob);
        });
    },

    destroy: function (containerId) {
        const state = this._instances[containerId];
        if (state) {
            if (state._cleanup) state._cleanup();
            delete this._instances[containerId];
        }
    },

    // ── Crop box (raw image phase) ──────────────────────────────────────

    _cropInstances: {},

    initCropBox: function (containerId) {
        const container = document.getElementById(containerId);
        if (!container) return;
        const img = container.querySelector('img');
        if (!img) return;

        this.destroyCropBox(containerId);

        const setup = () => {
            const containerRect = container.getBoundingClientRect();
            const imgRect = img.getBoundingClientRect();

            // Offsets of the displayed image inside the container
            const offX = imgRect.left - containerRect.left;
            const offY = imgRect.top - containerRect.top;
            const dispW = imgRect.width;
            const dispH = imgRect.height;
            const scale = dispW / img.naturalWidth;

            const MIN = 30;
            const crop = { x: offX, y: offY, w: dispW, h: dispH };

            // Canvas overlay — draws dark surround + crop border + rule-of-thirds
            const canvas = document.createElement('canvas');
            canvas.width = Math.round(containerRect.width);
            canvas.height = Math.round(containerRect.height);
            canvas.style.cssText = 'position:absolute;inset:0;pointer-events:none;z-index:10;';
            const ctx = canvas.getContext('2d');

            const draw = () => {
                const cx = Math.round(crop.x), cy = Math.round(crop.y);
                const cw = Math.round(crop.w), ch = Math.round(crop.h);
                ctx.clearRect(0, 0, canvas.width, canvas.height);
                ctx.fillStyle = 'rgba(0,0,0,0.55)';
                ctx.fillRect(0, 0, canvas.width, canvas.height);
                ctx.clearRect(cx, cy, cw, ch);
                ctx.strokeStyle = 'rgba(255,255,255,0.85)';
                ctx.lineWidth = 1;
                ctx.strokeRect(cx + 0.5, cy + 0.5, cw - 1, ch - 1);
                ctx.strokeStyle = 'rgba(255,255,255,0.22)';
                ctx.lineWidth = 0.5;
                for (let i = 1; i < 3; i++) {
                    ctx.beginPath(); ctx.moveTo(cx + cw * i / 3, cy); ctx.lineTo(cx + cw * i / 3, cy + ch); ctx.stroke();
                    ctx.beginPath(); ctx.moveTo(cx, cy + ch * i / 3); ctx.lineTo(cx + cw, cy + ch * i / 3); ctx.stroke();
                }
                updateHandles();
            };

            // Handles: 4 corner + 4 edge + 1 move zone
            const DIRS = ['nw', 'n', 'ne', 'e', 'se', 's', 'sw', 'w', 'move'];
            const handles = {};
            DIRS.forEach(dir => {
                const h = document.createElement('div');
                h.style.cssText = 'position:absolute;z-index:11;pointer-events:all;box-sizing:border-box;';
                if (dir === 'move') {
                    h.style.cursor = 'move';
                } else {
                    h.style.width = '20px';
                    h.style.height = '20px';
                    h.style.background = 'white';
                    h.style.border = '2px solid rgba(0,0,0,0.4)';
                    h.style.borderRadius = '3px';
                    h.style.cursor = dir + '-resize';
                }
                container.appendChild(h);
                handles[dir] = h;
            });

            const updateHandles = () => {
                const { x, y, w, h } = crop;
                const pos = (l, t, w, ht) => ({ left: l + 'px', top: t + 'px', width: w ? w + 'px' : '', height: ht ? ht + 'px' : '' });
                Object.assign(handles['nw'].style, pos(x - 10, y - 10));
                Object.assign(handles['n'].style,  pos(x + w / 2 - 10, y - 10));
                Object.assign(handles['ne'].style, pos(x + w - 10, y - 10));
                Object.assign(handles['e'].style,  pos(x + w - 10, y + h / 2 - 10));
                Object.assign(handles['se'].style, pos(x + w - 10, y + h - 10));
                Object.assign(handles['s'].style,  pos(x + w / 2 - 10, y + h - 10));
                Object.assign(handles['sw'].style, pos(x - 10, y + h - 10));
                Object.assign(handles['w'].style,  pos(x - 10, y + h / 2 - 10));
                Object.assign(handles['move'].style, {
                    left: (x + 20) + 'px', top: (y + 20) + 'px',
                    width: Math.max(0, w - 40) + 'px', height: Math.max(0, h - 40) + 'px'
                });
            };

            container.style.position = 'relative';
            container.appendChild(canvas);
            draw();

            let dragState = null;

            const clientXY = e => e.touches
                ? [e.touches[0].clientX, e.touches[0].clientY]
                : [e.clientX, e.clientY];

            const startDrag = (dir, e) => {
                e.preventDefault(); e.stopPropagation();
                const [cx, cy] = clientXY(e);
                dragState = { dir, startX: cx, startY: cy, startCrop: { ...crop } };
            };

            const onMove = e => {
                if (!dragState) return;
                const [cx, cy] = clientXY(e);
                const dx = cx - dragState.startX, dy = cy - dragState.startY;
                const sc = dragState.startCrop, dir = dragState.dir;
                let { x, y, w, h } = sc;

                if (dir === 'move') {
                    x = Math.max(offX, Math.min(offX + dispW - w, x + dx));
                    y = Math.max(offY, Math.min(offY + dispH - h, y + dy));
                } else {
                    if (dir.includes('e')) w = Math.max(MIN, Math.min(offX + dispW - x, w + dx));
                    if (dir.includes('s')) h = Math.max(MIN, Math.min(offY + dispH - y, h + dy));
                    if (dir.includes('w')) { const nx = Math.min(x + w - MIN, Math.max(offX, x + dx)); w = x + w - nx; x = nx; }
                    if (dir.includes('n')) { const ny = Math.min(y + h - MIN, Math.max(offY, y + dy)); h = y + h - ny; y = ny; }
                }
                crop.x = x; crop.y = y; crop.w = w; crop.h = h;
                draw();
            };

            const onUp = () => { dragState = null; };

            DIRS.forEach(dir => {
                handles[dir].addEventListener('mousedown', e => startDrag(dir, e));
                handles[dir].addEventListener('touchstart', e => startDrag(dir, e), { passive: false });
            });
            document.addEventListener('mousemove', onMove);
            document.addEventListener('mouseup', onUp);
            document.addEventListener('touchmove', onMove, { passive: false });
            document.addEventListener('touchend', onUp);

            this._cropInstances[containerId] = {
                crop, offX, offY, scale, img,
                cleanup: () => {
                    document.removeEventListener('mousemove', onMove);
                    document.removeEventListener('mouseup', onUp);
                    document.removeEventListener('touchmove', onMove);
                    document.removeEventListener('touchend', onUp);
                    Object.values(handles).forEach(h => h.parentNode && h.parentNode.removeChild(h));
                    canvas.parentNode && canvas.parentNode.removeChild(canvas);
                }
            };
        };

        if (img.complete && img.naturalWidth > 0) setup();
        else img.addEventListener('load', setup, { once: true });
    },

    getCroppedDataUrl: function (containerId) {
        const inst = this._cropInstances[containerId];
        if (!inst) return null;
        const { crop, offX, offY, scale, img } = inst;

        const srcX = (crop.x - offX) / scale;
        const srcY = (crop.y - offY) / scale;
        const srcW = crop.w / scale;
        const srcH = crop.h / scale;

        // Cap output at 800px on the longest side to keep PNG encoding fast
        const MAX = 800;
        const downscale = Math.min(1, MAX / Math.max(srcW, srcH));
        const canvas = document.createElement('canvas');
        canvas.width = Math.round(srcW * downscale);
        canvas.height = Math.round(srcH * downscale);
        const ctx = canvas.getContext('2d');
        ctx.drawImage(img, srcX, srcY, srcW, srcH, 0, 0, canvas.width, canvas.height);
        return canvas.toDataURL('image/png');
    },

    destroyCropBox: function (containerId) {
        const inst = this._cropInstances[containerId];
        if (inst) {
            inst.cleanup();
            delete this._cropInstances[containerId];
        }
    }
};
