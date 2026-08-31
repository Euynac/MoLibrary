#!/usr/bin/env python3
"""Project canonical Monica skills into repository-local agent directories."""

from __future__ import annotations

import argparse
import errno
import hashlib
import json
import os
import shutil
import sys
import tempfile
import time
from pathlib import Path
from typing import Iterable


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
CATALOG_PATH = REPOSITORY_ROOT / ".monica" / "agent-skill-catalog.json"
INDEX_PATH = REPOSITORY_ROOT / ".monica" / "agent-skill-index.json"
PROJECTION_PATHS = (
    REPOSITORY_ROOT / ".agents" / "skills",
    REPOSITORY_ROOT / ".claude" / "skills",
)
IGNORED_NAMES = {".DS_Store", "__pycache__"}
IGNORED_SUFFIXES = {".pyc", ".pyo"}
REPLACE_RETRY_DELAYS = (0.05, 0.1, 0.2, 0.4, 0.8)


class ProjectionError(RuntimeError):
    """Raised when the canonical tree or a projection is invalid."""


def _load_catalog() -> dict[str, object]:
    try:
        payload = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ProjectionError(f"Cannot read {CATALOG_PATH}: {exc}") from exc
    if not isinstance(payload, dict):
        raise ProjectionError(f"{CATALOG_PATH} must contain a JSON object.")
    return payload


def _load_managed_skills(catalog: dict[str, object] | None = None) -> dict[str, Path]:
    catalog = _load_catalog() if catalog is None else catalog

    managed: dict[str, Path] = {}
    skills = catalog.get("skills", {})
    if not isinstance(skills, dict):
        raise ProjectionError("Catalog skills must be an object.")
    for name, entry in skills.items():
        if not isinstance(name, str) or not isinstance(entry, dict):
            raise ProjectionError("Catalog skill entries must be named objects.")
        if entry.get("ownership") != "monica" or entry.get("managed") is not True:
            continue
        relative_path = Path(entry.get("path", ""))
        expected = Path("skills") / name
        if relative_path != expected:
            raise ProjectionError(
                f"Managed skill {name!r} must use canonical path {expected.as_posix()!r}."
            )
        canonical = REPOSITORY_ROOT / relative_path
        if not (canonical / "SKILL.md").is_file():
            raise ProjectionError(f"Managed skill {name!r} has no SKILL.md at {canonical}.")
        managed[name] = canonical

    canonical_names = {
        child.name
        for child in (REPOSITORY_ROOT / "skills").iterdir()
        if child.is_dir() and child.name.startswith("monica-")
    }
    if canonical_names != set(managed):
        missing = sorted(canonical_names - set(managed))
        nonexistent = sorted(set(managed) - canonical_names)
        details = []
        if missing:
            details.append(f"not cataloged: {', '.join(missing)}")
        if nonexistent:
            details.append(f"missing from skills/: {', '.join(nonexistent)}")
        raise ProjectionError("Canonical/catalog skill mismatch (" + "; ".join(details) + ").")
    return dict(sorted(managed.items()))


def _load_retired_aliases(catalog: dict[str, object] | None = None) -> set[str]:
    catalog = _load_catalog() if catalog is None else catalog
    aliases = catalog.get("aliases", {})
    if not isinstance(aliases, dict) or not all(isinstance(name, str) for name in aliases):
        raise ProjectionError("Catalog aliases must be an object with string keys.")
    return set(aliases)


def _iter_files(root: Path) -> Iterable[Path]:
    if root.is_symlink():
        raise ProjectionError(f"Skill roots must not be symlinks: {root}")
    for candidate in sorted(root.rglob("*")):
        if candidate.is_symlink():
            raise ProjectionError(f"Skill trees must not contain symlinks: {candidate}")
        if candidate.name in IGNORED_NAMES or candidate.suffix in IGNORED_SUFFIXES:
            raise ProjectionError(f"Generated or platform-specific file found in skill tree: {candidate}")
        if candidate.is_file():
            yield candidate


