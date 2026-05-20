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
            devicePixelRatio: 1,
            heldKeys: new Map(),
            pendingKeys: [],
            inFrame: false,
            immediateFrameRequested: false,
            listeners: []
        };
        this.pumps[id] = state;
        setupKeyboard(hostElement, state);
        updateCellMetrics(hostElement, state);

        const resize = () => updateCellMetrics(hostElement, state);
        window.addEventListener("resize", resize);
        window.visualViewport?.addEventListener("resize", resize);
        state.listeners.push(
            [window, "resize", resize],
            [window.visualViewport, "resize", resize]);

        const frame = async (timestamp) => {
            if (state.stopped) return;
            await runFrame(dotNetRef, hostElement, canvas, context, state, timestamp);
            requestAnimationFrame(frame);
        };

        state.requestImmediateFrame = () => {
            if (state.immediateFrameRequested || state.inFrame || state.stopped) return;
            state.immediateFrameRequested = true;
            setTimeout(async () => {
                state.immediateFrameRequested = false;
                await runFrame(dotNetRef, hostElement, canvas, context, state, performance.now());
            }, 0);
        };

        requestAnimationFrame(frame);
        return id;
    },
    stop(id) {
        const pump = this.pumps[id];
        if (pump) {
            pump.stopped = true;
            teardownKeyboard(pump);
        }
        delete this.pumps[id];
    }
};

async function runFrame(dotNetRef, hostElement, canvas, context, state, timestamp) {
    if (state.stopped || state.inFrame) return;
    state.inFrame = true;

    const elapsed = Math.max(1, timestamp - state.lastTimestamp);
    state.lastTimestamp = timestamp;

    try {
        const size = measure(hostElement, state);
        pumpKeyboardRepeats(state, timestamp);
        const keys = state.pendingKeys.splice(0, state.pendingKeys.length);
        const terminalFrame = await dotNetRef.invokeMethodAsync("Tick", size.width, size.height, elapsed, keys);
        drawFrame(canvas, context, terminalFrame, state);
    } catch (error) {
        console.error("klooie frame pump tick failed", error);
    } finally {
        state.inFrame = false;
        if (state.pendingKeys.length > 0) {
            state.requestImmediateFrame?.();
        }
    }
}

function setupKeyboard(hostElement, state) {
    if (!hostElement.hasAttribute("tabindex")) {
        hostElement.tabIndex = 0;
    }

    const startRepeat = (event) => {
        const keyId = keyboardEventId(event);
        if (state.heldKeys.has(keyId)) return;

        const payload = keyboardEventToPayload(event);
        state.pendingKeys.push(payload);
        state.requestImmediateFrame?.();
        state.heldKeys.set(keyId, {
            payload,
            nextRepeatAt: performance.now() + 400
        });
    };

    const clearRepeat = (event) => clearHeldKey(state, keyboardEventId(event));
    const clearAllRepeats = () => {
        for (const keyId of state.heldKeys.keys()) {
            clearHeldKey(state, keyId);
        }
    };

    const keydown = (event) => {
        if (event.isComposing || event.key === "Process") return;
        event.preventDefault();
        event.stopPropagation();

        if (isModifierOnlyKey(event.key)) return;
        if (event.repeat) return;
        startRepeat(event);
    };

    const keyup = (event) => {
        clearRepeat(event);
        event.preventDefault();
        event.stopPropagation();
    };

    const pointerdown = () => hostElement.focus({ preventScroll: true });

    hostElement.addEventListener("keydown", keydown);
    hostElement.addEventListener("keyup", keyup);
    hostElement.addEventListener("pointerdown", pointerdown);
    window.addEventListener("blur", clearAllRepeats);

    state.listeners.push(
        [hostElement, "keydown", keydown],
        [hostElement, "keyup", keyup],
        [hostElement, "pointerdown", pointerdown],
        [window, "blur", clearAllRepeats]);

    window.setTimeout(() => {
        if (!state.stopped) hostElement.focus({ preventScroll: true });
    }, 0);
}

function teardownKeyboard(state) {
    for (const [target, eventName, listener] of state.listeners) {
        target?.removeEventListener(eventName, listener);
    }

    state.listeners = [];
    for (const keyId of state.heldKeys.keys()) {
        clearHeldKey(state, keyId);
    }
}

function clearHeldKey(state, keyId) {
    state.heldKeys.delete(keyId);
}

function pumpKeyboardRepeats(state, timestamp) {
    for (const held of state.heldKeys.values()) {
        let repeats = 0;
        while (timestamp >= held.nextRepeatAt && repeats < 4) {
            state.pendingKeys.push(held.payload);
            held.nextRepeatAt += 33;
            repeats++;
        }

        if (repeats === 4 && timestamp >= held.nextRepeatAt) {
            held.nextRepeatAt = timestamp + 33;
        }
    }
}

function keyboardEventId(event) {
    return `${event.code || event.key}:${event.location || 0}`;
}

function keyboardEventToPayload(event) {
    return {
        key: event.key || "",
        code: event.code || "",
        location: event.location || 0,
        altKey: event.altKey,
        shiftKey: event.shiftKey,
        ctrlKey: event.ctrlKey,
        metaKey: event.metaKey,
        capsLock: event.getModifierState?.("CapsLock") || false
    };
}

function isModifierOnlyKey(key) {
    return key === "Alt" ||
        key === "AltGraph" ||
        key === "CapsLock" ||
        key === "Control" ||
        key === "Fn" ||
        key === "FnLock" ||
        key === "Hyper" ||
        key === "Meta" ||
        key === "NumLock" ||
        key === "ScrollLock" ||
        key === "Shift" ||
        key === "Super" ||
        key === "Symbol" ||
        key === "SymbolLock";
}

function measure(hostElement, state) {
    const viewport = window.visualViewport;
    const viewportWidth = viewport?.width || window.innerWidth || document.documentElement.clientWidth;
    const viewportHeight = viewport?.height || window.innerHeight || document.documentElement.clientHeight;
    state.devicePixelRatio = window.devicePixelRatio || 1;

    return {
        width: Math.max(1, Math.floor(viewportWidth / state.cellWidth)),
        height: Math.max(1, Math.floor(viewportHeight / state.cellHeight))
    };
}

function updateCellMetrics(hostElement, state) {
    const rect = measureCell(hostElement);
    state.cellWidth = Math.max(1, rect?.width || 8);
    state.cellHeight = Math.max(1, rect?.height || 16);
    state.devicePixelRatio = window.devicePixelRatio || 1;
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
    if (frame.full) {
        context.fillStyle = "#000";
        context.fillRect(0, 0, cssWidth, cssHeight);
    }

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
