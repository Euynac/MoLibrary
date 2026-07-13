---
name: supervise-subagents
description: Improve the reliability and output quality of multi-agent coding workflows by giving the main agent structured visibility into delegated subagents. Use when Codex or another agent runtime delegates parallel work and needs authoritative lifecycle tracking, factual child checkpoints, explicit blockers or input requests, bounded recovery, persistent history, or a local dashboard. This skill does not expose hidden reasoning.
---

# Supervise Subagents

Turn fire-and-forget delegation into an observable, recoverable workflow. Native runtime state remains authoritative; structured progress events add the work context needed for supervision.

## Core workflow

1. Read [runtime-adapter.md](references/runtime-adapter.md) and confirm the required capabilities exist.
2. In Main-Agent mode, read [main-agent-mode.md](references/main-agent-mode.md).
3. In Sub-Agent mode, read [sub-agent-mode.md](references/sub-agent-mode.md).
4. Read [dashboard.md](references/dashboard.md) when starting, inspecting, or stopping the UI.
5. Read [script-reference.md](references/script-reference.md) for exact CLI syntax.

## Storage contract

```text
.tmp/<timestamp>-<random>-agent-session/
├── session.json
└── agents/<agent-name>/
    ├── agent.json
    └── events.jsonl
```

- The main agent owns `session.json` and `agent.json` lifecycle fields.
- Each child appends only to its own `events.jsonl`.
- The dashboard reads these files and never controls agents.
- Never expose hidden reasoning, secrets, or raw terminal streams in progress messages.

## Operating rules

- Initialize one shared session for the overall task and reuse it for every child.
- Register a child immediately after the runtime spawns it, then persist native lifecycle changes after every authoritative runtime observation.
- Require a first `in_progress` event promptly and factual events at meaningful checkpoints.
- Keep native waits at 30 seconds or less so the main agent can continue communicating with the user.
- Act immediately on explicit `blocked` and `needs_input` reports. Do not infer lifecycle from message text.
- Close an agent only after the native runtime confirms completion, interruption, or permanent abandonment.
