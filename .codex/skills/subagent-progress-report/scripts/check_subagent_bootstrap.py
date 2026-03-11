#!/usr/bin/env python3
"""Verify that a delegated sub-agent initialized its session folder and log."""

from __future__ import annotations

import argparse
import json
import time
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Wait for <session-root>/<agent-name>/agent.log to appear and receive its first entry.",
    )
    parser.add_argument(
        "--session-root",
        required=True,
        help="Main-agent session root containing per-agent child directories.",
    )
    parser.add_argument(
        "--agent-name",
        required=True,
        help="Expected sub-agent directory name.",
    )
    parser.add_argument(
        "--timeout-seconds",
        type=float,
        default=60.0,
        help="Maximum wait time before classifying bootstrap state.",
    )
    parser.add_argument(
        "--poll-seconds",
        type=float,
        default=2.0,
        help="Polling interval while waiting.",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Emit JSON instead of text.",
    )
    return parser.parse_args()


def inspect_state(session_root: Path, agent_name: str) -> dict[str, object]:
    session_dir = session_root / agent_name
    log_path = session_dir / "agent.log"
    folder_exists = session_dir.is_dir()
    log_exists = log_path.is_file()
    latest_entry = ""
    has_log_entry = False

    if log_exists:
        try:
            latest_entry = log_path.read_text(encoding="utf-8").splitlines()[0].strip()
            has_log_entry = bool(latest_entry)
        except IndexError:
            latest_entry = ""

    if folder_exists and has_log_entry:
        status = "bootstrapped"
        action = "continue"
    elif folder_exists:
        status = "directory_only"
        action = "follow_up"
    else:
        status = "missing"
        action = "redelegate"

    return {
        "session_root": str(session_root),
        "session_dir": str(session_dir),
        "log_path": str(log_path),
        "agent_name": agent_name,
        "folder_exists": folder_exists,
        "log_exists": log_exists,
        "has_log_entry": has_log_entry,
        "latest_entry": latest_entry,
        "status": status,
        "recommended_action": action,
    }


def render_text(result: dict[str, object]) -> str:
    return (
        f"{result['agent_name']} | {result['status']} | {result['recommended_action']} | "
        f"{result['session_dir']}"
    )


def main() -> int:
    args = parse_args()
    session_root = Path(args.session_root).resolve()
    deadline = time.monotonic() + max(args.timeout_seconds, 0.0)
    poll_seconds = max(args.poll_seconds, 0.1)

    result = inspect_state(session_root, args.agent_name)
    while time.monotonic() < deadline and result["status"] != "bootstrapped":
        time.sleep(poll_seconds)
        result = inspect_state(session_root, args.agent_name)

    if args.json:
        print(json.dumps(result, ensure_ascii=True, indent=2))
    else:
        print(render_text(result))

    return 0 if result["status"] == "bootstrapped" else 1


if __name__ == "__main__":
    raise SystemExit(main())
