window.WORKBENCH_DATA = {
  snapshot: {
    schemaVersion: 3,
    compositionId: "f2c6b8d4-5413-4e1d-a78b-7093fc2022aa",
    revision: 17,
    capturedAt: "2026-08-06T07:42:18.643+08:00",
    host: "FlightService.API",
    environment: "Development",
    framework: ".NET 10.0 · Monica dev",
    outcome: "Succeeded",
    isFinal: true,
    applicationStartupMs: 5128.0308,
    totalMs: 3468,
    registrationMs: 2577,
    moduleCount: 93,
    findingCount: 2,
    criticalPathMs: 3031,
    discoveryMs: 597,
    scannedAssemblies: 47,
    discoveredTypes: 6842,
    distinctQueries: 14,
    totalMatches: 1137
  },

  findings: [
    {
      code: "PERF-COMPOSITION-BUDGET",
      severity: "warning",
      title: "Composition exceeded the local development budget",
      detail: "3,468 ms actual / 3,000 ms budget · 115.6% utilization",
      evidence: "ApplicationConfiguration contributes 1,605 ms to the critical path.",
      moduleId: "application-configuration"
    },
    {
      code: "PERF-CALLBACK-HOTSPOT",
      severity: "info",
      title: "One callback dominates serial module work",
      detail: "ModuleConfiguration.ConfigureApplicationBuilder · 582 ms",
      evidence: "This callback represents 37.1% of the active parallel-work span.",
      moduleId: "module-configuration"
    }
  ],

  criticalPath: [
    { id: "host-bootstrap", label: "Host", detail: "Host bootstrap", ms: 13, start: 0, tone: "neutral" },
    { id: "application-configuration", moduleId: "application-configuration", label: "App config", detail: "Application module configuration", ms: 1605, start: 13, tone: "amber" },
    { id: "type-discovery", label: "Discovery", detail: "Plan, scan, compile and commit", ms: 597, start: 1618, tone: "cyan", section: "discovery" },
    { id: "serial-callbacks", label: "Callbacks", detail: "652 serial registration callbacks", ms: 449, start: 2215, tone: "blue", section: "performance" },
    { id: "pipeline-before-routing", moduleId: "module-configuration", label: "Pipeline", detail: "Application pipeline before routing", ms: 619, start: 2664, tone: "amber" },
    { id: "endpoints", moduleId: "module-mcp", label: "Endpoints", detail: "Endpoint mapping and finalization", ms: 185, start: 3283, tone: "neutral" }
  ],

  stages: [
    { id: "plan", name: "Plan declaration", code: "TypeDiscoveryPlanDeclaration", ms: 9, countLabel: "93 modules · 14 plans", tone: "neutral" },
    { id: "resolution", name: "Assembly resolution", code: "TypeDiscoveryAssemblyResolution", ms: 38, countLabel: "47 scanned · 2 excluded", tone: "blue" },
    { id: "enumeration", name: "Type enumeration", code: "TypeDiscoveryTypeEnumeration", ms: 276, countLabel: "6,842 types · 3 partial loads", tone: "cyan" },
    { id: "evaluation", name: "Query evaluation", code: "TypeDiscoveryQueryEvaluation", ms: 171, countLabel: "14 distinct · 1,137 matches", tone: "purple" },
    { id: "commit", name: "Registration commit", code: "TypeDiscoveryRegistrationCommit", ms: 103, countLabel: "1,137 matches · 128 writes", tone: "green" }
  ],

  contributors: [
    { id: "c1", moduleId: "application-configuration", phase: "ApplicationConfiguration", label: "Application module configuration", ms: 1605, offset: 13, kind: "lifecycle", blocking: true },
    { id: "c2", moduleId: "module-configuration", phase: "ConfigureApplicationBuilder", label: "ModuleConfiguration", ms: 582, offset: 2671, kind: "lifecycle", blocking: true },
    { id: "c3", moduleId: "module-object-mapping", phase: "StartupWork", label: "ObjectMapping · Mapster compile", ms: 1375, offset: 18, kind: "startup-work", blocking: false },
    { id: "c4", moduleId: "module-mcp", phase: "MapEndpoints", label: "ModuleMcp", ms: 106, offset: 3292, kind: "lifecycle", blocking: true },
    { id: "c5", moduleId: "module-shell-ui", phase: "ConfigureApplicationBuilder", label: "ModuleShellUI", ms: 72, offset: 2940, kind: "lifecycle", blocking: true },
    { id: "c6", moduleId: "module-project-units", phase: "TypeDiscoveryRegistrationCommit", label: "ModuleProjectUnits", ms: 63, offset: 2119, kind: "discovery-commit", blocking: true },
    { id: "c7", moduleId: "module-object-mapping", phase: "PostConfigureServices", label: "ModuleObjectMapping", ms: 91, offset: 2461, kind: "lifecycle", blocking: true },
    { id: "c8", moduleId: "module-dependency-injection", phase: "TypeDiscoveryRegistrationCommit", label: "ModuleDependencyInjection", ms: 22, offset: 2184, kind: "discovery-commit", blocking: true },
    { id: "c9", moduleId: "module-configuration", phase: "PostConfigureServices", label: "ModuleConfiguration", ms: 18, offset: 2522, kind: "lifecycle", blocking: true },
    { id: "c10", moduleId: "module-telemetry", phase: "ConfigureServices", label: "ModuleTelemetry", ms: 13, offset: 2330, kind: "registration", blocking: true },
    { id: "c11", moduleId: "module-configuration", phase: "ConfigureBuilder", label: "ModuleConfiguration", ms: 8, offset: 2224, kind: "registration", blocking: true },
    { id: "c12", moduleId: "module-http-client", phase: "ConfigureServices", label: "ModuleHttpClient", ms: 3, offset: 2324, kind: "registration", blocking: true },
    { id: "c13", moduleId: "module-guid", phase: "ConfigureServices", label: "ModuleGuid", ms: 0, offset: 2327, kind: "registration", blocking: true }
  ],

  modules: [
    {
      id: "application-configuration", key: "FIPS2022:ApplicationConfiguration", name: "ApplicationConfiguration", assembly: "FlightService.API", capability: "Application", state: "Active", duration: 1605, dependencies: ["module-configuration", "module-logging"], dependents: [], callbackCount: 12, discoveryMatches: 0,
      summary: "Composes application-owned service and pipeline configuration for FlightService.",
      options: [{ name: "Environment", kind: "value", value: "Development" }, { name: "RemoteConfiguration", kind: "presence", value: "Configured" }]
    },
    {
      id: "module-configuration", key: "Monica:Configuration", name: "ModuleConfiguration", assembly: "Monica.Configuration", capability: "Configuration", state: "Active", duration: 606, dependencies: ["module-core"], dependents: ["application-configuration", "module-shell-ui", "module-openapi"], callbackCount: 18, discoveryMatches: 42,
      summary: "Builds configuration definitions and contributes the application configuration pipeline.",
      options: [{ name: "DefinitionCount", kind: "count", value: "42" }, { name: "ReloadOnChange", kind: "value", value: "True" }]
    },
    {
      id: "module-object-mapping", key: "Monica:ObjectMapping", name: "ModuleObjectMapping", assembly: "Monica.ObjectMapping", capability: "Mapping", state: "Active", duration: 93, startupWork: 1375, dependencies: ["module-core"], dependents: ["module-project-units"], callbackCount: 9, discoveryMatches: 119,
      summary: "Discovers mapping contracts and compiles the Mapster configuration as non-blocking startup work.",
      options: [{ name: "CompileMappings", kind: "value", value: "True" }, { name: "MappingCount", kind: "count", value: "119" }]
    },
    {
      id: "module-mcp", key: "Monica:Mcp", name: "ModuleMcp", assembly: "Monica.AI", capability: "AI", state: "Active", duration: 110, dependencies: ["module-ai", "module-project-units"], dependents: ["flight-mcp"], callbackCount: 7, discoveryMatches: 8,
      summary: "Registers MCP transports and maps tool endpoints after routing.",
      options: [{ name: "EnabledTransports", kind: "count", value: "2" }, { name: "ApiKey", kind: "presence", value: "Configured" }]
    },
    {
      id: "module-shell-ui", key: "Monica:ShellUI", name: "ModuleShellUI", assembly: "Monica.UI", capability: "UI", state: "Active", duration: 104, dependencies: ["module-configuration", "module-localization"], dependents: ["flight-ui"], callbackCount: 11, discoveryMatches: 22,
      summary: "Composes shell navigation, page metadata, theme, and static UI assets.",
      options: [{ name: "NavigationItems", kind: "count", value: "31" }, { name: "DefaultTheme", kind: "value", value: "System" }]
    },
    {
      id: "module-project-units", key: "Monica:ProjectUnits", name: "ModuleProjectUnits", assembly: "Monica.ProjectUnits", capability: "Application", state: "Active", duration: 82, dependencies: ["module-dependency-injection", "module-object-mapping"], dependents: ["module-mcp", "flight-domain"], callbackCount: 14, discoveryMatches: 6842,
      summary: "Builds the ProjectUnit catalog from the centralized type-discovery compilation.",
      options: [{ name: "ProjectUnitCount", kind: "count", value: "438" }]
    },
    {
      id: "module-dependency-injection", key: "Monica:DependencyInjection", name: "ModuleDependencyInjection", assembly: "Monica.DependencyInjection", capability: "Core", state: "Active", duration: 23, dependencies: ["module-core"], dependents: ["module-project-units", "module-http-client"], callbackCount: 26, discoveryMatches: 274,
      summary: "Commits discovered service contracts with explicit writer add/replace/skip semantics.",
      options: [{ name: "AddWrites", kind: "count", value: "247" }, { name: "ReplaceWrites", kind: "count", value: "19" }, { name: "SkippedWrites", kind: "count", value: "8" }]
    },
    {
      id: "module-telemetry", key: "Monica:Telemetry", name: "ModuleTelemetry", assembly: "Monica.OpenTelemetry", capability: "Observability", state: "Active", duration: 13, dependencies: ["module-core", "module-logging"], dependents: ["flight-observability"], callbackCount: 8, discoveryMatches: 6,
      summary: "Registers low-cardinality module metrics and tracing sources.",
      options: [{ name: "MeterName", kind: "value", value: "Monica.Core.Modularity" }, { name: "Exporters", kind: "count", value: "1" }]
    },
    {
      id: "module-openapi", key: "Monica:OpenApi", name: "ModuleOpenApi", assembly: "Monica.Web", capability: "Web", state: "Active", duration: 12, dependencies: ["module-configuration", "module-web"], dependents: ["flight-api"], callbackCount: 6, discoveryMatches: 12,
      summary: "Contributes endpoint metadata and the OpenAPI document pipeline.", options: [{ name: "Documents", kind: "count", value: "1" }]
    },
    {
      id: "module-localization", key: "Monica:Localization", name: "ModuleLocalization", assembly: "Monica.Localization", capability: "UI", state: "Active", duration: 9, dependencies: ["module-core"], dependents: ["module-shell-ui"], callbackCount: 5, discoveryMatches: 18,
      summary: "Registers localized resource catalogs and culture negotiation.", options: [{ name: "Cultures", kind: "count", value: "2" }, { name: "DefaultCulture", kind: "value", value: "zh-CN" }]
    },
    {
      id: "module-http-client", key: "Monica:HttpClient", name: "ModuleHttpClient", assembly: "Monica.HttpClient", capability: "Integration", state: "Active", duration: 7, dependencies: ["module-dependency-injection"], dependents: ["flight-integration"], callbackCount: 9, discoveryMatches: 17,
      summary: "Registers named HTTP client definitions and resilience policies.", options: [{ name: "Clients", kind: "count", value: "5" }, { name: "BearerToken", kind: "presence", value: "Configured" }]
    },
    {
      id: "module-ai", key: "Monica:AI", name: "ModuleAI", assembly: "Monica.AI", capability: "AI", state: "Active", duration: 6, dependencies: ["module-http-client"], dependents: ["module-mcp"], callbackCount: 8, discoveryMatches: 14,
      summary: "Provides AI provider contracts and shared execution infrastructure.", options: [{ name: "Providers", kind: "count", value: "2" }, { name: "OpenAiApiKey", kind: "presence", value: "Configured" }]
    },
    {
      id: "module-web", key: "Monica:Web", name: "ModuleWeb", assembly: "Monica.Web", capability: "Web", state: "Active", duration: 5, dependencies: ["module-core"], dependents: ["module-openapi", "flight-api"], callbackCount: 7, discoveryMatches: 9,
      summary: "Contributes shared ASP.NET Core web-host behavior.", options: [{ name: "ForwardedHeaders", kind: "value", value: "Enabled" }]
    },
    {
      id: "module-logging", key: "Monica:Logging", name: "ModuleLogging", assembly: "Monica.Logging", capability: "Observability", state: "Active", duration: 4, dependencies: ["module-core"], dependents: ["application-configuration", "module-telemetry"], callbackCount: 5, discoveryMatches: 2,
      summary: "Configures structured logging enrichers and framework categories.", options: [{ name: "MinimumLevel", kind: "value", value: "Information" }]
    },
    {
      id: "flight-domain", key: "FIPS2022:FlightDomain", name: "FlightDomainModule", assembly: "FlightService.Domain", capability: "Business", state: "Active", duration: 4, dependencies: ["module-project-units"], dependents: ["flight-api"], callbackCount: 8, discoveryMatches: 36,
      summary: "Registers Flight bounded-context units and domain handlers.", options: [{ name: "AggregateTypes", kind: "count", value: "11" }]
    },
    {
      id: "flight-api", key: "FIPS2022:FlightApi", name: "FlightApiModule", assembly: "FlightService.API", capability: "Business", state: "Active", duration: 3, dependencies: ["flight-domain", "module-openapi", "module-web"], dependents: [], callbackCount: 13, discoveryMatches: 28,
      summary: "Maps FlightService request endpoints and published protocols.", options: [{ name: "EndpointGroups", kind: "count", value: "9" }]
    },
    {
      id: "flight-integration", key: "FIPS2022:FlightIntegration", name: "FlightIntegrationModule", assembly: "FlightService.Infrastructure", capability: "Integration", state: "Active", duration: 3, dependencies: ["module-http-client", "flight-domain"], dependents: ["flight-api"], callbackCount: 6, discoveryMatches: 7,
      summary: "Registers downstream aviation data connectors.", options: [{ name: "Connectors", kind: "count", value: "3" }, { name: "SupplierToken", kind: "presence", value: "Configured" }]
    },
    {
      id: "flight-observability", key: "FIPS2022:FlightObservability", name: "FlightObservabilityModule", assembly: "FlightService.API", capability: "Observability", state: "Active", duration: 2, dependencies: ["module-telemetry"], dependents: [], callbackCount: 4, discoveryMatches: 1,
      summary: "Adds service-specific meters, activities, and health tags.", options: [{ name: "ServiceName", kind: "value", value: "FlightService.API" }]
    },
    {
      id: "flight-ui", key: "FIPS2022:FlightUI", name: "FlightUIModule", assembly: "FlightService.UI", capability: "UI", state: "Active", duration: 2, dependencies: ["module-shell-ui", "flight-domain"], dependents: [], callbackCount: 7, discoveryMatches: 16,
      summary: "Registers FlightService pages and localized navigation.", options: [{ name: "Pages", kind: "count", value: "6" }]
    },
    {
      id: "flight-mcp", key: "FIPS2022:FlightMcp", name: "FlightMcpModule", assembly: "FlightService.Application", capability: "AI", state: "Active", duration: 1, dependencies: ["module-mcp", "flight-domain"], dependents: [], callbackCount: 5, discoveryMatches: 5,
      summary: "Publishes flight search and operational tools over MCP.", options: [{ name: "Tools", kind: "count", value: "5" }]
    },
    {
      id: "module-core", key: "Monica:Core", name: "ModuleCore", assembly: "Monica.Core", capability: "Core", state: "Active", duration: 1, dependencies: [], dependents: ["module-configuration", "module-dependency-injection", "module-localization", "module-logging", "module-object-mapping", "module-web"], callbackCount: 4, discoveryMatches: 0,
      summary: "Owns the composition runtime, immutable graph, and diagnostics snapshot.", options: []
    }
  ],

  edges: [
    ["module-configuration", "module-core"], ["module-object-mapping", "module-core"], ["module-dependency-injection", "module-core"], ["module-project-units", "module-dependency-injection"], ["module-project-units", "module-object-mapping"],
    ["module-mcp", "module-ai"], ["module-mcp", "module-project-units"], ["module-shell-ui", "module-configuration"], ["module-shell-ui", "module-localization"], ["module-telemetry", "module-core"], ["module-telemetry", "module-logging"],
    ["application-configuration", "module-configuration"], ["application-configuration", "module-logging"], ["module-openapi", "module-configuration"], ["module-openapi", "module-web"], ["module-http-client", "module-dependency-injection"], ["module-ai", "module-http-client"],
    ["flight-domain", "module-project-units"], ["flight-api", "flight-domain"], ["flight-api", "module-openapi"], ["flight-api", "module-web"], ["flight-integration", "module-http-client"], ["flight-integration", "flight-domain"],
    ["flight-observability", "module-telemetry"], ["flight-ui", "module-shell-ui"], ["flight-ui", "flight-domain"], ["flight-mcp", "module-mcp"], ["flight-mcp", "flight-domain"]
  ],

  queryContributions: [
    { module: "ModuleProjectUnits", query: "All business types", matches: 6842, evaluatedMs: 61, commitMs: 63 },
    { module: "ModuleDependencyInjection", query: "Service contract attributes", matches: 274, evaluatedMs: 33, commitMs: 22 },
    { module: "ModuleObjectMapping", query: "Mapping interfaces", matches: 119, evaluatedMs: 29, commitMs: 6 },
    { module: "ModuleConfiguration", query: "Configuration definitions", matches: 42, evaluatedMs: 18, commitMs: 4 },
    { module: "ModuleShellUI", query: "UI pages and navigation", matches: 22, evaluatedMs: 12, commitMs: 3 },
    { module: "ModuleMcp", query: "MCP tools and prompts", matches: 8, evaluatedMs: 7, commitMs: 2 }
  ],

  assemblies: [
    { name: "FlightService.LegacyAdapters", outcome: "ResolutionFailed", types: null, ms: 11, detail: "FileNotFoundException · optional development adapter", path: "[path omitted in export]" },
    { name: "ThirdParty.Aviation.Protocols", outcome: "PartialTypeLoad", types: 184, ms: 37, detail: "3 loader failures · 184 usable types retained", path: "D:\\Repositories\\WorkTree1\\FIPS2022\\src\\FlightService\\bin\\Debug\\net10.0\\ThirdParty.Aviation.Protocols.dll" },
    { name: "FlightService.API", outcome: "Scanned", types: 218, ms: 22, detail: "Project library", path: "D:\\Repositories\\WorkTree1\\FIPS2022\\src\\FlightService\\bin\\Debug\\net10.0\\FlightService.API.dll" },
    { name: "FlightService.Application", outcome: "Scanned", types: 467, ms: 31, detail: "Project library", path: "D:\\Repositories\\WorkTree1\\FIPS2022\\src\\FlightService\\bin\\Debug\\net10.0\\FlightService.Application.dll" },
    { name: "FlightService.Domain", outcome: "Scanned", types: 386, ms: 28, detail: "Project library", path: "D:\\Repositories\\WorkTree1\\FIPS2022\\src\\FlightService\\bin\\Debug\\net10.0\\FlightService.Domain.dll" },
    { name: "FlightService.Infrastructure", outcome: "Scanned", types: 293, ms: 24, detail: "Project library", path: "D:\\Repositories\\WorkTree1\\FIPS2022\\src\\FlightService\\bin\\Debug\\net10.0\\FlightService.Infrastructure.dll" },
    { name: "Monica.Core", outcome: "Scanned", types: 611, ms: 35, detail: "Framework library", path: "D:\\Repositories\\WorkTree1\\FIPS2022\\src\\FlightService\\bin\\Debug\\net10.0\\Monica.Core.dll" },
    { name: "Monica.ProjectUnits", outcome: "Scanned", types: 173, ms: 19, detail: "Framework library", path: "D:\\Repositories\\WorkTree1\\FIPS2022\\src\\FlightService\\bin\\Debug\\net10.0\\Monica.ProjectUnits.dll" },
    { name: "Monica.UI", outcome: "Scanned", types: 731, ms: 42, detail: "Framework library", path: "D:\\Repositories\\WorkTree1\\FIPS2022\\src\\FlightService\\bin\\Debug\\net10.0\\Monica.UI.dll" },
    { name: "Microsoft.AspNetCore.Components", outcome: "Scanned", types: 527, ms: 29, detail: "Runtime library", path: "C:\\Program Files\\dotnet\\shared\\Microsoft.AspNetCore.App\\10.0.0\\Microsoft.AspNetCore.Components.dll" },
    { name: "Microsoft.Extensions.DependencyInjection", outcome: "Scanned", types: 194, ms: 13, detail: "Runtime library", path: "C:\\Program Files\\dotnet\\shared\\Microsoft.AspNetCore.App\\10.0.0\\Microsoft.Extensions.DependencyInjection.dll" },
    { name: "MudBlazor", outcome: "Excluded", types: null, ms: 0, detail: "Excluded by framework assembly policy", path: "D:\\Repositories\\WorkTree1\\FIPS2022\\src\\FlightService\\bin\\Debug\\net10.0\\MudBlazor.dll" }
  ],

  baseline: {
    schemaVersion: 3,
    name: "FlightService · dev · 2026-08-01",
    applicationStartupMs: 5470.4421,
    totalMs: 3891,
    registrationMs: 2860,
    discoveryMs: 744,
    moduleCount: 92,
    findingCount: 3,
    modulesAdded: ["FlightObservabilityModule"],
    modulesRemoved: [],
    edgesAdded: 1,
    edgesRemoved: 0
  },

  translations: {
    en: {
      overview: "Overview", performance: "Performance", modules: "Modules", dependencies: "Dependencies", discovery: "Discovery",
      subtitle: "Module composition flight recorder", final: "Final", succeeded: "Succeeded", compare: "Compare", export: "Export", refresh: "Refresh",
      section: "Section", light: "Light", dark: "Dark", help: "Help", noBaseline: "No baseline", baselineLoaded: "Baseline loaded",
      startupTiming: "Startup timing", applicationStartup: "Application startup", tracked: "Tracked", startupBoundary: "Start marker → ready",
      moduleComposition: "Module composition", compositionShare: "Composition share", remainingStartup: "Remaining startup",
      exact: "Exact", finalCompositionInterval: "Final composition interval", versusBaseline: "vs baseline",
      prototypeTrackingOn: "Prototype: tracking on", prototypeTrackingOff: "Prototype: tracking off"
    },
    zh: {
      overview: "总览", performance: "性能", modules: "模块", dependencies: "依赖关系", discovery: "类型发现",
      subtitle: "模块组合飞行记录器", final: "已完成", succeeded: "成功", compare: "对比", export: "导出", refresh: "刷新",
      section: "视图", light: "浅色", dark: "深色", help: "帮助", noBaseline: "未加载基线", baselineLoaded: "已加载基线",
      startupTiming: "启动耗时", applicationStartup: "应用启动", tracked: "已记录", startupBoundary: "计时起点 → 就绪",
      moduleComposition: "模块组合", compositionShare: "组合占比", remainingStartup: "其余启动耗时",
      exact: "精确值", finalCompositionInterval: "最终模块组合区间", versusBaseline: "相对基线",
      prototypeTrackingOn: "原型：已启用计时", prototypeTrackingOff: "原型：未启用计时"
    }
  }
};
