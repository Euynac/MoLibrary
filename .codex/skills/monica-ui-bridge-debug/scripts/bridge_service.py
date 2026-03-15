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
from typing import Iterable
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
    return dotnet_path.lower().endswith(".exe")


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
            marker = f":{port}"
            for line in result.stdout.splitlines():
                if marker not in line:
                    continue
                for match in re.finditer(r"pid=(\d+)", line):
                    pids.add(int(match.group(1)))
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


def collect_windows_port_pids(port: int, cmd_exe: str) -> set[int]:
    result = run_command([cmd_exe, "/c", f"netstat -ano -p tcp | findstr :{port}"])
    if result.returncode not in {0, 1}:
        return set()
    pids: set[int] = set()
    for line in result.stdout.splitlines():
        tokens = line.split()
        if len(tokens) < 5:
            continue
        local_address = tokens[1]
        state = tokens[3] if len(tokens) > 4 else ""
        pid_text = tokens[-1]
        if f":{port}" not in local_address:
            continue
        if state.upper() not in {"LISTENING", "ESTABLISHED", "TIME_WAIT", "CLOSE_WAIT"}:
            continue
        if pid_text.isdigit():
            pids.add(int(pid_text))
    return pids


def terminate_windows_pid(pid: int, cmd_exe: str) -> None:
    run_command([cmd_exe, "/c", f"taskkill /PID {pid} /F"])


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


def make_ready_payload(context: BridgeContext, observed_url: str, process: subprocess.Popen[str]) -> dict:
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
        "pid": process.pid,
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


def remove_old_artifacts(context: BridgeContext) -> None:
    for path in (context.log_path, context.ready_path, context.report_path):
        if path.exists():
            path.unlink()


def stream_run(context: BridgeContext, retry_limit: int) -> int:
    remove_old_artifacts(context)
    cleanup_details = cleanup_bridge_processes(context)
    print_info(f"Cleanup summary: {json.dumps(cleanup_details, ensure_ascii=False)}")

    attempt = 0
    while True:
        attempt += 1
        command = build_dotnet_command(context)
        print_info(f"Starting bridge service (attempt {attempt}): {' '.join(command)}")
        ready_payload: dict | None = None
        file_lock_detected = False

        with context.log_path.open("w", encoding="utf-8") as log_stream:
            process = subprocess.Popen(
                command,
                cwd=str(context.project_dir),
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                encoding="utf-8",
                errors="replace",
                bufsize=1,
            )
            assert process.stdout is not None

            try:
                for line in process.stdout:
                    sys.stdout.write(line)
                    sys.stdout.flush()
                    log_stream.write(line)
                    log_stream.flush()

                    observed = extract_listening_url(line)
                    if observed and observed in context.expected_listen_urls and ready_payload is None:
                        ready_payload = make_ready_payload(context, observed, process)
                        write_json(context.ready_path, ready_payload)
                        print_info(f"Detected readiness marker: {observed}")

                    if ready_payload is None and line_contains_file_lock(line):
                        file_lock_detected = True
                        print_info("Detected file-lock/build-lock marker. Restarting after cleanup.")
                        process.kill()
                        break
            except KeyboardInterrupt:
                print_info("Received interrupt. Stopping bridge service.")
                process.terminate()
                process.wait(timeout=10)
                raise
            finally:
                exit_code = process.wait()

        if ready_payload is not None:
            write_json(context.ready_path, make_stopped_payload(context, ready_payload, exit_code))

        if file_lock_detected and attempt <= retry_limit:
            cleanup_details = cleanup_bridge_processes(context)
            print_info(f"Retry cleanup summary: {json.dumps(cleanup_details, ensure_ascii=False)}")
            continue

        if file_lock_detected and attempt > retry_limit:
            print_info("File-lock retries exhausted.")
            return exit_code or 1

        if exit_code != 0 and ready_payload is None:
            print_info(f"Bridge service exited before readiness with code {exit_code}.")
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
    consecutive_successes = 0
    last_http_status: int | None = None
    last_http_excerpt: str | None = None
    marker_seen = False
    observed_marker: str | None = None
    ready_file_seen = False

    while time.monotonic() <= deadline:
        if context.ready_path.exists():
            ready_file_seen = True
            try:
                ready_payload = json.loads(context.ready_path.read_text(encoding="utf-8"))
            except json.JSONDecodeError:
                ready_payload = {}
            observed = ready_payload.get("observed_listen_url")
            if isinstance(observed, str) and observed:
                observed_marker = observed
                marker_seen = True

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
                },
            )
            return 0

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
        },
    )
    return 1


def command_cleanup(args: argparse.Namespace) -> int:
    context = build_context(args)
    print(json.dumps(cleanup_bridge_processes(context), indent=2, ensure_ascii=False))
    return 0


def command_run(args: argparse.Namespace) -> int:
    context = build_context(args)
    return stream_run(context, retry_limit=args.file_lock_retries)


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

    run_parser = subparsers.add_parser("run", help="Clean residual processes and run the bridge service in the foreground.")
    add_shared_run_arguments(run_parser, include_project=True)
    run_parser.add_argument(
        "--file-lock-retries",
        type=int,
        default=1,
        help="Number of automatic retries when MSB3026 or another file-lock marker appears. Default: 1",
    )
    run_parser.set_defaults(func=command_run)

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
