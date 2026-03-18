#!/usr/bin/env python3
"""Verify that a local MudBlazor v9 source tree is available when needed."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Iterable

from mudblazor_skill_state import SOURCE_CONFIG_FILE, load_source_config, save_source_config

REQUIRED_RELATIVE_FILE = Path("src/MudBlazor/Components/ThemeProvider/MudThemeProvider.razor.cs")


def _to_wsl_path(path_text: str) -> str | None:
    match = re.match(r"^([A-Za-z]):[\\/](.*)$", path_text)
    if not match:
        return None
    drive = match.group(1).lower()
    remainder = match.group(2).replace("\\", "/")
    return f"/mnt/{drive}/{remainder}"


def _to_windows_path(path_text: str) -> str | None:
    normalized = path_text.replace("\\", "/")
    match = re.match(r"^/mnt/([A-Za-z])/(.*)$", normalized)
    if not match:
        return None
    drive = match.group(1).upper()
    remainder = match.group(2).replace("/", "\\")
    return f"{drive}:\\{remainder}"


def _dedupe(values: Iterable[str]) -> list[str]:
    seen: set[str] = set()
    result: list[str] = []
    for value in values:
        key = value.strip()
        if not key:
            continue
        lowered = key.lower()
        if lowered in seen:
            continue
        seen.add(lowered)
        result.append(key)
    return result


def resolve_source_configuration() -> tuple[str | None, str]:
    return load_source_config()


def build_candidate_roots(configured_path: str) -> list[Path]:
    candidates: list[str] = [configured_path]

    as_wsl = _to_wsl_path(configured_path)
    if as_wsl:
        candidates.append(as_wsl)

    as_windows = _to_windows_path(configured_path)
    if as_windows:
        candidates.append(as_windows)

    # Round-trip conversions for robustness if users edit to mixed formats.
    if as_wsl:
        back_to_windows = _to_windows_path(as_wsl)
        if back_to_windows:
            candidates.append(back_to_windows)
    if as_windows:
        back_to_wsl = _to_wsl_path(as_windows)
        if back_to_wsl:
            candidates.append(back_to_wsl)

    return [Path(value) for value in _dedupe(candidates)]


def resolve_mudblazor_source_root() -> tuple[Path | None, list[Path]]:
    resolved_configured_path, _ = resolve_source_configuration()
    if not resolved_configured_path:
        return None, []

    candidate_roots = build_candidate_roots(resolved_configured_path)

    for root in candidate_roots:
        marker = root / REQUIRED_RELATIVE_FILE
        if marker.is_file():
            return root.resolve(), candidate_roots

    return None, candidate_roots


def _build_result() -> dict:
    resolved_configured_path, config_source = resolve_source_configuration()
    resolved_root, candidates = resolve_mudblazor_source_root()
    marker = str(REQUIRED_RELATIVE_FILE).replace("\\", "/")

    return {
        "config_file": str(SOURCE_CONFIG_FILE),
        "configured_path": resolved_configured_path,
        "config_source": config_source,
        "marker_file": marker,
        "candidate_paths": [path.as_posix() for path in candidates],
        "resolved_path": str(resolved_root) if resolved_root else None,
        "exists": resolved_root is not None,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Check if MudBlazor source is available.")
    parser.add_argument(
        "--save-source-root",
        help="Persist the MudBlazor source root into the project .tmp config before running the check.",
    )
    parser.add_argument("--json", action="store_true", help="Output machine-readable JSON.")
    args = parser.parse_args()

    if args.save_source_root:
        try:
            config_file = save_source_config(args.save_source_root)
        except Exception as exc:  # noqa: BLE001
            print(f"[ERROR] Failed to save source configuration: {exc}", file=sys.stderr)
            return 1

        if not args.json:
            print("[INFO] MudBlazor source root saved.")
            print(f"Config file:      {config_file}")

    result = _build_result()
    success = bool(result["exists"])

    if args.json:
        print(json.dumps(result, indent=2))
    else:
        if success:
            print("[OK] MudBlazor source is available.")
            print(f"Configured path: {result['configured_path']}")
            print(f"Configured via:  {result['config_source']}")
            print(f"Resolved path:   {result['resolved_path']}")
            print(f"Marker file:     {result['marker_file']}")
        else:
            if result["configured_path"] is None:
                print("[ERROR] MudBlazor source root is not configured.")
                print(f"Config file:      {result['config_file']}")
                print(f"Configuration state: {result['config_source']}")
                print()
                print("Source-dependent work must stop here.")
                print("Ask the user for the local MudBlazor source path, or ask them to download the source first.")
                print("Then save the path into the project temp config with:")
                print("  python scripts/check_mudblazor_source.py --save-source-root <path>")
            else:
                print("[ERROR] MudBlazor source is missing.")
                print(f"Config file:      {result['config_file']}")
                print(f"Configured path: {result['configured_path']}")
                print(f"Configured via:  {result['config_source']}")
                print("Checked candidates:")
                for candidate in result["candidate_paths"]:
                    print(f"  - {candidate}")
                print()
                print("Source-dependent work must stop here.")
                print("Ask the user to download MudBlazor source to the configured path above, or provide the correct local source path.")
                print("Then save the path into the project temp config with:")
                print("  python scripts/check_mudblazor_source.py --save-source-root <path>")

    return 0 if success else 1


if __name__ == "__main__":
    sys.exit(main())
