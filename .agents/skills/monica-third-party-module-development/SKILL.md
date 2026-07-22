---
name: monica-third-party-module-development
description: Create, modernize, validate, package, or publish independent Monica module libraries. Use when building a third-party Monica NuGet package, choosing a publisher-first package ID, placing multiple Monica modules in one package, assigning string ModuleKey values, creating infrastructure/web/UI/mixed modules, applying the Monica compatibility mark, selecting an open-source or commercial license, preparing NuGet metadata and CI, or migrating an older library into the Monica ecosystem.
---

# Monica Third-Party Module Development

Build independent Monica packages against one versioned ecosystem contract. Treat a NuGet package as a distribution boundary and a Monica module as a runtime capability: one package may contain one or many coherent modules.

## Required companion skills

- Use `$monica-architecture` for project layout and module boundaries.
- Use `$monica-development` for module registration, Guide methods, Facades, and services.
- Use `$monica-ui-development`, `$monica-ui-audit`, and `$monica-ui-localization` for UI modules.
- Use `$monica-unit-testing` for tests.
- Use `$monica-docs-authoring` only when changing Monica.Docs itself.
- Use `$monica-ui-bridge-debug` and `$playwright-cli` when a UI package needs a runnable verification host.

`$monica-unit-testing` describes Monica's first-party `tests/Test.Monica.*` layout. Independently published packages intentionally override that one naming rule: use `tests/Test.<PackageId>` and make the generated `tests/README.md` the repository-local test authority. The host lifecycle, isolation, assertion, and WSL execution rules still apply.

## Workflow

1. Inspect repository instructions, current status, license, project files, public APIs, and tests. Preserve unrelated user work.
2. Collect the package contract before generating files:
   - publisher ID and package family name
   - display authors and NuGet owner, description, project and support/security URLs, plus a repository URL when a source provider is configured
   - target framework and minimum Monica version
   - every module name, runtime kind, registration method, key, and dependency
   - explicit source availability and provider
   - license, open-source declaration, and public/private distribution model
   - package icon choice, open-source badge choice, and publishing target
3. Read [ecosystem-standard.md](references/ecosystem-standard.md). Reject IDs and module keys that do not comply.
4. Select the architecture from [project-patterns.md](references/project-patterns.md). A package may contain multiple modules; keep each module registration unit in its own `Modules/Module{Name}.cs` file.
5. For a new repository, write a manifest following [manifest-schema.md](references/manifest-schema.md), then run:

   ```bash
   python scripts/scaffold_package.py --manifest <manifest.json> --output <repository-directory>
   ```

6. Implement the real capability. Do not publish a no-op module, placeholder behavior, TODO comments, fake providers, or sample secrets.
7. Add sociable tests at the smallest honest boundary. Prove module composition with a Monica host when registration, options, dependencies, or UI integration matter.
8. Validate source and package metadata:

   ```bash
   python scripts/validate_package.py --root <repository-directory>
   ```

   Generated repositories carry their own validator and packed-artifact inspector under `scripts/`. A release workflow that derives `PackageVersion` from a tag must pass that effective version through `--package-version`; this prevents a stable tag from bypassing prerelease Monica dependency rules.

9. Restore, build, test, pack, inspect the `.nupkg`, and consume it from a clean local feed. Use one `dotnet` build/test process at a time under WSL and pass Windows paths.
10. Read [publishing.md](references/publishing.md), configure Trusted Publishing when available, and release an immutable SemVer version.

## Non-negotiable identity rules

- Reserve `Monica.*` package IDs and the purple Monica logo for first-party packages.
- Name a third-party package `<Publisher>.Monica.<Package>[.<Variant>]`.
- Use only dot-separated ASCII letter-or-digit segments, begin every segment with a letter, and keep the ID at or below 100 characters.
- Make `PackageId`, project name, assembly name, and root namespace identical.
- Put third-party module types and `Add*` extensions in `<PackageId>.Modules`. Keep official Monica dependency guides in `Monica.Modules`.
- Assign every module a unique string key that equals the package ID or starts with `<PackageId>.`.
- Use a final `.UI` key segment for a third-party UI module.
- End UI module type names with one exact `UI` suffix and give them a non-empty base name. Non-UI module names must not end in `UI`.
- Keep module keys unique under ordinal case-insensitive comparison.
- Do not encode license, maturity, or “community” status in the package ID.

Examples:

```text
PackageId: Acme.Monica.Observability
Module keys:
  Acme.Monica.Observability.Logging
  Acme.Monica.Observability.Tracing
  Acme.Monica.Observability.UI

PackageId: Euynac.Monica.GachaPool
Module keys:
  Euynac.Monica.GachaPool
  Euynac.Monica.GachaPool.UI
```

## Branding

- Copy [monica-compatibility-mark.svg](assets/monica-compatibility-mark.svg) or [monica-compatibility-mark.png](assets/monica-compatibility-mark.png) when the publisher wants the Monica Compatibility Mark.
- An open-source publisher may explicitly request the README-only [monica-open-source-badge.svg](assets/monica-open-source-badge.svg) when the manifest declares `license.openSource=true` and a NuGet-accepted SPDX `PackageLicenseExpression`. Never infer open-source status merely from the presence of an expression. The badge is not a package icon and does not change package identity.
- A publisher-owned icon is equally valid.
- Never recolor, modify, or reuse Monica's official purple package mark for a third-party package.
- Include this notice when using the compatibility mark:

  > This community package is independently maintained and is not affiliated with, endorsed by, or supported by the Monica project.

- Do not claim “official”, “certified”, or “verified”. The v1 mark is self-attested compatibility, not Monica approval.

## Licensing and commercial use

Read [licensing-and-commercial-use.md](references/licensing-and-commercial-use.md) whenever the package is proprietary, source-available, dual-licensed, paid, or distributed outside NuGet.org.

- Let the publisher choose an open-source or proprietary license.
- Configure exactly one of `PackageLicenseExpression` or `PackageLicenseFile`.
- Do not silently choose a license for the developer.
- Keep Monica's MIT copyright and permission notice with copied Monica source.
- Treat NuGet.org as a public download feed, not a payment or entitlement system. Use a private feed for restricted paid binaries.
- Do not generate or retain a NuGet.org publishing workflow for `private-feed` or `none` targets. CI automation follows the hosting provider, while Source Link and consumer-visible repository metadata additionally require `source.available=true`.

## Completion gate

Do not describe a package as ready until all applicable checks pass:

- package, assembly, namespace, module-key, and module-dependency alignment
- XML documentation for public/developer-facing APIs
- no warnings in restore/build/test/pack
- synchronized `zh-CN` and `en-US` resources for UI packages
- theme-token and responsive UI validation
- package icon, README, license, symbols, and ecosystem tags, plus repository metadata and Source Link when source is consumer-accessible
- local-feed restore and a clean consumer host registration test
- prerelease package version when any Monica dependency is prerelease
- no secrets, local absolute paths, or unpublished project references in the packed artifact
