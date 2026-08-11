# Toolbox intents

Guide is useful before the user has chosen a repository, profile, source checkout, or development path. Do not turn installation into initialization.

| User intent | First action |
| --- | --- |
| Learn what Monica Guide can do | Run `overview`. Explain capabilities and ask what matters next. |
| Inspect global health | Run `status` or `doctor` without `--workspace`. |
| Locate Monica implementation source | Run `source resolve --repository monica --json`. |
| Locate Monica.Docs source | Run `source resolve --repository docs --json`. |
| Add or replace a source lookup | Preview `source bind`; apply only the approved digest. |
| Start work in a repository | Add `--workspace`, inspect advisory detection, then ask the user to confirm a profile before previewing `init`. |
| Update installed Monica skills | Diagnose first, resolve an immutable release, and preview `update`. |
| Prepare an upstream report | Inspect the contribution preference and route to `$monica-contribution`; remote actions still need current-session approval. |

## Source lookup behavior

Global bindings answer “where can this agent inspect the exact first-party source?” They do not change project dependencies and do not authorize edits. Application users may bind Monica, Monica.Docs, both, or neither.

Return a healthy path even when the checkout is dirty or its commit differs from the current workspace expectation. Include stored and observed commits, ref, provenance, path health, compatibility, and warnings. Refuse only when identity or commit cannot be established, the path is unavailable, or the requested operation requires exact parity.

If no binding exists and an exact ref can be derived, try the cache-only `inspect-dependency-source resolve <repository> --ref <exact-ref> --json` contract. Offer the verified result for binding; do not register, fetch, clone, refresh, or select a default branch automatically.

## Repository setup behavior

Profile detection is a suggestion, not confirmation. Mixed or non-Monica repositories are valid toolbox contexts and require a user decision only when repository setup is requested. `init`, `configure`, and `update` remain preview-first; installing Guide alone must not install their downstream skill closures.
