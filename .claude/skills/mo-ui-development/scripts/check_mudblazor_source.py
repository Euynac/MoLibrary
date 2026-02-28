#!/usr/bin/env python3
"""
Verify that MudBlazor v9 source exists at the required local path.

The source path is intentionally hardcoded in this script:
    D:\\Repositories\\References\\MudBlazor-9.0.0

Users can edit the constant below when they need a different location.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from typing import Iterable

DEFAULT_MUDBLAZOR_SOURCE = r"D:\Repositories\References\MudBlazor-9.0.0"
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


def resolve_mudblazor_source_root(configured_path: str = DEFAULT_MUDBLAZOR_SOURCE) -> tuple[Path | None, list[Path]]:
    candidate_roots = build_candidate_roots(configured_path)

    for root in candidate_roots:
        marker = root / REQUIRED_RELATIVE_FILE
        if marker.is_file():
            return root.resolve(), candidate_roots

    return None, candidate_roots


def _build_result(configured_path: str) -> dict:
    resolved_root, candidates = resolve_mudblazor_source_root(configured_path)
    marker = str(REQUIRED_RELATIVE_FILE).replace("\\", "/")

    return {
        "configured_path": configured_path,
        "marker_file": marker,
        "candidate_paths": [path.as_posix() for path in candidates],
        "resolved_path": str(resolved_root) if resolved_root else None,
        "exists": resolved_root is not None,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Check if MudBlazor source is available.")
    parser.add_argument("--json", action="store_true", help="Output machine-readable JSON.")
    args = parser.parse_args()

    result = _build_result(DEFAULT_MUDBLAZOR_SOURCE)
    success = bool(result["exists"])

    if args.json:
        print(json.dumps(result, indent=2))
    else:
        if success:
            print("[OK] MudBlazor source is available.")
            print(f"Configured path: {result['configured_path']}")
            print(f"Resolved path:   {result['resolved_path']}")
            print(f"Marker file:     {result['marker_file']}")
        else:
            print("[ERROR] MudBlazor source is missing.")
            print(f"Configured path: {result['configured_path']}")
            print("Checked candidates:")
            for candidate in result["candidate_paths"]:
                print(f"  - {candidate}")
            print()
            print("Download MudBlazor v9 source and place it at the configured path.")
            print("You can edit DEFAULT_MUDBLAZOR_SOURCE inside this script if needed.")

    return 0 if success else 1


if __name__ == "__main__":
    sys.exit(main())
