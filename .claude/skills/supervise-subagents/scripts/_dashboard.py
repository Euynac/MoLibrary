#!/usr/bin/env python3
"""Local, read-only dashboard server for delegated-agent supervision."""

from __future__ import annotations

import json
import os
import secrets
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any
from urllib.parse import parse_qs, unquote, urlparse

from _progress_store import (
    ProgressStoreError,
    agent_paths,
    agent_snapshot,
    list_sessions,
    project_tmp_dir,
    read_json,
    resolve_project_root,
    session_root_by_id,
    session_snapshot,
    utc_now,
)

DASHBOARD_STATE_FILE = "supervise-subagents-dashboard.json"
DASHBOARD_LOG_FILE = "supervise-subagents-dashboard.log"
START_TIMEOUT_SECONDS = 8.0
_SPAWNED_PROCESSES: dict[str, subprocess.Popen[Any]] = {}


def _reap_spawned_process(
    instance_id: str,
    process: subprocess.Popen[Any],
    *,
    terminate: bool = False,
) -> None:
    """Remove a tracked child and ensure its OS process is reaped."""
    _SPAWNED_PROCESSES.pop(instance_id, None)
    if terminate and process.poll() is None:
        process.terminate()
    try:
        process.wait(timeout=1.0)
    except subprocess.TimeoutExpired:
        process.kill()
        process.wait(timeout=1.0)


def state_path(project_root: Path) -> Path:
    return project_tmp_dir(project_root) / DASHBOARD_STATE_FILE


def log_path(project_root: Path) -> Path:
    return project_tmp_dir(project_root) / DASHBOARD_LOG_FILE


def read_dashboard_state(project_root: Path) -> dict[str, Any] | None:
    path = state_path(project_root)
    if not path.exists():
        return None
    try:
        return read_json(path)
    except ProgressStoreError:
        return None


def request_json(
    url: str,
    *,
    method: str = "GET",
    headers: dict[str, str] | None = None,
    timeout: float = 1.0,
) -> dict[str, Any] | None:
    request = urllib.request.Request(url, method=method, headers=headers or {})
    try:
        with urllib.request.urlopen(request, timeout=timeout) as response:
            payload = json.loads(response.read().decode("utf-8"))
    except (OSError, urllib.error.URLError, json.JSONDecodeError):
        return None
    return payload if isinstance(payload, dict) else None


def dashboard_health(state: dict[str, Any]) -> dict[str, Any] | None:
    url = state.get("url")
    instance_id = state.get("instance_id")
    if not isinstance(url, str) or not isinstance(instance_id, str):
        return None
    payload = request_json(f"{url}/api/health")
    if payload and payload.get("instance_id") == instance_id:
        return payload
    return None


def public_dashboard_state(state: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in state.items() if key != "control_token"}


def dashboard_status(project_root_value: str | Path) -> dict[str, Any]:
    project_root = resolve_project_root(project_root_value)
    state = read_dashboard_state(project_root)
    if not state:
        return {"status": "stopped", "project_root": str(project_root)}
    health = dashboard_health(state)
    if not health:
        return {
            "status": "stale",
            "project_root": str(project_root),
            **public_dashboard_state(state),
        }
    return {
        "status": "running",
        "project_root": str(project_root),
        **public_dashboard_state(state),
    }


