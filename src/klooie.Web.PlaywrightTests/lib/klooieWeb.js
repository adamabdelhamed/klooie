import { expect } from "@playwright/test";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { PNG } from "pngjs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const screenshotRoot = path.resolve(__dirname, "..", "screenshots");

export async function openFresh(page) {
  const configuredRoute = process.env.KLOOIE_WEB_APP_ROUTE || "__klooie";
  const appPath = configuredRoute.startsWith("/") ? configuredRoute : `/${configuredRoute}`;
  const deadline = Date.now() + 180_000;
  while (Date.now() < deadline) {
    await page.goto(appPath, { waitUntil: "domcontentloaded" });
    const state = await page.evaluate(() => {
      return {
        hasHost: !!document.querySelector(".klooie-host"),
        appRoute: document.querySelector(".app-link")?.getAttribute("href") || "",
        bodyText: document.body?.innerText || ""
      };
    });

    if (state.bodyText.includes("Packaging fresh web output")) {
      throw new Error("klooie web tests reached the kpack packaging interstitial. Build/package must complete before Playwright starts browser assertions.");
    }

    if (state.hasHost) break;
    if (state.appRoute) {
      await page.goto(state.appRoute, { waitUntil: "domcontentloaded" });
      break;
    }

    break;
  }

  await expect(page.locator(".klooie-host")).toBeVisible({ timeout: 120_000 });
}

export async function waitForWebGlRenderer(page) {
  const canvas = page.locator("canvas.klooie-canvas");
  await expect(canvas).toBeVisible();
  await expect.poll(async () => await canvas.evaluate(element => element.dataset.klooieRenderer || ""), {
    message: "klooie canvas should use a WebGL renderer"
  }).toMatch(/^webgl/);
  return canvas;
}

export async function waitForFirstVisibleFrame(page, options = {}) {
  const requireScreenshotPixels = options.requireScreenshotPixels !== false;
  await waitForWebGlRenderer(page);
  await expect.poll(async () => await page.evaluate(() => Object.keys(window.klooieFramePump?.pumps || {}).length), {
    message: "klooie frame pump should be registered"
  }).toBeGreaterThan(0);
  await expect.poll(async () => await page.evaluate(() => {
    const pumps = Object.values(window.klooieFramePump?.pumps || {});
    return pumps.some(pump => pump?.firstVisibleFramePresented === true);
  }), {
    timeout: 60_000,
    message: "klooie host should report the first visible app frame"
  }).toBe(true);
  if (!requireScreenshotPixels) return;
  await expect.poll(async () => await countNonBlackPixels(await page.screenshot({ fullPage: false })), {
    timeout: 60_000,
    message: "browser screenshot should contain visible non-black app pixels"
  }).toBeGreaterThan(500);
}

export async function waitForLifecycleLoadingGone(page) {
  await expect.poll(async () => {
    return await page.evaluate(() => {
      const element = document.getElementById("klooie-lifecycle-loading");
      if (!element) return false;
      const style = getComputedStyle(element);
      const rect = element.getBoundingClientRect();
      return style.display !== "none" && style.visibility !== "hidden" && Number(style.opacity) !== 0 && rect.width > 0 && rect.height > 0;
    });
  }, {
    timeout: 60_000,
    message: "lifecycle loading overlay should be dismissed"
  }).toBe(false);
}

export async function assertElementFitsViewport(page, selector) {
  const result = await page.locator(selector).first().evaluate(element => {
    const rect = element.getBoundingClientRect();
    return {
      left: rect.left,
      top: rect.top,
      right: rect.right,
      bottom: rect.bottom,
      viewportWidth: window.innerWidth,
      viewportHeight: window.innerHeight
    };
  });

  expect(result.left, `${selector} left edge`).toBeGreaterThanOrEqual(0);
  expect(result.top, `${selector} top edge`).toBeGreaterThanOrEqual(0);
  expect(result.right, `${selector} right edge`).toBeLessThanOrEqual(result.viewportWidth);
  expect(result.bottom, `${selector} bottom edge`).toBeLessThanOrEqual(result.viewportHeight);
}

export async function assertMobileControlsReady(page) {
  const controller = page.locator(".klooie-touch-controller");
  await expect(controller).toBeVisible({ timeout: 60_000 });
  await expect(controller).toHaveClass(/is-visible/);
  await expect(page.locator(".klooie-touch-stick-base")).toBeVisible();
  await expect(page.locator(".klooie-touch-face button[data-button='0']")).toBeVisible();
  await assertElementFitsViewport(page, ".klooie-touch-stick-base");
  await assertElementFitsViewport(page, ".klooie-touch-face");

  const layering = await page.evaluate(() => {
    const canvas = document.querySelector(".klooie-canvas");
    const touch = document.querySelector(".klooie-touch-controller");
    const zoom = document.querySelector(".klooie-zoom-control");
    const loading = document.getElementById("klooie-lifecycle-loading");
    const z = element => element ? Number(getComputedStyle(element).zIndex) || 0 : null;
    return {
      canvas: z(canvas),
      touch: z(touch),
      zoom: z(zoom),
      loadingVisible: !!loading && getComputedStyle(loading).display !== "none" && loading.getBoundingClientRect().width > 0
    };
  });

  expect(layering.loadingVisible).toBe(false);
  expect(layering.touch).toBeGreaterThan(layering.canvas);
  if (layering.zoom !== null) expect(layering.touch).toBeGreaterThan(layering.zoom);
}

export async function installNoWebGlShim(page) {
  await page.addInitScript(() => {
    const originalGetContext = HTMLCanvasElement.prototype.getContext;
    HTMLCanvasElement.prototype.getContext = function(type, ...args) {
      const normalized = String(type || "").toLowerCase();
      if (normalized === "webgl" || normalized === "experimental-webgl" || normalized === "webgl2") return null;
      return originalGetContext.call(this, type, ...args);
    };
  });
}

export async function captureCriticalScreenshot(page, testInfo, name) {
  const safeProject = sanitizeFilePart(testInfo.project.name);
  const safeTest = sanitizeFilePart(testInfo.titlePath.slice(1).join("-"));
  const safeName = sanitizeFilePart(name);
  const dir = path.join(screenshotRoot, safeProject, safeTest);
  await fs.mkdir(dir, { recursive: true });
  await page.screenshot({ path: path.join(dir, `${safeName}.png`), fullPage: false });
}

function countNonBlackPixels(buffer) {
  const png = PNG.sync.read(buffer);
  let count = 0;
  for (let i = 0; i < png.data.length; i += 4) {
    const alpha = png.data[i + 3];
    if (alpha === 0) continue;
    const r = png.data[i];
    const g = png.data[i + 1];
    const b = png.data[i + 2];
    if (r + g + b > 45) count++;
  }
  return count;
}

function sanitizeFilePart(value) {
  return String(value || "unnamed").replace(/[^a-z0-9._-]+/gi, "-").replace(/^-+|-+$/g, "").toLowerCase();
}
