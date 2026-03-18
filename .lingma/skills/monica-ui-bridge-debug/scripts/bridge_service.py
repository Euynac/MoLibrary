from __future__ import annotations

import argparse
import csv
import ipaddress
import json
import os
import re
import shutil
import signal
import subprocess
import sys
import time
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Iterable, TextIO
from urllib.error import HTTPError, URLError
from urllib.parse import urljoin, urlparse
from urllib.request import Request, urlopen

LISTENING_PATTERN = re.compile(r"Now listening on:\s*(\S+)")
FILE_LOCK_MARKERS = (
    "MSB3026",
    "being used by another process",
    "used by another process",
    "The process cannot access the file",
)
DEFAULT_LOG_NAME = "app-run.log"
DEFAULT_READY_NAME = "bridge-ready.json"
DEFAULT_REPORT_NAME = "bridge-ready-report.json"
DEFAULT_STATE_NAME = "bridge-process.json"
DEFAULT_HOME_PATH = "/home"
SCRIPT_MARKER = "[bridge-service]"


@dataclass
class BridgeContext:
    project_dir: Path
    project_file: Path
    project_name: str
    task_dir: Path
    log_path: Path
    ready_path: Path
    report_path: Path
    state_path: Path
    service_url: str
    bind_url: str | None
    expected_listen_urls: list[str]
    home_url: str
    port: int


class BridgeServiceError(Exception):
    """Raised when bridge-service arguments or environment are invalid."""


def now_iso() -> str:
    return datetime.now().astimezone().isoformat(timespec="seconds")


def is_windows() -> bool:
    return os.name == "nt"


def is_wsl() -> bool:
    if os.name != "posix":
        return False
    try:
        return "microsoft" in os.uname().release.lower()
    except AttributeError:
        return False


def running_in_posix() -> bool:
    return os.name == "posix"


def locate_windows_cmd() -> str | None:
    if is_windows():
        return shutil.which("cmd.exe") or shutil.which("cmd") or "cmd.exe"
    if is_wsl():
        candidates = [
            "/mnt/c/Windows/System32/cmd.exe",
            shutil.which("cmd.exe"),
        ]
        for candidate in candidates:
            if candidate and Path(candidate).exists():
                return candidate
        return "cmd.exe"
    return None


def run_command(command: list[str], check: bool = False) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        command,
        check=check,
        text=True,
        encoding="utf-8",
        errors="replace",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
    )


def ensure_directory(path: Path) -> Path:
    path.mkdir(parents=True, exist_ok=True)
    return path


def resolve_output_path(task_dir: Path, name_or_path: str) -> Path:
    candidate = Path(name_or_path)
    if candidate.is_absolute():
        ensure_directory(candidate.parent)
        return candidate
    ensure_directory(task_dir)
    return task_dir / candidate


def normalize_base_service_url(service_url: str) -> str:
    parsed = urlparse(service_url)
    if not parsed.scheme or not parsed.netloc:
        raise BridgeServiceError(f"Invalid service URL: {service_url}")
    return f"{parsed.scheme}://{parsed.netloc}"


def parse_port(service_url: str) -> int:
    parsed = urlparse(service_url)
    if parsed.port is None:
        raise BridgeServiceError(f"Service URL must include an explicit port: {service_url}")
    return parsed.port


def should_bind_all_interfaces(service_url: str) -> bool:
    parsed = urlparse(service_url)
    host = parsed.hostname
    if not host:
        raise BridgeServiceError(f"Service URL is missing a host: {service_url}")
    if host in {"localhost", "0.0.0.0"}:
        return False
    try:
        address = ipaddress.ip_address(host)
    except ValueError:
        return False
    return not address.is_loopback and str(address) != "0.0.0.0"


def build_bind_url(service_url: str) -> str | None:
    if not should_bind_all_interfaces(service_url):
        return None
    parsed = urlparse(service_url)
    return f"{parsed.scheme}://0.0.0.0:{parsed.port}"


def build_home_url(service_url: str, home_path: str) -> str:
    normalized = normalize_base_service_url(service_url)
    path = home_path if home_path.startswith("/") else f"/{home_path}"
    return urljoin(f"{normalized}/", path.lstrip("/"))


def discover_project_file(project_dir: Path, project_file: str | None) -> Path:
    if project_file:
        candidate = (project_dir / project_file).resolve() if not Path(project_file).is_absolute() else Path(project_file)
        if candidate.exists():
            return candidate
        raise BridgeServiceError(f"Project file not found: {candidate}")

    matches = sorted(project_dir.glob("*.csproj"))
    if len(matches) == 1:
        return matches[0]
    if not matches:
        raise BridgeServiceError(f"No .csproj file found in {project_dir}")
    names = ", ".join(match.name for match in matches)
    raise BridgeServiceError(
        f"Multiple .csproj files found in {project_dir}. Use --project-file to choose one: {names}"
    )


