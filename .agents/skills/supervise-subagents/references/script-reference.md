# CLI Reference

Run every command through:

```bash
python <skill-dir>/scripts/supervise_subagents.py <command>
```

## Main-agent commands

```text
init --task TEXT [--base-dir PATH] [--no-dashboard]
register --session-root PATH --agent-name NAME --agent-id ID --task TEXT
lifecycle --session-root PATH --agent-name NAME --status STATUS
status [--session-root PATH] [--base-dir PATH] [--agent-name NAME ...] [--json]
close --session-root PATH --agent-name NAME --reason TEXT
dashboard start [--base-dir PATH] [--host 127.0.0.1] [--port PORT]
dashboard status [--base-dir PATH]
dashboard stop [--base-dir PATH]
```

Native statuses are `pending`, `running`, `idle`, `completed`, `failed`, `interrupted`, and `unknown`. A terminal native state cannot transition back to a non-terminal state.

## Child command

```text
report --session-root PATH --agent-name NAME \
  --state in_progress|blocked|needs_input|completed --message TEXT
```

All mutation commands emit JSON. `status` emits compact text unless `--json` is present. Commands reject invalid paths, unsupported states, and writes after terminal closure.
