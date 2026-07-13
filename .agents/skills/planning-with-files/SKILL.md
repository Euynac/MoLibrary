---
name: planning-with-files
description: Maintain concise, opt-in task memory in a numbered `.pending/NNN-description/` folder. Use only when the user explicitly invokes `$planning-with-files`, asks for planning or working-memory files on disk, or asks to resume an existing `.pending` task. Do not trigger solely because work is complex, multi-step, long-running, or delegated.
---

# Planning with Files

Persist only the task context that must survive a pause, handoff, or separate session. Use native runtime planning for ordinary work.

## Activation boundary

- Start this workflow only after the user explicitly chooses file-based planning.
- Do not let another skill or task complexity silently enable it.
- Keep planning artifacts local to the task; do not treat `.pending/` as permanent project documentation.

## Start a new task

Resolve `<skill-dir>` to the directory containing this `SKILL.md`, then run the cross-platform initializer from the project root:

```bash
python <skill-dir>/scripts/init_task.py "short task description" \
  --base-dir "<project-root>" \
  --goal "Concrete end state" \
  --phase "Understand the current state" \
  --phase "Implement the change" \
  --phase "Verify and deliver"
```

Add `--with-findings` when the task needs durable research, evidence, URLs, or artifact paths.

The initializer always creates `task_plan.md` in a new `.pending/NNN-description/` folder. It never reuses an older task folder and never reads runtime transcripts. Tailor the generated phases and next action before implementation.

## Resume an existing task

1. Use the task folder identified by the user. If no folder is named and exactly one clearly matches, use it; otherwise list the candidates and request a choice.
2. Read `task_plan.md` and any optional planning files that exist.
3. Inspect the real workspace with `git status --short` and the relevant diff or artifacts.
4. Reconcile stale planning state before continuing. The workspace and verified external state override old notes.

## Keep one source of truth

| File | Use | Update |
|---|---|---|
| `task_plan.md` | Goal, phases, current state, material decisions, blockers, next action, handoff snapshot | Required; update at durable checkpoints |
| `findings.md` | Research findings, source links, visual observations, and artifact paths | Optional; update when evidence would otherwise be lost |

Do not duplicate the same status, decision, or error across files. Keep phase status, decisions, verification, and the next action authoritative in `task_plan.md`.

## Update discipline

Update persistent memory when one of these occurs:

- a phase starts or finishes;
- scope or a material technical decision changes;
- a blocker or failure changes the approach;
- durable browser, document, or research evidence would otherwise be lost;
- work pauses, transfers to another agent or session, or completes.

Do not update after every tool call, every two reads, or every transient error. Do not re-read a file immediately after writing it unless verification is necessary.

When work completes, set the overall status and every completed phase consistently, clear the next action, record final verification, and remove obsolete blockers. Before handoff, confirm that the planning state agrees with the actual workspace.

## Storage boundary

`.pending/` is disposable local working state and is commonly gitignored. Move durable requirements, architecture decisions, and team knowledge into tracked project documentation when they must survive clones or be reviewed with the code.

## Resources

- `scripts/init_task.py` creates a new numbered task folder and selected planning files.
- `templates/task_plan.md` is the required concise plan template.
- `templates/findings.md` is the optional research and evidence template.
