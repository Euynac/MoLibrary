#!/usr/bin/env python3
"""
Validate MudBlazor CSS variables used by project style sources.

This script checks CSS and Razor files under a project root and reports usages
of unknown MudBlazor variables compared to the generated variable list.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

SKILL_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_VARIABLES_FILE = SKILL_ROOT / "references" / "mudblazor-css-variables.json"

IGNORED_DIRS = {".git", "bin", "obj", ".pending"}
SCAN_GLOBS = ("*.css", "*.razor")
VAR_USAGE_PATTERN = re.compile(r"var\(\s*(--mud-[a-z0-9-]+)\b", re.IGNORECASE)

SAFE_REPLACEMENTS = {
    "--mud-palette-background-grey": "--mud-palette-background-gray",
    "--mud-palette-action-hover": "--mud-palette-action-default-hover",
    "--mud-palette-action-selected": "--mud-palette-action-default-hover",
    "--mud-palette-action-selected-hover": "--mud-palette-action-default-hover",
}

MANUAL_REVIEW_HINTS = {
    "--mud-palette-lines-default-rgb": [
        "--mud-palette-divider-rgb",
        "--mud-palette-text-primary-rgb",
    ],
    "--mud-palette-appbar-background-rgb": [
        "--mud-palette-primary-rgb",
        "--mud-palette-appbar-background",
    ],
    "--mud-palette-surface-variant": [
        "--mud-palette-surface",
        "--mud-palette-background-gray",
    ],
}


class Colors:
    RED = "\033[91m"
    YELLOW = "\033[93m"
    GREEN = "\033[92m"
    BLUE = "\033[94m"
    BOLD = "\033[1m"
    END = "\033[0m"


@dataclass(frozen=True)
class Usage:
    file: str
    line: int
    column: int


def should_skip(path: Path) -> bool:
    return any(part.lower() in IGNORED_DIRS for part in path.parts)


def iter_scan_files(root: Path) -> Iterable[Path]:
    for glob in SCAN_GLOBS:
        for file_path in root.rglob(glob):
            if should_skip(file_path):
                continue
            if file_path.is_file():
                yield file_path


def load_valid_variables(path: Path) -> set[str]:
    if not path.exists():
        raise FileNotFoundError(f"Variable list file not found: {path}")

    payload = json.loads(path.read_text(encoding="utf-8"))
    values = payload.get("variables")
    if not isinstance(values, list):
        raise ValueError("Invalid variable list JSON format: missing 'variables' list.")

    return {str(value).strip().lower() for value in values if str(value).strip()}


def collect_usages(root: Path) -> tuple[dict[str, list[Usage]], dict[str, int]]:
    usages: dict[str, list[Usage]] = defaultdict(list)
    scanned_counts = {"css": 0, "razor": 0}

    for source_file in iter_scan_files(root):
        suffix = source_file.suffix.lower()
        if suffix == ".css":
            scanned_counts["css"] += 1
        elif suffix == ".razor":
            scanned_counts["razor"] += 1

        content = source_file.read_text(encoding="utf-8", errors="ignore")
        relative = source_file.relative_to(root).as_posix()

        for line_number, line in enumerate(content.splitlines(), start=1):
            for match in VAR_USAGE_PATTERN.finditer(line):
                variable = match.group(1).lower()
                usages[variable].append(
                    Usage(file=relative, line=line_number, column=match.start(1) + 1)
                )

    return dict(usages), scanned_counts


def apply_safe_fixes(root: Path) -> tuple[dict[str, int], int]:
    replacement_counts: dict[str, int] = {}
    files_changed = 0

    for source_file in iter_scan_files(root):
        original = source_file.read_text(encoding="utf-8", errors="ignore")
        updated = original
        file_replacements = 0

        for old, new in SAFE_REPLACEMENTS.items():
            pattern = re.compile(rf"(?<![A-Za-z0-9-]){re.escape(old)}(?![A-Za-z0-9-])", re.IGNORECASE)
            updated, replaced = pattern.subn(new, updated)
            if replaced > 0:
                replacement_counts[old] = replacement_counts.get(old, 0) + replaced
                file_replacements += replaced

        if file_replacements > 0 and updated != original:
            source_file.write_text(updated, encoding="utf-8")
            files_changed += 1

    return replacement_counts, files_changed


def build_report(
    root: Path,
    variables_file: Path,
    scanned_counts: dict[str, int],
    usages: dict[str, list[Usage]],
    unknown_usages: dict[str, list[Usage]],
    fix_counts: dict[str, int],
    fixed_files: int,
) -> dict:
    unknown_occurrences = sum(len(items) for items in unknown_usages.values())
    total_occurrences = sum(len(items) for items in usages.values())
    css_file_count = scanned_counts.get("css", 0)
    razor_file_count = scanned_counts.get("razor", 0)

    return {
        "root": str(root),
        "variables_file": str(variables_file),
        "summary": {
            "css_files_scanned": css_file_count,
            "razor_files_scanned": razor_file_count,
            "files_scanned_total": css_file_count + razor_file_count,
            "variables_used_distinct": len(usages),
            "variables_used_occurrences": total_occurrences,
            "unknown_variables_distinct": len(unknown_usages),
            "unknown_variables_occurrences": unknown_occurrences,
            "fixed_files": fixed_files,
            "fixed_occurrences": sum(fix_counts.values()),
            "status": "PASSED" if len(unknown_usages) == 0 else "FAILED",
        },
        "unknown_variables": {
            key: [usage.__dict__ for usage in value] for key, value in sorted(unknown_usages.items())
        },
        "safe_fix_counts": dict(sorted(fix_counts.items())),
        "manual_review_hints": {
            key: MANUAL_REVIEW_HINTS[key]
            for key in sorted(unknown_usages.keys())
            if key in MANUAL_REVIEW_HINTS
        },
    }


def print_console_report(report: dict, summary_only: bool) -> None:
    summary = report["summary"]
    unknown = report["unknown_variables"]
    hints = report["manual_review_hints"]

    print(f"\n{Colors.BOLD}=== MudBlazor CSS Variable Validation ==={Colors.END}\n")

    if not summary_only:
        if unknown:
            print(f"{Colors.RED}{Colors.BOLD}[ERROR] Unknown MudBlazor variables:{Colors.END}")
            for variable, locations in unknown.items():
                print(f"  {Colors.RED}x{Colors.END} {variable}")
                for item in locations:
                    print(f"    - {item['file']}:{item['line']}:{item['column']}")
                if variable in hints:
                    print(f"    {Colors.YELLOW}Hint:{Colors.END} {', '.join(hints[variable])}")
            print()
        else:
            print(f"{Colors.GREEN}[OK] No unknown MudBlazor variables found.{Colors.END}\n")

        safe_fix_counts = report["safe_fix_counts"]
        if safe_fix_counts:
            print(f"{Colors.BLUE}{Colors.BOLD}[INFO] Safe auto-fixes applied:{Colors.END}")
            for variable, count in safe_fix_counts.items():
                print(f"  - {variable}: {count}")
            print()

    status_color = Colors.GREEN if summary["status"] == "PASSED" else Colors.RED
    print(f"{Colors.BOLD}Summary:{Colors.END}")
    print(f"  CSS files scanned: {summary['css_files_scanned']}")
    print(f"  Razor files scanned: {summary['razor_files_scanned']}")
    print(f"  Total files scanned: {summary['files_scanned_total']}")
    print(f"  Variables used (distinct): {summary['variables_used_distinct']}")
    print(f"  Variables used (occurrences): {summary['variables_used_occurrences']}")
    print(f"  Unknown variables (distinct): {summary['unknown_variables_distinct']}")
    print(f"  Unknown variables (occurrences): {summary['unknown_variables_occurrences']}")
    print(f"  Fixed files: {summary['fixed_files']}")
    print(f"  Fixed occurrences: {summary['fixed_occurrences']}")
    print(f"  Status: {status_color}{Colors.BOLD}{summary['status']}{Colors.END}\n")


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Validate MudBlazor CSS variable usage in CSS and Razor files."
    )
    parser.add_argument("--root", type=str, default=".", help="Project root directory to scan.")
    parser.add_argument(
        "--variables-file",
        type=str,
        default=str(DEFAULT_VARIABLES_FILE),
        help="Path to generated MudBlazor CSS variables JSON file.",
    )
    parser.add_argument("--fix", action="store_true", help="Apply safe automatic replacements before validation.")
    parser.add_argument("--json", action="store_true", help="Output JSON report.")
    parser.add_argument("--summary", action="store_true", help="Show summary only.")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    variables_file = Path(args.variables_file).resolve()

    if not root.exists():
        print(f"[ERROR] Root path does not exist: {root}", file=sys.stderr)
        return 1

    try:
        valid_variables = load_valid_variables(variables_file)
    except Exception as exc:  # noqa: BLE001
        print(f"[ERROR] {exc}", file=sys.stderr)
        print(
            "Run scripts/sync_mud_css_variables.py to initialize or update the variable list.",
            file=sys.stderr,
        )
        return 1

    fix_counts: dict[str, int] = {}
    fixed_files = 0

    if args.fix:
        fix_counts, fixed_files = apply_safe_fixes(root)

    usages, scanned_counts = collect_usages(root)
    unknown_usages = {
        variable: locations
        for variable, locations in usages.items()
        if variable not in valid_variables
    }

    report = build_report(
        root=root,
        variables_file=variables_file,
        scanned_counts=scanned_counts,
        usages=usages,
        unknown_usages=unknown_usages,
        fix_counts=fix_counts,
        fixed_files=fixed_files,
    )

    if args.json:
        print(json.dumps(report, indent=2))
    else:
        print_console_report(report, summary_only=args.summary)

    return 0 if report["summary"]["status"] == "PASSED" else 1


if __name__ == "__main__":
    sys.exit(main())