def build_manifest(skill_roots: dict[str, Path]) -> dict[str, str]:
    """Return stable relative paths and SHA-256 values for managed skill files."""

    manifest: dict[str, str] = {}
    for name, root in skill_roots.items():
        for file_path in _iter_files(root):
            relative = (Path(name) / file_path.relative_to(root)).as_posix()
            manifest[relative] = hashlib.sha256(file_path.read_bytes()).hexdigest()
    return manifest


def _is_present(path: Path) -> bool:
    return path.exists() or path.is_symlink()


def _retired_alias_entries(
    projection: Path, managed_names: set[str], retired_aliases: set[str]
) -> list[tuple[Path, str]]:
    """Return exact retired aliases without inspecting any other external tree."""

    if not projection.is_dir() or projection.is_symlink():
        return []
    reserved: list[tuple[Path, str]] = []
    for entry in sorted(projection.iterdir(), key=lambda path: path.name):
        if entry.name in managed_names or not (entry.is_dir() or entry.is_symlink()):
            continue
        if entry.name in retired_aliases:
            reserved.append((entry, f"retired alias directory {entry.name}"))
    return reserved


def _inspect_projection(
    projection: Path, managed_names: set[str], retired_aliases: set[str]
) -> tuple[dict[str, str], list[str]]:
    """Inspect managed roots exactly while treating all external content as opaque."""

    manifest: dict[str, str] = {}
    issues: list[str] = []
    if not _is_present(projection):
        return manifest, issues
    if projection.is_symlink() or not projection.is_dir():
        return manifest, ["projection root is not a real directory"]

    for name in sorted(managed_names):
        skill_root = projection / name
        if not _is_present(skill_root):
            continue
        if skill_root.is_symlink() or not skill_root.is_dir():
            issues.append(f"invalid managed skill directory {name}")
            continue
        try:
            for file_path in _iter_files(skill_root):
                relative = (Path(name) / file_path.relative_to(skill_root)).as_posix()
                manifest[relative] = hashlib.sha256(file_path.read_bytes()).hexdigest()
        except ProjectionError as exc:
            issues.append(f"invalid managed skill directory {name}: {exc}")

    issues.extend(
        reason
        for _, reason in _retired_alias_entries(
            projection, managed_names, retired_aliases
        )
    )
    return manifest, issues


def _describe_diff(expected: dict[str, str], actual: dict[str, str]) -> list[str]:
    expected_paths = set(expected)
    actual_paths = set(actual)
    lines = [f"missing {path}" for path in sorted(expected_paths - actual_paths)]
    lines.extend(f"unexpected {path}" for path in sorted(actual_paths - expected_paths))
    lines.extend(
        f"changed {path}"
        for path in sorted(expected_paths & actual_paths)
        if expected[path] != actual[path]
    )
    return lines


def _projection_differences(
    projection: Path,
    expected: dict[str, str],
    managed_names: set[str],
    retired_aliases: set[str],
) -> list[str]:
    actual, issues = _inspect_projection(projection, managed_names, retired_aliases)
    return [*issues, *_describe_diff(expected, actual)]


def _projection_label(projection: Path) -> str:
    try:
        return projection.relative_to(REPOSITORY_ROOT).as_posix()
    except ValueError:
        return str(projection)


def check_projections(
    expected: dict[str, str],
    managed_names: set[str],
    retired_aliases: set[str],
    *,
    projection_paths: Iterable[Path] = PROJECTION_PATHS,
) -> bool:
    clean = True
    for projection in projection_paths:
        differences = _projection_differences(
            projection, expected, managed_names, retired_aliases
        )
        if differences:
            clean = False
            print(f"[drift] {_projection_label(projection)}")
            for difference in differences[:40]:
                print(f"  - {difference}")
            if len(differences) > 40:
                print(f"  - ... {len(differences) - 40} more differences")
        else:
            print(f"[ok] {_projection_label(projection)}")
    return clean


