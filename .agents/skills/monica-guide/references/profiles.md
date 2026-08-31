# Profiles and source minimums

Profiles are optional repository initialization presets. Installing or exploring Guide does not select one. `init` confirms a profile; `configure --workspace <path>` installs that profile's closure into the project's configured skill directories (`skillTargets`, default `.agents/skills`).

## Application

Initialize with `--capability microservice` or `--capability modular-monolith` (exactly one; `ui` is optional). The managed instruction block routes to `$monica-guide` and `$monica-application`. No source binding is required: the user may bind Monica, Monica.Docs, both, or neither.

## Extension author

Extension work consumes Monica through immutable NuGet packages. A Monica `ProjectReference` inside the workspace is an `init` blocker — bind exact Monica source separately as a lookup locator instead, and never add the bound source as a project reference.

## Framework contributor

Requires the workspace to be the canonical `Tairitsua/Monica` checkout at its Git root (origin or upstream identity). The global Monica binding should point at the commit being worked on for exact navigation; it remains lookup-only, and write authorization comes from the current task and active checkout, not from Guide state.

## Docs contributor

Requires the canonical `Tairitsua/Monica.Docs` checkout. The Monica.Docs binding locates the docs source; the Monica binding locates the framework the docs describe. Both remain lookup-only.

## Detection and versions

The engine infers the candidate profile from canonical Git identity first, then characteristic files (framework markers, docs tree, package/project references, extension markers, service/domain layout). Detection is advisory and never auto-confirms; ambiguous repositories need an explicit user choice.

Framework versions resolve for display in this order: project-reference root properties, resolved `packages.lock.json`/`obj/project.assets.json` versions, central package management, then project declarations. Mixed versions surface as warnings. When cache resolution is needed but `inspect-dependency-source` is unavailable, report the prerequisite instead of inventing a source; use only its pinned immutable distribution and never install it without current-session approval.
