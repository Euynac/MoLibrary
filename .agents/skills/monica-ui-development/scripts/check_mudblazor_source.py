#!/usr/bin/env python3
"""Resolve MudBlazor v9 source through third-party-source-catalog."""

from __future__ import annotations

import argparse
import json
import os
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

from mudblazor_skill_state import THIRD_PARTY_CATALOG_FILE, THIRD_PARTY_SOURCE_CATALOG_SCRIPT

REQUIRED_RELATIVE_FILE = Path("src/MudBlazor/Components/ThemeProvider/MudThemeProvider.razor.cs")
MAX_ANCESTOR_STEPS = 6
GENERIC_RECORD_NAMES = {"src", "test", "tests", "examples", "server", "ssr", "webassembly", "python", "dotnet"}


@dataclass(frozen=True)
class CatalogMatch:
    """A MudBlazor source root resolved from the shared source catalog."""

    record_name: str
    catalog_path: Path
    resolved_root: Path
    score: int


def absolute_path(path: str | Path) -> Path:
    return Path(os.path.abspath(os.fspath(path)))


def load_catalog_state() -> tuple[dict | None, str]:
    if not THIRD_PARTY_CATALOG_FILE.exists():
        return None, f"third-party source catalog is missing: {THIRD_PARTY_CATALOG_FILE}"

    try:
        payload = json.loads(THIRD_PARTY_CATALOG_FILE.read_text(encoding="utf-8"))
    except Exception as exc:  # noqa: BLE001
        return None, f"third-party source catalog is invalid: {THIRD_PARTY_CATALOG_FILE} ({exc})"

    records = payload.get("records")
    if not isinstance(records, list):
        return None, f"third-party source catalog does not contain a valid records list: {THIRD_PARTY_CATALOG_FILE}"

    return payload, f"third-party source catalog {THIRD_PARTY_CATALOG_FILE}"


def iter_record_paths(record: dict) -> Iterable[Path]:
    seen: set[str] = set()

    def yield_path(path_text: str | None) -> Iterable[Path]:
        normalized = str(path_text or "").strip()
        if not normalized or normalized in seen:
            return []
        seen.add(normalized)
        return [absolute_path(normalized)]

    for source in record.get("local_sources", []):
        yield from yield_path(source.get("path"))

    yield from yield_path(record.get("preferred_path"))

    downloads = record.get("downloads", {})
    if isinstance(downloads, dict):
        clone = downloads.get("clone") or {}
        if isinstance(clone, dict):
            yield from yield_path(clone.get("local_path"))

        tags = downloads.get("tags", {})
        if isinstance(tags, dict):
            for payload in tags.values():
                if isinstance(payload, dict):
                    yield from yield_path(payload.get("local_path"))

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


def is_mudblazor_related(record: dict, path: Path) -> bool:
    tokens = [
        record.get("canonical_name", ""),
        record.get("display_name", ""),
        record.get("github_full_name", ""),
        *record.get("aliases", []),
        str(path),
    ]
    text = " ".join(str(token) for token in tokens if token).casefold()
    return "mudblazor" in text


def build_probe_roots(path: Path) -> list[Path]:
    roots: list[Path] = []
    current = absolute_path(path if path.is_dir() else path.parent)
    for _ in range(MAX_ANCESTOR_STEPS + 1):
        roots.append(current)
        if current.parent == current:
            break
        current = current.parent
    return roots


def score_match(record: dict, catalog_path: Path, resolved_root: Path) -> int:
    text = " ".join(
        str(value)
        for value in [
            record.get("canonical_name", ""),
            record.get("display_name", ""),
            record.get("github_full_name", ""),
            str(catalog_path),
            str(resolved_root),
        ]
        if value
    ).casefold()
    score = 0

    if "mudblazor" in text:
        score += 100
    if "mudblazor-9" in text or "mudblazor/9" in text or "9.0.0" in text or "/9.0/" in text:
        score += 500
    if "mudblazor-8" in text or "8.9.0" in text:
        score -= 200
    if "markdown" in text:
        score -= 100
    if resolved_root == catalog_path:
        score += 20
    if resolved_root.name.casefold().startswith("mudblazor"):
        score += 40

    return score


