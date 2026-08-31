# Safety, discovery, and offline behavior

## Discovery and instruction reloads

- Skill projections are ordinary directories. The default is project-local (the workspace's `skillTargets`, typically `.agents/skills`, which Codex and Agent Skills hosts scan up the repository tree); global catalogs at `~/.agents/skills` and `~/.claude/skills` are explicit opt-ins selected per environment. The engine installs with whole-directory staging swaps and never merges into existing skill directories.
- Codex scans repository `.agents/skills` directories up to the repository root and user `~/.agents/skills`; it detects installed or changed skills live. `AGENTS.md` discovery occurs once per run, so start a new run after guide-managed instruction changes.
- Claude Code live-watches `~/.claude/skills`; restart only when the top-level skills directory did not exist at session start. `CLAUDE.md` and its relative `@AGENTS.md` import load at session start, so start a new session after instruction changes.
- The engine rewrites only its exact marked span in `AGENTS.md` and its exact owned `@AGENTS.md` line in `CLAUDE.md`; every surrounding byte, including line endings and blank lines, is preserved. Malformed or duplicate markers or imports fail closed.
- Nested `AGENTS.md`/`CLAUDE.md` files are diagnosed and reported but never rewritten by the engine.
- The reload warning appears only when the approved plan would actually change an instruction file; an idempotent plan requests no reload.

## Offline and release integrity

Offline operation uses the installed bundle, the local ledger, and local bindings only. The engine performs no network traffic except the explicit update check on the wizard's Update page (GitHub over HTTPS, digest-verified against the release `SHA256SUMS`). Never replace an unavailable immutable release with a branch or another version.

`source bind` may consume `inspect-dependency-source resolve <repository> --ref <exact-ref> --json` for either first-party repository. The resolver operation revalidates an existing cached artifact and is safe offline; never invoke its add, refresh, or fetch workflows. Require a successful exit, `verification_state: verified`, exact provenance, a readable absolute path, and the matching commit. Never edit catalog-managed source.

Global bindings are lookup locators, not authorization. Observe checkout dirtiness, path health, and identity on every `source resolve`. Return a usable dirty or moved binding with warnings; operations that need exact parity decide for themselves whether to reject it.

## Global release constraint

One user-level installation holds one active release per product, recorded in the unified ledger; every workspace installs from that same release, so projects never pin divergent skill versions. When a repository's expectation and the installed release differ, diagnose the conflict and require an explicit reconfigure from one complete bundle; never mix files across releases. Retired skill aliases still present in a target root are diagnosed by `doctor` (`stale-alias` checks) with the canonical replacement; directories no release owns are reported as unmanaged.

Apply safety is structural: every mutation is previewed with a digest, applied under the engine mutation lock through staging directories, and verified against the catalog afterwards. A failed apply leaves installed skills untouched — staged trees are swapped in only after they are complete — and stale staging directories never block the next run.

## Contribution safety

Route contribution preparation to `$monica-contribution`. Local classification and drafting need no further approval; every remote mutation (Issue/Discussion/PR creation, branch creation, pushing, draft PR publication) requires fresh current-session approval. Suspected vulnerabilities always use the private security route.
