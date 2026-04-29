#!/usr/bin/env python3
"""Shared state and dependency paths for the mo-ui-development skill."""

from __future__ import annotations

from datetime import datetime, timezone
from pathlib import Path

SKILL_ROOT = Path(__file__).resolve().parents[1]
PROJECT_ROOT = SKILL_ROOT.parents[2]
STATE_DIR = PROJECT_ROOT / ".tmp" / "mo-ui-development"
VARIABLES_JSON_FILE = STATE_DIR / "mudblazor-css-variables.json"
THIRD_PARTY_CATALOG_STATE_DIR = PROJECT_ROOT / ".tmp" / "third-party-source-catalog" / "state"
THIRD_PARTY_CATALOG_FILE = THIRD_PARTY_CATALOG_STATE_DIR / "catalog.json"
THIRD_PARTY_SOURCE_CATALOG_SCRIPT = (
    SKILL_ROOT.parent / "third-party-source-catalog" / "scripts" / "source_catalog.py"
)


def utc_now_text() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def ensure_state_dir() -> Path:
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    return STATE_DIR
