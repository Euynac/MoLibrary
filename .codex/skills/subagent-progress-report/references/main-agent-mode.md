# Main-Agent Mode

## Setup the shared session root

1. Create one shared session root under the current project `.tmp/`:

```bash
python <skill-dir>/scripts/init_main_agent_session.py
```

2. Keep the returned `<session-root>` and pass it to every sub-agent in the same coordination run.
3. Reuse that one shared root for the whole run. Do not create a second root unless you intentionally start a new supervision session.

## Delegate with a stable folder name

1. Finalize one stable session agent name for folder naming:
   - start with the delegated task label
   - append the sub-agent tool nickname when available
   - prefer `<task-label>--<tool-nickname>`
2. If the tool reveals its nickname only after `spawn_agent` returns, send one immediate follow-up message with the finalized session agent name before the child initializes its folder.
3. Explicitly tell the child to use `$subagent-progress-report` in `Sub-Agent mode`.
4. Require the child to initialize its own folder with:

```bash
python <skill-dir>/scripts/init_subagent_session.py --session-root "<session-root>" --agent-name "<agent-name>"
```

5. If delegation uses `fork_context=true` or equivalent full-context handoff, add a hard scope fence:

```text
You are a specialized subagent. Your task is ONLY to <delegated task>.
Do NOT act as the main orchestrator. You are not responsible for the overall project goal, only this task.
```

6. Require the child to keep its progress-report artifacts inside its child directory. If the delegated task itself requires repository edits, let the child perform those edits normally while still keeping coordination artifacts in the child directory.
7. Require the child to prefer active logging over silence:
   - write the first non-empty log entry immediately
   - log before and after long-running work
   - log failures and next adjustments immediately
   - add heartbeat updates during extended investigation

## Verify bootstrap

1. Run:

```bash
python <skill-dir>/scripts/check_subagent_bootstrap.py --session-root "<session-root>" --agent-name "<agent-name>"
```

2. Interpret the result:
   - `missing`: the child never created its folder within the directory window. Treat it as hallucinating and redelegate to a fresh sub-agent.
   - `directory_only`: the folder exists but the first non-empty log entry never arrived within the log window. Follow up with the same child. Do not redelegate automatically.
   - `bootstrapped`: the folder exists and the child wrote the first non-empty log entry. Continue supervision.
3. Once a child is `bootstrapped`, assume it is healthy until the log, harness state, or explicit drift says otherwise. Do not treat one quiet read as failure.

## Supervise a live child

1. Read one child directly from `<session-root>/<agent-name>/agent.log`, newest entry first.
2. For multiple children, prefer:

```bash
python <skill-dir>/scripts/collect_agent_status.py --session-root "<session-root>"
```

3. Use the latest log entry to classify the child as `completed`, `blocked`, `needs_input`, or `in_progress`.
4. Never edit `agent.log` from the main agent.
5. If the child clearly drifts from scope, interrupt immediately with a precise correction.
6. If the child is quiet after bootstrap, switch to [monitoring-and-recovery.md](monitoring-and-recovery.md). Do not close or redelegate before following that patience flow.
7. If the log explicitly says `blocked` or `needs input`, respond directly instead of waiting through a quiet-period round.
8. Silence is tolerable after bootstrap, but it is not the target behavior. Prefer children that keep emitting short factual progress updates.

## Close and archive

1. Close the sub-agent in the harness when its task is done or permanently abandoned.
2. Archive its directory so future status scans ignore it:

```bash
python <skill-dir>/scripts/mark_subagents_closed.py --session-root "<session-root>" --agent-name "<agent-name>"
```

3. Repeat `--agent-name` to archive multiple sub-agents in one command.

## Delegation Snippet

```text
Use $subagent-progress-report in Sub-Agent mode.
Your finalized session agent name is "<task-label>--<tool-nickname>".
Use the provided session root, initialize your child folder there, keep coordination artifacts there,
write your first non-empty agent.log entry immediately after initialization,
report progress by prepending entries to agent.log after each major phase,
log before and after long-running work, and add heartbeat updates instead of going silent during extended investigation.
```
