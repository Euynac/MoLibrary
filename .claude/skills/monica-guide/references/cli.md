# CLI and state contract

Run the dependency-free Node.js entry point by absolute path:

```bash
node "<skill-dir>/scripts/monica-guide.mjs" [intent] [options]
```

No intent is equivalent to `overview`.

## Read-only intents

| Intent | Behavior |
| --- | --- |
| `overview` | Explain toolbox capabilities, active/bundled release, agent targets, global first-party source bindings, and suggested next actions. It never requires a workspace. |
| `status` | Show global skill and binding state. Add `--workspace <path>` for advisory repository detection, configured expectations, and compatibility. |
| `doctor` | Diagnose global health. Add `--workspace <path>` for repository checks; add `--json` for the stable machine envelope. |
| `source list` | List the global Monica and Monica.Docs binding summaries. |
| `source resolve --repository monica\|docs` | Validate and return the selected global binding. Add `--workspace <path>` to compare it with that repository’s expected framework or checkout commit. |

`source resolve --json` returns canonical repository identity, stored and observed commits, exact ref, provenance, resolution kind, absolute path, path/dirty health, compatibility, and warnings. A usable dirty or mismatched lookup remains visible with warnings; exact-parity operations may reject it.

## Mutating intents

| Intent | Behavior |
| --- | --- |
| `init` | Initialize selected project configuration, profile skill closure, managed instructions, and conditional Claude import. Requires `--workspace`, immutable release selection, profile confirmation, and at least one validated `--agent`. |
| `update` | Resolve the configured release, reinstall only selected new/changed/unhealthy Monica skills, verify the complete managed set, and atomically switch the one global release. |
| `configure` | Change profile, channel, capabilities, agent targets, or managed instructions for an initialized workspace. |
| `source bind --repository monica\|docs` | Add or replace one global verified binding from `--source-path` or an exact `--source-ref` resolved through `--source-resolver`. |
| `source unbind --repository monica\|docs` | Remove that repository’s global binding or discard unresolved migration candidates. |
| `contribute` | Set or inspect the local `never`, `prepare`, or `ask` preference and route to `$monica-contribution`. |
| `forget` | Remove repository Guide configuration, Guide-owned instruction text, and that workspace’s preferences. It never removes global source bindings or unrelated skills. |

Every mutating intent is a dry run unless both `--apply` and `--plan-digest <digest>` are present. Apply rebuilds the plan from current state, validates local preconditions under an exclusive state lock, and rejects drift. Do not hold the lock while fetching network release data.

Common options include `--workspace`, `--profile`, `--channel`, repeated `--capability`, repeated `--agent`, `--catalog`, `--index`, `--offline`, and `--json`. Agent values are pinned `npx skills` target identifiers; verify each independently through `skills ls -g -a <target> --json`. Only `claude-code` enables the root `CLAUDE.md` import behavior.

`source bind` accepts either `--source-path <absolute-path> [--source-ref <exact-ref>]` or `--source-ref <exact-ref> [--source-resolver <path>]`. It never stores access, dirtiness, verification status, or compatibility observations. `source list` and `source resolve` are always read-only.

Exit codes are `0` for success, `1` for warning-only diagnostics, `3` for domain blockers or unhealthy results, and `2` for invocation, operational, or internal failures.

## Repository state

`.monica/guide.json` is shareable and contains only the confirmed profile/channel/capabilities, expected immutable catalog release, managed instruction version, and the minimal ownership flag for a Guide-managed Claude import. Agent targets remain user-global and are never written to repository state. The file never contains local paths, timestamps, source bindings, or contribution preferences.

## User state

Private state schema v4 contains one active global skill release, managed skill metadata, agent targets, a repository-keyed global `sourceBindings` map, workspace and contribution preferences, migration candidates, cached release contracts, and timestamped observations.

Binding keys are exactly `Tairitsua/Monica` and `Tairitsua/Monica.Docs`. Persist only repository, ref, commit, provenance, resolution kind, and source path. Migrating v3 promotes identical legacy Monica bindings, retains conflicting values as explicit candidates, and blocks mutations until `source bind` chooses a replacement or `source unbind` discards them.

Any Guide output that includes source bindings or local paths—including `overview`, `source list`, `source resolve`, `status`, and `doctor`—can expose private absolute paths. Keep it local and do not commit or share it by default. State locking uses a random owner token; never delete or replace a lock that the current process does not own. Diagnose stale/dead owners and require explicit recovery instead of automatic takeover.
