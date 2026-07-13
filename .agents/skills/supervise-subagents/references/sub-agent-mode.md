# Sub-Agent Mode

## Accept the contract

- Use exactly the session root and canonical agent name supplied by the main agent.
- Do not create another session, rename yourself, or act as the main orchestrator.
- You do not need access to native spawn, list, wait, or interrupt tools.

## Report immediately

Write the first event before investigation or implementation:

```bash
python <skill-dir>/scripts/supervise_subagents.py report \
  --session-root "<session-root>" --agent-name "<agent-name>" \
  --state in_progress --message "Started <delegated task>."
```

## Report meaningful checkpoints

Use the same command for:

- major phase changes
- before and after long-running commands or browser work
- discoveries that change the approach
- failures and the next adjustment
- a concrete blocker or question
- final completion

Keep messages short and factual. Do not include chain-of-thought, credentials, private data, or raw terminal dumps. A blocking tool call can prevent timed heartbeats; report before it starts and immediately after control returns rather than interrupting useful work solely to satisfy a timer.

Use explicit states:

- `in_progress`: work can continue without main-agent action
- `blocked`: progress cannot continue without an external change
- `needs_input`: the main agent or user must answer a concrete question
- `completed`: the delegated outcome is ready for collection

Before returning your final response, always write one final event whose state matches the outcome.
