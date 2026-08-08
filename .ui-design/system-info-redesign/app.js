(() => {
  "use strict";

  const data = window.SYSTEM_INFO_DATA;
  const body = document.body;
  const root = document.documentElement;
  const state = {
    language: "en",
    theme: "light",
    tracking: "tracked",
    density: "comfortable",
    uptimeSeconds: parseClock(data.service.uptime),
    captureAgeSeconds: data.pulse.captureAgeSeconds,
    capturedAt: data.pulse.capturedAt,
    restartRequested: false,
    lastFocusedElement: null,
    openModal: null,
    toastTimer: null
  };

  const $ = (selector, scope = document) => scope.querySelector(selector);
  const $$ = (selector, scope = document) => [...scope.querySelectorAll(selector)];

  function dictionary() {
    return data.i18n[state.language];
  }

  function text(key, ...values) {
    let value = dictionary()[key] ?? key;
    values.forEach((item, index) => {
      value = value.replace(`{${index}}`, item);
    });
    return value;
  }

  function localized(item, property) {
    const localizedProperty = state.language === "zh"
      ? `${property}Zh`
      : property;
    return item[localizedProperty] ?? item[property] ?? "";
  }

  function escapeHtml(value) {
    return String(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#039;");
  }

  function refreshIcons() {
    window.lucide?.createIcons({ attrs: { "aria-hidden": "true" } });
  }

  function applyTranslations() {
    root.lang = state.language === "zh" ? "zh-CN" : "en";

    $$('[data-i18n]').forEach(element => {
      const key = element.dataset.i18n;
      if (key in dictionary()) {
        element.textContent = text(key);
      }
    });

    $$('[data-i18n-title]').forEach(element => {
      const value = text(element.dataset.i18nTitle);
      element.title = value;
      element.setAttribute("aria-label", value);
    });

    $$('[data-i18n-aria]').forEach(element => {
      element.setAttribute("aria-label", text(element.dataset.i18nAria));
    });

    $$('[data-service-name]').forEach(element => {
      element.textContent = state.language === "zh" ? data.service.nameZh : data.service.name;
    });

    $(".language-code").textContent = state.language === "en" ? "中" : "EN";
    $("#tracking-toggle span").textContent = text(
      state.tracking === "tracked" ? "trackedToggle" : "untrackedToggle"
    );
    if (state.restartRequested) {
      $("#restart-button span").textContent = text("restartRequested");
    }
    $("[data-restart-delay]").textContent = text("restartDelay", data.restart.delaySeconds);

    renderRuntime();
    renderEnvironment();
    renderEndpoints();
    renderBuildEvidence();
    renderCustomLinks();
    updateCaptureEvidence();
    refreshIcons();
  }

  function renderRuntime() {
    $("#runtime-list").innerHTML = data.runtime.map(item => `
      <div class="evidence-row tone-${escapeHtml(item.tone)}">
        <div class="evidence-row__identity">
          <span class="evidence-icon"><i data-lucide="${escapeHtml(item.icon)}"></i></span>
          <span>${escapeHtml(text(item.key))}</span>
        </div>
        <div class="evidence-row__value">
          <strong title="${escapeHtml(localized(item, "value"))}">${escapeHtml(localized(item, "value"))}</strong>
          ${item.badge ? `<span class="row-badge">${escapeHtml(localized(item, "badge"))}</span>` : ""}
        </div>
      </div>
    `).join("");
  }

  function renderEnvironment() {
    $("#environment-list").innerHTML = data.environment.map(item => `
      <div class="environment-evidence tone-${escapeHtml(item.tone)} ${item.long ? "environment-evidence--long" : ""}">
        <div class="environment-evidence__identity">
          <span class="environment-evidence__icon"><i data-lucide="${escapeHtml(item.icon)}"></i></span>
          <span>${escapeHtml(text(item.key))}</span>
        </div>
        <strong class="environment-evidence__value" title="${escapeHtml(localized(item, "value"))}">${escapeHtml(localized(item, "value"))}</strong>
      </div>
    `).join("");
  }

  function renderEndpoints() {
    $("#endpoint-list").innerHTML = data.endpoints.map((endpoint, index) => {
      const scheme = endpoint.scheme.toUpperCase();
      return `
        <div class="endpoint-row endpoint-row--${scheme.toLowerCase()}">
          <span class="scheme-badge">${escapeHtml(scheme)}</span>
          <strong class="endpoint-row__address" title="${escapeHtml(endpoint.address)}">${escapeHtml(endpoint.address)}</strong>
          <span class="endpoint-row__meta">
            <span class="endpoint-row__role">${escapeHtml(text("address"))}</span>
            <span class="endpoint-row__security">${escapeHtml(text("transport"))}: ${escapeHtml(scheme)}</span>
          </span>
          <span class="endpoint-row__status">
            <span class="endpoint-row__live"><i></i>${escapeHtml(text("bound"))}</span>
            <button class="inspect-button" type="button" data-endpoint-index="${index}" title="${escapeHtml(text("inspect"))}" aria-label="${escapeHtml(text("inspect"))}: ${escapeHtml(endpoint.address)}">
              <i data-lucide="arrow-up-right"></i>
            </button>
          </span>
        </div>
      `;
    }).join("");

    $$('[data-endpoint-index]').forEach(button => {
      button.addEventListener("click", () => openEndpoint(Number(button.dataset.endpointIndex), button));
    });
  }

  function renderBuildEvidence() {
    const build = data.build;
    $("#build-evidence").innerHTML = `
      <div class="build-feature">
        <span class="build-key">${escapeHtml(text("product"))}</span>
        <strong title="${escapeHtml(build.productName)}">${escapeHtml(build.productName)}</strong>
        <span title="${escapeHtml(build.productVersion)}">${escapeHtml(build.productVersion)}</span>
      </div>
      <div class="build-grid">
        ${buildCell("fileVersion", build.fileVersion)}
        ${buildCell("builtAt", build.builtAt)}
        ${buildCell("company", build.company)}
        ${buildCell("legal", build.copyright)}
      </div>
    `;
  }

  function buildCell(key, value) {
    return `<div class="build-cell"><span class="build-key">${escapeHtml(text(key))}</span><strong title="${escapeHtml(value)}">${escapeHtml(value)}</strong></div>`;
  }

  function renderCustomLinks() {
    const groups = new Map();
    data.customLinks.forEach(link => {
      const category = localized(link, "category");
      const links = groups.get(category) ?? [];
      links.push(link);
      groups.set(category, links);
    });
    const content = [];

    for (const [category, links] of groups) {
      content.push(`<div class="route-group-title"><span>${escapeHtml(category)}</span><b>${links.length}</b></div>`);
      links.forEach(link => content.push(renderCustomLink(link)));
    }

    $("#route-list").innerHTML = content.join("");

    $$('.route-row[href="#"]').forEach(link => link.addEventListener("click", event => event.preventDefault()));
    $$('[data-access-key]').forEach(button => {
      button.addEventListener("click", () => {
        const panel = $(`#${button.getAttribute("aria-controls")}`);
        const expanded = button.getAttribute("aria-expanded") === "true";
        button.setAttribute("aria-expanded", String(!expanded));
        panel.hidden = expanded;
      });
    });
    $$('[data-copy-value]').forEach(button => {
      button.addEventListener("click", async () => {
        await copyText(button.dataset.copyValue);
        showToast("copied");
      });
    });
  }

  function renderCustomLink(link) {
    const target = link.target === "newTab" ? "_blank" : "_self";
    const rel = target === "_blank" ? ' rel="noopener noreferrer"' : "";
    const credentialKey = `credentials-${link.key}`;
    const credentials = link.credentials ? `
      <button class="route-access" type="button" data-access-key="${escapeHtml(link.key)}" aria-expanded="false" aria-controls="${credentialKey}">
        <i data-lucide="key-round"></i><span>${escapeHtml(text("access"))}</span>
      </button>
      <div class="route-credentials" id="${credentialKey}" hidden>
        <span>${escapeHtml(text("access"))}</span>
        <button type="button" data-copy-value="${escapeHtml(link.credentials.userName)}">${escapeHtml(text("copyUserName"))}</button>
        <button type="button" data-copy-value="${escapeHtml(link.credentials.password)}">${escapeHtml(text("copyPassword"))}</button>
      </div>
    ` : "";

    return `
      <div class="route-entry tone-${escapeHtml(link.accent)} ${link.credentials ? "has-credentials" : ""}">
        <a class="route-row" href="${escapeHtml(link.href)}" target="${target}"${rel}>
          <span class="route-row__icon"><i data-lucide="${escapeHtml(link.icon)}"></i></span>
          <span class="route-row__identity">
            <strong>${escapeHtml(text(`${link.key}Title`))}</strong>
            <p>${escapeHtml(text(`${link.key}Description`))}</p>
          </span>
          <span class="route-target">${escapeHtml(text(link.target))}</span>
          <i data-lucide="arrow-up-right"></i>
        </a>
        ${credentials}
      </div>
    `;
  }

  async function copyText(value) {
    try {
      await navigator.clipboard.writeText(value);
    } catch {
      const input = document.createElement("textarea");
      input.value = value;
      input.style.position = "fixed";
      input.style.opacity = "0";
      document.body.append(input);
      input.select();
      document.execCommand("copy");
      input.remove();
    }
  }

  function updateCaptureEvidence() {
    $$('[data-captured-time]').forEach(element => {
      element.textContent = state.capturedAt;
    });
    $$('[data-capture-age]').forEach(element => {
      element.removeAttribute("data-i18n");
      element.textContent = state.captureAgeSeconds === 0
        ? text("justNow")
        : text("secondsAgo", state.captureAgeSeconds);
    });
  }

  function updateUptime() {
    state.uptimeSeconds += 1;
    const formatted = formatClock(state.uptimeSeconds);
    $$('[data-uptime]').forEach(element => {
      element.textContent = formatted;
    });
  }

  function updateCaptureAge() {
    state.captureAgeSeconds += 1;
    updateCaptureEvidence();
  }

  function parseClock(value) {
    const [hours, minutes, seconds] = value.split(":").map(Number);
    return hours * 3600 + minutes * 60 + seconds;
  }

  function formatClock(totalSeconds) {
    const hours = Math.floor(totalSeconds / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;
    return [hours, minutes, seconds].map(value => String(value).padStart(2, "0")).join(":");
  }

  function formatCurrentTime() {
    return new Intl.DateTimeFormat(state.language === "zh" ? "zh-CN" : "en-GB", {
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
      hour12: false
    }).format(new Date());
  }

  function showToast(key) {
    const toast = $("#toast");
    $("#toast span").textContent = text(key);
    toast.classList.add("is-visible");
    clearTimeout(state.toastTimer);
    state.toastTimer = setTimeout(() => toast.classList.remove("is-visible"), 2300);
  }

  function openModal(modal, trigger) {
    closeModal(false);
    state.lastFocusedElement = trigger ?? document.activeElement;
    state.openModal = modal;
    modal.hidden = false;
    body.classList.add("modal-open");
    const first = focusableElements(modal)[0];
    first?.focus();
  }

  function closeModal(restoreFocus = true) {
    if (!state.openModal) {
      return;
    }

    state.openModal.hidden = true;
    state.openModal = null;
    body.classList.remove("modal-open");
    if (restoreFocus) {
      state.lastFocusedElement?.focus();
    }
  }

  function focusableElements(container) {
    return $$('button:not([disabled]), a[href], select:not([disabled]), [tabindex]:not([tabindex="-1"])', container)
      .filter(element => !element.hidden && element.offsetParent !== null);
  }

  function openEndpoint(index, trigger) {
    const endpoint = data.endpoints[index];
    $('[data-endpoint-address]').textContent = endpoint.address;
    $('[data-endpoint-transport]').textContent = endpoint.scheme.toUpperCase();
    $('[data-endpoint-address-detail]').textContent = endpoint.address;
    openModal($("#endpoint-modal"), trigger);
  }

  function wireControls() {
    $("#tracking-toggle").addEventListener("click", () => {
      state.tracking = state.tracking === "tracked" ? "untracked" : "tracked";
      body.dataset.tracking = state.tracking;
      $("#tracking-toggle").setAttribute("aria-pressed", String(state.tracking === "tracked"));
      applyTranslations();
    });

    $("#theme-toggle").addEventListener("click", () => {
      state.theme = state.theme === "light" ? "dark" : "light";
      root.dataset.theme = state.theme;
      $("#theme-toggle").innerHTML = `<i data-lucide="${state.theme === "light" ? "moon" : "sun"}" aria-hidden="true"></i>`;
      refreshIcons();
    });

    $("#language-toggle").addEventListener("click", () => {
      state.language = state.language === "en" ? "zh" : "en";
      applyTranslations();
    });

    $("#density-toggle").addEventListener("click", () => {
      state.density = state.density === "comfortable" ? "compact" : "comfortable";
      body.dataset.density = state.density;
      $("#density-toggle").setAttribute("aria-pressed", String(state.density === "compact"));
    });

    $("#refresh-button").addEventListener("click", () => {
      if (body.classList.contains("is-refreshing")) {
        return;
      }

      body.classList.add("is-refreshing");
      $("#refresh-button").disabled = true;
      setTimeout(() => {
        state.captureAgeSeconds = 0;
        state.capturedAt = formatCurrentTime();
        updateCaptureEvidence();
        body.classList.remove("is-refreshing");
        $("#refresh-button").disabled = false;
        showToast("refreshed");
      }, 650);
    });

    const restartButton = $("#restart-button");
    restartButton.hidden = !data.restart.enabled;
    restartButton.addEventListener("click", () => openModal($("#restart-modal"), restartButton));
    $$('[data-modal-close]').forEach(button => button.addEventListener("click", () => closeModal()));
    $$('[data-endpoint-close]').forEach(button => button.addEventListener("click", () => closeModal()));

    $("#restart-confirm").addEventListener("click", () => {
      closeModal();
      state.restartRequested = true;
      restartButton.disabled = true;
      restartButton.classList.add("is-requested");
      restartButton.querySelector("span").textContent = text("restartRequested");
      showToast("restartRequested");
    });

    $$('[data-target]').forEach(button => {
      button.addEventListener("click", () => scrollToSection(button.dataset.target));
    });
    $("#section-picker").addEventListener("change", event => scrollToSection(event.target.value));

    $$('.modal-backdrop').forEach(backdrop => {
      backdrop.addEventListener("mousedown", event => {
        if (event.target === backdrop) {
          closeModal();
        }
      });
    });

    document.addEventListener("keydown", event => {
      if (!state.openModal) {
        return;
      }

      if (event.key === "Escape") {
        event.preventDefault();
        closeModal();
        return;
      }

      if (event.key !== "Tab") {
        return;
      }

      const focusable = focusableElements(state.openModal);
      if (focusable.length === 0) {
        return;
      }

      const first = focusable[0];
      const last = focusable.at(-1);
      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault();
        last.focus();
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault();
        first.focus();
      }
    });
  }

  function scrollToSection(id) {
    const section = document.getElementById(id);
    section?.scrollIntoView({ behavior: "smooth", block: "start" });
    updateActiveSection(id);
  }

  function updateActiveSection(id) {
    $$('[data-target]').forEach(button => {
      const active = button.dataset.target === id;
      button.classList.toggle("is-active", active);
      if (active) {
        button.setAttribute("aria-current", "true");
      } else {
        button.removeAttribute("aria-current");
      }
    });
    $("#section-picker").value = id;
  }

  function observeSections() {
    if (!("IntersectionObserver" in window)) {
      return;
    }

    const observer = new IntersectionObserver(entries => {
      const visible = entries
        .filter(entry => entry.isIntersecting)
        .sort((left, right) => right.intersectionRatio - left.intersectionRatio)[0];
      if (visible) {
        updateActiveSection(visible.target.id);
      }
    }, { rootMargin: "-20% 0px -60%", threshold: [0.05, 0.25, 0.5] });

    $$('[data-section]').forEach(section => observer.observe(section));
  }

  root.dataset.theme = state.theme;
  body.dataset.tracking = state.tracking;
  body.dataset.density = state.density;
  applyTranslations();
  wireControls();
  observeSections();
  setInterval(updateUptime, 1000);
  setInterval(updateCaptureAge, 1000);
})();