def dotnet_prefers_windows_paths() -> bool:
    if not is_wsl():
        return False
    dotnet_path = shutil.which("dotnet") or ""
    if not dotnet_path:
        return False

    candidates = [dotnet_path]
    try:
        candidates.append(str(Path(dotnet_path).resolve()))
    except OSError:
        pass

    return any(candidate.lower().endswith(".exe") for candidate in candidates)


def convert_to_dotnet_path(path: Path) -> str:
    if not dotnet_prefers_windows_paths():
        return str(path)
    result = run_command(["wslpath", "-w", str(path)])
    if result.returncode == 0 and result.stdout.strip():
        return result.stdout.strip()
    return str(path)


def build_context(args: argparse.Namespace) -> BridgeContext:
    project_dir = Path(getattr(args, "project_dir", ".")).expanduser().resolve()
    if hasattr(args, "project_dir") and not project_dir.exists():
        raise BridgeServiceError(f"Project directory not found: {project_dir}")

    project_file = discover_project_file(project_dir, getattr(args, "project_file", None)) if hasattr(args, "project_dir") else Path()
    task_dir = ensure_directory(Path(args.task_dir).expanduser().resolve())
    log_path = resolve_output_path(task_dir, getattr(args, "log_name", DEFAULT_LOG_NAME))
    ready_path = resolve_output_path(task_dir, getattr(args, "ready_name", DEFAULT_READY_NAME))
    report_path = resolve_output_path(task_dir, getattr(args, "report_name", DEFAULT_REPORT_NAME))
    state_path = resolve_output_path(task_dir, getattr(args, "state_name", DEFAULT_STATE_NAME))
    service_url = normalize_base_service_url(args.service_url)
    bind_url = build_bind_url(service_url)
    expected_listen_urls = [service_url]
    if bind_url and bind_url not in expected_listen_urls:
        expected_listen_urls.append(bind_url)
    home_url = build_home_url(service_url, getattr(args, "home_path", DEFAULT_HOME_PATH))
    port = parse_port(service_url)
    return BridgeContext(
        project_dir=project_dir,
        project_file=project_file,
        project_name=project_file.stem if project_file else "",
        task_dir=task_dir,
        log_path=log_path,
        ready_path=ready_path,
        report_path=report_path,
        state_path=state_path,
        service_url=service_url,
        bind_url=bind_url,
        expected_listen_urls=expected_listen_urls,
        home_url=home_url,
        port=port,
    )


def print_info(message: str) -> None:
    print(f"{SCRIPT_MARKER} {message}", flush=True)


def write_json(path: Path, payload: dict) -> None:
    ensure_directory(path.parent)
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def read_json_file(path: Path) -> dict:
    if not path.exists():
        return {}
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError:
        return {}
    return payload if isinstance(payload, dict) else {}


def build_state_payload(context: BridgeContext, status: str, **extra: object) -> dict:
    payload = {
        "status": status,
        "timestamp": now_iso(),
        "project_name": context.project_name,
        "project_dir": str(context.project_dir),
        "project_file": str(context.project_file),
        "task_dir": str(context.task_dir),
        "log_path": str(context.log_path),
        "ready_path": str(context.ready_path),
        "report_path": str(context.report_path),
        "state_path": str(context.state_path),
        "service_url": context.service_url,
        "bind_url": context.bind_url,
        "home_url": context.home_url,
        "expected_listen_urls": context.expected_listen_urls,
        "runner_pid": os.getpid(),
    }
    payload.update(extra)
    return payload


def write_state(context: BridgeContext, status: str, **extra: object) -> None:
    write_json(context.state_path, build_state_payload(context, status, **extra))


def write_runtime_message(message: str, log_stream: TextIO | None = None) -> None:
    print_info(message)
    if log_stream is None:
        return
    log_stream.write(f"{SCRIPT_MARKER} {message}\n")
    log_stream.flush()


def safe_parse_pid(value: object) -> int | None:
    if isinstance(value, int) and value > 0:
        return value
    if isinstance(value, str) and value.isdigit():
        return int(value)
    return None


def extract_endpoint_port(value: str) -> int | None:
    match = re.search(r":(\d+)$", value.strip())
    if not match:
        return None
    return int(match.group(1))


def endpoint_matches_port(value: str, port: int) -> bool:
    return extract_endpoint_port(value) == port


def windows_process_exists(pid: int, cmd_exe: str) -> bool:
    result = run_command([cmd_exe, "/c", f'tasklist /FI "PID eq {pid}" /FO CSV /NH'])
    if result.returncode != 0:
        return False
    for row in parse_tasklist_rows(result.stdout):
        parsed_pid = safe_parse_pid(row["pid"])
        if parsed_pid == pid:
            return True
    return False


def process_exists(pid: int) -> bool:
    if is_windows():
        cmd_exe = locate_windows_cmd()
        if cmd_exe:
            return windows_process_exists(pid, cmd_exe)
    try:
        os.kill(pid, 0)
    except OSError:
        return False
    return True


