(() => {
    const previousApi = window.zcsAvatarControls;
    const pendingRef = window.__zcsAvatarControlsPendingRef || previousApi?.__pendingRef || null;
    let dotnetRef = null;
    const radialStart = 140 * Math.PI / 180;
    const radialEnd = 400 * Math.PI / 180;
    const snapPoints = [0, 0.25, 0.5, 0.75, 1];

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function parseNumber(value, fallback) {
        const parsed = Number.parseFloat(value);
        return Number.isFinite(parsed) ? parsed : fallback;
    }

    function formatValue(value) {
        return Number(value).toFixed(3).replace(/0+$/, '').replace(/\.$/, '');
    }

    function normalizedFromValue(value, min, max) {
        if (Math.abs(max - min) < 0.000001) return 0;
        return clamp((value - min) / (max - min), 0, 1);
    }

    function valueFromNormalized(normalized, min, max) {
        return min + normalized * (max - min);
    }

    function snapNormalized(normalized) {
        for (const snap of snapPoints) {
            if (Math.abs(normalized - snap) <= 0.025) return snap;
        }
        return normalized;
    }

    function drawRadial(element) {
        const canvas = element.querySelector('.zcs-radial-canvas');
        if (!canvas) return;

        const ctx = canvas.getContext('2d');
        const width = canvas.width;
        const height = canvas.height;
        const value = parseNumber(element.dataset.value, 0);
        const min = parseNumber(element.dataset.min, 0);
        const max = parseNumber(element.dataset.max, 1);
        const disabled = element.dataset.disabled === 'true';
        const normalized = normalizedFromValue(value, min, max);
        const centerX = width / 2;
        const centerY = height * 0.66;
        const radius = Math.min(width, height) * 0.39;
        const thickness = 13;
        const currentAngle = radialStart + (radialEnd - radialStart) * normalized;

        ctx.clearRect(0, 0, width, height);
        ctx.lineCap = 'round';
        ctx.lineWidth = thickness;
        ctx.strokeStyle = 'rgba(255,255,255,0.075)';
        ctx.beginPath();
        ctx.arc(centerX, centerY, radius, radialStart, radialEnd, false);
        ctx.stroke();

        ctx.strokeStyle = disabled ? 'rgba(120,120,120,0.65)' : 'rgba(82, 150, 255, 0.95)';
        ctx.shadowColor = disabled ? 'transparent' : 'rgba(82, 150, 255, 0.35)';
        ctx.shadowBlur = disabled ? 0 : 10;
        ctx.beginPath();
        ctx.arc(centerX, centerY, radius, radialStart, currentAngle, false);
        ctx.stroke();
        ctx.shadowBlur = 0;

        for (const snap of snapPoints) {
            const angle = radialStart + (radialEnd - radialStart) * snap;
            const inner = radius - thickness * 0.92;
            const outer = radius + thickness * 0.92;
            ctx.strokeStyle = Math.abs(normalized - snap) <= 0.002 ? 'rgba(255,255,255,0.92)' : 'rgba(255,255,255,0.23)';
            ctx.lineWidth = Math.abs(normalized - snap) <= 0.002 ? 3 : 2;
            ctx.beginPath();
            ctx.moveTo(centerX + Math.cos(angle) * inner, centerY + Math.sin(angle) * inner);
            ctx.lineTo(centerX + Math.cos(angle) * outer, centerY + Math.sin(angle) * outer);
            ctx.stroke();
        }

        const knobX = centerX + Math.cos(currentAngle) * radius;
        const knobY = centerY + Math.sin(currentAngle) * radius;
        ctx.fillStyle = disabled ? 'rgb(90,90,96)' : 'rgb(82, 150, 255)';
        ctx.strokeStyle = 'rgba(0,0,0,0.35)';
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.arc(knobX, knobY, 13, 0, Math.PI * 2);
        ctx.fill();
        ctx.stroke();

        const number = element.querySelector('.zcs-radial-number');
        if (number) number.textContent = formatValue(value);
    }

    function radialNormalizedFromPointer(element, event) {
        const canvas = element.querySelector('.zcs-radial-canvas');
        const rect = canvas.getBoundingClientRect();
        const x = event.clientX - rect.left;
        const y = event.clientY - rect.top;
        const centerX = rect.width / 2;
        const centerY = rect.height * 0.66;
        let angle = Math.atan2(y - centerY, x - centerX);
        if (angle < radialStart) angle += Math.PI * 2;
        return snapNormalized(clamp((angle - radialStart) / (radialEnd - radialStart), 0, 1));
    }

    function setRadialFromPointer(element, event) {
        if (element.dataset.disabled === 'true') return;
        const min = parseNumber(element.dataset.min, 0);
        const max = parseNumber(element.dataset.max, 1);
        const normalized = radialNormalizedFromPointer(element, event);
        const value = valueFromNormalized(normalized, min, max);
        const previous = parseNumber(element.dataset.value, 0);
        element.dataset.value = String(value);
        drawRadial(element);
        if (dotnetRef && Math.abs(previous - value) > 0.0005) {
            dotnetRef.invokeMethodAsync('SetRadialControlValue', element.dataset.address || '', value);
        }
    }

    function bindRadial(element) {
        drawRadial(element);
        if (element.__zcsRadialBound) return;
        element.__zcsRadialBound = true;

        const canvas = element.querySelector('.zcs-radial-canvas');
        if (!canvas) return;

        canvas.addEventListener('pointerdown', event => {
            if (element.dataset.disabled === 'true') return;
            event.preventDefault();
            canvas.setPointerCapture(event.pointerId);
            setRadialFromPointer(element, event);
        });
        canvas.addEventListener('pointermove', event => {
            if (element.dataset.disabled === 'true' || !canvas.hasPointerCapture(event.pointerId)) return;
            event.preventDefault();
            setRadialFromPointer(element, event);
        });
        canvas.addEventListener('pointerup', event => {
            if (canvas.hasPointerCapture(event.pointerId)) canvas.releasePointerCapture(event.pointerId);
        });
        canvas.addEventListener('pointercancel', event => {
            if (canvas.hasPointerCapture(event.pointerId)) canvas.releasePointerCapture(event.pointerId);
        });
    }

    function hsvToRgb(h, s, v) {
        h = ((h % 1) + 1) % 1;
        s = clamp(s, 0, 1);
        v = clamp(v, 0, 1);
        const i = Math.floor(h * 6);
        const f = h * 6 - i;
        const p = v * (1 - s);
        const q = v * (1 - f * s);
        const t = v * (1 - (1 - f) * s);
        let r, g, b;
        switch (i % 6) {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }
        return [Math.round(r * 255), Math.round(g * 255), Math.round(b * 255)];
    }

    function rgbToHex(rgb) {
        return '#' + rgb.map(component => component.toString(16).padStart(2, '0')).join('');
    }

    function drawHsv(element) {
        const square = element.querySelector('.zcs-hsv-square');
        const hueCanvas = element.querySelector('.zcs-hsv-hue');
        if (!square || !hueCanvas) return;

        const hue = parseNumber(element.dataset.hue, 0);
        const saturation = parseNumber(element.dataset.saturation, 1);
        const value = parseNumber(element.dataset.value, 1);
        const disabled = element.dataset.disabled === 'true';
        const squareCtx = square.getContext('2d');
        const hueCtx = hueCanvas.getContext('2d');
        const base = hsvToRgb(hue, 1, 1);

        squareCtx.clearRect(0, 0, square.width, square.height);
        squareCtx.fillStyle = `rgb(${base[0]}, ${base[1]}, ${base[2]})`;
        squareCtx.fillRect(0, 0, square.width, square.height);

        const whiteGradient = squareCtx.createLinearGradient(0, 0, square.width, 0);
        whiteGradient.addColorStop(0, 'rgba(255,255,255,1)');
        whiteGradient.addColorStop(1, 'rgba(255,255,255,0)');
        squareCtx.fillStyle = whiteGradient;
        squareCtx.fillRect(0, 0, square.width, square.height);

        const blackGradient = squareCtx.createLinearGradient(0, 0, 0, square.height);
        blackGradient.addColorStop(0, 'rgba(0,0,0,0)');
        blackGradient.addColorStop(1, 'rgba(0,0,0,1)');
        squareCtx.fillStyle = blackGradient;
        squareCtx.fillRect(0, 0, square.width, square.height);

        const markerX = saturation * square.width;
        const markerY = (1 - value) * square.height;
        squareCtx.strokeStyle = 'rgba(0,0,0,0.85)';
        squareCtx.lineWidth = 4;
        squareCtx.beginPath();
        squareCtx.arc(markerX, markerY, 8, 0, Math.PI * 2);
        squareCtx.stroke();
        squareCtx.strokeStyle = 'rgba(255,255,255,0.92)';
        squareCtx.lineWidth = 2;
        squareCtx.beginPath();
        squareCtx.arc(markerX, markerY, 8, 0, Math.PI * 2);
        squareCtx.stroke();

        const gradient = hueCtx.createLinearGradient(0, 0, hueCanvas.width, 0);
        for (let i = 0; i <= 6; i++) {
            const rgb = hsvToRgb(i / 6, 1, 1);
            gradient.addColorStop(i / 6, `rgb(${rgb[0]}, ${rgb[1]}, ${rgb[2]})`);
        }
        hueCtx.clearRect(0, 0, hueCanvas.width, hueCanvas.height);
        hueCtx.fillStyle = gradient;
        hueCtx.fillRect(0, 0, hueCanvas.width, hueCanvas.height);
        hueCtx.strokeStyle = 'rgba(0,0,0,0.65)';
        hueCtx.strokeRect(0, 0, hueCanvas.width, hueCanvas.height);
        const hueX = hue * hueCanvas.width;
        hueCtx.fillStyle = 'rgba(255,255,255,0.95)';
        hueCtx.strokeStyle = 'rgba(0,0,0,0.7)';
        hueCtx.lineWidth = 2;
        hueCtx.beginPath();
        if (typeof hueCtx.roundRect === 'function') {
            hueCtx.roundRect(hueX - 4, 1, 8, hueCanvas.height - 2, 5);
        } else {
            hueCtx.rect(hueX - 4, 1, 8, hueCanvas.height - 2);
        }
        hueCtx.fill();
        hueCtx.stroke();

        const rgb = hsvToRgb(hue, saturation, value);
        const hex = rgbToHex(rgb);
        const preview = element.querySelector('.zcs-hsv-preview');
        if (preview) preview.style.backgroundColor = hex;
        const values = element.querySelectorAll('.zcs-hsv-values span');
        if (values.length >= 4) {
            values[0].textContent = `H ${formatValue(hue)}`;
            values[1].textContent = `S ${formatValue(saturation)}`;
            values[2].textContent = `V ${formatValue(value)}`;
            values[3].textContent = hex;
        }

        element.classList.toggle('zcs-control-disabled', disabled);
    }

    function updateHsv(element, notify) {
        drawHsv(element);
        if (notify && dotnetRef) {
            dotnetRef.invokeMethodAsync(
                'SetHSVControlValue',
                element.dataset.hueAddress || '',
                parseNumber(element.dataset.hue, 0),
                parseNumber(element.dataset.saturation, 1),
                parseNumber(element.dataset.value, 1)
            );
        }
    }

    function setSquareFromPointer(element, event) {
        if (element.dataset.disabled === 'true') return;
        const square = element.querySelector('.zcs-hsv-square');
        const rect = square.getBoundingClientRect();
        element.dataset.saturation = String(clamp((event.clientX - rect.left) / rect.width, 0, 1));
        element.dataset.value = String(clamp(1 - (event.clientY - rect.top) / rect.height, 0, 1));
        updateHsv(element, true);
    }

    function setHueFromPointer(element, event) {
        if (element.dataset.disabled === 'true') return;
        const hueCanvas = element.querySelector('.zcs-hsv-hue');
        const rect = hueCanvas.getBoundingClientRect();
        element.dataset.hue = String(clamp((event.clientX - rect.left) / rect.width, 0, 1));
        updateHsv(element, true);
    }

    function bindHsv(element) {
        drawHsv(element);
        if (element.__zcsHsvBound) return;
        element.__zcsHsvBound = true;

        const square = element.querySelector('.zcs-hsv-square');
        const hueCanvas = element.querySelector('.zcs-hsv-hue');
        if (!square || !hueCanvas) return;

        square.addEventListener('pointerdown', event => {
            if (element.dataset.disabled === 'true') return;
            event.preventDefault();
            square.setPointerCapture(event.pointerId);
            setSquareFromPointer(element, event);
        });
        square.addEventListener('pointermove', event => {
            if (element.dataset.disabled === 'true' || !square.hasPointerCapture(event.pointerId)) return;
            event.preventDefault();
            setSquareFromPointer(element, event);
        });
        square.addEventListener('pointerup', event => {
            if (square.hasPointerCapture(event.pointerId)) square.releasePointerCapture(event.pointerId);
        });
        square.addEventListener('pointercancel', event => {
            if (square.hasPointerCapture(event.pointerId)) square.releasePointerCapture(event.pointerId);
        });

        hueCanvas.addEventListener('pointerdown', event => {
            if (element.dataset.disabled === 'true') return;
            event.preventDefault();
            hueCanvas.setPointerCapture(event.pointerId);
            setHueFromPointer(element, event);
        });
        hueCanvas.addEventListener('pointermove', event => {
            if (element.dataset.disabled === 'true' || !hueCanvas.hasPointerCapture(event.pointerId)) return;
            event.preventDefault();
            setHueFromPointer(element, event);
        });
        hueCanvas.addEventListener('pointerup', event => {
            if (hueCanvas.hasPointerCapture(event.pointerId)) hueCanvas.releasePointerCapture(event.pointerId);
        });
        hueCanvas.addEventListener('pointercancel', event => {
            if (hueCanvas.hasPointerCapture(event.pointerId)) hueCanvas.releasePointerCapture(event.pointerId);
        });
    }

    function bind(ref) {
        dotnetRef = ref;
        document.querySelectorAll('[data-zcs-radial="true"]').forEach(bindRadial);
        document.querySelectorAll('[data-zcs-hsv="true"]').forEach(bindHsv);
    }

    function safeBind(ref) {
        try {
            bind(ref);
        } catch (error) {
            console.error('zcsAvatarControls.bind failed', error);
        }
    }

    const api = { bind: safeBind };
    window.zcsAvatarControls = api;

    if (pendingRef) {
        window.__zcsAvatarControlsPendingRef = null;
        setTimeout(() => api.bind(pendingRef), 0);
    }
})();
