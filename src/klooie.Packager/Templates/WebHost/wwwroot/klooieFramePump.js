window.klooiePwa = window.klooiePwa || {
    deferredInstallPrompt: undefined,
    installPromptEnabled: false,
    installed: window.matchMedia?.("(display-mode: fullscreen)")?.matches || window.matchMedia?.("(display-mode: standalone)")?.matches || navigator.standalone === true
};

window.addEventListener("beforeinstallprompt", event => {
    if (!window.klooiePwa.installPromptEnabled) return;

    event.preventDefault();
    window.klooiePwa.deferredInstallPrompt = event;
    window.dispatchEvent(new Event("klooie-pwa-install-available"));
});

window.addEventListener("appinstalled", () => {
    window.klooiePwa.installed = true;
    window.klooiePwa.deferredInstallPrompt = undefined;
    window.dispatchEvent(new Event("klooie-pwa-installed"));
});

window.klooieFramePump = {
    nextId: 1,
    pumps: {},
    start(dotNetRef, hostElement, mobileOptions) {
        const id = this.nextId++;
        const canvas = hostElement.querySelector("canvas");
        const normalizedMobileOptions = normalizeMobileOptions(mobileOptions);
        const state = {
            stopped: false,
            lastTimestamp: performance.now(),
            cellWidth: 8,
            cellHeight: 16,
            devicePixelRatio: 1,
            font: "16px Consolas, 'Cascadia Mono', 'Courier New', monospace",
            heldKeys: new Map(),
            pendingKeys: [],
            inFrame: false,
            immediateFrameRequested: false,
            sizeDirty: true,
            frameTimer: undefined,
            frameAnimationId: undefined,
            renderer: undefined,
            knownGamepads: new Map(),
            touchController: undefined,
            zoomControl: undefined,
            mobileOptions: normalizedMobileOptions,
            zoomLevels: buildZoomLevels(normalizedMobileOptions),
            zoom: 1,
            baseCellWidth: 8,
            baseCellHeight: 16,
            baseFont: "16px Consolas, 'Cascadia Mono', 'Courier New', monospace",
            listeners: []
        };
        state.zoom = getInitialZoom(state);
        this.pumps[id] = state;
        window.klooiePwa.installPromptEnabled = window.klooiePwa.installPromptEnabled || (state.mobileOptions.requireHorizontal && shouldShowTouchController());
        setupKeyboard(hostElement, state);
        setupGamepads(state);
        setupTouchController(hostElement, state);
        setupZoomControl(hostElement, state);
        updateCellMetrics(hostElement, state);
        state.renderer = createConsoleRenderer(canvas, state);

        const resize = () => {
            updateCellMetrics(hostElement, state);
            state.sizeDirty = true;
            state.renderer?.invalidateMetrics();
            state.requestImmediateFrame?.();
        };
        window.addEventListener("resize", resize);
        window.visualViewport?.addEventListener("resize", resize);
        state.listeners.push(
            [window, "resize", resize],
            [window.visualViewport, "resize", resize]);

        state.requestImmediateFrame = () => {
            if (state.immediateFrameRequested || state.inFrame || state.stopped) return;
            state.immediateFrameRequested = true;
            requestAnimationFrame(async (timestamp) => {
                state.immediateFrameRequested = false;
                await runFrame(dotNetRef, hostElement, canvas, state, timestamp);
            });
        };

        const pumpFrame = async (timestamp) => {
            await runFrame(dotNetRef, hostElement, canvas, state, timestamp);
            if (!state.stopped) {
                state.frameAnimationId = requestAnimationFrame(pumpFrame);
            }
        };
        state.frameAnimationId = requestAnimationFrame(pumpFrame);
        return id;
    },
    stop(id) {
        const pump = this.pumps[id];
        if (pump) {
            pump.stopped = true;
            if (pump.frameAnimationId !== undefined) {
                cancelAnimationFrame(pump.frameAnimationId);
            }
            pump.renderer?.dispose();
            teardownKeyboard(pump);
            teardownGamepads(pump);
            teardownTouchController(pump);
            teardownZoomControl(pump);
        }
        delete this.pumps[id];
    }
};

window.clawsBigAscii = {
    rasterize(text) {
        text = text || "";
        const font = "bold 20px 'Cascadia Mono', Consolas, 'Courier New', monospace";
        const graphemes = getTextGraphemes(text);
        const measureCanvas = document.createElement("canvas");
        const measureContext = measureCanvas.getContext("2d", { willReadFrequently: true });
        measureContext.font = font;
        measureContext.textBaseline = "alphabetic";

        const fullMetrics = measureContext.measureText(text || " ");
        const ascent = Math.ceil(fullMetrics.actualBoundingBoxAscent || 20);
        const descent = Math.ceil(fullMetrics.actualBoundingBoxDescent || 5);
        const measuredHeight = Math.max(1, ascent + descent);
        let measuredWidth = 0;
        for (const grapheme of graphemes) {
            measuredWidth += Math.round(measureContext.measureText(grapheme).width);
        }

        const width = Math.max(1, measuredWidth + 2);
        const height = Math.max(1, measuredHeight + 4);
        measureCanvas.width = width;
        measureCanvas.height = height;
        measureContext.font = font;
        measureContext.textBaseline = "alphabetic";
        measureContext.fillStyle = "white";
        measureContext.fillRect(0, 0, width, height);
        measureContext.fillStyle = "black";
        measureContext.imageSmoothingEnabled = true;
        measureContext.imageSmoothingQuality = "high";

        let penX = 0;
        for (const grapheme of graphemes) {
            measureContext.fillText(grapheme, penX, ascent);
            penX += Math.round(measureContext.measureText(grapheme).width);
        }

        const rgba = measureContext.getImageData(0, 0, width, height).data;
        let binary = "";
        const chunkSize = 0x8000;
        for (let i = 0; i < rgba.length; i += 4) {
            const gray = Math.round((rgba[i] + rgba[i + 1] + rgba[i + 2]) / 3);
            binary += String.fromCharCode(gray);
            if (binary.length >= chunkSize) {
                const remaining = rgba.length - i - 4;
                if (remaining <= 0) break;
            }
        }

        return `${width},${height},${btoa(binary)}`;
    }
};

function getTextGraphemes(text) {
    if (!text) return [];
    if (typeof Intl !== "undefined" && Intl.Segmenter) {
        const segmenter = new Intl.Segmenter(undefined, { granularity: "grapheme" });
        return Array.from(segmenter.segment(text), s => s.segment);
    }
    return Array.from(text);
}

window.klooieAssets = {
    audioCache: new Map(),
    decodedAudioCache: new Map(),
    active: new Map(),
    music: new Set(),
    audioContext: undefined,
    paused: false,

    play(id, url, volume, pan, loop, isMusic, startPaused, dotNetRef) {
        const playbackId = String(id);
        this.stop(playbackId);
        if (isMusic) {
            for (const musicId of Array.from(this.music)) this.stop(musicId);
        }

        this.getDecodedAudio(url)
            .then(buffer => {
                const context = this.getAudioContext();
                const source = context.createBufferSource();
                const gain = context.createGain();
                const panner = typeof context.createStereoPanner === "function" ? context.createStereoPanner() : undefined;
                const state = {
                    id: playbackId,
                    trackId: url,
                    context,
                    source,
                    gain,
                    panner,
                    volume: normalizeUnit(volume, 1),
                    pan: normalizePan(pan),
                    loop: !!loop,
                    isMusic: !!isMusic,
                    dotNetRef,
                    paused: !!startPaused || this.paused,
                    startedAt: 0,
                    offset: 0,
                    positionTimer: undefined,
                    stopping: false,
                    buffer
                };

                source.buffer = buffer;
                source.loop = state.loop;
                gain.gain.value = state.paused ? 0 : state.volume;
                if (panner) {
                    panner.pan.value = state.pan;
                    source.connect(panner);
                    panner.connect(gain);
                } else {
                    source.connect(gain);
                }

                gain.connect(context.destination);
                source.onended = () => {
                    if (!state.stopping && !state.loop) {
                        this.active.delete(playbackId);
                        this.music.delete(playbackId);
                        this.clearPositionTimer(state);
                        state.dotNetRef?.invokeMethodAsync("AudioEnded", Number(playbackId))
                            ?.catch(error => console.debug("klooie audio ended callback failed", error));
                    }
                };

                this.active.set(playbackId, state);
                if (state.isMusic) this.music.add(playbackId);
                this.startState(state);
            })
            .catch(error => console.debug("klooie audio play failed", error));
    },

    startState(state) {
        try {
            if (state.context.state === "suspended" && !state.paused) {
                state.context.resume().catch(error => console.debug("klooie audio resume skipped", error));
            }

            state.startedAt = state.context.currentTime - state.offset;
            state.source.start(0, state.offset);
            this.startPositionTimer(state);
        } catch (error) {
            console.debug("klooie audio play skipped", error);
        }
    },

    startPositionTimer(state) {
        this.clearPositionTimer(state);
        if (!state.isMusic || !state.dotNetRef) return;

        const publishPosition = () => {
            if (state.stopping || !this.active.has(state.id)) return;

            const duration = state.buffer?.duration || 0;
            const elapsed = Math.max(0, state.context.currentTime - state.startedAt);
            const position = state.loop && duration > 0 ? elapsed % duration : Math.min(elapsed, duration || elapsed);
            state.dotNetRef.invokeMethodAsync("AudioPositionChanged", Number(state.id), state.trackId || "", position, true)
                .catch(error => console.debug("klooie audio position callback failed", error));
        };

        publishPosition();
        state.positionTimer = window.setInterval(publishPosition, 100);
    },

    clearPositionTimer(state) {
        if (state?.positionTimer === undefined) return;

        window.clearInterval(state.positionTimer);
        state.positionTimer = undefined;
    },

    getAudioContext() {
        if (!this.audioContext) {
            const AudioContextType = window.AudioContext || window.webkitAudioContext;
            this.audioContext = new AudioContextType();
        }

        return this.audioContext;
    },

    getAudioUrl(url) {
        const existing = this.audioCache.get(url);
        if (existing) return existing;

        const load = fetch(url, { cache: "force-cache" })
            .then(response => {
                if (!response.ok) throw new Error(`Audio request failed: ${response.status} ${url}`);
                return response.blob();
            })
            .then(blob => URL.createObjectURL(blob));
        this.audioCache.set(url, load);
        return load;
    },

    getDecodedAudio(url) {
        const existing = this.decodedAudioCache.get(url);
        if (existing) return existing;

        const load = fetch(url, { cache: "force-cache" })
            .then(response => {
                if (!response.ok) throw new Error(`Audio request failed: ${response.status} ${url}`);
                return response.arrayBuffer();
            })
            .then(bytes => this.getAudioContext().decodeAudioData(bytes));
        this.decodedAudioCache.set(url, load);
        return load;
    },

    preload(url) {
        this.getDecodedAudio(url).catch(error => console.debug("klooie audio preload failed", error));
    },

    update(id, volume, pan) {
        const state = this.active.get(String(id));
        if (!state) return;

        state.volume = normalizeUnit(volume, 1);
        state.pan = normalizePan(pan);
        state.gain.gain.value = state.paused || this.paused ? 0 : state.volume;
        if (state.panner) state.panner.pan.value = state.pan;
    },

    stop(id) {
        const state = this.active.get(String(id));
        if (!state) return;

        state.stopping = true;
        try {
            state.source.stop();
        } catch {
        }

        try {
            this.clearPositionTimer(state);
            state.source.disconnect();
            state.panner?.disconnect();
            state.gain.disconnect();
        } catch {
        }

        this.active.delete(String(id));
        this.music.delete(String(id));
    },

    pauseAll() {
        this.paused = true;
        for (const state of this.active.values()) {
            state.paused = true;
            state.gain.gain.value = 0;
        }
    },

    resumeAll() {
        this.paused = false;
        if (this.audioContext?.state === "suspended") {
            this.audioContext.resume().catch(error => console.debug("klooie audio resume skipped", error));
        }

        for (const state of this.active.values()) {
            state.paused = false;
            state.gain.gain.value = state.volume;
        }
    },

    clearAudioCache() {
        for (const urlPromise of this.audioCache.values()) {
            urlPromise.then(url => URL.revokeObjectURL(url)).catch(() => { });
        }

        this.audioCache.clear();
        this.decodedAudioCache.clear();
    }
};

