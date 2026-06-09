window.klooiePwa = window.klooiePwa || {
    deferredInstallPrompt: undefined,
    installPromptEnabled: false,
    installed: window.matchMedia?.("(display-mode: fullscreen)")?.matches || window.matchMedia?.("(display-mode: standalone)")?.matches || navigator.standalone === true
};

window.klooieLifecycle = window.klooieLifecycle || (() => {
    const layerZIndex = {
        klooie: 0,
        zoom: 30,
        touchController: 40,
        encourage: 50,
        loading: 3000,
        stopped: 3000,
        temporaryOverlay: 4000
    };

    const state = {
        loadingElement: undefined,
        stoppedElement: undefined,
        loadingShown: false,
        loadingReady: false,
        loadingDismissed: false,
        loadingDismissListeners: new Set(),
        stoppedShown: false,
        overlayElement: undefined,
        overlayActive: false,
        overlayGamepad: createOverlayGamepadNavigator(),
        overlayInputAnimationId: undefined,
        postOverlayGamepadReleasePending: false,
        options: normalizeLifecycleOptions(window.klooieLifecycleOptions)
    };

    function normalizeLifecycleOptions(options) {
        options = options || {};
        return {
            loadingHtmlPath: options.loadingHtmlPath || options.LoadingHtmlPath || undefined,
            stoppedHtmlPath: options.stoppedHtmlPath || options.StoppedHtmlPath || undefined
        };
    }

    function configure(options) {
        state.options = { ...state.options, ...normalizeLifecycleOptions(options) };
        if (!state.loadingDismissed) showLoading();
    }

    function showLoading() {
        if (state.loadingShown) return;
        state.loadingShown = true;
        const host = ensureOverlay("klooie-lifecycle-loading");
        state.loadingElement = host;
        prepareOverlayHost(host);
        startOverlayInputPump();
        loadLifecycleHtml(state.options.loadingHtmlPath, getDefaultLoadingHtml())
            .then(html => {
                if (state.loadingDismissed) return;
                mountLifecycleHtml(host, html);
                state.overlayGamepad.reset(host);
            });
    }

    function showStopped() {
        if (state.stoppedShown) return;
        state.stoppedShown = true;
        dismissLoading();
        const host = ensureOverlay("klooie-lifecycle-stopped");
        state.stoppedElement = host;
        prepareOverlayHost(host);
        startOverlayInputPump();
        loadLifecycleHtml(state.options.stoppedHtmlPath, getDefaultStoppedHtml())
            .then(html => {
                mountLifecycleHtml(host, html);
                wireRefresh(host);
                state.overlayGamepad.reset(host);
                window.klooieStoppedScreen?.show?.();
            });
    }

    function showOverlay(command) {
        command = command || {};
        dismissOverlay();
        const host = ensureOverlay("klooie-lifecycle-overlay");
        host.style.zIndex = String(layerZIndex.temporaryOverlay);
        host.style.background = "transparent";
        state.overlayElement = host;
        state.overlayActive = true;
        prepareOverlayHost(host);
        startOverlayInputPump();

        const dismiss = () => dismissOverlay();
        const handled = window.klooieCustomOverlay?.show?.(host, command, dismiss);
        if (handled) {
            state.overlayGamepad.reset(host);
            return;
        }

        mountDefaultOverlay(host, command, dismiss);
        state.overlayGamepad.reset(host);
    }

    function dismissOverlay() {
        const element = state.overlayElement || document.getElementById("klooie-lifecycle-overlay");
        state.overlayElement = undefined;
        state.overlayActive = false;
        state.overlayGamepad.reset();
        state.postOverlayGamepadReleasePending = true;
        element?.remove();
    }

    function isOverlayActive() {
        return state.overlayActive || isLifecycleOverlayVisible();
    }

    function isLifecycleOverlayVisible() {
        return (state.loadingDismissed == false && isElementVisible(state.loadingElement || document.getElementById("klooie-lifecycle-loading"))) ||
            isElementVisible(state.stoppedElement || document.getElementById("klooie-lifecycle-stopped"));
    }

    function pumpGamepadNavigation(timestamp, gamepads) {
        const host = getActiveOverlayHost();
        if (!host) return;
        state.overlayGamepad.pump(host, timestamp, gamepads);
    }

    function shouldBlockAppInput(gamepads) {
        if (isOverlayActive()) return true;
        if (state.postOverlayGamepadReleasePending == false) return false;
        if (hasHeldOverlayActionButton(gamepads)) return true;

        state.postOverlayGamepadReleasePending = false;
        return false;
    }

    function markReady(message) {
        if (state.loadingReady || state.loadingDismissed) return;

        state.loadingReady = true;
        const loader = window.klooieLoader;
        if (loader?.ready) {
            loader.ready(message);
            return;
        }

        dismissLoading();
    }

    function dismissLoading() {
        if (state.loadingDismissed) return;
        state.loadingDismissed = true;
        state.loadingShown = false;
        const loader = window.klooieLoader;
        if (loader?.hide) {
            loader.hide();
            state.overlayGamepad.reset();
            state.postOverlayGamepadReleasePending = true;
            notifyLoadingDismissed();
            return;
        }

        removeOverlay(state.loadingElement || document.getElementById("klooie-lifecycle-loading"));
        state.overlayGamepad.reset();
        state.postOverlayGamepadReleasePending = true;
        notifyLoadingDismissed();
    }

    function notifyLoadingDismissed() {
        for (const listener of Array.from(state.loadingDismissListeners)) {
            listener();
        }
    }

    function isLoadingDismissed() {
        return state.loadingDismissed;
    }

    function afterLoadingDismissed(listener) {
        if (typeof listener !== "function") return () => {};
        if (state.loadingDismissed) {
            listener();
            return () => {};
        }

        state.loadingDismissListeners.add(listener);
        return () => state.loadingDismissListeners.delete(listener);
    }

    async function requestFullscreenAndLandscape() {
        await requestFullscreen(document.documentElement);
        try {
            if (screen.orientation?.lock) await screen.orientation.lock("landscape");
        } catch {
        }
    }

    function ensureOverlay(id) {
        let element = document.getElementById(id);
        if (!element) {
            element = document.createElement("div");
            element.id = id;
            document.body.appendChild(element);
        }

        element.style.position = "fixed";
        element.style.inset = "0";
        element.style.zIndex = String(id.endsWith("stopped") ? layerZIndex.stopped : layerZIndex.loading);
        element.style.background = "#000";
        return element;
    }

    function prepareOverlayHost(host) {
        if (!host) return;
        ensureOverlayFocusStyle();
        host.classList.add("klooie-lifecycle-overlay-host");
        host.addEventListener("focusin", syncOverlayFocusAttribute);
        host.addEventListener("focusout", syncOverlayFocusAttribute);
        host.addEventListener("keydown", handleOverlayKeyDown, true);
    }

    function getActiveOverlayHost() {
        const commandOverlay = state.overlayActive ? state.overlayElement || document.getElementById("klooie-lifecycle-overlay") : undefined;
        if (isElementVisible(commandOverlay)) return commandOverlay;

        const loading = state.loadingDismissed == false ? state.loadingElement || document.getElementById("klooie-lifecycle-loading") : undefined;
        if (isElementVisible(loading)) return loading;

        const stopped = state.stoppedElement || document.getElementById("klooie-lifecycle-stopped");
        if (isElementVisible(stopped)) return stopped;

        return undefined;
    }

    function startOverlayInputPump() {
        if (state.overlayInputAnimationId !== undefined) return;

        const frame = timestamp => {
            if (isOverlayActive()) {
                pumpGamepadNavigation(timestamp, readConnectedBrowserGamepads());
                state.overlayInputAnimationId = requestAnimationFrame(frame);
                return;
            }

            state.overlayInputAnimationId = undefined;
            state.overlayGamepad.reset();
        };

        state.overlayInputAnimationId = requestAnimationFrame(frame);
    }

    function removeOverlay(element) {
        if (!element) return;
        element.style.opacity = "0";
        element.style.pointerEvents = "none";
        element.style.transition = "opacity 220ms ease";
        window.setTimeout(() => element.remove(), 240);
    }

    function ensureOverlayFocusStyle() {
        if (document.getElementById("klooie-lifecycle-overlay-focus-style")) return;

        const style = document.createElement("style");
        style.id = "klooie-lifecycle-overlay-focus-style";
        style.textContent = `
.klooie-lifecycle-overlay-host :where(a[href],button,input,select,textarea,[tabindex]):focus,
.klooie-lifecycle-overlay-host :where(a[href],button,input,select,textarea,[tabindex])[data-klooie-overlay-focused="true"]{
  outline:3px solid var(--klooie-overlay-focus-color,#fff5a8) !important;
  outline-offset:4px !important;
  box-shadow:0 0 0 2px rgba(0,0,0,.9),0 0 18px var(--klooie-overlay-focus-glow,rgba(255,245,168,.72)) !important;
}
.klooie-lifecycle-overlay-host :where(a[href],button,input,select,textarea,[tabindex])[data-klooie-overlay-focused="true"]{
  filter:brightness(1.14);
}`;
        document.head.appendChild(style);
    }

    function syncOverlayFocusAttribute(event) {
        const host = event.currentTarget;
        if (!host) return;
        for (const element of host.querySelectorAll("[data-klooie-overlay-focused]")) {
            element.removeAttribute("data-klooie-overlay-focused");
        }

        const current = document.activeElement;
        if (current && host.contains(current) && isOverlayFocusable(current)) {
            current.setAttribute("data-klooie-overlay-focused", "true");
        }
    }

    function handleOverlayKeyDown(event) {
        if (event.key !== "Enter" && event.key !== " ") return;
        const host = getActiveOverlayHost();
        if (!host || !host.contains(event.target)) return;
        event.preventDefault();
        event.stopPropagation();
        const current = document.activeElement;
        activateOverlayControl(current && host.contains(current) ? current : event.target);
    }

    function isElementVisible(element) {
        if (!element || !document.body.contains(element)) return false;
        const style = window.getComputedStyle(element);
        if (style.display === "none" || style.visibility === "hidden" || style.pointerEvents === "none") return false;
        const rect = element.getBoundingClientRect();
        return rect.width > 0 && rect.height > 0;
    }

    async function loadLifecycleHtml(path, fallback) {
        if (!path) return fallback;
        try {
            const response = await fetch(cacheBustUrl(path), { cache: "no-store" });
            if (!response.ok) return fallback;
            return await response.text();
        } catch {
            return fallback;
        }
    }

    function cacheBustUrl(path) {
        const url = new URL(path, document.baseURI);
        url.searchParams.set("v", window.klooieBuildId || String(Date.now()));
        return url.toString();
    }

    function mountLifecycleHtml(host, html) {
        host.replaceChildren();
        window.klooieLoader = undefined;
        const template = document.createElement("template");
        template.innerHTML = extractBodyHtml(html);
        host.appendChild(template.content.cloneNode(true));
        for (const script of Array.from(host.querySelectorAll("script"))) {
            const replacement = document.createElement("script");
            for (const attribute of Array.from(script.attributes)) replacement.setAttribute(attribute.name, attribute.value);
            replacement.textContent = script.textContent;
            script.replaceWith(replacement);
        }
        wireRefresh(host);
    }

    function mountDefaultOverlay(host, command, dismiss) {
        host.replaceChildren();
        if ((command.id || command.Id) === "claws-settings-cleared") {
            mountSettingsClearedOverlay(host, command, dismiss);
            return;
        }

        const title = command.title || command.Title || "Feature unavailable";
        const message = command.message || command.Message || "This feature is not available here.";
        const steamUrl = command.steamUrl || command.SteamUrl || "https://store.steampowered.com/";
        const root = document.createElement("div");
        root.style.cssText = "position:fixed;inset:0;display:flex;align-items:center;justify-content:center;padding:24px;background:rgba(0,0,0,.88);color:white;font-family:Consolas,Menlo,Monaco,'Courier New',monospace;user-select:none;touch-action:none";
        root.innerHTML = `
<div style="max-width:680px;text-align:center;border:1px solid rgba(255,255,255,.2);padding:28px;background:#050505">
  <h1 style="margin:0 0 18px;font-size:28px">${escapeHtml(title)}</h1>
  <p style="margin:0 0 24px;line-height:1.6;color:rgba(255,255,255,.78)">${escapeHtml(message)}</p>
  <a href="${escapeAttribute(steamUrl)}" target="_blank" rel="noopener" style="display:inline-block;margin:0 8px 12px;padding:12px 18px;color:white;background:#1b5cff;text-decoration:none">View on Steam</a>
  <button type="button" data-klooie-overlay-dismiss style="display:inline-block;margin:0 8px 12px;padding:12px 18px;color:white;background:#333;border:0;cursor:pointer">Back to Demo</button>
</div>`;
        root.querySelector("button")?.addEventListener("click", dismiss);
        host.appendChild(root);
    }

    function mountSettingsClearedOverlay(host, command, dismiss) {
        const title = command.title || command.Title || "Settings cleared";
        const message = command.message || command.Message || "Saved game state has been cleared.";
        const root = document.createElement("div");
        root.style.cssText = "position:fixed;inset:0;display:flex;align-items:center;justify-content:center;padding:24px;background:rgba(0,0,0,.88);color:white;font-family:Consolas,Menlo,Monaco,'Courier New',monospace;user-select:none;touch-action:none";
        root.innerHTML = `
<div style="max-width:680px;text-align:center;border:1px solid rgba(255,255,255,.2);padding:28px;background:#050505">
  <h1 style="margin:0 0 18px;font-size:28px">${escapeHtml(title)}</h1>
  <p style="margin:0 0 24px;line-height:1.6;color:rgba(255,255,255,.78)">${escapeHtml(message)}</p>
  <button type="button" style="display:inline-block;margin:0 8px 12px;padding:12px 18px;color:white;background:#1b5cff;border:0;cursor:pointer">Reset</button>
  <button type="button" data-klooie-overlay-dismiss style="display:inline-block;margin:0 8px 12px;padding:12px 18px;color:white;background:#333;border:0;cursor:pointer">Close</button>
</div>`;
        root.querySelector("button")?.addEventListener("click", () => location.reload());
        root.querySelector("[data-klooie-overlay-dismiss]")?.addEventListener("click", dismiss);
        host.appendChild(root);
    }

    function escapeHtml(value) {
        return String(value ?? "").replace(/[&<>"']/g, ch => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[ch]));
    }

    function escapeAttribute(value) {
        return escapeHtml(value).replace(/`/g, "&#96;");
    }

    function extractBodyHtml(html) {
        const bodyMatch = /<body[^>]*>([\s\S]*?)<\/body>/i.exec(html || "");
        return bodyMatch ? bodyMatch[1] : html;
    }

    function wireRefresh(host) {
        for (const element of host.querySelectorAll("[data-klooie-refresh]")) {
            element.addEventListener("click", () => location.reload());
        }
    }

    function getDefaultLoadingHtml() {
        return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
<title>Loading</title>
</head>
<body style="margin:0;background:#000;overflow:hidden">
<div id="klooie-loader" style="
  position:fixed;
  inset:0;
  width:100vw;
  height:100dvh;
  min-height:100vh;
  background:#000;
  color:#d7fff5;
  overflow:hidden;
  display:flex;
  align-items:center;
  justify-content:center;
  font-family:Consolas,Menlo,Monaco,'Courier New',monospace;
  user-select:none;
  -webkit-user-select:none;
  touch-action:none;
">
  <style>
    #klooie-loader, #klooie-loader * { box-sizing:border-box; }
    #klooie-loader .shell {
      position:relative;
      width:min(92vw,680px);
      height:min(72dvh,420px);
      min-height:260px;
      display:flex;
      flex-direction:column;
      align-items:center;
      justify-content:center;
      gap:22px;
      border:1px solid rgba(120,255,220,.24);
      border-radius:24px;
      background:
        radial-gradient(circle at 50% 40%, rgba(0,255,190,.11), transparent 42%),
        linear-gradient(180deg, rgba(255,255,255,.045), rgba(255,255,255,.01));
      box-shadow:0 0 44px rgba(0,255,190,.12), inset 0 0 40px rgba(0,255,190,.045);
      overflow:hidden;
    }

    #klooie-loader .shell:before {
      content:"";
      position:absolute;
      inset:-80px;
      background:
        linear-gradient(rgba(80,255,210,.055) 1px, transparent 1px),
        linear-gradient(90deg, rgba(80,255,210,.055) 1px, transparent 1px);
      background-size:28px 28px;
      transform:perspective(500px) rotateX(58deg) translateY(20px);
      animation:klooie-grid 2.8s linear infinite;
    }

    #klooie-loader .core {
      position:relative;
      width:116px;
      height:116px;
      border-radius:50%;
      display:grid;
      place-items:center;
      filter:drop-shadow(0 0 18px rgba(60,255,215,.45));
    }

    #klooie-loader .core:before,
    #klooie-loader .core:after {
      content:"";
      position:absolute;
      inset:0;
      border-radius:50%;
      border:2px solid transparent;
    }

    #klooie-loader .core:before {
      border-top-color:#a8fff0;
      border-right-color:rgba(168,255,240,.42);
      animation:klooie-spin 1.2s linear infinite;
    }

    #klooie-loader .core:after {
      inset:16px;
      border-bottom-color:#59ffd7;
      border-left-color:rgba(89,255,215,.35);
      animation:klooie-spin 1.7s linear infinite reverse;
    }

    #klooie-loader .glyph {
      position:relative;
      font-size:42px;
      font-weight:800;
      letter-spacing:-4px;
      color:#eafffb;
      text-shadow:0 0 8px rgba(180,255,245,.85), 0 0 24px rgba(0,255,190,.55);
      animation:klooie-pulse 1.8s ease-in-out infinite;
    }

    #klooie-loader .title {
      position:relative;
      font-size:clamp(18px,4.2vw,30px);
      letter-spacing:.18em;
      text-transform:uppercase;
      color:#f1fffc;
      text-shadow:0 0 14px rgba(0,255,200,.45);
    }

    #klooie-loader .subtitle {
      position:relative;
      width:min(72%,420px);
      height:12px;
      border-radius:999px;
      border:1px solid rgba(160,255,235,.28);
      overflow:hidden;
      background:rgba(255,255,255,.045);
    }

    #klooie-loader .bar {
      width:38%;
      height:100%;
      border-radius:999px;
      background:linear-gradient(90deg, transparent, rgba(150,255,235,.95), transparent);
      animation:klooie-load 1.6s ease-in-out infinite;
    }

    #klooie-loader .status {
      position:relative;
      min-height:1.2em;
      font-size:clamp(11px,2.5vw,14px);
      letter-spacing:.12em;
      color:rgba(220,255,248,.72);
    }

    #klooie-loader .bits {
      position:absolute;
      inset:0;
      pointer-events:none;
      opacity:.45;
    }

    #klooie-loader .bit {
      position:absolute;
      width:4px;
      height:4px;
      border-radius:50%;
      background:#8ffff0;
      box-shadow:0 0 12px #8ffff0;
      animation:klooie-float 3.5s ease-in-out infinite;
    }

    #klooie-loader .bit:nth-child(1){ left:18%; top:24%; animation-delay:-.2s; }
    #klooie-loader .bit:nth-child(2){ left:79%; top:28%; animation-delay:-1.1s; }
    #klooie-loader .bit:nth-child(3){ left:26%; top:74%; animation-delay:-2.0s; }
    #klooie-loader .bit:nth-child(4){ left:68%; top:72%; animation-delay:-2.8s; }
    #klooie-loader .bit:nth-child(5){ left:50%; top:18%; animation-delay:-1.7s; }

    @media (orientation:landscape) {
      #klooie-loader .shell {
        width:min(78vw,760px);
        height:min(78dvh,390px);
        flex-direction:row;
        gap:36px;
        padding:34px;
      }

      #klooie-loader .copy {
        position:relative;
        display:flex;
        flex-direction:column;
        align-items:flex-start;
        gap:16px;
        min-width:260px;
      }

      #klooie-loader .subtitle {
        width:min(42vw,390px);
      }
    }

    @media (orientation:portrait) {
      #klooie-loader .shell {
        padding:30px 20px;
      }

      #klooie-loader .copy {
        display:flex;
        flex-direction:column;
        align-items:center;
        gap:14px;
      }
    }

    @keyframes klooie-spin { to { transform:rotate(360deg); } }
    @keyframes klooie-pulse { 0%,100% { transform:scale(.96); opacity:.75; } 50% { transform:scale(1.04); opacity:1; } }
    @keyframes klooie-load { 0% { transform:translateX(-105%); } 100% { transform:translateX(270%); } }
    @keyframes klooie-grid { to { transform:perspective(500px) rotateX(58deg) translateY(48px); } }
    @keyframes klooie-float { 0%,100% { transform:translateY(0) scale(.8); opacity:.25; } 50% { transform:translateY(-18px) scale(1.25); opacity:1; } }
  </style>

  <div class="shell">
    <div class="bits">
      <i class="bit"></i><i class="bit"></i><i class="bit"></i><i class="bit"></i><i class="bit"></i>
    </div>

    <div class="core">
      <div class="glyph">K</div>
    </div>

    <div class="copy">
      <div class="title">Loading</div>
      <div class="subtitle"><div class="bar"></div></div>
      <div class="status" id="klooie-loader-status">Preparing console surface</div>
    </div>
  </div>

  <script>
    (() => {
      const el = document.getElementById("klooie-loader-status");
      const loader = document.getElementById("klooie-loader");
      const phrases = [
        "Preparing console surface",
        "Warming render loop",
        "Binding input layer",
        "Painting first frame"
      ];

      let i = 0;
      const timer = el ? setInterval(() => {
        i = (i + 1) % phrases.length;
        el.textContent = phrases[i];
      }, 1400) : undefined;

      window.klooieLoader = {
        ready: () => window.klooieLifecycle.dismissLoading(),
        hide: () => {
          if (timer) clearInterval(timer);
          loader?.remove();
          document.getElementById("klooie-lifecycle-loading")?.remove();
        }
      };
    })();
  </script>
</div>
</body>
</html>`;
    }

    function getDefaultStoppedHtml() {
        return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
<title>App Stopped</title>
</head>
<body style="margin:0;background:#000;overflow:hidden">
<div id="klooie-stopped" style="position:fixed;inset:0;width:100vw;height:100dvh;min-height:100vh;background:#000;color:#d7fff5;overflow:hidden;display:flex;align-items:center;justify-content:center;font-family:Consolas,Menlo,Monaco,'Courier New',monospace;user-select:none;-webkit-user-select:none;touch-action:none">
  <style>
    #klooie-stopped,#klooie-stopped *{box-sizing:border-box}
    #klooie-stopped .shell{position:relative;width:min(92vw,620px);min-height:300px;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:22px;padding:34px;border:1px solid rgba(120,255,220,.24);border-radius:24px;background:radial-gradient(circle at 50% 35%,rgba(0,255,190,.10),transparent 45%),linear-gradient(180deg,rgba(255,255,255,.045),rgba(255,255,255,.01));box-shadow:0 0 44px rgba(0,255,190,.12),inset 0 0 40px rgba(0,255,190,.045);overflow:hidden;text-align:center}
    #klooie-stopped .glyph{font-size:46px;font-weight:800;color:#eafffb;text-shadow:0 0 8px rgba(180,255,245,.85),0 0 24px rgba(0,255,190,.55)}
    #klooie-stopped .title{font-size:clamp(18px,4.2vw,30px);letter-spacing:.18em;text-transform:uppercase;color:#f1fffc;text-shadow:0 0 14px rgba(0,255,200,.45)}
    #klooie-stopped .message{max-width:460px;font-size:clamp(12px,2.6vw,15px);line-height:1.6;color:rgba(220,255,248,.72)}
    #klooie-stopped button{border:1px solid rgba(160,255,235,.34);border-radius:999px;padding:13px 28px;font:800 14px Consolas,Menlo,monospace;letter-spacing:.13em;text-transform:uppercase;color:#eafffb;background:rgba(0,255,190,.13);box-shadow:0 0 20px rgba(0,255,190,.16);cursor:pointer}
  </style>
  <div class="shell">
    <div class="glyph">K</div>
    <div class="title">App Stopped</div>
    <div class="message">The console app has ended. Refresh the page to start a new browser process and launch it again.</div>
    <button type="button" data-klooie-refresh>Refresh</button>
  </div>
</div>
</body>
</html>`;
    }

    return {
        configure,
        showLoading,
        markReady,
        dismissLoading,
        isLoadingDismissed,
        afterLoadingDismissed,
        requestFullscreenAndLandscape,
        showStopped,
        showOverlay,
        dismissOverlay,
        isOverlayActive,
        shouldBlockAppInput,
        pumpGamepadNavigation
    };
})();

window.klooieStorage = window.klooieStorage || {};
window.klooieStorage.clearGameStateAndReload = function () {
    const clearStorage = storage => {
        if (!storage) return;
        for (let i = storage.length - 1; i >= 0; i--) {
            const key = storage.key(i);
            if (key && (key.startsWith("CLAWS:") || key.startsWith("klooie-"))) storage.removeItem(key);
        }
    };

    const clearCookies = () => {
        const expire = name => {
            document.cookie = name + "=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/";
            document.cookie = name + "=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=" + location.pathname;
        };

        for (const cookie of document.cookie.split(";")) {
            const name = cookie.split("=")[0]?.trim();
            if (name) expire(name);
        }
    };

    const clearAsyncState = async () => {
        try {
            if (window.caches?.keys) {
                for (const key of await caches.keys()) await caches.delete(key);
            }
        } catch {
        }

        try {
            if (navigator.serviceWorker?.getRegistrations) {
                for (const registration of await navigator.serviceWorker.getRegistrations()) await registration.unregister();
            }
        } catch {
        }
    };

    try {
        clearStorage(localStorage);
    } catch {
    }

    try {
        clearStorage(sessionStorage);
    } catch {
    }

    try {
        clearCookies();
    } catch {
    }

    clearAsyncState().finally(() => location.reload());
};

function createOverlayGamepadNavigator() {
    const state = {
        host: undefined,
        lastDirection: undefined,
        stickCommitted: false,
        previousButtons: new Map(),
        repeatDirection: undefined,
        nextRepeatAt: 0
    };

    function reset(host) {
        state.host = host;
        state.lastDirection = undefined;
        state.stickCommitted = false;
        state.previousButtons.clear();
        state.repeatDirection = undefined;
        state.nextRepeatAt = 0;
        if (host) focusFirstOverlayControl(host);
    }

    function pump(host, timestamp, gamepads) {
        host = host || state.host;
        if (!host || !document.body.contains(host)) {
            reset();
            return;
        }

        state.host = host;
        focusOverlayControlIfNeeded(host);
        const gamepad = selectOverlayGamepad(gamepads);
        if (!gamepad) {
            state.lastDirection = undefined;
            state.stickCommitted = false;
            state.previousButtons.clear();
            state.repeatDirection = undefined;
            state.nextRepeatAt = 0;
            return;
        }

        pumpOverlayDirection(host, readOverlayDirection(gamepad, state), timestamp);
        pumpOverlayButtons(host, gamepad);
    }

    function pumpOverlayDirection(host, direction, timestamp) {
        if (!direction) {
            state.lastDirection = undefined;
            state.stickCommitted = false;
            state.repeatDirection = undefined;
            state.nextRepeatAt = 0;
            return;
        }

        if (direction !== state.lastDirection) {
            state.lastDirection = direction;
            state.repeatDirection = direction;
            state.nextRepeatAt = timestamp + 430;
            moveOverlayFocus(host, direction);
            return;
        }

        if (direction !== state.repeatDirection) {
            state.repeatDirection = direction;
            state.nextRepeatAt = timestamp + 430;
            return;
        }

        let repeats = 0;
        while (timestamp >= state.nextRepeatAt && repeats < 3) {
            moveOverlayFocus(host, direction);
            state.nextRepeatAt += 120;
            repeats++;
        }

        if (repeats === 3 && timestamp >= state.nextRepeatAt) state.nextRepeatAt = timestamp + 120;
    }

    function pumpOverlayButtons(host, gamepad) {
        const activate = readOverlayButton(gamepad, 0) || readOverlayButton(gamepad, 9);
        const back = readOverlayButton(gamepad, 1);

        if (buttonPressed("activate", activate)) activateOverlayFocus(host);
        if (buttonPressed("back", back)) activateOverlayBack(host);
    }

    function buttonPressed(id, isDown) {
        if (state.previousButtons.has(id) == false) {
            state.previousButtons.set(id, isDown);
            return false;
        }

        const wasDown = state.previousButtons.get(id) === true;
        state.previousButtons.set(id, isDown);
        return isDown && !wasDown;
    }

    return { reset, pump };
}

function selectOverlayGamepad(gamepads) {
    for (const gamepad of gamepads || []) {
        if (gamepad?.connected) return gamepad;
    }

    return undefined;
}

function hasHeldOverlayActionButton(gamepads) {
    for (const gamepad of gamepads || []) {
        if (!gamepad?.connected) continue;
        if (readOverlayButton(gamepad, 0) || readOverlayButton(gamepad, 1) || readOverlayButton(gamepad, 9)) return true;
    }

    return false;
}

function readOverlayDirection(gamepad, state) {
    if (readOverlayButton(gamepad, 12)) return "up";
    if (readOverlayButton(gamepad, 13)) return "down";
    if (readOverlayButton(gamepad, 14)) return "left";
    if (readOverlayButton(gamepad, 15)) return "right";
    return leftStickToOverlayDirection(gamepad, state);
}

function leftStickToOverlayDirection(gamepad, state) {
    const x = remapOverlayStickAxis(gamepad?.axes?.[0]);
    const y = remapOverlayStickAxis(gamepad?.axes?.[1]);
    const magnitude = Math.sqrt((x * x) + (y * y));
    const previous = state.lastDirection;
    if (previous) {
        if (magnitude < 0.22) {
            state.stickCommitted = false;
            return undefined;
        }

        const stronglyHeld = isOverlayDirectionStronglyHeld(previous, x, y, 0.725);
        if (!state.stickCommitted) {
            if (stronglyHeld) state.stickCommitted = true;
            return previous;
        }

        if (!stronglyHeld) {
            state.stickCommitted = false;
            return undefined;
        }

        return previous;
    }

    if (magnitude < 0.60) {
        state.stickCommitted = false;
        return undefined;
    }

    let angle = Math.atan2(y, x) * 180 / Math.PI;
    angle = normalizeOverlayDegrees(angle - 8);
    let result = undefined;
    if (isOverlayAngleWithinSector(angle, 0, 35)) result = "right";
    else if (isOverlayAngleWithinSector(angle, 90, 35)) result = "down";
    else if (isOverlayAngleWithinSector(angle, 180, 35)) result = "left";
    else if (isOverlayAngleWithinSector(angle, 270, 35)) result = "up";

    state.stickCommitted = result ? isOverlayDirectionStronglyHeld(result, x, y, 0.725) : false;
    return result;
}

function isOverlayDirectionStronglyHeld(direction, x, y, threshold) {
    if (direction === "up") return y <= -threshold;
    if (direction === "down") return y >= threshold;
    if (direction === "left") return x <= -threshold;
    if (direction === "right") return x >= threshold;
    return false;
}

function remapOverlayStickAxis(value) {
    value = normalizeAxis(value);
    const magnitude = Math.abs(value);
    if (magnitude < 0.24) return 0;
    return clamp(((magnitude - 0.24) / 0.76) * Math.sign(value), -1, 1);
}

function readOverlayButton(gamepad, index) {
    const button = gamepad?.buttons?.[index];
    return !!button?.pressed || normalizeUnit(button?.value, 0) > (8 / 255);
}

function focusFirstOverlayControl(host) {
    window.setTimeout(() => {
        if (!host || !document.body.contains(host)) return;
        focusOverlayControlIfNeeded(host);
    }, 0);
}

function focusOverlayControlIfNeeded(host) {
    const current = document.activeElement;
    if (current && host.contains(current) && isOverlayFocusable(current)) return;
    setOverlayFocusedControl(findOverlayControls(host)[0]);
}

function moveOverlayFocus(host, direction) {
    const controls = findOverlayControls(host);
    if (controls.length === 0) return;
    if (controls.length === 1) {
        setOverlayFocusedControl(controls[0]);
        return;
    }

    const current = controls.includes(document.activeElement) ? document.activeElement : controls[0];
    const currentRect = centerOf(current.getBoundingClientRect());
    const candidates = controls.filter(control => control !== current)
        .map(control => ({ control, score: scoreOverlayFocusCandidate(currentRect, centerOf(control.getBoundingClientRect()), direction) }))
        .filter(candidate => Number.isFinite(candidate.score))
        .sort((a, b) => a.score - b.score);

    const next = candidates[0]?.control || controls[(controls.indexOf(current) + 1) % controls.length];
    setOverlayFocusedControl(next);
}

function scoreOverlayFocusCandidate(from, to, direction) {
    const dx = to.x - from.x;
    const dy = to.y - from.y;
    if (direction === "up" && dy >= -1) return Number.POSITIVE_INFINITY;
    if (direction === "down" && dy <= 1) return Number.POSITIVE_INFINITY;
    if (direction === "left" && dx >= -1) return Number.POSITIVE_INFINITY;
    if (direction === "right" && dx <= 1) return Number.POSITIVE_INFINITY;

    const primary = direction === "up" || direction === "down" ? Math.abs(dy) : Math.abs(dx);
    const secondary = direction === "up" || direction === "down" ? Math.abs(dx) : Math.abs(dy);
    return primary + (secondary * 2.2);
}

function activateOverlayFocus(host) {
    const current = document.activeElement;
    const target = current && host.contains(current) && isOverlayFocusable(current)
        ? current
        : findOverlayControls(host)[0];
    activateOverlayControl(target);
}

function activateOverlayBack(host) {
    const controls = findOverlayControls(host);
    const target = controls.find(control => isOverlayBackControl(control));
    activateOverlayControl(target);
}

function setOverlayFocusedControl(target) {
    if (!target) return;
    const host = target.closest(".klooie-lifecycle-overlay-host");
    for (const element of host?.querySelectorAll("[data-klooie-overlay-focused]") || []) {
        element.removeAttribute("data-klooie-overlay-focused");
    }

    target.setAttribute("data-klooie-overlay-focused", "true");
    target.focus({ preventScroll: true });
}

function activateOverlayControl(target) {
    if (!target) return;
    if (target instanceof HTMLAnchorElement && target.href) {
        if ((target.target || "").toLowerCase() === "_blank") {
            window.open(target.href, "_blank", "noopener");
            return;
        }

        window.location.href = target.href;
        return;
    }

    target.click?.();
}

function isOverlayBackControl(control) {
    if (control.hasAttribute("data-klooie-overlay-dismiss")) return true;
    const text = (control.textContent || control.getAttribute("aria-label") || control.title || "").trim().toLowerCase();
    return text === "back" || text.includes("back") || text.includes("cancel") || text.includes("close") || text.includes("dismiss");
}

function findOverlayControls(host) {
    return Array.from(host.querySelectorAll("a[href],button,input,select,textarea,[tabindex]")).filter(isOverlayFocusable);
}

function isOverlayFocusable(element) {
    if (!element || element.disabled || element.getAttribute("aria-disabled") === "true") return false;
    if (element.tabIndex < 0) return false;
    const style = window.getComputedStyle(element);
    if (style.visibility === "hidden" || style.display === "none") return false;
    const rect = element.getBoundingClientRect();
    return rect.width > 0 && rect.height > 0;
}

function centerOf(rect) {
    return {
        x: rect.left + (rect.width / 2),
        y: rect.top + (rect.height / 2)
    };
}

function normalizeOverlayDegrees(angle) {
    while (angle < 0) angle += 360;
    while (angle >= 360) angle -= 360;
    return angle;
}

function isOverlayAngleWithinSector(angle, center, halfWidth) {
    return Math.abs(shortestOverlayAngleDelta(angle, center)) <= halfWidth;
}

function shortestOverlayAngleDelta(a, b) {
    let delta = a - b;
    while (delta <= -180) delta += 360;
    while (delta > 180) delta -= 360;
    return delta;
}

window.klooieLifecycle.showLoading();

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
            pendingTouchButtonHints: [],
            firstVisibleFramePresented: false,
            stoppedScreenPresented: false,
            loadingDismissSubscription: undefined,
            mobileOptions: normalizedMobileOptions,
            mobileExperience: shouldShowTouchController(),
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
        state.loadingDismissSubscription = window.klooieLifecycle?.afterLoadingDismissed?.(() => {
            if (state.firstVisibleFramePresented) ensureMobileControls(hostElement, state);
        });
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
    syncZoomEncourageVisibility(visible) {
        for (const id of Object.keys(this.pumps)) {
            this.pumps[id]?.zoomControl?.syncEncourageVisibility?.(visible === true);
        }
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
            pump.loadingDismissSubscription?.();
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
    playGenerations: new Map(),
    music: new Set(),
    audioContext: undefined,
    paused: false,

    play(id, url, volume, pan, loop, isMusic, startPaused, dotNetRef) {
        if (!isMusic && window.klooieLifecycle?.isLoadingDismissed?.() === false) return;

        const playbackId = String(id);
        this.stop(playbackId);
        const playGeneration = this.nextPlayGeneration(playbackId);
        if (isMusic) {
            for (const musicId of Array.from(this.music)) this.stop(musicId);
        }

        this.getDecodedAudio(url)
            .then(buffer => {
                if (this.playGenerations.get(playbackId) !== playGeneration) return;

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

    nextPlayGeneration(id) {
        const playbackId = String(id);
        const next = (this.playGenerations.get(playbackId) || 0) + 1;
        this.playGenerations.set(playbackId, next);
        return next;
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
        const playbackId = String(id);
        this.nextPlayGeneration(playbackId);

        const state = this.active.get(playbackId);
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

        this.active.delete(playbackId);
        this.music.delete(playbackId);
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
        const browserGamepads = readConnectedBrowserGamepads(state);
        const appInputBlocked = window.klooieLifecycle?.shouldBlockAppInput?.(browserGamepads) === true;
        if (appInputBlocked) {
            clearAllHeldKeys(state);
            state.pendingKeys.length = 0;
            state.touchController?.releaseAll?.();
        } else {
            pumpKeyboardRepeats(state, timestamp);
        }
        const keys = appInputBlocked ? [] : state.pendingKeys.splice(0, state.pendingKeys.length);
        const gamepadSnapshotJson = appInputBlocked ? getNeutralGamepadSnapshotJson() : readGamepadSnapshotJson(state, browserGamepads);
        const mobileExperience = shouldShowTouchController();
        if (mobileExperience !== state.mobileExperience) {
            state.mobileExperience = mobileExperience;
            updateCellMetrics(hostElement, state);
            state.sizeDirty = true;
            state.renderer?.invalidateMetrics();
        }

        const terminalFrame = await dotNetRef.invokeMethodAsync("Tick", size.width, size.height, elapsed, keys, gamepadSnapshotJson, mobileExperience, window.location.hostname || "", window.location.search || "");
        applyBrowserControllerCommands(state, terminalFrame);
        state.renderer.render(canvas, terminalFrame, state);
        applyLifecycleFrameState(hostElement, terminalFrame, state);
        state.sizeDirty = false;
    } catch (error) {
        if (isCleanDotNetExit(error)) {
            handleAppStopped(state);
        } else {
            console.error("klooie frame pump tick failed", error);
        }
    } finally {
        state.inFrame = false;
        if (state.pendingKeys.length > 0) {
            state.requestImmediateFrame?.();
        }
    }
}

function isCleanDotNetExit(error) {
    return error?.name === "ExitStatus" && Number(error?.status) === 0;
}

function handleAppStopped(state) {
    if (state.stoppedScreenPresented) return;

    state.stopped = true;
    state.stoppedScreenPresented = true;
    if (state.frameAnimationId !== undefined) {
        cancelAnimationFrame(state.frameAnimationId);
        state.frameAnimationId = undefined;
    }

    teardownKeyboard(state);
    teardownGamepads(state);
    teardownTouchController(state);
    teardownZoomControl(state);
    state.loadingDismissSubscription?.();
    window.klooieLifecycle?.showStopped?.();
}

function setupKeyboard(hostElement, state) {
    if (!hostElement.hasAttribute("tabindex")) {
        hostElement.tabIndex = 0;
    }

    const startRepeat = (event) => {
        if (window.klooieLifecycle?.isOverlayActive?.() === true) return;
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
        if (window.klooieLifecycle?.isOverlayActive?.() === true) return;
        event.preventDefault();
        event.stopPropagation();

        if (isModifierOnlyKey(event.key)) return;
        if (event.repeat) return;
        startRepeat(event);
    };

    const keyup = (event) => {
        if (window.klooieLifecycle?.isOverlayActive?.() === true) return;
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

function clearAllHeldKeys(state) {
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

function applyLifecycleFrameState(hostElement, frame, state) {
    if (!state.firstVisibleFramePresented && frameHasVisibleContent(frame)) {
        state.firstVisibleFramePresented = true;
        window.klooieLifecycle?.markReady?.("Ready");
        if (window.klooieLifecycle?.isLoadingDismissed?.() !== false) ensureMobileControls(hostElement, state);
    } else if (state.firstVisibleFramePresented && state.mobileExperience && !state.touchController) {
        if (window.klooieLifecycle?.isLoadingDismissed?.() !== false) ensureMobileControls(hostElement, state);
    }

    const appStopped = !!(frame?.appStopped ?? frame?.AppStopped);
    if (appStopped && !state.stoppedScreenPresented) {
        state.stoppedScreenPresented = true;
        window.klooieLifecycle?.showStopped?.();
    }
}

function frameHasVisibleContent(frame) {
    const text = frame?.text || frame?.Text || [];
    const foreground = frame?.foreground || frame?.Foreground || [];
    const background = frame?.background || frame?.Background || [];
    for (let i = 0; i < text.length; i++) {
        const runText = text[i] || "";
        const bg = normalizeFrameColorValue(background[i]);
        if (bg !== 0) return true;
        const fg = normalizeFrameColorValue(foreground[i]);
        if (fg !== 0 && Array.from(runText).some(ch => !isBlankGlyph(ch))) return true;
    }

    const presentation = frame?.presentation || frame?.Presentation;
    const scaledRegions = presentation?.scaledRegions || presentation?.ScaledRegions || [];
    const focusRegions = presentation?.focusRegions || presentation?.FocusRegions || [];
    return scaledRegions.length > 0 || focusRegions.length > 0;
}

function normalizeFrameColorValue(color) {
    if (typeof color === "number") return color & 0xffffff;
    if (typeof color === "string" && color[0] === "#") {
        const parsed = parseInt(color.slice(1), 16);
        return Number.isFinite(parsed) ? parsed & 0xffffff : 0;
    }

    return 0;
}

function ensureMobileControls(hostElement, state) {
    if (!shouldShowTouchController()) return;
    if (!state.touchController) setupTouchController(hostElement, state, true);
    if (state.pendingTouchButtonHints.length > 0) state.touchController?.applyButtonHints(state.pendingTouchButtonHints);
    if (!state.zoomControl) setupZoomControl(hostElement, state);
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
    control.className = "klooie-zoom-control is-dimmed";
    control.innerHTML = `
        <button type="button" class="klooie-zoom-out" aria-label="Zoom out">-</button>
        <output class="klooie-zoom-value"></output>
        <button type="button" class="klooie-zoom-in" aria-label="Zoom in">+</button>`;
    hostElement.appendChild(control);

    const value = control.querySelector(".klooie-zoom-value");
    const zoomOut = control.querySelector(".klooie-zoom-out");
    const zoomIn = control.querySelector(".klooie-zoom-in");
    let dimTimer = undefined;

    const dim = () => {
        control.classList.add("is-dimmed");
        dimTimer = undefined;
    };

    const markInteractive = () => {
        control.classList.remove("is-dimmed");
        if (dimTimer !== undefined) window.clearTimeout(dimTimer);
        dimTimer = window.setTimeout(dim, 5000);
    };

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
        markInteractive();
        setZoomIndex(getZoomIndex(state) - 1);
    });

    zoomIn.addEventListener("pointerdown", event => {
        event.preventDefault();
        markInteractive();
        setZoomIndex(getZoomIndex(state) + 1);
    });

    control.addEventListener("pointerenter", markInteractive);
    control.addEventListener("focusin", markInteractive);
    render();
    control.classList.toggle("is-hidden-by-encourage", isTouchEncourageDrawerVisible());
    state.zoomControl = {
        syncEncourageVisibility(visible) {
            control.classList.toggle("is-hidden-by-encourage", visible === true);
        },
        dispose() {
            if (dimTimer !== undefined) window.clearTimeout(dimTimer);
            control.remove();
        }
    };
}

function isTouchEncourageDrawerVisible() {
    const drawer = document.querySelector(".klooie-touch-encourage-drawer:not([hidden])");
    return !!drawer && drawer.textContent.trim().length > 0;
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

function setupTouchController(hostElement, state, fadeIn)
{
    if (!shouldShowTouchController()) return;

    const overlay = document.createElement("div");
    overlay.className = fadeIn ? "klooie-touch-controller klooie-touch-controller-enter" : "klooie-touch-controller";
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
        <div class="klooie-touch-encourage-drawer" hidden></div>
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
    if (fadeIn) {
        requestAnimationFrame(() => overlay.classList.add("is-visible"));
    }

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
        fitTouchButtonLabels(overlay);
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
        releaseAll()
        {
            for (let i = 0; i < buttons.length; i++) buttons[i] = false;
            axes[0] = 0;
            axes[1] = 0;
            axes[2] = 0;
            axes[3] = 0;
            leftStickPressPulseReads = 0;
            stickPointerId = undefined;
            stickBase.style.transform = "";
            stickKnob.style.transform = "";
            for (const button of buttonElements) button.classList.remove("is-pressed");
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
    return !!(document.fullscreenElement || document.webkitFullscreenElement);
}

function isFullscreenDisplayMode() {
    return window.matchMedia?.("(display-mode: fullscreen)")?.matches === true;
}

function canRequestFullscreen() {
    if (isFullscreenDisplayMode()) return false;

    const element = document.documentElement;
    return !!(element.requestFullscreen || element.webkitRequestFullscreen);
}

function isIosBrowser() {
    return /iPad|iPhone|iPod/.test(navigator.userAgent) || (navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1);
}

async function requestFullscreen(element) {
    if (isFullscreenDisplayMode()) return;

    try {
        if (element.requestFullscreen) {
            await element.requestFullscreen({ navigationUI: "auto" });
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

function readConnectedBrowserGamepads(state) {
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

    return Array.from(gamepadsByIndex.values());
}

function readGamepadSnapshotJson(state, browserGamepads) {
    try {
        browserGamepads = browserGamepads || readConnectedBrowserGamepads(state);
        const gamepads = Array.from(browserGamepads, gamepad => ({
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

function getNeutralGamepadSnapshotJson() {
    return JSON.stringify({
        gamepads: [{
            id: "overlay-blocked",
            index: 0,
            connected: true,
            mapping: "",
            buttons: new Array(17).fill(0).map(() => ({ pressed: false, value: 0 })),
            axes: [0, 0, 0, 0]
        }]
    });
}

function applyBrowserControllerCommands(state, frame) {
    const overlays = frame?.overlayCommands || frame?.OverlayCommands;
    if (overlays?.length > 0) {
        for (const overlay of overlays) window.klooieLifecycle?.showOverlay?.(overlay);
    }

    const releases = frame?.touchButtonReleases || frame?.TouchButtonReleases;
    if (releases?.length > 0) {
        state.touchController?.releaseButtons(releases);
    }

    const hints = frame?.touchButtonHints || frame?.TouchButtonHints;
    if (hints?.length > 0) {
        state.pendingTouchButtonHints = mergeTouchButtonHints(state.pendingTouchButtonHints, hints);
        state.touchController?.applyButtonHints(state.pendingTouchButtonHints);
    }
}

function mergeTouchButtonHints(existing, incoming) {
    if (incoming?.some?.(hint => Number(hint?.button ?? hint?.Button) < 0)) return incoming;

    const byButton = new Map();
    for (const hint of existing || []) {
        const index = Number(hint?.button ?? hint?.Button);
        if (Number.isInteger(index) && index >= 0) byButton.set(index, hint);
    }

    for (const hint of incoming || []) {
        const index = Number(hint?.button ?? hint?.Button);
        if (Number.isInteger(index) && index >= 0) byButton.set(index, hint);
    }

    return Array.from(byButton.values());
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
            setTouchButtonLabel(element, labelElement, getDefaultTouchButtonLabel(index));
            element.classList.remove("is-disabled");
            element.classList.remove("is-encouraged");
            element.setAttribute("aria-disabled", "false");
        }

        updateTouchEncourageDrawer(overlay, []);
        return;
    }

    for (const hint of hints || []) {
        const index = Number(hint?.button ?? hint?.Button);
        if (!Number.isInteger(index) || index < 0) continue;

        const label = String(hint?.label ?? hint?.Label ?? getDefaultTouchButtonLabel(index));
        const enabled = (hint?.enabled ?? hint?.Enabled) !== false;
        const encouraged = (hint?.encourage ?? hint?.Encourage) === true;
        const element = overlay.querySelector(`[data-button="${index}"]`);
        if (!element) continue;

        const labelElement = element.classList.contains("klooie-touch-stick-base")
            ? element.querySelector(".klooie-touch-stick-label")
            : element;
        setTouchButtonLabel(element, labelElement, label);
        element.classList.toggle("is-disabled", !enabled);
        element.classList.toggle("is-encouraged", encouraged);
        element.setAttribute("aria-disabled", enabled ? "false" : "true");
    }

    updateTouchEncourageDrawer(overlay, hints);
}

function updateTouchEncourageDrawer(overlay, hints) {
    const drawer = overlay.querySelector(".klooie-touch-encourage-drawer");
    if (!drawer) return;

    let message = "";
    for (const hint of hints || []) {
        if ((hint?.encourage ?? hint?.Encourage) !== true) continue;
        message = String(hint?.encourageMessage ?? hint?.EncourageMessage ?? "");
        if (message.trim().length > 0) break;
    }

    drawer.textContent = message;
    const visible = message.trim().length > 0;
    drawer.hidden = !visible;
    window.klooieFramePump?.syncZoomEncourageVisibility?.(visible);
}

function setTouchButtonLabel(element, labelElement, label) {
    if (!labelElement) return;

    labelElement.textContent = label;
    labelElement.title = label;
    fitTouchButtonLabel(element, labelElement);
}

function fitTouchButtonLabels(overlay) {
    if (!overlay) return;

    for (const element of overlay.querySelectorAll("[data-button]")) {
        const labelElement = element.classList.contains("klooie-touch-stick-base")
            ? element.querySelector(".klooie-touch-stick-label")
            : element;
        fitTouchButtonLabel(element, labelElement);
    }
}

function fitTouchButtonLabel(element, labelElement) {
    if (!element || !labelElement) return;

    labelElement.style.fontSize = "";

    const maxWidth = getTouchButtonLabelMaxWidth(element, labelElement);
    if (maxWidth <= 0 || labelElement.scrollWidth <= maxWidth) return;

    const style = window.getComputedStyle(labelElement);
    const baseFontSize = parseFloat(style.fontSize);
    if (!Number.isFinite(baseFontSize) || baseFontSize <= 0) return;

    const minFontSize = Math.max(10, baseFontSize * 0.72);
    const fittedFontSize = Math.max(minFontSize, Math.floor(baseFontSize * maxWidth / labelElement.scrollWidth));
    labelElement.style.fontSize = `${fittedFontSize}px`;
}

function getTouchButtonLabelMaxWidth(element, labelElement) {
    const rect = labelElement.getBoundingClientRect();
    if (element.classList.contains("klooie-touch-stick-base")) {
        return Math.max(0, rect.width);
    }

    return Math.max(0, element.clientWidth - 2);
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
    probe.classList.toggle("browser-console-measure-mobile", state.mobileExperience);
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
        return new WebGlConsoleRenderer(canvas, state);
    } catch (error) {
        console.warn("klooie retained WebGL renderer unavailable; falling back to WebGL2 cell renderer", error);
        try {
            return new WebGl2CellConsoleRenderer(canvas, state);
        } catch (fallbackError) {
            console.error("klooie GPU renderer unavailable; WebGL is required", fallbackError);
            return new WebGlRequiredRenderer(canvas);
        }
    }
}

class WebGlRequiredRenderer {
    constructor(canvas) {
        canvas.dataset.klooieRenderer = "unsupported-webgl";
        this.overlay = document.createElement("div");
        this.overlay.id = "klooie-webgl-required";
        this.overlay.setAttribute("role", "alert");
        this.overlay.style.cssText = "position:fixed;inset:0;z-index:5000;display:flex;align-items:center;justify-content:center;padding:24px;background:#050505;color:white;font-family:Consolas,Menlo,Monaco,'Courier New',monospace;text-align:center";
        this.overlay.innerHTML = `<div style="max-width:680px;border:1px solid rgba(255,255,255,.22);padding:28px;background:#101010">
  <h1 style="margin:0 0 16px;font-size:28px">WebGL Required</h1>
  <p style="margin:0;line-height:1.6;color:rgba(255,255,255,.78)">This browser or device does not expose WebGL, which this app needs for the web renderer. Try a current browser with hardware acceleration enabled.</p>
</div>`;
        document.body.appendChild(this.overlay);
        window.klooieLifecycle?.dismissLoading?.();
    }

    invalidateMetrics() {
    }

    render() {
    }

    dispose() {
        this.overlay?.remove();
        this.overlay = undefined;
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
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
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
        this.context.imageSmoothingEnabled = false;
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
    vec2 atlasUv = atlasPixel / u_atlasSize;
    float glyphAlpha = texture(u_atlas, atlasUv).a;
    vec2 presentationScale = u_targetSize / max(vec2(1.0), u_sourceSize * u_cellSize);
    if (max(presentationScale.x, presentationScale.y) > 1.01) {
        vec2 atlasTexel = vec2(1.0) / u_atlasSize;
        glyphAlpha = max(glyphAlpha, texture(u_atlas, atlasUv + vec2(atlasTexel.x, 0.0)).a);
        glyphAlpha = max(glyphAlpha, texture(u_atlas, atlasUv - vec2(atlasTexel.x, 0.0)).a);
        glyphAlpha = max(glyphAlpha, texture(u_atlas, atlasUv + vec2(0.0, atlasTexel.y)).a);
        glyphAlpha = max(glyphAlpha, texture(u_atlas, atlasUv - vec2(0.0, atlasTexel.y)).a);
    }
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
