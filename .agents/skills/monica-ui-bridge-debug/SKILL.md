---
name: monica-ui-bridge-debug
description: Orchestrate Monica UI inspection, debugging, and refinement through a user-provided bridge ASP.NET Core project. Use when Monica has no standalone entry point and Codex must launch a bridge service, choose between a simple single-agent bridge workflow and a delegated sub-agent workflow, capture browser artifacts with Playwright, and implement Monica UI fixes under $mo-ui-development. Use simple mode for narrow tasks or when the user requests simple mode. Use $subagent-progress-report for complex work or when the user requests sub-agent mode.
---

# Monica UI Bridge Debug

Use this skill when Monica UI work must be verified through a separate runnable application.

## Required companion skills

- Use `$planning-with-files` to create a new task folder for every bridge run.
- Use `$mo-ui-development` for every Monica Blazor UI implementation or style change.
- Use `$playwright-cli` for browser inspection, snapshots, and screenshots.
- Use `$subagent-progress-report` whenever the selected workflow is delegated sub-agent mode.

## Required user inputs

Request these inputs before bridge-based UI testing starts:

1. Bridge implementation project directory, for example `..\SomeApp\src\Services\Sample\SampleService.API`
2. Bridge service base URL, for example `http://localhost:5092`
3. Target page route or enough module context to discover the page route from the ModuleUI page
4. The actual UI task to inspect, fix, or implement

Do not hardcode any bridge project path or service URL inside the workflow.

## DOM-first bridge diagnosis

When debugging Monica UI through a bridge app, prioritize runtime DOM and CSS evidence over screenshots.

- Always inspect the live page HTML for the target elements before changing code.
- Always inspect computed styles for the target elements when the issue is spacing, sizing, alignment, overflow, or visibility.
- Always inspect the loaded stylesheet rules or emitted Blazor `*.bundle.scp.css` when a class appears in HTML but the expected style does not apply.
- Validate actual runtime MudBlazor DOM class names before writing selectors. Do not guess internal names.
- Use screenshots only to confirm the visible impact after the root cause is understood. Do not diagnose layout issues from screenshots alone.

## Select execution mode first

Choose the workflow before launching the bridge service.

### Simple mode

Use simple mode when the task is narrow, the expected fix is localized, or the user explicitly asks for simple mode.

Rules:

- Run only the requirement-folder setup script from `$planning-with-files`.
- Do not run the `$planning-with-files` session initialization script.
- Do not create `task_plan.md`, `findings.md`, or `progress.md`.
- Do not use sub-agents.
- Store bridge logs, screenshots, snapshots, and readiness artifacts in the created task folder.

### Delegated sub-agent mode

Use delegated sub-agent mode when the task is complex, spans multiple debug or verification loops, benefits from parallel long-running work, or the user explicitly asks for sub-agent mode.

Rules:

- Use `$subagent-progress-report` without exception.
- Act only as the orchestrator. Collect inputs, decide the workflow, ask the user for design confirmation, initialize coordination folders, supervise sub-agents, and summarize results.
- Delegate concrete bridge testing, Playwright capture, debugging, implementation, and verification to sub-agents through the runtime's SubAgent features.
- Keep bridge artifacts in the task folder and sub-agent coordination logs in `.tmp/<timestamp>-agent-session/`.
- Do not silently downgrade to simple mode when the user asked for sub-agent mode.

## Mandatory Monica UI rule handoff

Before editing Monica UI files, also apply `$mo-ui-development`:

```bash
python scripts/check_mudblazor_source.py
```

If the source check fails, stop implementation and ask the user to provide the required MudBlazor source.

## Ask for design confirmation early

If a meaningful design choice needs user confirmation, ask as soon as the decision becomes clear.

- Provide a small set of concrete options and the main tradeoff for each option.
- Pause before implementation if the choice changes architecture, UX, component structure, or shared patterns in a non-obvious way.

## Quick start

### Simple mode

1. Collect the required inputs.
2. Create the task folder with the `$planning-with-files` setup script only.
3. Place screenshots, snapshots, bridge logs, and readiness files in that task folder.
4. Launch the bridge service with `scripts/bridge_service.py run`.
5. Wait for readiness with `scripts/bridge_service.py wait-ready`.
6. Open the full page URL with `$playwright-cli` and capture artifacts.
7. Implement Monica UI changes under `$mo-ui-development` rules.
8. Restart the bridge service with the same script when verification requires a rebuild.
9. Leave the bridge service running after success so the user can inspect it.

