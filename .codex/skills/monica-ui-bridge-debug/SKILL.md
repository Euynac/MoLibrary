---
name: monica-ui-bridge-debug
description: Orchestrate Monica UI inspection, debugging, and refinement through a user-provided bridge ASP.NET Core project. Use when Monica has no standalone entry point and Codex must launch a bridge service, usually through a single-agent `bridge_service.py run` plus `wait-ready` workflow, then capture browser artifacts with Playwright and implement Monica UI fixes under $mo-ui-development. Use $subagent-progress-report only when optional delegation is actually needed.
---

# Monica UI Bridge Debug

Use this skill when Monica UI work must be verified through a separate runnable application.

## Required companion skills

- Use `$planning-with-files` for every multi-step task and store artifacts in the new task folder.
- Use `$mo-ui-development` for every Monica Blazor UI implementation or style change.
- Use `$playwright-cli` for browser inspection, snapshots, and screenshots.
- Use `$subagent-progress-report` only when you intentionally choose a delegated multi-agent workflow.

## Required user inputs

Request these inputs before bridge-based UI testing starts:

1. Bridge implementation project directory, for example `..\SomeApp\src\Services\Sample\SampleService.API`
2. Bridge service base URL, for example `http://localhost:5092`
3. Target page route or enough module context to discover the page route from the ModuleUI page
4. The actual UI task to inspect, fix, or implement

Do not hardcode any bridge project path or service URL inside the workflow.

## Mandatory Monica UI rule handoff

Before editing Monica UI files, also apply `$mo-ui-development`:

```bash
python scripts/check_mudblazor_source.py
```

If the source check fails, stop implementation and ask the user to provide the required MudBlazor source.

## Quick start

1. Create a planning task folder with `$planning-with-files`.
2. Place all screenshots, snapshots, bridge logs, and readiness files in that task folder.
3. Launch the bridge service with `scripts/bridge_service.py run`.
4. Wait for readiness with `scripts/bridge_service.py wait-ready`.
5. Open the full page URL with `$playwright-cli` and capture artifacts.
6. Implement Monica UI changes under `$mo-ui-development` rules.
7. Restart the bridge service with the same script when verification requires a rebuild.
8. Leave the bridge service running after success so the user can inspect it.

## Workflow

### 1. Planning and task folder

Create the task folder first with `$planning-with-files`. Save these artifacts there:

- `app-run.log`
- `bridge-ready.json`
- `bridge-ready-report.json`
- `bridge-process.json`
- Playwright screenshots and snapshots
- Any additional debug notes

### 2. Default execution model

Prefer a single-agent flow:

1. Run `bridge_service.py run`
2. Run `bridge_service.py wait-ready`
3. Use `$playwright-cli`
4. Implement or verify the Monica UI change

`run` now starts the bridge service in the background by default and returns immediately, so the same agent can continue with `wait-ready`.

Only switch to `$subagent-progress-report` when you explicitly want one worker to keep a foreground session open or when long-running browser work and service work must progress independently.

### 3. Bridge service startup script

Use the bundled Python script instead of ad-hoc shell snippets.

Background launch:

```bash
python scripts/bridge_service.py run \
  --project-dir "<bridge-project-dir>" \
  --service-url "<bridge-service-url>" \
  --task-dir "<task-folder>"
```

This is the default and recommended mode. It returns after the detached runner starts.

Optional foreground launch when you intentionally want live attached logs in the current shell:

```bash
python scripts/bridge_service.py run \
  --project-dir "<bridge-project-dir>" \
  --service-url "<bridge-service-url>" \
  --task-dir "<task-folder>" \
  --foreground
```

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

### 4. WSL NAT handling

If the user provides a specific non-loopback IP such as `http://172.31.96.1:5092`, treat it as a WSL NAT-style access URL.

The bundled script automatically converts the runtime bind address to:

```text
http://0.0.0.0:<port>
```

and passes it to `dotnet run` as an application argument. The external URL used for browser access remains the user-provided service URL.

### 5. What the startup script guarantees

`bridge_service.py` provides these behaviors:

- Cross-platform Python implementation for Windows, WSL, and Linux workflows
- Residual process cleanup before launch
- Extra Windows-side cleanup when running inside WSL
- Task-folder artifact reset before each new detached launch
- Background `run` mode for the normal single-agent workflow
- Optional foreground `run --foreground` mode for live attached logs
- Internal state tracking in `bridge-process.json`
- Foreground `dotnet run` worker with live log mirroring into `app-run.log`
- Automatic retry when MSBuild reports file-lock markers such as `MSB3026`
- `bridge-ready.json` creation when a listening marker is observed
- `bridge-ready-report.json` creation when readiness checks finish
- Safer readiness semantics: if `/home` becomes reachable three times but the log marker is still missing, `wait-ready` returns success with a warning by default instead of incorrectly treating the run as blocked

If strict log-marker enforcement is required, add `--strict-marker` to `wait-ready`.

### 6. Playwright capture workflow

After readiness succeeds, build the page URL from:

- user-provided service base URL
- target page route from the relevant ModuleUI page

Then use `$playwright-cli` to inspect and capture the page.

Use explicit artifact paths inside the task folder, for example:

```bash
playwright-cli open "<service-url><page-route>"
playwright-cli snapshot --filename="<task-folder>/page.yaml"
playwright-cli screenshot --filename="<task-folder>/page.png"
```

If the global `playwright-cli` binary is unavailable, fall back to `npx playwright-cli`.

### 7. Reporting UI errors

Do not claim a runtime UI error unless one of these is true:

- the error text is visible in the saved screenshot, or
- the same error text is searchable in the saved Playwright snapshot

Otherwise report it as unconfirmed instead of as a verified UI error.

## References

- `references/bridge-service-script.md`

## Scripts

- `scripts/bridge_service.py` - cross-platform bridge cleanup, foreground launch, and readiness checks

## Quick checklist

- [ ] Collect bridge project directory and bridge service URL from the user
- [ ] Create a new task folder with `$planning-with-files`
- [ ] Run `$mo-ui-development` source check before UI edits
- [ ] Use `bridge_service.py run` instead of ad-hoc launch commands
- [ ] Use `bridge_service.py wait-ready` before opening Playwright
- [ ] Save screenshots and snapshots inside the task folder
- [ ] Keep the bridge service running after successful verification
