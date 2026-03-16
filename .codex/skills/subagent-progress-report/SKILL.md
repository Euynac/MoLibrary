---
name: subagent-progress-report
description: Coordinate progress reporting between a main agent and spawned sub-agents using a shared session root, per-agent folders, prepended `agent.log` files, bootstrap verification, active sub-agent logging, patient post-bootstrap recovery, status scans, and closed-session archiving. Use when the runtime supports sub-agent features and the main agent must delegate with scoped ownership, require sub-agents to log frequently instead of staying silent, monitor progress without editing child logs, wait through temporary quiet periods only as a fallback, or summarize updates from `.tmp/YYYYMMDD-HHMMSS-agent-session/agent-name/agent.log`.
---

# Subagent Progress Report

Use this skill only when the runtime exposes sub-agent management capabilities such as `spawn_agent`, `send_input`, or `wait`. If the environment cannot create or supervise sub-agents, stop and report that limitation.

Keep one shared session root under the current project root:

```text
.tmp/<timestamp>-agent-session/
```

Each sub-agent owns one child directory inside that root:

```text
.tmp/<timestamp>-agent-session/<agent-name>/
```

Each sub-agent writes only its own `agent.log` and coordination artifacts inside its own child directory.

Prefer active logging over silence. Quiet-period recovery exists as a fallback when a healthy child temporarily stops logging, not as the normal operating style for sub-agents.

## Load Only What You Need

- Main-agent setup, delegation, bootstrap, supervision, and archiving: read [references/main-agent-mode.md](references/main-agent-mode.md).
- Main-agent quiet-period patience and redelegation recovery: read [references/monitoring-and-recovery.md](references/monitoring-and-recovery.md).
- Sub-agent initialization, artifact ownership, and logging rules: read [references/sub-agent-mode.md](references/sub-agent-mode.md).
- Exact CLI syntax for bundled scripts: read [references/script-reference.md](references/script-reference.md).

## Quick Start

Main agent:

1. In `Main-Agent mode`, confirm sub-agent tooling is available, then create a shared session root with `python <skill-dir>/scripts/init_main_agent_session.py`.
2. Delegate with a stable `<task-label>--<tool-nickname>` agent name and require `$subagent-progress-report` in `Sub-Agent mode`.
3. Run `python <skill-dir>/scripts/check_subagent_bootstrap.py --session-root "<session-root>" --agent-name "<agent-name>"`.
4. Instruct the child to prefer many short factual log entries and to avoid long silent stretches.
5. If the child complains about missing sub-agent-management tools or starts acting like the main orchestrator, treat that as role confusion and redelegate with a sharper scope fence.
6. After bootstrap succeeds, treat silence as a temporary quiet period only until the recovery flow says otherwise.

Sub-agent:

1. Reuse the main-agent-provided `<session-root>` and exact `<agent-name>`. Do not rerun the main-agent tool-availability check.
2. Initialize `<session-root>/<agent-name>/` with `python <skill-dir>/scripts/init_subagent_session.py --session-root "<session-root>" --agent-name "<agent-name>"`.
3. Write the first non-empty `agent.log` entry immediately after initialization.
4. Keep logging through the task at every meaningful step, especially before and after long-running work, after discoveries, and after failures.
5. If you are investigating for more than about `30-60s` without an external signal, write a short heartbeat log entry with your current hypothesis and next step.
6. Finish with `completed`, `blocked`, or `needs input` in the final log entry.

## Logging bias

Default to too many short factual log entries rather than too few.

- Log at task start, on each phase change, before long-running commands, after commands finish, after each failure, after each approach change, and at completion.
- Keep messages compact and specific: current action, finding, failure, or next step.
- When in doubt, write a new entry. Silence should be rare and short.
- Treat the main-agent patience flow as a recovery tool, not permission to go dark.

## Key Rules

- Let only the sub-agent write `agent.log`; the main agent only reads it.
- Read newest log entries first because `agent.log` is prepended, not appended.
- Treat explicit `blocked` or `needs input` log entries as actionable state, not silence.
- Run the bootstrap check after each new delegation before trusting the child.
- In `Sub-Agent mode`, prefer active progress logging over long quiet stretches.
- Archive closed sub-agents with `scripts/mark_subagents_closed.py` so later scans ignore them.
