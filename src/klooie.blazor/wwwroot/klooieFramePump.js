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
            listeners: []
        };
        this.pumps[id] = state;
        setupKeyboard(dotNetRef, hostElement, state);

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
        if (pump) {
            pump.stopped = true;
            teardownKeyboard(pump);
        }
        delete this.pumps[id];
    }
};

function setupKeyboard(dotNetRef, hostElement, state) {
    if (!hostElement.hasAttribute("tabindex")) {
        hostElement.tabIndex = 0;
    }

    const send = (event) => {
        const payload = keyboardEventToPayload(event);
        dotNetRef.invokeMethodAsync("SendKey", payload)
            .catch(error => console.error("klooie key dispatch failed", error));
    };

    const startRepeat = (event) => {
        const keyId = keyboardEventId(event);
        if (state.heldKeys.has(keyId)) return;

        const payload = keyboardEventToPayload(event);
        const held = {
            delayId: 0,
            intervalId: 0,
            payload
        };

        held.delayId = window.setTimeout(() => {
            if (!state.heldKeys.has(keyId)) return;

            held.intervalId = window.setInterval(() => {
                dotNetRef.invokeMethodAsync("SendKey", held.payload)
                    .catch(error => console.error("klooie key repeat dispatch failed", error));
            }, 33);
        }, 400);

        state.heldKeys.set(keyId, held);
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
        send(event);
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
        target.removeEventListener(eventName, listener);
    }

    state.listeners = [];
    for (const keyId of state.heldKeys.keys()) {
        clearHeldKey(state, keyId);
    }
}

function clearHeldKey(state, keyId) {
    const held = state.heldKeys.get(keyId);
    if (!held) return;

    window.clearTimeout(held.delayId);
    window.clearInterval(held.intervalId);
    state.heldKeys.delete(keyId);
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
