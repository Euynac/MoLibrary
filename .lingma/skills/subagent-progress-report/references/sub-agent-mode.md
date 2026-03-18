# Sub-Agent Mode

## Accept the main-agent contract

1. Expect the main agent to provide one shared `<session-root>` and one finalized `<agent-name>`.
2. Use exactly the main-agent-provided `<agent-name>`. Do not invent or rename it, even if the harness also shows a generated nickname.
3. Reuse the provided session root. Do not create a second root.
4. Do not rerun the main-agent tool-availability check. In `Sub-Agent mode`, lacking direct access to `spawn_agent`, `send_input`, or `wait` is normal unless your delegated task explicitly requires spawning child agents.

## Initialize immediately

1. Create or reuse your child directory:

```bash
python <skill-dir>/scripts/init_subagent_session.py --session-root "<session-root>" --agent-name "<agent-name>"
```

2. The script creates or confirms:
   - `<session-root>/<agent-name>/`
   - `<session-root>/<agent-name>/agent.log`
3. Write the first non-empty log entry immediately after initialization. Main-agent bootstrap depends on it.

```bash
python <skill-dir>/scripts/write_agent_log.py --session-dir "<session-dir>" --message "Started <task>."
```

Do not wait until you have a result. Bootstrap requires proof of life early.

## Own the child directory

1. Keep progress-report artifacts, notes, and temporary outputs for the delegated task inside your child directory.
2. If the delegated task itself requires repository edits, perform those edits normally, but keep coordination artifacts in the child directory.
3. Never write to another sub-agent's directory.

## Log with clear phase boundaries

1. Prepend updates with:

```bash
python <skill-dir>/scripts/write_agent_log.py --session-dir "<session-dir>" --message "<message>"
```

2. Log these events:
   - task start
   - major phase changes
   - before a long-running command, browser step, or wait
   - after that command, browser step, or wait completes
   - findings that may change the approach
   - failed attempts and the next adjustment
   - blockers or questions for the main agent
   - short heartbeat updates during longer investigation
   - completion
3. Keep messages short and factual.
4. Prefer over-logging to silence. If you are thinking, searching, or debugging for more than about `30-60s` without a concrete tool result, write a heartbeat log entry with:
   - current hypothesis
   - current or last checked artifact
   - next action
5. If the main agent interrupts or nudges you, acknowledge it by writing a fresh log entry before resuming work.
6. Before finishing, write a final entry that explicitly states `completed`, `blocked`, or `needs input`.

## Logging pattern

Use a bias toward frequent short updates:

1. Log when you start.
2. Log before you run something that may take time.
3. Log immediately after it returns with the result or failure.
4. Log when you change direction.
5. Log a heartbeat during extended analysis.
6. Log the final state.

Quiet periods should be exceptional. The main agent can tolerate temporary silence, but you should not rely on that tolerance as your normal workflow.

## Ownership rule

1. Only the sub-agent writes `agent.log`.
2. The main agent reads `agent.log` but must never edit it.
3. Use fresh log entries to prove continued progress. A long silent period may trigger the main agent's patience-and-recovery flow, so prefer visible heartbeat updates before it gets to that point.
