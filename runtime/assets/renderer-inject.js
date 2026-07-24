(function harleyCodexSkin() {
  if (!document.head || !document.body) {
    const retry = () => window.requestAnimationFrame(harleyCodexSkin);
    if (document.readyState === "loading") {
      document.addEventListener("DOMContentLoaded", retry, { once: true });
    } else {
      retry();
    }
    return;
  }

  const rootId = "harley-codex-skin-root";
  const styleId = "harley-codex-skin-style";
  const sidebarAttr = "data-harley-sidebar";
  const softCardAttr = "data-harley-soft-card";
  const softCardIndexAttr = "data-harley-card-index";
  const softCardCountAttr = "data-harley-card-count";
  const composerAttr = "data-harley-composer";
  const composerDockAttr = "data-harley-composer-dock";
  const composerStuckAttr = "data-harley-composer-stuck";
  const bottomFadeAttr = "data-harley-bottom-fade";
  const sidebarActiveAttr = "data-harley-sidebar-active";
  const topStripAttr = "data-harley-top-strip";
  const settingsPanelAttr = "data-harley-settings-panel";
  const settingsSidebarAttr = "data-harley-settings-sidebar";
  const softenedAttr = "data-harley-softened";
  const observerKey = "__HARLEY_CODEX_SKIN_OBSERVER__";
  const clickKey = "__HARLEY_CODEX_SKIN_CLICK__";
  const mediaQueryKey = "__HARLEY_CODEX_SKIN_MEDIA_QUERY__";
  const mediaChangeKey = "__HARLEY_CODEX_SKIN_MEDIA_CHANGE__";
  const settingsModeKey = "harleySettingsMode";
  const perfStorageKey = "harleyPerf";
  const passChrome = 1;
  const passSettings = 2;
  const passWelcome = 4;
  const passComposer = 8;
  const passSoften = 16;
  const passFull = passChrome | passSettings | passWelcome | passComposer | passSoften;
  let welcomeTitleElement = null;
  let followUpPolishTimers = [];
  let perfSamples = [];
  let perfLastReportAt = 0;

  function removeExisting() {
    if (window[observerKey]) {
      window[observerKey].disconnect();
      delete window[observerKey];
    }
    if (window[clickKey]) {
      document.removeEventListener("click", window[clickKey], true);
      delete window[clickKey];
    }
    if (window[mediaQueryKey] && window[mediaChangeKey]) {
      window[mediaQueryKey].removeEventListener?.("change", window[mediaChangeKey]);
      delete window[mediaQueryKey];
      delete window[mediaChangeKey];
    }
    document.querySelectorAll(`[${sidebarAttr}], [${softCardAttr}], [${composerAttr}], [${composerDockAttr}], [${composerStuckAttr}], [${bottomFadeAttr}], [${sidebarActiveAttr}], [${topStripAttr}], [${settingsPanelAttr}], [${settingsSidebarAttr}], [${softenedAttr}]`).forEach((element) => {
      element.style.removeProperty("background");
      element.style.removeProperty("background-color");
      element.style.removeProperty("background-image");
      element.style.removeProperty("backdrop-filter");
      element.style.removeProperty("-webkit-backdrop-filter");
      element.style.removeProperty("box-shadow");
      element.style.removeProperty("border-color");
      element.style.removeProperty("border-top-color");
      element.style.removeProperty("border-bottom-color");
      element.style.removeProperty("transform");
      element.removeAttribute(sidebarAttr);
      element.removeAttribute(softCardAttr);
      element.removeAttribute(softCardIndexAttr);
      element.removeAttribute(softCardCountAttr);
      element.style.removeProperty("--harley-card-index");
      element.style.removeProperty("--harley-card-count");
      element.style.removeProperty("--harley-card-pos");
      element.style.removeProperty("--harley-card-bg-size");
      element.style.removeProperty("--harley-card-offset-x");
      element.style.removeProperty("--harley-card-group-width");
      element.removeAttribute(composerAttr);
      element.removeAttribute(composerDockAttr);
      element.removeAttribute(composerStuckAttr);
      element.removeAttribute(bottomFadeAttr);
      element.removeAttribute(sidebarActiveAttr);
      element.removeAttribute(topStripAttr);
      element.removeAttribute(settingsPanelAttr);
      element.removeAttribute(settingsSidebarAttr);
      element.removeAttribute(softenedAttr);
    });
    document.getElementById(rootId)?.remove();
    document.getElementById(styleId)?.remove();
    document.documentElement.classList.remove("harley-codex-skin");
    delete document.documentElement.dataset.harleyCodexSkin;
    delete document.documentElement.dataset[settingsModeKey];
  }

  if (window.__HARLEY_CODEX_SKIN_RESTORE__) {
    removeExisting();
    return;
  }

  const payload = window.__HARLEY_CODEX_SKIN_PAYLOAD__ || {};
  const css = payload.css || "";
  const backgrounds = payload.backgrounds || { fallback: payload.background || "" };
  const theme = payload.theme || {};
  const art = theme.art || {};
  const welcome = theme.welcome || {};
  const effects = theme.effects || {};
  const colors = theme.colors || {};

  removeExisting();

  const style = document.createElement("style");
  style.id = styleId;
  style.textContent = css;
  document.head.appendChild(style);

  const root = document.createElement("div");
  root.id = rootId;
  root.setAttribute("aria-hidden", "true");
  root.innerHTML = [
    '<div class="harley-codex-backdrop"></div>',
    '<div class="harley-codex-workspace-veil"></div>',
    '<div class="harley-codex-top-veil"></div>',
    '<div class="harley-codex-left-veil"></div>',
    '<div class="harley-codex-bottom-veil"></div>'
  ].join("");
  document.body.prepend(root);

  const x = Number.isFinite(art.focusX) ? Math.round(art.focusX * 100) : 72;
  const y = Number.isFinite(art.focusY) ? Math.round(art.focusY * 100) : 45;
  function applyAppearance() {
    const configured = theme.appearance || "auto";
    const prefersDark = window.matchMedia?.("(prefers-color-scheme: dark)").matches;
    const mode = configured === "auto" ? (prefersDark ? "dark" : "light") : configured;
    const background = backgrounds[mode] || backgrounds.fallback || backgrounds.light || backgrounds.dark || "";
    const cardBackground = backgrounds.cards || backgrounds.card || welcome.cardsImage || welcome.cardImage || background;
    document.documentElement.dataset.harleyAppearance = mode;
    document.documentElement.style.setProperty("--harley-background", `url("${background}")`);
    document.documentElement.style.setProperty("--harley-card-background", `url("${cardBackground}")`);
  }

  applyAppearance();
  const colorSchemeQuery = window.matchMedia?.("(prefers-color-scheme: dark)");
  colorSchemeQuery?.addEventListener?.("change", applyAppearance);
  window[mediaQueryKey] = colorSchemeQuery;
  window[mediaChangeKey] = applyAppearance;
  document.documentElement.style.setProperty("--harley-bg-x", `${x}%`);
  document.documentElement.style.setProperty("--harley-bg-y", `${y}%`);
  document.documentElement.style.setProperty("--harley-accent", colors.accent || "#4BAFD4");
  document.documentElement.style.setProperty("--harley-accent-warm", colors.accentWarm || colors.accent || "#4BAFD4");
  document.documentElement.style.setProperty("--harley-accent-cool", colors.accentCool || colors.accent || "#7EC8DC");
  document.documentElement.style.setProperty("--harley-accent-deep", colors.accentDeep || colors.accent || "#143243");
  document.documentElement.style.setProperty("--harley-accent-mist", colors.accentMist || "rgba(75, 175, 212, 0.1)");
  document.documentElement.style.setProperty("--harley-accent-soft", colors.accentSoft || "rgba(75, 175, 212, 0.22)");
  document.documentElement.style.setProperty("--harley-accent-text", colors.accentText || "#143243");
  document.documentElement.dataset.harleyEffects = effects.material || "balanced";
  document.documentElement.classList.add("harley-codex-skin");
  document.documentElement.dataset.harleyCodexSkin = "active";

  function markSidebar() {
    const existing = document.querySelector(`[${sidebarAttr}]`);
    if (existing?.isConnected) return;

    let best = null;
    let bestHeight = 0;
    document.querySelectorAll("#root aside, #root nav, #root [role='navigation'], #root > div > div").forEach((element) => {
      const rect = element.getBoundingClientRect();
      if (rect.left > 24 || rect.top > 120) return;
      if (rect.width < 220 || rect.width > Math.min(420, innerWidth * 0.36)) return;
      if (rect.height < innerHeight * 0.72) return;
      const text = element.textContent || "";
      if (!/新建任务|拉取请求|站点|项目|插件/.test(text)) return;
      if (rect.height > bestHeight) {
        best = element;
        bestHeight = rect.height;
      }
    });
    best?.setAttribute(sidebarAttr, "true");
  }

  function markSidebarActiveItems() {
    const sidebar = document.querySelector(`[${sidebarAttr}]`);
    if (!sidebar) return;

    const activeElements = new Set();
    sidebar.querySelectorAll("button, a, li, [role='button'], [aria-current], [aria-selected='true'], [data-state], [class*='bg-'], [class*='active'], [class*='selected']").forEach((element) => {
      const rect = element.getBoundingClientRect();
      if (rect.width < 120 || rect.height < 22 || rect.height > 58) return;
      const className = String(element.className || "");
      const hasStateClass = /\bbg-|active|selected|current/i.test(className);
      const activeByState = element.getAttribute("aria-current") || element.getAttribute("aria-selected") === "true" || element.getAttribute("data-state") === "active";
      if (activeByState || hasStateClass) {
        activeElements.add(element);
        if (element.getAttribute(sidebarActiveAttr) !== "true") {
          element.setAttribute(sidebarActiveAttr, "true");
        }
      }
    });
    sidebar.querySelectorAll(`[${sidebarActiveAttr}]`).forEach((element) => {
      if (!activeElements.has(element)) element.removeAttribute(sidebarActiveAttr);
    });
  }

  function markTopStrips() {
    document.querySelectorAll(`[${topStripAttr}]`).forEach((element) => {
      element.removeAttribute(topStripAttr);
    });

    const sidebar = document.querySelector(`[${sidebarAttr}]`);
    const sidebarRight = sidebar?.getBoundingClientRect().right || 260;
    const mainLeft = Math.max(220, sidebarRight - 8);
    document.querySelectorAll("#root header, #root main > *, #root [role='main'] > *, #root [class*='top'], #root [class*='Top']").forEach((element) => {
      if (element.closest(`[${sidebarAttr}], [role="dialog"], [role="menu"], [role="listbox"], [${composerAttr}]`)) return;
      const rect = element.getBoundingClientRect();
      if (rect.left < mainLeft || rect.width < Math.min(520, innerWidth * 0.38)) return;
      if (rect.top < 24 || rect.top > 145 || rect.height < 16 || rect.height > 104) return;

      const style = getComputedStyle(element);
      const hasPaint =
        style.backgroundImage !== "none" ||
        !/^rgba?\(0,\s*0,\s*0,\s*0\)$/.test(style.backgroundColor) ||
        style.boxShadow !== "none" ||
        style.borderTopColor !== "rgba(0, 0, 0, 0)" ||
        style.borderBottomColor !== "rgba(0, 0, 0, 0)";
      const looksLikeTopChrome = rect.top < 96 && rect.height <= 88;
      if (hasPaint || looksLikeTopChrome) element.setAttribute(topStripAttr, "true");
    });
  }

  function textMatchesSettingsSidebar(text) {
    return /返回应用|搜索设置|Back to app|Search settings/i.test(text) &&
      /常规|个人资料|外观|语言|配置|个性化|宠物|键盘快捷键|使用情况和计费|账户|插件|浏览器|电脑操控|钩子|连接|Git|环境|工作树|General|Profile|Appearance|Hooks|Connectors|Environment|Worktree/i.test(text);
  }

  function looksLikeSettingsSidebar(element) {
    if (!element || element.nodeType !== Node.ELEMENT_NODE) return false;
    const rect = element.getBoundingClientRect();
    if (rect.left > Math.min(320, innerWidth * 0.32) || rect.top > 180) return false;
    if (rect.width < 150 || rect.width > 390 || rect.height < innerHeight * 0.45) return false;
    return textMatchesSettingsSidebar(element.textContent || "");
  }

  function collectSettingsSidebarCandidates() {
    const candidates = new Set();
    document.querySelectorAll(`[${settingsSidebarAttr}], .app-shell-left-panel, aside, nav, [role='navigation'], [aria-label='设置'], [aria-label='Settings']`).forEach((element) => {
      candidates.add(element);
    });

    const sampleBottom = Math.max(220, Math.min(innerHeight - 72, 560));
    [[18, 60], [24, 96], [28, 148], [32, 228], [112, 136], [220, 168], [32, sampleBottom]].forEach(([x, y]) => {
      let element = document.elementFromPoint(x, y);
      for (let depth = 0; element && depth < 9; depth += 1) {
        candidates.add(element);
        element = element.parentElement;
      }
    });

    return [...candidates].filter(looksLikeSettingsSidebar);
  }

  function markSettingsPanels() {
    document.querySelectorAll(`[${settingsPanelAttr}]`).forEach((element) => {
      element.removeAttribute(settingsPanelAttr);
    });
    document.querySelectorAll(`[${settingsSidebarAttr}]`).forEach((element) => {
      element.removeAttribute(settingsSidebarAttr);
    });

    const sidebar = document.querySelector(`[${sidebarAttr}]`);
    const sidebarRight = sidebar?.getBoundingClientRect().right || 240;
    const settingsPattern = /返回应用|搜索设置|个人资料|外观|语言|配置|个性化|宠物|键盘快捷键|使用情况和计费|账户|插件|浏览器|电脑操控|钩子|连接|Git|环境|工作树|默认权限|自动审核|完全访问权限|默认文件打开目标|集成终端|Shell|Settings|General|Appearance|Keyboard shortcuts|Integrations|Hooks|Connectors|Environment|Worktree|Default permissions/i;
    const leftSettingsRegions = collectSettingsSidebarCandidates();
    const inSettingsMode = leftSettingsRegions.length > 0;
    if (inSettingsMode) {
      document.documentElement.dataset[settingsModeKey] = "true";
      let settingsSidebar = null;
      let settingsSidebarHeight = 0;
      leftSettingsRegions.forEach((element) => {
        const height = element.getBoundingClientRect().height;
        if (height > settingsSidebarHeight) {
          settingsSidebar = element;
          settingsSidebarHeight = height;
        }
      });
      settingsSidebar?.setAttribute(settingsSidebarAttr, "true");
    } else {
      delete document.documentElement.dataset[settingsModeKey];
      return;
    }

    document.querySelectorAll("#root main, #root [role='main'], #root main > *, #root [role='main'] > *, #root [class*='surface'], #root [class*='Surface'], #root [class*='content'], #root [class*='Content']").forEach((element) => {
      if (element.closest(`[${sidebarAttr}], [role="dialog"], [role="menu"], [role="listbox"], [${composerAttr}]`)) return;
      const rect = element.getBoundingClientRect();
      if (rect.left < sidebarRight - 8 || rect.top < 48 || rect.width < innerWidth * 0.48 || rect.height < innerHeight * 0.55) return;
      const text = (element.textContent || "").replace(/\s+/g, " ").trim();
      const looksLikeSettings = inSettingsMode || settingsPattern.test(text);
      if (looksLikeSettings) element.setAttribute(settingsPanelAttr, "true");
    });
  }

  function markSoftCards() {
    document.querySelectorAll(`#root [${softCardAttr}], #root [${composerAttr}], #root [${composerDockAttr}], #root [${composerStuckAttr}], #root [${bottomFadeAttr}]`).forEach((element) => {
      element.removeAttribute(softCardAttr);
      element.removeAttribute(composerAttr);
      element.removeAttribute(composerDockAttr);
      element.removeAttribute(composerStuckAttr);
      element.removeAttribute(bottomFadeAttr);
    });

    const surface = document.querySelector("#root .composer-surface-chrome");
    let composer = findComposerShell(surface);
    if (!composer) {
      for (const element of document.querySelectorAll("#root textarea, #root input, #root [contenteditable='true']")) {
        composer = findComposerShell(element);
        if (composer) break;
      }
    }
    if (composer) {
      composer.setAttribute(composerAttr, "true");
      markComposerDock(composer);
      markBottomFades(composer);
    }

    if (!document.querySelector("[data-harley-welcome-title='true']")) return;
    const actionCards = [];
    document.querySelectorAll("#root button, #root [role='button']").forEach((element) => {
      if (element.closest(`[${sidebarAttr}], [role="dialog"], [role="menu"], [role="listbox"], [${composerAttr}]`)) return;
      const rect = element.getBoundingClientRect();
      if (rect.left < 260 || rect.top < innerHeight * 0.34 || rect.top > innerHeight * 0.72) return;
      const text = (element.textContent || "").trim();
      if (text && text.length < 90 && rect.width > 120 && rect.width < 280 && rect.height >= 56 && rect.height < 150) {
        actionCards.push({ element, left: rect.left });
      }
    });
    const orderedCards = actionCards.sort((a, b) => a.left - b.left);
    const groupLeft = Math.min(...orderedCards.map((card) => card.left));
    const groupRight = Math.max(...orderedCards.map((card) => card.left + card.element.getBoundingClientRect().width));
    const groupWidth = Math.max(1, groupRight - groupLeft);
    orderedCards
      .forEach(({ element, left }, index, cards) => {
        const rect = element.getBoundingClientRect();
        element.setAttribute(softCardAttr, "true");
        element.setAttribute(softCardIndexAttr, String(index));
        element.setAttribute(softCardCountAttr, String(cards.length));
        element.style.setProperty("--harley-card-index", String(index));
        element.style.setProperty("--harley-card-count", String(cards.length));
        element.style.setProperty("--harley-card-pos", `${cards.length > 1 ? (index / (cards.length - 1)) * 100 : 50}%`);
        element.style.setProperty("--harley-card-bg-size", `${groupWidth}px 100%`);
        element.style.setProperty("--harley-card-group-width", `${groupWidth}px`);
        element.style.setProperty("--harley-card-offset-x", `${groupLeft - left}px`);
        element.style.setProperty("--harley-card-local-width", `${rect.width}px`);
      });
  }

  function findComposerShell(seed) {
    if (!seed) return null;

    let current = seed;
    let best = null;
    for (let i = 0; i < 9 && current; i += 1) {
      const rect = current.getBoundingClientRect();
      const text = current.textContent || "";
      const hasInput = current.querySelector?.("textarea, input, [contenteditable='true'], .composer-surface-chrome");
      const nearBottom = rect.top > innerHeight * 0.52;
      const shellSized = rect.width > Math.min(520, innerWidth * 0.42) && rect.width < innerWidth * 0.9 && rect.height >= 68 && rect.height <= 260;
      const style = getComputedStyle(current);
      const isDock = /sticky|fixed/.test(style.position) || /\bbottom-0\b/.test(String(current.className || ""));
      const looksLikeComposer = current.matches?.("form, [class*='composer'], [class*='Composer'], [class*='input'], [class*='Input'], [class*='surface'], [class*='Surface']");
      if (!isDock && hasInput && nearBottom && shellSized && looksLikeComposer && !/Full access is on|ChatGPT will be able/i.test(text)) {
        best = current;
      }
      current = current.parentElement;
    }
    return best || seed.parentElement?.closest("form") || seed;
  }

  function markComposerDock(composer) {
    if (document.querySelector("[data-harley-welcome-title='true']")) return;
    const composerRect = composer.getBoundingClientRect();
    const bottomGap = innerHeight - composerRect.bottom;
    if (bottomGap > 36) return;

    let current = composer.parentElement;
    let bestDock = null;
    for (let i = 0; i < 6 && current; i += 1) {
      if (current.matches?.("html, body, #root, main, [role='main']")) break;
      const rect = current.getBoundingClientRect();
      const style = getComputedStyle(current);
      const nearBottom = innerHeight - rect.bottom <= 36;
      const wrapsComposer = rect.width >= composerRect.width && rect.height >= composerRect.height;
      const isScrollContainer = /auto|scroll/.test(style.overflowY);
      const dockLike = !isScrollContainer && (
        /sticky|fixed|absolute/.test(style.position) ||
        /bottom|composer|chat/i.test(String(current.className || ""))
      );
      if (nearBottom && wrapsComposer && dockLike) bestDock = current;
      current = current.parentElement;
    }
    if (bestDock) {
      bestDock.setAttribute(composerDockAttr, "true");
      bestDock.setAttribute(composerStuckAttr, "true");
    } else {
      composer.setAttribute(composerStuckAttr, "true");
    }
  }

  function markBottomFades(composer) {
    const composerRect = composer.getBoundingClientRect();
    const candidates = [
      ...document.querySelectorAll("#root [class*='bg-gradient-to-t'], #root [class*='from-token-main-surface-primary'], #root [class*='to-transparent']")
    ];
    candidates.forEach((element) => {
      if (element.closest(`[${composerAttr}]`)) return;
      const rect = element.getBoundingClientRect();
      if (rect.width < composerRect.width * 0.75 || rect.height < 16) return;
      if (rect.bottom < composerRect.top - 60 || rect.top > innerHeight) return;
      const style = getComputedStyle(element);
      if (!/gradient/i.test(style.backgroundImage)) return;
      element.setAttribute(bottomFadeAttr, "true");
    });
  }

  function applyWelcomeTitle() {
    const title = String(welcome.title || "").trim();
    if (welcomeTitleElement?.isConnected && welcomeTitleElement.textContent === title) return;
    document.querySelectorAll("[data-harley-welcome-title='true']").forEach((element) => {
      element.removeAttribute("data-harley-welcome-title");
    });
    welcomeTitleElement = null;
    if (!title) return;

    const getDirectText = (element) => [...element.childNodes]
      .filter((node) => node.nodeType === Node.TEXT_NODE)
      .map((node) => node.textContent || "")
      .join(" ")
      .replace(/\s+/g, " ")
      .trim();
    let target = null;
    let targetArea = Number.POSITIVE_INFINITY;
    document.querySelectorAll("#root h1, #root h2, #root [class*='text-'], #root div, #root span").forEach((element) => {
      const directText = getDirectText(element);
      const text = (directText || element.textContent || "").replace(/\s+/g, " ").trim();
      if (!text || text.length > 80) return;
      if (element.closest(`[${sidebarAttr}], [role="dialog"], [role="menu"], [role="listbox"], [${composerAttr}]`)) return;
      const looksLikeWelcomeTitle =
        /我们.*(应该|要|可以)?.*(构建|做|创建|开发).*什么/i.test(text) ||
        /What\s+(should|can)\s+we\s+(build|make|create|do)/i.test(text) ||
        /What.*(build|make|create|do)/i.test(text);
      if (!looksLikeWelcomeTitle) return;
      const rect = element.getBoundingClientRect();
      if (rect.left < 260 || rect.top < 96 || rect.top > innerHeight * 0.74 || rect.height < 20 || rect.height > 110) return;
      if (rect.width < 160 || rect.width > Math.min(760, innerWidth * 0.66)) return;
      const style = getComputedStyle(element);
      const fontSize = parseFloat(style.fontSize) || 0;
      if (fontSize < 20 && !/text-(2xl|3xl|4xl|5xl|xl)/.test(String(element.className || ""))) return;
      const area = rect.width * rect.height;
      if (area < targetArea) {
        target = element;
        targetArea = area;
      }
    });

    if (target) {
      target.textContent = title;
      target.setAttribute("data-harley-welcome-title", "true");
      welcomeTitleElement = target;
    }
  }

  function softenMarkedControls() {
    document.querySelectorAll(`[${softCardAttr}] > *:not([${softenedAttr}])`).forEach((element) => {
      element.style.setProperty("background", "transparent", "important");
      element.style.setProperty("background-color", "transparent", "important");
      element.style.setProperty("background-image", "none", "important");
      element.style.setProperty("box-shadow", "none", "important");
      element.setAttribute(softenedAttr, "true");
    });

    document.querySelectorAll(`[${composerAttr}] textarea:not([${softenedAttr}]), [${composerAttr}] input:not([${softenedAttr}]), [${composerAttr}] [contenteditable="true"]:not([${softenedAttr}])`).forEach((element) => {
      element.style.setProperty("background", "transparent", "important");
      element.style.setProperty("background-color", "transparent", "important");
      element.style.setProperty("background-image", "none", "important");
      element.style.setProperty("box-shadow", "none", "important");
      element.setAttribute(softenedAttr, "true");
    });
  }

  function perfEnabled() {
    try {
      return localStorage.getItem(perfStorageKey) === "1" || window.__HARLEY_CODEX_SKIN_PERF__ === true;
    } catch {
      return window.__HARLEY_CODEX_SKIN_PERF__ === true;
    }
  }

  function passNames(passes) {
    const names = [];
    if (passes & passChrome) names.push("chrome");
    if (passes & passSettings) names.push("settings");
    if (passes & passWelcome) names.push("welcome");
    if (passes & passComposer) names.push("composer");
    if (passes & passSoften) names.push("soften");
    return names;
  }

  function measuredStep(name, rows, fn) {
    const start = performance.now();
    fn();
    rows.push({ name, ms: performance.now() - start });
  }

  function reportPerf(passes, rows, totalMs) {
    const now = performance.now();
    window.__HARLEY_CODEX_SKIN_PERF_LAST__ = {
      passes: passNames(passes),
      totalMs: Number(totalMs.toFixed(2)),
      rows: rows.map((row) => ({ name: row.name, ms: Number(row.ms.toFixed(2)) }))
    };
    perfSamples.push(...rows);
    if (now - perfLastReportAt < 1200) return;

    const grouped = new Map();
    perfSamples.forEach((row) => {
      const current = grouped.get(row.name) || { pass: row.name, count: 0, total: 0, max: 0 };
      current.count += 1;
      current.total += row.ms;
      current.max = Math.max(current.max, row.ms);
      grouped.set(row.name, current);
    });
    const summary = [...grouped.values()].map((row) => ({
      pass: row.pass,
      count: row.count,
      avgMs: Number((row.total / row.count).toFixed(2)),
      maxMs: Number(row.max.toFixed(2))
    }));
    console.groupCollapsed(`[Harley Skin Perf] ${passNames(passes).join(", ")} total ${totalMs.toFixed(2)}ms`);
    console.table(summary);
    console.groupEnd();
    perfSamples = [];
    perfLastReportAt = now;
  }

  function polishLayout(passes = passFull) {
    if (!perfEnabled()) {
      if (passes & passChrome) {
        markSidebar();
        markSidebarActiveItems();
        markTopStrips();
      }
      if (passes & passSettings) markSettingsPanels();
      if (passes & passWelcome) applyWelcomeTitle();
      if (passes & passComposer) markSoftCards();
      if (passes & passSoften) softenMarkedControls();
      return;
    }

    const rows = [];
    const start = performance.now();
    if (passes & passChrome) {
      measuredStep("sidebar", rows, markSidebar);
      measuredStep("sidebar-active", rows, markSidebarActiveItems);
      measuredStep("top-strips", rows, markTopStrips);
    }
    if (passes & passSettings) measuredStep("settings", rows, markSettingsPanels);
    if (passes & passWelcome) measuredStep("welcome", rows, applyWelcomeTitle);
    if (passes & passComposer) measuredStep("composer", rows, markSoftCards);
    if (passes & passSoften) measuredStep("soften", rows, softenMarkedControls);
    reportPerf(passes, rows, performance.now() - start);
  }

  let polishTimer = 0;
  let lastPolishAt = 0;
  let pendingPolishPasses = 0;
  function queuePolish(urgent = false, passes = passFull) {
    pendingPolishPasses |= passes;
    clearTimeout(polishTimer);
    const delay = urgent ? 16 : Math.max(80, 320 - (Date.now() - lastPolishAt));
    polishTimer = window.setTimeout(() => {
      const run = () => {
        const passesToRun = pendingPolishPasses || passFull;
        pendingPolishPasses = 0;
        lastPolishAt = Date.now();
        polishLayout(passesToRun);
      };
      if (!urgent && "requestIdleCallback" in window) {
        window.requestIdleCallback(run, { timeout: 700 });
      } else {
        window.requestAnimationFrame(run);
      }
    }, delay);
  }

  function scheduleFollowUpPolish() {
    followUpPolishTimers.forEach((timer) => clearTimeout(timer));
    followUpPolishTimers = [90, 350, 800].map((delay) => window.setTimeout(() => {
      queuePolish(delay <= 120, passChrome | passSettings | passSoften);
    }, delay));
  }

  function getMutationPolishPasses(mutations) {
    let passes = 0;
    const settingsPattern = /返回应用|搜索设置|默认权限|自动审核|完全访问权限|默认文件打开目标|外观|键盘快捷键|钩子|连接|Git|环境|工作树|插件|浏览器|Settings|General|Appearance|Hooks|Connectors|Environment|Worktree/i;
    const welcomePattern = /What.*(build|make|create|do)|我们.*(构建|做|创建|开发).*什么/;
    const sidebarPattern = /新建任务|项目|插件|拉取请求|站点/;

    const addPassesForNode = (node) => {
      if (node.nodeType !== Node.ELEMENT_NODE) return;
      if (node === document.body || node === document.documentElement || node.id === "root") {
        passes |= passChrome | passWelcome | passComposer;
        return;
      }
      if (node.matches?.("aside, header, nav, [role='navigation']") || node.querySelector?.("aside, header, nav, [role='navigation']")) {
        passes |= passChrome;
      }
      if (node.matches?.("form, textarea, input, [contenteditable='true']") || node.querySelector?.("form, textarea, input, [contenteditable='true']")) {
        passes |= passComposer | passSoften;
      }
      if (node.matches?.("main, [role='main'], button, a, [role='button']") || node.querySelector?.("main, [role='main'], button, a, [role='button']")) {
        passes |= passComposer | passWelcome;
      }
      const text = node.textContent || "";
      if (settingsPattern.test(text)) passes |= passChrome | passSettings | passSoften;
      if (welcomePattern.test(text)) passes |= passWelcome | passComposer | passSoften;
      if (sidebarPattern.test(text)) passes |= passChrome;
    };

    for (const mutation of mutations) {
      const target = mutation.target;
      if (target?.closest?.(`#${rootId}, [${composerAttr}], [${composerDockAttr}]`)) continue;
      addPassesForNode(target);
      for (const node of mutation.addedNodes) {
        addPassesForNode(node);
      }
      for (const node of mutation.removedNodes) {
        addPassesForNode(node);
      }
    }
    return passes;
  }

  function eventLooksLikeSettings(event) {
    const target = event.target?.closest?.("button, a, [role='button'], [role='tab'], [aria-label], [data-state], [class*='settings'], [class*='Settings']");
    const text = `${target?.textContent || ""} ${target?.getAttribute?.("aria-label") || ""}`;
    return /返回应用|搜索设置|个人资料|外观|语言|配置|个性化|宠物|键盘快捷键|使用情况和计费|账户|插件|浏览器|电脑操控|钩子|连接|Git|环境|工作树|Settings|Hooks|Connectors|Environment|Worktree/i.test(text);
  }

  polishLayout();
  setTimeout(polishLayout, 600);
  window[observerKey] = new MutationObserver((mutations) => {
    const passes = getMutationPolishPasses(mutations);
    if (passes) queuePolish(Boolean(passes & passSettings), passes);
  });
  window[observerKey].observe(document.body, {
    childList: true,
    subtree: true
  });
  window[clickKey] = (event) => {
    const urgent = eventLooksLikeSettings(event);
    queuePolish(urgent, urgent ? passChrome | passSettings | passSoften : passChrome | passWelcome | passComposer | passSoften);
    if (urgent) {
      scheduleFollowUpPolish();
    }
  };
  document.addEventListener("click", window[clickKey], true);
})();
