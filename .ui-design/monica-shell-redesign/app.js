(() => {
  "use strict";

  const data = window.monicaDesignData;
  if (!data) {
    return;
  }

  const $ = (selector, root = document) => root.querySelector(selector);
  const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];
  const shell = $("#appShell");
  const mainContent = $("#mainContent");

  const readPreference = (key, fallback) => {
    try {
      return window.localStorage.getItem(key) ?? fallback;
    } catch {
      return fallback;
    }
  };

  const savePreference = (key, value) => {
    try {
      window.localStorage.setItem(key, value);
    } catch {
      // The prototype remains fully usable when storage is blocked.
    }
  };

  const systemPrefersDark = window.matchMedia?.("(prefers-color-scheme: dark)").matches;
  const state = {
    groupId: "module",
    pageId: "module-dashboard",
    tab: "System Overview",
    theme: readPreference("monica-shell-theme", systemPrefersDark ? "dark" : "light"),
    locale: readPreference("monica-shell-locale", "en"),
    collapsed: readPreference("monica-shell-context-collapsed", "false") === "true",
    resultIndex: 0,
    refreshing: false,
    lastFocus: null,
    toastTimer: null,
  };

  const escapeHtml = (value) => String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");

  const getGroup = (groupId = state.groupId) => data.groups.find((group) => group.id === groupId) ?? data.groups[0];
  const getPage = (group = getGroup(), pageId = state.pageId) => group.items.find((item) => item.id === pageId) ?? group.items[0];
  const getProfile = (groupId = state.groupId) => data.profiles[groupId] ?? data.profiles.module;
  const isTablet = () => window.innerWidth >= 768 && window.innerWidth < 1180;
  const isMobile = () => window.innerWidth < 768;

  const refreshIcons = () => {
    window.lucide?.createIcons({ attrs: { "stroke-width": 1.7 } });
  };

  function renderCategoryNavigation() {
    const desktop = $("#categoryRail");
    const mobile = $("#mobileCategoryRail");

    desktop.innerHTML = data.groups.map((group) => `
      <button
        class="category-button focus-ring${group.id === state.groupId ? " is-active" : ""}"
        type="button"
        data-group="${escapeHtml(group.id)}"
        aria-label="${escapeHtml(group.label)}"
        aria-pressed="${group.id === state.groupId}">
        <i data-lucide="${escapeHtml(group.icon)}" aria-hidden="true"></i>
        <span class="category-button__tooltip" aria-hidden="true">${escapeHtml(group.label)}</span>
      </button>
    `).join("");

    mobile.innerHTML = data.groups.map((group) => `
      <button
        class="mobile-category-button focus-ring${group.id === state.groupId ? " is-active" : ""}"
        type="button"
        data-group="${escapeHtml(group.id)}"
        aria-pressed="${group.id === state.groupId}">
        <i data-lucide="${escapeHtml(group.icon)}" aria-hidden="true"></i>
        <span>${escapeHtml(group.shortLabel ?? group.label)}</span>
      </button>
    `).join("");
  }

  function renderContextNavigation() {
    const group = getGroup();
    const page = getPage(group);
    const linkMarkup = (item, mobile = false) => `
      <button
        class="${mobile ? "mobile-context-link" : "context-link"} focus-ring${item.id === page.id ? " is-active" : ""}"
        type="button"
        data-page="${escapeHtml(item.id)}"
        data-group="${escapeHtml(group.id)}"
        ${item.id === page.id ? 'aria-current="page"' : ""}>
        <i data-lucide="${escapeHtml(item.icon)}" aria-hidden="true"></i>
        <span>${escapeHtml(item.label)}</span>
      </button>
    `;

    $("#contextTitle").textContent = group.label;
    $("#contextCount").textContent = String(group.items.length).padStart(2, "0");
    $("#contextNav").innerHTML = group.items.map((item) => linkMarkup(item)).join("");
    $("#mobileContextTitle").textContent = group.label;
    $("#mobileContextCount").textContent = `${group.items.length} ${group.items.length === 1 ? "page" : "pages"}`;
    $("#mobileContextNav").innerHTML = group.items.map((item) => linkMarkup(item, true)).join("");
  }

  function renderTabs(profile) {
    if (!profile.tabs.includes(state.tab)) {
      state.tab = profile.tabs[0];
    }

    $("#pageTabs").innerHTML = profile.tabs.map((tab, index) => `
      <button
        class="page-tab focus-ring${tab === state.tab ? " is-active" : ""}"
        type="button"
        data-tab-target="${escapeHtml(tab)}"
        aria-pressed="${tab === state.tab}">
        ${escapeHtml(tab)} <span class="sr-only">section ${index + 1}</span>
      </button>
    `).join("");

    $$(".saved-view").forEach((button) => {
      const active = button.dataset.tabTarget === state.tab;
      button.classList.toggle("is-active", active);
      button.setAttribute("aria-pressed", String(active));
    });
  }

  function renderMetrics(profile) {
    const iconForTrend = { up: "trending-up", down: "trending-down", steady: "minus" };
    $("#metricGrid").innerHTML = profile.metrics.map((metric, index) => `
      <article class="metric-card" data-index="0${index + 1}">
        <span class="metric-card__label">${escapeHtml(metric.label)}</span>
        <div>
          <strong>${escapeHtml(metric.value)}</strong>
          <span class="metric-card__detail">
            <i data-lucide="${iconForTrend[metric.trend] ?? "minus"}" aria-hidden="true"></i>
            ${escapeHtml(metric.detail)}
          </span>
        </div>
      </article>
    `).join("");
  }

  function renderPosture(profile) {
    const score = Math.min(100, Math.max(0, Number.parseFloat(profile.posture.score) || 0));
    const circumference = 452.39;
    $("#postureLabel").textContent = profile.posture.label;
    $("#postureScore").textContent = profile.posture.score;
    $("#postureSummary").textContent = profile.posture.summary;
    $("#scoreArc").style.strokeDashoffset = String(circumference * (1 - score / 100));
    $(".posture-score").setAttribute("aria-label", `${profile.posture.label} ${profile.posture.score} percent`);
    $("#postureFacts").innerHTML = profile.posture.facts.map(([term, value]) => `
      <div class="posture-fact"><dt>${escapeHtml(term)}</dt><dd>${escapeHtml(value)}</dd></div>
    `).join("");
  }

  function renderLedger(profile) {
    const labels = ["Signal", "Type", "Status", "Reading"];
    $("#ledgerTitle").textContent = profile.ledgerTitle;
    $("#ledgerBody").innerHTML = profile.ledger.map((row) => `
      <tr>${row.map((cell, index) => {
        const isWarning = index === 2 && /warning|indexing|queued|review/i.test(cell);
        const content = index === 2
          ? `<span class="state-label${isWarning ? " state-label--warning" : ""}">${escapeHtml(cell)}</span>`
          : escapeHtml(cell);
        return `<td data-label="${labels[index]}">${content}</td>`;
      }).join("")}</tr>
    `).join("");
  }

  function renderActivity(profile) {
    $("#activityList").innerHTML = profile.activity.map(([title, detail, time]) => `
      <li class="activity-item">
        <span class="activity-item__mark" aria-hidden="true"></span>
        <div>
          <strong>${escapeHtml(title)}</strong>
          <p>${escapeHtml(detail)}</p>
          <time>${escapeHtml(time)} ago</time>
        </div>
      </li>
    `).join("");
  }

  function renderPage() {
    const group = getGroup();
    const page = getPage(group);
    const profile = getProfile(group.id);

    $("#breadcrumbGroup").textContent = group.label;
    $("#breadcrumbPage").textContent = page.label;
    $("#pageEyebrow").textContent = profile.eyebrow;
    $("#pageStatus").className = `status-pill status-pill--${profile.statusTone}`;
    $("#pageStatus").innerHTML = `<span aria-hidden="true"></span>${escapeHtml(profile.status)}`;
    $("#pageTitle").textContent = page.label;
    $("#pageDescription").textContent = page.description;
    $("#mobilePageTitle").textContent = page.label;
    $("#primaryAction").dataset.action = "primary";
    $("#primaryAction span").textContent = profile.action;

    renderTabs(profile);
    renderMetrics(profile);
    renderPosture(profile);
    renderLedger(profile);
    renderActivity(profile);
  }

  function renderNavigationAndPage({ animate = false } = {}) {
    renderCategoryNavigation();
    renderContextNavigation();
    renderPage();
    refreshIcons();

    if (animate) {
      mainContent.classList.remove("is-view-changing");
      void mainContent.offsetWidth;
      mainContent.classList.add("is-view-changing");
      window.setTimeout(() => mainContent.classList.remove("is-view-changing"), 260);
    }
  }

  function renderNotifications() {
    $("#notificationList").innerHTML = data.notifications.map((notification) => `
      <article class="notification-item notification-item--${escapeHtml(notification.tone)}">
        <span class="notification-item__mark" aria-hidden="true"></span>
        <div>
          <strong>${escapeHtml(notification.title)}</strong>
          <p>${escapeHtml(notification.detail)}</p>
          <time>${escapeHtml(notification.time)}</time>
        </div>
      </article>
    `).join("");
  }

  function selectGroup(groupId) {
    const group = getGroup(groupId);
    state.groupId = group.id;
    state.pageId = group.items[0].id;
    state.tab = getProfile(group.id).tabs[0];
    renderNavigationAndPage({ animate: true });

    if (isTablet()) {
      shell.dataset.contextOpen = "true";
    } else if (!isMobile() && state.collapsed) {
      setContextCollapsed(false);
    }
  }

  function selectPage(groupId, pageId) {
    const group = getGroup(groupId);
    const page = getPage(group, pageId);
    state.groupId = group.id;
    state.pageId = page.id;
    state.tab = getProfile(group.id).tabs[0];
    renderNavigationAndPage({ animate: true });
    closeContextOverlay();
    closeMobileNavigation({ restoreFocus: false });
    window.setTimeout(() => mainContent.focus({ preventScroll: true }), 20);
  }

  function selectTab(tab) {
    const profile = getProfile();
    if (!profile.tabs.includes(tab)) {
      return;
    }

    state.tab = tab;
    renderTabs(profile);
    showToast(tab, "Section preview selected.");
  }

  function setContextCollapsed(collapsed) {
    state.collapsed = collapsed;
    shell.dataset.contextCollapsed = String(collapsed);
    savePreference("monica-shell-context-collapsed", String(collapsed));
    const button = $("[data-action='collapse-context']");
    button?.setAttribute("aria-pressed", String(collapsed));
    button?.setAttribute("aria-label", collapsed ? "Expand contextual navigation" : "Collapse contextual navigation");
  }

  function openContextOverlay() {
    if (isTablet()) {
      shell.dataset.contextOpen = "true";
      window.setTimeout(() => $("#contextNav .context-link")?.focus(), 30);
    } else if (!isMobile()) {
      setContextCollapsed(false);
    }
  }

  function closeContextOverlay() {
    shell.dataset.contextOpen = "false";
  }

  function updateTheme() {
    document.body.dataset.theme = state.theme;
    savePreference("monica-shell-theme", state.theme);
    const nextTheme = state.theme === "light" ? "dark" : "light";

    $$('[data-action="theme"]').forEach((button) => {
      button.setAttribute("aria-label", `Switch to ${nextTheme} theme`);
      const icon = $("[data-lucide], svg", button);
      if (icon) {
        icon.setAttribute("data-lucide", state.theme === "light" ? "moon" : "sun");
      }
    });
    refreshIcons();
  }

  function toggleTheme() {
    state.theme = state.theme === "light" ? "dark" : "light";
    updateTheme();
    showToast(`${state.theme === "dark" ? "Ink" : "Parchment"} theme`, "Shell composition updated.");
  }

  function updateLocale() {
    const isEnglish = state.locale === "en";
    $("#localeButton").textContent = isEnglish ? "EN" : "简";
    $$(".mobile-sheet__footer [data-action='locale']").forEach((button) => {
      button.textContent = isEnglish ? "EN / 简" : "简 / EN";
    });
    document.documentElement.lang = isEnglish ? "en" : "zh-CN";
    savePreference("monica-shell-locale", state.locale);
  }

  function toggleLocale() {
    state.locale = state.locale === "en" ? "zh" : "en";
    updateLocale();
    showToast(state.locale === "en" ? "English preview" : "中文预览", "Production uses the registered culture list.");
  }

  function setLayerOpen(layer, open, focusTarget, restoreFocus = true) {
    if (open) {
      state.lastFocus = document.activeElement;
      layer.setAttribute("aria-hidden", "false");
      window.setTimeout(() => focusTarget?.focus(), 20);
      return;
    }

    layer.setAttribute("aria-hidden", "true");
    if (restoreFocus && state.lastFocus instanceof HTMLElement) {
      state.lastFocus.focus({ preventScroll: true });
    }
    state.lastFocus = null;
  }

  function openMobileNavigation() {
    closeFloatingPanels();
    setLayerOpen($("#mobileNavSheet"), true, $("#mobileNavSheet [data-action='close-mobile-nav']"));
  }

  function closeMobileNavigation({ restoreFocus = true } = {}) {
    const layer = $("#mobileNavSheet");
    if (layer.getAttribute("aria-hidden") === "false") {
      setLayerOpen(layer, false, null, restoreFocus);
    }
  }

  const allResults = () => data.groups.flatMap((group) => group.items.map((page) => ({ group, page })));

  function renderCommandResults(query = "") {
    const normalized = query.trim().toLocaleLowerCase();
    const matches = allResults().filter(({ group, page }) => {
      if (!normalized) {
        return true;
      }
      return `${group.label} ${page.label} ${page.description}`.toLocaleLowerCase().includes(normalized);
    }).slice(0, 18);

    state.resultIndex = Math.min(state.resultIndex, Math.max(0, matches.length - 1));
    $("#resultCount").textContent = `${matches.length} ${matches.length === 1 ? "result" : "results"}`;
    $("#commandResults").innerHTML = matches.length
      ? matches.map(({ group, page }, index) => `
          <button
            class="command-result focus-ring${index === state.resultIndex ? " is-selected" : ""}"
            type="button"
            role="option"
            aria-selected="${index === state.resultIndex}"
            data-page="${escapeHtml(page.id)}"
            data-group="${escapeHtml(group.id)}">
            <span class="command-result__icon"><i data-lucide="${escapeHtml(page.icon)}" aria-hidden="true"></i></span>
            <span class="command-result__copy"><strong>${escapeHtml(page.label)}</strong><small>${escapeHtml(page.description)}</small></span>
            <span class="command-result__group">${escapeHtml(group.shortLabel ?? group.label)}</span>
          </button>
        `).join("")
      : '<p class="command-empty">No registered page matches this query.</p>';
    refreshIcons();
    return matches;
  }

  function openCommandPalette() {
    closeFloatingPanels();
    closeMobileNavigation({ restoreFocus: false });
    const input = $("#commandInput");
    input.value = "";
    state.resultIndex = 0;
    renderCommandResults();
    setLayerOpen($("#commandPalette"), true, input);
  }

  function closeCommandPalette({ restoreFocus = true } = {}) {
    const layer = $("#commandPalette");
    if (layer.getAttribute("aria-hidden") === "false") {
      setLayerOpen(layer, false, null, restoreFocus);
    }
  }

  function moveCommandSelection(direction) {
    const results = $$(".command-result", $("#commandResults"));
    if (!results.length) {
      return;
    }
    state.resultIndex = (state.resultIndex + direction + results.length) % results.length;
    results.forEach((result, index) => {
      result.classList.toggle("is-selected", index === state.resultIndex);
      result.setAttribute("aria-selected", String(index === state.resultIndex));
    });
    results[state.resultIndex].scrollIntoView({ block: "nearest" });
  }

  function openSelectedCommandResult() {
    const result = $$(".command-result", $("#commandResults"))[state.resultIndex];
    if (result) {
      closeCommandPalette({ restoreFocus: false });
      selectPage(result.dataset.group, result.dataset.page);
    }
  }

  function closeFloatingPanels(except = null) {
    [$("#notificationPanel"), $("#profilePanel")].forEach((panel) => {
      if (panel !== except) {
        panel.setAttribute("aria-hidden", "true");
      }
    });
  }

  function toggleFloatingPanel(panel) {
    const opening = panel.getAttribute("aria-hidden") !== "false";
    closeFloatingPanels(opening ? panel : null);
    panel.setAttribute("aria-hidden", String(!opening));
    if (opening) {
      window.setTimeout(() => $("button", panel)?.focus(), 20);
    }
  }

  function showToast(title, detail) {
    const toast = $("#toast");
    $("#toastTitle").textContent = title;
    $("#toastDetail").textContent = detail;
    toast.setAttribute("aria-hidden", "false");
    window.clearTimeout(state.toastTimer);
    state.toastTimer = window.setTimeout(() => toast.setAttribute("aria-hidden", "true"), 2700);
  }

  function triggerRefresh() {
    if (state.refreshing) {
      return;
    }
    state.refreshing = true;
    shell.classList.add("is-refreshing");
    $("#primaryAction").classList.add("is-refreshing");
    $("#primaryAction").disabled = true;
    $("#lastUpdated").textContent = "Refreshing live signals…";

    window.setTimeout(() => {
      state.refreshing = false;
      shell.classList.remove("is-refreshing");
      $("#primaryAction").classList.remove("is-refreshing");
      $("#primaryAction").disabled = false;
      $("#lastUpdated").textContent = "Updated just now";
      showToast("Runtime refreshed", "Signals are current.");
    }, 720);
  }

  function triggerPrimaryAction() {
    const profile = getProfile();
    if (state.groupId === "module" || state.groupId === "monitor") {
      triggerRefresh();
      return;
    }
    showToast(profile.action, "Prototype action acknowledged.");
  }

  function handleAction(action) {
    switch (action) {
      case "home":
        selectPage("module", "module-dashboard");
        break;
      case "search":
        openCommandPalette();
        break;
      case "close-search":
        closeCommandPalette();
        break;
      case "notifications":
        toggleFloatingPanel($("#notificationPanel"));
        break;
      case "profile":
        toggleFloatingPanel($("#profilePanel"));
        break;
      case "close-panels":
        closeFloatingPanels();
        break;
      case "theme":
        toggleTheme();
        break;
      case "locale":
        toggleLocale();
        break;
      case "collapse-context":
        setContextCollapsed(!state.collapsed);
        break;
      case "open-context":
        openContextOverlay();
        break;
      case "close-context":
        closeContextOverlay();
        break;
      case "mobile-nav":
        openMobileNavigation();
        break;
      case "close-mobile-nav":
        closeMobileNavigation();
        break;
      case "primary":
      case "refresh":
        triggerPrimaryAction();
        break;
      case "export":
        showToast("Share link prepared", "This prototype does not publish external links.");
        break;
      case "details":
        showToast("Posture details", "Diagnostic evidence would open in a page-owned drawer.");
        break;
      case "view-all":
        showToast("Full ledger", "The complete module-owned record set would open here.");
        break;
      case "activity":
        closeFloatingPanels();
        showToast("Activity journal", "Operational history preview selected.");
        break;
      default:
        break;
    }
  }

  document.addEventListener("click", (event) => {
    const actionTarget = event.target.closest("[data-action]");
    const groupTarget = event.target.closest("[data-group]:not([data-page])");
    const pageTarget = event.target.closest("[data-page]");
    const tabTarget = event.target.closest("[data-tab-target]");

    if (actionTarget) {
      handleAction(actionTarget.dataset.action);
      return;
    }

    if (pageTarget) {
      if (pageTarget.closest("#commandPalette")) {
        closeCommandPalette({ restoreFocus: false });
      }
      selectPage(pageTarget.dataset.group, pageTarget.dataset.page);
      return;
    }

    if (groupTarget) {
      selectGroup(groupTarget.dataset.group);
      return;
    }

    if (tabTarget) {
      selectTab(tabTarget.dataset.tabTarget);
      return;
    }

    if (!event.target.closest(".floating-panel")) {
      closeFloatingPanels();
    }
  });

  document.addEventListener("pointerover", (event) => {
    const categoryButton = event.target.closest(".category-button");
    const tooltip = categoryButton?.querySelector(".category-button__tooltip");
    if (tooltip) {
      const rect = categoryButton.getBoundingClientRect();
      tooltip.style.top = `${Math.max(8, rect.top + rect.height / 2 - 16)}px`;
    }
  });

  $("#commandInput").addEventListener("input", (event) => {
    state.resultIndex = 0;
    renderCommandResults(event.currentTarget.value);
  });

  document.addEventListener("keydown", (event) => {
    const commandOpen = $("#commandPalette").getAttribute("aria-hidden") === "false";
    if ((event.metaKey || event.ctrlKey) && event.key.toLocaleLowerCase() === "k") {
      event.preventDefault();
      commandOpen ? closeCommandPalette() : openCommandPalette();
      return;
    }

    if (event.altKey && event.key === "[") {
      event.preventDefault();
      setContextCollapsed(!state.collapsed);
      return;
    }

    if (commandOpen && event.key === "ArrowDown") {
      event.preventDefault();
      moveCommandSelection(1);
      return;
    }

    if (commandOpen && event.key === "ArrowUp") {
      event.preventDefault();
      moveCommandSelection(-1);
      return;
    }

    if (commandOpen && event.key === "Enter") {
      event.preventDefault();
      openSelectedCommandResult();
      return;
    }

    if (event.key === "Escape") {
      closeCommandPalette();
      closeMobileNavigation();
      closeFloatingPanels();
      closeContextOverlay();
    }
  });

  window.addEventListener("resize", () => {
    if (!isTablet()) {
      closeContextOverlay();
    }
    if (!isMobile()) {
      closeMobileNavigation({ restoreFocus: false });
    }
  }, { passive: true });

  $("#appName").textContent = data.app.name;
  $("#appId").textContent = data.app.id;
  $("#appVersion").textContent = data.app.version;
  $("#environmentName").textContent = data.app.environment;
  $("#mobileEnvironment").textContent = `${data.app.environment} · Live`;
  setContextCollapsed(state.collapsed);
  updateTheme();
  updateLocale();
  renderNotifications();
  renderNavigationAndPage();
  renderCommandResults();
})();
