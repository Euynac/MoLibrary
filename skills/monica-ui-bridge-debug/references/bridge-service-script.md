# Bridge Service Script Reference

## Purpose

Use `scripts/bridge_service.py` to standardize Monica bridge-service startup across Windows, WSL, and Linux.

## Commands

### `run`

Launch the bridge service after cleanup.

Default behavior:

- Start a detached background worker
- Return immediately so the same agent can continue with `wait-ready`

Required arguments:

- `--project-dir`
- `--service-url`
- `--task-dir`

`--service-url` must be concrete, but it does not have to come from the user. The agent may discover it from launch settings, an existing project-owned listener, or an unused localhost port before calling the script.

Optional arguments:

- `--project-file`
- `--log-name`
- `--ready-name`
- `--report-name`
- `--state-name`
- `--probe-path`
- `--file-lock-retries`

Artifacts written to the task folder by default:

- `app-run.log`
- `bridge-ready.json`
- `bridge-ready-report.json` (only when `wait-ready` is used)
- `bridge-process.json`

PID fields:

- `bridge-ready.json` now records the actual listening process in `pid` and `listener_pid`
- `bridge-ready.json` also keeps `child_pid` for the intermediate `dotnet run` process when one exists
- `bridge-process.json` and `bridge-ready-report.json` include both `child_pid` and `listener_pid`
- `listener_pid_source` shows whether the listener PID came from a port scan or a fallback to the launched process

### `serve`

Internal worker used by `run` after the detached launcher prepares the task folder. Do not use this command in the normal skill workflow unless you are debugging the launcher itself.

### `cleanup`

Run the cleanup logic without launching the service.

Cleanup checks:

- recorded bridge state in `bridge-process.json`
- project-owned bridge processes whose command line clearly matches the selected project
- same-port listeners are classified as owned bridge processes vs unrelated conflicts
- Windows-side cleanup when available

### `wait-ready`

Wait for readiness evidence.

Success rules by default:

1. `bridge-ready.json` exists or `app-run.log` contains a matching `Now listening on:` marker
2. The probe URL responds three consecutive times without a `5xx` status

Default probe behavior:

- `wait-ready` probes the service root URL by default
- `404`, `401`, and `403` still count as reachable because they prove the ASP.NET Core pipeline is serving requests
- Use `--probe-path` when a project has a stronger application-specific readiness endpoint

If rule 2 succeeds before rule 1, the command returns success with `ready-with-warning` unless `--strict-marker` is provided.

## WSL path behavior

When the script detects WSL plus a Windows `dotnet.exe`, it converts the project path to Windows format before invoking `dotnet run`.

## WSL NAT behavior

When `--service-url` uses a non-loopback IP address, the script keeps the external access URL unchanged but binds the bridge process to `0.0.0.0:<port>`.

For loopback URLs, the script also passes the requested URL to `dotnet run -- --urls`, so a discovered `http://localhost:<port>` value is honored by the bridge process.

## Recommended sub-agent split

Single-agent execution is the default workflow:

1. `python scripts/bridge_service.py run ...`
2. `python scripts/bridge_service.py wait-ready ...`
3. Playwright capture

Delegated split is usually unnecessary because `run` already returns immediately. If you still need parallel work, one worker can trigger `run` while another waits for readiness and captures browser artifacts.

## Exit codes

- `0`: success, including `ready-with-warning`
- `1`: blocked readiness or service startup failure
- `2`: invalid arguments or environment
- `130`: interrupted