def collect_unix_named_pids(project_name: str) -> set[int]:
    if not running_in_posix():
        return set()
    result = run_command(["ps", "-eo", "pid=,args="])
    if result.returncode != 0:
        return set()
    target = project_name.lower()
    pids: set[int] = set()
    for line in result.stdout.splitlines():
        line = line.strip()
        if not line:
            continue
        parts = line.split(None, 1)
        if len(parts) != 2:
            continue
        pid_text, command_line = parts
        try:
            pid = int(pid_text)
        except ValueError:
            continue
        if pid == os.getpid():
            continue
        if target in command_line.lower():
            pids.add(pid)
    return pids


def collect_unix_port_pids(port: int) -> set[int]:
    if not running_in_posix():
        return set()

    if shutil.which("lsof"):
        result = run_command(["lsof", "-ti", f"tcp:{port}"])
        pids: set[int] = set()
        if result.returncode == 0:
            for line in result.stdout.splitlines():
                line = line.strip()
                if line.isdigit():
                    pids.add(int(line))
            if pids:
                return pids

    if shutil.which("ss"):
        result = run_command(["ss", "-ltnp"])
        pids: set[int] = set()
        if result.returncode == 0:
            for line in result.stdout.splitlines():
                tokens = line.split()
                if len(tokens) < 4:
                    continue
                if not endpoint_matches_port(tokens[3], port):
                    continue
                for match in re.finditer(r"pid=(\d+)", line):
                    pids.add(int(match.group(1)))
        return pids

    return set()


def collect_unix_listening_pids(port: int) -> set[int]:
    if not running_in_posix():
        return set()

    if shutil.which("lsof"):
        result = run_command(["lsof", "-nP", "-t", f"-iTCP:{port}", "-sTCP:LISTEN"])
        pids: set[int] = set()
        if result.returncode == 0:
            for line in result.stdout.splitlines():
                line = line.strip()
                if line.isdigit():
                    pids.add(int(line))
            if pids:
                return pids

    if shutil.which("ss"):
        result = run_command(["ss", "-ltnp"])
        pids: set[int] = set()
        if result.returncode == 0:
            for line in result.stdout.splitlines():
                tokens = line.split()
                if len(tokens) < 4:
                    continue
                if not endpoint_matches_port(tokens[3], port):
                    continue
                for match in re.finditer(r"pid=(\d+)", line):
                    pids.add(int(match.group(1)))
            if pids:
                return pids

    if shutil.which("netstat"):
        result = run_command(["netstat", "-ltnp"])
        pids: set[int] = set()
        if result.returncode == 0:
            for line in result.stdout.splitlines():
                tokens = line.split()
                if len(tokens) < 7:
                    continue
                if not endpoint_matches_port(tokens[3], port):
                    continue
                pid = safe_parse_pid(tokens[6].split("/", 1)[0])
                if pid:
                    pids.add(pid)
        return pids

    return set()


def terminate_posix_pid(pid: int) -> None:
    for signal_name in (signal.SIGTERM, signal.SIGKILL):
        try:
            os.kill(pid, signal_name)
            time.sleep(0.2)
        except ProcessLookupError:
            return
        except PermissionError:
            return


def cleanup_posix_side(project_name: str, port: int) -> dict:
    if not running_in_posix():
        return {"platform": "posix", "killed_pids": []}
    pids = sorted(collect_unix_named_pids(project_name) | collect_unix_port_pids(port))
    for pid in pids:
        terminate_posix_pid(pid)
    return {"platform": "posix", "killed_pids": pids}


def parse_tasklist_rows(raw_output: str) -> list[dict[str, str]]:
    rows: list[dict[str, str]] = []
    reader = csv.reader(raw_output.splitlines())
    for row in reader:
        if len(row) < 2:
            continue
        rows.append({"image_name": row[0].strip(), "pid": row[1].strip()})
    return rows


def collect_windows_named_pids(project_name: str, cmd_exe: str) -> set[int]:
    result = run_command([cmd_exe, "/c", "tasklist /FO CSV /NH"])
    if result.returncode != 0:
        return set()
    project_image = f"{project_name}.exe".lower()
    pids: set[int] = set()
    for row in parse_tasklist_rows(result.stdout):
        image_name = row["image_name"].lower()
        pid_text = row["pid"]
        if image_name != project_image:
            continue
        if pid_text.isdigit():
            pids.add(int(pid_text))
    return pids


def collect_windows_port_pids(port: int, cmd_exe: str, allowed_states: set[str] | None = None) -> set[int]:
    result = run_command([cmd_exe, "/c", "netstat -ano -p tcp"])
    if result.returncode not in {0, 1}:
        return set()
    pids: set[int] = set()
    normalized_states = {state.upper() for state in allowed_states} if allowed_states else None
    for line in result.stdout.splitlines():
        tokens = line.split()
        if len(tokens) < 5:
            continue
        local_address = tokens[1]
        state = tokens[3] if len(tokens) > 4 else ""
        pid_text = tokens[-1]
        if not endpoint_matches_port(local_address, port):
            continue
        if normalized_states is not None and state.upper() not in normalized_states:
            continue
        if normalized_states is None and state.upper() not in {"LISTENING", "ESTABLISHED", "TIME_WAIT", "CLOSE_WAIT"}:
            continue
        if pid_text.isdigit() and int(pid_text) > 0:
            pids.add(int(pid_text))
    return pids


