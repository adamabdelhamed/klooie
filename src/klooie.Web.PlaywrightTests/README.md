# klooie Web Playwright Tests

This package validates the reusable Blazor/WebAssembly host used by klooie apps. It focuses on browser-only contracts that normal C# tests cannot cover: lifecycle loading, first visible frame readiness, renderer selection, WebGL unsupported behavior, and mobile touch-control layering.

## Install

```powershell
cd C:\Users\Adam\source\repos\TotallyTextualBattleSimulator\external\klooie\src\klooie.Web.PlaywrightTests
npm install
npm run install:browsers
```

## Run Against The klooie Sample App

The default command packages `klooie.blazorSampleApp` with `kpack -type Web`, waits for packaging to finish, then serves `bin\klooie.web` on port 5187 in Fast mode:

```powershell
npm test
```

CLAWS-specific loader and demo tests live in the parent repo under `Claws.Web.PlaywrightTests`.

## Run Against An Already Hosted Site

Set `KLOOIE_WEB_URL` to skip local packaging and server startup:

```powershell
$env:KLOOIE_WEB_URL="https://your-github-pages-site/"
npm test
```

The tests intentionally fail if the canvas renderer falls back to Canvas2D. Unsupported WebGL should show the host-owned `#klooie-webgl-required` overlay.

The generated single-app route defaults to `/__klooie`. Set `KLOOIE_WEB_APP_ROUTE` only if a future package uses a different route.
The suite defaults to one worker because WASM startup is sensitive to many simultaneous first-loads. Set `KLOOIE_WEB_WORKERS` only when validating against a production-grade static server.
Set `KLOOIE_WEB_ASSUME_BUILT=true` to serve existing `bin\klooie.web` without invoking kpack.
