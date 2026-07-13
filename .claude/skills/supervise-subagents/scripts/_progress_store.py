#!/usr/bin/env python3
"""Validated storage primitives for delegated-agent supervision sessions."""

from __future__ import annotations

import json
import os
import re
import secrets
import tempfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

SCHEMA_VERSION = 1
PROJECT_MARKERS = (".git", ".agents", ".claude", "AGENTS.md", "CLAUDE.md")
SESSION_PATTERN = re.compile(
    r"^(?P<timestamp>\d{8}-\d{6})-(?P<nonce>[0-9a-f]{8})-agent-session$"
)
NATIVE_STATUSES = {
    "pending",
    "running",
    "idle",
    "completed",
    "failed",
    "interrupted",
    "unknown",
}
TERMINAL_NATIVE_STATUSES = {"completed", "failed", "interrupted"}
PROGRESS_STATES = {"in_progress", "blocked", "needs_input", "completed"}
FRESHNESS_SECONDS = 90
MAX_MESSAGE_LENGTH = 8_000


class ProgressStoreError(ValueError):
    """Raised when progress data or a requested path violates the store contract."""


def utc_now() -> str:
    """Return a stable UTC timestamp suitable for persisted records."""

    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def parse_timestamp(value: str | None) -> datetime | None:
    if not value:
        return None
    try:
        return datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError:
        return None


def resolve_project_root(start_dir: str | Path | None = None) -> Path:
    current = Path(start_dir or Path.cwd()).expanduser().resolve()
    if current.is_file():
        current = current.parent
    for candidate in (current, *current.parents):
        if any((candidate / marker).exists() for marker in PROJECT_MARKERS):
            return candidate
    raise ProgressStoreError(f"could not locate a project root from: {current}")


def project_tmp_dir(project_root: Path) -> Path:
    return project_root / ".tmp"


def normalize_agent_name(value: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9._-]+", "-", value.strip())
    cleaned = cleaned.strip("-._")
    if not cleaned:
        raise ProgressStoreError("agent name must contain at least one letter or digit")
    if len(cleaned) > 96:
        raise ProgressStoreError("normalized agent name must not exceed 96 characters")
    return cleaned


def validate_message(value: str, field_name: str = "message") -> str:
    cleaned = value.strip()
    if not cleaned:
        raise ProgressStoreError(f"{field_name} must not be empty")
    if len(cleaned) > MAX_MESSAGE_LENGTH:
        raise ProgressStoreError(
            f"{field_name} must not exceed {MAX_MESSAGE_LENGTH} characters"
        )
    return cleaned


def read_json(path: Path) -> dict[str, Any]:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise ProgressStoreError(f"required file does not exist: {path}") from exc
    except (OSError, json.JSONDecodeError) as exc:
        raise ProgressStoreError(f"could not read valid JSON from: {path}") from exc
    if not isinstance(value, dict):
        raise ProgressStoreError(f"expected a JSON object in: {path}")
    return value