def collect_windows_listening_pids(port: int, cmd_exe: str) -> set[int]:
    return collect_windows_port_pids(port, cmd_exe, allowed_states={"LISTENING"})


def terminate_windows_pid(pid: int, cmd_exe: str) -> None:
    run_command([cmd_exe, "/c", f"taskkill /PID {pid} /F"])


def terminate_recorded_pid(pid: int) -> bool:
    if pid <= 0 or pid == os.getpid():
        return False
    if running_in_posix():
        if not process_exists(pid):
            return False
        terminate_posix_pid(pid)
        return not process_exists(pid)
    if is_windows():
        cmd_exe = locate_windows_cmd()
        if cmd_exe:
            if not windows_process_exists(pid, cmd_exe):
                return False
            terminate_windows_pid(pid, cmd_exe)
            return not windows_process_exists(pid, cmd_exe)
    return False


def cleanup_recorded_state_processes(context: BridgeContext) -> dict:
    state_payload = read_json_file(context.state_path)
    recorded_pids = {
        pid
        for pid in (
            safe_parse_pid(state_payload.get("runner_pid")),
            safe_parse_pid(state_payload.get("child_pid")),
            safe_parse_pid(state_payload.get("listener_pid")),
        )
        if pid and pid != os.getpid()
    }
    killed_pids: list[int] = []
    for pid in sorted(recorded_pids):
        if terminate_recorded_pid(pid):
            killed_pids.append(pid)
    return {
        "state_file": str(context.state_path),
        "state_status": state_payload.get("status"),
        "recorded_pids": sorted(recorded_pids),
        "killed_pids": killed_pids,
    }


def cleanup_windows_side(project_name: str, port: int) -> dict:
    cmd_exe = locate_windows_cmd()
    if not cmd_exe:
        return {"platform": "windows", "killed_pids": []}
    pids = sorted(collect_windows_named_pids(project_name, cmd_exe) | collect_windows_port_pids(port, cmd_exe))
    for pid in pids:
        terminate_windows_pid(pid, cmd_exe)
    return {"platform": "windows", "killed_pids": pids}


def cleanup_bridge_processes(context: BridgeContext) -> dict:
    return {
        "timestamp": now_iso(),
        "project_name": context.project_name,
        "service_url": context.service_url,
        "port": context.port,
        "recorded_state": cleanup_recorded_state_processes(context),
        "posix": cleanup_posix_side(context.project_name, context.port) if running_in_posix() else None,
        "windows": cleanup_windows_side(context.project_name, context.port) if (is_windows() or is_wsl()) else None,
    }


def build_dotnet_command(context: BridgeContext) -> list[str]:
    command = [
        "dotnet",
        "run",
        "--project",
        convert_to_dotnet_path(context.project_file),
    ]
    if context.bind_url:
        command.extend(["--", "--urls", context.bind_url])
    return command


def collect_runtime_listener_pids(port: int) -> set[int]:
    if is_wsl():
        cmd_exe = locate_windows_cmd()
        windows_pids = collect_windows_listening_pids(port, cmd_exe) if cmd_exe else set()
        return windows_pids or collect_unix_listening_pids(port)
    if is_windows():
        cmd_exe = locate_windows_cmd()
        return collect_windows_listening_pids(port, cmd_exe) if cmd_exe else set()
    if running_in_posix():
        return collect_unix_listening_pids(port)
    return set()


def resolve_listener_pid(
    context: BridgeContext,
    process: subprocess.Popen[str],
    timeout_seconds: float = 5.0,
    poll_seconds: float = 0.25,
) -> tuple[int, str]:
    deadline = time.monotonic() + timeout_seconds
    while time.monotonic() <= deadline:
        candidate_pids = sorted(
            pid
            for pid in collect_runtime_listener_pids(context.port)
            if pid > 0 and pid != os.getpid()
        )
        if candidate_pids:
            if process.pid in candidate_pids:
                return process.pid, "port-listener"
            return candidate_pids[0], "port-listener"
        if process.poll() is not None:
            break
        time.sleep(poll_seconds)
    return process.pid, "process-fallback"


def make_ready_payload(
    context: BridgeContext,
    observed_url: str,
    process: subprocess.Popen[str],
    listener_pid: int,
    listener_pid_source: str,
) -> dict:
    return {
        "status": "ready",
        "timestamp": now_iso(),
        "project_name": context.project_name,
        "project_dir": str(context.project_dir),
        "project_file": str(context.project_file),
        "task_dir": str(context.task_dir),
        "service_url": context.service_url,
        "bind_url": context.bind_url,
        "observed_listen_url": observed_url,
        "expected_listen_urls": context.expected_listen_urls,
        "home_url": context.home_url,
        "pid": listener_pid,
        "child_pid": process.pid,
        "listener_pid": listener_pid,
        "listener_pid_source": listener_pid_source,
    }


