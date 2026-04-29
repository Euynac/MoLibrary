#!/usr/bin/env python3
"""Create a shared main-agent session directory under .tmp."""

from __future__ import annotations

import argparse
import json
from datetime import datetime
from pathlib import Path

from _session_common import resolve_project_tmp_dir


def make_timestamp() -> str:
    return datetime.now().astimezone().strftime("%Y%m%d-%H%M%S")


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Create .tmp/<timestamp>-agent-session and print its path.",
    )
    parser.add_argument(
        "--base-dir",
        default=".",
        help="Starting directory used to locate the current project root.",
    )
    parser.add_argument(
        "--timestamp",
        help="Optional timestamp override in YYYYMMDD-HHMMSS format.",
    )
    args = parser.parse_args()

    base_dir, tmp_dir = resolve_project_tmp_dir(Path(args.base_dir).resolve())
    tmp_dir.mkdir(parents=True, exist_ok=True)

    timestamp = args.timestamp or make_timestamp()
    session_root = tmp_dir / f"{timestamp}-agent-session"
    session_root.mkdir(parents=True, exist_ok=True)

    result = {
        "base_dir": str(base_dir),
        "tmp_dir": str(tmp_dir),
        "session_root": str(session_root),
        "timestamp": timestamp,
    }
    print(json.dumps(result, ensure_ascii=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
