# klooie.Packager Agent Notes

This project builds the `kpack` command line tool. It packages normal klooie projects into either a Windows executable or a static Blazor WebAssembly site.

## Important Constraints

- Do not edit klooie's performance-critical browser runtime paths casually. The copied WebHost template contains marshalling, frame pumping, rendering, keyboard mapping, and `ConsoleApp.Tick()` integration code under `Templates/WebHost/BrowserConsole` and `Templates/WebHost/wwwroot/klooieFramePump.js`.
- If those paths must change, verify with an actual packaged app in a browser, not just `dotnet build`.
- Keep the generated Blazor host route simple: `/` should start the single packaged app directly.
- The old `klooie.blazor` prototype project was intentionally folded into this packager as template files under `Templates/WebHost`.

## Key Files

- `Program.cs`: CLI parsing, project inspection, entrypoint discovery, temp host generation, publish/copy operations.
- `Templates/WebHost`: Source template copied to `obj/klooie.packager/web` before generated files are overlaid.
- `Templates/WebHost/klooie.Packager.WebHostTemplate.csproj`: Compile check project. `klooie.Packager` references this with `ReferenceOutputAssembly=false` so template source and Razor files compile when the packager builds.
- `../klooie/Packaging/KlooieWebTargetAttribute.cs`: Marker used by target projects to identify the web entrypoint.
- `../klooie.blazorSampleApp/DemoAppProgram.cs`: Sample target using `[KlooieWebTarget]`.
- `../klooie.blazorSampleApp/klooie.blazorSampleApp.csproj`: Builds the packager and runs `kpack` after normal builds so Visual Studio sample builds refresh `bin/klooie.web`.
- `RunKpack.cmd`: VS-friendly launcher that copies Debug packager output to a temp folder before running `kpack`; this avoids locking `bin\Debug\net10.0\kpack.dll` across rebuilds.
- `../klooie.blazorSampleApp/Properties/launchSettings.json`: VS launch profile for running `RunKpack.cmd ... -type Serve` on port 5187. VS launches from `bin/<Configuration>/net10.0`, so the profile reaches the launcher and target project with relative paths.

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
- The packager currently discovers `[KlooieWebTarget]` on a static no-argument `MainAsync` or `Main` method. If no marker exists, it falls back to no-argument static `MainAsync`/`Main` candidates.
- `[KlooieWebTarget(DisplayName = "...", Description = "...")]` is used for generated registry metadata.
- The generated web host keeps `RootNamespace` as `klooie.blazor` because the folded template source still uses that namespace.
- `kpack -type Web` passes `KlooieOptimizeWebAssembly=true`, which makes the generated WebHost set `PublishTrimmed=true` and `RunAOTCompilation=true` so the Blazor WebAssembly SDK can AOT compile and relink the runtime when `wasm-tools` is installed. `-type Serve` derives that property from the Visual Studio launch working directory: Release launches AOT publish, Debug launches stay fast.
- Serve packaging is guarded by a per-project cross-process temp-file lock. Keep that lock because Release AOT publishes are long-running and concurrent publishes for the same target can collide in referenced project intermediate outputs even when the generated host directories are unique.
- Generated web host intermediates use short unique paths under `obj/kp`; keep those paths short because Blazor WebAssembly publish creates deep `obj/Release/net*/wasm/for-publish` trees and long paths can surface as missing WebCIL/static-asset files.
- Keep `Templates/WebHost/Program.cs` valid. The packager overwrites `Program.cs` in generated temp hosts, but the template version is needed by the compile-check project.
- `kpack` passes `-p:KlooiePackageOnBuild=false` to its internal `dotnet publish` calls. Keep this suppression or sample build packaging will recurse.
