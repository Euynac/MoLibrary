# Third-Party Project Patterns

## Choose the distribution boundary first

Use one package when its modules share ownership, versioning, support policy, release cadence, and consumers. Split packages only when users need to install or version capabilities independently.

## Single infrastructure module

```text
<PackageId>/
├── Modules/Module{Name}.cs
├── Abstractions/
├── Models/
├── Facades/
├── Services/
└── Providers/
```

Use `ModuleBase` unless the module actually configures middleware or endpoints.

## Multiple modules in one package

```text
<PackageId>/
├── Modules/
│   ├── ModuleFeatureA.cs
│   ├── ModuleFeatureB.cs
│   └── ModuleFamily.cs          # optional aggregator
├── FeatureA/
│   ├── Abstractions/
│   ├── Models/
│   ├── Facades/
│   └── Services/
└── FeatureB/
    ├── Abstractions/
    ├── Models/
    ├── Facades/
    └── Services/
```

Each feature follows the normal Monica layer rules. Do not let one feature consume another feature's internal services; use public abstractions and models.

All independently published module registration units and `Add*` extensions use `<PackageId>.Modules`. References to official Monica dependency guides continue to import `Monica.Modules`.

## Mixed infrastructure and UI package

Use this when UI is tightly coupled to the capability and does not need independent distribution.

```text
<PackageId>/
├── Modules/
│   ├── Module{Name}.cs
│   └── Module{Name}UI.cs
├── Models/
├── Facades/
├── Services/
├── Pages/
│   └── UI{Name}Page.razor
├── UI{Name}/
│   ├── Components/
│   ├── State/
│   └── Support/
├── Localization/
│   ├── {Name}Resource.cs
│   └── {Name}Resource/
│       ├── en-US.json
│       └── zh-CN.json
└── wwwroot/
```

Rules:

- The UI module normally remains `ModuleBase`; use `WebModuleBase` only for middleware or endpoints.
- The UI module depends on the infrastructure module and `ModuleShellUI`.
- Register navigation with the UI module's own localization resource type. Derive its category ID from that UI module's key by removing only the final `.UI`; do not derive it from the package ID when a package contains multiple UI modules.
- Derive every public UI route from the package family after removing `<Publisher>.Monica.` and a distribution-only final `.UI` segment. Convert the remaining PascalCase segments to readable kebab-case words: `Tairitsua.Monica.GachaPool` owns `/gacha-pool` and subroutes such as `/gacha-pool-history`. The host route namespace is shared, and Monica rejects duplicate normalized routes, so use distinct package families or package-family subroutes for extensions that must coexist. Never claim an unrelated generic route such as `/dashboard`.
- Pages inject Facades directly. Do not add a UI service wrapper around a Facade.
- Keep page code thin and move state/orchestration to `UI{Name}/State` or `Support`.
- Use isolated `.razor.css`, MudBlazor primitives, and Monica/MudBlazor theme tokens.

Use one startup contribution block per UI module:

```csharp
shellGuide.RegisterUIComponents(registry =>
{
    var category = registry.RegisterLocalizedCategory<AuditResource>(
        "Acme.Monica.Toolkit.Audit",
        "Navigation:Category",
        order: 450);
    registry.RegisterLocalizedPage<UIAuditPage, AuditResource>(
        "/toolkit-audit",
        "Navigation:Title",
        Icons.Material.Filled.Extension,
        categoryId: category,
        addToNav: true,
        navOrder: 80);
});
```

Use deterministic explicit values. The scaffold assigns category order `450 + UI module index`, after the shell's Configuration category, and page navigation order `80 + UI module index`. Keep category identity stable when labels, cultures, or page titles change.

## Provider packages

Use `<Publisher>.Monica.<Capability>.<Provider>` when the provider has a genuinely independent dependency/release boundary. A provider module should implement public abstractions from the capability package and expose explicit Guide registration such as `UseNatsProvider()`.

## Tests

- Pure value behavior: direct construction.
- Module registration/options/dependencies: `MonicaTestApplicationFactory<TDiscoveryAnchor>`.
- Name the external runnable project `tests/Test.<PackageId>`; this intentionally overrides the first-party `Test.Monica.*` convention.
- Register graph entry modules only in the composition factory so dependency options are proven transitively. For UI modules, resolve the host-owned read-only `IPageCatalog` and assert the contributed page, category, navigation metadata, and module-owned localization resource marker. The write-only `INavigationRegistryBuilder` exists only inside startup contribution callbacks.
- Facade flows: public `Res`/`Res<T>` assertions plus observable boundary state.
- UI component logic: bUnit.
- Full visual behavior: bridge host plus Playwright at desktop and narrow widths.

For a runnable .NET 10 bridge, use `Microsoft.NET.Sdk.Web` and set
`<RequiresAspNetWebAssets>true</RequiresAspNetWebAssets>` in the bridge
project. This ensures the host emits the framework asset endpoints required by
`/_framework/blazor.web.js`; without it, static prerendering may appear while
the dashboard never becomes interactive.
