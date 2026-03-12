---
name: subagent-progress-report
description: Coordinate progress reporting between a main agent and spawned sub-agents using a main-agent-owned session folder, per-agent artifact folders, prepend-only log files, status scans, and closed-session archiving. Use when the runtime supports sub-agent features and the main agent needs to supervise delegated work, keep sub-agent artifacts isolated, enforce sub-agent scope when delegating with forked context, read status without interrupting the worker, archive closed sub-agents, or summarize updates from `.tmp/YYYYMMDD-HHMMSS-agent-session/agent-name/agent.log`.
---

# Subagent Progress Report

Use this skill in two modes: `Main-Agent mode` when delegating work and `Sub-Agent mode` when executing delegated work.

## Mandatory Check

Before any setup, confirm the current runtime exposes sub-agent management capabilities.

- Continue only if the environment provides sub-agent tooling such as `spawn_agent` and a way to send input or wait for completion.
- If no sub-agent feature is available, stop immediately and tell the user that the current environment does not support sub-agents and should be checked.

## Directory Model

The main agent owns one shared session directory under the current project root:

```text
.tmp/<timestamp>-agent-session/
```

Each sub-agent owns one child directory inside that session:

```text
.tmp/<timestamp>-agent-session/<agent-name>/
```

Each sub-agent writes only its own `agent.log` and artifacts inside its own child directory.

## Main-Agent Mode

1. Start by creating the shared session directory under the current project root:

```bash
python <skill-dir>/scripts/init_main_agent_session.py
```

2. Keep the returned `<session-root>` and pass it to every sub-agent.
3. When spawning a sub-agent, explicitly tell it to use `$subagent-progress-report` in `Sub-Agent mode`.
4. Finalize one stable session agent name for folder naming:
   - start with the delegated task label
   - append the sub-agent tool's generated nickname when available
   - prefer a format like `<task-label>--<tool-nickname>`
   - if the tool reveals its nickname only after `spawn_agent` returns, immediately send one follow-up message with the finalized session agent name before the sub-agent initializes its folder
5. If delegation uses `fork_context=true` or any equivalent full-context handoff, add an explicit scope fence to the delegated prompt. Use wording equivalent to:

```text
You are a specialized subagent (for example, "Code Reviewer"). Your task is ONLY to <delegated task>.
Do NOT act as the main orchestrator. You are not responsible for the overall project goal, only this task.
```

6. Tell the sub-agent to initialize its own child directory inside the shared session directory with that finalized session agent name:

```bash
python <skill-dir>/scripts/init_subagent_session.py --session-root "<session-root>" --agent-name "<agent-name>"
```

7. Immediately after delegation, verify that the sub-agent actually entered `Sub-Agent mode`:

```bash
python <skill-dir>/scripts/check_subagent_bootstrap.py --session-root "<session-root>" --agent-name "<agent-name>" --timeout-seconds 60
```

8. If the bootstrap check times out with `missing`, treat that sub-agent as hallucinating: it did not realize it was a sub-agent, did not create its dedicated folder, and did not provide a log. Stop or discard that sub-agent and delegate the task again to a fresh sub-agent.
9. Require the sub-agent to keep all generated files for that delegated task inside the returned child directory.
10. Require the sub-agent to log:
   - task start
   - phase completions
   - discoveries that may change the plan
   - blockers or questions for the main agent
   - final outcome
11. For one session, read progress from `<session-root>/<agent-name>/agent.log`. Read newest entries first because the log is prepended, not appended to the end.
12. For multi-agent supervision, prefer the summary script:

```bash
python <skill-dir>/scripts/collect_agent_status.py --session-root "<session-root>"
```

13. Use the summary output to identify:
    - each sub-agent folder under the current session
    - the latest logged message
    - whether the session looks `completed`, `blocked`, `needs_input`, or `in_progress`
14. `collect_agent_status.py` ignores directories prefixed with `(Closed)` by default. Pass `--include-closed` only when archived agents still matter for the current inspection.
15. After closing a sub-agent in the harness, archive its directory so future status scans ignore it:

```bash
python <skill-dir>/scripts/mark_subagents_closed.py --session-root "<session-root>" --agent-name "<agent-name>"
```

Repeat `--agent-name` to archive multiple sub-agents in one command.
16. Read-only rule: the main agent may read `agent.log` and summary output but must never edit `agent.log`.
17. If the logged direction drifts, interrupt the sub-agent and provide a precise correction.

