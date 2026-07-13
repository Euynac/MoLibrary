# Main-Agent Mode

## Initialize the task

Run `init` once with a concise task title. It creates the session and starts or reuses the project dashboard. Report the returned dashboard URL to the user.

```bash
python <skill-dir>/scripts/supervise_subagents.py init --task "<overall task>"
```

Keep the returned `session_root` for every child in this task.

## Spawn and register

1. Spawn a child with a narrow task and hard scope fence. Tell it to wait for the finalized progress-session handoff before starting work.
2. Use the runtime's returned canonical name and identifier to register it immediately. If no separate identifier is exposed, use the stable canonical name for both fields:

```bash
python <skill-dir>/scripts/supervise_subagents.py register \
  --session-root "<session-root>" \
  --agent-name "<canonical-name>" \
  --agent-id "<runtime-id>" \
  --task "<delegated task>"
```

3. Persist the authoritative runtime state:

```bash
python <skill-dir>/scripts/supervise_subagents.py lifecycle \
  --session-root "<session-root>" --agent-name "<canonical-name>" --status running
```

4. After registration succeeds, send the child the exact session root and canonical name. Require `$supervise-subagents` in Sub-Agent mode and an immediate first report.

## Verify bootstrap without blocking

1. Wait through the native runtime for no more than 30 seconds, then inspect:

```bash
python <skill-dir>/scripts/supervise_subagents.py status --session-root "<session-root>" --json
```

2. If no progress event exists and the runtime still says `running`, send one precise bootstrap reminder.
3. Wait at most one more 30-second cycle. If the child still ignored the reporting contract, interrupt and redelegate with a sharper prompt.
4. If an event exists, do not treat later quiet periods as failure by themselves.

## Supervise

- Refresh native lifecycle after every spawn, list, wait, completion, failure, or interruption observation.
- Use native waits of 30 seconds or less, then inspect status and send the user an update when work continues.
- Respond immediately to `blocked` or `needs_input`. Use a message for information and a follow-up task when an idle child must resume work.
- Interrupt only for explicit scope drift, ignored direct correction, native failure, or deliberate abandonment. Do not redelegate solely because events are temporarily quiet.
- Keep child messages factual; never request hidden reasoning or secret data.

## Close

After the native runtime reaches a terminal state, persist it and close the record:

```bash
python <skill-dir>/scripts/supervise_subagents.py close \
  --session-root "<session-root>" --agent-name "<canonical-name>" \
  --reason "Native task completed and result collected."
```

## Delegation snippet

```text
Use $supervise-subagents in Sub-Agent mode.
Your exact session root is "<session-root>" and agent name is "<canonical-name>".
Report in_progress immediately, then report factual checkpoints, blockers, needs_input, and completion.
Do not act as the main orchestrator and do not expose hidden reasoning or secrets.
```
