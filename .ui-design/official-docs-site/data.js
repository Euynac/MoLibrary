window.MonicaDocsPrototype = {
  docs: [
    {
      group: "Start",
      items: [
        {
          slug: "zh-cn/index",
          type: "start",
          title: "Monica Documentation",
          cnTitle: "Monica 文档",
          path: "docs/zh-CN/index.md",
          description: "The rebuilt documentation entry for Monica's zh-CN source tree, framed for an official bilingual docs experience.",
          headings: ["Recommended Reading", "Current Modules", "Host Integration"],
          body: [
            "Monica is a modular .NET infrastructure library. Each module follows the same registration model and keeps public contracts separate from internal services and providers.",
            "The active documentation source is organized by language, topic, and module. This prototype keeps that structure visible while giving it a polished product shell.",
            "Readers can start with Quick Start, build mental models through Concepts, then dive into module packages and real host scenarios."
          ],
          code: "Mo.AddConfiguration()\n  .UseLocalJsonFileProvider();\n\nMo.AddJobScheduler()\n  .UseEfCoreMetadataRepository();",
          asideTitle: "Best first path",
          asideBody: "Quick Start -> Core Concepts -> Module Atlas -> Scenarios"
        },
        {
          slug: "getting-started/installation",
          type: "start",
          title: "Installation and Host Access",
          cnTitle: "安装与主机接入",
          path: "docs/zh-CN/getting-started/installation.md",
          description: "Install only the Monica packages you need and wire them into a host through the unified `Mo.Add*()` convention.",
          headings: ["Install Packages", "Register Modules", "Run the Host"],
          body: [
            "Monica is intentionally package-oriented. A host can pull in only Core, Repository, Configuration, JobScheduler, AI, or other needed modules.",
            "Module registration should read as a composition script. Options capture host policy; Guide methods select providers and optional behavior."
          ],
          code: "dotnet add package Monica.Core\ndotnet add package Monica.Configuration\ndotnet add package Monica.JobScheduler",
          asideTitle: "Prototype cue",
          asideBody: "Installation commands are treated as reusable cards with copy affordances."
        },
        {
          slug: "getting-started/first-module",
          type: "start",
          title: "Register the First Module",
          cnTitle: "注册第一个模块",
          path: "docs/zh-CN/getting-started/first-module.md",
          description: "A minimal path from an empty host to a Monica-enabled runtime.",
          headings: ["Minimal Program", "Options", "Guide Chaining"],
          body: [
            "The first module should demonstrate the pattern rather than a special case. Monica's value comes from each module feeling familiar after the first one.",
            "The docs reader highlights Option, Guide, Module, and extension method shape beside code examples."
          ],
          code: "var builder = WebApplication.CreateBuilder(args);\n\nMo.AddConfiguration(options =>\n{\n    options.EnableHotReload = true;\n});\n\nvar app = builder.Build();\napp.Run();",
          asideTitle: "Core lesson",
          asideBody: "Every module teaches the same registration grammar."
        }
      ]
    },
    {
      group: "Concepts",
      items: [
        {
          slug: "concepts/module-pattern",
          type: "concept",
          title: "Module Pattern",
          cnTitle: "Module 模式",
          path: "docs/zh-CN/concepts/module-pattern.md",
          description: "The stable mental model behind Monica modules: Option, Guide, Module, and BuilderExtensions.",
          headings: ["Option", "Guide", "Module", "Builder Extensions"],
          body: [
            "The module pattern keeps public configuration, fluent provider selection, runtime registration, and developer-facing extension methods separate.",
            "The official site should teach the pattern once, then reuse it across every module detail page."
          ],
          code: "Module{Name}Option\nModule{Name}Guide\nModule{Name}\nModule{Name}BuilderExtensions",
          asideTitle: "Design treatment",
          asideBody: "The hero module spine makes this pattern visually memorable."
        },
        {
          slug: "concepts/configuration-and-guide",
          type: "concept",
          title: "Option and Guide",
          cnTitle: "Option 与 Guide",
          path: "docs/zh-CN/concepts/configuration-and-guide.md",
          description: "Options describe host policy; Guides describe supported fluent configuration paths.",
          headings: ["Options", "Guide Methods", "Provider Selection"],
          body: [
            "Options should explain purpose, effects, important defaults, and when developers should configure them.",
            "Guide methods should read as explicit capability choices rather than hidden side effects."
          ],
          code: "Mo.AddRepository(options =>\n{\n    options.DbContextProviderType = DbContextProviderType.UnitOfWork;\n})\n.AddDbContextProvider<AppDbContext>();",
          asideTitle: "Reader cue",
          asideBody: "Docs pages need contract panels for defaults and side effects."
        },
        {
          slug: "concepts/facades-services-providers",
          type: "concept",
          title: "Facade, Service, Provider Boundaries",
          cnTitle: "Facade、Service、Provider 边界",
          path: "docs/zh-CN/concepts/facades-services-providers.md",
          description: "Public facades return `Res`; internal services and providers use direct .NET patterns.",
          headings: ["Facade Contracts", "Internal Services", "Provider Boundaries"],
          body: [
            "Facades are public infrastructure module entry points for Minimal API and UI consumers.",
            "Internal services should stay direct and throw standard exceptions for exceptional states. Other infrastructure modules depend on abstractions, not facades."
          ],
          code: "public async Task<Res<string>> LoadAsync()\n{\n    var content = await service.LoadAsync();\n    return Res.Ok<string>(content);\n}",
          asideTitle: "Important trap",
          asideBody: "`Res<string>` must use explicit generic construction to preserve Data."
        },
        {
          slug: "concepts/result-envelope",
          type: "concept",
          title: "Unified Result Envelope",
          cnTitle: "统一结果模型 Res",
          path: "docs/zh-CN/concepts/result-envelope.md",
          description: "`Res` and `Res<T>` create a consistent public result shape for facades and application services.",
          headings: ["When to Use Res", "Failure Flow", "String Overloads"],
          body: [
            "The official docs should make the boundary explicit: Facades return `Res`, while internal service code remains idiomatic .NET.",
            "This distinction helps UI, Minimal API, and infrastructure code stay simple without turning every internal method into envelope plumbing."
          ],
          code: "if (result.IsFailed)\n{\n    return result;\n}\n\nreturn Res.Ok<MyDto>(dto);",
          asideTitle: "Docs pattern",
          asideBody: "Use visual warnings for overload traps and boundary mistakes."
        }
      ]
    },
    {
      group: "Modules",
      items: [
        {
          slug: "modules/configuration",
          type: "module",
          title: "Configuration",
          cnTitle: "Configuration",
          path: "docs/zh-CN/modules/configuration/index.md",
          description: "Automatically discover `[Configuration]` types, bind to `IOptions*`, manage history, updates, rollback, and provider diagnostics.",
          headings: ["When to Use", "Package", "Public Surface", "UI Module"],
          body: [
            "Configuration turns host configuration into inspectable metadata. The docs should show both runtime integration and dashboard-oriented workflows.",
            "This is a flagship module for demonstrating how Monica infrastructure modules expose UI-safe facades."
          ],
          code: "Mo.AddConfiguration(options =>\n{\n    options.EnableHotReload = true;\n    options.ValidateOnStartup = true;\n});",
          asideTitle: "Related UI",
          asideBody: "`Mo.AddConfigurationUI()`"
        },
        {
          slug: "modules/job-scheduler",
          type: "module",
          title: "JobScheduler",
          cnTitle: "JobScheduler",
          path: "docs/zh-CN/modules/job-scheduler/index.md",
          description: "Scan recurring and triggered jobs, provide metadata storage, execution providers, monitoring, and dashboard facades.",
          headings: ["Recurring Jobs", "Triggered Jobs", "Metadata Store", "Monitoring"],
          body: [
            "JobScheduler is one of the strongest examples of Monica's operation-first philosophy.",
            "The future docs should connect job definitions, options, execution providers, and dashboard views in one route family."
          ],
          code: "Mo.AddJobScheduler(options =>\n{\n    options.RecurringJobDebugMode = true;\n})\n.UseEfCoreMetadataRepository();",
          asideTitle: "Runtime surface",
          asideBody: "Control plane, execution plane, query facade, analytics facade."
        },
        {
          slug: "modules/project-units",
          type: "module",
          title: "ProjectUnits",
          cnTitle: "ProjectUnits",
          path: "docs/zh-CN/modules/project-units/index.md",
          description: "Discover Monica project units and build a runtime map of requests, application services, domain services, events, jobs, and configuration.",
          headings: ["Discovery", "Naming Governance", "Runtime Visualization"],
          body: [
            "ProjectUnits gives Monica business projects a shared language for code structure.",
            "This should become one of the official site's signature diagrams because it bridges framework concepts and real business solution architecture."
          ],
          code: "Mo.AddProjectUnits(options =>\n{\n    options.Naming.Mode = ENameConventionMode.Warning;\n});",
          asideTitle: "Docs angle",
          asideBody: "Show ProjectUnits as the bridge between framework docs and business architecture docs."
        },
        {
          slug: "modules/repository",
          type: "module",
          title: "Repository",
          cnTitle: "Repository",
          path: "docs/zh-CN/modules/repository/index.md",
          description: "EF Core repository abstractions, DbContext Provider registration, entity repository discovery, and GUID generation.",
          headings: ["Repository Contracts", "DbContext Providers", "UnitOfWork Composition"],
          body: [
            "Repository documentation should emphasize persistence ownership, DbContext provider selection, and UnitOfWork composition.",
            "The module atlas makes package and registration details scannable before users enter the full page."
          ],
          code: "Mo.AddRepository()\n  .AddDbContextProvider<AppDbContext>();",
          asideTitle: "Pairs with",
          asideBody: "UnitOfWork for consistent transaction boundaries."
        }
      ]
    },
    {
      group: "Scenarios",
      items: [
        {
          slug: "scenarios/minimal-api-host",
          type: "scenario",
          title: "Build a Minimal Monica API Host",
          cnTitle: "构建最小 Monica API 主机",
          path: "docs/zh-CN/scenarios/minimal-api-host.md",
          description: "A host-focused route showing how multiple Monica modules compose into a working ASP.NET Core service.",
          headings: ["Host Shape", "Module Composition", "OpenAPI", "Run"],
          body: [
            "Scenario pages should avoid repeating module references. They should show how modules collaborate in a real host.",
            "The docs product can surface this as a recipe with linked module cards and API contract previews."
          ],
          code: "Mo.AddAutoControllers();\nMo.AddConfiguration();\nMo.AddProjectUnits();\nMo.AddSwagger();",
          asideTitle: "Reader promise",
          asideBody: "Move from module knowledge to deployable host composition."
        },
        {
          slug: "scenarios/diagnostics-and-ops",
          type: "scenario",
          title: "Compose Diagnostics and Operations",
          cnTitle: "组合调试与管理能力",
          path: "docs/zh-CN/scenarios/diagnostics-and-ops.md",
          description: "Combine dashboards and facades for configuration, jobs, channels, project units, and runtime metrics.",
          headings: ["Configuration UI", "Scheduler UI", "Runtime Metrics", "Operational Boundaries"],
          body: [
            "Monica includes built-in management surfaces. The official site should treat operations as a first-class learning path.",
            "This scenario is ideal for visual examples, dashboard screenshots, and module dependency callouts."
          ],
          code: "Mo.AddConfigurationUI();\nMo.AddJobSchedulerUI();\nMo.AddProjectUnitsUI();\nMo.AddProfiling();",
          asideTitle: "Visual cue",
          asideBody: "Use control-room cards and telemetry accents for operations docs."
        }
      ]
    }
  ],
  modules: [
    {
      title: "AutoModel",
      category: "Modeling",
      packageName: "Monica.AutoModel",
      registration: "Mo.AddAutoModel()",
      ui: "No UI module",
      description: "Dynamic filtering, field snapshots, and memory/database query support for automatic CRUD and general search.",
      glow: "rgba(0, 167, 181, 0.16)"
    },
    {
      title: "AutoControllers",
      category: "Web API",
      packageName: "Monica.WebApi",
      registration: "Mo.AddAutoControllers(...)",
      ui: "No UI module",
      description: "Automatic discovery for ControllerBase and CRUD application services with ASP.NET Core route integration.",
      glow: "rgba(213, 139, 45, 0.18)"
    },
    {
      title: "Configuration",
      category: "Runtime",
      packageName: "Monica.Configuration",
      registration: "Mo.AddConfiguration()",
      ui: "Mo.AddConfigurationUI()",
      description: "Strongly typed configuration discovery, binding, hot reload, history, rollback, and provider diagnostics.",
      glow: "rgba(76, 43, 214, 0.18)"
    },
    {
      title: "DataChannel",
      category: "Communication",
      packageName: "Monica.DataChannel",
      registration: "Mo.AddDataChannel()",
      ui: "Mo.AddDataChannelUI()",
      description: "Pipeline-based external communication channels with status, exception summaries, and reinitialization controls.",
      glow: "rgba(0, 167, 181, 0.18)"
    },
    {
      title: "DependencyInjection",
      category: "Core",
      packageName: "Monica.DependencyInjection",
      registration: "Mo.AddDependencyInjection()",
      ui: "Mo.AddDependencyInjectionUI()",
      description: "Convention-based service registration, lifecycle markers, exposure rules, keyed services, and diagnostics snapshots.",
      glow: "rgba(155, 79, 47, 0.16)"
    },
    {
      title: "EventBus",
      category: "Communication",
      packageName: "Monica.EventBus",
      registration: "Mo.AddEventBus()",
      ui: "Mo.AddEventBusUI()",
      description: "Unified local and distributed event bus abstractions with automatic handler discovery and keyed bus composition.",
      glow: "rgba(0, 167, 181, 0.2)"
    },
    {
      title: "JobScheduler",
      category: "Operations",
      packageName: "Monica.JobScheduler",
      registration: "Mo.AddJobScheduler()",
      ui: "Mo.AddJobSchedulerUI()",
      description: "Recurring and triggered job scanning, metadata storage, execution providers, monitoring, and dashboard facades.",
      glow: "rgba(213, 139, 45, 0.22)"
    },
    {
      title: "ProjectUnits",
      category: "Architecture",
      packageName: "Monica.Framework",
      registration: "Mo.AddProjectUnits()",
      ui: "Mo.AddProjectUnitsUI()",
      description: "Runtime structure discovery for requests, application services, domain services, entities, events, jobs, and configuration.",
      glow: "rgba(76, 43, 214, 0.22)"
    },
    {
      title: "Repository",
      category: "Data",
      packageName: "Monica.Repository",
      registration: "Mo.AddRepository()",
      ui: "No UI module",
      description: "EF Core repository abstractions, DbContext provider registration, entity repository discovery, and GUID generation.",
      glow: "rgba(155, 79, 47, 0.18)"
    },
    {
      title: "SignalR",
      category: "Communication",
      packageName: "Monica.SignalR",
      registration: "Mo.AddSignalR()",
      ui: "Mo.AddSignalRUI()",
      description: "Strongly typed SignalR Hub registration, connection tracking, Hub metadata inspection, and optional debug UI.",
      glow: "rgba(0, 167, 181, 0.18)"
    },
    {
      title: "UnitOfWork",
      category: "Data",
      packageName: "Monica.Repository",
      registration: "Mo.AddUnitOfWork()",
      ui: "No UI module",
      description: "Unified transaction boundaries for repositories and DbContext usage with SaveChanges aggregation and nested scopes.",
      glow: "rgba(213, 139, 45, 0.16)"
    }
  ],
  pipeline: [
    {
      title: "Markdown Source",
      badge: "docs/**/*.md",
      summary: "Content remains author-friendly and repository-native.",
      detail: "The current v1 source of truth is the existing markdown tree. The frontend should not parse files directly; it should consume normalized HTTP contracts.",
      contracts: ["docs/zh-CN/index.md", "docs/zh-CN/modules/*/index.md", "docs/attachments/**"]
    },
    {
      title: "Documentation Domain",
      badge: "Domains/Documentation",
      summary: "Business rules own slugs, headings, metadata, and attachment references.",
      detail: "The Documentation bounded context owns parsing, normalization, navigation ordering, visibility rules, and file-system provider details.",
      contracts: ["DocumentationSourceDocument", "DocumentationTreeNode", "DomainDocumentationMarkdownProcessor"]
    },
    {
      title: "Published Language",
      badge: "Platform.Protocol",
      summary: "Stable DTOs cross the backend/frontend boundary.",
      detail: "Contracts such as DocTreeItemDto, DocContentDto, DocHeadingDto, and requests belong in Platform.Protocol so the frontend consumes stable language only.",
      contracts: ["GetDocTreeRequest", "GetDocBySlugRequest", "DocTreeItemDto", "DocContentDto"]
    },
    {
      title: "Docs API",
      badge: "/api/v1/Documentation",
      summary: "A small read-only API powers the first frontend version.",
      detail: "The API exposes tree, document, and asset endpoints. It should stay composition-focused in AppHost while the domain package owns behavior.",
      contracts: ["GET /tree", "GET /doc?slug={slug}", "GET /assets?assetPath={path}"]
    },
    {
      title: "Frontend Shell",
      badge: "monica-docs-web",
      summary: "A decoupled web app presents search, docs routes, and module exploration.",
      detail: "The frontend owns presentation only. It calls backend APIs, renders markdown content, supports mobile and desktop docs navigation, and later gains SEO/search optimization.",
      contracts: ["Docs layout", "Module atlas", "Command search", "Content routes"]
    }
  ]
};
