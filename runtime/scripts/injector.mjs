#!/usr/bin/env node
import fs from "node:fs";
import path from "node:path";

const args = new Map();
for (let i = 2; i < process.argv.length; i += 2) {
  args.set(process.argv[i], process.argv[i + 1] ?? "true");
}

const port = Number(args.get("--port") || 9335);
const root = path.resolve(args.get("--root") || process.cwd());
const mode = args.get("--mode") || "apply";

const assetsDir = path.join(root, "assets");
const cssPath = path.join(assetsDir, "harley-skin.css");
const jsPath = path.join(assetsDir, "renderer-inject.js");
const themePath = path.join(assetsDir, "theme.json");

function readText(file) {
  return fs.readFileSync(file, "utf8");
}

function readTheme() {
  return JSON.parse(readText(themePath));
}

function readBackgroundFile(name) {
  const file = path.join(assetsDir, name);
  if (!fs.existsSync(file)) return null;
  const ext = path.extname(name).toLowerCase();
  const mime = ext === ".svg" ? "image/svg+xml" : ext === ".png" ? "image/png" : "image/jpeg";
  const encoded = fs.readFileSync(file).toString("base64");
  return `data:${mime};base64,${encoded}`;
}

function pickFallbackBackground() {
  const candidates = ["background.jpg", "background.png", "background.svg"];
  for (const name of candidates) {
    const background = readBackgroundFile(name);
    if (background) return background;
  }
  throw new Error("No background asset found.");
}

function pickBackgrounds(theme) {
  const config = theme.backgrounds || {};
  const configuredLight = readBackgroundFile(config.light || "");
  const configuredDark = readBackgroundFile(config.dark || "");
  const fallback = readBackgroundFile(config.fallback || "") || configuredLight || configuredDark || pickFallbackBackground();
  const cards =
    readBackgroundFile(theme.welcome?.cardsImage || "") ||
    readBackgroundFile(theme.welcome?.cardImage || "") ||
    readBackgroundFile(config.cards || "") ||
    readBackgroundFile(config.card || "");
  return {
    light: configuredLight || fallback,
    dark: configuredDark || fallback,
    fallback,
    cards: cards || fallback
  };
}

function buildExpression() {
  if (mode === "status") {
    return `
      (() => {
        const root = document.getElementById("harley-codex-skin-root");
        const style = document.getElementById("harley-codex-skin-style");
        return {
          active: document.documentElement.dataset.harleyCodexSkin === "active",
          rootExists: Boolean(root),
          styleExists: Boolean(style),
          url: location.href,
          title: document.title
        };
      })()
    `;
  }

  if (mode === "restore") {
    return `
      window.__HARLEY_CODEX_SKIN_RESTORE__ = true;
      ${readText(jsPath)}
      delete window.__HARLEY_CODEX_SKIN_RESTORE__;
    `;
  }

  const theme = readTheme();
  const payload = {
    css: readText(cssPath),
    backgrounds: pickBackgrounds(theme),
    theme
  };

  return `
    window.__HARLEY_CODEX_SKIN_PAYLOAD__ = ${JSON.stringify(payload)};
    ${readText(jsPath)}
  `;
}

async function getTargets() {
  const response = await fetch(`http://127.0.0.1:${port}/json/list`);
  if (!response.ok) {
    throw new Error(`CDP target list failed: ${response.status}`);
  }
  const targets = await response.json();
  return targets.filter((target) => target.webSocketDebuggerUrl && target.type === "page");
}

function send(ws, method, params = {}) {
  const id = send.nextId++;
  ws.send(JSON.stringify({ id, method, params }));
  return new Promise((resolve, reject) => {
    const timeout = setTimeout(() => reject(new Error(`${method} timed out`)), 8000);
    function onMessage(event) {
      const raw = event?.data ?? event;
      const text = typeof raw === "string" ? raw : Buffer.from(raw).toString("utf8");
      const message = JSON.parse(text);
      if (message.id !== id) return;
      clearTimeout(timeout);
      ws.removeEventListener("message", onMessage);
      if (message.error) reject(new Error(message.error.message));
      else resolve(message.result);
    }
    ws.addEventListener("message", onMessage);
  });
}
send.nextId = 1;

async function injectTarget(target, expression) {
  const ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((resolve, reject) => {
    ws.addEventListener("open", resolve, { once: true });
    ws.addEventListener("error", reject, { once: true });
  });
  await send(ws, "Runtime.enable");
  await send(ws, "Page.enable").catch(() => {});
  if (mode === "apply") {
    await send(ws, "Page.addScriptToEvaluateOnNewDocument", { source: expression }).catch(() => {});
  }
  const result = await send(ws, "Runtime.evaluate", {
    expression,
    awaitPromise: mode === "status",
    returnByValue: mode === "status",
    userGesture: false
  });
  ws.close();
  if (result.exceptionDetails) {
    const detail = result.exceptionDetails.exception?.description || result.exceptionDetails.text || "Runtime.evaluate failed";
    throw new Error(detail);
  }
  return result;
}

async function main() {
  const expression = buildExpression();
  const deadline = Date.now() + 30000;
  let lastError;

  while (Date.now() < deadline) {
    try {
      const targets = await getTargets();
      if (targets.length > 0) {
        const results = await Promise.all(targets.map((target) => injectTarget(target, expression)));
        if (mode === "status") {
          console.log(JSON.stringify(results.map((result, index) => ({
            target: targets[index].title || targets[index].url,
            status: result.result?.value ?? null
          })), null, 2));
          return;
        }
        console.log(`${mode === "restore" ? "Restored" : "Injected"} ${targets.length} renderer target(s).`);
        return;
      }
    } catch (error) {
      lastError = error;
    }
    await new Promise((resolve) => setTimeout(resolve, 700));
  }

  throw lastError || new Error("No Codex renderer target found.");
}

main().catch((error) => {
  console.error(error.message);
  process.exit(1);
});
