# Toolbox intents

Guide is useful before the user has chosen a repository, profile, source checkout, or development path. Do not turn installation into initialization.

| User intent | First action |
| --- | --- |
| Learn what Monica Guide can do | Run `overview`. Explain capabilities and ask what matters next. |
| Inspect global health | Run `status` or `doctor` without `--workspace`. |
| Locate Monica implementation source | Run `source resolve --repository monica --json`. |
| Locate Monica.Docs source | Run `source resolve --repository docs --json`. |
| Add or replace a source lookup | Preview `source bind`; apply only the approved digest. |
| Start work in a repository | Run `status --workspace <path>` (or `workspaces`), explain the advisory detection, and ask the user to confirm a profile before previewing `init`; then `configure --workspace` installs its skills. |
| Install or update skills for a project | Preview `configure --workspace <path>` (the workspace's confirmed profile picks the closure), then apply the approved digest. Updating means configuring a newer release bundle — never mixing files across releases. |
| Install skills machine-wide | Only on explicit request: preview `configure --environment <env> --target shared\|claude`, then apply. |
| Prepare an upstream report | Route to `$monica-contribution`; remote actions still need current-session approval. |

## Source lookup behavior

Global bindings answer "where can this agent inspect the exact first-party source?" They do not change project dependencies and do not authorize edits. Application users may bind Monica, Monica.Docs, both, or neither.

Return a healthy path even when the checkout is dirty or its observed commit moved; the binding check carries the stored and observed commits, ref, provenance, and health warnings. Refuse only when identity or an exact commit cannot be established or the path is gone. If no binding exists and an exact ref is known, `source bind --source-ref` can resolve through the pinned `inspect-dependency-source` cache — never clone, fetch, or select a default branch automatically.

## Repository setup behavior

Profile detection is a suggestion, not confirmation. Mixed or non-Monica repositories are valid toolbox contexts and require a user decision only when repository setup is requested. `init` and `configure` are separate: initializing a workspace writes repository facts only, while `configure --profile <name>` installs that profile's skill closure into agent targets.
