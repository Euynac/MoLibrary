#!/usr/bin/env python3
"""Mark one or more subagent session directories as closed."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from _session_common import (
    make_closed_agent_dir_name,
    normalize_agent_name,
    resolve_project_tmp_dir,
    resolve_session_root,
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Rename one or more <session-root>/<agent-name> directories to the (Closed) prefix.",
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
        help="Logical subagent name to mark closed. Repeat for multiple names.",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Emit JSON instead of text.",
    )
    args = parser.parse_args()
    if not args.agent_name:
        parser.error("provide at least one --agent-name value")
    return args


def close_agent_dir(session_root: Path, requested_name: str) -> dict[str, str]:
    normalized_name = normalize_agent_name(requested_name)
    open_dir = session_root / normalized_name
    closed_dir = session_root / make_closed_agent_dir_name(normalized_name)

    if open_dir.is_dir():
        if closed_dir.exists():
            return {
                "requested_name": requested_name,
                "agent_name": normalized_name,
                "from_dir": str(open_dir),
                "to_dir": str(closed_dir),
                "status": "conflict",
            }
        open_dir.rename(closed_dir)
        return {
            "requested_name": requested_name,
            "agent_name": normalized_name,
            "from_dir": str(open_dir),
            "to_dir": str(closed_dir),
            "status": "closed",
        }

    if closed_dir.is_dir():
        return {
            "requested_name": requested_name,
            "agent_name": normalized_name,
            "from_dir": str(open_dir),
            "to_dir": str(closed_dir),
            "status": "already_closed",
        }

    return {
        "requested_name": requested_name,
        "agent_name": normalized_name,
        "from_dir": str(open_dir),
        "to_dir": str(closed_dir),
        "status": "missing",
    }


def render_text(session_root: Path, results: list[dict[str, str]]) -> str:
    lines = [f"Session root: {session_root}"]
    for item in results:
        lines.append(
            f"- {item['agent_name']} | {item['status']} | {item['from_dir']} -> {item['to_dir']}"
        )
    return "\n".join(lines)


def main() -> int:
    args = parse_args()
    base_dir = Path(args.base_dir).resolve()
    session_root = resolve_session_root(base_dir, args.session_root)
    _, tmp_dir = resolve_project_tmp_dir(base_dir)

    try:
        session_root.relative_to(tmp_dir)
    except ValueError as exc:
        raise SystemExit(
            f"session root must be inside the current project tmp directory: {tmp_dir}"
        ) from exc

    if not session_root.exists():
        raise SystemExit(f"session root does not exist: {session_root}")

    results = [close_agent_dir(session_root, name) for name in args.agent_name]
    exit_code = 0 if all(item["status"] in {"closed", "already_closed"} for item in results) else 1

    if args.json:
        print(
            json.dumps(
                {
                    "session_root": str(session_root),
                    "results": results,
                },
                ensure_ascii=True,
                indent=2,
            )
        )
    else:
        print(render_text(session_root, results))

    return exit_code


if __name__ == "__main__":
    raise SystemExit(main())