def make_stopped_payload(context: BridgeContext, ready_payload: dict | None, exit_code: int) -> dict:
    payload = dict(ready_payload or {})
    payload.update(
        {
            "status": "stopped",
            "stopped_at": now_iso(),
            "exit_code": exit_code,
            "service_url": context.service_url,
            "bind_url": context.bind_url,
        }
    )
    return payload


def line_contains_file_lock(line: str) -> bool:
    return any(marker.lower() in line.lower() for marker in FILE_LOCK_MARKERS)


def extract_listening_url(line: str) -> str | None:
    match = LISTENING_PATTERN.search(line)
    if not match:
        return None
    return match.group(1).rstrip()


def unlink_with_retries(path: Path, retries: int = 20, delay_seconds: float = 0.25) -> None:
    for attempt in range(retries):
        try:
            path.unlink()
            return
        except FileNotFoundError:
            return
        except PermissionError:
            if attempt == retries - 1:
                raise
            time.sleep(delay_seconds)


def remove_old_artifacts(context: BridgeContext) -> None:
    for path in (context.log_path, context.ready_path, context.report_path, context.state_path):
        unlink_with_retries(path)


def prepare_run(context: BridgeContext) -> dict:
    cleanup_details = cleanup_bridge_processes(context)
    remove_old_artifacts(context)
    return cleanup_details


def stream_run(context: BridgeContext, retry_limit: int, skip_cleanup: bool = False) -> int:
    cleanup_details = prepare_run(context) if not skip_cleanup else {"skipped": True}
    attempt = 0
    with context.log_path.open("w", encoding="utf-8") as log_stream:
        if skip_cleanup:
            write_runtime_message("Initial cleanup skipped because the detached launcher already prepared the task folder.", log_stream)
        else:
            write_runtime_message(f"Cleanup summary: {json.dumps(cleanup_details, ensure_ascii=False)}", log_stream)

        while True:
            attempt += 1
            command = build_dotnet_command(context)
            write_state(
                context,
                "starting",
                attempt=attempt,
                retry_limit=retry_limit,
                command=command,
            )
            write_runtime_message(f"Starting bridge service (attempt {attempt}): {' '.join(command)}", log_stream)
            ready_payload: dict | None = None
            file_lock_detected = False

            process = subprocess.Popen(
                command,
                cwd=str(context.project_dir),
                stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                encoding="utf-8",
                errors="replace",
                bufsize=1,
            )
            assert process.stdout is not None
            write_state(
                context,
                "starting",
                attempt=attempt,
                retry_limit=retry_limit,
                command=command,
                child_pid=process.pid,
            )

            try:
                for line in process.stdout:
                    sys.stdout.write(line)
                    sys.stdout.flush()
                    log_stream.write(line)
                    log_stream.flush()

                    observed = extract_listening_url(line)
                    if observed and observed in context.expected_listen_urls and ready_payload is None:
                        listener_pid, listener_pid_source = resolve_listener_pid(context, process)
                        ready_payload = make_ready_payload(context, observed, process, listener_pid, listener_pid_source)
                        write_json(context.ready_path, ready_payload)
                        write_state(
                            context,
                            "ready",
                            attempt=attempt,
                            retry_limit=retry_limit,
                            command=command,
                            child_pid=process.pid,
                            listener_pid=listener_pid,
                            listener_pid_source=listener_pid_source,
                            observed_listen_url=observed,
                            ready_path=str(context.ready_path),
                        )
                        write_runtime_message(f"Detected readiness marker: {observed}", log_stream)

                    if ready_payload is None and line_contains_file_lock(line):
                        file_lock_detected = True
                        write_state(
                            context,
                            "restarting",
                            attempt=attempt,
                            retry_limit=retry_limit,
                            command=command,
                            child_pid=process.pid,
                            reason="file-lock-detected",
                        )
                        write_runtime_message("Detected file-lock/build-lock marker. Restarting after cleanup.", log_stream)
                        process.kill()
                        break
            except KeyboardInterrupt:
                write_runtime_message("Received interrupt. Stopping bridge service.", log_stream)
                process.terminate()
                process.wait(timeout=10)
                write_state(
                    context,
                    "stopped",
                    attempt=attempt,
                    retry_limit=retry_limit,
                    command=command,
                    child_pid=process.pid,
                    listener_pid=safe_parse_pid(ready_payload.get("listener_pid")) if ready_payload else None,
                    listener_pid_source=ready_payload.get("listener_pid_source") if ready_payload else None,
                    exit_code=130,
                    interrupted=True,
                )
                raise
            finally:
                exit_code = process.wait()

            if ready_payload is not None:
                write_json(context.ready_path, make_stopped_payload(context, ready_payload, exit_code))

            write_state(
                context,
                "stopped",
                attempt=attempt,
                retry_limit=retry_limit,
                command=command,
                child_pid=process.pid,
                listener_pid=safe_parse_pid(ready_payload.get("listener_pid")) if ready_payload else None,
                listener_pid_source=ready_payload.get("listener_pid_source") if ready_payload else None,
                exit_code=exit_code,
                ready_observed=ready_payload is not None,
                observed_listen_url=ready_payload.get("observed_listen_url") if ready_payload else None,
            )

            if file_lock_detected and attempt <= retry_limit:
                cleanup_details = cleanup_bridge_processes(context)
                write_runtime_message(f"Retry cleanup summary: {json.dumps(cleanup_details, ensure_ascii=False)}", log_stream)
                continue

            if file_lock_detected and attempt > retry_limit:
                write_runtime_message("File-lock retries exhausted.", log_stream)
                return exit_code or 1

            if exit_code != 0 and ready_payload is None:
                write_runtime_message(f"Bridge service exited before readiness with code {exit_code}.", log_stream)
            return exit_code


