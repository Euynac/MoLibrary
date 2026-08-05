#!/usr/bin/env python3
"""Shared state and user-level dependency skill paths."""

from __future__ import annotations

import os
from pathlib import Path

SKILL_ROOT = Path(__file__).resolve().parents[1]
PROJECT_ROOT = SKILL_ROOT.parents[2]
STATE_DIR = PROJECT_ROOT / ".tmp" / "monica-ui-development"
VARIABLES_JSON_FILE = STATE_DIR / "mudblazor-css-variables.json"
INSPECT_DEPENDENCY_SOURCE_CLI_ENV = "INSPECT_DEPENDENCY_SOURCE_CLI"
INSPECT_DEPENDENCY_SOURCE_RELATIVE_CLI = (
    Path("skills") / "inspect-dependency-source" / "scripts" / "inspect_dependency_source.py"
)


def ensure_state_dir() -> Path:
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    return STATE_DIR


def default_dependency_source_cli() -> Path:
    """Return the canonical Codex user-level CLI location."""

    return Path.home() / ".agents" / INSPECT_DEPENDENCY_SOURCE_RELATIVE_CLI


def dependency_source_cli_candidates() -> tuple[Path, ...]:
    """Return CLI candidates in override, Codex, then Claude precedence order."""

    candidates: list[Path] = []
    override = os.environ.get(INSPECT_DEPENDENCY_SOURCE_CLI_ENV, "").strip()
    if override:
        candidates.append(Path(override).expanduser())

    home = Path.home()
    candidates.extend(
        (
            default_dependency_source_cli(),
            home / ".claude" / INSPECT_DEPENDENCY_SOURCE_RELATIVE_CLI,
        )
    )

    unique: list[Path] = []
    seen: set[str] = set()
    for candidate in candidates:
        absolute = Path(os.path.abspath(candidate))
        key = os.path.normcase(str(absolute))
        if key not in seen:
            seen.add(key)
            unique.append(absolute)
    return tuple(unique)
