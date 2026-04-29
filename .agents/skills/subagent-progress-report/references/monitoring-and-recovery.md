# Monitoring and Recovery

Use this file only after `check_subagent_bootstrap.py` has already returned `bootstrapped` for the child.

## Core rule

Silence is not failure. Once a child has bootstrapped, do not interrupt, close, or redelegate it after a single quiet read of `agent.log`.

## Act immediately only for real signals

Skip the patience flow and respond immediately when:

- the latest log line explicitly says `blocked` or `needs input`
- the child clearly drifts from scope or ignores a hard constraint
- the harness itself reports the child has exited or failed

The patience flow is for silence, not for obvious bad behavior.

## Round 1: wait before nudging

1. Capture the current latest non-empty `agent.log` entry, or let the helper script capture it for you.
2. Re-read `agent.log` with exponential backoff at least 3 times before interrupting.
3. Use the default schedule `30s`, `60s`, `120s`. Keep the total wait for one round at or below `5 minutes`.
4. If any re-read shows a new prepended entry, treat the child as responsive and continue supervising normally.
5. Only if all 3 checks show no new entry should you interrupt the child with one precise nudge.

## Round 2: wait again after the nudge

1. Start a fresh patience round after the nudge.
2. Repeat the same 3 exponential-backoff checks.
3. If a new log entry appears during this second round, treat the child as responsive and continue.
4. Only if the entire second round also shows no new log entry should you treat the child as unresponsive and redelegate the task.

## Helper script

Use the bundled helper to make the waiting policy explicit instead of improvising the timing:

Round 1, before any interrupt:

```bash
python <skill-dir>/scripts/wait_for_agent_log_update.py --session-root "<session-root>" --agent-name "<agent-name>" --phase quiet-wait --attempts 3 --initial-wait-seconds 30 --backoff-factor 2 --max-total-wait-seconds 300 --json
```

Round 2, after the interrupt or nudge:

```bash
python <skill-dir>/scripts/wait_for_agent_log_update.py --session-root "<session-root>" --agent-name "<agent-name>" --phase post-nudge --attempts 3 --initial-wait-seconds 30 --backoff-factor 2 --max-total-wait-seconds 300 --json
```

Interpret the result:

- `progressed` with `continue`: the child wrote a new prepended entry. Keep supervising normally.
- `stalled` with `follow_up`: the first quiet-period round ended with no new entry. Interrupt once and nudge precisely.
- `stalled` with `redelegate`: the post-nudge round also ended with no new entry. The child now counts as unresponsive.

## Keep the recovery message precise

When you interrupt after round 1, send one short, specific follow-up. Ask for a status update or a concrete next checkpoint. Do not replace the child immediately, and do not stack repeated nags while the patience timer is still running.