def read_recent_log_content(path: Path, max_bytes: int = 128_000) -> str:
    if not path.exists():
        return ""
    with path.open("rb") as stream:
        stream.seek(0, os.SEEK_END)
        size = stream.tell()
        stream.seek(max(0, size - max_bytes), os.SEEK_SET)
        return stream.read().decode("utf-8", errors="replace")


def probe_home(url: str, timeout_seconds: float) -> tuple[bool, int | None, str | None]:
    request = Request(url, method="GET")
    try:
        with urlopen(request, timeout=timeout_seconds) as response:
            body = response.read(512).decode("utf-8", errors="replace")
            return 200 <= response.status < 400, response.status, body
    except HTTPError as error:
        body = error.read(512).decode("utf-8", errors="replace")
        return False, error.code, body
    except URLError as error:
        return False, None, str(error.reason)
    except Exception as error:
        return False, None, str(error)


def write_wait_report(context: BridgeContext, payload: dict) -> None:
    write_json(context.report_path, payload)
    print_info(f"Wrote readiness report to {context.report_path}")


def wait_ready(context: BridgeContext, timeout_seconds: int, poll_seconds: float, strict_marker: bool) -> int:
    deadline = time.monotonic() + timeout_seconds
    wait_started_epoch = time.time()
    consecutive_successes = 0
    last_http_status: int | None = None
    last_http_excerpt: str | None = None
    marker_seen = False
    observed_marker: str | None = None
    ready_file_seen = False
    state_status: str | None = None
    state_exit_code: int | None = None
    runner_pid: int | None = None
    child_pid: int | None = None
    listener_pid: int | None = None
    listener_pid_source: str | None = None
    state_updated_during_wait = False

    while time.monotonic() <= deadline:
        if context.ready_path.exists():
            ready_file_seen = True
            ready_payload = read_json_file(context.ready_path)
            observed = ready_payload.get("observed_listen_url")
            if isinstance(observed, str) and observed:
                observed_marker = observed
                marker_seen = True
            ready_child_pid = safe_parse_pid(ready_payload.get("child_pid"))
            if ready_child_pid:
                child_pid = ready_child_pid
            ready_listener_pid = safe_parse_pid(ready_payload.get("listener_pid")) or safe_parse_pid(ready_payload.get("pid"))
            if ready_listener_pid:
                listener_pid = ready_listener_pid
            ready_listener_pid_source = ready_payload.get("listener_pid_source")
            if isinstance(ready_listener_pid_source, str) and ready_listener_pid_source:
                listener_pid_source = ready_listener_pid_source

        if context.state_path.exists():
            try:
                state_updated_during_wait = context.state_path.stat().st_mtime >= wait_started_epoch - 1
            except FileNotFoundError:
                state_updated_during_wait = False
            state_payload = read_json_file(context.state_path)
            status_value = state_payload.get("status")
            state_status = status_value if isinstance(status_value, str) else None
            state_exit_code = safe_parse_pid(state_payload.get("exit_code"))
            parsed_runner_pid = safe_parse_pid(state_payload.get("runner_pid"))
            if parsed_runner_pid:
                runner_pid = parsed_runner_pid
            parsed_child_pid = safe_parse_pid(state_payload.get("child_pid"))
            if parsed_child_pid:
                child_pid = parsed_child_pid
            parsed_listener_pid = safe_parse_pid(state_payload.get("listener_pid"))
            if parsed_listener_pid:
                listener_pid = parsed_listener_pid
            parsed_listener_pid_source = state_payload.get("listener_pid_source")
            if isinstance(parsed_listener_pid_source, str) and parsed_listener_pid_source:
                listener_pid_source = parsed_listener_pid_source

        if not marker_seen:
            log_content = read_recent_log_content(context.log_path)
            for expected in context.expected_listen_urls:
                if f"Now listening on: {expected}" in log_content:
                    marker_seen = True
                    observed_marker = expected
                    break

        ok, status_code, excerpt = probe_home(context.home_url, timeout_seconds=5)
        last_http_status = status_code
        last_http_excerpt = excerpt
        consecutive_successes = consecutive_successes + 1 if ok else 0

        if consecutive_successes >= 3 and marker_seen:
            write_wait_report(
                context,
                {
                    "status": "ready",
                    "timestamp": now_iso(),
                    "service_url": context.service_url,
                    "bind_url": context.bind_url,
                    "home_url": context.home_url,
                    "marker_seen": True,
                    "observed_marker": observed_marker,
                    "ready_file_seen": ready_file_seen,
                    "consecutive_home_successes": consecutive_successes,
                    "last_http_status": last_http_status,
                    "state_status": state_status,
                    "state_exit_code": state_exit_code,
                    "runner_pid": runner_pid,
                    "child_pid": child_pid,
                    "listener_pid": listener_pid,
                    "listener_pid_source": listener_pid_source,
                },
            )
            return 0

        if consecutive_successes >= 3 and not strict_marker:
            write_wait_report(
                context,
                {
                    "status": "ready-with-warning",
                    "timestamp": now_iso(),
                    "service_url": context.service_url,
                    "bind_url": context.bind_url,
                    "home_url": context.home_url,
                    "marker_seen": False,
                    "observed_marker": observed_marker,
                    "ready_file_seen": ready_file_seen,
                    "consecutive_home_successes": consecutive_successes,
                    "last_http_status": last_http_status,
                    "warning": "Home endpoint became reachable before a listening marker was observed.",
                    "state_status": state_status,
                    "state_exit_code": state_exit_code,
                    "runner_pid": runner_pid,
                    "child_pid": child_pid,
                    "listener_pid": listener_pid,
                    "listener_pid_source": listener_pid_source,
                },
            )
            return 0

        if (
            state_updated_during_wait
            and state_status in {"stopped", "failed"}
            and consecutive_successes < 3
        ):
            write_wait_report(
                context,
                {
                    "status": "blocked",
                    "timestamp": now_iso(),
                    "service_url": context.service_url,
                    "bind_url": context.bind_url,
                    "home_url": context.home_url,
                    "marker_seen": marker_seen,
                    "observed_marker": observed_marker,
                    "ready_file_seen": ready_file_seen,
                    "consecutive_home_successes": consecutive_successes,
                    "last_http_status": last_http_status,
                    "last_http_excerpt": last_http_excerpt,
                    "strict_marker": strict_marker,
                    "state_status": state_status,
                    "state_exit_code": state_exit_code,
                    "runner_pid": runner_pid,
                    "child_pid": child_pid,
                    "listener_pid": listener_pid,
                    "listener_pid_source": listener_pid_source,
                    "reason": "Bridge runner stopped before readiness completed.",
                },
            )
            return 1

        time.sleep(poll_seconds)

    write_wait_report(
        context,
        {
            "status": "blocked",
            "timestamp": now_iso(),
            "service_url": context.service_url,
            "bind_url": context.bind_url,
            "home_url": context.home_url,
            "marker_seen": marker_seen,
            "observed_marker": observed_marker,
            "ready_file_seen": ready_file_seen,
            "consecutive_home_successes": consecutive_successes,
            "last_http_status": last_http_status,
            "last_http_excerpt": last_http_excerpt,
            "strict_marker": strict_marker,
            "state_status": state_status,
            "state_exit_code": state_exit_code,
            "runner_pid": runner_pid,
            "child_pid": child_pid,
            "listener_pid": listener_pid,
            "listener_pid_source": listener_pid_source,
        },
    )
    return 1


