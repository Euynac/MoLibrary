# Script Reference

Use `<skill-dir>` below as the absolute path to this skill directory.

## Session creation

- `init_main_agent_session.py`: create `.tmp/<timestamp>-agent-session` under the current project and print JSON with `session_root`.

```bash
python <skill-dir>/scripts/init_main_agent_session.py
```

- `init_subagent_session.py`: create or reuse `<session-root>/<agent-name>/` and `agent.log`. Reject roots outside the current project `.tmp/`.

```bash
python <skill-dir>/scripts/init_subagent_session.py --session-root "<session-root>" --agent-name "<agent-name>"
```

## Logging

- `write_agent_log.py`: prepend a timestamped entry to `<session-dir>/agent.log`.

```bash
python <skill-dir>/scripts/write_agent_log.py --session-dir "<session-dir>" --message "Started <task>."
```

## Bootstrap and quiet-period supervision

- `check_subagent_bootstrap.py`: wait for the child directory, then wait for the first non-empty log entry.

```bash
python <skill-dir>/scripts/check_subagent_bootstrap.py --session-root "<session-root>" --agent-name "<agent-name>" --directory-timeout-seconds 60 --log-timeout-seconds 180 --json
```

- `wait_for_agent_log_update.py`: after bootstrap, wait for a new prepended log entry with exponential backoff. Use `quiet-wait` before the first interrupt and `post-nudge` after the interrupt.

```bash
python <skill-dir>/scripts/wait_for_agent_log_update.py --session-root "<session-root>" --agent-name "<agent-name>" --phase quiet-wait --attempts 3 --initial-wait-seconds 30 --backoff-factor 2 --max-total-wait-seconds 300 --json
```

## Status scans and archiving

- `collect_agent_status.py`: summarize the newest entry from each child directory under one session root, or under the newest session if no root is provided.

```bash
python <skill-dir>/scripts/collect_agent_status.py --session-root "<session-root>" --json
```

- `mark_subagents_closed.py`: archive one or more closed child directories by renaming them with the `(Closed)` prefix so later scans ignore them.

```bash
python <skill-dir>/scripts/mark_subagents_closed.py --session-root "<session-root>" --agent-name "<agent-name>" --json
```