function normalizeUnit(value, fallback) {
    const numeric = Number(value);
    return Math.max(0, Math.min(1, Number.isFinite(numeric) ? numeric : fallback));
}

function normalizePan(value) {
    const numeric = Number(value);
    return Math.max(-1, Math.min(1, Number.isFinite(numeric) ? numeric : 0));
}

async function runFrame(dotNetRef, hostElement, canvas, state, timestamp) {
    if (state.stopped || state.inFrame) return;
    state.inFrame = true;

    const elapsed = Math.max(1, timestamp - state.lastTimestamp);
    state.lastTimestamp = timestamp;

    try {
        const size = measure(hostElement, state);
        pumpKeyboardRepeats(state, timestamp);
        const keys = state.pendingKeys.splice(0, state.pendingKeys.length);
        const gamepadSnapshotJson = readGamepadSnapshotJson(state);
        const mobileExperience = shouldShowTouchController();
        const terminalFrame = await dotNetRef.invokeMethodAsync("Tick", size.width, size.height, elapsed, keys, gamepadSnapshotJson, mobileExperience);
        applyBrowserControllerCommands(state, terminalFrame);
        state.renderer.render(canvas, terminalFrame, state);
        state.sizeDirty = false;
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

function setupGamepads(state) {
    if (typeof window === "undefined") return;

    const connected = (event) => {
        const gamepad = event?.gamepad;
        if (gamepad) state.knownGamepads.set(gamepad.index || 0, gamepad);
        state.requestImmediateFrame?.();
    };

    const disconnected = (event) => {
        const gamepad = event?.gamepad;
        if (gamepad) state.knownGamepads.delete(gamepad.index || 0);
        state.requestImmediateFrame?.();
    };

    window.addEventListener("gamepadconnected", connected);
    window.addEventListener("gamepaddisconnected", disconnected);
    state.listeners.push(
        [window, "gamepadconnected", connected],
        [window, "gamepaddisconnected", disconnected]);
}

function teardownGamepads(state) {
    state.knownGamepads?.clear();
}

function normalizeMobileOptions(options) {
    options = options || {};
    const zoomMin = normalizePositiveNumber(options.zoomMin ?? options.ZoomMin, 0.6);
    const zoomDefault = normalizePositiveNumber(options.zoomDefault ?? options.ZoomDefault, 0.6);
    const zoomMax = normalizePositiveNumber(options.zoomMax ?? options.ZoomMax, 1.3);
    const orderedMin = Math.min(zoomMin, zoomDefault, zoomMax);
    const orderedMax = Math.max(zoomMin, zoomDefault, zoomMax);
    return {
        requireHorizontal: !!(options.requireHorizontal ?? options.RequireHorizontal),
        touchTriggerToggle: !!(options.touchTriggerToggle ?? options.TouchTriggerToggle),
        enableZoom: (options.enableZoom ?? options.EnableZoom) !== false,
        zoomMin: orderedMin,
        zoomDefault: clamp(zoomDefault, orderedMin, orderedMax),
        zoomMax: orderedMax
    };
}

function buildZoomLevels(options) {
    const displayStep = 5;
    const minDisplayPercent = Math.ceil(((options.zoomMin / options.zoomDefault) * 100) / displayStep) * displayStep;
    const maxDisplayPercent = Math.floor(((options.zoomMax / options.zoomDefault) * 100) / displayStep) * displayStep;
    const displayPercents = [];
    for (let percent = minDisplayPercent; percent <= maxDisplayPercent; percent += displayStep) {
        displayPercents.push(percent);
    }

    const levels = displayPercents
        .concat([100])
        .map(percent => options.zoomDefault * (percent / 100))
        .map(level => roundZoom(clamp(level, options.zoomMin, options.zoomMax)))
        .filter((level, index, all) => all.indexOf(level) === index)
        .sort((left, right) => left - right);

    return levels.length === 0 ? [options.zoomDefault] : levels;
}

function normalizePositiveNumber(value, fallback) {
    const numeric = Number(value);
    return Number.isFinite(numeric) && numeric > 0 ? numeric : fallback;
}

function roundZoom(value) {
    return Math.round(value * 1000) / 1000;
}

function setupZoomControl(hostElement, state) {
    if (!state.mobileOptions.enableZoom || !shouldShowTouchController()) return;

    const control = document.createElement("div");
    control.className = "klooie-zoom-control";
    control.innerHTML = `
        <button type="button" class="klooie-zoom-out" aria-label="Zoom out">-</button>
        <output class="klooie-zoom-value"></output>
        <button type="button" class="klooie-zoom-in" aria-label="Zoom in">+</button>`;
    hostElement.appendChild(control);

    const value = control.querySelector(".klooie-zoom-value");
    const zoomOut = control.querySelector(".klooie-zoom-out");
    const zoomIn = control.querySelector(".klooie-zoom-in");

    const render = () => {
        value.textContent = `${Math.round((state.zoom / state.mobileOptions.zoomDefault) * 100)}%`;
        const index = getZoomIndex(state);
        zoomOut.disabled = index <= 0;
        zoomIn.disabled = index >= state.zoomLevels.length - 1;
    };

    const setZoomIndex = index => {
        state.zoom = state.zoomLevels[clamp(index, 0, state.zoomLevels.length - 1)];
        try {
            localStorage.setItem("klooie-mobile-zoom-v3", String(state.zoom));
        } catch {
        }
        updateCellMetrics(hostElement, state);
        state.sizeDirty = true;
        state.renderer?.invalidateMetrics();
        state.requestImmediateFrame?.();
        render();
    };

    zoomOut.addEventListener("pointerdown", event => {
        event.preventDefault();
        setZoomIndex(getZoomIndex(state) - 1);
    });

    zoomIn.addEventListener("pointerdown", event => {
        event.preventDefault();
        setZoomIndex(getZoomIndex(state) + 1);
    });

    render();
    state.zoomControl = {
        dispose() {
            control.remove();
        }
    };
}

function teardownZoomControl(state) {
    state.zoomControl?.dispose?.();
    state.zoomControl = undefined;
}

function getInitialZoom(state) {
    if (!state.mobileOptions.enableZoom || !shouldShowTouchController()) return 1;

    try {
        const stored = Number(localStorage.getItem("klooie-mobile-zoom-v3"));
        if (Number.isFinite(stored)) return state.zoomLevels[getZoomIndex({ ...state, zoom: stored })];
    } catch {
    }

    return getDefaultMobileZoom(state);
}

function getDefaultMobileZoom(state) {
    return state.mobileOptions.zoomDefault;
}

function getZoomIndex(state) {
    let bestIndex = 0;
    let bestDistance = Number.MAX_VALUE;
    for (let i = 0; i < state.zoomLevels.length; i++) {
        const distance = Math.abs(state.zoomLevels[i] - state.zoom);
        if (distance < bestDistance) {
            bestDistance = distance;
            bestIndex = i;
        }
    }

    return bestIndex;
}

function setupTouchController(hostElement, state)
{
    if (!shouldShowTouchController()) return;

    const overlay = document.createElement("div");
    overlay.className = "klooie-touch-controller";
    overlay.setAttribute("aria-hidden", "true");
    overlay.innerHTML = `
        <div class="klooie-horizontal-required">
            <div class="klooie-horizontal-required-card">
                <div class="klooie-horizontal-required-icon">↻</div>
                <div>Flip your phone horizontally</div>
            </div>
        </div>
        <div class="klooie-mobile-actions" hidden>
            <button type="button" class="klooie-mobile-fullscreen">Fullscreen</button>
            <button type="button" class="klooie-mobile-install" hidden>Install</button>
            <button type="button" class="klooie-mobile-dismiss" aria-label="Dismiss">X</button>
        </div>
        <div class="klooie-touch-stick-zone">
            <div class="klooie-touch-stick-base" data-button="10">
                <div class="klooie-touch-stick-label">LS</div>
                <div class="klooie-touch-stick-knob"></div>
            </div>
        </div>
        <div class="klooie-touch-shoulders klooie-touch-left-shoulders">
            <button type="button" data-button="6">LT</button>
            <button type="button" data-button="4">LB</button>
        </div>
        <div class="klooie-touch-shoulders klooie-touch-right-shoulders">
            <button type="button" data-button="7">RT</button>
            <button type="button" data-button="5">RB</button>
        </div>
        <div class="klooie-touch-system">
            <button type="button" data-button="8">View</button>
            <button type="button" data-button="9">Menu</button>
        </div>
        <div class="klooie-touch-face">
            <button type="button" class="klooie-touch-y" data-button="3">Y</button>
            <button type="button" class="klooie-touch-x" data-button="2">X</button>
            <button type="button" class="klooie-touch-b" data-button="1">B</button>
            <button type="button" class="klooie-touch-a" data-button="0">A</button>
        </div>`;
    hostElement.appendChild(overlay);

    const buttons = new Array(17).fill(false);
    const axes = [0, 0, 0, 0];
    const stickZone = overlay.querySelector(".klooie-touch-stick-zone");
    const stickBase = overlay.querySelector(".klooie-touch-stick-base");
    const stickKnob = overlay.querySelector(".klooie-touch-stick-knob");
    const mobileActions = overlay.querySelector(".klooie-mobile-actions");
    const fullscreenButton = overlay.querySelector(".klooie-mobile-fullscreen");
    const installButton = overlay.querySelector(".klooie-mobile-install");
    const dismissButton = overlay.querySelector(".klooie-mobile-dismiss");
    const leftStickPressButtonIndex = 10;
    const leftStickTapMaxMs = 220;
    const leftStickTapMaxMovementPx = 14;
    let stickPointerId = undefined;
    let stickBaseCenterX = 0;
    let stickBaseCenterY = 0;
    let stickTapStartTime = 0;
    let stickTapStartX = 0;
    let stickTapStartY = 0;
    let stickTapMoved = false;
    let leftStickPressPulseReads = 0;
    let mobileActionsDismissed = sessionStorage.getItem("klooie-mobile-actions-dismissed") === "true";
    const stickRadius = () => Math.max(42, Math.min(72, stickBase.getBoundingClientRect().width * 0.42));

    const updateMobileMode = () =>
    {
        const requireHorizontal = state.mobileOptions.requireHorizontal;
        const portrait = isPortraitViewport();
        overlay.classList.toggle("requires-horizontal", requireHorizontal);
        overlay.classList.toggle("is-portrait", requireHorizontal && portrait);
        hostElement.classList.toggle("klooie-mobile-portrait-blocked", requireHorizontal && portrait);
        updateMobileActions();
    };

    const updateMobileActions = () =>
    {
        const active = state.mobileOptions.requireHorizontal && !isPortraitViewport() && !mobileActionsDismissed;
        const canInstall = !!window.klooiePwa?.deferredInstallPrompt && !window.klooiePwa.installed;
        const canManualInstall = isIosBrowser() && !window.klooiePwa.installed;
        const fullscreenActive = isFullscreenActive();
        const canFullscreen = canRequestFullscreen();
        fullscreenButton.hidden = fullscreenActive || !canFullscreen;
        installButton.hidden = !canInstall && !canManualInstall;
        installButton.textContent = canInstall ? "Install" : "Add to Home Screen";
        mobileActions.hidden = !active || ((fullscreenActive || !canFullscreen) && !canInstall && !canManualInstall);
    };

    const clampStickBaseCenter = () =>
    {
        const zoneRect = stickZone.getBoundingClientRect();
        const half = stickBase.offsetWidth / 2;
        stickBaseCenterX = clamp(stickBaseCenterX, zoneRect.left + half, zoneRect.right - half);
        stickBaseCenterY = clamp(stickBaseCenterY, zoneRect.top + half, zoneRect.bottom - half);
    };

    const moveStickBase = () =>
    {
        const zoneRect = stickZone.getBoundingClientRect();
        stickBase.style.transform = `translate(${stickBaseCenterX - zoneRect.left - stickBase.offsetWidth / 2}px, ${stickBaseCenterY - zoneRect.top - stickBase.offsetHeight / 2}px)`;
    };

    const resetStick = (event) =>
    {
        if (event && stickPointerId !== event.pointerId) return;
        event?.preventDefault?.();

        const isTap =
            event?.type === "pointerup" &&
            stickTapMoved == false &&
            performance.now() - stickTapStartTime <= leftStickTapMaxMs;

        axes[0] = 0;
        axes[1] = 0;
        stickPointerId = undefined;
        stickBase.style.transform = "";
        stickKnob.style.transform = "";

        if (isTap) {
            leftStickPressPulseReads = Math.max(leftStickPressPulseReads, 2);
        }

        state.requestImmediateFrame?.();
    };

    const updateStick = (event) =>
    {
        if (stickPointerId !== event.pointerId) return;
        event.preventDefault();
        if (Math.hypot(event.clientX - stickTapStartX, event.clientY - stickTapStartY) > leftStickTapMaxMovementPx) {
            stickTapMoved = true;
        }

        const radius = stickRadius();
        clampStickBaseCenter();

        let dx = event.clientX - stickBaseCenterX;
        let dy = event.clientY - stickBaseCenterY;
        let distance = Math.hypot(dx, dy);
        let clampedDistance = Math.min(distance, radius);
        let unitX = distance > 0 ? dx / distance : 0;
        let unitY = distance > 0 ? dy / distance : 0;
        let knobX = unitX * clampedDistance;
        let knobY = unitY * clampedDistance;

        if (distance > radius) {
            stickBaseCenterX = event.clientX - knobX;
            stickBaseCenterY = event.clientY - knobY;
            clampStickBaseCenter();

            dx = event.clientX - stickBaseCenterX;
            dy = event.clientY - stickBaseCenterY;
            distance = Math.hypot(dx, dy);
            clampedDistance = Math.min(distance, radius);
            unitX = distance > 0 ? dx / distance : 0;
            unitY = distance > 0 ? dy / distance : 0;
            knobX = unitX * clampedDistance;
            knobY = unitY * clampedDistance;
        }

        moveStickBase();
        stickKnob.style.transform = `translate(${knobX}px, ${knobY}px)`;
        axes[0] = clamp(knobX / radius, -1, 1);
        axes[1] = clamp(knobY / radius, -1, 1);
        state.requestImmediateFrame?.();
    };

    const stickDown = (event) =>
    {
        if (stickPointerId !== undefined) return;
        event.preventDefault();
        stickPointerId = event.pointerId;
        stickZone.setPointerCapture?.(event.pointerId);
        stickBaseCenterX = event.clientX;
        stickBaseCenterY = event.clientY;
        stickTapStartTime = performance.now();
        stickTapStartX = event.clientX;
        stickTapStartY = event.clientY;
        stickTapMoved = false;
        clampStickBaseCenter();
        moveStickBase();
        updateStick(event);
    };

    stickZone.addEventListener("pointerdown", stickDown);
    stickZone.addEventListener("pointermove", updateStick);
    stickZone.addEventListener("pointerup", resetStick);
    stickZone.addEventListener("pointercancel", resetStick);

    const buttonPointerDown = (event) =>
    {
        const index = Number(event.currentTarget.dataset.button);
        if (!Number.isInteger(index)) return;
        event.preventDefault();
        event.currentTarget.setPointerCapture?.(event.pointerId);

        const isTrigger = index === 6 || index === 7;
        if (isTrigger && state.mobileOptions.touchTriggerToggle) {
            buttons[index] = !buttons[index];
        } else {
            buttons[index] = true;
        }

        event.currentTarget.classList.toggle("is-pressed", buttons[index]);
        state.requestImmediateFrame?.();
    };

    const buttonPointerUp = (event) =>
    {
        const index = Number(event.currentTarget.dataset.button);
        if (!Number.isInteger(index)) return;
        event.preventDefault();

        const isTrigger = index === 6 || index === 7;
        if (isTrigger && state.mobileOptions.touchTriggerToggle) return;

        buttons[index] = false;
        event.currentTarget.classList.remove("is-pressed");
        state.requestImmediateFrame?.();
    };

    const buttonElements = Array.from(overlay.querySelectorAll("button[data-button]"));
    for (const button of buttonElements) {
        button.addEventListener("pointerdown", buttonPointerDown);
        button.addEventListener("pointerup", buttonPointerUp);
        button.addEventListener("pointercancel", buttonPointerUp);
        button.addEventListener("contextmenu", preventDefault);
    }

    window.addEventListener("resize", updateMobileMode);
    window.addEventListener("orientationchange", updateMobileMode);
    document.addEventListener("fullscreenchange", updateMobileMode);
    document.addEventListener("webkitfullscreenchange", updateMobileMode);
    window.addEventListener("klooie-pwa-install-available", updateMobileMode);
    window.addEventListener("klooie-pwa-installed", updateMobileMode);
    fullscreenButton.addEventListener("pointerdown", requestFullscreenFromButton);
    installButton.addEventListener("pointerdown", promptPwaInstall);
    dismissButton.addEventListener("pointerdown", dismissMobileActions);
    updateMobileMode();

    state.touchController = {
        overlay,
        buttons,
        axes,
        readGamepad()
        {
            const effectiveButtons = buttons.map(pressed => ({ pressed, value: pressed ? 1 : 0 }));
            if (leftStickPressPulseReads > 0) {
                effectiveButtons[leftStickPressButtonIndex] = { pressed: true, value: 1 };
                leftStickPressPulseReads--;
                if (leftStickPressPulseReads === 0) {
                    window.setTimeout(() => state.requestImmediateFrame?.(), 0);
                }
            }

            return {
                id: "Mobile Touch Controller",
                index: 1000,
                connected: true,
                mapping: "klooie-touch",
                buttons: effectiveButtons,
                axes: axes.slice(0)
            };
        },
        releaseButtons(indices)
        {
            for (const index of indices || []) {
                if (!Number.isInteger(index) || index < 0 || index >= buttons.length) continue;
                buttons[index] = false;
                overlay.querySelector(`button[data-button="${index}"]`)?.classList.remove("is-pressed");
            }

            state.requestImmediateFrame?.();
        },
        applyButtonHints(hints)
        {
            applyTouchButtonHints(overlay, hints);
        },
        dispose()
        {
            window.removeEventListener("resize", updateMobileMode);
            window.removeEventListener("orientationchange", updateMobileMode);
            document.removeEventListener("fullscreenchange", updateMobileMode);
            document.removeEventListener("webkitfullscreenchange", updateMobileMode);
            window.removeEventListener("klooie-pwa-install-available", updateMobileMode);
            window.removeEventListener("klooie-pwa-installed", updateMobileMode);
            hostElement.classList.remove("klooie-mobile-portrait-blocked");
            overlay.remove();
        }
    };

    function requestFullscreenFromButton(event) {
        event.preventDefault();
        requestFullscreen(hostElement).finally(updateMobileMode);
    }

    async function promptPwaInstall(event) {
        event.preventDefault();
        const prompt = window.klooiePwa?.deferredInstallPrompt;
        if (!prompt) {
            if (isIosBrowser()) window.alert("Use the browser Share button, then choose Add to Home Screen.");
            return;
        }

        window.klooiePwa.deferredInstallPrompt = undefined;
        try {
            await prompt.prompt();
            await prompt.userChoice;
        } catch {
        }

        updateMobileMode();
    }

    function dismissMobileActions(event) {
        event.preventDefault();
        mobileActionsDismissed = true;
        sessionStorage.setItem("klooie-mobile-actions-dismissed", "true");
        updateMobileMode();
    }
}

function teardownTouchController(state) {
    state.touchController?.dispose?.();
    state.touchController = undefined;
}

function shouldShowTouchController() {
    if (typeof window === "undefined") return false;
    const coarse = window.matchMedia?.("(pointer: coarse)")?.matches;
    const hoverless = window.matchMedia?.("(hover: none)")?.matches;
    const touchPoints = navigator.maxTouchPoints || 0;
    return !!(coarse || hoverless || touchPoints > 0);
}

function isPortraitViewport() {
    const viewport = window.visualViewport;
    const width = viewport?.width || window.innerWidth || document.documentElement.clientWidth;
    const height = viewport?.height || window.innerHeight || document.documentElement.clientHeight;
    return height > width;
}

function isFullscreenActive() {
    return !!(document.fullscreenElement || document.webkitFullscreenElement) ||
        window.matchMedia?.("(display-mode: fullscreen)")?.matches ||
        window.matchMedia?.("(display-mode: standalone)")?.matches ||
        navigator.standalone === true;
}

function canRequestFullscreen() {
    const element = document.documentElement;
    return !!(element.requestFullscreen || element.webkitRequestFullscreen);
}

function isIosBrowser() {
    return /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
}

async function requestFullscreen(element) {
    try {
        if (element.requestFullscreen) {
            await element.requestFullscreen({ navigationUI: "hide" });
        } else if (element.webkitRequestFullscreen) {
            element.webkitRequestFullscreen();
        }
    } catch {
    }
}

function preventDefault(event) {
    event.preventDefault();
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

function readGamepadSnapshotJson(state) {
    try {
        const rawGamepads = typeof navigator !== "undefined" && typeof navigator.getGamepads === "function"
            ? navigator.getGamepads()
            : [];
        const gamepadsByIndex = new Map();
        for (const gamepad of state?.knownGamepads?.values?.() || []) {
            if (!gamepad || !gamepad.connected) continue;
            gamepadsByIndex.set(gamepad.index || 0, gamepad);
        }

        for (const gamepad of rawGamepads) {
            if (!gamepad || !gamepad.connected) continue;
            gamepadsByIndex.set(gamepad.index || 0, gamepad);
        }

        const gamepads = Array.from(gamepadsByIndex.values(), gamepad => ({
            id: gamepad.id || "",
            index: gamepad.index || 0,
            connected: !!gamepad.connected,
            mapping: gamepad.mapping || "",
            buttons: Array.from(gamepad.buttons || [], button => ({
                pressed: !!button?.pressed,
                value: normalizeUnit(button?.value, 0)
            })),
            axes: Array.from(gamepad.axes || [], axis => normalizeAxis(axis))
        }));

        if (state?.touchController) {
            gamepads.push(state.touchController.readGamepad());
        }

        return JSON.stringify({ gamepads });
    } catch (error) {
        console.debug("klooie gamepad snapshot skipped", error);
        return null;
    }
}

function applyBrowserControllerCommands(state, frame) {
    const releases = frame?.touchButtonReleases || frame?.TouchButtonReleases;
    if (releases?.length > 0) {
        state.touchController?.releaseButtons(releases);
    }

    const hints = frame?.touchButtonHints || frame?.TouchButtonHints;
    if (hints?.length > 0) {
        state.touchController?.applyButtonHints(hints);
    }
}

function applyTouchButtonHints(overlay, hints) {
    if (!overlay) return;

    const resetToDefaults = hints?.some?.(hint => Number(hint?.button ?? hint?.Button) < 0);
    if (resetToDefaults) {
        for (const element of overlay.querySelectorAll("[data-button]")) {
            const index = Number(element.dataset.button);
            const labelElement = element.classList.contains("klooie-touch-stick-base")
                ? element.querySelector(".klooie-touch-stick-label")
                : element;
            if (labelElement) labelElement.textContent = getDefaultTouchButtonLabel(index);
            element.classList.remove("is-disabled");
            element.setAttribute("aria-disabled", "false");
        }

        return;
    }

    for (const hint of hints || []) {
        const index = Number(hint?.button ?? hint?.Button);
        if (!Number.isInteger(index) || index < 0) continue;

        const label = String(hint?.label ?? hint?.Label ?? getDefaultTouchButtonLabel(index));
        const enabled = (hint?.enabled ?? hint?.Enabled) !== false;
        const element = overlay.querySelector(`[data-button="${index}"]`);
        if (!element) continue;

        const labelElement = element.classList.contains("klooie-touch-stick-base")
            ? element.querySelector(".klooie-touch-stick-label")
            : element;
        if (labelElement) labelElement.textContent = label;
        element.classList.toggle("is-disabled", !enabled);
        element.setAttribute("aria-disabled", enabled ? "false" : "true");
    }
}

function getDefaultTouchButtonLabel(index) {
    if (index === 0) return "A";
    if (index === 1) return "B";
    if (index === 2) return "X";
    if (index === 3) return "Y";
    if (index === 4) return "LB";
    if (index === 5) return "RB";
    if (index === 6) return "LT";
    if (index === 7) return "RT";
    if (index === 8) return "View";
    if (index === 9) return "Menu";
    if (index === 10) return "LS";
    return "";
}

function normalizeAxis(value) {
    const numeric = Number(value);
    return Math.max(-1, Math.min(1, Number.isFinite(numeric) ? numeric : 0));
}

function clamp(value, min, max) {
    return Math.max(min, Math.min(max, value));
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
    const probe = getMeasureProbe(hostElement);
    const rect = probe.getBoundingClientRect();
    const style = window.getComputedStyle(probe);
    state.baseCellWidth = Math.max(1, rect?.width || 8);
    state.baseCellHeight = Math.max(1, rect?.height || 16);
    state.baseFont = style.font || "16px Consolas, 'Cascadia Mono', 'Courier New', monospace";
    state.cellWidth = Math.max(1, state.baseCellWidth * state.zoom);
    state.cellHeight = Math.max(1, state.baseCellHeight * state.zoom);
    state.devicePixelRatio = window.devicePixelRatio || 1;
    state.font = scaleFont(state.baseFont, state.zoom);
}

function getMeasureProbe(hostElement) {
    let probe = hostElement.querySelector(".browser-console-measure");
    if (!probe) {
        probe = document.createElement("span");
        probe.className = "browser-console-measure";
        probe.textContent = "M";
        hostElement.appendChild(probe);
    }

    return probe;
}

function buildPresentationDraws(frame, state, pixelWidth, pixelHeight) {
    const fullSource = { left: 0, top: 0, width: frame.width, height: frame.height };
    const fullTarget = { left: 0, top: 0, width: pixelWidth, height: pixelHeight };
    const focus = updateFocusPresentation(frame, state, pixelWidth, pixelHeight, fullSource, fullTarget);
    const viewTarget = mapSourceRectToTarget(fullSource, focus.source, focus.target);
    const draws = [{ source: fullSource, target: viewTarget }];
    const scaledRegions = frame.presentation?.scaledRegions || frame.presentation?.ScaledRegions || [];

    for (const region of scaledRegions) {
        const source = normalizePresentationRect(region.sourceRegion || region.SourceRegion, frame.width, frame.height);
        if (!source) continue;

        const scale = normalizePositiveNumber(region.scale ?? region.Scale, 1);
        const baseTarget = mapSourceRectToTarget(source, focus.source, focus.target);
        const targetWidth = baseTarget.width * scale;
        const targetHeight = baseTarget.height * scale;
        const anchor = region.anchor ?? region.Anchor ?? 0;
        const offsetX = Number(region.offsetX ?? region.OffsetX ?? 0) * (baseTarget.width / Math.max(1, source.width));
        const offsetY = Number(region.offsetY ?? region.OffsetY ?? 0) * (baseTarget.height / Math.max(1, source.height));
        const anchorPoint = getAnchoredPoint(baseTarget, anchor);
        const target = rectFromAnchor(anchorPoint.x + offsetX, anchorPoint.y + offsetY, targetWidth, targetHeight, anchor);
        draws.push({ source, target });
    }

    return draws;
}

function getPresentationGlyphScale(draws, state) {
    const baseCellWidth = Math.max(1, state.cellWidth * state.devicePixelRatio);
    const baseCellHeight = Math.max(1, state.cellHeight * state.devicePixelRatio);
    let scale = 1;

    for (const draw of draws) {
        const sourceWidth = Math.max(1, draw.source.width * baseCellWidth);
        const sourceHeight = Math.max(1, draw.source.height * baseCellHeight);
        scale = Math.max(scale, draw.target.width / sourceWidth, draw.target.height / sourceHeight);
    }

    return clamp(Math.ceil(scale), 1, 8);
}

function mapSourceRectToTarget(source, viewSource, viewTarget) {
    const scaleX = viewTarget.width / Math.max(1, viewSource.width);
    const scaleY = viewTarget.height / Math.max(1, viewSource.height);
    return {
        left: viewTarget.left + (source.left - viewSource.left) * scaleX,
        top: viewTarget.top + (source.top - viewSource.top) * scaleY,
        width: source.width * scaleX,
        height: source.height * scaleY
    };
}

function updateFocusPresentation(frame, state, pixelWidth, pixelHeight, fullSource, fullTarget) {
    const activeFocus = getActiveFocusRegion(frame.presentation);
    const now = performance.now();
    const fullDraw = { source: fullSource, target: fullTarget };

    if (activeFocus) {
        const targetDraw = computeFocusDraw(activeFocus, frame, state, pixelWidth, pixelHeight);
        if (!state.presentationFocus || state.presentationFocus.id !== activeFocus.id || state.presentationFocus.exiting) {
            const fromDraw = state.presentationFocus?.currentDraw || fullDraw;
            state.presentationFocus = { id: activeFocus.id, region: activeFocus, startedAt: now, exiting: false, fromDraw, toDraw: targetDraw };
        } else {
            state.presentationFocus.region = activeFocus;
            state.presentationFocus.toDraw = targetDraw;
        }

        const duration = normalizePositiveNumber(activeFocus.animationMilliseconds ?? activeFocus.AnimationMilliseconds, 450);
        const progress = easeInOutCinematic(clamp((now - state.presentationFocus.startedAt) / duration, 0, 1));
        if (progress < 1) state.requestImmediateFrame?.();
        const currentDraw = interpolateDraw(state.presentationFocus.fromDraw.source, state.presentationFocus.fromDraw.target, state.presentationFocus.toDraw.source, state.presentationFocus.toDraw.target, progress);
        state.presentationFocus.currentDraw = currentDraw;
        return currentDraw;
    }

    if (state.presentationFocus && !state.presentationFocus.exiting) {
        state.presentationFocus = {
            id: state.presentationFocus.id,
            region: state.presentationFocus.region,
            startedAt: now,
            exiting: true,
            fromDraw: state.presentationFocus.currentDraw || state.presentationFocus.toDraw || fullDraw,
            toDraw: fullDraw
        };
    }

    if (state.presentationFocus?.exiting) {
        const region = state.presentationFocus.region;
        const duration = normalizePositiveNumber(region.animationMilliseconds ?? region.AnimationMilliseconds, 450);
        const progress = easeInOutCinematic(clamp((now - state.presentationFocus.startedAt) / duration, 0, 1));
        if (progress < 1) {
            state.requestImmediateFrame?.();
            const currentDraw = interpolateDraw(state.presentationFocus.fromDraw.source, state.presentationFocus.fromDraw.target, state.presentationFocus.toDraw.source, state.presentationFocus.toDraw.target, progress);
            state.presentationFocus.currentDraw = currentDraw;
            return currentDraw;
        }

        state.presentationFocus = undefined;
    }

    return { source: fullSource, target: fullTarget };
}

function getActiveFocusRegion(presentation) {
    const focusRegions = presentation?.focusRegions || presentation?.FocusRegions || [];
    return focusRegions.length > 0 ? focusRegions[focusRegions.length - 1] : undefined;
}

function computeFocusDraw(region, frame, state, pixelWidth, pixelHeight) {
    const source = normalizePresentationRect(region.sourceRegion || region.SourceRegion, frame.width, frame.height) || { left: 0, top: 0, width: frame.width, height: frame.height };
    const padding = clamp(Number(region.padding ?? region.Padding ?? .06), 0, .45);
    const safe = getSafeAreaPixels(pixelWidth, pixelHeight);
    const safeWidth = Math.max(1, pixelWidth - safe.left - safe.right);
    const safeHeight = Math.max(1, pixelHeight - safe.top - safe.bottom);
    const paddedWidth = Math.max(1, safeWidth * (1 - padding * 2));
    const paddedHeight = Math.max(1, safeHeight * (1 - padding * 2));
    const sourcePixelWidth = source.width * state.cellWidth * state.devicePixelRatio;
    const sourcePixelHeight = source.height * state.cellHeight * state.devicePixelRatio;
    const scale = Math.min(paddedWidth / Math.max(1, sourcePixelWidth), paddedHeight / Math.max(1, sourcePixelHeight));
    const targetWidth = sourcePixelWidth * scale;
    const targetHeight = sourcePixelHeight * scale;
    const anchor = region.anchor ?? region.Anchor ?? 4;
    const offsetX = Number(region.offsetX ?? region.OffsetX ?? 0) * state.cellWidth * state.devicePixelRatio;
    const offsetY = Number(region.offsetY ?? region.OffsetY ?? 0) * state.cellHeight * state.devicePixelRatio;
    const anchorPoint = getAnchoredPoint({
        left: safe.left + padding * safeWidth,
        top: safe.top + padding * safeHeight,
        width: paddedWidth,
        height: paddedHeight
    }, anchor);

    return {
        source,
        target: rectFromAnchor(anchorPoint.x + offsetX, anchorPoint.y + offsetY, targetWidth, targetHeight, anchor)
    };
}

function normalizePresentationRect(rect, frameWidth, frameHeight) {
    if (!rect) return undefined;

    const left = Math.max(0, Number(rect.left ?? rect.Left ?? 0));
    const top = Math.max(0, Number(rect.top ?? rect.Top ?? 0));
    if (left >= frameWidth || top >= frameHeight) return undefined;

    const width = Math.max(1, Number(rect.width ?? rect.Width ?? 1));
    const height = Math.max(1, Number(rect.height ?? rect.Height ?? 1));
    const clippedWidth = Math.min(width, frameWidth - left);
    const clippedHeight = Math.min(height, frameHeight - top);
    return { left, top, width: clippedWidth, height: clippedHeight };
}

function getSafeAreaPixels(pixelWidth, pixelHeight) {
    let probe = document.querySelector(".klooie-safe-area-probe");
    if (!probe) {
        probe = document.createElement("div");
        probe.className = "klooie-safe-area-probe";
        probe.style.cssText = "position:fixed;inset:0;padding:env(safe-area-inset-top) env(safe-area-inset-right) env(safe-area-inset-bottom) env(safe-area-inset-left);pointer-events:none;visibility:hidden;";
        document.body.appendChild(probe);
    }

    const style = window.getComputedStyle(probe);
    const dpr = window.devicePixelRatio || 1;
    return {
        left: clamp(parseCssPixels(style.paddingLeft) * dpr, 0, pixelWidth * .45),
        top: clamp(parseCssPixels(style.paddingTop) * dpr, 0, pixelHeight * .45),
        right: clamp(parseCssPixels(style.paddingRight) * dpr, 0, pixelWidth * .45),
        bottom: clamp(parseCssPixels(style.paddingBottom) * dpr, 0, pixelHeight * .45)
    };
}

function parseCssPixels(value) {
    const numeric = Number.parseFloat(value);
    return Number.isFinite(numeric) ? numeric : 0;
}

function getAnchoredPoint(rect, anchor) {
    const index = normalizeAnchor(anchor);
    const x = index === 2 || index === 5 || index === 8 ? rect.left + rect.width : index === 1 || index === 4 || index === 7 ? rect.left + rect.width / 2 : rect.left;
    const y = index === 6 || index === 7 || index === 8 ? rect.top + rect.height : index === 3 || index === 4 || index === 5 ? rect.top + rect.height / 2 : rect.top;
    return { x, y };
}

function rectFromAnchor(anchorX, anchorY, width, height, anchor) {
    const index = normalizeAnchor(anchor);
    const left = index === 2 || index === 5 || index === 8 ? anchorX - width : index === 1 || index === 4 || index === 7 ? anchorX - width / 2 : anchorX;
    const top = index === 6 || index === 7 || index === 8 ? anchorY - height : index === 3 || index === 4 || index === 5 ? anchorY - height / 2 : anchorY;
    return { left, top, width, height };
}

function normalizeAnchor(anchor) {
    if (typeof anchor === "number") return clamp(Math.round(anchor), 0, 8);
    switch (String(anchor || "Center")) {
        case "TopLeft": return 0;
        case "Top": return 1;
        case "TopRight": return 2;
        case "Left": return 3;
        case "Right": return 5;
        case "BottomLeft": return 6;
        case "Bottom": return 7;
        case "BottomRight": return 8;
        default: return 4;
    }
}

function interpolateDraw(fromSource, fromTarget, toSource, toTarget, progress) {
    return {
        source: interpolateRect(fromSource, toSource, progress),
        target: interpolateRect(fromTarget, toTarget, progress)
    };
}

function interpolateRect(from, to, progress) {
    return {
        left: lerp(from.left, to.left, progress),
        top: lerp(from.top, to.top, progress),
        width: lerp(from.width, to.width, progress),
        height: lerp(from.height, to.height, progress)
    };
}

function lerp(from, to, progress) {
    return from + (to - from) * progress;
}

function easeInOutCinematic(value) {
    value = clamp(value, 0, 1);
    return value * value * value * (value * (value * 6 - 15) + 10);
}

function createConsoleRenderer(canvas, state) {
    try {
        return new WebGl2CellConsoleRenderer(canvas, state);
    } catch (error) {
        console.warn("klooie WebGL2 cell renderer unavailable; falling back to retained WebGL renderer", error);
        try {
            return new WebGlConsoleRenderer(canvas, state);
        } catch (fallbackError) {
            console.warn("klooie GPU renderer unavailable; falling back to Canvas2D", fallbackError);
            return new Canvas2DConsoleRenderer(canvas);
        }
    }
}

function verifyWebGlFrame(gl, pixelWidth, pixelHeight, rendererName, frame, state) {
    if (!frame || frame.text.length === 0) return true;

    const pixels = new Uint8Array(pixelWidth * pixelHeight * 4);
    gl.readPixels(0, 0, pixelWidth, pixelHeight, gl.RGBA, gl.UNSIGNED_BYTE, pixels);

    let visiblePixels = 0;
    for (let i = 0; i < pixels.length; i += 4) {
        if (pixels[i] !== 0 || pixels[i + 1] !== 0 || pixels[i + 2] !== 0) {
            visiblePixels++;
            if (visiblePixels > 16) return true;
        }
    }

    console.error("klooie WebGL renderer produced an all-black frame", {
        renderer: rendererName,
        canvasWidth: pixelWidth,
        canvasHeight: pixelHeight,
        frameWidth: frame.width,
        frameHeight: frame.height,
        textRunCount: frame.text.length,
        firstTextRun: frame.text[0] || "",
        devicePixelRatio: state.devicePixelRatio,
        userAgent: navigator.userAgent
    });
    return false;
}

class WebGl2CellConsoleRenderer {
    constructor(canvas, state) {
        const gl = canvas.getContext("webgl2", {
            alpha: false,
            antialias: false,
            depth: false,
            preserveDrawingBuffer: false,
            stencil: false
        });

        if (!gl) {
            throw new Error("WebGL2 is not available.");
        }

        canvas.dataset.klooieRenderer = "webgl2-cell";
        this.gl = gl;
        this.program = createProgram(gl, cellVertexShaderSource, cellFragmentShaderSource);
        this.vertexBuffer = gl.createBuffer();
        this.glyphTexture = gl.createTexture();
        this.foregroundTexture = gl.createTexture();
        this.backgroundTexture = gl.createTexture();
        this.atlasTexture = gl.createTexture();
        this.colorCache = new Map();
        this.glyphToIndex = new Map([[" ", 0], ["\u00A0", 0]]);
        this.indexToGlyph = [" "];
        this.gridWidth = 0;
        this.gridHeight = 0;
        this.glyphData = new Uint8Array(0);
        this.foregroundData = new Uint8Array(0);
        this.backgroundData = new Uint8Array(0);
        this.uploads = [];
        this.vertexData = new Float32Array(12);
        this.blackFrameChecks = 0;
        this.blackFrameReported = false;
        this.metricsKey = "";
        this.atlasDirty = false;
        this.atlasSize = 2048;
        this.atlasPadding = 2;
        this.atlasCanvas = createRasterCanvas(this.atlasSize, this.atlasSize);
        this.atlasContext = this.atlasCanvas.getContext("2d", { alpha: true });

        if (!this.atlasContext) {
            throw new Error("A raster canvas is required to build the glyph atlas.");
        }

        gl.disable(gl.DEPTH_TEST);
        gl.disable(gl.CULL_FACE);
        gl.disable(gl.BLEND);
        gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);

        this.locations = {
            position: gl.getAttribLocation(this.program, "a_position"),
            resolution: gl.getUniformLocation(this.program, "u_resolution"),
            gridSize: gl.getUniformLocation(this.program, "u_gridSize"),
            cellSize: gl.getUniformLocation(this.program, "u_cellSize"),
            glyphTexture: gl.getUniformLocation(this.program, "u_glyphs"),
            foregroundTexture: gl.getUniformLocation(this.program, "u_foreground"),
            backgroundTexture: gl.getUniformLocation(this.program, "u_background"),
            atlasTexture: gl.getUniformLocation(this.program, "u_atlas"),
            atlasSize: gl.getUniformLocation(this.program, "u_atlasSize"),
            atlasCellSize: gl.getUniformLocation(this.program, "u_atlasCellSize"),
            glyphSize: gl.getUniformLocation(this.program, "u_glyphSize"),
            atlasColumns: gl.getUniformLocation(this.program, "u_atlasColumns"),
            sourceOrigin: gl.getUniformLocation(this.program, "u_sourceOrigin"),
            sourceSize: gl.getUniformLocation(this.program, "u_sourceSize"),
            targetOrigin: gl.getUniformLocation(this.program, "u_targetOrigin"),
            targetSize: gl.getUniformLocation(this.program, "u_targetSize")
        };

        this.configureCellTexture(this.glyphTexture);
        this.configureCellTexture(this.foregroundTexture);
        this.configureCellTexture(this.backgroundTexture);
        this.configureAtlasTexture();
        this.ensureMetrics(state, 1);
    }

    invalidateMetrics() {
        this.metricsKey = "";
    }

    dispose() {
        const gl = this.gl;
        gl.deleteBuffer(this.vertexBuffer);
        gl.deleteTexture(this.glyphTexture);
        gl.deleteTexture(this.foregroundTexture);
        gl.deleteTexture(this.backgroundTexture);
        gl.deleteTexture(this.atlasTexture);
        gl.deleteProgram(this.program);
    }

    render(canvas, frame, state) {
        const cssWidth = frame.width * state.cellWidth;
        const cssHeight = frame.height * state.cellHeight;
        const pixelWidth = Math.max(1, Math.ceil(cssWidth * state.devicePixelRatio));
        const pixelHeight = Math.max(1, Math.ceil(cssHeight * state.devicePixelRatio));

        if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
            canvas.width = pixelWidth;
            canvas.height = pixelHeight;
        }

        const draws = buildPresentationDraws(frame, state, pixelWidth, pixelHeight);
        const glyphScale = getPresentationGlyphScale(draws, state);
        this.ensureMetrics(state, glyphScale);
        const resized = this.ensureCellTextures(frame.width, frame.height);
        const uploadAll = this.applyFrame(frame, resized);
        this.uploadCellTextures(uploadAll);
        this.draw(pixelWidth, pixelHeight, frame, state, draws);
        if (!this.blackFrameReported && this.blackFrameChecks < 3 && frame.text.length > 0) {
            this.blackFrameChecks++;
            this.blackFrameReported = !verifyWebGlFrame(this.gl, pixelWidth, pixelHeight, "webgl2-cell", frame, state);
        }
    }

    configureCellTexture(texture) {
        const gl = this.gl;
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    }

    configureAtlasTexture() {
        const gl = this.gl;
        gl.bindTexture(gl.TEXTURE_2D, this.atlasTexture);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    }

    ensureMetrics(state, presentationScale) {
        const dpr = state.devicePixelRatio || 1;
        const scale = normalizePositiveNumber(presentationScale, 1);
        const glyphWidth = Math.max(1, Math.ceil(state.cellWidth * dpr * scale));
        const glyphHeight = Math.max(1, Math.ceil(state.cellHeight * dpr * scale));
        const key = `${glyphWidth}x${glyphHeight}:${dpr}:${scale}:${state.font}`;
        if (key === this.metricsKey) return;

        this.metricsKey = key;
        this.glyphWidth = glyphWidth;
        this.glyphHeight = glyphHeight;
        this.atlasCellWidth = glyphWidth + this.atlasPadding * 2;
        this.atlasCellHeight = glyphHeight + this.atlasPadding * 2;
        this.atlasColumns = Math.max(1, Math.floor(this.atlasSize / this.atlasCellWidth));
        this.scaledFont = scaleFont(state.font, dpr * scale);
        this.rasterizeAtlas();
    }

    rasterizeAtlas() {
        this.atlasContext.setTransform(1, 0, 0, 1, 0, 0);
        this.atlasContext.clearRect(0, 0, this.atlasSize, this.atlasSize);
        this.atlasContext.fillStyle = "#fff";
        this.atlasContext.textBaseline = "top";
        this.atlasContext.font = this.scaledFont;

        for (let index = 1; index < this.indexToGlyph.length; index++) {
            this.drawGlyphAtIndex(this.indexToGlyph[index], index);
        }

        const gl = this.gl;
        gl.bindTexture(gl.TEXTURE_2D, this.atlasTexture);
        gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, this.atlasCanvas);
        this.atlasDirty = false;
    }

    ensureCellTextures(width, height) {
        if (width === this.gridWidth && height === this.gridHeight) return false;

        this.gridWidth = width;
        this.gridHeight = height;
        const byteLength = width * height * 4;
        this.glyphData = new Uint8Array(byteLength);
        this.foregroundData = new Uint8Array(byteLength);
        this.backgroundData = new Uint8Array(byteLength);

        for (let i = 0; i < byteLength; i += 4) {
            this.foregroundData[i] = 255;
            this.foregroundData[i + 1] = 255;
            this.foregroundData[i + 2] = 255;
            this.foregroundData[i + 3] = 255;
            this.backgroundData[i + 3] = 255;
            this.glyphData[i + 3] = 255;
        }

        const gl = this.gl;
        this.allocateCellTexture(this.glyphTexture, this.glyphData);
        this.allocateCellTexture(this.foregroundTexture, this.foregroundData);
        this.allocateCellTexture(this.backgroundTexture, this.backgroundData);
        return true;
    }

    allocateCellTexture(texture, data) {
        const gl = this.gl;
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, this.gridWidth, this.gridHeight, 0, gl.RGBA, gl.UNSIGNED_BYTE, data);
    }

    applyFrame(frame, resized) {
        this.uploads.length = 0;
        let dirtyCells = 0;
        let uploadAll = resized || frame.full;

        if (uploadAll && frame.text.length === 0) {
            this.clearCells();
        }

        for (let runIndex = 0; runIndex < frame.text.length; runIndex++) {
            const runText = frame.text[runIndex];
            const row = frame.y[runIndex];
            const column = frame.x[runIndex];
            if (row < 0 || row >= this.gridHeight) continue;

            const start = Math.max(0, column);
            const end = Math.min(this.gridWidth, column + runText.length);
            if (end <= start) continue;

            const foreground = this.parseColorBytes(frame.foreground[runIndex]);
            const background = this.parseColorBytes(frame.background[runIndex]);

            for (let x = start; x < end; x++) {
                const sourceOffset = x - column;
                const cellOffset = (row * this.gridWidth + x) * 4;
                const glyphIndex = this.ensureGlyph(runText[sourceOffset]);

                this.glyphData[cellOffset] = glyphIndex & 0xff;
                this.glyphData[cellOffset + 1] = (glyphIndex >> 8) & 0xff;
                this.glyphData[cellOffset + 2] = 0;
                this.glyphData[cellOffset + 3] = 255;

                this.foregroundData[cellOffset] = foreground[0];
                this.foregroundData[cellOffset + 1] = foreground[1];
                this.foregroundData[cellOffset + 2] = foreground[2];
                this.foregroundData[cellOffset + 3] = 255;

                this.backgroundData[cellOffset] = background[0];
                this.backgroundData[cellOffset + 1] = background[1];
                this.backgroundData[cellOffset + 2] = background[2];
                this.backgroundData[cellOffset + 3] = 255;
            }

            dirtyCells += end - start;
            if (!uploadAll) {
                this.uploads.push({ x: start, y: row, length: end - start });
            }
        }

        if (!uploadAll && (dirtyCells > 8192 || this.uploads.length > 128)) {
            uploadAll = true;
        }

        this.uploadAtlasIfNeeded();
        return uploadAll;
    }

    clearCells() {
        for (let i = 0; i < this.glyphData.length; i += 4) {
            this.glyphData[i] = 0;
            this.glyphData[i + 1] = 0;
            this.glyphData[i + 2] = 0;
            this.glyphData[i + 3] = 255;

            this.foregroundData[i] = 255;
            this.foregroundData[i + 1] = 255;
            this.foregroundData[i + 2] = 255;
            this.foregroundData[i + 3] = 255;

            this.backgroundData[i] = 0;
            this.backgroundData[i + 1] = 0;
            this.backgroundData[i + 2] = 0;
            this.backgroundData[i + 3] = 255;
        }
    }

    uploadCellTextures(uploadAll) {
        const gl = this.gl;
        if (uploadAll) {
            this.uploadWholeCellTexture(this.glyphTexture, this.glyphData);
            this.uploadWholeCellTexture(this.foregroundTexture, this.foregroundData);
            this.uploadWholeCellTexture(this.backgroundTexture, this.backgroundData);
            return;
        }

        for (const upload of this.uploads) {
            const start = (upload.y * this.gridWidth + upload.x) * 4;
            const end = start + upload.length * 4;
            this.uploadCellRun(this.glyphTexture, upload.x, upload.y, upload.length, this.glyphData.subarray(start, end));
            this.uploadCellRun(this.foregroundTexture, upload.x, upload.y, upload.length, this.foregroundData.subarray(start, end));
            this.uploadCellRun(this.backgroundTexture, upload.x, upload.y, upload.length, this.backgroundData.subarray(start, end));
        }
    }

    uploadWholeCellTexture(texture, data) {
        const gl = this.gl;
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, this.gridWidth, this.gridHeight, gl.RGBA, gl.UNSIGNED_BYTE, data);
    }

    uploadCellRun(texture, x, y, length, data) {
        const gl = this.gl;
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.texSubImage2D(gl.TEXTURE_2D, 0, x, y, length, 1, gl.RGBA, gl.UNSIGNED_BYTE, data);
    }

    ensureGlyph(glyph) {
        if (isBlankGlyph(glyph)) return 0;

        let index = this.glyphToIndex.get(glyph);
        if (index !== undefined) return index;

        index = this.indexToGlyph.length;
        const row = Math.floor(index / this.atlasColumns);
        if ((row + 1) * this.atlasCellHeight > this.atlasSize) {
            throw new Error("The glyph atlas is full.");
        }

        this.glyphToIndex.set(glyph, index);
        this.indexToGlyph[index] = glyph;
        this.drawGlyphAtIndex(glyph, index);
        this.atlasDirty = true;
        return index;
    }

    drawGlyphAtIndex(glyph, index) {
        const column = index % this.atlasColumns;
        const row = Math.floor(index / this.atlasColumns);
        const x = column * this.atlasCellWidth;
        const y = row * this.atlasCellHeight;
        this.atlasContext.clearRect(x, y, this.atlasCellWidth, this.atlasCellHeight);
        this.atlasContext.font = this.scaledFont;
        this.atlasContext.fillStyle = "#fff";
        this.atlasContext.fillText(glyph, x + this.atlasPadding, y + this.atlasPadding);
    }

    uploadAtlasIfNeeded() {
        if (!this.atlasDirty) return;

        const gl = this.gl;
        gl.bindTexture(gl.TEXTURE_2D, this.atlasTexture);
        gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, this.atlasCanvas);
        this.atlasDirty = false;
    }

    draw(pixelWidth, pixelHeight, frame, state, draws) {
        const gl = this.gl;
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        gl.viewport(0, 0, pixelWidth, pixelHeight);
        gl.clearColor(0, 0, 0, 1);
        gl.clear(gl.COLOR_BUFFER_BIT);
        gl.useProgram(this.program);

        gl.uniform2f(this.locations.resolution, pixelWidth, pixelHeight);
        gl.uniform2f(this.locations.gridSize, this.gridWidth, this.gridHeight);
        gl.uniform2f(this.locations.cellSize, state.cellWidth * state.devicePixelRatio, state.cellHeight * state.devicePixelRatio);
        gl.uniform2f(this.locations.atlasSize, this.atlasSize, this.atlasSize);
        gl.uniform2f(this.locations.atlasCellSize, this.atlasCellWidth, this.atlasCellHeight);
        gl.uniform2f(this.locations.glyphSize, this.glyphWidth, this.glyphHeight);
        gl.uniform1f(this.locations.atlasColumns, this.atlasColumns);

        this.bindTexture(0, this.glyphTexture, this.locations.glyphTexture);
        this.bindTexture(1, this.foregroundTexture, this.locations.foregroundTexture);
        this.bindTexture(2, this.backgroundTexture, this.locations.backgroundTexture);
        this.bindTexture(3, this.atlasTexture, this.locations.atlasTexture);

        for (const draw of draws) {
            this.drawRegion(pixelWidth, pixelHeight, draw);
        }
    }

    drawRegion(pixelWidth, pixelHeight, draw) {
        const gl = this.gl;
        gl.uniform2f(this.locations.sourceOrigin, draw.source.left, draw.source.top);
        gl.uniform2f(this.locations.sourceSize, draw.source.width, draw.source.height);
        gl.uniform2f(this.locations.targetOrigin, draw.target.left, draw.target.top);
        gl.uniform2f(this.locations.targetSize, draw.target.width, draw.target.height);

        const data = this.vertexData;
        const left = draw.target.left;
        const top = draw.target.top;
        const right = left + draw.target.width;
        const bottom = top + draw.target.height;
        data[0] = left;
        data[1] = top;
        data[2] = right;
        data[3] = top;
        data[4] = left;
        data[5] = bottom;
        data[6] = left;
        data[7] = bottom;
        data[8] = right;
        data[9] = top;
        data[10] = right;
        data[11] = bottom;

        gl.bindBuffer(gl.ARRAY_BUFFER, this.vertexBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, data, gl.STREAM_DRAW);
        gl.enableVertexAttribArray(this.locations.position);
        gl.vertexAttribPointer(this.locations.position, 2, gl.FLOAT, false, 0, 0);
        gl.drawArrays(gl.TRIANGLES, 0, 6);
    }

    bindTexture(unit, texture, uniform) {
        const gl = this.gl;
        gl.activeTexture(gl.TEXTURE0 + unit);
        gl.bindTexture(gl.TEXTURE_2D, texture);
        gl.uniform1i(uniform, unit);
    }

    parseColorBytes(color) {
        let parsed = this.colorCache.get(color);
        if (parsed) return parsed;

        const rgba = parseFrameColor(color);
        parsed = [
            Math.round(rgba[0] * 255),
            Math.round(rgba[1] * 255),
            Math.round(rgba[2] * 255)
        ];
        this.colorCache.set(color, parsed);
        return parsed;
    }
}

