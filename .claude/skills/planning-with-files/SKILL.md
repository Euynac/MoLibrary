---
name: planning-with-files
version: "2.11.0"
description: Implements Manus-style file-based planning for complex tasks. Creates task_plan.md, findings.md, and progress.md in .pending/NNN-description/ folders. Use when starting complex multi-step tasks, research projects, or any task requiring >5 tool calls. Now with automatic session recovery and completion tracking.
user-invocable: true
allowed-tools:
  - Read
  - Write
  - Edit
  - Bash
  - Glob
  - Grep
  - WebFetch
  - WebSearch
hooks:
  PreToolUse:
    - matcher: "Write|Edit|Bash|Read|Glob|Grep"
      hooks:
        - type: command
          command: |
            # Try to find task_plan.md in .pending/ folders first, then fall back to root
            TASK_PLAN=$(find .pending -name "task_plan.md" -type f 2>/dev/null | grep -v "(done)" | head -1)
            if [ -z "$TASK_PLAN" ]; then
              TASK_PLAN="task_plan.md"
            fi
            if [ -f "$TASK_PLAN" ]; then
              cat "$TASK_PLAN" | head -30
            fi
  PostToolUse:
    - matcher: "Write|Edit"
      hooks:
        - type: command
          command: "echo '[planning-with-files] File updated. If this completes a phase, update task_plan.md status.'"
  Stop:
    - hooks:
        - type: command
          command: |
            SCRIPT_DIR="${CLAUDE_PLUGIN_ROOT:-$HOME/.claude/plugins/planning-with-files}/scripts"

            IS_WINDOWS=0
            if [ "${OS-}" = "Windows_NT" ]; then
              IS_WINDOWS=1
            else
              UNAME_S="$(uname -s 2>/dev/null || echo '')"
              case "$UNAME_S" in
                CYGWIN*|MINGW*|MSYS*) IS_WINDOWS=1 ;;
              esac
            fi

            if [ "$IS_WINDOWS" -eq 1 ]; then
              if command -v pwsh >/dev/null 2>&1; then
                pwsh -ExecutionPolicy Bypass -File "$SCRIPT_DIR/check-complete.ps1" 2>/dev/null ||
                powershell -ExecutionPolicy Bypass -File "$SCRIPT_DIR/check-complete.ps1" 2>/dev/null ||
                sh "$SCRIPT_DIR/check-complete.sh"

                # Run mark-complete script
                pwsh -ExecutionPolicy Bypass -File "$SCRIPT_DIR/mark-complete.ps1" 2>/dev/null ||
                powershell -ExecutionPolicy Bypass -File "$SCRIPT_DIR/mark-complete.ps1" 2>/dev/null ||
                sh "$SCRIPT_DIR/mark-complete.sh"
              else
                powershell -ExecutionPolicy Bypass -File "$SCRIPT_DIR/check-complete.ps1" 2>/dev/null ||
                sh "$SCRIPT_DIR/check-complete.sh"

                # Run mark-complete script
                powershell -ExecutionPolicy Bypass -File "$SCRIPT_DIR/mark-complete.ps1" 2>/dev/null ||
                sh "$SCRIPT_DIR/mark-complete.sh"
              fi
            else
              sh "$SCRIPT_DIR/check-complete.sh"
              sh "$SCRIPT_DIR/mark-complete.sh"
            fi
---

# Planning with Files

Work like Manus: Use persistent markdown files as your "working memory on disk."

## FIRST: Setup Requirement Folder

**Before starting work**, set up a requirement folder in `.pending/`:

1. **Extract task description** from the user's initial message:
   - Identify 2-4 key words describing the task
   - Convert to kebab-case (lowercase with hyphens)
   - Examples:
     - "I want to add a RAG module" → "rag-module"
     - "Create localization support" → "localization-support"
     - "Fix the authentication bug" → "auth-bug-fix"

2. **Create the requirement folder**:

```bash
# Linux/macOS
FOLDER_PATH=$(${CLAUDE_PLUGIN_ROOT}/scripts/setup-requirement-folder.sh "task-description")
```

```powershell
# Windows PowerShell
$FOLDER_PATH = & "$env:USERPROFILE\.claude\skills\planning-with-files\scripts\setup-requirement-folder.ps1" "task-description"
```

3. **Initialize planning files** in the folder:

```bash
# Linux/macOS
${CLAUDE_PLUGIN_ROOT}/scripts/init-session.sh "project-name" "$FOLDER_PATH"
```

```powershell
# Windows PowerShell
& "$env:USERPROFILE\.claude\skills\planning-with-files\scripts\init-session.ps1" "project-name" "$FOLDER_PATH"
```

The folder will be created as `.pending/NNN-description/` where NNN is the next available number (e.g., 010, 011, etc.).

## Check for Previous Session (v2.2.0)

**After folder setup**, check for unsynced context from a previous session:

```bash
# Linux/macOS
$(command -v python3 || command -v python) ${CLAUDE_PLUGIN_ROOT}/scripts/session-catchup.py "$(pwd)"
```

```powershell
# Windows PowerShell
& (Get-Command python -ErrorAction SilentlyContinue).Source "$env:USERPROFILE\.claude\skills\planning-with-files\scripts\session-catchup.py" (Get-Location)
```

If catchup report shows unsynced context:
1. Run `git diff --stat` to see actual code changes
2. Read current planning files
3. Update planning files based on catchup + git diff
4. Then proceed with task

## Important: Where Files Go

