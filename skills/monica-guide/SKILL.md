---
name: monica-guide
description: Explain, bootstrap, configure, diagnose, update, and safely route the Monica toolbox for any Agent Skills-compatible host. Use when someone is new to Monica or has not chosen a repository/profile yet; when initializing or maintaining a Monica application, extension, framework checkout, or Monica.Docs checkout; when selecting Monica skills or releases; when an agent needs verified Monica or Monica.Docs source; or when preparing a safe upstream contribution.
---

# Monica Guide

Use the bundled CLI as the source of truth for toolbox state and environment changes. Keep ordinary implementation work in the routed Monica development skills.

## Workflow

1. Start without assuming a repository or development path:

   ```bash
   node "<skill-dir>/scripts/monica-guide.mjs" overview
   ```

   Explain the available tools, current global skill release and agent targets, Monica and Monica.Docs bindings, and reasonable next actions. Ask what the user wants to do.

2. When an agent needs first-party source, resolve the global binding before guessing a path, cloning, or opening a moving branch:

   ```bash
   node "<skill-dir>/scripts/monica-guide.mjs" source resolve --repository monica --json
   node "<skill-dir>/scripts/monica-guide.mjs" source resolve --repository docs --json
   ```

   Treat a returned path as a verified lookup location, never as write authorization. Report compatibility and dirty-state warnings instead of hiding the path.

3. Run global `status` or `doctor` without a workspace. Add `--workspace "<path>"` only when the user wants repository-specific detection and compatibility checks.

4. Run `init`, `configure`, or `update` only after the user chooses a concrete repository workflow. Require explicit profile confirmation, present the complete dry-run actions, diffs, warnings, blockers, and `planDigest`, then apply only the unchanged approved plan.

5. Route application work to `$monica-application`, framework and extension work to `$monica-framework`, documentation work to `$monica-docs-authoring`, and upstream contribution preparation to `$monica-contribution`.

## Operating rules

- Keep toolbox discovery healthy without forcing initialization. Empty, mixed, uninitialized, and non-Monica directories are valid until the user selects a repository operation.
- Treat agent target values as Agent Skills identifiers. Verify each target independently with the catalog-pinned `skills` CLI; do not restrict targets to named hosts or infer installation directories.
- Keep `stable`, `preview`, and exact `source` distinct. Never replace an unavailable immutable release with a branch or another version.
- Maintain one active global Monica skill release and at most one global binding for each first-party repository. Either binding may be used from any profile.
- Store source identity, ref, commit, provenance, resolution kind, and path in private user state. Observe compatibility, dirtiness, and path health at lookup time; never persist or infer write permission.
- Revalidate exact parity for operations that require it. A normal lookup may return a dirty or version-mismatched binding with prominent warnings.
- Install downstream Monica skills only for a user-selected profile. Require `--switch-global` before replacing the active global release.
- Modify only Guide-owned instruction spans. Never treat managed instructions, contribution preferences, or source bindings as permission for remote or repository mutations.
- Source public bootstrap instructions only from [assets/bootstrap-prompts.json](assets/bootstrap-prompts.json). Never maintain host-, goal-, release-, or locale-specific executable prompt copies outside that manifest.

Read [references/toolbox.md](references/toolbox.md) for intent selection, [references/cli.md](references/cli.md) for command and state contracts, [references/profiles.md](references/profiles.md) for profile source minimums, [references/versioning.md](references/versioning.md) for release timing, and [references/safety.md](references/safety.md) for discovery, offline, privacy, and contribution constraints.
