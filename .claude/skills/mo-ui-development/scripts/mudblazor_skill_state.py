#!/usr/bin/env python3
"""Shared temporary state helpers for the mo-ui-development skill."""

from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path

SKILL_ROOT = Path(__file__).resolve().parents[1]
PROJECT_ROOT = SKILL_ROOT.parents[2]
STATE_DIR = PROJECT_ROOT / ".tmp" / "mo-ui-development"
SOURCE_CONFIG_FILE = STATE_DIR / "mudblazor-source.json"
VARIABLES_JSON_FILE = STATE_DIR / "mudblazor-css-variables.json"


def utc_now_text() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def ensure_state_dir() -> Path:
    STATE_DIR.mkdir(parents=True, exist_ok=True)
    return STATE_DIR


def load_source_config() -> tuple[str | None, str]:
    if not SOURCE_CONFIG_FILE.exists():
        return None, f"project temp config file is missing: {SOURCE_CONFIG_FILE}"

    try:
        payload = json.loads(SOURCE_CONFIG_FILE.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001
        return None, f"project temp config file is invalid: {SOURCE_CONFIG_FILE} ({exc})"

    source_root = str(payload.get("source_root", "")).strip()
    if not source_root:
        return None, f"project temp config file does not contain a valid source_root: {SOURCE_CONFIG_FILE}"

    return source_root, f"project temp config {SOURCE_CONFIG_FILE}"


def save_source_config(source_root: str) -> Path:
    normalized_root = source_root.strip()
    if not normalized_root:
        raise ValueError("source_root cannot be empty.")

    ensure_state_dir()
    payload = {
        "source_root": normalized_root,
        "updated_at_utc": utc_now_text(),
    }
    SOURCE_CONFIG_FILE.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    return SOURCE_CONFIG_FILE

