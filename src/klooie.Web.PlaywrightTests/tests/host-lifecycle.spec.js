import { test, expect } from "@playwright/test";
import {
  assertMobileControlsReady,
  installNoWebGlShim,
  openFresh,
  waitForFirstVisibleFrame,
  waitForLifecycleLoadingGone,
  waitForWebGlRenderer
} from "../lib/klooieWeb.js";

test("first load reaches a visible WebGL frame without browser errors", async ({ page }) => {
  const errors = [];
  page.on("pageerror", error => errors.push(error.message));
  page.on("console", message => {
    if (message.type() === "error") errors.push(message.text());
  });

  await openFresh(page);
  await waitForFirstVisibleFrame(page);
  await waitForLifecycleLoadingGone(page);

  const renderer = await page.locator("canvas.klooie-canvas").evaluate(element => element.dataset.klooieRenderer);
  expect(renderer).toMatch(/^webgl/);
  expect(errors.filter(error => /blazor|webgl|required|exception|error/i.test(error))).toEqual([]);
});

test("mobile lifecycle reveals fitted touch controls after the app is visible and loading is dismissed", async ({ page, isMobile }) => {
  test.skip(!isMobile, "mobile shell is only expected on coarse pointer projects");

  await openFresh(page);
  await waitForWebGlRenderer(page);
  await waitForFirstVisibleFrame(page);
  await waitForLifecycleLoadingGone(page);
  await assertMobileControlsReady(page);
});

test("unsupported WebGL shows a blocking error instead of falling back to Canvas2D", async ({ page }) => {
  await installNoWebGlShim(page);
  await openFresh(page);

  await expect(page.locator("#klooie-webgl-required")).toBeVisible({ timeout: 60_000 });
  await expect(page.locator("#klooie-webgl-required")).toContainText("WebGL Required");
  await expect(page.locator("canvas.klooie-canvas")).toHaveAttribute("data-klooie-renderer", "unsupported-webgl");
  await expect(page.locator("canvas.klooie-canvas")).not.toHaveAttribute("data-klooie-renderer", "canvas2d");
});
