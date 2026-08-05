#!/usr/bin/env python3
"""Run every Python and Node test suite owned by a canonical Monica skill."""

from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
SKILLS_ROOT = REPOSITORY_ROOT / "skills"
NODE_TEST_PATTERNS = ("*.test.js", "*.test.cjs", "*.test.mjs")


def main() -> int:
    commands: list[tuple[Path, list[str]]] = []
    for skill_root in sorted(path for path in SKILLS_ROOT.iterdir() if path.is_dir()):
        tests_root = skill_root / "tests"
        if not tests_root.is_dir():
            continue
        if any(tests_root.rglob("test*.py")):
            commands.append(
                (
                    skill_root,
                    [sys.executable, "-m", "unittest", "discover", "-s", "tests", "-p", "test*.py"],
                )
            )
        node_tests = sorted(
            {
                test_path
                for pattern in NODE_TEST_PATTERNS
                for test_path in tests_root.rglob(pattern)
            }
        )
        if node_tests:
            if shutil.which("node") is None:
                print(f"[error] Node.js is required for {skill_root.name} tests.", file=sys.stderr)
                return 2
            commands.append(
                (
                    skill_root,
                    ["node", "--test", *(str(path.relative_to(skill_root)) for path in node_tests)],
                )
            )

    if not commands:
        print("[error] No skill-owned test suites were discovered.", file=sys.stderr)
        return 2

    for working_directory, command in commands:
        print(f"[test] {working_directory.name}: {' '.join(command)}", flush=True)
        child_environment = os.environ.copy()
        child_environment["PYTHONDONTWRITEBYTECODE"] = "1"
        completed = subprocess.run(
            command,
            cwd=working_directory,
            env=child_environment,
            check=False,
        )
        if completed.returncode != 0:
            return completed.returncode
    print(f"[ok] Ran {len(commands)} skill-owned test suite(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
