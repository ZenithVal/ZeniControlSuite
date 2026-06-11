const graphs = new WeakMap();

export function start(canvas) {
    if (!canvas) return;

    stop(canvas);

    const state = {
        canvas,
        ctx: canvas.getContext('2d'),
        latest: 0,
        samples: [],
        lastSampleTime: 0,
        raf: 0
    };

    graphs.set(canvas, state);
    resize(state);
    state.raf = requestAnimationFrame((time) => draw(state, time));
}

export function setValue(canvas, value) {
    const state = graphs.get(canvas);
    if (!state) return;
    const number = Number(value);
    state.latest = Number.isFinite(number) ? Math.max(0, Math.min(1, number)) : 0;
}

export function stop(canvas) {
    const state = graphs.get(canvas);
    if (!state) return;
    cancelAnimationFrame(state.raf);
    graphs.delete(canvas);
}

function resize(state) {
    const rect = state.canvas.getBoundingClientRect();
    const ratio = window.devicePixelRatio || 1;
    const width = Math.max(1, Math.floor(rect.width * ratio));
    const height = Math.max(1, Math.floor(rect.height * ratio));

    if (state.canvas.width !== width || state.canvas.height !== height) {
        state.canvas.width = width;
        state.canvas.height = height;
    }
}

function draw(state, time) {
    resize(state);

    if (time - state.lastSampleTime >= 33) {
        state.samples.push(state.latest);
        const maxSamples = Math.max(32, Math.floor(state.canvas.width / 3));
        if (state.samples.length > maxSamples) {
            state.samples.splice(0, state.samples.length - maxSamples);
        }
        state.lastSampleTime = time;
    }

    const ctx = state.ctx;
    const width = state.canvas.width;
    const height = state.canvas.height;

    ctx.clearRect(0, 0, width, height);
    drawGrid(ctx, width, height);
    drawTrace(ctx, state.samples, width, height);

    state.raf = requestAnimationFrame((nextTime) => draw(state, nextTime));
}

function drawGrid(ctx, width, height) {
    ctx.save();
    ctx.lineWidth = 1;

    ctx.strokeStyle = 'rgba(255,255,255,0.11)';
    for (let i = 1; i < 4; i++) {
        const y = height * i / 4;
        ctx.beginPath();
        ctx.moveTo(0, y);
        ctx.lineTo(width, y);
        ctx.stroke();
    }

    ctx.strokeStyle = 'rgba(255,255,255,0.07)';
    for (let i = 1; i < 6; i++) {
        const x = width * i / 6;
        ctx.beginPath();
        ctx.moveTo(x, 0);
        ctx.lineTo(x, height);
        ctx.stroke();
    }

    ctx.restore();
}

function drawTrace(ctx, samples, width, height) {
    if (samples.length < 2) return;

    const step = width / Math.max(1, samples.length - 1);

    ctx.save();
    ctx.lineJoin = 'round';
    ctx.lineCap = 'round';

    ctx.beginPath();
    samples.forEach((sample, index) => {
        const x = index * step;
        const y = height - sample * height;
        if (index === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
    });
    ctx.strokeStyle = 'rgba(90,255,170,0.18)';
    ctx.lineWidth = 8;
    ctx.stroke();

    ctx.beginPath();
    samples.forEach((sample, index) => {
        const x = index * step;
        const y = height - sample * height;
        if (index === 0) ctx.moveTo(x, y);
        else ctx.lineTo(x, y);
    });
    ctx.strokeStyle = 'rgba(90,255,170,0.95)';
    ctx.lineWidth = 3;
    ctx.stroke();

    ctx.restore();
}
