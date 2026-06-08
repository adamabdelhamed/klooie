# klooie.Packager Agent Notes

This project builds the `kpack` command line tool. It packages normal klooie projects into either a Windows executable or a static Blazor WebAssembly site.

## Important Constraints

- Do not edit klooie's performance-critical browser runtime paths casually. The copied WebHost template contains marshalling, frame pumping, rendering, keyboard mapping, and `ConsoleApp.Tick()` integration code under `Templates/WebHost/BrowserConsole` and `Templates/WebHost/wwwroot/klooieFramePump.js`.
- If those paths must change, verify with an actual packaged app in a browser, not just `dotnet build`.
- Keep the generated Blazor host route simple: `/` should start the single packaged app directly.
- The old `klooie.blazor` prototype project was intentionally folded into this packager as template files under `Templates/WebHost`.
- The WebHost template injects a touch controller overlay only for coarse-pointer/touch environments. It synthesizes a browser gamepad snapshot with `mapping: "klooie-touch"` and Xbox-style button indexes; keep it compatible with `klooie/Controllers/BrowserGamepadController.cs` rather than app-specific input code.
- The WebHost template owns generic lifecycle overlays for loading and app-stopped states. `[KlooieWebTarget(LoadingHtmlAssetPath = "...", StoppedHtmlAssetPath = "...")]` points at files under the target project's `Assets` directory and resolves them to `assets/...`; a loading snippet can expose `window.klooieLoader.ready(...)` to delay dismissal behind a user gesture, while the host calls that signal only after the first visible non-black app frame.
- Browser title, favicon, PWA manifest names, colors, and icon paths are generated from `[KlooieWebTarget]` metadata. Keep this C#-driven path intact so app authors do not need to edit host pages or manifests.
- Mobile zoom is a browser presentation-layer feature in `Templates/WebHost/wwwroot/klooieFramePump.js`. It changes measured/rendered cell size before terminal dimensions are sent to the app; do not add app/game APIs for zoom.
- Mobile touch controls and zoom controls are created only after both the first visible app frame and lifecycle loading dismissal, then fade in from CSS. Non-music browser audio is also suppressed until loading dismissal so app startup SFX cannot play behind a blocking loader. Keep loading/end screens in the lifecycle layer and controller/audio visibility in the browser host path.
- Web host visual layers are ordered intentionally: temporary lifecycle overlays are topmost, lifecycle loading/stopped screens sit above the mobile shell, the encourage drawer sits above touch controls, touch controls sit above zoom/game canvas, and the canvas stays at the bottom. Keep the loading overlay idempotent because Blazor rerenders and mobile fullscreen/orientation changes can otherwise remount custom loading HTML after the user has already dismissed it.
- Generated web packages use the package fingerprint as `window.klooieBuildId` and append it to host CSS/JS/manifest/service-worker URLs. Lifecycle HTML fetches add that same build id and use `no-store`; avoid `force-cache` for host lifecycle assets because stale loader CSS/HTML can break mobile startup layering.
- HTML lifecycle overlays consume live browser gamepad input in `klooieFramePump.js` while the app receives a neutral gamepad snapshot. Keep this separation so overlay links/buttons remain usable from controllers without leaking overlay navigation into C# game input. Overlay HTML can rely on the host's focus indicator; mark the dismiss/back action with `data-klooie-overlay-dismiss` so the B button has a stable target.
- The mobile zoom buttons step on the displayed percent scale in 5% increments. Keep the internal zoom derived from `zoomDefault * displayedPercent / 100` so app-specific min/default/max values can tune the experience without making the UI feel irregular.
- The browser host supports WebGL only. `createConsoleRenderer` may fall from the retained WebGL renderer to the WebGL2 cell renderer, but if both fail it must show `#klooie-webgl-required` and must not use Canvas2D as a hidden support path.
- Playwright coverage for this host lives in `../klooie.Web.PlaywrightTests`; run it after changes to `Templates/WebHost/wwwroot/klooieFramePump.js`, lifecycle overlay loading, renderer fallback behavior, or mobile controls. App-specific loader tests belong in the consuming app repo.

## Key Files

- `Program.cs`: CLI parsing, project inspection, entrypoint discovery, temp host generation, publish/copy operations.
- `Templates/WebHost`: Source template copied to `obj/klooie.packager/web` before generated files are overlaid.
- `Templates/WebHost/klooie.Packager.WebHostTemplate.csproj`: Compile check project. `klooie.Packager` references this with `ReferenceOutputAssembly=false` so template source and Razor files compile when the packager builds.
- `../klooie/Packaging/KlooieWebTargetAttribute.cs`: Marker used by target projects to identify the web entrypoint.
- `../klooie.blazorSampleApp/DemoAppProgram.cs`: Sample target using `[KlooieWebTarget]`.
- `../klooie.blazorSampleApp/klooie.blazorSampleApp.csproj`: Builds the packager and runs `kpack` after normal builds so Visual Studio sample builds refresh `bin/klooie.web`.
- `RunKpack.cmd`: VS-friendly launcher that copies Debug packager output to a temp folder before running `kpack`; this avoids locking `bin\Debug\net10.0\kpack.dll` across rebuilds.
- `../klooie.blazorSampleApp/Properties/launchSettings.json`: VS launch profiles for running `RunKpack.cmd ... -type Serve -webMode Fast|Aot` on port 5187. VS launches from `bin/<Configuration>/net10.0`, so the profile reaches the launcher and target project with relative paths.

