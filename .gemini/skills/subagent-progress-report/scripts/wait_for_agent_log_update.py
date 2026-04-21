#!/usr/bin/env python3
"""Wait for a new prepended agent.log entry using exponential backoff."""

from __future__ import annotations

import argparse
import json
import time
from pathlib import Path

from _session_common import normalize_agent_name


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "After a sub-agent has bootstrapped, wait for agent.log to receive a new "
            "prepended entry using exponential backoff."
        ),
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
        "--phase",
        choices=("quiet-wait", "post-nudge"),
        default="quiet-wait",
        help="Use quiet-wait before the first interrupt, or post-nudge after the interrupt.",
    )
    parser.add_argument(
        "--attempts",
        type=int,
        default=3,
        help="Number of exponential-backoff checks to perform.",
    )
    parser.add_argument(
        "--initial-wait-seconds",
        type=float,
        default=30.0,
        help="Delay before the first re-read.",
    )
    parser.add_argument(
        "--backoff-factor",
        type=float,
        default=2.0,
        help="Multiplier applied after each wait.",
    )
    parser.add_argument(
        "--max-total-wait-seconds",
        type=float,
        default=300.0,
        help="Maximum total wait time across the whole round.",
    )
    parser.add_argument(
        "--baseline-entry",
        help="Optional latest non-empty log entry to compare against. If omitted, capture it at start.",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Emit JSON instead of text.",
    )
    return parser.parse_args()


def latest_non_empty_entry(log_path: Path) -> str:
    if not log_path.exists():
        return ""

    try:
        with log_path.open("r", encoding="utf-8") as handle:
            for line in handle:
                entry = line.strip()
                if entry:
                    return entry
    except OSError:
        return ""

    return ""


def validate_args(args: argparse.Namespace) -> None:
    if args.attempts < 1:
        raise SystemExit("--attempts must be at least 1")
    if args.initial_wait_seconds <= 0:
        raise SystemExit("--initial-wait-seconds must be greater than 0")
    if args.backoff_factor < 1:
        raise SystemExit("--backoff-factor must be at least 1")
    if args.max_total_wait_seconds <= 0:
        raise SystemExit("--max-total-wait-seconds must be greater than 0")


def render_text(result: dict[str, object]) -> str:
    return (
        f"{result['agent_name']} | {result['status']} | {result['recommended_action']} | "
        f"waited {result['total_wait_seconds']:.1f}s | {result['session_dir']}"
    )


def main() -> int:
    args = parse_args()
    validate_args(args)

    session_root = Path(args.session_root).resolve()
    agent_name = normalize_agent_name(args.agent_name)
    session_dir = session_root / agent_name
    log_path = session_dir / "agent.log"

    baseline_entry = args.baseline_entry
    if baseline_entry is None:
        baseline_entry = latest_non_empty_entry(log_path)

    result: dict[str, object] = {
        "session_root": str(session_root),
        "session_dir": str(session_dir),
        "log_path": str(log_path),
        "agent_name": agent_name,
        "phase": args.phase,
        "baseline_entry": baseline_entry,
        "latest_entry": baseline_entry,
        "attempts_requested": args.attempts,
        "attempts_completed": 0,
        "total_wait_seconds": 0.0,
        "status": "stalled",
        "recommended_action": "follow_up" if args.phase == "quiet-wait" else "redelegate",
        "checks": [],
    }

    if not session_dir.is_dir():
        result["status"] = "missing"
        result["recommended_action"] = "investigate"
    else:
        total_wait = 0.0
        latest_entry = baseline_entry

        for attempt in range(1, args.attempts + 1):
            scheduled_delay = args.initial_wait_seconds * (args.backoff_factor ** (attempt - 1))
            remaining_budget = max(args.max_total_wait_seconds - total_wait, 0.0)
            if remaining_budget <= 0:
                break

            actual_delay = min(scheduled_delay, remaining_budget)
            time.sleep(actual_delay)
            total_wait += actual_delay

            latest_entry = latest_non_empty_entry(log_path)
            changed = bool(latest_entry) and latest_entry != baseline_entry
            result["checks"].append(
                {
                    "attempt": attempt,
                    "wait_seconds": actual_delay,
                    "latest_entry": latest_entry,
                    "changed": changed,
                }
            )
            result["attempts_completed"] = attempt
            result["total_wait_seconds"] = total_wait
            result["latest_entry"] = latest_entry

            if changed:
                result["status"] = "progressed"
                result["recommended_action"] = "continue"
                break

    if args.json:
        print(json.dumps(result, ensure_ascii=True, indent=2))
    else:
        print(render_text(result))

    return 0 if result["status"] == "progressed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