def atomic_write_json(path: Path, value: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temp_name = tempfile.mkstemp(
        prefix=f".{path.name}.", suffix=".tmp", dir=path.parent
    )
    temp_path = Path(temp_name)
    try:
        with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as handle:
            json.dump(value, handle, ensure_ascii=False, indent=2, sort_keys=True)
            handle.write("\n")
            handle.flush()
            os.fsync(handle.fileno())
        os.replace(temp_path, path)
    finally:
        temp_path.unlink(missing_ok=True)


def create_session(base_dir: str | Path, task: str) -> dict[str, Any]:
    project_root = resolve_project_root(base_dir)
    tmp_dir = project_tmp_dir(project_root)
    tmp_dir.mkdir(parents=True, exist_ok=True)
    task = validate_message(task, "task")

    while True:
        timestamp = datetime.now().astimezone().strftime("%Y%m%d-%H%M%S")
        session_name = f"{timestamp}-{secrets.token_hex(4)}-agent-session"
        session_root = tmp_dir / session_name
        try:
            session_root.mkdir()
            break
        except FileExistsError:
            continue

    (session_root / "agents").mkdir()
    session = {
        "schema_version": SCHEMA_VERSION,
        "session_id": session_name,
        "task": task,
        "created_at": utc_now(),
        "project_root": str(project_root),
    }
    atomic_write_json(session_root / "session.json", session)
    return {**session, "session_root": str(session_root)}


def validate_session_root(value: str | Path) -> Path:
    supplied = Path(value).expanduser()
    try:
        session_root = supplied.resolve(strict=True)
    except FileNotFoundError as exc:
        raise ProgressStoreError(f"session root does not exist: {supplied}") from exc

    if not session_root.is_dir() or not SESSION_PATTERN.fullmatch(session_root.name):
        raise ProgressStoreError(f"invalid session root name: {session_root}")
    if session_root.parent.name != ".tmp":
        raise ProgressStoreError("session root must be a direct child of a project .tmp directory")
    project_root = session_root.parent.parent
    if not any((project_root / marker).exists() for marker in PROJECT_MARKERS):
        raise ProgressStoreError(f"session root is not inside a recognized project: {session_root}")

    session = read_json(session_root / "session.json")
    if session.get("schema_version") != SCHEMA_VERSION:
        raise ProgressStoreError(f"unsupported session schema in: {session_root}")
    if session.get("session_id") != session_root.name:
        raise ProgressStoreError("session metadata does not match the session directory")
    recorded_root = Path(str(session.get("project_root", ""))).expanduser().resolve()
    if recorded_root != project_root.resolve():
        raise ProgressStoreError("session metadata references a different project root")
    return session_root


def resolve_session_root(
    base_dir: str | Path | None = None,
    explicit_root: str | Path | None = None,
) -> Path:
    if explicit_root is not None:
        return validate_session_root(explicit_root)

    project_root = resolve_project_root(base_dir)
    tmp_dir = project_tmp_dir(project_root)
    if not tmp_dir.exists():
        raise ProgressStoreError(f"no supervision sessions found under: {tmp_dir}")
    candidates = [
        path
        for path in tmp_dir.iterdir()
        if path.is_dir() and SESSION_PATTERN.fullmatch(path.name)
    ]
    for candidate in sorted(candidates, key=lambda item: item.name, reverse=True):
        try:
            return validate_session_root(candidate)
        except ProgressStoreError:
            continue
    raise ProgressStoreError(f"no valid supervision sessions found under: {tmp_dir}")


def session_root_by_id(project_root: Path, session_id: str) -> Path:
    if not SESSION_PATTERN.fullmatch(session_id):
        raise ProgressStoreError("invalid session id")
    return validate_session_root(project_tmp_dir(project_root) / session_id)


def agent_paths(session_root: Path, agent_name: str) -> tuple[str, Path, Path, Path]:
    normalized = normalize_agent_name(agent_name)
    agents_root = session_root / "agents"
    agent_dir = agents_root / normalized
    resolved_parent = agent_dir.parent.resolve()
    if resolved_parent != agents_root.resolve():
        raise ProgressStoreError("agent directory escaped the session agents directory")
    return normalized, agent_dir, agent_dir / "agent.json", agent_dir / "events.jsonl"


def register_agent(
    session_root_value: str | Path,
    agent_name: str,
    agent_id: str,
    task: str,
) -> dict[str, Any]:
    session_root = validate_session_root(session_root_value)
    normalized, agent_dir, manifest_path, _ = agent_paths(session_root, agent_name)
    agent_id = validate_message(agent_id, "agent id")
    task = validate_message(task, "task")
    agent_dir.mkdir(parents=False, exist_ok=True)

    if manifest_path.exists():
        existing = read_json(manifest_path)
        if existing.get("agent_id") != agent_id:
            raise ProgressStoreError(
                f"agent name {normalized!r} is already registered to another agent id"
            )
        return existing

    now = utc_now()
    manifest = {
        "schema_version": SCHEMA_VERSION,
        "agent_name": normalized,
        "agent_id": agent_id,
        "task": task,
        "registered_at": now,
        "native_status": "pending",
        "native_updated_at": now,
        "closed_at": None,
        "close_reason": None,
    }
    atomic_write_json(manifest_path, manifest)
    return manifest


def load_agent_manifest(session_root: Path, agent_name: str) -> tuple[Path, dict[str, Any]]:
    normalized, _, manifest_path, _ = agent_paths(session_root, agent_name)
    manifest = read_json(manifest_path)
    if manifest.get("schema_version") != SCHEMA_VERSION:
        raise ProgressStoreError(f"unsupported agent schema for: {normalized}")
    if manifest.get("agent_name") != normalized:
        raise ProgressStoreError(f"agent metadata does not match directory: {normalized}")
    return manifest_path, manifest


def update_lifecycle(
    session_root_value: str | Path,
    agent_name: str,
    status: str,
) -> dict[str, Any]:
    if status not in NATIVE_STATUSES:
        raise ProgressStoreError(f"invalid native lifecycle status: {status}")
    session_root = validate_session_root(session_root_value)
    manifest_path, manifest = load_agent_manifest(session_root, agent_name)
    previous = str(manifest.get("native_status", "unknown"))
    if previous in TERMINAL_NATIVE_STATUSES and status != previous:
        raise ProgressStoreError(
            f"cannot transition terminal native status from {previous} to {status}"
        )
    if manifest.get("closed_at") and status != previous:
        raise ProgressStoreError("cannot change lifecycle after the agent has been closed")
    manifest["native_status"] = status
    manifest["native_updated_at"] = utc_now()
    atomic_write_json(manifest_path, manifest)
    return manifest


def parse_event_line(line: str, after_sequence: int = 0) -> dict[str, Any] | None:
    if not line.strip():
        return None
    try:
        event = json.loads(line)
    except json.JSONDecodeError:
        return None
    if not isinstance(event, dict):
        return None
    sequence = event.get("sequence")
    if (
        event.get("schema_version") != SCHEMA_VERSION
        or not isinstance(sequence, int)
        or sequence <= after_sequence
        or event.get("state") not in PROGRESS_STATES
    ):
        return None
    return event


def load_events(events_path: Path, after_sequence: int = 0) -> list[dict[str, Any]]:
    if not events_path.exists():
        return []
    events: list[dict[str, Any]] = []
    try:
        with events_path.open("r", encoding="utf-8") as handle:
            for line in handle:
                event = parse_event_line(line, after_sequence)
                if event is not None:
                    events.append(event)
    except OSError as exc:
        raise ProgressStoreError(f"could not read events: {events_path}") from exc
    return sorted(events, key=lambda item: int(item["sequence"]))


def load_latest_event(events_path: Path) -> dict[str, Any] | None:
    if not events_path.exists():
        return None
    try:
        with events_path.open("rb") as handle:
            handle.seek(0, os.SEEK_END)
            position = handle.tell()
            remainder = b""
            while position > 0:
                chunk_size = min(position, 8_192)
                position -= chunk_size
                handle.seek(position)
                remainder = handle.read(chunk_size) + remainder
                lines = remainder.split(b"\n")
                remainder = lines[0]
                for raw_line in reversed(lines[1:]):
                    event = parse_event_line(raw_line.decode("utf-8", errors="replace"))
                    if event is not None:
                        return event
            event = parse_event_line(remainder.decode("utf-8", errors="replace"))
            if event is not None:
                return event
    except OSError as exc:
        raise ProgressStoreError(f"could not read events: {events_path}") from exc
    return None


def append_progress_event(
    session_root_value: str | Path,
    agent_name: str,
    state: str,
    message: str,
) -> dict[str, Any]:
    if state not in PROGRESS_STATES:
        raise ProgressStoreError(f"invalid progress state: {state}")
    message = validate_message(message)
    session_root = validate_session_root(session_root_value)
    _, _ = load_agent_manifest(session_root, agent_name)
    _, _, _, events_path = agent_paths(session_root, agent_name)
    latest = load_latest_event(events_path)
    if latest and latest["state"] == "completed" and state != "completed":
        raise ProgressStoreError("cannot report non-terminal progress after completion")

    event = {
        "schema_version": SCHEMA_VERSION,
        "sequence": int(latest["sequence"]) + 1 if latest else 1,
        "timestamp": utc_now(),
        "state": state,
        "message": message,
    }
    events_path.parent.mkdir(parents=True, exist_ok=True)
    encoded = json.dumps(event, ensure_ascii=False, separators=(",", ":")) + "\n"
    needs_separator = False
    if events_path.exists() and events_path.stat().st_size:
        with events_path.open("rb") as existing:
            existing.seek(-1, os.SEEK_END)
            needs_separator = existing.read(1) != b"\n"
    with events_path.open("a", encoding="utf-8", newline="\n") as handle:
        if needs_separator:
            handle.write("\n")
        handle.write(encoded)
        handle.flush()
        os.fsync(handle.fileno())
    return event


def close_agent(
    session_root_value: str | Path,
    agent_name: str,
    reason: str,
) -> dict[str, Any]:
    reason = validate_message(reason, "reason")
    session_root = validate_session_root(session_root_value)
    manifest_path, manifest = load_agent_manifest(session_root, agent_name)
    if manifest.get("closed_at"):
        return manifest
    native_status = str(manifest.get("native_status", "unknown"))
    if native_status not in TERMINAL_NATIVE_STATUSES:
        raise ProgressStoreError(
            f"cannot close agent while native lifecycle is {native_status}"
        )
    manifest["closed_at"] = utc_now()
    manifest["close_reason"] = reason
    atomic_write_json(manifest_path, manifest)
    return manifest


def is_fresh(timestamp: str | None, now: datetime | None = None) -> bool:
    parsed = parse_timestamp(timestamp)
    if parsed is None:
        return False
    current = now or datetime.now(timezone.utc)
    return (current - parsed).total_seconds() <= FRESHNESS_SECONDS


def agent_snapshot(
    agent_dir: Path,
    include_events: bool = False,
    after_sequence: int = 0,
) -> dict[str, Any]:
    manifest = read_json(agent_dir / "agent.json")
    events_path = agent_dir / "events.jsonl"
    events = load_events(events_path, after_sequence) if include_events else []
    latest = load_latest_event(events_path)
    snapshot = {
        **manifest,
        "reported_state": latest.get("state") if latest else None,
        "latest_message": latest.get("message") if latest else None,
        "reported_updated_at": latest.get("timestamp") if latest else None,
        "latest_sequence": latest.get("sequence") if latest else 0,
        "native_fresh": is_fresh(manifest.get("native_updated_at")),
        "reported_fresh": is_fresh(latest.get("timestamp") if latest else None),
    }
    if include_events:
        snapshot["events"] = events
    return snapshot


def session_snapshot(
    session_root_value: str | Path,
    include_events: bool = False,
    agent_names: set[str] | None = None,
) -> dict[str, Any]:
    session_root = validate_session_root(session_root_value)
    session = read_json(session_root / "session.json")
    agents: list[dict[str, Any]] = []
    normalized_filters = (
        {normalize_agent_name(name) for name in agent_names} if agent_names else None
    )
    agents_root = session_root / "agents"
    for agent_dir in sorted(agents_root.iterdir() if agents_root.exists() else []):
        if not agent_dir.is_dir() or not (agent_dir / "agent.json").exists():
            continue
        if normalized_filters and agent_dir.name not in normalized_filters:
            continue
        agents.append(agent_snapshot(agent_dir, include_events))

    counts: dict[str, int] = {}
    for agent in agents:
        status = str(agent.get("native_status", "unknown"))
        counts[status] = counts.get(status, 0) + 1
    return {
        **session,
        "session_root": str(session_root),
        "agents": agents,
        "counts": counts,
    }


def list_sessions(project_root_value: str | Path) -> list[dict[str, Any]]:
    project_root = resolve_project_root(project_root_value)
    tmp_dir = project_tmp_dir(project_root)
    if not tmp_dir.exists():
        return []
    sessions: list[dict[str, Any]] = []
    for path in sorted(tmp_dir.iterdir(), key=lambda item: item.name, reverse=True):
        if not path.is_dir() or not SESSION_PATTERN.fullmatch(path.name):
            continue
        try:
            snapshot = session_snapshot(path, include_events=False)
        except ProgressStoreError:
            continue
        sessions.append(snapshot)
    return sessions
