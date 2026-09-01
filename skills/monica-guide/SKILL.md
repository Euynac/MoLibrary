---
name: monica-guide
description: Explain, bootstrap, configure, diagnose, update, and safely route the Monica toolbox for any Agent Skills-compatible host. Use when someone is new to Monica or has not chosen a repository/profile yet; when initializing or maintaining a Monica application, extension, framework checkout, or Monica.Docs checkout; when selecting Monica skills or releases; when an agent needs verified Monica or Monica.Docs source; or when preparing a safe upstream contribution.
---

# Monica Guide

The authoritative guide engine is the **Monica.Guide executable** (`Monica.Guide.exe` on Windows, `Monica.Guide` on Linux and macOS); this skill only teaches and routes. Locate the executable and assign it to `$monica-guide`, then use its `overview | status | doctor | configure | unconfigure | init | forget | source <action> | issue <action>` commands as the single source of truth for installation, workspace bootstrap, source lookup state, and the issue-reporting mode.

## Locating the engine

Try in order:

1. The installed guide executable: read `ExecutablePath` from `%LOCALAPPDATA%\Monica\installation.json` (Windows; `~/.local/share/Monica` on Linux, `~/Library/Application Support/Monica` on macOS). `MONICA_GUIDE_DATA_ROOT` overrides the data root.
2. Inside a canonical Monica checkout: `dotnet run --project Monica.Guide.App -- <command> …` (development form; commands and envelopes are identical).

Every mutating command is preview-first: it prints a plan with a `planDigest`, changes nothing, and only `--apply --plan-digest <digest>` executes the unchanged approved plan. Exit codes are stable machine contracts: `0` ready, `1` warnings, `2` usage error, `3` errors/blockers. Prefer `--json` for machine parsing.

## Workflow

Monica skills install **into the project by default**, never globally unless the user explicitly asks for a machine-wide install.

1. Start without assuming a repository or development path:

   ```bash
   $monica-guide overview --json
   $monica-guide workspaces --json
   ```

   Explain the installed products, registered workspaces, and reasonable next actions. Ask what the user wants to do. Never force initialization on an empty, mixed, or non-Monica directory.

2. When an agent needs first-party source, resolve the global binding before guessing a path, cloning, or opening a moving branch:

   ```bash
   $monica-guide source resolve --repository monica --json
   $monica-guide source resolve --repository docs --json
   ```

   Treat a returned path as a verified lookup location, never as write authorization. Report dirty-checkout and commit-movement warnings instead of hiding the path. Bind or rebind with `source bind --repository <repo> --source-path <checkout>` (or `--source-ref <exact-ref>` through the pinned `inspect-dependency-source` resolver), always through preview and apply.

   Before preparing any upstream issue artifact, read the machine's issue-reporting mode with `issue status` and honor it: `prepare` allows local drafts (remote actions still need current-session approval), `ask` requires asking before drafting, and `never` forbids issue preparation entirely. A persisted mode is never authorization for a remote action.

3. Initialize a repository workspace after the user confirms a concrete profile:

   ```bash
   $monica-guide init --workspace <path> --profile <application|extension-author|framework-contributor|docs-contributor> --capability <microservice|modular-monolith|ui> --json
   ```

   `init` writes `.monica/guide.json` (including the optional `skillTargets` list of workspace-relative install directories, default `.agents/skills`) plus one managed instruction block in the root `AGENTS.md` (and the minimal `@AGENTS.md` import in `CLAUDE.md` when a Claude target is installed), and registers the workspace in the engine registry. Inspect advisory detection with `status --workspace <path>` first; detection is a suggestion, confirmation is the user's.

4. Install or refresh that workspace's skills — this is the default skill flow:

   ```bash
   $monica-guide configure --workspace <path> --json
   $monica-guide configure --workspace <path> --apply --plan-digest <digest>
   ```

   The workspace's confirmed profile selects the skill closure from the release bundle's catalog; skills land in the workspace's configured project directories. `unconfigure --workspace <path>` removes exactly that workspace's installations; `forget --workspace <path>` removes the configuration, instruction block, project skill trees, and registry entry together.

5. Global installs are explicit opt-ins only (`configure --environment windows --target shared|claude`), for users who want the skills in every project; never choose them on the user's behalf.

6. Diagnose with `status` or `doctor` (add `--workspace` for repository-specific checks, including the installed-versus-profile skill count). Update skills by configuring a newer release bundle — or with the wizard's Update page, which verifies the release `SHA256SUMS` before activation. The wizard also offers a Workspaces page listing every registered workspace and a Sources page observing the global source bindings and the issue-reporting mode.

7. Route application work to `$monica-application`, framework and extension work to `$monica-framework`, documentation work to `$monica-docs-authoring`, and upstream contribution preparation to `$monica-contribution`.

## Operating rules

- One active skill release per product, recorded in the unified guide ledger; installations are keyed by environment and target root, so many project directories coexist with the global catalogs, and each is removable through its own `unconfigure` preview.
- Maintain at most one global binding per first-party repository. Bindings record exact provenance (ref, commit, resolver) and remain lookup-only locators; never treat them as permission for remote or repository mutations.
- Keep `stable`-grade releases immutable: never replace an unavailable release asset with a branch or another version. Offline operation uses the installed bundle and local bindings only.
- Install downstream Monica skills only for a user-selected profile, into that project's directories by default; require explicit approval before replacing an active installation.
- Source public bootstrap prompts only from [assets/bootstrap-prompts.json](assets/bootstrap-prompts.json). Never maintain host-, goal-, release-, or locale-specific executable prompt copies outside that manifest.
- Extension work must consume Monica through immutable NuGet packages; a Monica `ProjectReference` in an extension workspace is an `init` blocker. Bind exact source separately as a lookup locator instead.

Read [references/toolbox.md](references/toolbox.md) for intent selection, [references/cli.md](references/cli.md) for the command and state contracts, [references/profiles.md](references/profiles.md) for profile source minimums, [references/versioning.md](references/versioning.md) for release timing, and [references/safety.md](references/safety.md) for discovery, offline, privacy, and contribution constraints.
