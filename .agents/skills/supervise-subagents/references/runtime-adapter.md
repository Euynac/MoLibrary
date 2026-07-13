# Runtime Adapter

Use native runtime lifecycle as the source of truth. The file protocol remains portable; only this capability mapping is Codex-specific.

| Operation | Codex tool | Required behavior |
|---|---|---|
| Create a child | `spawn_agent` | Return a canonical task name and, when available, a separate runtime id. |
| Inspect lifecycle | `list_agents` | Report running, idle, terminal, or failure state. |
| Wait for activity | `wait_agent` | Wait no more than 30 seconds per supervision cycle. |
| Deliver information | `send_message` | Send context without forcing an idle child to start a turn. |
| Resume or assign work | `followup_task` | Deliver work and start a turn when the child is idle. |
| Stop current work | `interrupt_agent` | Request interruption before redelegation or abandonment. |

If another runtime exposes equivalent operations under different names, map those operations once and keep the rest of the workflow unchanged. If create, inspect, wait, message, and interrupt capabilities are not available, stop and report that this skill cannot supervise sub-agents in the current runtime.

The dashboard server cannot call these tools. The main agent must persist observed native states with the `lifecycle` command after each authoritative runtime observation.

When a runtime exposes no separate opaque child id, use its stable canonical task name as both `agent_name` and `agent_id`. Do not invent an unstable identifier.
