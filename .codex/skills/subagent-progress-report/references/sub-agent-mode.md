# Sub-Agent Mode

## Accept the main-agent contract

1. Expect the main agent to provide one shared `<session-root>` and one finalized `<agent-name>`.
2. Use exactly the main-agent-provided `<agent-name>`. Do not invent or rename it, even if the harness also shows a generated nickname.
3. Reuse the provided session root. Do not create a second root.

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
   - findings that may change the approach
   - blockers or questions for the main agent
   - completion
3. Keep messages short and factual.
4. If the main agent interrupts or nudges you, acknowledge it by writing a fresh log entry before resuming work.
5. Before finishing, write a final entry that explicitly states `completed`, `blocked`, or `needs input`.

## Ownership rule

1. Only the sub-agent writes `agent.log`.
2. The main agent reads `agent.log` but must never edit it.
3. Use fresh log entries to prove continued progress. A long silent period may trigger the main agent's patience-and-recovery flow.