## Expected Commands

Build the tool:

```powershell
dotnet build .\src\klooie.Packager\klooie.Packager.csproj --nologo
```

Package the sample app for web:

```powershell
dotnet run --project .\src\klooie.Packager\klooie.Packager.csproj -- .\src\klooie.blazorSampleApp\klooie.blazorSampleApp.csproj -type Web
```

Build the full solution after changes:

```powershell
dotnet build .\src\klooie.sln --nologo
```

Serve generated static output for browser verification:

```powershell
dotnet .\src\klooie.Packager\bin\Debug\net10.0\kpack.dll .\src\klooie.blazorSampleApp\klooie.blazorSampleApp.csproj -type Serve -port 5187
```

Run browser regression tests:

```powershell
cd .\src\klooie.Web.PlaywrightTests
npm test
```

Or use the no-argument wrapper scripts from the klooie repo root:

```powershell
.\Scripts\Test-Klooie-Web-Fast-Headless.cmd
.\Scripts\Test-Klooie-Web-Fast-Headful.cmd
```

## Acceptance Check

For this milestone, packaging `klooie.blazorSampleApp` with `-type Web` should:

- Create `src/klooie.blazorSampleApp/bin/klooie.web`.
- Put publish-ready static files directly in that folder, including `index.html` and `_framework`.
- Start the sample app at `/` when served by a static web server.
- Render a full-screen klooie canvas and visible sample app contents.
- Produce no Blazor error UI or browser console errors.

## Implementation Notes

- The generated project is written to the target project's `obj/klooie.packager/web`.
- Intermediate publish output goes to `obj/klooie.packager/publish`.
- Final web output goes to `bin/klooie.web`.
- For web packages, an `Assets` directory beside the target `.csproj` is copied by convention to `bin/klooie.web/assets`; `assets/klooie-assets.json` lists the copied files for the browser asset provider.
- `-type Serve` first runs the normal web package path, then stops any existing Windows TCP listener on the requested port and serves `bin/klooie.web` on `127.0.0.1` with no-cache headers. Use it for Visual Studio F5/Ctrl+F5 browser launches instead of a standalone static server.
- Windows `HttpListener` registrations can appear in `netstat` as PID 4, so serve cleanup also checks kpack PID files and `netsh http show servicestate` to stop stale `dotnet.exe` listeners for the requested localhost port.
- The packager currently discovers `[KlooieWebTarget]` on a static no-argument `MainAsync` or `Main` method. If no marker exists, it falls back to no-argument static `MainAsync`/`Main` candidates.
- `[KlooieWebTarget(DisplayName = "...", BrowserTitle = "...", PwaName = "...", PwaShortName = "...", IconPath = "...", Description = "...")]` is used for generated registry, document head, and PWA manifest metadata. Relative `IconPath` values resolve from the target project directory; `.ico` files are copied for favicons, and embedded PNG frames are extracted for install icons when available.
- The generated web host keeps `RootNamespace` as `klooie.blazor` because the folded template source still uses that namespace.
- Web packaging uses `KlooieWebMode` from the target project unless `-webMode Fast|Aot` is passed. `Aot` makes the generated WebHost set `PublishTrimmed=true` and `RunAOTCompilation=true`; `Fast` leaves AOT off even in Release. Do not infer AOT from Debug/Release.
- `kpack` writes `bin/klooie.web/klooie.package.stamp` and skips publish when the project graph, template, packager runtime, assets, and selected web mode are unchanged. Keep that stamp mode-aware so switching Fast/Aot repackages.
- Serve packaging is guarded by a per-project cross-process temp-file lock. Keep that lock because Release AOT publishes are long-running and concurrent publishes for the same target can collide in referenced project intermediate outputs even when the generated host directories are unique.
- Generated web host intermediates use short unique paths under `obj/kp`; keep those paths short because Blazor WebAssembly publish creates deep `obj/Release/net*/wasm/for-publish` trees and long paths can surface as missing WebCIL/static-asset files.
- Keep `Templates/WebHost/Program.cs` valid. The packager overwrites `Program.cs` in generated temp hosts, but the template version is needed by the compile-check project.
- `kpack` passes `-p:KlooiePackageOnBuild=false` to its internal `dotnet publish` calls. Keep this suppression or sample build packaging will recurse.
