#!/usr/bin/env python3
"""Supervise delegated agents through structured events and a local dashboard."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

from _dashboard import (
    dashboard_status,
    serve_dashboard,
    start_dashboard,
    stop_dashboard,
)
from _progress_store import (
    NATIVE_STATUSES,
    PROGRESS_STATES,
    ProgressStoreError,
    append_progress_event,
    close_agent,
    create_session,
    register_agent,
    resolve_project_root,
    resolve_session_root,
    session_snapshot,
    update_lifecycle,
)


def emit_json(value: Any) -> None:
    print(json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True))


def render_status(snapshot: dict[str, Any]) -> str:
    lines = [
        f"Task: {snapshot['task']}",
        f"Session: {snapshot['session_id']}",
        f"Agents: {len(snapshot['agents'])}",
    ]
    for agent in snapshot["agents"]:
        reported = agent.get("reported_state") or "no_report"
        message = agent.get("latest_message") or "No progress event yet."
        freshness = "fresh" if agent.get("native_fresh") else "stale"
        lines.append(
            f"- {agent['agent_name']} | native={agent['native_status']} ({freshness}) "
            f"| reported={reported} | {message}"
        )
    return "\n".join(lines)


def add_session_root(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--session-root", required=True)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Supervise delegated agents with authoritative lifecycle, factual checkpoints, and a local dashboard."
    )
    commands = parser.add_subparsers(dest="command", required=True)

    init_parser = commands.add_parser("init", help="Create a task session.")
    init_parser.add_argument("--task", required=True)
    init_parser.add_argument("--base-dir", default=".")
    init_parser.add_argument("--no-dashboard", action="store_true")

    register_parser = commands.add_parser("register", help="Register a spawned agent.")
    add_session_root(register_parser)
    register_parser.add_argument("--agent-name", required=True)
    register_parser.add_argument("--agent-id", required=True)
    register_parser.add_argument("--task", required=True)

    report_parser = commands.add_parser("report", help="Append child-reported progress.")
    add_session_root(report_parser)
    report_parser.add_argument("--agent-name", required=True)
    report_parser.add_argument("--state", choices=sorted(PROGRESS_STATES), required=True)
    report_parser.add_argument("--message", required=True)

    lifecycle_parser = commands.add_parser(
        "lifecycle", help="Persist a native runtime lifecycle snapshot."
    )
    add_session_root(lifecycle_parser)
    lifecycle_parser.add_argument("--agent-name", required=True)
    lifecycle_parser.add_argument(
        "--status", choices=sorted(NATIVE_STATUSES), required=True
    )

    status_parser = commands.add_parser("status", help="Summarize a task session.")
    status_parser.add_argument("--session-root")
    status_parser.add_argument("--base-dir", default=".")
    status_parser.add_argument("--agent-name", action="append", default=[])
    status_parser.add_argument("--json", action="store_true")

    close_parser = commands.add_parser("close", help="Archive an agent logically.")
    add_session_root(close_parser)
    close_parser.add_argument("--agent-name", required=True)
    close_parser.add_argument("--reason", required=True)

    dashboard_parser = commands.add_parser(
        "dashboard", help="Manage the reusable project dashboard."
    )
    dashboard_commands = dashboard_parser.add_subparsers(
        dest="dashboard_command", required=True
    )
    dashboard_start = dashboard_commands.add_parser("start")
    dashboard_start.add_argument("--base-dir", default=".")
    dashboard_start.add_argument("--host", default="127.0.0.1")
    dashboard_start.add_argument("--port", type=int, default=0)
    dashboard_status_parser = dashboard_commands.add_parser("status")
    dashboard_status_parser.add_argument("--base-dir", default=".")
    dashboard_stop = dashboard_commands.add_parser("stop")
    dashboard_stop.add_argument("--base-dir", default=".")

    return parser


def build_internal_serve_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--base-dir", required=True)
    parser.add_argument("--host", required=True)
    parser.add_argument("--port", type=int, required=True)
    parser.add_argument("--instance-id", required=True)
    parser.add_argument("--control-token", required=True)
    return parser


def execute(args: argparse.Namespace) -> int:
    cli_path = Path(__file__).resolve()
    if args.command == "init":
        session = create_session(args.base_dir, args.task)
        if not args.no_dashboard:
            session["dashboard"] = start_dashboard(
                session["project_root"], cli_path=cli_path
            )
        emit_json(session)
        return 0
    if args.command == "register":
        emit_json(
            register_agent(
                args.session_root, args.agent_name, args.agent_id, args.task
            )
        )
        return 0
    if args.command == "report":
        emit_json(
            append_progress_event(
                args.session_root, args.agent_name, args.state, args.message
            )
        )
        return 0
    if args.command == "lifecycle":
        emit_json(update_lifecycle(args.session_root, args.agent_name, args.status))
        return 0
    if args.command == "status":
        session_root = resolve_session_root(args.base_dir, args.session_root)
        filters = set(args.agent_name) if args.agent_name else None
        snapshot = session_snapshot(session_root, agent_names=filters)
        if args.json:
            emit_json(snapshot)
        else:
            print(render_status(snapshot))
        return 0
    if args.command == "close":
        emit_json(close_agent(args.session_root, args.agent_name, args.reason))
        return 0
    if args.command == "dashboard":
        if args.dashboard_command == "start":
            emit_json(
                start_dashboard(
                    resolve_project_root(args.base_dir),
                    cli_path=cli_path,
                    host=args.host,
                    port=args.port,
                )
            )
        elif args.dashboard_command == "status":
            emit_json(dashboard_status(args.base_dir))
        else:
            emit_json(stop_dashboard(args.base_dir))
        return 0
    raise ProgressStoreError(f"unsupported command: {args.command}")


def main() -> int:
    if len(sys.argv) > 1 and sys.argv[1] == "_serve":
        args = build_internal_serve_parser().parse_args(sys.argv[2:])
        try:
            return serve_dashboard(
                args.base_dir,
                args.host,
                args.port,
                args.instance_id,
                args.control_token,
            )
        except ProgressStoreError as exc:
            print(f"error: {exc}", file=sys.stderr)
            return 2

    parser = build_parser()
    args = parser.parse_args()
    try:
        return execute(args)
    except ProgressStoreError as exc:
        parser.exit(2, f"error: {exc}\n")


if __name__ == "__main__":
    raise SystemExit(main())
