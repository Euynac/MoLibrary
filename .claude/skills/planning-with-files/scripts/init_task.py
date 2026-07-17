#!/usr/bin/env python3
"""Create a new numbered folder containing concise persistent task memory."""

from __future__ import annotations

import argparse
import re
import unicodedata
from pathlib import Path

DEFAULT_PHASES = (
    "Understand the current state",
    "Execute the work",
    "Verify and deliver",
)
FOLDER_PATTERN = re.compile(r"^(\d+)-")
TEMPLATE_TOKEN_PATTERN = re.compile(r"\{\{[A-Z_]+\}\}")


def slugify(value: str) -> str:
    """Convert a human description into a stable, filesystem-safe slug."""

    normalized = unicodedata.normalize("NFKC", value).casefold().replace("_", "-")
    slug = re.sub(r"[^\w]+", "-", normalized, flags=re.UNICODE)
    slug = re.sub(r"-{2,}", "-", slug).strip("-")
    return slug[:64].rstrip("-") or "task"


def next_task_directory(pending_dir: Path, slug: str) -> Path:
    """Atomically reserve the next numbered task directory."""

    pending_dir.mkdir(parents=True, exist_ok=True)
    highest_number = 0
    for path in pending_dir.iterdir():
        match = FOLDER_PATTERN.match(path.name) if path.is_dir() else None
        if match:
            highest_number = max(highest_number, int(match.group(1)))

    number = highest_number + 1

    while True:
        candidate = pending_dir / f"{number:03d}-{slug}"
        try:
            candidate.mkdir()
            return candidate
        except FileExistsError:
            number += 1


def render_template(template_path: Path, replacements: dict[str, str]) -> str:
    """Render a bundled Markdown template and reject unresolved tokens."""

    template = template_path.read_text(encoding="utf-8")
    required_tokens = set(TEMPLATE_TOKEN_PATTERN.findall(template))
    missing_tokens = sorted(required_tokens - replacements.keys())
    if missing_tokens:
        raise ValueError(
            f"Missing template values for {template_path.name}: {missing_tokens}"
        )
    return TEMPLATE_TOKEN_PATTERN.sub(
        lambda match: replacements[match.group(0)], template
    )


def escape_markdown_table_cell(value: str) -> str:
    """Keep a phase description inside one Markdown table cell."""

    return value.replace("|", "\\|").replace("\r", " ").replace("\n", " ")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Create opt-in persistent planning files under .pending/."
    )
    parser.add_argument("description", help="Short task description used for the folder slug.")
    parser.add_argument("--goal", required=True, help="Concrete task end state.")
    parser.add_argument("--title", help="Plan title; defaults to the task description.")
    parser.add_argument(
        "--phase",
        action="append",
        dest="phases",
        help="Task phase in execution order; repeat for multiple phases.",
    )
    parser.add_argument(
        "--base-dir",
        type=Path,
        default=Path.cwd(),
        help="Project root containing .pending/; defaults to the current directory.",
    )
    parser.add_argument("--with-findings", action="store_true")
    return parser


def main() -> int:
    args = build_parser().parse_args()
    description = args.description.strip()
    goal = args.goal.strip()
    title = (args.title or description).strip()
    phases = tuple(
        phase.strip() for phase in (args.phases or DEFAULT_PHASES) if phase.strip()
    )
    if not description or not goal or not title or not phases:
        raise SystemExit("description, goal, title, and at least one non-empty phase are required")

    skill_dir = Path(__file__).resolve().parents[1]
    templates_dir = skill_dir / "templates"
    phase_rows = "\n".join(
        f"| {index} | {escape_markdown_table_cell(phase)} | "
        f"{'in_progress' if index == 1 else 'pending'} |"
        for index, phase in enumerate(phases, start=1)
    )
    replacements = {
        "{{TITLE}}": title,
        "{{GOAL}}": goal,
        "{{FIRST_PHASE}}": escape_markdown_table_cell(phases[0]),
        "{{PHASE_ROWS}}": phase_rows,
    }

    outputs = {
        "task_plan.md": render_template(templates_dir / "task_plan.md", replacements),
    }
    if args.with_findings:
        outputs["findings.md"] = render_template(
            templates_dir / "findings.md", replacements
        )
    task_dir = next_task_directory(
        args.base_dir.resolve() / ".pending", slugify(description)
    )
    for filename, content in outputs.items():
        (task_dir / filename).write_text(content, encoding="utf-8")

    print(task_dir)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