class WebGlConsoleRenderer {
    constructor(canvas, state) {
        const gl = canvas.getContext("webgl", {
            alpha: false,
            antialias: false,
            depth: false,
            preserveDrawingBuffer: false,
            stencil: false
        });

        if (!gl) {
            throw new Error("WebGL is not available.");
        }

        canvas.dataset.klooieRenderer = "webgl-retained";
        this.gl = gl;
        this.solidProgram = createProgram(gl, solidVertexShaderSource, solidFragmentShaderSource);
        this.textProgram = createProgram(gl, textVertexShaderSource, textFragmentShaderSource);
        this.screenProgram = createProgram(gl, screenVertexShaderSource, screenFragmentShaderSource);
        this.solidBuffer = gl.createBuffer();
        this.textBuffer = gl.createBuffer();
        this.screenBuffer = gl.createBuffer();
        this.atlasTexture = gl.createTexture();
        this.frameTexture = gl.createTexture();
        this.frameBuffer = gl.createFramebuffer();
        this.colorCache = new Map();
        this.glyphs = new Map();
        this.maxTextureSize = gl.getParameter(gl.MAX_TEXTURE_SIZE) || 4096;
        this.atlasSize = 2048;
        this.atlasPadding = 2;
        this.atlasX = 0;
        this.atlasY = 0;
        this.atlasRowHeight = 0;
        this.atlasDirty = false;
        this.metricsKey = "";
        this.renderScale = 1;
        this.renderTargetWidth = 0;
        this.renderTargetHeight = 0;
        this.solidVertexCount = 0;
        this.textVertexCount = 0;
        this.solidData = new Float32Array(0);
        this.textData = new Float32Array(0);
        this.blackFrameChecks = 0;
        this.blackFrameReported = false;
        this.atlasCanvas = createRasterCanvas(this.atlasSize, this.atlasSize);
        this.atlasContext = this.atlasCanvas.getContext("2d", { alpha: true });

        if (!this.atlasContext) {
            throw new Error("A raster canvas is required to build the glyph atlas.");
        }

        gl.disable(gl.DEPTH_TEST);
        gl.disable(gl.CULL_FACE);
        gl.enable(gl.BLEND);
        gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);

