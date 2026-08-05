# CLI and state contract

Run the dependency-free Node.js entry point by absolute path:

```bash
node "<skill-dir>/scripts/monica-guide.mjs" <intent> --workspace "<path>" [options]
```

## Intents

| Intent | Behavior |
| --- | --- |
| `status` | Show detected repository, profile, framework version, configured release, global release, source binding, and per-skill installed/target revisions and change states. |
| `doctor` | Emit actionable checks; add `--json` for the stable machine envelope. |
| `init` | Preview or initialize project configuration, global profile skills, managed instructions, Claude import, and required source binding. |
| `update` | Resolve the configured channel/version, reinstall only new/changed/unhealthy Monica skills, verify the complete managed set, and atomically switch the one global release. |
| `configure` | Change profile, channel, capabilities, agent targets, and managed instructions. |
| `source` | Bind an exact verified source path or an exact cached `inspect-dependency-source resolve --json` result. |
| `contribute` | Set or inspect the local `never`, `prepare`, or `ask` contribution preference and route to `$monica-contribution`. |
| `forget` | Remove repository Guide configuration, Guide-owned instruction text, and this workspace's user preferences without uninstalling global skills. |

Common options are `--workspace`, `--profile`, `--channel`, repeated `--capability`, repeated `--agent`, `--catalog`, `--index`, `--offline`, and `--json`. `source` accepts `--source-path`, `--source-ref`, and `--source-access`. Use `--switch-global` only after the user explicitly chooses to replace the one active global Monica release.

`update` alone is the safe default. It reinstalls every managed skill when agent targets changed or any stored skill metadata is unknown; otherwise it reinstalls only new skills, changed digests, and skills whose installed bytes, discovery, or target-agent membership drifted. It always verifies every managed skill before writing release state. A successful verification can advance unchanged skill records without reinstalling their bytes.

`update` accepts repeatable `--skill <name>` for an explicitly targeted update. The Guide expands required Monica dependencies and reinstalls unhealthy or changed members of that closure. It blocks instead of switching the global release when another managed skill is new, unknown, has a changed target digest, is missing, is tampered, or cannot be verified for every target agent. Run a full update in that case. `--skill` is invalid for every other intent.

`init`, `configure`, and `update` also accept repeatable `--nested-instruction <relative-path>`. Each value must name an existing, diagnosed nested `AGENTS.md` or `CLAUDE.md` beneath the selected repository. Paths must stay inside the repository, may not contain symlinks, and are never inferred from discovery alone. A selected nested `CLAUDE.md` requires the `claude-code` target and an existing sibling `AGENTS.md`. All unselected nested instruction files remain byte-for-byte unchanged.

`framework-contributor` and `docs-contributor` require `--workspace` to identify the canonical Git root, not a subdirectory. The canonical `Tairitsua/Monica` or `Tairitsua/Monica.Docs` identity may be supplied by `origin` or by `upstream` when `origin` is a fork. `status` and `doctor` enforce the same identity, root, and writability contract as mutation planning. Framework checkout detection may resolve its exact version from the root `Directory.Build.props` `<Version>` when no consumer reference establishes a version.

`--offline` permits only verified cache/local reads. `init`, `update`, and `configure` require both cached immutable release contracts and an exact verified local/cached Monica checkout whose managed skill manifest matches those contracts. Their generated installation and verification commands use the catalog-pinned `skills` CLI with npm's `--offline` flag and local `skills/<name>` inputs. `source` channel installation follows the same local-source rule even without `--offline`.

Every mutating intent is a dry run unless both `--apply` and `--plan-digest <digest>` are present. The CLI recomputes the plan from current workspace and user state and rejects a stale digest. Applied file changes use an exclusive state lock and atomic same-directory replacement.

Plans that install or verify global skills include a `protect-global-skills` action. Apply snapshots the catalog-selected Monica skills and every planned file before invoking the pinned `skills` CLI. It marks each skill or file before attempting that action and compensates only attempted work. The observable recovery contract verifies the canonical skill path, bytes, portable file modes, full discovered agent membership, CLI-reported provenance, and planned file bytes/modes. The pinned CLI does not expose per-agent copy-versus-symlink topology, so the Guide does not claim to restore that hidden topology.

If any protected install, verification, cache, state, project, or instruction action fails, the Guide restores planned files and re-adds pre-existing skills through a supported pinned-CLI source. Successful compensation returns the original failure with `details.transaction.status` set to `compensated`. Incomplete compensation fails with `global_skill_rollback_incomplete` and retains a private recovery directory beside user state. A cleanup failure after an otherwise committed or compensated operation retains evidence without triggering a second rollback. `status` and `doctor` surface retained transactions, and another mutation fails with `global_skill_recovery_required` until the evidence is reviewed and reconciled. This is verified best-effort compensation, not an atomic guarantee against process or machine termination.

## Repository state

`.monica/guide.json` is shareable and contains only:

- schema version;
- confirmed profile and channel;
- selected capabilities and agent targets;
- expected immutable catalog release;
- managed instruction block version.

It never contains user paths, timestamps, or contribution preferences. Guide writes it with repository-readable mode `0644`.

## User state

`state.json` uses the platform data directory (`XDG_DATA_HOME`, macOS Application Support, or `LOCALAPPDATA`) unless `MONICA_GUIDE_STATE` or `--state` explicitly overrides it. Schema v3 contains one active global release, agent targets, exact source bindings, per-workspace preferences, contribution preferences, and a separate timestamped observations collection. `managedSkills` maps every name to `{ revision, digest, lastChangedIn }`. Tagged releases use a positive catalog revision, exact digest, and immutable change-origin tag. Source installs use `null`, an exact digest, and `null`, and display as `source@<commit>`. Migrated v2 names use an all-null record and require a subsequent full verified update before they can be treated as current. Mixed or partial tuples fail closed. User state and verified release caches are written with private mode `0600`.

`status --json` and `doctor --json` are safe for automation but can contain local paths. Keep their payloads local. Status emits each skill's installed record, target record, and `new`, `unknown`, `content-changed`, `metadata-changed`, or `unchanged` state. Doctor reports unknown metadata as an error and pending changes or repairable drift as actionable diagnostics. Both validate the managed `AGENTS.md` body and configured block version; when Claude Code is selected they also require exactly one root `CLAUDE.md` import of `@AGENTS.md`.
