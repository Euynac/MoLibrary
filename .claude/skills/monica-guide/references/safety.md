# Safety, discovery, and offline behavior

## Discovery and instruction reloads

- Codex scans repository `.agents/skills` directories up to the repository root and user `$HOME/.agents/skills`. It detects installed or changed skills live; restart only when the new skill does not appear. `AGENTS.md` discovery occurs once per run, so start a new run after Guide-managed instruction changes.
- Claude Code live-watches `~/.claude/skills` and project `.claude/skills`; restart only when the top-level skills directory did not exist at session start. `CLAUDE.md` and its relative `@AGENTS.md` import load at session start, so start a new session after instruction changes.
- Guide rewrites only its exact marked span in `AGENTS.md` and its exact owned `@AGENTS.md` line in `CLAUDE.md`; every surrounding byte, including line endings, trailing spaces, and blank lines, is preserved. Malformed or duplicate markers/imports fail closed.
- Nested instruction files are diagnostic-only unless the user explicitly repeats `--nested-instruction <relative-path>` during `init`, `configure`, or `update`. Selection is limited to existing, non-symlinked nested `AGENTS.md` and `CLAUDE.md` files that discovery already reported. Unselected files are never rewritten.
- `instruction_reload_required` appears only when the approved plan would actually change a root or explicitly selected nested instruction file. An idempotent plan does not request a reload.
- Use the exact `distribution.skillsCli.version` pinned by the catalog. For the current catalog, verify source discovery with `npx --yes skills@1.5.21 add <immutable-skill-url> --list` and installed discovery with `npx --yes skills@1.5.21 ls -g -a <agent> --json`. Offline install and CLI verification commands add npm's `--offline` before the pinned package spec.

## Offline and release integrity

Use only the bundled catalog or an explicitly selected locally cached catalog/index whose digest matches the immutable release metadata. Offline mutation additionally requires an exact verified local/cached Monica source whose complete catalog-managed skill bytes match the target release manifest. Install each selected skill from that source's local `skills/<name>` directory with `npx --offline --yes skills@<pinned-version>` and run any CLI-based discovery verification with the same npm offline flag. Never use a GitHub URL, refresh, fetch, substitute another release, or fall back to a moving branch. A missing or mismatched local source is a blocker and produces no install or verification actions.

`source` consumes `inspect-dependency-source resolve Tairitsua/Monica --ref <exact-ref> --json` only. This resolver operation revalidates an existing local/catalog artifact and is safe in offline mode; never invoke its add, refresh, or fetch workflows while offline. Require a successful exit, `verification_state: verified`, exact provenance, a readable absolute path, and a matching commit. Re-run the cache-only resolution before apply and reject a source path or commit switch even when skill bytes happen to match. Never edit catalog-managed source.

The `source` channel always installs from the exact bound checkout. It never turns the selected commit into a GitHub installation URL.

## Global release constraint

One user-level installation can have only one active Monica release. When project expectation and user state differ, diagnose the conflict and require an explicit global switch or a project upgrade. Never claim simultaneous global multi-version isolation.

## Global install compensation

Before global mutation, snapshot the planned Monica skills and planned file actions into a private transaction directory beside user state. Mark each skill or file before attempting it. On any protected failure, restore only attempted files and skills; compare canonical paths, discovery memberships, CLI-reported provenance, bytes, and portable file modes with the pre-apply inventory. Do not touch unrelated or unattempted skills.

Restore remote provenance only from the prior immutable Monica ref. A local-source reinstall can preserve an existing lock entry only when no binding removal is needed; otherwise fail closed offline rather than contacting the network or losing provenance. Reject pre-existing skills whose only memberships or provenance cannot be represented by the pinned CLI. The recovery contract intentionally excludes per-agent copy-versus-symlink topology because `skills@1.5.21` does not expose it.

Delete the snapshot after a fully committed plan or fully verified compensation. If removal, restoration, verification, or cleanup is incomplete, retain private evidence and surface it in `status` and `doctor`; all later mutations fail with `global_skill_recovery_required` until it is reconciled. A cleanup failure never initiates rollback after an already committed operation. Describe this as compensating behavior because process or machine termination can interrupt either mutation or recovery.

## Contribution safety

Preferences mean:

- `never`: do not prepare remote contributions.
- `prepare`: prepare local drafts and plans without remote mutation.
- `ask`: ask before preparing a contribution path.

All three still require fresh approval before remote Issue/Discussion/PR creation, branch creation, pushing, or draft PR publication. Suspected vulnerabilities always use the private security route regardless of preference.
