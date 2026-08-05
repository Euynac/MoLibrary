#!/usr/bin/env python3
"""Roll a smoke-tested release index into source without rewriting history."""

from __future__ import annotations

import argparse
import json
import os
import sys
import tempfile
from pathlib import Path

from build_agent_skill_release import (
    INDEX_PATH,
    ReleaseError,
    load_json,
    merge_verified_history,
)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--released-index", type=Path, required=True)
    parser.add_argument("--expected-tag", required=True)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--check", action="store_true")
    mode.add_argument("--write", action="store_true")
    return parser.parse_args()


def atomic_write(path: Path, content: bytes) -> None:
    with tempfile.NamedTemporaryFile(dir=path.parent, prefix=f".{path.name}.", delete=False) as file:
        temporary_path = Path(file.name)
        file.write(content)
        file.flush()
        os.fsync(file.fileno())
    try:
        os.replace(temporary_path, path)
    except OSError:
        temporary_path.unlink(missing_ok=True)
        raise


def main() -> int:
    args = parse_args()
    try:
        current = load_json(INDEX_PATH)
        released = load_json(args.released_index)
        merged = merge_verified_history(
            current,
            released,
            expected_previous_tag=args.expected_tag,
        )
        content = (json.dumps(merged, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
        if INDEX_PATH.read_bytes() == content:
            print(f"[ok] {INDEX_PATH} already includes {args.expected_tag}.")
            return 0
        if args.check:
            print(
                f"[drift] {INDEX_PATH} does not include smoke-tested release {args.expected_tag}.",
                file=sys.stderr,
            )
            return 1
        atomic_write(INDEX_PATH, content)
        print(f"[written] Rolled {args.expected_tag} into {INDEX_PATH}.")
        print("Run scripts/sync_agent_skills.py --write and commit the resulting index snapshots.")
        return 0
    except (OSError, ReleaseError, json.JSONDecodeError) as exc:
        print(f"[error] {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