        this.solidLocations = {
            position: gl.getAttribLocation(this.solidProgram, "a_position"),
            color: gl.getAttribLocation(this.solidProgram, "a_color"),
            resolution: gl.getUniformLocation(this.solidProgram, "u_resolution")
        };

        this.textLocations = {
            position: gl.getAttribLocation(this.textProgram, "a_position"),
            texCoord: gl.getAttribLocation(this.textProgram, "a_texCoord"),
            color: gl.getAttribLocation(this.textProgram, "a_color"),
            resolution: gl.getUniformLocation(this.textProgram, "u_resolution"),
            atlas: gl.getUniformLocation(this.textProgram, "u_atlas")
        };

        this.screenLocations = {
            position: gl.getAttribLocation(this.screenProgram, "a_position"),
            texCoord: gl.getAttribLocation(this.screenProgram, "a_texCoord"),
            resolution: gl.getUniformLocation(this.screenProgram, "u_resolution"),
            frame: gl.getUniformLocation(this.screenProgram, "u_frame")
        };

        this.resetAtlas(state);
    }

    invalidateMetrics() {
        this.metricsKey = "";
    }

    dispose() {
        const gl = this.gl;
        gl.deleteBuffer(this.solidBuffer);
        gl.deleteBuffer(this.textBuffer);
        gl.deleteBuffer(this.screenBuffer);
        gl.deleteTexture(this.atlasTexture);
        gl.deleteTexture(this.frameTexture);
        gl.deleteFramebuffer(this.frameBuffer);
        gl.deleteProgram(this.solidProgram);
        gl.deleteProgram(this.textProgram);
        gl.deleteProgram(this.screenProgram);
    }

    render(canvas, frame, state) {
        const cssWidth = frame.width * state.cellWidth;
        const cssHeight = frame.height * state.cellHeight;
        const displayPixelWidth = Math.max(1, Math.ceil(cssWidth * state.devicePixelRatio));
        const displayPixelHeight = Math.max(1, Math.ceil(cssHeight * state.devicePixelRatio));

        if (canvas.width !== displayPixelWidth || canvas.height !== displayPixelHeight) {
            canvas.width = displayPixelWidth;
            canvas.height = displayPixelHeight;
        }

        const resized = this.ensureRenderTarget(cssWidth, cssHeight, state.devicePixelRatio);
        this.ensureMetrics(state);

        const gl = this.gl;
        gl.bindFramebuffer(gl.FRAMEBUFFER, this.frameBuffer);
        gl.viewport(0, 0, this.renderTargetWidth, this.renderTargetHeight);
        gl.enable(gl.BLEND);

        if (resized || frame.full) {
            gl.clearColor(0, 0, 0, 1);
            gl.clear(gl.COLOR_BUFFER_BIT);
        }

        if (frame.full || frame.text.length > 0) {
            this.buildGeometry(frame, state);
            this.uploadAtlasIfNeeded();
            this.drawSolid(this.renderTargetWidth, this.renderTargetHeight);
            this.drawText(this.renderTargetWidth, this.renderTargetHeight);
        }

        this.blitToCanvas(displayPixelWidth, displayPixelHeight, frame, state);
        if (!this.blackFrameReported && this.blackFrameChecks < 3 && frame.text.length > 0) {
            this.blackFrameChecks++;
            this.blackFrameReported = !verifyWebGlFrame(this.gl, displayPixelWidth, displayPixelHeight, "webgl-retained", frame, state);
        }
    }

    ensureMetrics(state) {
        const scale = this.renderScale || 1;
        const glyphWidth = Math.max(1, Math.ceil(state.cellWidth * scale));
        const glyphHeight = Math.max(1, Math.ceil(state.cellHeight * scale));
        const key = `${glyphWidth}x${glyphHeight}:${scale}:${state.font}`;
        if (key === this.metricsKey) return;

        this.metricsKey = key;
        this.glyphWidth = glyphWidth;
        this.glyphHeight = glyphHeight;
        this.atlasCellWidth = glyphWidth + this.atlasPadding * 2;
        this.atlasCellHeight = glyphHeight + this.atlasPadding * 2;
        this.scaledFont = scaleFont(state.font, scale);
        this.resetAtlas(state);
    }

    ensureRenderTarget(cssWidth, cssHeight, devicePixelRatio) {
        const scale = Math.min(
            devicePixelRatio || 1,
            this.maxTextureSize / Math.max(1, cssWidth),
            this.maxTextureSize / Math.max(1, cssHeight));
        this.renderScale = Math.max(1 / Math.max(1, cssWidth, cssHeight), scale);

        const width = Math.max(1, Math.ceil(cssWidth * this.renderScale));
        const height = Math.max(1, Math.ceil(cssHeight * this.renderScale));
        if (width === this.renderTargetWidth && height === this.renderTargetHeight) return false;

        this.renderTargetWidth = width;
        this.renderTargetHeight = height;

        const gl = this.gl;
        gl.bindTexture(gl.TEXTURE_2D, this.frameTexture);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, width, height, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
        gl.bindFramebuffer(gl.FRAMEBUFFER, this.frameBuffer);
        gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, this.frameTexture, 0);

        if (gl.checkFramebufferStatus(gl.FRAMEBUFFER) !== gl.FRAMEBUFFER_COMPLETE) {
            throw new Error(`Unable to allocate the retained terminal render target at ${width}x${height}.`);
        }

        return true;
    }

    resetAtlas(state) {
        this.glyphs.clear();
        this.atlasX = 0;
        this.atlasY = 0;
        this.atlasRowHeight = 0;
        this.atlasContext.setTransform(1, 0, 0, 1, 0, 0);
        this.atlasContext.clearRect(0, 0, this.atlasSize, this.atlasSize);
        this.atlasContext.fillStyle = "#fff";
        this.atlasContext.textBaseline = "top";
        this.atlasContext.font = this.scaledFont || scaleFont(state.font, state.devicePixelRatio || 1);

        const gl = this.gl;
        gl.bindTexture(gl.TEXTURE_2D, this.atlasTexture);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, this.atlasSize, this.atlasSize, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
        this.atlasDirty = true;
    }

    buildGeometry(frame, state) {
        const cellWidth = state.cellWidth * this.renderScale;
        const cellHeight = state.cellHeight * this.renderScale;
        const solid = [];
        const text = [];

        for (let runIndex = 0; runIndex < frame.text.length; runIndex++) {
            const runText = frame.text[runIndex];
            const left = frame.x[runIndex] * cellWidth;
            const top = frame.y[runIndex] * cellHeight;
            const background = this.parseColor(frame.background[runIndex]);
            appendSolidQuad(solid, left, top, runText.length * cellWidth, cellHeight, background);

            const foreground = this.parseColor(frame.foreground[runIndex]);
            for (let column = 0; column < runText.length; column++) {
                const glyph = runText[column];
                if (isBlankGlyph(glyph)) continue;

                const atlasGlyph = this.ensureGlyph(glyph, state);
                appendTextQuad(
                    text,
                    left + column * cellWidth,
                    top,
                    cellWidth,
                    cellHeight,
                    atlasGlyph,
                    foreground);
            }
        }

        this.solidData = toFloat32Array(solid, this.solidData);
        this.textData = toFloat32Array(text, this.textData);
        this.solidVertexCount = solid.length / 6;
        this.textVertexCount = text.length / 8;
    }

    ensureGlyph(glyph, state) {
        let atlasGlyph = this.glyphs.get(glyph);
        if (atlasGlyph) return atlasGlyph;

        if (this.atlasX + this.atlasCellWidth > this.atlasSize) {
            this.atlasX = 0;
            this.atlasY += this.atlasRowHeight;
            this.atlasRowHeight = 0;
        }

        if (this.atlasY + this.atlasCellHeight > this.atlasSize) {
            this.resetAtlas(state);
        }

        const x = this.atlasX;
        const y = this.atlasY;
        this.atlasContext.clearRect(x, y, this.atlasCellWidth, this.atlasCellHeight);
        this.atlasContext.font = this.scaledFont;
        this.atlasContext.fillStyle = "#fff";
        this.atlasContext.fillText(glyph, x + this.atlasPadding, y + this.atlasPadding);

        const u0 = (x + this.atlasPadding) / this.atlasSize;
        const v0 = (y + this.atlasPadding) / this.atlasSize;
        const u1 = (x + this.atlasPadding + this.glyphWidth) / this.atlasSize;
        const v1 = (y + this.atlasPadding + this.glyphHeight) / this.atlasSize;
        atlasGlyph = { u0, v0, u1, v1 };
        this.glyphs.set(glyph, atlasGlyph);
        this.atlasX += this.atlasCellWidth;
        this.atlasRowHeight = Math.max(this.atlasRowHeight, this.atlasCellHeight);
        this.atlasDirty = true;
        return atlasGlyph;
    }

    uploadAtlasIfNeeded() {
        if (!this.atlasDirty) return;

        const gl = this.gl;
        gl.bindTexture(gl.TEXTURE_2D, this.atlasTexture);
        gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, this.atlasCanvas);
        this.atlasDirty = false;
    }

    drawSolid(pixelWidth, pixelHeight) {
        if (this.solidVertexCount === 0) return;

        const gl = this.gl;
        gl.useProgram(this.solidProgram);
        gl.uniform2f(this.solidLocations.resolution, pixelWidth, pixelHeight);
        gl.bindBuffer(gl.ARRAY_BUFFER, this.solidBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, this.solidData.subarray(0, this.solidVertexCount * 6), gl.STREAM_DRAW);

        const stride = 6 * Float32Array.BYTES_PER_ELEMENT;
        gl.enableVertexAttribArray(this.solidLocations.position);
        gl.vertexAttribPointer(this.solidLocations.position, 2, gl.FLOAT, false, stride, 0);
        gl.enableVertexAttribArray(this.solidLocations.color);
        gl.vertexAttribPointer(this.solidLocations.color, 4, gl.FLOAT, false, stride, 2 * Float32Array.BYTES_PER_ELEMENT);
        gl.drawArrays(gl.TRIANGLES, 0, this.solidVertexCount);
    }

    drawText(pixelWidth, pixelHeight) {
        if (this.textVertexCount === 0) return;

        const gl = this.gl;
        gl.useProgram(this.textProgram);
        gl.uniform2f(this.textLocations.resolution, pixelWidth, pixelHeight);
        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, this.atlasTexture);
        gl.uniform1i(this.textLocations.atlas, 0);
        gl.bindBuffer(gl.ARRAY_BUFFER, this.textBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, this.textData.subarray(0, this.textVertexCount * 8), gl.STREAM_DRAW);

        const stride = 8 * Float32Array.BYTES_PER_ELEMENT;
        gl.enableVertexAttribArray(this.textLocations.position);
        gl.vertexAttribPointer(this.textLocations.position, 2, gl.FLOAT, false, stride, 0);
        gl.enableVertexAttribArray(this.textLocations.texCoord);
        gl.vertexAttribPointer(this.textLocations.texCoord, 2, gl.FLOAT, false, stride, 2 * Float32Array.BYTES_PER_ELEMENT);
        gl.enableVertexAttribArray(this.textLocations.color);
        gl.vertexAttribPointer(this.textLocations.color, 4, gl.FLOAT, false, stride, 4 * Float32Array.BYTES_PER_ELEMENT);
        gl.drawArrays(gl.TRIANGLES, 0, this.textVertexCount);
    }

    blitToCanvas(pixelWidth, pixelHeight, frame, state) {
        const gl = this.gl;
        gl.bindFramebuffer(gl.FRAMEBUFFER, null);
        gl.viewport(0, 0, pixelWidth, pixelHeight);
        gl.disable(gl.BLEND);
        gl.clearColor(0, 0, 0, 1);
        gl.clear(gl.COLOR_BUFFER_BIT);
        gl.useProgram(this.screenProgram);
        gl.uniform2f(this.screenLocations.resolution, pixelWidth, pixelHeight);
        gl.activeTexture(gl.TEXTURE0);
        gl.bindTexture(gl.TEXTURE_2D, this.frameTexture);
        gl.uniform1i(this.screenLocations.frame, 0);

        const vertices = [];
        const draws = buildPresentationDraws(frame, state, pixelWidth, pixelHeight);
        for (const draw of draws) {
            this.appendScreenRegion(vertices, draw, state);
        }
        const data = new Float32Array(vertices);

        gl.bindBuffer(gl.ARRAY_BUFFER, this.screenBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, data, gl.STREAM_DRAW);

        const stride = 4 * Float32Array.BYTES_PER_ELEMENT;
        gl.enableVertexAttribArray(this.screenLocations.position);
        gl.vertexAttribPointer(this.screenLocations.position, 2, gl.FLOAT, false, stride, 0);
        gl.enableVertexAttribArray(this.screenLocations.texCoord);
        gl.vertexAttribPointer(this.screenLocations.texCoord, 2, gl.FLOAT, false, stride, 2 * Float32Array.BYTES_PER_ELEMENT);
        gl.drawArrays(gl.TRIANGLES, 0, data.length / 4);
        gl.enable(gl.BLEND);
    }

    appendScreenRegion(vertices, draw, state) {
        const left = draw.target.left;
        const top = draw.target.top;
        const right = left + draw.target.width;
        const bottom = top + draw.target.height;
        const sourceLeft = draw.source.left * state.cellWidth * this.renderScale;
        const sourceTop = draw.source.top * state.cellHeight * this.renderScale;
        const sourceRight = sourceLeft + draw.source.width * state.cellWidth * this.renderScale;
        const sourceBottom = sourceTop + draw.source.height * state.cellHeight * this.renderScale;
        const u0 = sourceLeft / this.renderTargetWidth;
        const u1 = sourceRight / this.renderTargetWidth;
        const v0 = 1 - sourceTop / this.renderTargetHeight;
        const v1 = 1 - sourceBottom / this.renderTargetHeight;

        vertices.push(
            left, top, u0, v0,
            right, top, u1, v0,
            left, bottom, u0, v1,
            left, bottom, u0, v1,
            right, top, u1, v0,
            right, bottom, u1, v1);
    }

    parseColor(color) {
        let parsed = this.colorCache.get(color);
        if (parsed) return parsed;

        parsed = parseFrameColor(color);
        this.colorCache.set(color, parsed);
        return parsed;
    }
}