def start_dashboard(
    project_root_value: str | Path,
    cli_path: Path,
    host: str = "127.0.0.1",
    port: int = 0,
) -> dict[str, Any]:
    if host != "127.0.0.1":
        raise ProgressStoreError("dashboard host must be 127.0.0.1")
    if not 0 <= port <= 65535:
        raise ProgressStoreError("dashboard port must be between 0 and 65535")

    project_root = resolve_project_root(project_root_value)
    tmp_dir = project_tmp_dir(project_root)
    tmp_dir.mkdir(parents=True, exist_ok=True)
    existing = read_dashboard_state(project_root)
    if existing and dashboard_health(existing):
        return {
            "status": "reused",
            "project_root": str(project_root),
            **public_dashboard_state(existing),
        }
    state_path(project_root).unlink(missing_ok=True)

    instance_id = secrets.token_hex(12)
    control_token = secrets.token_urlsafe(32)
    command = [
        sys.executable,
        str(cli_path.resolve()),
        "_serve",
        "--base-dir",
        str(project_root),
        "--host",
        host,
        "--port",
        str(port),
        "--instance-id",
        instance_id,
        "--control-token",
        control_token,
    ]
    creation_flags = 0
    popen_kwargs: dict[str, Any] = {"start_new_session": True}
    if os.name == "nt":
        creation_flags = subprocess.CREATE_NEW_PROCESS_GROUP | subprocess.DETACHED_PROCESS
        popen_kwargs = {"creationflags": creation_flags}

    with log_path(project_root).open("a", encoding="utf-8") as output:
        process = subprocess.Popen(
            command,
            stdin=subprocess.DEVNULL,
            stdout=output,
            stderr=subprocess.STDOUT,
            cwd=project_root,
            close_fds=True,
            **popen_kwargs,
        )
    _SPAWNED_PROCESSES[instance_id] = process

    deadline = time.monotonic() + START_TIMEOUT_SECONDS
    while time.monotonic() < deadline:
        state = read_dashboard_state(project_root)
        if state and state.get("instance_id") == instance_id and dashboard_health(state):
            return {
                "status": "started",
                "project_root": str(project_root),
                **public_dashboard_state(state),
            }
        if process.poll() is not None:
            _reap_spawned_process(instance_id, process)
            break
        time.sleep(0.1)

    if instance_id in _SPAWNED_PROCESSES:
        _reap_spawned_process(instance_id, process, terminate=True)
    log_tail = ""
    try:
        log_tail = log_path(project_root).read_text(encoding="utf-8")[-2_000:]
    except OSError:
        pass
    raise ProgressStoreError(
        "dashboard did not become ready within "
        f"{START_TIMEOUT_SECONDS:.0f} seconds; log tail: {log_tail.strip() or '(empty)'}"
    )


def stop_dashboard(project_root_value: str | Path) -> dict[str, Any]:
    project_root = resolve_project_root(project_root_value)
    state = read_dashboard_state(project_root)
    if not state:
        return {"status": "already_stopped", "project_root": str(project_root)}
    if not dashboard_health(state):
        state_path(project_root).unlink(missing_ok=True)
        return {"status": "stale_state_removed", "project_root": str(project_root)}

    url = str(state["url"])
    control_token = str(state.get("control_token", ""))
    response = request_json(
        f"{url}/_control/shutdown",
        method="POST",
        headers={"X-Dashboard-Token": control_token},
        timeout=2.0,
    )
    if not response or response.get("status") != "stopping":
        raise ProgressStoreError("dashboard rejected the authenticated stop request")

    deadline = time.monotonic() + 4.0
    while time.monotonic() < deadline:
        if not dashboard_health(state):
            state_path(project_root).unlink(missing_ok=True)
            instance_id = str(state.get("instance_id", ""))
            process = _SPAWNED_PROCESSES.get(instance_id)
            if process is not None:
                _reap_spawned_process(instance_id, process, terminate=True)
            return {"status": "stopped", "project_root": str(project_root)}
        time.sleep(0.1)
    raise ProgressStoreError("dashboard did not stop within 4 seconds")


class DashboardServer(ThreadingHTTPServer):
    daemon_threads = True

    def __init__(
        self,
        address: tuple[str, int],
        project_root: Path,
        assets_root: Path,
        instance_id: str,
        control_token: str,
    ) -> None:
        self.project_root = project_root
        self.assets_root = assets_root
        self.instance_id = instance_id
        self.control_token = control_token
        super().__init__(address, DashboardRequestHandler)


