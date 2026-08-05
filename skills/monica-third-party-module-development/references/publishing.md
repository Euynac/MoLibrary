# Publishing Workflow

## Before the first release

1. Confirm the NuGet owner/publisher identity and search NuGet.org for ID collisions.
2. Consider reserving the publisher-first prefix after packages establish ownership.
3. Enable two-factor authentication and publication notifications.
4. Decide whether the package is public on NuGet.org or restricted on a private feed.
5. Validate README, license, icon, support, and security links; validate the consumer-visible repository link only when source is available.

## CI gates

Run in this order:

1. run `python scripts/validate_repository.py --root .` and applicable localization/OCI checks
2. restore
3. build with warnings as errors
4. test
5. pack without rebuilding
6. inspect the exact package set, contents, internal dependencies, and nuspecs with `python scripts/inspect_packages.py --root . --artifacts artifacts`
7. restore and run a clean consumer for every package entry point against a local feed
8. build declared OCI targets, inspect runtime metadata, and run provider-specific CPU plus applicable real-GPU smoke tests on the declared managed runner
9. upload `.nupkg`, `.snupkg`, image digests, and provenance as workflow artifacts

Do not publish on every branch build. Publish only from an immutable tag or an explicitly protected release workflow/environment.

## Trusted Publishing

Prefer NuGet Trusted Publishing with GitHub Actions OIDC when the account supports it. Configure the nuget.org policy with the repository owner, repository name, workflow file name, and optional protected environment. Request the temporary key immediately before push because it is valid for one hour.

```yaml
permissions:
  contents: read
  id-token: write

steps:
  - uses: actions/checkout@v4
  - uses: actions/setup-dotnet@v4
    with:
      dotnet-version: "10.0.x"
  - run: python scripts/validate_repository.py --root .
  - run: dotnet restore
  - run: dotnet build --configuration Release --no-restore
  - run: dotnet test --configuration Release --no-build
  - run: dotnet pack --configuration Release --no-build --output artifacts
  - run: python scripts/inspect_packages.py --root . --artifacts artifacts
  - name: Exchange OIDC token for a temporary NuGet key
    uses: NuGet/login@v1
    id: nuget-login
    with:
      user: ${{ secrets.NUGET_USER }}
  - name: Publish packages
    run: dotnet nuget push "artifacts/*.nupkg" --api-key "${{ steps.nuget-login.outputs.NUGET_API_KEY }}" --source https://api.nuget.org/v3/index.json
```

If Trusted Publishing is unavailable, use a NuGet key scoped to the exact package prefix and push permission, store it as a protected secret, set a short expiry, and rotate it.

When a tag supplies `PackageVersion`, validate that effective value before restore/build/pack:

```bash
python scripts/validate_repository.py --root . --package-version "${GITHUB_REF_NAME#v}"
```

Generate the GitHub OIDC example only for a GitHub-hosted repository targeting NuGet.org. Private-feed and `none` targets need publisher-owned, protected automation for their declared target and must not inherit a NuGet.org push step.

## Multi-package and OCI releases

- Treat the manifest version as one release-unit version for all declared packages and image tags.
- Complete all validation and builds before the first external push to reduce partial releases.
- Make `inspect_packages.py` reject missing or extra artifacts before a wildcard NuGet push.
- Publish one OCI repository with target-specific immutable tags such as `0.1.0-alpha.1-cpu-amd64` and `0.1.0-alpha.1-nvidia-cu129-amd64`.
- Record pushed digests; never replace an existing version tag.
- Standard GitHub runners may build a GPU image but cannot prove GPU inference. Use an explicitly managed NVIDIA runner for the release GPU smoke test.
- The scaffold emits no publish workflow for a release unit with OCI images until every image declares complete `releaseGates`. With complete gates, the workflow loads and inspects every image and runs all provider-specific CPU/NVIDIA commands before registry login and before either NuGet or OCI push.
- Keep package restoration on the declared Monica NuGet version. Do not make release success depend on a local Monica source checkout.
- Require every resolved `Monica.*` `PackageReference`, including centrally managed/property-based versions, to equal manifest `monicaVersion`.

## Versioning

- Follow SemVer.
- Keep a package prerelease while its Monica dependency is prerelease.
- Never overwrite a version. NuGet.org versions are immutable.
- If a release is bad, unlist it, fix the issue, increment the version, and publish again.
- Use deprecation metadata for superseded packages or versions.

## Official references

- NuGet package authoring: <https://learn.microsoft.com/nuget/create-packages/package-authoring-best-practices>
- Package IDs: <https://learn.microsoft.com/nuget/create-packages/creating-a-package-msbuild#choose-a-unique-package-identifier-and-set-the-version-number>
- Prefix reservation: <https://learn.microsoft.com/nuget/nuget-org/id-prefix-reservation>
- Trusted Publishing: <https://learn.microsoft.com/nuget/nuget-org/trusted-publishing>
- Publishing and public/private feeds: <https://learn.microsoft.com/nuget/nuget-org/publish-a-package>
