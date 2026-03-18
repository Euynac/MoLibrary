#!/usr/bin/env python3
"""Prepend timestamped entries to a subagent agent.log file."""

from __future__ import annotations

import argparse
from datetime import datetime
from pathlib import Path


def make_timestamp() -> str:
    value = datetime.now().astimezone().strftime("%z")
    timezone = f"{value[:3]}:{value[3:]}" if len(value) == 5 else value
    return datetime.now().astimezone().strftime("%Y-%m-%d %H:%M:%S ") + timezone


def format_message(message: str) -> str:
    lines = [line.rstrip() for line in message.replace("\r\n", "\n").replace("\r", "\n").split("\n")]
    while lines and not lines[-1]:
        lines.pop()
    if not lines:
        return ""
    head = lines[0]
    tail = [f"    {line}" if line else "" for line in lines[1:]]
    if not tail:
        return head
    return "\n".join([head, *tail])


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Prepend a timestamped message to <session-dir>/agent.log.",
    )
    parser.add_argument(
        "--session-dir",
        required=True,
        help="Directory that contains agent.log.",
    )
    parser.add_argument(
        "--message",
        required=True,
        help="Message to write to the top of agent.log.",
    )
    args = parser.parse_args()

    session_dir = Path(args.session_dir).resolve()
    session_dir.mkdir(parents=True, exist_ok=True)
    log_path = session_dir / "agent.log"
    existing = log_path.read_text(encoding="utf-8") if log_path.exists() else ""
    message = format_message(args.message)
    if not message:
        raise SystemExit("message must not be empty")

    entry = f"[{make_timestamp()}] {message}\n"
    if existing:
        entry = entry + "\n" + existing

    log_path.write_text(entry, encoding="utf-8")
    print(str(log_path))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