class DashboardRequestHandler(BaseHTTPRequestHandler):
    server: DashboardServer

    def log_message(self, format_string: str, *args: object) -> None:
        sys.stderr.write(
            f"[{self.log_date_time_string()}] {self.address_string()} "
            f"{format_string % args}\n"
        )

    def end_headers(self) -> None:
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header("X-Frame-Options", "DENY")
        self.send_header(
            "Content-Security-Policy",
            "default-src 'self'; script-src 'self'; style-src 'self'; "
            "connect-src 'self'; img-src 'self'; frame-ancestors 'none'; base-uri 'none'",
        )
        super().end_headers()

    def send_json(self, status: HTTPStatus, value: Any) -> None:
        payload = json.dumps(value, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def send_asset(self, name: str, content_type: str) -> None:
        path = self.server.assets_root / name
        try:
            payload = path.read_bytes()
        except OSError:
            self.send_error(HTTPStatus.NOT_FOUND)
            return
        self.send_response(HTTPStatus.OK)
        self.send_header("Content-Type", content_type)
        self.send_header("Cache-Control", "no-cache")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def do_GET(self) -> None:
        parsed = urlparse(self.path)
        path = parsed.path
        try:
            if path == "/api/health":
                self.send_json(
                    HTTPStatus.OK,
                    {
                        "status": "ok",
                        "instance_id": self.server.instance_id,
                        "pid": os.getpid(),
                    },
                )
                return
            if path == "/api/sessions":
                sessions = list_sessions(self.server.project_root)
                summaries = [
                    {
                        "session_id": item["session_id"],
                        "task": item["task"],
                        "created_at": item["created_at"],
                        "counts": item["counts"],
                        "agent_count": len(item["agents"]),
                    }
                    for item in sessions
                ]
                self.send_json(HTTPStatus.OK, {"sessions": summaries})
                return
            if path.startswith("/api/sessions/"):
                self.handle_session_api(path, parse_qs(parsed.query))
                return
            if path in {"/", "/index.html"}:
                self.send_asset("index.html", "text/html; charset=utf-8")
                return
            if path == "/app.js":
                self.send_asset("app.js", "text/javascript; charset=utf-8")
                return
            if path == "/styles.css":
                self.send_asset("styles.css", "text/css; charset=utf-8")
                return
            self.send_error(HTTPStatus.NOT_FOUND)
        except ProgressStoreError as exc:
            self.send_json(HTTPStatus.BAD_REQUEST, {"error": str(exc)})

    def handle_session_api(self, path: str, query: dict[str, list[str]]) -> None:
        parts = [unquote(part) for part in path.strip("/").split("/")]
        if len(parts) < 3 or parts[:2] != ["api", "sessions"]:
            self.send_error(HTTPStatus.NOT_FOUND)
            return
        session_root = session_root_by_id(self.server.project_root, parts[2])
        if len(parts) == 3:
            self.send_json(HTTPStatus.OK, session_snapshot(session_root))
            return
        if len(parts) == 6 and parts[3] == "agents" and parts[5] == "events":
            after_value = query.get("after", ["0"])[0]
            try:
                after = max(int(after_value), 0)
            except ValueError as exc:
                raise ProgressStoreError("after must be a non-negative integer") from exc
            normalized, agent_dir, _, _ = agent_paths(session_root, parts[4])
            if not agent_dir.is_dir():
                raise ProgressStoreError(f"unknown agent: {normalized}")
            snapshot = agent_snapshot(
                agent_dir, include_events=True, after_sequence=after
            )
            self.send_json(
                HTTPStatus.OK,
                {
                    "agent_name": snapshot["agent_name"],
                    "latest_sequence": snapshot["latest_sequence"],
                    "events": snapshot["events"],
                },
            )
            return
        self.send_error(HTTPStatus.NOT_FOUND)

    def do_POST(self) -> None:
        if self.path != "/_control/shutdown":
            self.send_error(HTTPStatus.METHOD_NOT_ALLOWED)
            return
        supplied_token = self.headers.get("X-Dashboard-Token", "")
        if not secrets.compare_digest(supplied_token, self.server.control_token):
            self.send_json(HTTPStatus.FORBIDDEN, {"error": "forbidden"})
            return
        self.send_json(HTTPStatus.OK, {"status": "stopping"})
        threading.Thread(target=self.server.shutdown, daemon=True).start()


def serve_dashboard(
    project_root_value: str | Path,
    host: str,
    port: int,
    instance_id: str,
    control_token: str,
) -> int:
    if host != "127.0.0.1":
        raise ProgressStoreError("dashboard host must be 127.0.0.1")
    project_root = resolve_project_root(project_root_value)
    assets_root = Path(__file__).resolve().parent.parent / "assets" / "dashboard"
    if not assets_root.is_dir():
        raise ProgressStoreError(f"dashboard assets are missing: {assets_root}")

    server = DashboardServer(
        (host, port), project_root, assets_root, instance_id, control_token
    )
    actual_port = int(server.server_address[1])
    state = {
        "schema_version": 1,
        "instance_id": instance_id,
        "control_token": control_token,
        "pid": os.getpid(),
        "host": host,
        "port": actual_port,
        "url": f"http://{host}:{actual_port}",
        "started_at": utc_now(),
    }
    from _progress_store import atomic_write_json

    atomic_write_json(state_path(project_root), state)
    try:
        server.serve_forever(poll_interval=0.2)
    finally:
        server.server_close()
        current = read_dashboard_state(project_root)
        if current and current.get("instance_id") == instance_id:
            state_path(project_root).unlink(missing_ok=True)
    return 0