def command_cleanup(args: argparse.Namespace) -> int:
    context = build_context(args)
    print(json.dumps(cleanup_bridge_processes(context), indent=2, ensure_ascii=False))
    return 0


def build_background_command(context: BridgeContext, args: argparse.Namespace) -> list[str]:
    return [
        sys.executable,
        str(Path(__file__).resolve()),
        "serve",
        "--project-dir",
        str(context.project_dir),
        "--project-file",
        str(context.project_file),
        "--service-url",
        context.service_url,
        "--task-dir",
        str(context.task_dir),
        "--log-name",
        str(context.log_path),
        "--ready-name",
        str(context.ready_path),
        "--report-name",
        str(context.report_path),
        "--state-name",
        str(context.state_path),
        "--home-path",
        getattr(args, "home_path", DEFAULT_HOME_PATH),
        "--file-lock-retries",
        str(args.file_lock_retries),
        "--skip-cleanup",
    ]


def spawn_background_run(context: BridgeContext, args: argparse.Namespace) -> int:
    cleanup_details = prepare_run(context)
    print_info(f"Cleanup summary: {json.dumps(cleanup_details, ensure_ascii=False)}")
    command = build_background_command(context, args)
    print_info(f"Starting detached bridge runner: {' '.join(command)}")

    popen_kwargs = {
        "cwd": str(context.project_dir),
        "stdin": subprocess.DEVNULL,
        "stdout": subprocess.DEVNULL,
        "stderr": subprocess.DEVNULL,
        "close_fds": True,
    }
    if is_windows():
        creationflags = 0
        creationflags |= getattr(subprocess, "DETACHED_PROCESS", 0)
        creationflags |= getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0)
        creationflags |= getattr(subprocess, "CREATE_NO_WINDOW", 0)
        process = subprocess.Popen(command, creationflags=creationflags, **popen_kwargs)
    else:
        process = subprocess.Popen(command, start_new_session=True, **popen_kwargs)

    time.sleep(1.0)
    exit_code = process.poll()
    if exit_code is not None:
        raise BridgeServiceError(
            f"Detached bridge runner exited immediately with code {exit_code}. Inspect {context.log_path} for details."
        )

    print_info(
        "Detached bridge runner started "
        f"(pid {process.pid}). Run wait-ready next to confirm initialization."
    )
    return 0