## Sub-Agent Mode

1. Expect the main agent to provide a shared `<session-root>` and one finalized `<agent-name>`.
2. Use exactly the main-agent-provided `<agent-name>`. Do not invent a different folder name even if the harness also shows a generated nickname.
3. Start by creating or reusing your child directory:

```bash
python scripts/init_subagent_session.py --session-root "<session-root>" --agent-name "<agent-name>"
```

4. The script creates or confirms:
   - `<session-root>/<agent-name>/`
   - `<session-root>/<agent-name>/agent.log`
5. Keep every generated artifact for this delegated task inside that child directory.
6. Whenever you need to notify the main agent, prepend a new entry to `agent.log`:

```bash
python scripts/write_agent_log.py --session-dir "<session-dir>" --message "Started cleanup of bridge processes."
```

7. Use log entries for:
   - task start
   - major phase changes
   - findings that may affect the approach
   - blockers
   - completion
8. Log format is fixed:
   - `[YYYY-MM-DD HH:MM:SS +08:00] message`
9. Only the sub-agent writes `agent.log`. The main agent only reads it.
10. Before finishing, write a final entry that clearly states `completed`, `blocked`, or `needs input`.

## Quick Start

Example setup for the main agent:

```text
Use $subagent-progress-report in Main-Agent mode.
Create one shared session root under the current project directory's .tmp for this coordination run.
Pass that session root to every sub-agent and require them to log progress in their own child folder.
If you delegate with forked context, add a scope fence that says the sub-agent is ONLY responsible for the delegated task and must not act as the main orchestrator.
```

Example delegation instruction for one sub-agent:

```text
Use $subagent-progress-report in Sub-Agent mode.
Your finalized session agent name is "BridgeVerifier--<tool-nickname>".
Use the provided session root, initialize your child folder there, keep all artifacts there,
and report progress by prepending entries to agent.log after each major phase.
```

Example bootstrap check for the main agent:

```bash
python scripts/check_subagent_bootstrap.py --session-root "<session-root>" --agent-name "BridgeVerifier--cedar" --timeout-seconds 60 --json
```

Example status scan for the main agent:

```bash
python scripts/collect_agent_status.py --json
```

Example batch close after the main agent closes sub-agents in the harness:

```bash
python scripts/mark_subagents_closed.py --session-root "<session-root>" --agent-name "BridgeVerifier--cedar" --agent-name "CodeReviewer--lark" --json
```

## Scripts

### `scripts/init_main_agent_session.py`

Locate the current project root, create `.tmp/<timestamp>-agent-session` there, and print a JSON object with the session root path.

### `scripts/init_subagent_session.py`

Create or reuse `<session-root>/<agent-name>` and print a JSON object with the session and log paths. Reject session roots outside the current project directory's `.tmp`.

### `scripts/write_agent_log.py`

Prepend a timestamped entry to `agent.log`. Use this script instead of manual editing so the newest update is always first.

### `scripts/check_subagent_bootstrap.py`

Wait for a specific sub-agent to create its dedicated folder and first log entry. If the timeout ends with no folder and no log entry, classify the sub-agent as `missing` so the main agent can discard it and redelegate the task.

### `scripts/collect_agent_status.py`

Scan one session root, or the newest session under the current project directory's `.tmp`, read the newest entry from each child `agent.log`, classify the session state, and print a main-agent-friendly summary. Ignore `(Closed)` directories by default. Use `--include-closed` when archived agents still matter. Use `--json` when another tool needs structured output.

### `scripts/mark_subagents_closed.py`

Rename one or more `<session-root>/<agent-name>` directories to `(Closed)<agent-name>`. This archives already-closed sub-agents so `collect_agent_status.py` ignores them during future scans.

## Notes

- Keep agent names filesystem-safe and stable.
- Prefer agent names that combine a task label and the tool-generated nickname when one exists.
- Prefer short, factual status messages.
- Reuse the main-agent-provided session root instead of creating a second root.
- Use explicit keywords in final log messages when possible: `completed`, `blocked`, or `needs input`.
- Run the bootstrap check after each new delegation before trusting the sub-agent.
- After closing a sub-agent in the harness, archive its folder with `mark_subagents_closed.py`.