class Canvas2DConsoleRenderer {
    constructor(canvas) {
        canvas.dataset.klooieRenderer = "canvas2d";
        this.context = canvas.getContext("2d", { alpha: false });
        this.frameCanvas = createRasterCanvas(1, 1);
        this.frameContext = this.frameCanvas.getContext("2d", { alpha: false });
    }

    invalidateMetrics() {
    }

    dispose() {
    }

    render(canvas, frame, state) {
        const cssWidth = frame.width * state.cellWidth;
        const cssHeight = frame.height * state.cellHeight;
        const pixelWidth = Math.max(1, Math.ceil(cssWidth * state.devicePixelRatio));
        const pixelHeight = Math.max(1, Math.ceil(cssHeight * state.devicePixelRatio));

        if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
            canvas.width = pixelWidth;
            canvas.height = pixelHeight;
        }

        if (this.frameCanvas.width !== pixelWidth || this.frameCanvas.height !== pixelHeight) {
            this.frameCanvas.width = pixelWidth;
            this.frameCanvas.height = pixelHeight;
            frame = { ...frame, full: true };
        }

        this.frameContext.setTransform(state.devicePixelRatio, 0, 0, state.devicePixelRatio, 0, 0);
        this.frameContext.textBaseline = "top";
        this.frameContext.font = state.font;
        if (frame.full) {
            this.frameContext.fillStyle = "#000";
            this.frameContext.fillRect(0, 0, cssWidth, cssHeight);
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
            this.frameContext.fillStyle = frameColorToCss(background[i]);
            this.frameContext.fillRect(left, top, runText.length * state.cellWidth, state.cellHeight);
            this.frameContext.fillStyle = frameColorToCss(foreground[i]);
            this.frameContext.fillText(runText, left, top);
        }