def command_serve(args: argparse.Namespace) -> int:
    context = build_context(args)
    return stream_run(
        context,
        retry_limit=args.file_lock_retries,
        skip_cleanup=args.skip_cleanup,
    )


def command_run(args: argparse.Namespace) -> int:
    context = build_context(args)
    return spawn_background_run(context, args)


def command_wait_ready(args: argparse.Namespace) -> int:
    context = build_context(args)
    return wait_ready(
        context,
        timeout_seconds=args.timeout,
        poll_seconds=args.poll_seconds,
        strict_marker=args.strict_marker,
    )


def add_shared_run_arguments(parser: argparse.ArgumentParser, include_project: bool = True) -> None:
    if include_project:
        parser.add_argument("--project-dir", required=True, help="Bridge application project directory.")
        parser.add_argument(
            "--project-file",
            help="Optional .csproj file name or absolute path when the directory contains multiple projects.",
        )
    parser.add_argument("--service-url", required=True, help="Bridge service base URL, for example http://localhost:5092")
    parser.add_argument("--task-dir", required=True, help="Task folder used for logs, readiness files, and screenshots.")
    parser.add_argument("--log-name", default=DEFAULT_LOG_NAME, help=f"Log file name or absolute path. Default: {DEFAULT_LOG_NAME}")
    parser.add_argument(
        "--ready-name",
        default=DEFAULT_READY_NAME,
        help=f"Ready file name or absolute path. Default: {DEFAULT_READY_NAME}",
    )
    parser.add_argument(
        "--report-name",
        default=DEFAULT_REPORT_NAME,
        help=f"Readiness report file name or absolute path. Default: {DEFAULT_REPORT_NAME}",
    )
    parser.add_argument(
        "--state-name",
        default=DEFAULT_STATE_NAME,
        help=f"Bridge process state file name or absolute path. Default: {DEFAULT_STATE_NAME}",
    )
    parser.add_argument(
        "--home-path",
        default=DEFAULT_HOME_PATH,
        help=f"Health page path checked by wait-ready. Default: {DEFAULT_HOME_PATH}",
    )


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Cross-platform bridge-service helper for Monica UI debugging workflows.",
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    cleanup_parser = subparsers.add_parser("cleanup", help="Clean residual bridge processes on the current side and Windows side when available.")
    add_shared_run_arguments(cleanup_parser, include_project=True)
    cleanup_parser.set_defaults(func=command_cleanup)

    run_parser = subparsers.add_parser(
        "run",
        help="Clean residual processes and start the bridge service in the background.",
    )
    add_shared_run_arguments(run_parser, include_project=True)
    run_parser.add_argument(
        "--file-lock-retries",
        type=int,
        default=1,
        help="Number of automatic retries when MSB3026 or another file-lock marker appears. Default: 1",
    )
    run_parser.set_defaults(func=command_run)

    serve_parser = subparsers.add_parser(
        "serve",
        help="Internal worker used by run after the detached launcher has prepared the task folder.",
    )
    add_shared_run_arguments(serve_parser, include_project=True)
    serve_parser.add_argument(
        "--file-lock-retries",
        type=int,
        default=1,
        help="Number of automatic retries when MSB3026 or another file-lock marker appears. Default: 1",
    )
    serve_parser.add_argument(
        "--skip-cleanup",
        action="store_true",
        help="Skip initial cleanup because the detached launcher already prepared the task folder.",
    )
    serve_parser.set_defaults(func=command_serve)

    wait_parser = subparsers.add_parser(
        "wait-ready",
        help="Wait for readiness evidence by checking the ready file or log marker plus repeated /home success.",
    )
    add_shared_run_arguments(wait_parser, include_project=False)
    wait_parser.add_argument("--timeout", type=int, default=120, help="Maximum wait time in seconds. Default: 120")
    wait_parser.add_argument(
        "--poll-seconds",
        type=float,
        default=2.0,
        help="Polling interval in seconds. Default: 2.0",
    )
    wait_parser.add_argument(
        "--strict-marker",
        action="store_true",
        help="Fail when /home becomes reachable but no listening marker is observed.",
    )
    wait_parser.set_defaults(func=command_wait_ready)

    return parser


def main(argv: Iterable[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(list(argv) if argv is not None else None)
    try:
        return args.func(args)
    except BridgeServiceError as error:
        print(f"{SCRIPT_MARKER} {error}", file=sys.stderr)
        return 2
    except KeyboardInterrupt:
        print(f"{SCRIPT_MARKER} Interrupted.", file=sys.stderr)
        return 130


if __name__ == "__main__":
    raise SystemExit(main())
