(function () {
  const data = window.MonicaDocsPrototype;
  const flatDocs = data.docs.flatMap(group => group.items.map(item => ({ ...item, group: group.group })));
  const uiText = {
    en: {
      "brand.subtitle": "Modular .NET Infrastructure",
      "nav.overview": "Overview",
      "nav.docs": "Docs",
      "nav.modules": "Modules",
      "nav.architecture": "Architecture",
      "actions.signalMode": "Signal mode",
      "actions.search": "Search",
      "actions.copy": "Copy",
      "actions.copied": "Install command copied",
      "hero.eyebrow": "Internal development preview",
      "hero.title": "Infrastructure modules that read like a system map.",
      "hero.lede": "Monica is modular .NET infrastructure for cutting-edge apps: independent packages, unified `Mo.Add*()` registration, built-in dashboards, and a documentation product that doubles as a real Monica modular-monolith example.",
      "hero.primaryCta": "Explore docs shell",
      "hero.secondaryCta": "Browse module atlas",
      "hero.installLabel": "Start with one module",
      "hero.spineLabel": "Module registration spine",
      "hero.optionText": "Configure purpose, defaults, and host-level constraints.",
      "hero.guideText": "Chain providers and optional module capabilities.",
      "hero.moduleText": "Register infrastructure without forcing the whole framework.",
      "hero.facadeText": "Expose public UI and Minimal API entry points with `Res`.",
      "hero.metricModules": "Modules",
      "hero.metricDocs": "Docs source",
      "hero.metricRuntime": "Runtime",
      "narrative.kicker": "Why this site feels different",
      "narrative.title": "Documentation should expose Monica's architecture, not hide it.",
      "narrative.body": "The prototype treats docs as a product surface: routes, module contracts, source markdown, published language DTOs, and host integration are visible and searchable.",
      "principles.independentTitle": "Independent modules",
      "principles.independentBody": "Each package can stand alone, while sharing the same Option, Guide, Module, and extension method shape.",
      "principles.onboardingTitle": "Guided onboarding",
      "principles.onboardingBody": "Quick Start leads into concepts, then module packages, then scenario guides for real host composition.",
      "principles.opsTitle": "Operations-native",
      "principles.opsBody": "Configuration, jobs, profiling, SignalR, DataChannel, and ProjectUnits all point toward inspectable runtime surfaces.",
      "principles.docsTitle": "Docs as example app",
      "principles.docsBody": "`Monica.Docs` becomes a modular monolith with a decoupled frontend and Documentation bounded context.",
      "docs.kicker": "Interactive docs shell",
      "docs.title": "Read by task, module, or command.",
      "docs.body": "The reader mirrors the current `docs/zh-CN` information architecture while presenting an English-first official site frame.",
      "docs.treeButton": "Docs tree",
      "docs.tocTitle": "On this page",
      "docs.apiPreview": "API preview",
      "docs.designAnnotation": "Design annotation",
      "modules.kicker": "Module atlas",
      "modules.title": "Pick only the infrastructure you need.",
      "modules.body": "Module cards are shaped from the current documentation packages: package name, registration entry, UI module, and intended usage.",
      "architecture.kicker": "Documentation product architecture",
      "architecture.title": "From markdown source to a decoupled docs frontend.",
      "architecture.body": "This mirrors the target `Monica.Docs` direction: domain-first backend, published language contracts, and a separate frontend consuming HTTP APIs.",
      "footer.note": "Monica is under active internal development. This prototype is a design artifact for the future official site and docs frontend.",
      "footer.overview": "Overview",
      "footer.docs": "Docs reader",
      "footer.modules": "Module atlas",
      "footer.architecture": "Architecture",
      "filters.all": "All",
      "filters.start": "Start",
      "filters.concepts": "Concepts",
      "filters.modules": "Modules",
      "filters.scenarios": "Scenarios",
      "search.dialogLabel": "Search Monica docs",
      "search.placeholder": "Search modules, concepts, APIs, docs paths...",
      "search.help": "Try: `Res`, `Configuration`, `Mo.AddJobScheduler`, `Facade`",
      "search.closeHint": "Esc to close",
      "search.noResults": "No results yet",
      "search.noResultsHint": "Try a module name, route, or contract term.",
      "search.docKind": "Doc",
      "search.moduleKind": "Module"
    },
    zh: {
      "brand.subtitle": "模块化 .NET 基础设施",
      "nav.overview": "概览",
      "nav.docs": "文档",
      "nav.modules": "模块",
      "nav.architecture": "架构",
      "actions.signalMode": "信号模式",
      "actions.search": "搜索",
      "actions.copy": "复制",
      "actions.copied": "安装命令已复制",
      "hero.eyebrow": "内部开发预览",
      "hero.title": "像系统地图一样阅读基础设施模块。",
      "hero.lede": "Monica 是面向前沿应用的模块化 .NET 基础设施：独立包、统一的 `Mo.Add*()` 注册方式、内置管理面板，以及一个同时作为 Monica 模块化单体示例的文档产品。",
      "hero.primaryCta": "进入文档外壳",
      "hero.secondaryCta": "浏览模块图谱",
      "hero.installLabel": "从一个模块开始",
      "hero.spineLabel": "模块注册主线",
      "hero.optionText": "配置用途、默认值和宿主级约束。",
      "hero.guideText": "串联 Provider 与可选模块能力。",
      "hero.moduleText": "注册基础设施能力，而不强迫引入整个框架。",
      "hero.facadeText": "用 `Res` 暴露 UI 与 Minimal API 公共入口。",
      "hero.metricModules": "模块数量",
      "hero.metricDocs": "文档来源",
      "hero.metricRuntime": "运行时",
      "narrative.kicker": "这个站点为什么不一样",
      "narrative.title": "文档应该暴露 Monica 的架构，而不是把它藏起来。",
      "narrative.body": "这个原型把文档当作产品界面：路由、模块契约、Markdown 源、Published Language DTO 与宿主集成都可见、可搜索。",
      "principles.independentTitle": "独立模块",
      "principles.independentBody": "每个包都可以独立使用，同时共享 Option、Guide、Module 与扩展方法的统一结构。",
      "principles.onboardingTitle": "引导式上手",
      "principles.onboardingBody": "从快速开始进入核心概念，再进入模块包与真实宿主组合场景。",
      "principles.opsTitle": "运维优先",
      "principles.opsBody": "Configuration、JobScheduler、Profiling、SignalR、DataChannel 与 ProjectUnits 都指向可检查的运行时界面。",
      "principles.docsTitle": "文档即示例应用",
      "principles.docsBody": "`Monica.Docs` 将成为带解耦前端与 Documentation 有界上下文的模块化单体。",
      "docs.kicker": "交互式文档外壳",
      "docs.title": "按任务、模块或命令阅读。",
      "docs.body": "阅读器沿用当前 `docs/zh-CN` 信息架构，同时呈现一个正式官网的双语文档框架。",
      "docs.treeButton": "文档树",
      "docs.tocTitle": "本页内容",
      "docs.apiPreview": "API 预览",
      "docs.designAnnotation": "设计标注",
      "modules.kicker": "模块图谱",
      "modules.title": "只选择你需要的基础设施。",
      "modules.body": "模块卡片来自当前文档包结构：包名、注册入口、UI 模块与使用目的。",
      "architecture.kicker": "文档产品架构",
      "architecture.title": "从 Markdown 源到解耦文档前端。",
      "architecture.body": "这对应 `Monica.Docs` 的目标方向：领域优先后端、Published Language 契约，以及通过 HTTP API 消费数据的独立前端。",
      "footer.note": "Monica 仍处于内部开发阶段。这个原型是未来官网与文档前端的设计稿。",
      "footer.overview": "概览",
      "footer.docs": "文档阅读器",
      "footer.modules": "模块图谱",
      "footer.architecture": "架构",
      "filters.all": "全部",
      "filters.start": "快速开始",
      "filters.concepts": "核心概念",
      "filters.modules": "模块",
      "filters.scenarios": "场景",
      "search.dialogLabel": "搜索 Monica 文档",
      "search.placeholder": "搜索模块、概念、API、文档路径...",
      "search.help": "试试：`Res`、`Configuration`、`Mo.AddJobScheduler`、`Facade`",
      "search.closeHint": "Esc 关闭",
      "search.noResults": "暂无结果",
      "search.noResultsHint": "试试模块名、路由或契约关键词。",
      "search.docKind": "文档",
      "search.moduleKind": "模块"
    }
  };

  const docGroups = {
    zh: {
      Start: "快速开始",
      Concepts: "核心概念",
      Modules: "模块文档",
      Scenarios: "场景指南"
    }
  };

  const docTypes = {
    zh: {
      start: "快速",
      concept: "概念",
      module: "模块",
      scenario: "场景"
    }
  };

  const docZh = {
    "zh-cn/index": {
      title: "Monica 文档",
      description: "Monica 重构版文档入口，以当前 zh-CN 源目录为基础，并预留正式双语官网体验。",
      headings: ["推荐阅读顺序", "当前已整理模块", "宿主集成"],
      body: [
        "Monica 是一个模块化的 .NET 基础设施库。每个模块都遵循统一注册模型，并将公共契约与内部 Service、Provider 明确分离。",
        "当前文档源按语言、主题和模块组织。这个原型在保留结构可见性的同时，为它加上正式产品级文档外壳。",
        "读者可以从快速开始进入核心概念，再进入模块包与真实宿主场景。"
      ],
      asideTitle: "推荐路径",
      asideBody: "快速开始 -> 核心概念 -> 模块图谱 -> 场景指南"
    },
    "getting-started/installation": {
      title: "安装与主机接入",
      description: "只安装需要的 Monica 包，并通过统一的 `Mo.Add*()` 约定接入宿主。",
      headings: ["安装包", "注册模块", "运行宿主"],
      body: [
        "Monica 明确以包为边界。宿主可以只引入 Core、Repository、Configuration、JobScheduler、AI 或其他所需模块。",
        "模块注册应该像一段组合脚本。Option 承载宿主策略，Guide 方法选择 Provider 与可选行为。"
      ],
      asideTitle: "原型提示",
      asideBody: "安装命令以可复用卡片呈现，并带复制操作。"
    },
    "getting-started/first-module": {
      title: "注册第一个模块",
      description: "从空宿主到 Monica 运行时的最小路径。",
      headings: ["最小 Program", "Options", "Guide 链式配置"],
      body: [
        "第一个模块应该展示统一模式，而不是展示一个特例。Monica 的价值在于学会第一个模块后，其他模块都会变得熟悉。",
        "文档阅读器会把 Option、Guide、Module 与扩展方法结构放在代码示例旁边。"
      ],
      asideTitle: "核心认知",
      asideBody: "每个模块都在教授同一套注册语法。"
    },
    "concepts/module-pattern": {
      title: "Module 模式",
      description: "Monica 模块背后的稳定心智模型：Option、Guide、Module 与 BuilderExtensions。",
      headings: ["Option", "Guide", "Module", "Builder Extensions"],
      body: [
        "模块模式把公开配置、流式 Provider 选择、运行时注册和开发者入口扩展方法分离开。",
        "官网文档应该先讲清楚这套模式，再在每个模块详情页重复使用它。"
      ],
      asideTitle: "设计处理",
      asideBody: "首页模块主线让这套模式更容易被记住。"
    },
    "concepts/configuration-and-guide": {
      title: "Option 与 Guide",
      description: "Option 描述宿主策略；Guide 描述受支持的流式配置路径。",
      headings: ["Options", "Guide 方法", "Provider 选择"],
      body: [
        "Option 应说明用途、效果、重要默认值，以及开发者应该何时配置。",
        "Guide 方法应该像明确的能力选择，而不是隐藏副作用。"
      ],
      asideTitle: "阅读提示",
      asideBody: "文档页需要为默认值和副作用提供契约面板。"
    },
    "concepts/facades-services-providers": {
      title: "Facade、Service、Provider 边界",
      description: "公开 Facade 返回 `Res`；内部 Service 与 Provider 使用直接的 .NET 模式。",
      headings: ["Facade 契约", "内部 Service", "Provider 边界"],
      body: [
        "Facade 是面向 Minimal API 与 UI 消费者的基础设施模块公共入口。",
        "内部 Service 应保持直接返回并使用标准异常表达异常状态。其他基础设施模块应依赖 Abstractions，而不是 Facade。"
      ],
      asideTitle: "重要陷阱",
      asideBody: "`Res<string>` 必须显式使用泛型构造，避免丢失 Data。"
    },
    "concepts/result-envelope": {
      title: "统一结果模型 Res",
      description: "`Res` 与 `Res<T>` 为 Facade 和 ApplicationService 提供一致的公共结果形态。",
      headings: ["何时使用 Res", "失败流", "字符串重载"],
      body: [
        "官网文档应该明确边界：Facade 返回 `Res`，内部 Service 代码保持惯用 .NET 风格。",
        "这能让 UI、Minimal API 与基础设施代码保持简单，而不会把每个内部方法都变成结果信封管道。"
      ],
      asideTitle: "文档模式",
      asideBody: "对重载陷阱和边界错误使用醒目的视觉警告。"
    },
    "modules/configuration": {
      title: "Configuration",
      description: "自动发现 `[Configuration]` 类型，绑定到 `IOptions*`，并管理历史、更新、回滚与 Provider 诊断。",
      headings: ["何时使用", "包", "公开使用面", "UI 模块"],
      body: [
        "Configuration 把宿主配置变成可检查的元数据。文档应同时展示运行时接入与 Dashboard 管理流程。",
        "这是展示 Monica 基础设施模块如何暴露 UI 安全 Facade 的代表模块。"
      ],
      asideTitle: "相关 UI",
      asideBody: "`Mo.AddConfigurationUI()`"
    },
    "modules/job-scheduler": {
      title: "JobScheduler",
      description: "扫描定时作业与触发式作业，提供元数据存储、执行 Provider、监控与 Dashboard Facade。",
      headings: ["定时作业", "触发式作业", "元数据存储", "监控"],
      body: [
        "JobScheduler 是 Monica 运维优先理念最强的示例之一。",
        "未来文档应该把作业定义、Options、执行 Provider 与 Dashboard 视图连接到同一路由家族中。"
      ],
      asideTitle: "运行时界面",
      asideBody: "控制面、执行面、查询 Facade、分析 Facade。"
    },
    "modules/project-units": {
      title: "ProjectUnits",
      description: "发现 Monica 项目单元，并构建请求、应用服务、领域服务、事件、作业与配置的运行时地图。",
      headings: ["发现", "命名治理", "运行时可视化"],
      body: [
        "ProjectUnits 为 Monica 业务项目提供统一的代码结构语言。",
        "它应该成为官网的标志性图示之一，因为它连接了框架概念与真实业务解决方案架构。"
      ],
      asideTitle: "文档角度",
      asideBody: "把 ProjectUnits 展示为框架文档与业务架构文档之间的桥。"
    },
    "modules/repository": {
      title: "Repository",
      description: "EF Core 仓储抽象、DbContext Provider 注册、实体仓储发现与 GUID 生成。",
      headings: ["仓储契约", "DbContext Provider", "UnitOfWork 组合"],
      body: [
        "Repository 文档应强调持久化所有权、DbContext Provider 选择与 UnitOfWork 组合。",
        "模块图谱让用户在进入完整页面前快速扫描包名和注册入口。"
      ],
      asideTitle: "常见组合",
      asideBody: "与 UnitOfWork 组合出一致事务边界。"
    },
    "scenarios/minimal-api-host": {
      title: "构建最小 Monica API 主机",
      description: "以宿主为中心，展示多个 Monica 模块如何组合成可运行的 ASP.NET Core 服务。",
      headings: ["宿主结构", "模块组合", "OpenAPI", "运行"],
      body: [
        "场景页不应重复模块参考内容，而应展示模块如何在真实宿主中协作。",
        "文档产品可以把它呈现为配方，并链接相关模块卡片与 API 契约预览。"
      ],
      asideTitle: "阅读承诺",
      asideBody: "从模块知识走向可部署的宿主组合。"
    },
    "scenarios/diagnostics-and-ops": {
      title: "组合调试与管理能力",
      description: "组合 Configuration、JobScheduler、DataChannel、ProjectUnits 与运行时指标的 Dashboard 和 Facade。",
      headings: ["Configuration UI", "Scheduler UI", "运行时指标", "运维边界"],
      body: [
        "Monica 内置管理界面。官网应该把运维作为一条一等学习路径。",
        "这个场景适合承载可视化示例、Dashboard 截图与模块依赖提示。"
      ],
      asideTitle: "视觉提示",
      asideBody: "运维文档使用控制室卡片和遥测强调色。"
    }
  };

  const moduleZh = {
    AutoModel: { category: "模型", ui: "无 UI 模块", description: "动态过滤、字段快照，以及面向自动 CRUD 和通用检索的内存/数据库查询能力。" },
    AutoControllers: { category: "Web API", ui: "无 UI 模块", description: "自动发现 ControllerBase 与 CRUD 应用服务，并接入 ASP.NET Core 路由。" },
    Configuration: { category: "运行时", ui: "Mo.AddConfigurationUI()", description: "强类型配置发现、绑定、热重载、历史、回滚与 Provider 诊断。" },
    DataChannel: { category: "通信", ui: "Mo.AddDataChannelUI()", description: "基于 Pipeline 的外部通信通道，包含状态、异常摘要与重初始化控制。" },
    DependencyInjection: { category: "核心", ui: "Mo.AddDependencyInjectionUI()", description: "约定式服务注册、生命周期标记、暴露规则、keyed services 与诊断快照。" },
    EventBus: { category: "通信", ui: "Mo.AddEventBusUI()", description: "统一的本地/分布式事件总线抽象，支持处理器自动发现与 keyed bus 组合。" },
    JobScheduler: { category: "运维", ui: "Mo.AddJobSchedulerUI()", description: "定时/触发作业扫描、元数据存储、执行 Provider、监控与 Dashboard Facade。" },
    ProjectUnits: { category: "架构", ui: "Mo.AddProjectUnitsUI()", description: "发现请求、应用服务、领域服务、实体、事件、作业与配置的运行时结构。" },
    Repository: { category: "数据", ui: "无 UI 模块", description: "EF Core 仓储抽象、DbContext Provider 注册、实体仓储发现与 GUID 生成。" },
    SignalR: { category: "通信", ui: "Mo.AddSignalRUI()", description: "强类型 SignalR Hub 注册、连接跟踪、Hub 元数据检查与可选调试界面。" },
    UnitOfWork: { category: "数据", ui: "无 UI 模块", description: "为仓储与 DbContext 提供统一事务边界、SaveChanges 聚合与嵌套作用域。" }
  };

  const categoryZh = {
    All: "全部",
    Modeling: "模型",
    "Web API": "Web API",
    Runtime: "运行时",
    Communication: "通信",
    Core: "核心",
    Operations: "运维",
    Architecture: "架构",
    Data: "数据"
  };

  const pipelineZh = [
    {
      title: "Markdown 源",
      summary: "内容保持作者友好，并原生存在仓库中。",
      detail: "当前 v1 的事实来源是已有 Markdown 树。前端不应直接解析文件，而应消费规范化后的 HTTP 契约。"
    },
    {
      title: "Documentation 领域",
      summary: "业务规则拥有 slug、标题、元数据与附件引用。",
      detail: "Documentation 有界上下文拥有解析、规范化、导航排序、可见性规则与文件系统 Provider 细节。"
    },
    {
      title: "Published Language",
      summary: "稳定 DTO 跨越后端/前端边界。",
      detail: "DocTreeItemDto、DocContentDto、DocHeadingDto 与请求契约属于 Platform.Protocol，因此前端只消费稳定语言。"
    },
    {
      title: "Docs API",
      summary: "一组小型只读 API 支撑第一版前端。",
      detail: "API 暴露 tree、doc 与 assets 端点。AppHost 保持组合职责，领域包拥有行为。"
    },
    {
      title: "前端外壳",
      summary: "解耦 Web 应用呈现搜索、文档路由与模块探索。",
      detail: "前端只拥有呈现。它调用后端 API，渲染 Markdown 内容，支持移动端和桌面端文档导航，并为后续 SEO/搜索优化留空间。"
    }
  ];

  const state = {
    activeDocSlug: "zh-cn/index",
    docFilter: "all",
    moduleFilter: "All",
    pipelineIndex: 0,
    language: "en"
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
    applyInitialLanguage();
    applyInitialMode();
    applyStaticText();
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

  function applyInitialLanguage() {
    const params = new URLSearchParams(window.location.search);
    const requestedLanguage = params.get("lang") || window.localStorage.getItem("monica-docs-language");
    state.language = requestedLanguage === "zh" ? "zh" : "en";
    updateLanguageUi();
  }

  function applyInitialMode() {
    const params = new URLSearchParams(window.location.search);
    if (params.get("mode") !== "signal") {
      return;
    }

    document.body.classList.add("signal-mode");
    document.querySelector("#tone-toggle")?.setAttribute("aria-pressed", "true");
  }

  function switchLanguage(language) {
    if (!["en", "zh"].includes(language) || state.language === language) {
      return;
    }

    state.language = language;
    window.localStorage.setItem("monica-docs-language", language);
    updateLanguageUi();
    applyStaticText();
    renderDocsTree();
    renderActiveDoc();
    renderModuleFilters();
    renderModules();
    renderPipeline();

    if (searchOverlay.classList.contains("open")) {
      renderSearch(searchInput.value);
    }
  }

  function updateLanguageUi() {
    document.documentElement.lang = state.language === "zh" ? "zh-CN" : "en";
    document.querySelectorAll("[data-language]").forEach(button => {
      button.classList.toggle("active", button.dataset.language === state.language);
      button.setAttribute("aria-pressed", String(button.dataset.language === state.language));
    });
  }

  function applyStaticText() {
    document.querySelectorAll("[data-i18n]").forEach(element => {
      element.textContent = text(element.dataset.i18n);
    });
    document.querySelectorAll("[data-i18n-placeholder]").forEach(element => {
      element.setAttribute("placeholder", text(element.dataset.i18nPlaceholder));
    });
    document.querySelectorAll("[data-i18n-aria-label]").forEach(element => {
      element.setAttribute("aria-label", text(element.dataset.i18nAriaLabel));
    });
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
    document.querySelectorAll("[data-language]").forEach(button => {
      button.addEventListener("click", () => switchLanguage(button.dataset.language));
    });

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
        showToast(text("actions.copied"));
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
          <span>${escapeHtml(localizeDoc(item).title)}</span>
          <span class="tag">${escapeHtml(localizeDocType(item.type))}</span>
          <span class="path">${escapeHtml(item.path)}</span>
        </button>
      `).join("");

      return `
        <section class="doc-group">
          <h3 class="doc-group-title">${escapeHtml(localizeDocGroup(group.group))}</h3>
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
    const content = localizeDoc(doc);

    docArticle.innerHTML = `
      <div class="article-topline">
        <span class="article-pill">${escapeHtml(localizeDocGroup(doc.group))}</span>
        <span class="article-pill">${escapeHtml(doc.path)}</span>
        <span class="article-pill">${escapeHtml(state.language === "zh" ? doc.title : doc.cnTitle)}</span>
      </div>
      <h3>${escapeHtml(content.title)}</h3>
      <p class="doc-description">${formatInlineCode(escapeHtml(content.description))}</p>
      <div class="doc-body-grid">
        <div class="doc-body">
          ${content.body.map(paragraph => `<p>${formatInlineCode(escapeHtml(paragraph))}</p>`).join("")}
          <div class="code-window">
            <div class="code-window-header"><span></span><span></span><span></span></div>
            <pre><code>${escapeHtml(doc.code)}</code></pre>
          </div>
        </div>
        <aside class="doc-aside-card">
          <span>${escapeHtml(text("docs.designAnnotation"))}</span>
          <strong>${escapeHtml(content.asideTitle)}</strong>
          <p>${formatInlineCode(escapeHtml(content.asideBody))}</p>
        </aside>
      </div>
    `;

    tocList.innerHTML = content.headings.map(heading => `<a class="toc-link" href="#docs">${escapeHtml(heading)}</a>`).join("");
    initializeIcons();
  }

  function renderModuleFilters() {
    const categories = ["All", ...new Set(data.modules.map(module => module.category))];
    moduleFilters.innerHTML = categories.map(category => `
      <button class="module-filter ${category === state.moduleFilter ? "active" : ""}" type="button" data-module-filter="${escapeHtml(category)}">
        ${escapeHtml(localizeCategory(category))}
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
          <span class="module-category">${escapeHtml(localizeModule(module).category)}</span>
          <span class="module-ui">${escapeHtml(localizeModule(module).ui)}</span>
        </div>
        <h3>${escapeHtml(module.title)}</h3>
        <p>${escapeHtml(localizeModule(module).description)}</p>
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
          <strong>${escapeHtml(localizePipeline(stage, index).title)}</strong>
          ${escapeHtml(localizePipeline(stage, index).summary)}
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
    const content = localizePipeline(stage, state.pipelineIndex);
    pipelineDetail.innerHTML = `
      <span class="badge">${escapeHtml(stage.badge)}</span>
      <h3>${escapeHtml(content.title)}</h3>
      <p>${escapeHtml(content.detail)}</p>
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
      .filter(doc => {
        const content = localizeDoc(doc);
        return matches(normalized, [doc.title, doc.cnTitle, content.title, doc.path, doc.description, content.description, doc.code, doc.type]);
      })
      .map(doc => ({
        kind: text("search.docKind"),
        title: localizeDoc(doc).title,
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
      .filter(module => {
        const content = localizeModule(module);
        return matches(normalized, [module.title, module.category, content.category, module.packageName, module.registration, module.description, content.description, module.ui, content.ui]);
      })
      .map(module => ({
        kind: text("search.moduleKind"),
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
      : `<div class="search-result"><span><strong>${escapeHtml(text("search.noResults"))}</strong><span>${escapeHtml(text("search.noResultsHint"))}</span></span></div>`;

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

  function text(key) {
    return uiText[state.language]?.[key] ?? uiText.en[key] ?? key;
  }

  function localizeDoc(doc) {
    if (state.language !== "zh") {
      return doc;
    }

    return { ...doc, ...docZh[doc.slug] };
  }

  function localizeDocGroup(group) {
    return state.language === "zh" ? docGroups.zh[group] ?? group : group;
  }

  function localizeDocType(type) {
    return state.language === "zh" ? docTypes.zh[type] ?? type : type;
  }

  function localizeCategory(category) {
    return state.language === "zh" ? categoryZh[category] ?? category : category;
  }

  function localizeModule(module) {
    if (state.language !== "zh") {
      return module;
    }

    return { ...module, ...moduleZh[module.title] };
  }

  function localizePipeline(stage, index) {
    if (state.language !== "zh") {
      return stage;
    }

    return { ...stage, ...pipelineZh[index] };
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
