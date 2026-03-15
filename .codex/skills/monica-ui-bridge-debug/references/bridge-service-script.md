# Bridge Service Script Reference

## Purpose

Use `scripts/bridge_service.py` to standardize Monica bridge-service startup across Windows, WSL, and Linux.

## Commands

### `run`

Launch the bridge service in the foreground after cleanup and keep the process attached.

Required arguments:

- `--project-dir`
- `--service-url`
- `--task-dir`

Optional arguments:

- `--project-file`
- `--log-name`
- `--ready-name`
- `--report-name`
- `--home-path`
- `--file-lock-retries`

Artifacts written to the task folder by default:

- `app-run.log`
- `bridge-ready.json`
- `bridge-ready-report.json` (only when `wait-ready` is used)

### `cleanup`

Run the cleanup logic without launching the service.

### `wait-ready`

Wait for readiness evidence.

Success rules by default:

1. `bridge-ready.json` exists or `app-run.log` contains a matching `Now listening on:` marker
2. `<service-url>/home` succeeds three consecutive times

If rule 2 succeeds before rule 1, the command returns success with `ready-with-warning` unless `--strict-marker` is provided.

## WSL path behavior

When the script detects WSL plus a Windows `dotnet.exe`, it converts the project path to Windows format before invoking `dotnet run`.

## WSL NAT behavior

When `--service-url` uses a non-loopback IP address, the script keeps the external access URL unchanged but binds the bridge process to `0.0.0.0:<port>`.

## Recommended sub-agent split

- Worker A: `python scripts/bridge_service.py run ...`
- Worker B: `python scripts/bridge_service.py wait-ready ...` and then Playwright capture

## Exit codes

- `0`: success, including `ready-with-warning`
- `1`: blocked readiness or foreground process failure
- `2`: invalid arguments or environment
- `130`: interrupted