        this.context.setTransform(1, 0, 0, 1, 0, 0);
        this.context.fillStyle = "#000";
        this.context.fillRect(0, 0, pixelWidth, pixelHeight);

        const draws = buildPresentationDraws(frame, state, pixelWidth, pixelHeight);
        for (const draw of draws) {
            this.context.drawImage(
                this.frameCanvas,
                draw.source.left * state.cellWidth * state.devicePixelRatio,
                draw.source.top * state.cellHeight * state.devicePixelRatio,
                draw.source.width * state.cellWidth * state.devicePixelRatio,
                draw.source.height * state.cellHeight * state.devicePixelRatio,
                draw.target.left,
                draw.target.top,
                draw.target.width,
                draw.target.height);
        }
    }
}

function createRasterCanvas(width, height) {
    if (typeof OffscreenCanvas !== "undefined") {
        return new OffscreenCanvas(width, height);
    }

    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    return canvas;
}

function createProgram(gl, vertexSource, fragmentSource) {
    const vertexShader = createShader(gl, gl.VERTEX_SHADER, vertexSource);
    const fragmentShader = createShader(gl, gl.FRAGMENT_SHADER, fragmentSource);
    const program = gl.createProgram();
    gl.attachShader(program, vertexShader);
    gl.attachShader(program, fragmentShader);
    gl.linkProgram(program);

    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
        const message = gl.getProgramInfoLog(program) || "Unknown shader program link error.";
        gl.deleteShader(vertexShader);
        gl.deleteShader(fragmentShader);
        gl.deleteProgram(program);
        throw new Error(message);
    }

    gl.deleteShader(vertexShader);
    gl.deleteShader(fragmentShader);
    return program;
}

