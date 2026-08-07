# Profiles and source policy

## Application

Install the common `monica-application` and ProjectUnit closure. Select exactly one architecture capability when solution-level guidance is needed: `microservice` installs `$monica-application-microservice`, while `modular-monolith` installs `$monica-application-modular-monolith`. Do not guess for a new, mixed, or ambiguous repository; require the user to choose before applying an architecture-specific plan. Exact read-only framework source is optional but recommended whenever behavior depends on internals. Generated and existing applications continue consuming Monica through normal project references or NuGet.

## Extension author

Install the framework, architecture, development, third-party extension, and testing closure. Enable the UI closure only when the repository has UI capability or the user selects it. Require exact verified read-only Monica source. Generated extensions continue consuming Monica through NuGet; never add the managed source tree as a project reference.

## Framework contributor

Install the framework development and testing closure. Require the current workspace to be a writable `Tairitsua/Monica` checkout with an identifiable commit. A dirty writable checkout is diagnosed but remains valid for active contribution work.

## Docs contributor

Install docs authoring plus the Monica.Docs application closure. Require a writable Monica.Docs checkout and exact verified read-only Monica source. Grant writable Monica access separately and only for an explicitly approved cross-repository fix.

## Detection and versions

Infer profiles from canonical Git repository identity first, then characteristic files. Treat mixed, ambiguous, or non-Monica repositories as a user decision. Detection is never confirmation.

Resolve framework versions in this order:

1. Monica `ProjectReference` source metadata.
2. `packages.lock.json` and `obj/project.assets.json` resolved versions.
3. central package management.
4. project package declarations.

Fail on mixed versions, ranges, an unavailable immutable release, or source provenance that cannot establish the exact commit/ref. Never select a likely tag or a default branch.

When `extension-author` or `docs-contributor` lacks `inspect-dependency-source`, report the prerequisite as a blocker. The catalog's `externalSkills.inspect-dependency-source.distribution` object is the only approved installation source: it pins a human release ref, resolved commit, immutable URL, and file-manifest digest. Do not install a moving branch or unpinned package, and do not perform the external installation without current-session approval.