### Delegated sub-agent mode

1. Collect the required inputs and ask for any blocking design decision immediately.
2. Create the task folder with the `$planning-with-files` setup script.
3. Initialize one shared session root with `$subagent-progress-report`.
4. Spawn sub-agents with explicit ownership for bridge testing, debugging, implementation, or verification.
5. Bootstrap-check every sub-agent before trusting its progress.
6. Supervise sub-agent logs and keep the main agent out of direct implementation work.
7. Keep the bridge service running after successful verification unless the user asks otherwise.

## Workflow

### Cleanup safety note

The helper must never kill unrelated system listeners just because they share the target port. Before trusting `bridge_service.py cleanup/run`, verify cleanup only terminates:
- PIDs recorded in the task folder state file, or
- project-owned bridge processes whose command line clearly references the selected bridge project.

If the target port is occupied by an unrelated listener, the helper should report that conflict instead of force-killing it.

### 1. Planning and task folder

Create the task folder first with the setup script from `$planning-with-files`.

- In simple mode, stop after the folder is created. Do not create the three planning files.
- In delegated sub-agent mode, create additional planning files only when the user explicitly wants file-based planning or the orchestration genuinely needs persistent high-level notes.

Save these artifacts in the task folder:

- `app-run.log`
- `bridge-ready.json`
- `bridge-ready-report.json`
- `bridge-process.json`
- Playwright screenshots and snapshots
- Any additional debug notes

### 2. Default execution model

Prefer simple mode by default for narrow bridge tasks:

1. Run `bridge_service.py run`
2. Run `bridge_service.py wait-ready`
3. Use `$playwright-cli`
4. Implement or verify the Monica UI change

`run` now starts the bridge service in the background by default and returns immediately, so the same agent can continue with `wait-ready`.

Switch to delegated sub-agent mode when the task is complex or the user requests it. In that mode, the main agent only coordinates and the sub-agents perform the concrete bridge, debug, and implementation work.

### 3. Delegated sub-agent orchestration

When delegated sub-agent mode is selected:

1. Use `$subagent-progress-report` in Main-Agent mode.
2. Create one shared session root and reuse it for the whole run.
3. Give each sub-agent a stable ownership boundary and require `$subagent-progress-report` in Sub-Agent mode.
4. Run the bootstrap check after every delegation before trusting the child.
5. Read the newest `agent.log` entries first and respond to `blocked` or `needs input` immediately.
6. Archive closed sub-agents when their work is complete so future scans stay clean.

### 4. Bridge service startup script

Use the bundled Python script instead of ad-hoc shell snippets.

Launch:

```bash
python scripts/bridge_service.py run \
  --project-dir "<bridge-project-dir>" \
  --service-url "<bridge-service-url>" \
  --task-dir "<task-folder>"
```

This is the default and recommended mode. It returns after the detached runner starts.

Readiness wait:

```bash
python scripts/bridge_service.py wait-ready \
  --service-url "<bridge-service-url>" \
  --task-dir "<task-folder>" \
  --timeout 120
```

Optional explicit project selection when the directory contains more than one `.csproj`:

```bash
python scripts/bridge_service.py run \
  --project-dir "<bridge-project-dir>" \
  --project-file "<project-file>.csproj" \
  --service-url "<bridge-service-url>" \
  --task-dir "<task-folder>"
```

In delegated sub-agent mode, the sub-agent that owns bridge execution should run these commands.

### 5. WSL NAT handling

If the user provides a specific non-loopback IP such as `http://172.31.96.1:5092`, treat it as a WSL NAT-style access URL.

The bundled script automatically converts the runtime bind address to:

```text
http://0.0.0.0:<port>
```

and passes it to `dotnet run` as an application argument. The external URL used for browser access remains the user-provided service URL.

If the user provides `localhost` or `127.0.0.1` and the agent is running inside WSL, always consider a fallback:

- if shell probes such as `curl` work
- but Playwright gets `net::ERR_CONNECTION_REFUSED`

then retry with the WSL gateway IP instead of `localhost`.

Example:

```bash
ip route | awk '/default/ { print $3; exit }'
```

If that returns `172.31.96.1`, switch from `http://localhost:5092` to `http://172.31.96.1:5092`, restart the bridge with that URL, and use that URL for Playwright.

### 6. What the startup script guarantees

`bridge_service.py` provides these behaviors:

