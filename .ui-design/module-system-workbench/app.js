(() => {
  const data = window.WORKBENCH_DATA;
  const sections = ["overview", "performance", "modules", "dependencies", "discovery"];
  const state = {
    section: "overview",
    theme: "light",
    language: "en",
    baseline: null,
    selectedCritical: "application-configuration",
    selectedModule: null,
    drawerTab: "summary",
    moduleSearch: "",
    moduleCapability: "all",
    moduleAssembly: "all",
    moduleCost: "all",
    modulePage: 1,
    modulePageSize: 25,
    perfLimit: 10,
    perfMinimum: 1,
    perfHideZero: true,
    dependencyFocus: "module-configuration",
    dependencyDepth: "one",
    dependencyTable: false,
    inventoryLoaded: false,
    inventorySearch: "",
    inventoryOutcome: "all",
    inventoryPage: 1,
    inventoryPageSize: 8,
    startupTracked: true
  };

  const $ = (selector, root = document) => root.querySelector(selector);
  const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];
  const fmt = value => new Intl.NumberFormat("en-US").format(value);
  const ms = value => `${fmt(value)} ms`;
  const exactMs = value => `${Number(value).toFixed(4)} ms`;
  const escapeHtml = value => String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
  const moduleById = id => data.modules.find(module => module.id === id);
  const icon = (name, className = "") => `<i data-lucide="${name}"${className ? ` class="${className}"` : ""}></i>`;

  function refreshIcons() {
    if (window.lucide) window.lucide.createIcons({ attrs: { "stroke-width": 1.8 } });
  }

  function comparisonBanner() {
    if (!state.baseline) return "";
    const delta = data.snapshot.totalMs - state.baseline.totalMs;
    return `
      <div class="comparison-banner">
        <div>${icon("git-compare-arrows")}<span><strong>${escapeHtml(state.baseline.name)}</strong> · Total composition improved by <strong>${fmt(Math.abs(delta))} ms</strong></span></div>
        <button type="button" data-action="remove-baseline">Remove comparison</button>
      </div>`;
  }

  function sectionHeading(id, kicker, title, description, aside = "") {
    return `
      <header class="section-heading">
        <div><p class="panel-kicker">${escapeHtml(kicker)}</p><h2 id="${id}">${escapeHtml(title)}</h2><p>${escapeHtml(description)}</p></div>
        <div class="section-aside">${aside}<span class="evidence-time">snapshot #17 · immutable</span></div>
      </header>`;
  }

  function criticalRibbon() {
    const total = data.criticalPath.reduce((sum, item) => sum + item.ms, 0);
    const items = [];
    data.criticalPath.forEach((segment, index) => {
      const width = Math.max(10, (segment.ms / total) * 100);
      items.push(`
        <button class="critical-segment ${state.selectedCritical === segment.id ? "selected" : ""}" data-critical-id="${segment.id}" data-tone="${segment.tone}" type="button" style="flex:${width} 1 0" title="${escapeHtml(segment.detail)} · ${ms(segment.ms)}">
          <span class="segment-label">${escapeHtml(segment.label)}</span>
          <span class="segment-time">${ms(segment.ms)}</span>
          <span class="segment-offset">+${fmt(segment.start)} ms</span>
        </button>`);
      if (index < data.criticalPath.length - 1) items.push(`<span class="critical-ribbon-arrow" aria-hidden="true">${icon("chevron-right")}</span>`);
    });
    const selected = data.criticalPath.find(segment => segment.id === state.selectedCritical) ?? data.criticalPath[0];
    const route = selected.section || (selected.moduleId ? "modules" : "performance");
    const targetLabel = route === "modules" ? "Inspect module" : route === "discovery" ? "Open discovery" : "Open performance";
    return `
      <div class="critical-ribbon" role="list" aria-label="Composition critical path">${items.join("")}</div>
      <div class="critical-evidence">
        <div><strong>${escapeHtml(selected.detail)}</strong><p>${ms(selected.ms)} blocking time · starts at +${fmt(selected.start)} ms · ${((selected.ms / data.snapshot.totalMs) * 100).toFixed(1)}% of composition</p></div>
        <button class="secondary-button" type="button" data-critical-open="${selected.id}" data-target-section="${route}">${targetLabel} ${icon("arrow-right")}</button>
      </div>`;
  }

  function renderOverview() {
    const text = data.translations[state.language];
    const applicationStartupMs = state.startupTracked ? data.snapshot.applicationStartupMs : null;
    const startupDelta = applicationStartupMs != null && state.baseline?.applicationStartupMs != null
      ? applicationStartupMs - state.baseline.applicationStartupMs
      : null;
    const compositionShare = applicationStartupMs != null && applicationStartupMs > 0
      ? Math.min(100, (data.snapshot.totalMs / applicationStartupMs) * 100)
      : null;
    const kpis = [
      { label: "Active modules", icon: "boxes", value: data.snapshot.moduleCount, badge: "93 declared", detail: "128 direct dependency edges", tone: "success" },
      { label: "Service registration", icon: "workflow", value: fmt(data.snapshot.registrationMs), unit: "ms", badge: "Exact", detail: "74.3% of module composition", tone: "info" },
      { label: "Type discovery", icon: "scan-search", value: fmt(data.snapshot.discoveryMs), unit: "ms", badge: "14 queries", detail: "1,137 compiled matches", tone: "secondary" },
      { label: "Findings", icon: "triangle-alert", value: data.snapshot.findingCount, badge: "1 warning", detail: "0 composition errors", tone: "warning" }
    ];
    const kpiHtml = kpis.map(kpi => `
      <article class="kpi-cell" data-tone="${kpi.tone}">
        <div class="kpi-card-head"><span class="kpi-icon">${icon(kpi.icon)}</span><span class="kpi-badge">${kpi.badge}</span></div>
        <div class="kpi-label">${kpi.label}</div>
        <div class="kpi-value"><span>${kpi.value}</span>${kpi.unit ? `<small>${kpi.unit}</small>` : ""}</div>
        <div class="kpi-detail">${kpi.detail}</div>
      </article>`).join("");

    const startupCard = applicationStartupMs != null
      ? `<article class="startup-kpi" data-tracked="true">
          <div class="startup-kpi-head">
            <span class="kpi-icon">${icon("rocket")}</span>
            <div><span class="kpi-label">${text.startupTiming}</span><strong>${text.applicationStartup}</strong></div>
            <span class="tracking-badge">${icon("radio")}${text.tracked}</span>
          </div>
          <div class="startup-kpi-metrics">
            <div class="startup-primary"><strong>${exactMs(applicationStartupMs)}</strong><small>${text.startupBoundary}</small></div>
            <div class="startup-secondary"><span>${text.moduleComposition}</span><strong>${ms(data.snapshot.totalMs)}</strong></div>
          </div>
          <div class="startup-share" aria-label="${text.moduleComposition} ${compositionShare.toFixed(1)}%">
            <div class="startup-share-labels"><span>${text.compositionShare}</span><strong class="startup-share-badge">${compositionShare.toFixed(1)}%</strong></div>
            <div class="startup-share-track"><span class="startup-share-composition" style="width:${compositionShare}%"></span><span class="startup-share-remaining"></span></div>
            <div class="startup-share-legend"><span><i></i>${text.moduleComposition}</span><span><i></i>${text.remainingStartup}</span></div>
          </div>
          ${startupDelta != null ? `<span class="startup-delta">${startupDelta < 0 ? "↓" : "↑"} ${exactMs(Math.abs(startupDelta))} ${text.versusBaseline}</span>` : ""}
        </article>`
      : `<article class="kpi-cell composition-only" data-tone="primary">
          <div class="kpi-card-head"><span class="kpi-icon">${icon("timer")}</span><span class="kpi-badge">${text.exact}</span></div>
          <div class="kpi-label">${text.moduleComposition}</div>
          <div class="kpi-value"><span>${fmt(data.snapshot.totalMs)}</span><small>ms</small></div>
          <div class="kpi-detail">${text.finalCompositionInterval}</div>
        </article>`;

    const findings = data.findings.map(finding => `
      <article class="finding-card ${finding.severity}">
        <span class="finding-icon">${icon(finding.severity === "warning" ? "triangle-alert" : "info")}</span>
        <div><h4>${escapeHtml(finding.title)}</h4><p>${escapeHtml(finding.detail)}</p><code>${finding.code}</code></div>
        <button type="button" data-open-module="${finding.moduleId}">Inspect</button>
      </article>`).join("");

    const hotspots = data.contributors.slice(0, 5).map(item => {
      const width = Math.max(2, item.ms / data.contributors[0].ms * 100);
      return `<div class="hotspot-row" data-open-module="${item.moduleId}" role="button" tabindex="0"><div><strong>${escapeHtml(item.label)}</strong><span>${escapeHtml(item.phase)}${item.blocking ? " · blocking" : " · non-blocking"}</span><div class="micro-bar"><span style="width:${width}%"></span></div></div><code>${ms(item.ms)}</code></div>`;
    }).join("");

    $("#section-overview").innerHTML = `
      ${sectionHeading("overview-title", "01 / COMPOSITION VERDICT", "Overview", "One immutable answer to what happened during FlightService module composition.", `<button class="small-button" type="button" data-action="toggle-startup-timing">${state.startupTracked ? text.prototypeTrackingOn : text.prototypeTrackingOff}</button><span class="status-badge success"><span class="status-dot"></span>Final snapshot</span>`)}
      ${comparisonBanner()}
      <div class="kpi-strip ${applicationStartupMs != null ? "tracked" : "untracked"}">${startupCard}${kpiHtml}</div>
      <div class="overview-grid">
        <article class="panel critical-panel">
          <header class="panel-header"><div><p class="panel-kicker">CAUSAL STARTUP TRACE</p><h3>Critical path · ${ms(data.snapshot.criticalPathMs)}</h3></div><span class="kind-chip">87.4% of composition</span></header>
          ${criticalRibbon()}
        </article>
        <article class="panel">
          <header class="panel-header"><div><p class="panel-kicker">STRUCTURED FINDINGS</p><h3>Evidence requiring attention</h3></div><span class="kind-chip">2 open</span></header>
          <div class="panel-body findings-list">${findings}</div>
        </article>
        <article class="panel">
          <header class="panel-header"><div><p class="panel-kicker">TOP CONTRIBUTORS</p><h3>Where to inspect first</h3></div><button class="small-button" type="button" data-navigate="performance">Full trace</button></header>
          <div class="panel-body hotspot-list">${hotspots}</div>
        </article>
        <article class="panel">
          <header class="panel-header"><div><p class="panel-kicker">HOST CONTEXT</p><h3>Captured composition facts</h3></div>${icon("server")}</header>
          <div class="panel-body fact-list">
            <div class="fact-row"><span>Host</span><strong>FlightService.API</strong></div>
            <div class="fact-row"><span>Environment</span><strong>Development</strong></div>
            <div class="fact-row"><span>Framework</span><strong>.NET 10.0 · Monica dev</strong></div>
            <div class="fact-row"><span>Type discovery</span><strong>One compilation · ${ms(data.snapshot.discoveryMs)}</strong></div>
            <div class="fact-row"><span>Assembly scan</span><strong>47 scanned · 3 partial</strong></div>
          </div>
        </article>
      </div>`;
  }

  function filteredContributors() {
    return data.contributors
      .filter(item => !state.perfHideZero || item.ms > 0)
      .filter(item => item.ms >= state.perfMinimum)
      .slice(0, state.perfLimit);
  }

  function stageCards() {
    return data.stages.map((stage, index) => `
      <article class="stage-card" data-tone="${stage.tone}">
        <span class="stage-index">0${index + 1}</span><h4>${stage.name}</h4><code title="${stage.code}">${stage.code}</code>
        <div class="stage-duration">${fmt(stage.ms)} <small>ms</small></div><div class="stage-count">${stage.countLabel}</div>
      </article>`).join("");
  }

  function renderPerformance() {
    const contributors = filteredContributors();
    const timeline = contributors.map(item => {
      const left = Math.min(98, item.offset / data.snapshot.totalMs * 100);
      const width = Math.max(.3, item.ms / data.snapshot.totalMs * 100);
      return `<div class="timeline-row"><div class="timeline-label"><strong>${escapeHtml(item.label)}</strong><span>${escapeHtml(item.phase)}</span></div><div class="timeline-track"><button class="timeline-bar ${item.blocking ? (item.ms > 400 ? "amber" : "") : "non-blocking"}" type="button" data-open-module="${item.moduleId}" style="left:${left}%;width:${Math.min(width, 100 - left)}%" title="${escapeHtml(item.label)} · ${ms(item.ms)}"><span class="timeline-value">${ms(item.ms)}</span></button></div></div>`;
    }).join("");
    const chain = data.criticalPath.slice(1).map((segment, index) => `
      ${index ? `<div class="chain-link"></div>` : ""}<div class="chain-segment"><span class="chain-number">${String(index + 1).padStart(2, "0")}</span><div><strong>${segment.detail}</strong><span>${segment.moduleId ? moduleById(segment.moduleId)?.name ?? "System" : "Module system"}</span></div><code>${ms(segment.ms)}</code></div>`).join("");

    $("#section-performance").innerHTML = `
      ${sectionHeading("performance-title", "02 / WALL-CLOCK CAUSALITY", "Performance", "Separate blocking time, parallel startup work, and factual type-discovery stages.", `<span class="kind-chip">Debug cold start</span>`)}
      ${comparisonBanner()}
      <div class="performance-grid">
        <article class="panel">
          <header class="panel-header">
            <div><p class="panel-kicker">STARTUP WATERFALL</p><h3>Module and system contributors</h3></div>
            <div class="control-row">
              <label class="control-label">Top <select id="perf-limit"><option ${state.perfLimit === 10 ? "selected" : ""}>10</option><option ${state.perfLimit === 25 ? "selected" : ""}>25</option><option ${state.perfLimit === 100 ? "selected" : ""}>100</option></select></label>
              <label class="control-label">Min <input id="perf-minimum" type="number" min="0" value="${state.perfMinimum}"> ms</label>
              <label class="checkbox-label"><input id="perf-hide-zero" type="checkbox" ${state.perfHideZero ? "checked" : ""}> Hide zero</label>
            </div>
          </header>
          <div class="panel-body timeline"><div class="timeline-scale"><span>0</span><span>867</span><span>1,734</span><span>2,601</span><span>3,468 ms</span></div>${timeline || `<div class="empty-state"><div>${icon("filter-x")}<strong>No callbacks match these filters</strong></div></div>`}</div>
        </article>
        <aside class="panel">
          <header class="panel-header"><div><p class="panel-kicker">BLOCKING CHAIN</p><h3>${ms(data.snapshot.criticalPathMs)} causal path</h3></div><span class="kind-chip">complete</span></header>
          <div class="panel-body blocking-chain">${chain}</div>
        </aside>
        <article class="panel span-all">
          <header class="panel-header"><div><p class="panel-kicker">ONE COMPILATION, FIVE STAGES</p><h3>Type discovery · ${ms(data.snapshot.discoveryMs)}</h3><p>Registration commit consumes compiled matches; it does not enumerate types again.</p></div><button class="small-button" type="button" data-navigate="discovery">Open discovery inventory</button></header>
          <div class="panel-body stage-grid">${stageCards()}</div>
        </article>
      </div>`;
  }

  function filteredModules() {
    const search = state.moduleSearch.trim().toLowerCase();
    return data.modules.filter(module => {
      const searchMatch = !search || [module.name, module.key, module.assembly, module.capability].some(value => value.toLowerCase().includes(search));
      const capabilityMatch = state.moduleCapability === "all" || module.capability === state.moduleCapability;
      const assemblyMatch = state.moduleAssembly === "all" || module.assembly === state.moduleAssembly;
      const costMatch = state.moduleCost === "all" || (state.moduleCost === "high" && module.duration >= 50) || (state.moduleCost === "medium" && module.duration >= 10 && module.duration < 50) || (state.moduleCost === "low" && module.duration < 10);
      return searchMatch && capabilityMatch && assemblyMatch && costMatch;
    }).sort((a, b) => b.duration - a.duration || a.name.localeCompare(b.name));
  }

  function renderModules() {
    const capabilities = [...new Set(data.modules.map(module => module.capability))].sort();
    const assemblies = [...new Set(data.modules.map(module => module.assembly))].sort();
    const filtered = filteredModules();
    const pages = Math.max(1, Math.ceil(filtered.length / state.modulePageSize));
    state.modulePage = Math.min(state.modulePage, pages);
    const start = (state.modulePage - 1) * state.modulePageSize;
    const visible = filtered.slice(start, start + state.modulePageSize);
    const rows = visible.map(module => `
      <tr data-open-module="${module.id}">
        <td class="primary-cell"><strong>${escapeHtml(module.name)}</strong><code title="${escapeHtml(module.key)}">${escapeHtml(module.key)}</code></td>
        <td><span class="kind-chip">${escapeHtml(module.capability)}</span></td>
        <td>${escapeHtml(module.assembly)}</td>
        <td><span class="state-chip">${module.state}</span></td>
        <td class="table-number">${ms(module.duration)}</td>
        <td class="table-number">${module.dependencies.length}</td>
        <td class="table-number">${module.dependents.length}</td>
        <td class="table-number">${fmt(module.discoveryMatches)}</td>
      </tr>`).join("");
    const cards = visible.map(module => `
      <article class="module-card" data-open-module="${module.id}" role="button" tabindex="0"><div class="module-card-top"><div><strong>${escapeHtml(module.name)}</strong><code>${escapeHtml(module.key)}</code></div><code>${ms(module.duration)}</code></div><div class="module-card-meta"><span class="kind-chip">${module.capability}</span><span>${module.dependencies.length} dependencies</span><span>${fmt(module.discoveryMatches)} matches</span></div></article>`).join("");

    $("#section-modules").innerHTML = `
      ${sectionHeading("modules-title", "03 / MODULE CATALOG", "Modules", "Search the immutable module graph, then inspect one module without losing catalog context.", `<span class="kind-chip">${filtered.length} shown / 93 active</span>`)}
      ${comparisonBanner()}
      <article class="panel">
        <header class="panel-header">
          <div class="module-toolbar" style="width:100%">
            <div class="toolbar-field"><span>Search</span><label>${icon("search")}<input id="module-search" type="search" value="${escapeHtml(state.moduleSearch)}" placeholder="Name, key, assembly or capability"></label></div>
            <label class="toolbar-field"><span>Capability</span><select id="module-capability"><option value="all">All capabilities</option>${capabilities.map(value => `<option ${state.moduleCapability === value ? "selected" : ""}>${value}</option>`).join("")}</select></label>
            <label class="toolbar-field"><span>Assembly</span><select id="module-assembly"><option value="all">All assemblies</option>${assemblies.map(value => `<option ${state.moduleAssembly === value ? "selected" : ""}>${value}</option>`).join("")}</select></label>
            <label class="toolbar-field hide-tablet"><span>State</span><select disabled><option>Active</option></select></label>
            <label class="toolbar-field"><span>Registration cost</span><select id="module-cost"><option value="all">All costs</option><option value="high" ${state.moduleCost === "high" ? "selected" : ""}>50 ms and above</option><option value="medium" ${state.moduleCost === "medium" ? "selected" : ""}>10–49 ms</option><option value="low" ${state.moduleCost === "low" ? "selected" : ""}>Below 10 ms</option></select></label>
            <button class="small-button" type="button" data-action="clear-module-filters">Clear</button>
          </div>
        </header>
        <div class="module-table-wrap data-table-wrap"><table class="data-table"><thead><tr><th>Module / key</th><th>Capability</th><th>Assembly</th><th>State</th><th class="table-number">Cost</th><th class="table-number">Deps</th><th class="table-number">Used by</th><th class="table-number">Matches</th></tr></thead><tbody>${rows}</tbody></table></div>
        <div class="module-mobile-list panel-body">${cards || `<div class="empty-state"><div>${icon("search-x")}<strong>No modules match</strong><p>Remove a filter or search for another module.</p></div></div>`}</div>
        <footer class="pagination"><span>${filtered.length ? `${start + 1}–${Math.min(start + state.modulePageSize, filtered.length)}` : "0"} of ${filtered.length} filtered modules · 93 total</span><div class="pagination-actions"><button class="small-button" data-module-page="prev" ${state.modulePage === 1 ? "disabled" : ""}>Previous</button>${[1, 2, 3].filter(page => page <= pages).map(page => `<button class="small-button ${state.modulePage === page ? "active" : ""}" data-module-page="${page}">${page}</button>`).join("")}<button class="small-button" data-module-page="next" ${state.modulePage === pages ? "disabled" : ""}>Next</button><select class="small-button" id="module-page-size" aria-label="Rows per page"><option ${state.modulePageSize === 25 ? "selected" : ""}>25</option><option ${state.modulePageSize === 50 ? "selected" : ""}>50</option><option ${state.modulePageSize === 100 ? "selected" : ""}>100</option></select></div></footer>
      </article>`;
  }

  function neighborhood() {
    const focus = moduleById(state.dependencyFocus) || data.modules[0];
    const directIds = new Set([focus.id, ...focus.dependencies, ...focus.dependents]);
    if (state.dependencyDepth === "full") data.modules.slice(0, 13).forEach(module => directIds.add(module.id));
    const nodes = [...directIds].map(moduleById).filter(Boolean);
    const nodeSet = new Set(nodes.map(node => node.id));
    const edges = data.edges.filter(([source, target]) => nodeSet.has(source) && nodeSet.has(target));
    return { focus, nodes, edges };
  }

  function graphMarkup(graph) {
    const positions = new Map();
    positions.set(graph.focus.id, { x: 50, y: 48, role: "selected" });
    const deps = graph.nodes.filter(node => graph.focus.dependencies.includes(node.id));
    const dependents = graph.nodes.filter(node => graph.focus.dependents.includes(node.id));
    const extras = graph.nodes.filter(node => node.id !== graph.focus.id && !deps.includes(node) && !dependents.includes(node));
    deps.forEach((node, index) => positions.set(node.id, { x: 18, y: 20 + (index * 60 / Math.max(1, deps.length - 1 || 1)), role: "dependency" }));
    dependents.forEach((node, index) => positions.set(node.id, { x: 82, y: 20 + (index * 60 / Math.max(1, dependents.length - 1 || 1)), role: "dependent" }));
    extras.forEach((node, index) => {
      const angle = (Math.PI * 2 * index / Math.max(1, extras.length)) - Math.PI / 2;
      positions.set(node.id, { x: 50 + Math.cos(angle) * 39, y: 48 + Math.sin(angle) * 39, role: index % 2 ? "dependency" : "dependent" });
    });
    const lines = graph.edges.map(([source, target]) => {
      const from = positions.get(source), to = positions.get(target);
      if (!from || !to) return "";
      const focused = source === graph.focus.id || target === graph.focus.id;
      return `<line class="graph-edge ${focused ? "focused" : ""}" x1="${from.x}%" y1="${from.y}%" x2="${to.x}%" y2="${to.y}%"></line>`;
    }).join("");
    const nodes = graph.nodes.map(module => {
      const position = positions.get(module.id);
      return `<button class="graph-node ${position.role} ${module.id === graph.focus.id ? "selected" : ""}" data-dependency-focus="${module.id}" type="button" style="left:${position.x}%;top:${position.y}%"><strong title="${escapeHtml(module.name)}">${escapeHtml(module.name)}</strong><span>${module.duration} ms · ${module.dependencies.length} deps</span></button>`;
    }).join("");
    return `<svg aria-hidden="true"><defs><marker id="arrowhead" markerWidth="7" markerHeight="7" refX="6" refY="3.5" orient="auto"><polygon points="0 0, 7 3.5, 0 7" fill="var(--line-strong)"></polygon></marker></defs>${lines}</svg>${nodes}<div class="graph-legend"><span><i class="legend-dot"></i>Selected</span><span><i class="legend-dot dep"></i>Depends on</span><span><i class="legend-dot child"></i>Used by</span></div>`;
  }

  function renderDependencies() {
    const graph = neighborhood();
    const edgeRows = graph.edges.map(([source, target]) => `<tr data-dependency-focus="${source}"><td class="primary-cell"><strong>${escapeHtml(moduleById(source).name)}</strong><code>${escapeHtml(source)}</code></td><td>${icon("arrow-right")}</td><td class="primary-cell"><strong>${escapeHtml(moduleById(target).name)}</strong><code>${escapeHtml(target)}</code></td><td><span class="kind-chip">Direct</span></td></tr>`).join("");
    $("#section-dependencies").innerHTML = `
      ${sectionHeading("dependencies-title", "04 / COMPILED TOPOLOGY", "Dependencies", "Start with a readable one-hop neighborhood; expand the 93-module graph only when evidence requires it.", `<span class="kind-chip">128 direct edges · depth 7</span>`)}
      ${comparisonBanner()}
      <div class="dependency-layout">
        <article class="panel">
          <header class="panel-header"><div><p class="panel-kicker">NEIGHBORHOOD EXPLORER</p><h3>${escapeHtml(graph.focus.name)}</h3></div><div class="graph-toolbar"><label class="toolbar-field"><span>Focus module</span><select id="dependency-focus">${data.modules.map(module => `<option value="${module.id}" ${module.id === graph.focus.id ? "selected" : ""}>${escapeHtml(module.name)}</option>`).join("")}</select></label><div class="segmented-control"><button class="small-button ${state.dependencyDepth === "one" ? "active" : ""}" data-dependency-depth="one">1 hop</button><button class="small-button ${state.dependencyDepth === "full" ? "active" : ""}" data-dependency-depth="full">Expanded</button></div></div></header>
          <div class="graph-canvas" role="img" aria-label="Direct dependency neighborhood for ${escapeHtml(graph.focus.name)}">${graphMarkup(graph)}</div>
        </article>
        <aside class="panel">
          <header class="panel-header"><div><p class="panel-kicker">SELECTED EVIDENCE</p><h3>Direct relationships</h3></div><button class="icon-button" type="button" data-open-module="${graph.focus.id}" aria-label="Open module details">${icon("panel-right-open")}</button></header>
          <div class="panel-body neighborhood-summary">
            <button class="neighborhood-module" data-open-module="${graph.focus.id}" type="button"><span>${escapeHtml(graph.focus.key)}</span><strong>${escapeHtml(graph.focus.summary)}</strong></button>
            <div class="fact-row"><span>Registration cost</span><strong>${ms(graph.focus.duration)}</strong></div><div class="fact-row"><span>Dependency depth</span><strong>${graph.focus.dependencies.length ? "4" : "0"}</strong></div><div class="fact-row"><span>Direct dependencies</span><strong>${graph.focus.dependencies.length}</strong></div><div class="fact-row"><span>Direct dependents</span><strong>${graph.focus.dependents.length}</strong></div>
            <div class="relationship-block"><h4>DEPENDS ON</h4><div class="relationship-list">${graph.focus.dependencies.length ? graph.focus.dependencies.map(id => `<button data-dependency-focus="${id}">${escapeHtml(moduleById(id)?.name ?? id)}</button>`).join("") : "<span class='kind-chip'>Graph root</span>"}</div></div>
            <div class="relationship-block"><h4>USED BY</h4><div class="relationship-list">${graph.focus.dependents.length ? graph.focus.dependents.map(id => `<button data-dependency-focus="${id}">${escapeHtml(moduleById(id)?.name ?? id)}</button>`).join("") : "<span class='kind-chip'>No direct dependents</span>"}</div></div>
          </div>
        </aside>
      </div>
      <article class="panel graph-table-panel">
        <header class="panel-header"><div><p class="panel-kicker">ACCESSIBLE ALTERNATIVE</p><h3>Direct-edge table</h3><p>The table mirrors the current graph neighborhood for keyboard and screen-reader inspection.</p></div><button class="small-button" type="button" data-action="toggle-dependency-table">${state.dependencyTable ? "Hide table" : "Show table"}</button></header>
        ${state.dependencyTable ? `<div class="data-table-wrap"><table class="data-table"><thead><tr><th>Dependent module</th><th></th><th>Required module</th><th>Edge</th></tr></thead><tbody>${edgeRows}</tbody></table></div>` : ""}
      </article>`;
  }

  function filteredAssemblies() {
    const order = { ResolutionFailed: 0, PartialTypeLoad: 1, Excluded: 2, Scanned: 3 };
    const search = state.inventorySearch.trim().toLowerCase();
    return data.assemblies.filter(item => (!search || [item.name, item.detail, item.path].some(value => String(value).toLowerCase().includes(search))) && (state.inventoryOutcome === "all" || item.outcome === state.inventoryOutcome)).sort((a, b) => order[a.outcome] - order[b.outcome] || a.name.localeCompare(b.name));
  }

  function renderDiscovery() {
    const assemblies = filteredAssemblies();
    const pages = Math.max(1, Math.ceil(assemblies.length / state.inventoryPageSize));
    state.inventoryPage = Math.min(state.inventoryPage, pages);
    const start = (state.inventoryPage - 1) * state.inventoryPageSize;
    const visible = assemblies.slice(start, start + state.inventoryPageSize);
    const rows = visible.map(item => {
      const outcomeClass = item.outcome === "ResolutionFailed" ? "failed" : item.outcome === "PartialTypeLoad" ? "partial" : item.outcome === "Excluded" ? "excluded" : "";
      return `<tr><td class="primary-cell"><strong>${escapeHtml(item.name)}</strong><code>${escapeHtml(item.detail)}</code></td><td><span class="outcome-chip ${outcomeClass}">${escapeHtml(item.outcome)}</span></td><td class="table-number">${item.types == null ? "—" : fmt(item.types)}</td><td class="table-number">${ms(item.ms)}</td><td class="path-cell"><code title="${escapeHtml(item.path)}">${escapeHtml(item.path)}</code></td></tr>`;
    }).join("");
    const queries = data.queryContributions.map(query => `<div class="query-row"><div><strong>${escapeHtml(query.module)}</strong><span>${escapeHtml(query.query)}</span><div class="query-bar"><span style="width:${Math.max(3, query.matches / data.queryContributions[0].matches * 100)}%"></span></div></div><code>${fmt(query.matches)} matches · ${query.evaluatedMs} ms</code></div>`).join("");

    $("#section-discovery").innerHTML = `
      ${sectionHeading("discovery-title", "05 / CENTRALIZED TYPE DISCOVERY", "Discovery", "One host-scoped compilation, separated into attributable stages and scan outcomes.", `<span class="kind-chip">6,842 types · 14 queries</span>`)}
      ${comparisonBanner()}
      <article class="panel">
        <header class="panel-header"><div><p class="panel-kicker">ONE COMPILATION, FIVE STAGES</p><h3>${ms(data.snapshot.discoveryMs)} total discovery window</h3><p>The cached type snapshot is enumerated once; commit reuses compiled matches.</p></div><span class="status-badge success"><span class="status-dot"></span>Completed</span></header>
        <div class="panel-body stage-grid">${stageCards()}</div>
      </article>
      <div class="discovery-summary" style="margin-top:16px">
        <article class="panel"><header class="panel-header"><div><p class="panel-kicker">QUERY CONTRIBUTIONS</p><h3>Where evaluation and matches originate</h3></div><span class="kind-chip">14 distinct</span></header><div class="panel-body query-list">${queries}</div></article>
        <aside class="panel"><header class="panel-header"><div><p class="panel-kicker">SCAN OUTCOMES</p><h3>Assembly evidence</h3></div>${icon("scan-line")}</header><div class="panel-body outcome-grid"><div class="outcome-cell"><span>Scanned</span><strong>47</strong><small>6,842 types</small></div><div class="outcome-cell"><span>Excluded</span><strong>2</strong><small>policy match</small></div><div class="outcome-cell problem"><span>Partial</span><strong>3</strong><small>usable types kept</small></div><div class="outcome-cell problem"><span>Failed</span><strong>1</strong><small>optional adapter</small></div></div><div class="panel-body fact-list"><div class="fact-row"><span>Plans declared</span><strong>93</strong></div><div class="fact-row"><span>Non-empty plans</span><strong>14</strong></div><div class="fact-row"><span>Registration matches</span><strong>1,137</strong></div><div class="fact-row"><span>Service writes</span><strong>247 add · 19 replace · 8 skip</strong></div></div></aside>
      </div>
      <article class="panel inventory-panel">
        <header class="panel-header"><div><p class="panel-kicker">LAZY DETAIL</p><h3>Assembly inventory</h3><p>Failure-first · paths remain local and are removed from exports.</p></div>${state.inventoryLoaded ? `<button class="small-button" type="button" data-action="collapse-inventory">Collapse inventory</button>` : `<button class="secondary-button" type="button" data-action="load-inventory">Load 47 assembly records ${icon("arrow-down")}</button>`}</header>
        ${state.inventoryLoaded ? `<div class="panel-body inventory-toolbar"><div class="toolbar-field"><span>Search inventory</span><label>${icon("search")}<input id="inventory-search" type="search" value="${escapeHtml(state.inventorySearch)}" placeholder="Assembly, outcome or path"></label></div><label class="toolbar-field"><span>Outcome</span><select id="inventory-outcome"><option value="all">All outcomes</option><option value="ResolutionFailed" ${state.inventoryOutcome === "ResolutionFailed" ? "selected" : ""}>Resolution failed</option><option value="PartialTypeLoad" ${state.inventoryOutcome === "PartialTypeLoad" ? "selected" : ""}>Partial type load</option><option value="Scanned" ${state.inventoryOutcome === "Scanned" ? "selected" : ""}>Scanned</option><option value="Excluded" ${state.inventoryOutcome === "Excluded" ? "selected" : ""}>Excluded</option></select></label><button class="small-button" data-action="clear-inventory">Clear</button></div><div class="data-table-wrap"><table class="data-table"><thead><tr><th>Assembly / detail</th><th>Outcome</th><th class="table-number">Types</th><th class="table-number">Time</th><th>Local path</th></tr></thead><tbody>${rows}</tbody></table></div><footer class="pagination"><span>${assemblies.length ? `${start + 1}–${Math.min(start + state.inventoryPageSize, assemblies.length)}` : "0"} of ${assemblies.length} prototype records · 47 captured</span><div class="pagination-actions"><button class="small-button" data-inventory-page="prev" ${state.inventoryPage === 1 ? "disabled" : ""}>Previous</button><span class="kind-chip">Page ${state.inventoryPage} / ${pages}</span><button class="small-button" data-inventory-page="next" ${state.inventoryPage === pages ? "disabled" : ""}>Next</button></div></footer>` : `<div class="inventory-collapsed">${icon("archive-restore")}<div><strong>Inventory has not been loaded</strong><p>The main snapshot stays compact. Open this detail only when scan evidence is needed.</p></div></div>`}
      </article>`;
  }

  function renderSection(section = state.section) {
    if (section === "overview") renderOverview();
    if (section === "performance") renderPerformance();
    if (section === "modules") renderModules();
    if (section === "dependencies") renderDependencies();
    if (section === "discovery") renderDiscovery();
    refreshIcons();
  }

  function navigate(section, updateHash = true) {
    if (!sections.includes(section)) section = "overview";
    state.section = section;
    $$(".section-tab").forEach(button => button.classList.toggle("active", button.dataset.section === section));
    $("#mobile-navigation").value = section;
    $$(".workbench-section").forEach(panel => {
      const active = panel.dataset.sectionPanel === section;
      panel.hidden = !active;
      panel.classList.toggle("active", active);
    });
    renderSection(section);
    if (updateHash) history.replaceState(null, "", `#${section}${state.selectedModule && section === "modules" ? `/${state.selectedModule}` : ""}`);
    window.scrollTo({ top: 0, behavior: matchMedia("(prefers-reduced-motion: reduce)").matches ? "auto" : "smooth" });
  }

  function drawerMarkup(module, tab) {
    if (tab === "summary") return `<div class="drawer-summary-hero"><span class="kind-chip">${module.capability}</span><p>${escapeHtml(module.summary)}</p></div><div class="drawer-metrics"><div class="drawer-metric"><span>REGISTRATION</span><strong>${ms(module.duration)}</strong></div><div class="drawer-metric"><span>CALLBACKS</span><strong>${module.callbackCount}</strong></div><div class="drawer-metric"><span>MATCHES</span><strong>${fmt(module.discoveryMatches)}</strong></div></div><section class="drawer-section"><h3>Composition identity</h3><div class="fact-list"><div class="fact-row"><span>Assembly</span><strong>${escapeHtml(module.assembly)}</strong></div><div class="fact-row"><span>State</span><strong>Active · final</strong></div><div class="fact-row"><span>Direct dependencies</span><strong>${module.dependencies.length}</strong></div><div class="fact-row"><span>Direct dependents</span><strong>${module.dependents.length}</strong></div></div></section>`;
    if (tab === "performance") {
      const spans = data.contributors.filter(item => item.moduleId === module.id);
      return `<div class="drawer-summary-hero"><span class="panel-kicker">MODULE COST</span><div class="kpi-value"><span>${fmt(module.duration)}</span><small>ms registration</small></div>${module.startupWork ? `<p>${ms(module.startupWork)} non-blocking startup work is reported separately.</p>` : ""}</div><section class="drawer-section"><h3>Recorded spans</h3>${spans.length ? spans.map(span => `<div class="fact-row"><span>${escapeHtml(span.phase)}${span.blocking ? "" : " · non-blocking"}</span><strong class="tabular">${ms(span.ms)}</strong></div>`).join("") : `<div class="empty-state"><div>${icon("activity")}<strong>No span exceeds the visible threshold</strong><p>Zero and sub-millisecond callbacks remain available in the complete trace.</p></div></div>`}</section>`;
    }
    if (tab === "dependencies") return `<section><h3>Direct dependencies</h3><div class="relationship-list">${module.dependencies.length ? module.dependencies.map(id => `<button data-dependency-focus="${id}" data-navigate="dependencies">${escapeHtml(moduleById(id)?.name ?? id)}</button>`).join("") : `<span class="kind-chip">Graph root</span>`}</div></section><section class="drawer-section"><h3>Direct dependents</h3><div class="relationship-list">${module.dependents.length ? module.dependents.map(id => `<button data-dependency-focus="${id}" data-navigate="dependencies">${escapeHtml(moduleById(id)?.name ?? id)}</button>`).join("") : `<span class="kind-chip">No direct dependents</span>`}</div></section><button class="secondary-button" style="margin-top:22px" data-dependency-focus="${module.id}" data-navigate="dependencies">Open neighborhood explorer</button>`;
    if (tab === "options") return `<div class="privacy-note" style="margin-top:0">${icon("shield-check")}<div><strong>Explicitly safe projection</strong><p>Only bounded values, presence, and collection counts are exposed. Raw options are unavailable.</p></div></div><section class="drawer-section">${module.options.length ? module.options.map(option => `<div class="option-row"><div><strong>${escapeHtml(option.name)}</strong><span>${option.kind === "presence" ? "Presence only · value redacted" : option.kind === "count" ? "Collection count" : "Approved scalar value"}</span></div><code>${escapeHtml(option.value)}</code></div>`).join("") : `<div class="empty-state"><div>${icon("shield-minus")}<strong>No option diagnostics exposed</strong><p>This module has not opted in to any safe diagnostic fields.</p></div></div>`}</section>`;
    return `<div class="empty-state"><div>${icon("circle-check-big")}<strong>No module errors</strong><p>The final composition snapshot contains no exception or registration failure related to ${escapeHtml(module.name)}.</p></div></div>`;
  }

  function openModule(id, tab = "summary") {
    const module = moduleById(id);
    if (!module) return;
    state.selectedModule = id;
    state.drawerTab = tab;
    $("#drawer-title").textContent = module.name;
    $("#drawer-key").textContent = module.key;
    $$("[data-drawer-tab]").forEach(button => button.classList.toggle("active", button.dataset.drawerTab === tab));
    $("#drawer-content").innerHTML = drawerMarkup(module, tab);
    $("#module-drawer").classList.add("open");
    $("#module-drawer").setAttribute("aria-hidden", "false");
    $("#drawer-scrim").hidden = false;
    refreshIcons();
    history.replaceState(null, "", `#${state.section}/${id}`);
  }

  function closeDrawers() {
    state.selectedModule = null;
    $("#module-drawer").classList.remove("open");
    $("#module-drawer").setAttribute("aria-hidden", "true");
    $("#help-drawer").classList.remove("open");
    $("#help-drawer").setAttribute("aria-hidden", "true");
    $("#drawer-scrim").hidden = true;
    history.replaceState(null, "", `#${state.section}`);
  }

  function showToast(title, detail = "") {
    const toast = document.createElement("div");
    toast.className = "toast";
    toast.innerHTML = `${icon("circle-check")}<div><strong>${escapeHtml(title)}</strong>${detail ? `<span>${escapeHtml(detail)}</span>` : ""}</div>`;
    $("#toast-region").append(toast);
    refreshIcons();
    setTimeout(() => toast.remove(), 3200);
  }

  function renderTranslations() {
    const dictionary = data.translations[state.language];
    $$(`[data-i18n]`).forEach(element => {
      const value = dictionary[element.dataset.i18n];
      if (value) element.textContent = value;
    });
    $("#language-button").textContent = state.language === "en" ? "中" : "EN";
    document.documentElement.lang = state.language === "en" ? "en" : "zh-CN";
  }

  function applyBaseline(baseline) {
    state.baseline = baseline;
    $("#baseline-state").textContent = data.translations[state.language].baselineLoaded;
    $("#comparison-dialog").close();
    renderSection();
    showToast("Baseline loaded", `${baseline.name} · schema v${baseline.schemaVersion}`);
  }

  async function readBaseline(file) {
    try {
      const payload = JSON.parse(await file.text());
      if (payload.schemaVersion !== data.snapshot.schemaVersion) throw new Error(`Schema v${payload.schemaVersion ?? "unknown"} is incompatible. This workbench requires schema v${data.snapshot.schemaVersion}.`);
      const baseline = payload.summary ? { ...payload.summary, schemaVersion: payload.schemaVersion, name: payload.name || file.name } : { ...payload, name: payload.name || file.name };
      applyBaseline(baseline);
    } catch (error) {
      $("#baseline-error").textContent = error.message || "This file is not a compatible diagnostics export.";
    }
  }

  function exportSnapshot() {
    const sanitized = {
      schemaVersion: data.snapshot.schemaVersion,
      name: "FlightService.API · snapshot #17",
      exportedAt: new Date().toISOString(),
      privacy: "Sanitized: option diagnostics, assembly paths, stack traces, and raw exceptions omitted.",
      summary: {
        applicationStartupMs: state.startupTracked ? data.snapshot.applicationStartupMs : null,
        totalMs: data.snapshot.totalMs,
        registrationMs: data.snapshot.registrationMs,
        discoveryMs: data.snapshot.discoveryMs,
        moduleCount: data.snapshot.moduleCount,
        findingCount: data.snapshot.findingCount
      },
      stages: data.stages.map(({ code, ms: duration, countLabel }) => ({ code, duration, countLabel })),
      modules: data.modules.map(({ key, name, capability, state: moduleState, duration, dependencies }) => ({ key, name, capability, state: moduleState, duration, dependencyCount: dependencies.length })),
      edges: data.edges
    };
    const url = URL.createObjectURL(new Blob([JSON.stringify(sanitized, null, 2)], { type: "application/json" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = "flightservice-module-diagnostics-snapshot-17.json";
    link.click();
    URL.revokeObjectURL(url);
    showToast("Sanitized snapshot exported", "No option values, paths, stack traces, or raw exceptions included.");
  }

  function bindEvents() {
    document.addEventListener("click", event => {
      const target = event.target.closest("button, [role='button'], tr[data-open-module]");
      if (!target) return;

      if (target.dataset.section) navigate(target.dataset.section);
      if (target.dataset.navigate) {
        const focus = target.dataset.dependencyFocus;
        if (focus) state.dependencyFocus = focus;
        navigate(target.dataset.navigate);
        if (target.dataset.navigate !== "modules") closeDrawers();
      }
      if (target.dataset.criticalId) {
        state.selectedCritical = target.dataset.criticalId;
        renderSection();
      }
      if (target.dataset.criticalOpen) {
        const segment = data.criticalPath.find(item => item.id === target.dataset.criticalOpen);
        if (segment?.moduleId) openModule(segment.moduleId, "performance");
        else navigate(target.dataset.targetSection || segment?.section || "performance");
      }
      if (target.dataset.openModule) openModule(target.dataset.openModule);
      if (target.dataset.drawerTab && state.selectedModule) openModule(state.selectedModule, target.dataset.drawerTab);
      if (target.dataset.dependencyFocus) {
        state.dependencyFocus = target.dataset.dependencyFocus;
        if (state.section === "dependencies") renderSection();
      }
      if (target.dataset.dependencyDepth) {
        state.dependencyDepth = target.dataset.dependencyDepth;
        renderSection();
      }
      if (target.dataset.modulePage) {
        const count = Math.max(1, Math.ceil(filteredModules().length / state.modulePageSize));
        state.modulePage = target.dataset.modulePage === "prev" ? Math.max(1, state.modulePage - 1) : target.dataset.modulePage === "next" ? Math.min(count, state.modulePage + 1) : Number(target.dataset.modulePage);
        renderSection();
      }
      if (target.dataset.inventoryPage) {
        const count = Math.max(1, Math.ceil(filteredAssemblies().length / state.inventoryPageSize));
        state.inventoryPage = target.dataset.inventoryPage === "prev" ? Math.max(1, state.inventoryPage - 1) : Math.min(count, state.inventoryPage + 1);
        renderSection();
      }
      if (target.dataset.action === "clear-module-filters") {
        Object.assign(state, { moduleSearch: "", moduleCapability: "all", moduleAssembly: "all", moduleCost: "all", modulePage: 1 });
        renderSection();
      }
      if (target.dataset.action === "toggle-dependency-table") { state.dependencyTable = !state.dependencyTable; renderSection(); }
      if (target.dataset.action === "load-inventory") { state.inventoryLoaded = true; renderSection(); }
      if (target.dataset.action === "collapse-inventory") { state.inventoryLoaded = false; renderSection(); }
      if (target.dataset.action === "clear-inventory") { Object.assign(state, { inventorySearch: "", inventoryOutcome: "all", inventoryPage: 1 }); renderSection(); }
      if (target.dataset.action === "remove-baseline") {
        state.baseline = null;
        $("#baseline-state").textContent = data.translations[state.language].noBaseline;
        renderSection();
      }
      if (target.dataset.action === "toggle-startup-timing") {
        state.startupTracked = !state.startupTracked;
        renderOverview();
        refreshIcons();
      }
    });

    document.addEventListener("change", event => {
      const target = event.target;
      if (target.id === "mobile-navigation") navigate(target.value);
      if (target.id === "perf-limit") { state.perfLimit = Number(target.value); renderSection(); }
      if (target.id === "perf-minimum") { state.perfMinimum = Math.max(0, Number(target.value) || 0); renderSection(); }
      if (target.id === "perf-hide-zero") { state.perfHideZero = target.checked; renderSection(); }
      if (target.id === "module-capability") { state.moduleCapability = target.value; state.modulePage = 1; renderSection(); }
      if (target.id === "module-assembly") { state.moduleAssembly = target.value; state.modulePage = 1; renderSection(); }
      if (target.id === "module-cost") { state.moduleCost = target.value; state.modulePage = 1; renderSection(); }
      if (target.id === "module-page-size") { state.modulePageSize = Number(target.value); state.modulePage = 1; renderSection(); }
      if (target.id === "dependency-focus") { state.dependencyFocus = target.value; renderSection(); }
      if (target.id === "inventory-outcome") { state.inventoryOutcome = target.value; state.inventoryPage = 1; renderSection(); }
      if (target.id === "baseline-file" && target.files[0]) readBaseline(target.files[0]);
    });

    document.addEventListener("input", event => {
      const target = event.target;
      if (target.id === "module-search") { state.moduleSearch = target.value; state.modulePage = 1; renderModules(); refreshIcons(); $("#module-search")?.focus(); $("#module-search")?.setSelectionRange(state.moduleSearch.length, state.moduleSearch.length); }
      if (target.id === "inventory-search") { state.inventorySearch = target.value; state.inventoryPage = 1; renderDiscovery(); refreshIcons(); $("#inventory-search")?.focus(); $("#inventory-search")?.setSelectionRange(state.inventorySearch.length, state.inventorySearch.length); }
    });

    $("#drawer-close").addEventListener("click", closeDrawers);
    $("#help-close").addEventListener("click", closeDrawers);
    $("#drawer-scrim").addEventListener("click", closeDrawers);
    $("#help-button").addEventListener("click", () => {
      $("#help-drawer").classList.add("open");
      $("#help-drawer").setAttribute("aria-hidden", "false");
      $("#drawer-scrim").hidden = false;
    });
    $("#baseline-button").addEventListener("click", () => { $("#baseline-error").textContent = ""; $("#comparison-dialog").showModal(); });
    $("#select-baseline").addEventListener("click", () => $("#baseline-file").click());
    $("#demo-baseline").addEventListener("click", () => applyBaseline(data.baseline));
    $("#export-button").addEventListener("click", exportSnapshot);
    $("#theme-button").addEventListener("click", () => {
      state.theme = state.theme === "light" ? "dark" : "light";
      document.documentElement.dataset.theme = state.theme;
      $("#theme-button").innerHTML = icon(state.theme === "light" ? "moon" : "sun");
      $("#theme-button").setAttribute("aria-label", `Use ${state.theme === "light" ? "dark" : "light"} theme`);
      refreshIcons();
    });
    $("#language-button").addEventListener("click", () => {
      state.language = state.language === "en" ? "zh" : "en";
      renderTranslations();
      renderSection();
    });
    $("#refresh-button").addEventListener("click", event => {
      event.currentTarget.classList.add("refreshing");
      setTimeout(() => event.currentTarget.classList.remove("refreshing"), 500);
      showToast("Snapshot unchanged", "Revision 17 is final and immutable.");
    });
    document.addEventListener("keydown", event => { if (event.key === "Escape") closeDrawers(); });

    const dropzone = $("#baseline-dropzone");
    ["dragenter", "dragover"].forEach(type => dropzone.addEventListener(type, event => { event.preventDefault(); dropzone.classList.add("dragover"); }));
    ["dragleave", "drop"].forEach(type => dropzone.addEventListener(type, event => { event.preventDefault(); dropzone.classList.remove("dragover"); }));
    dropzone.addEventListener("drop", event => { const file = event.dataTransfer.files[0]; if (file) readBaseline(file); });
  }

  function initialize() {
    const route = location.hash.slice(1).split("/");
    if (sections.includes(route[0])) state.section = route[0];
    renderOverview();
    renderPerformance();
    renderModules();
    renderDependencies();
    renderDiscovery();
    bindEvents();
    navigate(state.section, false);
    if (route[1] && moduleById(route[1])) openModule(route[1]);
    renderTranslations();
    refreshIcons();
  }

  initialize();
})();
