window.klooieFramePump = {
    nextId: 1,
    pumps: {},
    start(dotNetRef, hostElement) {
        const id = this.nextId++;
        const canvas = hostElement.querySelector("canvas");
        const context = canvas.getContext("2d", { alpha: false });
        const state = {
            stopped: false,
            lastTimestamp: performance.now(),
            cellWidth: 8,
            cellHeight: 16,
            devicePixelRatio: 1
        };
        this.pumps[id] = state;

        const frame = async (timestamp) => {
            if (state.stopped) return;

            const elapsed = Math.max(1, timestamp - state.lastTimestamp);
            state.lastTimestamp = timestamp;

            try {
                const size = measure(hostElement, state);
                const terminalFrame = await dotNetRef.invokeMethodAsync("Tick", size.width, size.height, elapsed);
                drawFrame(canvas, context, terminalFrame, state);
            } catch (error) {
                console.error("klooie frame pump tick failed", error);
            }

            requestAnimationFrame(frame);
        };

        requestAnimationFrame(frame);
        return id;
    },
    stop(id) {
        const pump = this.pumps[id];
        if (pump) pump.stopped = true;
        delete this.pumps[id];
    }
};

function measure(hostElement, state) {
    const rect = measureCell(hostElement);
    const viewport = window.visualViewport;
    const viewportWidth = viewport?.width || window.innerWidth || document.documentElement.clientWidth;
    const viewportHeight = viewport?.height || window.innerHeight || document.documentElement.clientHeight;
    const cellWidth = Math.max(1, rect?.width || 8);
    const cellHeight = Math.max(1, rect?.height || 16);
    state.cellWidth = cellWidth;
    state.cellHeight = cellHeight;
    state.devicePixelRatio = window.devicePixelRatio || 1;

    return {
        width: Math.max(1, Math.floor(viewportWidth / cellWidth)),
        height: Math.max(1, Math.floor(viewportHeight / cellHeight))
    };
}

function measureCell(hostElement) {
    let probe = hostElement.querySelector(".browser-console-measure");
    if (!probe) {
        probe = document.createElement("span");
        probe.className = "browser-console-measure";
        probe.textContent = "M";
        hostElement.appendChild(probe);
    }

    return probe.getBoundingClientRect();
}

function drawFrame(canvas, context, frame, state) {
    const cssWidth = frame.width * state.cellWidth;
    const cssHeight = frame.height * state.cellHeight;
    const pixelWidth = Math.max(1, Math.ceil(cssWidth * state.devicePixelRatio));
    const pixelHeight = Math.max(1, Math.ceil(cssHeight * state.devicePixelRatio));

    if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
        canvas.width = pixelWidth;
        canvas.height = pixelHeight;
    }

    context.setTransform(state.devicePixelRatio, 0, 0, state.devicePixelRatio, 0, 0);
    context.textBaseline = "top";
    context.font = "16px Consolas, 'Cascadia Mono', 'Courier New', monospace";
    context.fillStyle = "#000";
    context.fillRect(0, 0, cssWidth, cssHeight);

    const x = frame.x;
    const y = frame.y;
    const text = frame.text;
    const foreground = frame.foreground;
    const background = frame.background;

    for (let i = 0; i < text.length; i++) {
        const left = x[i] * state.cellWidth;
        const top = y[i] * state.cellHeight;
        const runText = text[i];
        context.fillStyle = background[i];
        context.fillRect(left, top, runText.length * state.cellWidth, state.cellHeight);
        context.fillStyle = foreground[i];
        context.fillText(runText, left, top);
    }
}
