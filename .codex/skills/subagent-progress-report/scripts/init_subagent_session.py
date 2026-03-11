#!/usr/bin/env python3
"""Create a per-subagent directory inside a main-agent session root."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

from _session_common import resolve_project_tmp_dir


def sanitize_agent_name(value: str) -> str:
    cleaned = re.sub(r"[^A-Za-z0-9._-]+", "-", value.strip())
    cleaned = cleaned.strip("-._")
    return cleaned or "subagent"


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Create <session-root>/<agent-name> and agent.log.",
    )
    parser.add_argument(
        "--session-root",
        required=True,
        help="Existing main-agent session root directory.",
    )
    parser.add_argument(
        "--agent-name",
        required=True,
        help="Stable subagent name used for the child directory name.",
    )
    args = parser.parse_args()

    session_root = Path(args.session_root).resolve()
    if not session_root.exists():
        raise SystemExit(
            f"session root does not exist: {session_root}. Initialize it from the main agent first."
        )

    base_dir, tmp_dir = resolve_project_tmp_dir(Path.cwd())
    try:
        session_root.relative_to(tmp_dir)
    except ValueError as exc:
        raise SystemExit(
            f"session root must be inside the current project tmp directory: {tmp_dir}"
        ) from exc

    safe_agent_name = sanitize_agent_name(args.agent_name)
    session_dir = session_root / safe_agent_name
    session_dir.mkdir(parents=True, exist_ok=True)

    log_path = session_dir / "agent.log"
    log_path.touch(exist_ok=True)

    result = {
        "base_dir": str(base_dir),
        "tmp_dir": str(tmp_dir),
        "session_root": str(session_root),
        "session_dir": str(session_dir),
        "log_path": str(log_path),
        "agent_name": safe_agent_name,
    }
    print(json.dumps(result, ensure_ascii=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
