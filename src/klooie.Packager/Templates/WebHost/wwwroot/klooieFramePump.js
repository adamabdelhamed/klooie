window.klooieFramePump = {
    nextId: 1,
    pumps: {},
    start(dotNetRef, hostElement) {
        const id = this.nextId++;
        const canvas = hostElement.querySelector("canvas");
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
            listeners: []
        };
        this.pumps[id] = state;
        setupKeyboard(hostElement, state);
        setupGamepads(state);
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
        } catch (error) {
            console.debug("klooie audio play skipped", error);
        }
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
        const terminalFrame = await dotNetRef.invokeMethodAsync("Tick", size.width, size.height, elapsed, keys, gamepadSnapshotJson);
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

        return JSON.stringify({ gamepads });
    } catch (error) {
        console.debug("klooie gamepad snapshot skipped", error);
        return null;
    }
}

function normalizeAxis(value) {
    const numeric = Number(value);
    return Math.max(-1, Math.min(1, Number.isFinite(numeric) ? numeric : 0));
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
    state.cellWidth = Math.max(1, rect?.width || 8);
    state.cellHeight = Math.max(1, rect?.height || 16);
    state.devicePixelRatio = window.devicePixelRatio || 1;
    state.font = style.font || "16px Consolas, 'Cascadia Mono', 'Courier New', monospace";
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
            atlasColumns: gl.getUniformLocation(this.program, "u_atlasColumns")
        };

        this.configureCellTexture(this.glyphTexture);
        this.configureCellTexture(this.foregroundTexture);
        this.configureCellTexture(this.backgroundTexture);
        this.configureAtlasTexture();
        this.ensureMetrics(state);
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

        this.ensureMetrics(state);
        const resized = this.ensureCellTextures(frame.width, frame.height);
        const uploadAll = this.applyFrame(frame, resized);
        this.uploadCellTextures(uploadAll);
        this.draw(pixelWidth, pixelHeight, state);
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

    ensureMetrics(state) {
        const dpr = state.devicePixelRatio || 1;
        const glyphWidth = Math.max(1, Math.ceil(state.cellWidth * dpr));
        const glyphHeight = Math.max(1, Math.ceil(state.cellHeight * dpr));
        const key = `${glyphWidth}x${glyphHeight}:${dpr}:${state.font}`;
        if (key === this.metricsKey) return;

        this.metricsKey = key;
        this.glyphWidth = glyphWidth;
        this.glyphHeight = glyphHeight;
        this.atlasCellWidth = glyphWidth + this.atlasPadding * 2;
        this.atlasCellHeight = glyphHeight + this.atlasPadding * 2;
        this.atlasColumns = Math.max(1, Math.floor(this.atlasSize / this.atlasCellWidth));
        this.scaledFont = scaleFont(state.font, dpr);
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

    draw(pixelWidth, pixelHeight, state) {
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

        const data = this.vertexData;
        data[0] = 0;
        data[1] = 0;
        data[2] = pixelWidth;
        data[3] = 0;
        data[4] = 0;
        data[5] = pixelHeight;
        data[6] = 0;
        data[7] = pixelHeight;
        data[8] = pixelWidth;
        data[9] = 0;
        data[10] = pixelWidth;
        data[11] = pixelHeight;

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

        this.blitToCanvas(displayPixelWidth, displayPixelHeight);
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

    blitToCanvas(pixelWidth, pixelHeight) {
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

        const data = new Float32Array([
            0, 0, 0, 1,
            pixelWidth, 0, 1, 1,
            0, pixelHeight, 0, 0,
            0, pixelHeight, 0, 0,
            pixelWidth, 0, 1, 1,
            pixelWidth, pixelHeight, 1, 0
        ]);

        gl.bindBuffer(gl.ARRAY_BUFFER, this.screenBuffer);
        gl.bufferData(gl.ARRAY_BUFFER, data, gl.STREAM_DRAW);

        const stride = 4 * Float32Array.BYTES_PER_ELEMENT;
        gl.enableVertexAttribArray(this.screenLocations.position);
        gl.vertexAttribPointer(this.screenLocations.position, 2, gl.FLOAT, false, stride, 0);
        gl.enableVertexAttribArray(this.screenLocations.texCoord);
        gl.vertexAttribPointer(this.screenLocations.texCoord, 2, gl.FLOAT, false, stride, 2 * Float32Array.BYTES_PER_ELEMENT);
        gl.drawArrays(gl.TRIANGLES, 0, 6);
        gl.enable(gl.BLEND);
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
    }

    invalidateMetrics() {
    }

    dispose() {
    }

    render(canvas, frame, state) {
        if (!frame.full && frame.text.length === 0) return;

        const cssWidth = frame.width * state.cellWidth;
        const cssHeight = frame.height * state.cellHeight;
        const pixelWidth = Math.max(1, Math.ceil(cssWidth * state.devicePixelRatio));
        const pixelHeight = Math.max(1, Math.ceil(cssHeight * state.devicePixelRatio));

        if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
            canvas.width = pixelWidth;
            canvas.height = pixelHeight;
        }

        this.context.setTransform(state.devicePixelRatio, 0, 0, state.devicePixelRatio, 0, 0);
        this.context.textBaseline = "top";
        this.context.font = state.font;
        if (frame.full) {
            this.context.fillStyle = "#000";
            this.context.fillRect(0, 0, cssWidth, cssHeight);
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
            this.context.fillStyle = frameColorToCss(background[i]);
            this.context.fillRect(left, top, runText.length * state.cellWidth, state.cellHeight);
            this.context.fillStyle = frameColorToCss(foreground[i]);
            this.context.fillText(runText, left, top);
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
out vec4 outColor;

void main() {
    vec2 pixel = vec2(gl_FragCoord.x, u_resolution.y - gl_FragCoord.y);
    vec2 cell = floor(pixel / u_cellSize);
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
    vec2 cellLocal = fract(pixel / u_cellSize);
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
