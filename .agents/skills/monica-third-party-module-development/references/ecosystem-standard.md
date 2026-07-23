# Monica Ecosystem Standard v1

## 1. Scope and governance

This standard covers independently owned NuGet packages that integrate with Monica. Conformance is self-attested in v1. Monica does not review, certify, support, or warrant third-party packages merely because they follow this standard or use the compatibility mark.

## 2. Package identity

Use:

```text
<Publisher>.Monica.<Package>[.<Variant>]
```

Rules:

- `Publisher` is the NuGet owner, organization, or durable product identity.
- Every segment matches `[A-Za-z][A-Za-z0-9]*`.
- Separate segments with dots only. Do not use hyphens, spaces, or underscores.
- Keep the complete ID to 100 characters or fewer.
- `Publisher` must not be `Monica` under case-insensitive comparison.
- Match the project file name, `PackageId`, `AssemblyName`, and `RootNamespace` exactly.
- Use variants only for real distribution boundaries such as a separately shipped provider or UI companion.
- Do not add `.Community`, `.Unofficial`, `.OpenSource`, license names, or maturity labels to the ID.

Examples:

```text
Contoso.Monica.FeatureFlags
Contoso.Monica.EventBus.Nats
Contoso.Monica.EventBus.Nats.UI
```

## 3. Multiple modules in one package

A package may expose any coherent number of Monica modules. Mirror Monica's official package organization:

- place one registration unit per `Modules/Module{Name}.cs`
- use feature folders when the package contains independent capabilities
- declare dependencies explicitly with `DependsOnModule<...>().Register()`
- provide an aggregator module only when registering the full bundle is a meaningful user action
- do not create a package per module merely to satisfy this standard

The package ID is the ownership prefix for all included module keys. A module key must either equal the package ID or start with `<PackageId>.`.

```text
Package: Acme.Monica.Observability
Keys:
  Acme.Monica.Observability.Logging
  Acme.Monica.Observability.Tracing
  Acme.Monica.Observability.UI
```

Keys are compared with ordinal case-insensitive semantics. Preserve canonical casing in source and diagnostics. A third-party UI module uses `.UI` as its final segment.

Third-party UI routes use the package family without the publisher-owned identity prefix. Remove the leading `<Publisher>.Monica.` segments and a distribution-only final `.UI` segment, split the remaining PascalCase segments into lowercase kebab-case words, and join the result with hyphens. For example, `Tairitsua.Monica.GachaPool` uses `/gacha-pool`, while `Acme.Monica.Analytics.UI` uses `/analytics`. A UI capability not already represented by a package-family segment appends its own name, so an Audit UI module in `Acme.Monica.Toolkit` uses `/toolkit-audit`.

The resulting route namespace is shared by the host rather than isolated by publisher. Keep every contributed route at the package-family prefix or a descriptive extension such as `/gacha-pool-history`. Monica rejects duplicate normalized routes during registration; packages that must coexist therefore need distinct package families or distinct package-family subroutes. Do not claim an unrelated generic route such as `/dashboard` or `/settings`.

## 4. Public API naming

For each module named `{Name}`:

| Surface | Pattern |
|---|---|
| Module | `Module{Name}` |
| Options | `Module{Name}Option` |
| Guide | `Module{Name}Guide` |
| Registration | `monica.Add{Name}()` inside `builder.AddMonica(...)` |
| Module file | `Modules/Module{Name}.cs` |

Put independently published registration types in `<PackageId>.Modules`, for example `Acme.Monica.Observability.Modules`. Consumers opt into the package with its publisher-owned namespace, while official dependency guides such as `ModuleLocalizationGuide` and `ModuleShellUIGuide` remain in `Monica.Modules`. Keep business APIs in the package root namespace or its feature namespaces.

## 5. Package metadata

Require:

- `PackageId`, `Version`, `Authors`, `Description`, `PackageTags`
- `PackageProjectUrl`, plus `RepositoryUrl` and `RepositoryType=git` only when source is consumer-accessible
- `PackageReadmeFile` and an embedded README
- `PackageIcon` and an embedded 64×64 or larger transparent PNG
- exactly one license expression or packed license file
- XML documentation and symbols, plus Source Link and repository metadata where source is consumer-accessible
- tags `monica`, `monica-module`, and `monica-ecosystem-v1`

Add `monica-ui` for UI packages and at least one useful package-specific capability/provider tag. Never copy Monica's first-party author, company, repository, or copyright metadata.

## 6. Compatibility and quality

- State the minimum supported Monica version.
- A stable package must not depend on a prerelease Monica package. Keep the extension prerelease until its Monica dependency is stable.
- Avoid exact dependency pins and upper bounds unless a verified incompatibility requires them.
- Treat warnings as failures in CI.
- Test all public registration paths, including transitive dependencies and multiple hosts when state ownership matters.
- Pack and restore from a local feed before publishing.
- Never ship secrets, machine-specific paths, build outputs, or source-project references.

## 7. Branding

`Monica.*` and the official purple mark identify first-party packages. Third parties may use their own icon or the emerald Monica Compatibility Mark. Color is not the only distinction: the compatibility mark includes an extension/plug motif.

The mark means only that the publisher asserts compatibility with this standard. It does not mean official, certified, verified, endorsed, or supported by Monica. Include the standard independence disclaimer in the package README.
