import { defineConfig, devices } from "@playwright/test";
import path from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const defaultProject = path.resolve(__dirname, "..", "klooie.blazorSampleApp", "klooie.blazorSampleApp.csproj");
const projectPath = process.env.KLOOIE_WEB_PROJECT || defaultProject;
const port = Number(process.env.KLOOIE_WEB_PORT || "5187");
const baseURL = process.env.KLOOIE_WEB_URL || `http://127.0.0.1:${port}/`;
const webMode = process.env.KLOOIE_WEB_MODE || "Fast";
const shouldStartServer = !process.env.KLOOIE_WEB_URL;
const assumeBuiltBits = (process.env.KLOOIE_WEB_ASSUME_BUILT || "").toLowerCase() === "true";
const serverScript = assumeBuiltBits ? "serve-built-klooie-web.ps1" : "build-and-serve-klooie-web.ps1";
const serverModeArgs = assumeBuiltBits ? "" : ` -WebMode "${webMode}"`;

export default defineConfig({
  testDir: "./tests",
  timeout: 120_000,
  expect: { timeout: 30_000 },
  fullyParallel: false,
  workers: Number(process.env.KLOOIE_WEB_WORKERS || "1"),
  reporter: [
    ["list"],
    ["html", { open: "never", outputFolder: "playwright-report" }]
  ],
  use: {
    baseURL,
    trace: "retain-on-failure",
    screenshot: "off",
    video: "retain-on-failure"
  },
  webServer: shouldStartServer ? {
    command: `powershell -NoProfile -ExecutionPolicy Bypass -File "${path.resolve(__dirname, "scripts", serverScript)}" -ProjectPath "${projectPath}" -Port ${port}${serverModeArgs}`,
    url: baseURL,
    timeout: 240_000,
    reuseExistingServer: true
  } : undefined,
  projects: [
    {
      name: "chromium-desktop",
      use: {
        ...devices["Desktop Chrome"],
        viewport: { width: 1366, height: 768 }
      }
    },
    {
      name: "chromium-ultrawide",
      use: {
        ...devices["Desktop Chrome"],
        viewport: { width: 2560, height: 1080 }
      }
    },
    {
      name: "chromium-small-desktop",
      use: {
        ...devices["Desktop Chrome"],
        viewport: { width: 1024, height: 640 }
      }
    },
    {
      name: "firefox-desktop",
      use: {
        ...devices["Desktop Firefox"],
        viewport: { width: 1440, height: 900 }
      }
    },
    {
      name: "webkit-desktop",
      use: {
        ...devices["Desktop Safari"],
        viewport: { width: 1440, height: 900 }
      }
    },
    {
      name: "mobile-chrome-landscape",
      use: {
        ...devices["Pixel 7 landscape"]
      }
    },
    {
      name: "mobile-chrome-portrait",
      use: {
        ...devices["Pixel 7"]
      }
    },
    {
      name: "mobile-safari-portrait",
      use: {
        ...devices["iPhone 14"]
      }
    },
    {
      name: "mobile-safari-landscape",
      use: {
        ...devices["iPhone 14 landscape"]
      }
    },
    {
      name: "webkit-tablet-portrait",
      use: {
        ...devices["iPad Pro 11"]
      }
    },
    {
      name: "webkit-tablet-landscape",
      use: {
        ...devices["iPad Pro 11 landscape"]
      }
    }
  ]
});