- **Templates** are in `${CLAUDE_PLUGIN_ROOT}/templates/`
- **Your planning files** go in **`.pending/NNN-description/`** folders

| Location | What Goes There |
|----------|-----------------|
| Skill directory (`${CLAUDE_PLUGIN_ROOT}/`) | Templates, scripts, reference docs |
| `.pending/NNN-description/` | `task_plan.md`, `findings.md`, `progress.md` |
| Project root | Fallback location if `.pending/` cannot be created |

## Automatic Completion Tracking

When all phases in `task_plan.md` are marked as `Status: complete`, the requirement folder is automatically renamed with a "(done)" prefix when you stop the session:

- Before: `.pending/010-rag-module/`
- After: `.pending/(done) 010-rag-module/`

This provides visual indication of completed requirements in the file system.

## Quick Start

Before ANY complex task:

1. **Setup requirement folder** — Extract task description from user's message, create `.pending/NNN-description/` folder
2. **Initialize planning files** — Run init-session script with the folder path
3. **Create planning files** — `task_plan.md`, `findings.md`, `progress.md` in the requirement folder
4. **Re-read plan before decisions** — Refreshes goals in attention window
5. **Update after each phase** — Mark complete, log errors
6. **Automatic completion** — Folder gets "(done)" prefix when all phases complete

> **Note:** Planning files go in `.pending/NNN-description/` folders, not the project root.

## The Core Pattern

```
Context Window = RAM (volatile, limited)
Filesystem = Disk (persistent, unlimited)

→ Anything important gets written to disk.
```

## File Purposes

| File | Purpose | When to Update |
|------|---------|----------------|
| `task_plan.md` | Phases, progress, decisions | After each phase |
| `findings.md` | Research, discoveries | After ANY discovery |
| `progress.md` | Session log, test results | Throughout session |

## Critical Rules

### 1. Create Plan First
Never start a complex task without `task_plan.md`. Non-negotiable.

### 2. The 2-Action Rule
> "After every 2 view/browser/search operations, IMMEDIATELY save key findings to text files."

This prevents visual/multimodal information from being lost.

### 3. Read Before Decide
Before major decisions, read the plan file. This keeps goals in your attention window.

### 4. Update After Act
After completing any phase:
- Mark phase status: `in_progress` → `complete`
- Log any errors encountered
- Note files created/modified

### 5. Log ALL Errors
Every error goes in the plan file. This builds knowledge and prevents repetition.

```markdown
## Errors Encountered
| Error | Attempt | Resolution |
|-------|---------|------------|
| FileNotFoundError | 1 | Created default config |
| API timeout | 2 | Added retry logic |
```

### 6. Never Repeat Failures
```
if action_failed:
    next_action != same_action
```
Track what you tried. Mutate the approach.

## The 3-Strike Error Protocol

```
ATTEMPT 1: Diagnose & Fix
  → Read error carefully
  → Identify root cause
  → Apply targeted fix

ATTEMPT 2: Alternative Approach
  → Same error? Try different method
  → Different tool? Different library?
  → NEVER repeat exact same failing action

ATTEMPT 3: Broader Rethink
  → Question assumptions
  → Search for solutions
  → Consider updating the plan

AFTER 3 FAILURES: Escalate to User
  → Explain what you tried
  → Share the specific error
  → Ask for guidance
```

## Read vs Write Decision Matrix

| Situation | Action | Reason |
|-----------|--------|--------|
| Just wrote a file | DON'T read | Content still in context |
| Viewed image/PDF | Write findings NOW | Multimodal → text before lost |
| Browser returned data | Write to file | Screenshots don't persist |
| Starting new phase | Read plan/findings | Re-orient if context stale |
| Error occurred | Read relevant file | Need current state to fix |
| Resuming after gap | Read all planning files | Recover state |

## The 5-Question Reboot Test

If you can answer these, your context management is solid:

| Question | Answer Source |
|----------|---------------|
| Where am I? | Current phase in task_plan.md |
| Where am I going? | Remaining phases |
| What's the goal? | Goal statement in plan |
| What have I learned? | findings.md |
| What have I done? | progress.md |

## When to Use This Pattern

**Use for:**
- Multi-step tasks (3+ steps)
- Research tasks
- Building/creating projects
- Tasks spanning many tool calls
- Anything requiring organization

**Skip for:**
- Simple questions
- Single-file edits
- Quick lookups

## Templates

Copy these templates to start:

- [templates/task_plan.md](templates/task_plan.md) — Phase tracking
- [templates/findings.md](templates/findings.md) — Research storage
- [templates/progress.md](templates/progress.md) — Session logging

## Scripts

Helper scripts for automation:

- `scripts/init-session.sh` — Initialize all planning files
- `scripts/check-complete.sh` — Verify all phases complete
- `scripts/session-catchup.py` — Recover context from previous session (v2.2.0)

## Advanced Topics

- **Manus Principles:** See [reference.md](reference.md)
- **Real Examples:** See [examples.md](examples.md)

## Anti-Patterns

| Don't | Do Instead |
|-------|------------|
| Use TodoWrite for persistence | Create task_plan.md file in requirement folder |
| State goals once and forget | Re-read plan before decisions |
| Hide errors and retry silently | Log errors to plan file |
| Stuff everything in context | Store large content in files |
| Start executing immediately | Setup requirement folder and create plan FIRST |
| Repeat failed actions | Track attempts, mutate approach |
| Create files in project root | Create files in `.pending/NNN-description/` folder |
| Manually track completion | Let the Stop hook auto-rename completed folders |
