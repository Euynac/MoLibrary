# Scaffold Manifest

Create a UTF-8 JSON manifest and pass it to `scripts/scaffold_package.py`. The manifest is the explicit package contract; unsupported fields are rejected instead of silently ignored.
The scaffold writes a normalized `package.manifest.json` into the generated repository so later maintainers retain the decisions that shaped metadata and automation.

```json
{
  "schemaVersion": 1,
  "packageId": "Acme.Monica.Observability",
  "version": "0.1.0-alpha.1",
  "description": "Observable infrastructure extensions for Monica.",
  "authors": "Acme Engineering",
  "nugetOwner": "Acme",
  "repositoryUrl": "https://github.com/acme/acme-monica-observability",
  "projectUrl": "https://github.com/acme/acme-monica-observability",
  "supportUrl": "https://github.com/acme/acme-monica-observability/issues",
  "securityUrl": "https://github.com/acme/acme-monica-observability/security/policy",
  "source": {
    "available": true,
    "provider": "github",
    "sourceLinkVersion": "10.0.102"
  },
  "distribution": "public",
  "publishing": {
    "target": "nuget.org"
  },
  "targetFramework": "net10.0",
  "monicaVersion": "1.0.0-rc.6",
  "capabilityTags": ["observability", "diagnostics"],
  "license": {
    "openSource": true,
    "expression": "MIT"
  },
  "branding": {
    "icon": {
      "kind": "compatibility-mark"
    },
    "showOpenSourceBadge": true
  },
  "modules": [
    {
      "name": "Observability",
      "kind": "infrastructure",
      "key": "Acme.Monica.Observability"
    },
    {
      "name": "ObservabilityUI",
      "kind": "ui",
      "key": "Acme.Monica.Observability.UI",
      "dependsOn": ["Observability"]
    }
  ]
}
```

## Identity and contacts

- `packageId`, `version`, `description`, and `authors` become NuGet metadata.
- `nugetOwner` identifies the NuGet.org or private-feed account expected to publish the package; it is distinct from display authors.
- `projectUrl`, `supportUrl`, and `securityUrl` are always required HTTPS URLs.
- `repositoryUrl` is required only when `source.provider` is configured as `github`, `gitlab`, or `azure-repos`; omit it for `none`. This URL lets the scaffold select provider-aware automation even when the repository is private.
- `version` and `monicaVersion` must be valid three-part SemVer. A prerelease Monica dependency requires a prerelease package version.

## Source, distribution, and publishing

- `source.available` explicitly states whether package consumers can access source. It is independent from the hosting provider: a private GitHub repository uses `available: false` with `provider: github`.
- `source.provider` is `github`, `gitlab`, `azure-repos`, or `none`. When source is available, `none` is invalid. `none` requires `repositoryUrl` to be absent and produces no provider workflow.
- Consumer-visible `RepositoryUrl`, `RepositoryType`, Source Link, `PublishRepositoryUrl`, and README repository links are emitted only when `source.available` is `true`. Source Link uses the provider-specific package with `PrivateAssets=All`.
- `source.sourceLinkVersion` is required when source is available and records the deliberately selected provider-package version; the scaffold does not guess a current version. Omit it when source is unavailable.
- `distribution` is `public` or `private`.
- `publishing.target` is `nuget.org`, `private-feed`, or `none`. `nuget.org` requires public distribution. `private-feed` requires private distribution and an HTTPS `feedUrl`.
- GitHub Actions CI is generated only for the `github` provider. A Trusted Publishing workflow is generated only for the `nuget.org` target. Private-feed and `none` targets never receive a NuGet.org push workflow; configure provider-specific protected publishing separately.

## License and branding

- `license.openSource` is an explicit publisher declaration.
- Configure exactly one string `license.expression` or relative `license.file`. An open-source declaration requires a NuGet SPDX expression.
- `branding.icon.kind` is `compatibility-mark` or `publisher`. `compatibility-mark` copies Monica's canonical emerald PNG and SVG unchanged and adds the required self-attestation and independence notice to the README. A publisher icon requires a relative PNG `file` that is transparent and at least 64×64.
- `branding.showOpenSourceBadge` is explicit. It may be `true` only when `license.openSource` is `true` and the package uses a license expression. The scaffold otherwise neither copies nor displays the badge.
- README images and the package icon are copied and packed at the package root so the embedded NuGet README uses root-relative paths.

## Tags and modules

- `capabilityTags` must contain one or more lowercase package-specific tags. The scaffold owns the ecosystem tags and automatically adds `monica-ui` when any UI module exists.
- Module `kind` is `infrastructure`, `web`, or `ui`.
- UI module names must end with one exact `UI` suffix and have a non-empty base name. Non-UI module names must not end in `UI`.
- `key` is the explicit ecosystem module key. `dependsOn` contains unique, exact module names from the same manifest.
- For each UI module, the scaffold removes only the key's final `.UI` segment to create its stable navigation category ID. Multiple UI modules in one package therefore need distinct pre-`.UI` key identities; package ID alone is not used as their shared category.
- Duplicate dependencies, dependency cycles, duplicate names/keys, and UI generated-name collisions are rejected before writing files.
- Third-party module registrations and `Add*` extensions use `<PackageId>.Modules`; Monica framework dependency guides remain in `Monica.Modules`.

The scaffold is a compilable architectural starting point, not a releasable capability. Its generated tests prove graph-entry registration, transitive options, stable category identity, localized page and navigation metadata, deterministic order, and resource-marker wiring. Replace the sample shell with real public behavior and scenario tests before publishing.

Until the selected Monica version is available on a feed, build against a local Monica checkout without changing project files:

```bash
dotnet build -p:MonicaSourceRoot=<path-to-Monica-source>
```

Release workflows leave `MonicaSourceRoot` empty and therefore restore the declared NuGet dependency.
