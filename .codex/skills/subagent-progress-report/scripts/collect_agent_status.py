#!/usr/bin/env python3
"""Summarize the latest status from one main-agent session root."""

from __future__ import annotations

import argparse
import json
import re
from dataclasses import asdict, dataclass
from pathlib import Path

from _session_common import resolve_project_tmp_dir

ENTRY_PATTERN = re.compile(
    r"^\[(?P<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2} [+-]\d{2}:\d{2})\]\s*(?P<message>.*)$"
)
ROOT_PATTERN = re.compile(r"^(?P<stamp>\d{8}-\d{6})-agent-session$")


@dataclass
class SessionStatus:
    agent_name: str
    session_name: str
    session_dir: str
    log_path: str
    session_stamp: str
    latest_timestamp: str | None
    state: str
    message: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Scan one agent-session root and summarize the newest entry from each agent.log.",
    )
    parser.add_argument(
        "--base-dir",
        default=".",
        help="Starting directory used to locate the current project root.",
    )
    parser.add_argument(
        "--session-root",
        help="Optional explicit session root. If omitted, use the newest .tmp/*-agent-session.",
    )
    parser.add_argument(
        "--agent-name",
        action="append",
        default=[],
        help="Filter by agent name. Repeat for multiple names.",
    )
    parser.add_argument(
        "--latest-per-agent",
        action="store_true",
        help="Return only the newest session for each agent.",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Emit JSON instead of text.",
    )
    return parser.parse_args()


def classify_state(message: str) -> str:
    lowered = message.lower()
    if "needs input" in lowered or "needs_input" in lowered:
        return "needs_input"
    if "blocked" in lowered:
        return "blocked"
    if "completed" in lowered or "complete" in lowered:
        return "completed"
    return "in_progress"


def first_entry(log_path: Path) -> tuple[str | None, str]:
    if not log_path.exists():
        return None, ""
    with log_path.open("r", encoding="utf-8") as handle:
        first_line = handle.readline().rstrip("\n")
        if not first_line:
            return None, ""
        match = ENTRY_PATTERN.match(first_line)
        if not match:
            return None, first_line
        return match.group("timestamp"), match.group("message").strip()


def resolve_session_root(base_dir: Path, explicit_root: str | None) -> Path:
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


def iter_sessions(session_root: Path, filters: set[str]) -> list[SessionStatus]:
    if not session_root.exists():
        return []

    results: list[SessionStatus] = []
    for path in sorted(session_root.iterdir()):
        if not path.is_dir():
            continue
        agent_name = path.name
        stamp = session_root.name

        if filters and agent_name not in filters:
            continue

        log_path = path / "agent.log"
        latest_timestamp, message = first_entry(log_path)
        results.append(
            SessionStatus(
                agent_name=agent_name,
                session_name=path.name,
                session_dir=str(path),
                log_path=str(log_path),
                session_stamp=stamp,
                latest_timestamp=latest_timestamp,
                state=classify_state(message),
                message=message,
            )
        )
    return results


def latest_per_agent(items: list[SessionStatus]) -> list[SessionStatus]:
    newest: dict[str, SessionStatus] = {}
    for item in items:
        current = newest.get(item.agent_name)
        if current is None or item.session_stamp > current.session_stamp:
            newest[item.agent_name] = item
    return sorted(
        newest.values(),
        key=lambda item: (item.session_stamp, item.latest_timestamp or ""),
        reverse=True,
    )


def render_text(items: list[SessionStatus], session_root: Path) -> str:
    if not items:
        return f"No session logs found under {session_root}"

    lines = [f"Session root: {session_root}"]
    for item in items:
        timestamp = item.latest_timestamp or "no-entry"
        message = item.message or "(empty log)"
        lines.append(
            f"- {item.agent_name} | {item.state} | {timestamp} | {item.session_dir}"
        )
        lines.append(f"  {message}")
    return "\n".join(lines)


def main() -> int:
    args = parse_args()
    session_root = resolve_session_root(Path(args.base_dir).resolve(), args.session_root)
    filters = set(args.agent_name)
    items = iter_sessions(session_root, filters)
    if args.latest_per_agent:
        items = latest_per_agent(items)

    if args.json:
        print(
            json.dumps(
                {
                    "session_root": str(session_root),
                    "sessions": [asdict(item) for item in items],
                },
                ensure_ascii=True,
                indent=2,
            )
        )
    else:
        print(render_text(items, session_root))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