def _populate_staging(staging: Path, skill_roots: dict[str, Path]) -> None:
    for name, source in skill_roots.items():
        shutil.copytree(source, staging / name, copy_function=shutil.copy2)


def _remove_path(path: Path) -> None:
    if path.is_symlink() or path.is_file():
        path.unlink()
    elif path.is_dir():
        shutil.rmtree(path)


def _replace_path(source: Path, destination: Path) -> None:
    """Retry transient Windows/WSL directory-sharing failures."""

    for attempt, delay in enumerate((*REPLACE_RETRY_DELAYS, None)):
        try:
            os.replace(source, destination)
            return
        except OSError as exc:
            retryable = exc.errno in {errno.EACCES, errno.EPERM, errno.EBUSY}
            if not retryable or delay is None:
                raise
            time.sleep(delay)


def _replace_projection(
    projection: Path,
    skill_roots: dict[str, Path],
    retired_aliases: set[str],
) -> None:
    """Transactionally replace only Monica-owned directories in a projection."""

    projection.parent.mkdir(parents=True, exist_ok=True)
    if _is_present(projection) and (projection.is_symlink() or not projection.is_dir()):
        raise ProjectionError(f"Projection root must be a real directory: {projection}")
    created_projection = not projection.exists()
    projection.mkdir(exist_ok=True)
    managed_names = set(skill_roots)
    with tempfile.TemporaryDirectory(
        dir=projection.parent, prefix=f".{projection.name}-staging-"
    ) as temporary_root:
        temporary = Path(temporary_root)
        staging = temporary / "staging"
        backup = temporary / "backup"
        staging.mkdir()
        backup.mkdir()
        _populate_staging(staging, skill_roots)

        stale_entries = [
            path
            for path, _ in _retired_alias_entries(
                projection, managed_names, retired_aliases
            )
        ]
        replaced_entries = [
            projection / name
            for name in sorted(managed_names)
            if _is_present(projection / name)
        ]
        moved_entries: list[Path] = []
        installed_entries: list[Path] = []
        try:
            for existing in [*replaced_entries, *stale_entries]:
                _replace_path(existing, backup / existing.name)
                moved_entries.append(existing)
            for name in sorted(managed_names):
                destination = projection / name
                _replace_path(staging / name, destination)
                installed_entries.append(destination)
        except OSError as exc:
            for installed in reversed(installed_entries):
                _remove_path(installed)
            for original in reversed(moved_entries):
                _replace_path(backup / original.name, original)
            if created_projection:
                try:
                    projection.rmdir()
                except OSError:
                    pass
            raise ProjectionError(f"Cannot replace managed skills in {projection}: {exc}") from exc


def write_projections(
    skill_roots: dict[str, Path],
    expected: dict[str, str],
    retired_aliases: set[str],
    *,
    projection_paths: Iterable[Path] = PROJECTION_PATHS,
) -> None:
    managed_names = set(skill_roots)
    for projection in projection_paths:
        _replace_projection(projection, skill_roots, retired_aliases)
        differences = _projection_differences(
            projection, expected, managed_names, retired_aliases
        )
        if differences:
            raise ProjectionError(
                f"Projection verification failed for {projection}: {differences[0]}"
            )
        print(f"[written] {_projection_label(projection)}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Synchronize byte-equivalent .agents and .claude Monica skill projections."
    )
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument(
        "--write",
        action="store_true",
        help="Replace catalog-managed Monica skills while preserving external skills.",
    )
    mode.add_argument(
        "--check",
        action="store_true",
        help="Fail when managed Monica skills drift; ignore external skill trees.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        catalog = _load_catalog()
        skill_roots = _load_managed_skills(catalog)
        retired_aliases = _load_retired_aliases(catalog)
        expected = build_manifest(skill_roots)
        if args.write:
            write_projections(skill_roots, expected, retired_aliases)
            return 0
        return 0 if check_projections(expected, set(skill_roots), retired_aliases) else 1
    except (OSError, ProjectionError) as exc:
        print(f"[error] {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