function createShader(gl, type, source) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);

    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const message = gl.getShaderInfoLog(shader) || "Unknown shader compile error.";
        gl.deleteShader(shader);
        throw new Error(message);
    }

    return shader;
}

function appendSolidQuad(target, x, y, width, height, color) {
    const x2 = x + width;
    const y2 = y + height;
    appendSolidVertex(target, x, y, color);
    appendSolidVertex(target, x2, y, color);
    appendSolidVertex(target, x, y2, color);
    appendSolidVertex(target, x, y2, color);
    appendSolidVertex(target, x2, y, color);
    appendSolidVertex(target, x2, y2, color);
}

function appendSolidVertex(target, x, y, color) {
    target.push(x, y, color[0], color[1], color[2], color[3]);
}

function appendTextQuad(target, x, y, width, height, glyph, color) {
    const x2 = x + width;
    const y2 = y + height;
    appendTextVertex(target, x, y, glyph.u0, glyph.v0, color);
    appendTextVertex(target, x2, y, glyph.u1, glyph.v0, color);
    appendTextVertex(target, x, y2, glyph.u0, glyph.v1, color);
    appendTextVertex(target, x, y2, glyph.u0, glyph.v1, color);
    appendTextVertex(target, x2, y, glyph.u1, glyph.v0, color);
    appendTextVertex(target, x2, y2, glyph.u1, glyph.v1, color);
}

