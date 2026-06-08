# klooie Web Playwright Test Notes

This folder owns browser end-to-end tests for the reusable klooie Blazor/WebAssembly host. Keep tests focused on host contracts and mobile lifecycle behavior that cannot be covered by C# unit tests.

## Commands

Install once:

```powershell
npm install
npm run install:browsers
```

Run the default sample-app suite:

```powershell
npm test
```

Set `KLOOIE_WEB_URL` to test already-hosted static output without starting `kpack`.
The tests open `/__klooie` by default. Override with `KLOOIE_WEB_APP_ROUTE` only when testing a package with a different route.
The config intentionally defaults to one worker. Parallel browser first-loads can overwhelm `kpack Serve`.

## Intent

- The sample app is the reusable host contract target.
- CLAWS-specific assertions belong in the parent repo's `Claws.Web.PlaywrightTests`, not here.
- Do not reintroduce Canvas2D renderer support. WebGL failure should present `#klooie-webgl-required`.
- Keep mobile checks geometry-based: controls must exist, fit the viewport, and layer above the canvas after lifecycle loading is dismissed.
