# Profiles and source minimums

Profiles are optional repository initialization presets. Installing or exploring Guide does not select one.

## Application

Install the common `$monica-application` and ProjectUnit closure only after the user selects application initialization. Select an architecture capability only when needed. No source binding is required: the user may bind Monica, Monica.Docs, both, or neither.

## Extension author

Install the framework, architecture, development, third-party extension, and testing closure; enable UI/design only when selected. Require an exact Monica binding only when work depends on framework internals. Generated extensions continue consuming Monica through NuGet; never add the bound source as a project reference.

## Framework contributor

Install the framework development and testing closure. Require the global Monica binding to match the active canonical Monica workspace commit for exact contribution operations. The binding remains lookup-only; write authorization comes from the current task and active checkout, not Guide state.

## Docs contributor

Install docs authoring plus the Monica.Docs application closure. Require the Monica binding to match the framework consumed by the docs workspace and the Monica.Docs binding to match the active docs checkout. Both bindings remain lookup-only.

## Detection and versions

Infer profiles from canonical Git identity first, then characteristic files. Detection is advisory. Treat empty, mixed, ambiguous, or non-Monica repositories as normal toolbox contexts until the user chooses initialization.

Resolve framework versions in this order:

1. Monica `ProjectReference` source metadata.
2. `packages.lock.json` and `obj/project.assets.json` resolved versions.
3. central package management.
4. project package declarations.

Fail exact-parity operations on mixed versions, ranges, unavailable immutable releases, missing identity/commit, or an incompatible binding. A normal `source resolve` still returns a usable mismatched or dirty checkout with explicit warnings.

When cache resolution is needed but `inspect-dependency-source` is unavailable, report the `cached-source-resolution` prerequisite instead of inventing a source. Use only its catalog-pinned immutable distribution and never install it without current-session approval. This capability is toolbox-wide, not owned by a profile.
