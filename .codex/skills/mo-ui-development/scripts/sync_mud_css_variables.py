#!/usr/bin/env python3
"""Initialize or update MudBlazor CSS variable JSON from MudBlazor source."""

from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path

from check_mudblazor_source import REQUIRED_RELATIVE_FILE, resolve_mudblazor_source_root

SKILL_ROOT = Path(__file__).resolve().parents[1]
REFERENCES_DIR = SKILL_ROOT / "references"
OUTPUT_JSON = REFERENCES_DIR / "mudblazor-css-variables.json"
CONST_PATTERN = re.compile(r'private const string\s+(\w+)\s*=\s*"([^"]+)";')
PLACEHOLDER_PATTERN = re.compile(r"\{([A-Za-z_][A-Za-z0-9_]*)\}")
VAR_TEMPLATE_PATTERN = re.compile(
    r"--(?:\{[A-Za-z_][A-Za-z0-9_]*\}|[A-Za-z0-9-])+(?=\s*:)"
)
VAR_PATTERN = re.compile(r"^--mud-[a-z0-9-]+$")


def _strip_comment_only_lines(content: str) -> str:
    return "\n".join(
        line for line in content.splitlines() if not line.lstrip().startswith("//")
    )


def _replace_placeholders(template: str, constants: dict[str, str]) -> str:
    def replace(match: re.Match[str]) -> str:
        key = match.group(1)
        return constants.get(key, match.group(0))

    return PLACEHOLDER_PATTERN.sub(replace, template)


def extract_mud_variables(source_text: str) -> list[str]:
    cleaned = _strip_comment_only_lines(source_text)
    constants = dict(CONST_PATTERN.findall(cleaned))
    found: set[str] = set()

    for template in VAR_TEMPLATE_PATTERN.findall(cleaned):
        resolved = _replace_placeholders(template, constants).lower()
        match = VAR_PATTERN.match(resolved)
        if match:
            found.add(match.group(0))

    return sorted(found)


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Initialize or update MudBlazor CSS variable JSON from MudBlazor source."
    )
    parser.add_argument("--force", action="store_true", help="Rewrite JSON even when unchanged.")
    args = parser.parse_args()

    resolved_root, candidates = resolve_mudblazor_source_root()
    if resolved_root is None:
        print("[ERROR] MudBlazor source is missing.")
        print("Checked candidates:")
        for candidate in candidates:
            print(f"  - {candidate.as_posix()}")
        print("Run scripts/check_mudblazor_source.py for details.")
        return 1

    source_file = resolved_root / REQUIRED_RELATIVE_FILE
    source_text = source_file.read_text(encoding="utf-8")
    variables = extract_mud_variables(source_text)
    if not variables:
        print("[ERROR] No MudBlazor CSS variables were extracted from source.")
        return 1

    generated_at_utc = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    payload = {
        "source_root": str(resolved_root),
        "source_file": str(source_file),
        "generated_at_utc": generated_at_utc,
        "variable_count": len(variables),
        "variables": variables,
    }
    json_content = json.dumps(payload, indent=2) + "\n"

    mode = "initialized" if not OUTPUT_JSON.exists() else "updated"
    OUTPUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    if args.force:
        OUTPUT_JSON.write_text(json_content, encoding="utf-8")
    else:
        current_json = OUTPUT_JSON.read_text(encoding="utf-8") if OUTPUT_JSON.exists() else None
        if current_json != json_content:
            OUTPUT_JSON.write_text(json_content, encoding="utf-8")

    print(f"[OK] MudBlazor CSS variable JSON {mode}.")
    print(f"Source root: {resolved_root}")
    print(f"Variable count: {len(variables)}")
    print(f"JSON: {OUTPUT_JSON}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