def build_record_name(record: dict, catalog_path: Path) -> str:
    candidates = [
        record.get("github_full_name"),
        record.get("canonical_name"),
        record.get("display_name"),
    ]
    for candidate in candidates:
        normalized = str(candidate or "").strip()
        if normalized and normalized.casefold() not in GENERIC_RECORD_NAMES:
            return normalized
    return catalog_path.as_posix()


def resolve_catalog_matches() -> tuple[CatalogMatch | None, list[CatalogMatch], str]:
    catalog, catalog_source = load_catalog_state()
    if catalog is None:
        return None, [], catalog_source

    resolved_by_root: dict[str, CatalogMatch] = {}
    records = catalog.get("records", [])
    for record in records:
        for catalog_path in iter_record_paths(record):
            if not is_mudblazor_related(record, catalog_path):
                continue
            record_name = build_record_name(record, catalog_path)

            for probe_root in build_probe_roots(catalog_path):
                marker = probe_root / REQUIRED_RELATIVE_FILE
                if not marker.is_file():
                    continue

                score = score_match(record, catalog_path, probe_root)
                key = probe_root.as_posix()
                current = resolved_by_root.get(key)
                match = CatalogMatch(
                    record_name=record_name,
                    catalog_path=catalog_path,
                    resolved_root=probe_root,
                    score=score,
                )
                if current is None or match.score > current.score:
                    resolved_by_root[key] = match

    matches = sorted(
        resolved_by_root.values(),
        key=lambda item: (-item.score, item.resolved_root.as_posix(), item.catalog_path.as_posix()),
    )
    return (matches[0] if matches else None), matches, catalog_source


def resolve_source_configuration() -> tuple[str | None, str]:
    best_match, _, catalog_source = resolve_catalog_matches()
    if best_match is None:
        return None, catalog_source
    return str(best_match.catalog_path), f"{catalog_source} (record: {best_match.record_name})"


def resolve_mudblazor_source_root() -> tuple[Path | None, list[Path]]:
    best_match, matches, _ = resolve_catalog_matches()
    if best_match is None:
        return None, []
    return best_match.resolved_root, [match.resolved_root for match in matches]


def registration_commands() -> list[str]:
    script = THIRD_PARTY_SOURCE_CATALOG_SCRIPT
    return _dedupe(
        [
            f"python3 {script} local add /mnt/d/Repositories/References/MudBlazor-9.0.0",
            f"python3 {script} local scan /mnt/d/Repositories/References --update-existing",
        ]
    )


def _build_result() -> dict:
    best_match, matches, catalog_source = resolve_catalog_matches()
    marker = str(REQUIRED_RELATIVE_FILE).replace("\\", "/")

    return {
        "catalog_file": str(THIRD_PARTY_CATALOG_FILE),
        "catalog_source": catalog_source,
        "marker_file": marker,
        "matched_record": best_match.record_name if best_match else None,
        "catalog_path": str(best_match.catalog_path) if best_match else None,
        "candidate_paths": [match.resolved_root.as_posix() for match in matches],
        "resolved_path": str(best_match.resolved_root) if best_match else None,
        "exists": best_match is not None,
        "registration_commands": registration_commands(),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Check if MudBlazor source is available via third-party-source-catalog.")
    parser.add_argument("--json", action="store_true", help="Output machine-readable JSON.")
    args = parser.parse_args()

    result = _build_result()
    success = bool(result["exists"])

    if args.json:
        print(json.dumps(result, indent=2))
    else:
        if success:
            print("[OK] MudBlazor source is available.")
            print(f"Catalog file:    {result['catalog_file']}")
            print(f"Catalog source:  {result['catalog_source']}")
            print(f"Matched record:  {result['matched_record']}")
            print(f"Catalog path:    {result['catalog_path']}")
            print(f"Resolved path:   {result['resolved_path']}")
            print(f"Marker file:     {result['marker_file']}")
        else:
            print("[ERROR] MudBlazor source is not available through third-party-source-catalog.")
            print(f"Catalog file:    {result['catalog_file']}")
            print(f"Catalog source:  {result['catalog_source']}")
            if result["candidate_paths"]:
                print("Checked candidate roots:")
                for candidate in result["candidate_paths"]:
                    print(f"  - {candidate}")
            print()
            print("Source-dependent work must stop here.")
            print("Register MudBlazor source with third-party-source-catalog, then rerun this check.")
            print("Suggested commands:")
            for command in result["registration_commands"]:
                print(f"  {command}")

    return 0 if success else 1


if __name__ == "__main__":
    sys.exit(main())