- Cross-platform Python implementation for Windows, WSL, and Linux workflows
- Residual process cleanup before launch
- Extra Windows-side cleanup when running inside WSL
- Task-folder artifact reset before each new detached launch
- Background `run` mode for the normal single-agent workflow
- Internal state tracking in `bridge-process.json`
- Internal worker that runs `dotnet run` and mirrors output into `app-run.log`
- Automatic retry when MSBuild reports file-lock markers such as `MSB3026`
- `bridge-ready.json` creation when a listening marker is observed
- `bridge-ready-report.json` creation when readiness checks finish
- Safer readiness semantics: if the probe URL becomes reachable three times but the log marker is still missing, `wait-ready` returns success with a warning by default instead of incorrectly treating the run as blocked

If strict log-marker enforcement is required, add `--strict-marker` to `wait-ready`.

### 7. Playwright capture workflow

After readiness succeeds, build the page URL from:

- user-provided service base URL
- target page route from the relevant ModuleUI page

Then use `$playwright-cli` to inspect and capture the page.

For UI debugging, the default workflow is:

1. Open the page and save a snapshot.
2. Inspect the target element HTML with `eval`, for example:

```bash
playwright-cli eval "() => document.querySelector('<selector>')?.outerHTML"
```

3. Inspect computed styles for the same element, for example:

```bash
playwright-cli eval "() => JSON.stringify((() => { const el = document.querySelector('<selector>'); if (!el) return null; const cs = getComputedStyle(el); const r = el.getBoundingClientRect(); return { width: r.width, height: r.height, padding: cs.padding, margin: cs.margin, display: cs.display, borderRadius: cs.borderRadius }; })(), null, 2)"
```

4. If the class is present but styling is missing, inspect loaded stylesheet rules or fetch the emitted Blazor CSS bundle and compare the selector shape against the runtime DOM.
5. Only after the DOM/CSS root cause is clear, capture screenshots to confirm the visible result.

Use explicit artifact paths inside the task folder, for example:

```bash
playwright-cli open "<service-url><page-route>"
playwright-cli snapshot --filename="<task-folder>/page.yaml"
playwright-cli screenshot --filename="<task-folder>/page.png"
```

If the global `playwright-cli` binary is unavailable, fall back to `npx playwright-cli`.

If `open` is unavailable but an existing browser session is already running, prefer reusing that session with `playwright-cli list`, `-s=<session> goto`, `snapshot`, and `eval` instead of switching to screenshot-only debugging.

### 8. Debug logging and log markers

For difficult problems, let the delegated sub-agent add focused debug output and log search markers.

- Use a unique marker string for each investigation attempt, for example `[bridge-marker:<short-id>]`.
- Write the marker, approximate timestamp, page route, and investigation goal into the sub-agent's `agent.log` before reproducing the issue.
- Search `app-run.log` by marker first, then narrow by timestamp if needed.
- Keep debug logging scoped to the suspected path and remove temporary logs after verification unless the user asks to keep them.
- Assume service logs can become very large. Use markers to make retrieval practical instead of scanning the whole log repeatedly.

### 9. Reporting UI errors

Do not claim a runtime UI error unless one of these is true:

- the error text is visible in the saved screenshot, or
- the same error text is searchable in the saved Playwright snapshot

For layout and styling bugs, do not claim a root cause unless one of these is true:

- the target HTML and computed styles confirm the cause, or
- the emitted stylesheet rules and runtime DOM mismatch confirm the cause

Otherwise report it as unconfirmed instead of as a verified UI error.

## References

- `references/bridge-service-script.md`

## Scripts

- `scripts/bridge_service.py` - cross-platform bridge cleanup, detached launch, and readiness checks

## Quick checklist

- [ ] Collect bridge project directory and bridge service URL from the user
- [ ] Select simple mode or delegated sub-agent mode before launch
- [ ] Create a new task folder with `$planning-with-files`
- [ ] Skip the three planning files in simple mode
- [ ] Use `$subagent-progress-report` in delegated sub-agent mode
- [ ] Run `$mo-ui-development` source check before UI edits
- [ ] Use `bridge_service.py run` instead of ad-hoc launch commands
- [ ] Use `bridge_service.py wait-ready` before opening Playwright
- [ ] If `localhost` works in shell probes but Playwright cannot connect in WSL, retry with the WSL gateway IP
- [ ] Save screenshots and snapshots inside the task folder
- [ ] Keep the bridge service running after successful verification
