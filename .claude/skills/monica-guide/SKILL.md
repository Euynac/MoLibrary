---
name: monica-guide
description: Bootstrap, configure, diagnose, update, and safely route Monica development environments for Codex, Claude Code, and other Agent Skills hosts. Use when initializing a Monica application, extension, framework checkout, or Monica.Docs checkout; selecting a Monica skill profile or release channel; binding exact framework source; managing Monica-owned AGENTS.md instructions; checking installation or version health; or preparing to contribute a finding upstream.
---

# Monica Guide

Use the bundled CLI as the source of truth for environment changes. Keep ordinary implementation work in the routed Monica development skills.

## Workflow

1. Run a read-only status or doctor command first:

   ```bash
   node "<skill-dir>/scripts/monica-guide.mjs" doctor --workspace "<repository>"
   ```

2. Preview a mutating intent. Never add `--apply` on the first run:

   ```bash
   node "<skill-dir>/scripts/monica-guide.mjs" init --workspace "<repository>" --profile application
   ```

3. Present the complete actions, diffs, warnings, blockers, and `planDigest`. Obtain confirmation for the detected profile and the plan.
4. Apply only the unchanged plan:

   ```bash
   node "<skill-dir>/scripts/monica-guide.mjs" init --workspace "<repository>" \
     --profile application --apply --plan-digest "sha256:..."
   ```

5. Run `doctor` again. Route application work to `$monica-application`, framework work to `$monica-framework`, and upstream contribution preparation to `$monica-contribution`.

## Operating rules

- Treat `stable`, `preview`, and explicit `source` as distinct channels. Never replace an unavailable immutable release with a branch or another version.
- In offline mode, require verified cached release contracts plus an exact local/cached Monica source whose complete managed skill tree matches the target manifest. Install from that local tree with the pinned CLI in npm offline mode; never contact GitHub or the npm registry.
- Install the `source` channel only from its exact bound checkout. Revalidate the source path, commit, cleanliness/provenance, and managed skill manifest at preview and apply time.
- Require an explicit profile for the first applied initialization, even when detection is confident.
- Keep only one active global Monica skill release per user. Require `--switch-global` before replacing it.
- Read per-skill revisions from the immutable release catalog. Treat digests as the machine contract and revisions plus `lastChangedIn` as the human-readable change history; never infer versions from `SKILL.md` frontmatter.
- A normal `update` reinstalls only new, changed, unknown, missing, or tampered skills, then verifies every managed skill before switching the global release. Use repeatable `--skill` only for an explicitly requested targeted update; include required dependencies and block when another managed skill would change or prevent full verification.
- Treat the previewed `protect-global-skills` action as mandatory. The Guide snapshots selected Monica skills and planned files, restores only attempted work, and verifies its observable recovery contract if any protected action fails. Stop when private recovery evidence is retained; `doctor` must be clean before another mutation.
- Store local source paths and contribution preferences only in user state. Never put them in repository configuration.
- Modify only the marked root `AGENTS.md` block. Refuse malformed or duplicate markers. Do not rewrite nested instruction files implicitly.
- Create or preserve the minimal root `CLAUDE.md` import `@AGENTS.md`. Skill discovery is live when supported; AGENTS/CLAUDE instruction changes take effect in a new agent run.
- Do not create issues, branches, commits, pushes, or pull requests. Persisted contribution preferences never replace current-session approval.

Read [references/cli.md](references/cli.md) for command and state contracts, [references/profiles.md](references/profiles.md) for profile and source rules, and [references/safety.md](references/safety.md) for offline, discovery, privacy, and contribution constraints.
