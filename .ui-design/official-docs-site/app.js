(function () {
  const data = window.MonicaDocsPrototype;
  const flatDocs = data.docs.flatMap(group => group.items.map(item => ({ ...item, group: group.group })));
  const state = {
    activeDocSlug: "zh-cn/index",
    docFilter: "all",
    moduleFilter: "All",
    pipelineIndex: 0
  };

  const docTree = document.querySelector("#doc-tree");
  const docArticle = document.querySelector("#doc-article");
  const tocList = document.querySelector("#toc-list");
  const moduleFilters = document.querySelector("#module-filters");
  const moduleAtlas = document.querySelector("#module-atlas");
  const pipelineStages = document.querySelector("#pipeline-stages");
  const pipelineDetail = document.querySelector("#pipeline-detail");
  const searchOverlay = document.querySelector("#search-overlay");
  const searchInput = document.querySelector("#search-input");
  const searchResults = document.querySelector("#search-results");
  const toast = document.querySelector("#toast");
  const docsSidebar = document.querySelector("#docs-sidebar");

  document.addEventListener("DOMContentLoaded", () => {
    applyInitialMode();
    renderDocsTree();
    renderActiveDoc();
    renderModuleFilters();
    renderModules();
    renderPipeline();
    bindInteractions();
    initializeIcons();
    observeSections();
    scrollToInitialSection();
  });

  function applyInitialMode() {
    const params = new URLSearchParams(window.location.search);
    if (params.get("mode") !== "signal") {
      return;
    }

    document.body.classList.add("signal-mode");
    document.querySelector("#tone-toggle")?.setAttribute("aria-pressed", "true");
  }

  function scrollToInitialSection() {
    const params = new URLSearchParams(window.location.search);
    const sectionName = params.get("section");
    if (!sectionName) {
      return;
    }

    const section = document.getElementById(sectionName);
    if (!section) {
      return;
    }

    const offset = Number(params.get("offset") || 0);
    setTimeout(() => {
      window.scrollTo({
        top: Math.max(0, section.offsetTop - 90 + offset),
        behavior: "instant"
      });
    }, 0);
  }

  function bindInteractions() {
    document.querySelectorAll("[data-doc-filter]").forEach(button => {
      button.addEventListener("click", () => {
        state.docFilter = button.dataset.docFilter;
        document.querySelectorAll("[data-doc-filter]").forEach(item => item.classList.toggle("active", item === button));
        renderDocsTree();
      });
    });

    document.querySelector("#open-search").addEventListener("click", openSearch);
    document.querySelector("#close-search").addEventListener("click", closeSearch);
    searchOverlay.addEventListener("click", event => {
      if (event.target === searchOverlay) {
        closeSearch();
      }
    });
    searchInput.addEventListener("input", () => renderSearch(searchInput.value));

    document.addEventListener("keydown", event => {
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        openSearch();
      }
      if (event.key === "Escape") {
        closeSearch();
        docsSidebar.classList.remove("open");
      }
    });

    document.querySelector("#tone-toggle").addEventListener("click", event => {
      const button = event.currentTarget;
      const next = !document.body.classList.contains("signal-mode");
      document.body.classList.toggle("signal-mode", next);
      button.setAttribute("aria-pressed", String(next));
    });

    document.querySelector("#copy-install").addEventListener("click", async () => {
      const command = document.querySelector("#install-command").textContent;
      try {
        await navigator.clipboard.writeText(command);
        showToast("Install command copied");
      } catch {
        showToast(command);
      }
    });

    document.querySelector("#mobile-tree-toggle").addEventListener("click", () => docsSidebar.classList.add("open"));
    document.querySelector("#close-tree").addEventListener("click", () => docsSidebar.classList.remove("open"));
  }

  function renderDocsTree() {
    docTree.innerHTML = data.docs.map(group => {
      const items = group.items.filter(item => state.docFilter === "all" || item.type === state.docFilter);
      if (!items.length) {
        return "";
      }

      const links = items.map(item => `
        <button class="doc-link ${item.slug === state.activeDocSlug ? "active" : ""}" type="button" data-doc-slug="${item.slug}">
          <span>${escapeHtml(item.title)}</span>
          <span class="tag">${escapeHtml(item.type)}</span>
          <span class="path">${escapeHtml(item.path)}</span>
        </button>
      `).join("");

      return `
        <section class="doc-group">
          <h3 class="doc-group-title">${escapeHtml(group.group)}</h3>
          ${links}
        </section>
      `;
    }).join("");

    docTree.querySelectorAll("[data-doc-slug]").forEach(button => {
      button.addEventListener("click", () => {
        state.activeDocSlug = button.dataset.docSlug;
        renderDocsTree();
        renderActiveDoc();
        docsSidebar.classList.remove("open");
      });
    });
  }

  function renderActiveDoc() {
    const doc = flatDocs.find(item => item.slug === state.activeDocSlug) || flatDocs[0];

    docArticle.innerHTML = `
      <div class="article-topline">
        <span class="article-pill">${escapeHtml(doc.group)}</span>
        <span class="article-pill">${escapeHtml(doc.path)}</span>
        <span class="article-pill">${escapeHtml(doc.cnTitle)}</span>
      </div>
      <h3>${escapeHtml(doc.title)}</h3>
      <p class="doc-description">${escapeHtml(doc.description)}</p>
      <div class="doc-body-grid">
        <div class="doc-body">
          ${doc.body.map(paragraph => `<p>${formatInlineCode(escapeHtml(paragraph))}</p>`).join("")}
          <div class="code-window">
            <div class="code-window-header"><span></span><span></span><span></span></div>
            <pre><code>${escapeHtml(doc.code)}</code></pre>
          </div>
        </div>
        <aside class="doc-aside-card">
          <span>Design annotation</span>
          <strong>${escapeHtml(doc.asideTitle)}</strong>
          <p>${formatInlineCode(escapeHtml(doc.asideBody))}</p>
        </aside>
      </div>
    `;

    tocList.innerHTML = doc.headings.map(heading => `<a class="toc-link" href="#docs">${escapeHtml(heading)}</a>`).join("");
    initializeIcons();
  }

  function renderModuleFilters() {
    const categories = ["All", ...new Set(data.modules.map(module => module.category))];
    moduleFilters.innerHTML = categories.map(category => `
      <button class="module-filter ${category === state.moduleFilter ? "active" : ""}" type="button" data-module-filter="${escapeHtml(category)}">
        ${escapeHtml(category)}
      </button>
    `).join("");

    moduleFilters.querySelectorAll("[data-module-filter]").forEach(button => {
      button.addEventListener("click", () => {
        state.moduleFilter = button.dataset.moduleFilter;
        renderModuleFilters();
        renderModules();
      });
    });
  }

  function renderModules() {
    const modules = state.moduleFilter === "All"
      ? data.modules
      : data.modules.filter(module => module.category === state.moduleFilter);

    moduleAtlas.innerHTML = modules.map(module => `
      <article class="module-card" style="--module-glow: ${module.glow}">
        <div class="module-meta">
          <span class="module-category">${escapeHtml(module.category)}</span>
          <span class="module-ui">${escapeHtml(module.ui)}</span>
        </div>
        <h3>${escapeHtml(module.title)}</h3>
        <p>${escapeHtml(module.description)}</p>
        <div class="module-details">
          <code>${escapeHtml(module.packageName)}</code>
          <code>${escapeHtml(module.registration)}</code>
        </div>
      </article>
    `).join("");
  }

  function renderPipeline() {
    pipelineStages.innerHTML = data.pipeline.map((stage, index) => `
      <button class="pipeline-stage ${index === state.pipelineIndex ? "active" : ""}" type="button" data-stage-index="${index}">
        <span class="pipeline-index">${String(index + 1).padStart(2, "0")}</span>
        <span>
          <strong>${escapeHtml(stage.title)}</strong>
          ${escapeHtml(stage.summary)}
        </span>
        <span>${escapeHtml(stage.badge)}</span>
      </button>
    `).join("");

    pipelineStages.querySelectorAll("[data-stage-index]").forEach(button => {
      button.addEventListener("click", () => {
        state.pipelineIndex = Number(button.dataset.stageIndex);
        renderPipeline();
      });
    });

    const stage = data.pipeline[state.pipelineIndex];
    pipelineDetail.innerHTML = `
      <span class="badge">${escapeHtml(stage.badge)}</span>
      <h3>${escapeHtml(stage.title)}</h3>
      <p>${escapeHtml(stage.detail)}</p>
      <div class="contract-preview">
        ${stage.contracts.map(contract => `<code>${escapeHtml(contract)}</code>`).join("")}
      </div>
    `;
  }

  function openSearch() {
    searchOverlay.classList.add("open");
    searchOverlay.setAttribute("aria-hidden", "false");
    searchInput.value = "";
    renderSearch("");
    requestAnimationFrame(() => searchInput.focus());
  }

  function closeSearch() {
    searchOverlay.classList.remove("open");
    searchOverlay.setAttribute("aria-hidden", "true");
  }

  function renderSearch(query) {
    const normalized = query.trim().toLowerCase();
    const docMatches = flatDocs
      .filter(doc => matches(normalized, [doc.title, doc.cnTitle, doc.path, doc.description, doc.code, doc.type]))
      .map(doc => ({
        kind: "Doc",
        title: doc.title,
        detail: doc.path,
        action: () => {
          state.activeDocSlug = doc.slug;
          renderDocsTree();
          renderActiveDoc();
          closeSearch();
          document.querySelector("#docs").scrollIntoView({ behavior: "smooth" });
        }
      }));

    const moduleMatches = data.modules
      .filter(module => matches(normalized, [module.title, module.category, module.packageName, module.registration, module.description, module.ui]))
      .map(module => ({
        kind: "Module",
        title: module.title,
        detail: `${module.packageName} · ${module.registration}`,
        action: () => {
          state.moduleFilter = module.category;
          renderModuleFilters();
          renderModules();
          closeSearch();
          document.querySelector("#modules").scrollIntoView({ behavior: "smooth" });
        }
      }));

    const results = [...docMatches, ...moduleMatches].slice(0, 9);
    searchResults.innerHTML = results.length
      ? results.map((result, index) => `
          <button class="search-result" type="button" data-result-index="${index}">
            <span>
              <strong>${escapeHtml(result.title)}</strong>
              <span>${escapeHtml(result.detail)}</span>
            </span>
            <span>${escapeHtml(result.kind)}</span>
          </button>
        `).join("")
      : `<div class="search-result"><span><strong>No results yet</strong><span>Try a module name, route, or contract term.</span></span></div>`;

    searchResults.querySelectorAll("[data-result-index]").forEach(button => {
      button.addEventListener("click", () => results[Number(button.dataset.resultIndex)].action());
    });
  }

  function matches(query, values) {
    if (!query) {
      return true;
    }

    return values.some(value => String(value).toLowerCase().includes(query));
  }

  function observeSections() {
    const links = [...document.querySelectorAll(".desktop-nav a")];
    const sections = links.map(link => document.querySelector(link.getAttribute("href"))).filter(Boolean);

    const observer = new IntersectionObserver(entries => {
      const visible = entries
        .filter(entry => entry.isIntersecting)
        .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];

      if (!visible) {
        return;
      }

      links.forEach(link => link.classList.toggle("active", link.getAttribute("href") === `#${visible.target.id}`));
    }, { rootMargin: "-30% 0px -60% 0px", threshold: [0.08, 0.2, 0.4] });

    sections.forEach(section => observer.observe(section));
  }

  function showToast(message) {
    toast.textContent = message;
    toast.classList.add("visible");
    window.clearTimeout(showToast.timeout);
    showToast.timeout = window.setTimeout(() => toast.classList.remove("visible"), 1800);
  }

  function initializeIcons() {
    if (window.lucide) {
      window.lucide.createIcons();
    }
  }

  function formatInlineCode(value) {
    return value.replace(/`([^`]+)`/g, "<code>$1</code>");
  }

  function escapeHtml(value) {
    return String(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#039;");
  }
})();