function appendTextVertex(target, x, y, u, v, color) {
    target.push(x, y, u, v, color[0], color[1], color[2], color[3]);
}

function toFloat32Array(source, existing) {
    if (existing.length >= source.length) {
        existing.set(source);
        return existing;
    }

    const nextLength = Math.max(source.length, existing.length * 2, 1024);
    const next = new Float32Array(nextLength);
    next.set(source);
    return next;
}

function parseFrameColor(color) {
    if (typeof color === "number") {
        return [
            ((color >> 16) & 0xff) / 255,
            ((color >> 8) & 0xff) / 255,
            (color & 0xff) / 255,
            1
        ];
    }

    if (typeof color === "string" && color.length === 7 && color[0] === "#") {
        return [
            parseInt(color.slice(1, 3), 16) / 255,
            parseInt(color.slice(3, 5), 16) / 255,
            parseInt(color.slice(5, 7), 16) / 255,
            1
        ];
    }

    return [0, 0, 0, 1];
}

function frameColorToCss(color) {
    if (typeof color === "number") {
        return `#${(color & 0xffffff).toString(16).padStart(6, "0")}`;
    }

    return color || "#000000";
}

function isBlankGlyph(glyph) {
    return glyph === " " || glyph === "\u00A0" || glyph === "\t" || glyph === "\r" || glyph === "\n";
}

function scaleFont(font, scale) {
    if (!font) return `${16 * scale}px Consolas, 'Cascadia Mono', 'Courier New', monospace`;
    return font.replace(/(\d+(?:\.\d+)?)px/g, (_, size) => `${Number(size) * scale}px`);
}

const cellVertexShaderSource = `#version 300 es
in vec2 a_position;
uniform vec2 u_resolution;

void main() {
    vec2 zeroToOne = a_position / u_resolution;
    vec2 zeroToTwo = zeroToOne * 2.0;
    vec2 clipSpace = zeroToTwo - 1.0;
    gl_Position = vec4(clipSpace * vec2(1.0, -1.0), 0.0, 1.0);
}`;

const cellFragmentShaderSource = `#version 300 es
precision highp float;
uniform vec2 u_resolution;
uniform vec2 u_gridSize;
uniform vec2 u_cellSize;
uniform sampler2D u_glyphs;
uniform sampler2D u_foreground;
uniform sampler2D u_background;
uniform sampler2D u_atlas;
uniform vec2 u_atlasSize;
uniform vec2 u_atlasCellSize;
uniform vec2 u_glyphSize;
uniform float u_atlasColumns;
uniform vec2 u_sourceOrigin;
uniform vec2 u_sourceSize;
uniform vec2 u_targetOrigin;
uniform vec2 u_targetSize;
out vec4 outColor;

void main() {
    vec2 pixel = vec2(gl_FragCoord.x, u_resolution.y - gl_FragCoord.y);
    vec2 local = (pixel - u_targetOrigin) / u_targetSize;
    if (local.x < 0.0 || local.y < 0.0 || local.x > 1.0 || local.y > 1.0) {
        discard;
    }

    vec2 sourcePixel = (u_sourceOrigin + local * u_sourceSize) * u_cellSize;
    vec2 cell = floor(sourcePixel / u_cellSize);
    if (cell.x < 0.0 || cell.y < 0.0 || cell.x >= u_gridSize.x || cell.y >= u_gridSize.y) {
        outColor = vec4(0.0, 0.0, 0.0, 1.0);
        return;
    }

    vec2 cellUv = (cell + vec2(0.5)) / u_gridSize;
    vec4 background = texture(u_background, cellUv);
    vec4 encodedGlyph = texture(u_glyphs, cellUv);
    float glyphIndex = floor(encodedGlyph.r * 255.0 + 0.5) + floor(encodedGlyph.g * 255.0 + 0.5) * 256.0;
    if (glyphIndex < 0.5) {
        outColor = vec4(background.rgb, 1.0);
        return;
    }

    float atlasColumn = mod(glyphIndex, u_atlasColumns);
    float atlasRow = floor(glyphIndex / u_atlasColumns);
    vec2 cellLocal = fract(sourcePixel / u_cellSize);
    vec2 atlasPixel = vec2(atlasColumn, atlasRow) * u_atlasCellSize + vec2(2.0, 2.0) + cellLocal * u_glyphSize;
    float glyphAlpha = texture(u_atlas, atlasPixel / u_atlasSize).a;
    vec4 foreground = texture(u_foreground, cellUv);
    outColor = vec4(mix(background.rgb, foreground.rgb, glyphAlpha), 1.0);
}`;

const solidVertexShaderSource = `
attribute vec2 a_position;
attribute vec4 a_color;
uniform vec2 u_resolution;
varying vec4 v_color;

void main() {
    vec2 zeroToOne = a_position / u_resolution;
    vec2 zeroToTwo = zeroToOne * 2.0;
    vec2 clipSpace = zeroToTwo - 1.0;
    gl_Position = vec4(clipSpace * vec2(1.0, -1.0), 0.0, 1.0);
    v_color = a_color;
}`;

const solidFragmentShaderSource = `
precision mediump float;
varying vec4 v_color;

void main() {
    gl_FragColor = v_color;
}`;

const textVertexShaderSource = `
attribute vec2 a_position;
attribute vec2 a_texCoord;
attribute vec4 a_color;
uniform vec2 u_resolution;
varying vec2 v_texCoord;
varying vec4 v_color;

void main() {
    vec2 zeroToOne = a_position / u_resolution;
    vec2 zeroToTwo = zeroToOne * 2.0;
    vec2 clipSpace = zeroToTwo - 1.0;
    gl_Position = vec4(clipSpace * vec2(1.0, -1.0), 0.0, 1.0);
    v_texCoord = a_texCoord;
    v_color = a_color;
}`;

const textFragmentShaderSource = `
precision mediump float;
uniform sampler2D u_atlas;
varying vec2 v_texCoord;
varying vec4 v_color;

void main() {
    float alpha = texture2D(u_atlas, v_texCoord).a;
    gl_FragColor = vec4(v_color.rgb, v_color.a * alpha);
}`;

const screenVertexShaderSource = `
attribute vec2 a_position;
attribute vec2 a_texCoord;
uniform vec2 u_resolution;
varying vec2 v_texCoord;

void main() {
    vec2 zeroToOne = a_position / u_resolution;
    vec2 zeroToTwo = zeroToOne * 2.0;
    vec2 clipSpace = zeroToTwo - 1.0;
    gl_Position = vec4(clipSpace * vec2(1.0, -1.0), 0.0, 1.0);
    v_texCoord = a_texCoord;
}`;

const screenFragmentShaderSource = `
precision mediump float;
uniform sampler2D u_frame;
varying vec2 v_texCoord;

void main() {
    gl_FragColor = texture2D(u_frame, v_texCoord);
}`;
