#!/usr/bin/env python3
"""Shared helpers for subagent session path resolution."""

from __future__ import annotations

from pathlib import Path

PROJECT_MARKERS = (".git", ".codex", "AGENTS.md")


def resolve_project_root(start_dir: str | Path | None = None) -> Path:
    current = Path(start_dir or Path.cwd()).resolve()
    for candidate in (current, *current.parents):
        if any((candidate / marker).exists() for marker in PROJECT_MARKERS):
            return candidate
    return current


def resolve_project_tmp_dir(start_dir: str | Path | None = None) -> tuple[Path, Path]:
    project_root = resolve_project_root(start_dir)
    return project_root, project_root / ".tmp"
