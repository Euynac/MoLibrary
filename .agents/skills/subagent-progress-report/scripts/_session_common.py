#!/usr/bin/env python3
"""Shared helpers for subagent session path resolution and naming."""

from __future__ import annotations

import re
from pathlib import Path

PROJECT_MARKERS = (".git", ".agents", "AGENTS.md")
ROOT_PATTERN = re.compile(r"^(?P<stamp>\d{8}-\d{6})-agent-session$")
CLOSED_PREFIX = "(Closed)"


def resolve_project_root(start_dir: str | Path | None = None) -> Path:
    current = Path(start_dir or Path.cwd()).resolve()
    for candidate in (current, *current.parents):
        if any((candidate / marker).exists() for marker in PROJECT_MARKERS):
            return candidate
    return current


def resolve_project_tmp_dir(start_dir: str | Path | None = None) -> tuple[Path, Path]:
    project_root = resolve_project_root(start_dir)
    return project_root, project_root / ".tmp"


def sanitize_agent_name(value: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9._-]+", "-", value.strip())
    cleaned = cleaned.strip("-._")
    return cleaned or "subagent"


def strip_closed_prefix(value: str) -> str:
    cleaned = value.strip()
    while cleaned.startswith(CLOSED_PREFIX):
        cleaned = cleaned[len(CLOSED_PREFIX):].lstrip()
    return cleaned


def normalize_agent_name(value: str) -> str:
    return sanitize_agent_name(strip_closed_prefix(value))


def is_closed_agent_dir(name: str) -> bool:
    return name.startswith(CLOSED_PREFIX)


def make_closed_agent_dir_name(agent_name: str) -> str:
    return f"{CLOSED_PREFIX}{normalize_agent_name(agent_name)}"


def resolve_session_root(base_dir: str | Path, explicit_root: str | Path | None = None) -> Path:
    if explicit_root:
        return Path(explicit_root).resolve()

    _, tmp_dir = resolve_project_tmp_dir(base_dir)
    if not tmp_dir.exists():
        return tmp_dir / "missing-agent-session"

    candidates = [
        path for path in tmp_dir.iterdir()
        if path.is_dir() and ROOT_PATTERN.match(path.name)
    ]
    if not candidates:
        return tmp_dir / "missing-agent-session"
    return sorted(candidates, reverse=True)[0]
