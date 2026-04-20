#!/usr/bin/env python3
"""Catalog and cache third-party source trees for agent inspection."""

from __future__ import annotations

import argparse
import difflib
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tarfile
import textwrap
import uuid
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path

import tomllib

SKILL_NAME = "third-party-source-catalog"
DEFAULT_TTL_HOURS = 24
CONFIG_VERSION = 1
MANIFEST_STUB_NAME = "manifest.stub.json"
MANIFEST_NAME = "manifest.json"
INTERNAL_SKIP_DIRS = {
    ".git",
    ".hg",
    ".svn",
    ".idea",
    ".vs",
    ".vscode",
    ".venv",
    "__pycache__",
    "node_modules",
    "bin",
    "obj",
    "build",
    "dist",
    "target",
    "coverage",
    "out",
    ".tmp",
}
PROJECT_MARKERS = {
    "package.json",
    "pyproject.toml",
    "Cargo.toml",
    "go.mod",
    "pom.xml",
    "build.gradle",
    "build.gradle.kts",
    "Directory.Build.props",
}


@dataclass(frozen=True)
class RuntimePaths:
    """Resolved filesystem locations for skill data."""

    bootstrap_root: Path
    bootstrap_state_dir: Path
    bootstrap_config_path: Path
    root_dir: Path
    state_dir: Path
    catalog_path: Path
    repos_dir: Path


class CatalogError(RuntimeError):
    """Raised when a catalog operation cannot be completed."""


def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    if not getattr(args, "command", None):
        parser.print_help()
        return 0

    try:
        runtime = load_runtime_paths()

        if args.command == "doctor":
            return 0 if run_doctor(verbose=True) else 1

        if not run_doctor(verbose=False):
            run_doctor(verbose=True)
            return 1

        ensure_runtime_dirs(runtime)

        if args.command == "config":
            return handle_config(runtime, args)

        catalog = load_catalog(runtime)

        if args.command == "repo":
            changed = handle_repo(runtime, catalog, args)
        elif args.command == "local":
            changed = handle_local(runtime, catalog, args)
        else:
            raise CatalogError(f"Unknown command: {args.command}")

        if changed:
            save_catalog(runtime, catalog)
        return 0
    except CatalogError as exc:
        print(f"[ERROR] {exc}", file=sys.stderr)
        return 1


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="source_catalog.py",
        description="Catalog, fetch, and enrich third-party source trees.",
    )
    subparsers = parser.add_subparsers(dest="command")

    subparsers.add_parser("doctor", help="Validate gh and git prerequisites.")

    config_parser = subparsers.add_parser("config", help="Inspect or update the active runtime root.")
    config_subparsers = config_parser.add_subparsers(dest="config_command", required=True)
    config_subparsers.add_parser("show", help="Show the bootstrap and active runtime paths.")
    config_set_root = config_subparsers.add_parser("set-root", help="Change the active runtime root.")
    config_set_root.add_argument("path", help="Absolute path for the new runtime root.")

    repo_parser = subparsers.add_parser("repo", help="Manage GitHub-backed catalog entries.")
    repo_subparsers = repo_parser.add_subparsers(dest="repo_command", required=True)

    repo_add = repo_subparsers.add_parser("add-gh", help="Register a GitHub repository.")
    repo_add.add_argument("full_name", help="GitHub repository in owner/repo format.")
    repo_add.add_argument("--alias", action="append", default=[], dest="aliases", help="Additional search alias.")

    repo_remove = repo_subparsers.add_parser("remove", help="Remove one catalog entry and its managed cache.")
    repo_remove.add_argument("query", help="Fuzzy query resolving to a single record.")

    repo_refresh = repo_subparsers.add_parser("refresh", help="Refresh tag metadata from GitHub.")
    repo_refresh.add_argument("query", nargs="?", help="Refresh one matching record.")
    repo_refresh.add_argument("--all", action="store_true", help="Refresh every GitHub-backed record.")

    repo_list = repo_subparsers.add_parser("list", help="List catalog entries or resolve one entry.")
    repo_list.add_argument("query", nargs="?", help="Optional fuzzy query.")

    repo_fetch = repo_subparsers.add_parser("fetch", help="Download a tag archive or clone a repository.")
    repo_fetch.add_argument("query", help="Fuzzy query resolving to a single record.")
    repo_fetch.add_argument("--tag", help="Specific tag to fetch before falling back to clone.")
    repo_fetch.add_argument("--force", action="store_true", help="Replace any existing managed download.")

    local_parser = subparsers.add_parser("local", help="Register or discover manually added source trees.")
    local_subparsers = local_parser.add_subparsers(dest="local_command", required=True)

    local_add = local_subparsers.add_parser("add", help="Register one local source path.")
    local_add.add_argument("path", help="Absolute path to a local source root.")
    local_add.add_argument("--alias", action="append", default=[], dest="aliases", help="Additional search alias.")

    local_scan = local_subparsers.add_parser("scan", help="Recursively discover local source roots.")
    local_scan.add_argument("path", nargs="?", help="Path to scan. Defaults to the active runtime root.")
    local_scan.add_argument("--max-depth", type=int, default=6, help="Maximum recursion depth.")
    local_scan.add_argument(
        "--update-existing",
        action="store_true",
        help="Refresh metadata for existing local sources instead of skipping them.",
    )

    return parser


def load_runtime_paths() -> RuntimePaths:
    script_path = Path(__file__).resolve()
    repo_root = find_repo_root(script_path)
    bootstrap_root = repo_root / ".tmp" / SKILL_NAME
    bootstrap_state_dir = bootstrap_root / "state"
    bootstrap_config_path = bootstrap_state_dir / "config.json"

    config = load_json(bootstrap_config_path, default={})
    root_dir = Path(config.get("root_dir", bootstrap_root)).expanduser()
    if not root_dir.is_absolute():
        root_dir = absolute_path(repo_root / root_dir)
    else:
        root_dir = absolute_path(root_dir)

    return RuntimePaths(
        bootstrap_root=bootstrap_root,
        bootstrap_state_dir=bootstrap_state_dir,
        bootstrap_config_path=bootstrap_config_path,
        root_dir=root_dir,
        state_dir=root_dir / "state",
        catalog_path=root_dir / "state" / "catalog.json",
        repos_dir=root_dir / "repos",
    )


def ensure_runtime_dirs(runtime: RuntimePaths) -> None:
    runtime.bootstrap_state_dir.mkdir(parents=True, exist_ok=True)
    runtime.state_dir.mkdir(parents=True, exist_ok=True)
    runtime.repos_dir.mkdir(parents=True, exist_ok=True)

    if not runtime.bootstrap_config_path.exists():
        save_config(runtime, runtime.root_dir)


def save_config(runtime: RuntimePaths, root_dir: Path) -> None:
    config = {
        "version": CONFIG_VERSION,
        "root_dir": str(root_dir),
        "tag_cache_ttl_hours": DEFAULT_TTL_HOURS,
        "updated_at": utc_now(),
    }
    write_json(runtime.bootstrap_config_path, config)


def load_catalog(runtime: RuntimePaths) -> dict:
    catalog = load_json(runtime.catalog_path, default={"records": []})
    catalog.setdefault("records", [])
    return catalog


def save_catalog(runtime: RuntimePaths, catalog: dict) -> None:
    records = [refresh_record_summary(runtime, record) for record in catalog.get("records", [])]
    payload = {
        "version": CONFIG_VERSION,
        "updated_at": utc_now(),
        "records": sorted(records, key=lambda item: item["canonical_name"].lower()),
    }
    write_json(runtime.catalog_path, payload)


def handle_config(runtime: RuntimePaths, args: argparse.Namespace) -> int:
    if args.config_command == "show":
        print(f"bootstrap_root: {runtime.bootstrap_root}")
        print(f"bootstrap_config: {runtime.bootstrap_config_path}")
        print(f"active_root: {runtime.root_dir}")
        print(f"active_state_dir: {runtime.state_dir}")
        print(f"active_catalog: {runtime.catalog_path}")
        print(f"active_repos_dir: {runtime.repos_dir}")
        return 0

    if args.config_command == "set-root":
        new_root = Path(args.path).expanduser()
        if not new_root.is_absolute():
            raise CatalogError("config set-root requires an absolute path.")

        new_root = absolute_path(new_root)
        new_root.mkdir(parents=True, exist_ok=True)
        save_config(runtime, new_root)
        print(f"[OK] Active runtime root updated to {new_root}")
        print("[INFO] Existing cached data was not migrated.")
        return 0

    raise CatalogError(f"Unknown config command: {args.config_command}")


def handle_repo(runtime: RuntimePaths, catalog: dict, args: argparse.Namespace) -> bool:
    command = args.repo_command

    if command == "add-gh":
        record = add_github_repo(runtime, catalog, args.full_name, args.aliases)
        print_record_detail(runtime, record)
        return True

    if command == "remove":
        record = resolve_unique_record(runtime, catalog, args.query)
        remove_record(runtime, catalog, record)
        print(f"[OK] Removed {record['canonical_name']}")
        return True

    if command == "refresh":
        if args.all and args.query:
            raise CatalogError("Use either a query or --all for repo refresh, not both.")
        if not args.all and not args.query:
            raise CatalogError("repo refresh requires a query or --all.")

        targets = (
            [record for record in catalog["records"] if record.get("github_full_name")]
            if args.all
            else [resolve_unique_record(runtime, catalog, args.query)]
        )
        refreshed = 0
        for record in targets:
            if not record.get("github_full_name"):
                print(f"[SKIP] {record['canonical_name']} has no GitHub identity.")
                continue
            refresh_github_metadata(record)
            refresh_record_summary(runtime, record)
            ensure_stub_manifest(runtime, record)
            refreshed += 1
            print(
                f"[OK] Refreshed {record['canonical_name']} ({len(record.get('available_tags', []))} tags)"
            )
        if refreshed == 0:
            print("[INFO] No GitHub-backed records were refreshed.")
        return refreshed > 0

    if command == "list":
        if not args.query:
            print_catalog_summary(runtime, catalog)
            return False

        resolved = resolve_record_query(runtime, catalog, args.query)
        if resolved["status"] == "ambiguous":
            print_candidates(runtime, args.query, resolved["matches"])
            raise CatalogError("Query matched multiple repositories.")
        if resolved["status"] == "missing":
            raise CatalogError(f"No repository matched '{args.query}'.")

        print_record_detail(runtime, resolved["record"])
        return False

    if command == "fetch":
        record = resolve_unique_record(runtime, catalog, args.query)
        fetch_repository(runtime, record, tag=args.tag, force=args.force)
        ensure_stub_manifest(runtime, record)
        print_record_detail(runtime, record)
        return True

    raise CatalogError(f"Unknown repo command: {command}")


def handle_local(runtime: RuntimePaths, catalog: dict, args: argparse.Namespace) -> bool:
    command = args.local_command

    if command == "add":
        source_path = normalize_source_path(args.path)
        record = register_local_source(runtime, catalog, source_path, aliases=args.aliases, update_existing=True)
        print_record_detail(runtime, record)
        return True

    if command == "scan":
        base_path = normalize_source_path(args.path) if args.path else runtime.root_dir
        discovered = scan_local_sources(base_path, max_depth=args.max_depth, runtime=runtime)
        changed = False
        if not discovered:
            print(f"[INFO] No source roots found under {base_path}")
            return False
        for source_path in discovered:
            already_registered = has_local_source(catalog, source_path)
            if already_registered and not args.update_existing:
                continue
            register_local_source(runtime, catalog, source_path, aliases=[], update_existing=True)
            changed = True
        print(f"[OK] Discovered {len(discovered)} source root(s) under {base_path}")
        return changed

    raise CatalogError(f"Unknown local command: {command}")


def add_github_repo(runtime: RuntimePaths, catalog: dict, full_name: str, aliases: list[str]) -> dict:
    owner, repo = parse_full_name(full_name)
    full_name = f"{owner}/{repo}"

    existing = find_record_by_github_name(catalog, full_name)
    if existing:
        existing["aliases"] = merge_aliases(existing.get("aliases", []), aliases)
        existing["display_name"] = full_name
        existing["canonical_name"] = full_name
        existing["origin_kind"] = "hybrid" if existing.get("local_sources") else "github"
        existing["git_remote_url"] = existing.get("git_remote_url") or f"https://github.com/{full_name}.git"
        refresh_record_summary(runtime, existing)
        ensure_stub_manifest(runtime, existing)
        return existing

    record = new_record(
        repo_id=make_repo_id("gh", full_name),
        canonical_name=full_name,
        display_name=full_name,
        origin_kind="github",
        github_full_name=full_name,
        git_remote_url=f"https://github.com/{full_name}.git",
        aliases=aliases,
    )
    catalog["records"].append(record)
    refresh_record_summary(runtime, record)
    ensure_stub_manifest(runtime, record)
    return record


def remove_record(runtime: RuntimePaths, catalog: dict, record: dict) -> None:
    catalog["records"] = [item for item in catalog["records"] if item["repo_id"] != record["repo_id"]]
    repo_dir = get_repo_dir(runtime, record)
    if repo_dir.exists():
        shutil.rmtree(repo_dir)


def fetch_repository(runtime: RuntimePaths, record: dict, tag: str | None, force: bool) -> None:
    if not record.get("github_full_name"):
        raise CatalogError(f"{record['canonical_name']} does not have a GitHub identity.")

    repo_dir = get_repo_dir(runtime, record)
    repo_dir.mkdir(parents=True, exist_ok=True)
    downloads = record.setdefault("downloads", {"clone": None, "tags": {}})

    if tag:
        tag_dir = repo_dir / "sources" / "github" / "tag" / safe_file_name(tag)
        archive_path = repo_dir / "archives" / f"{safe_file_name(tag)}.tar.gz"
        if force:
            remove_if_exists(tag_dir)
            remove_if_exists(archive_path)

        if not tag_dir.exists():
            try:
                download_github_tarball(record["github_full_name"], tag, archive_path)
                extract_tarball(archive_path, tag_dir)
                print(f"[OK] Downloaded tag {tag} into {tag_dir}")
                downloads.setdefault("tags", {})[tag] = {
                    "mode": "tag-archive",
                    "ref": tag,
                    "archive_path": str(archive_path),
                    "local_path": str(tag_dir),
                    "downloaded_at": utc_now(),
                }
            except CatalogError:
                print(f"[WARN] Tag {tag} was not available. Falling back to clone.")
                fetch_clone(record, repo_dir, downloads, force=force)
        else:
            print(f"[INFO] Reusing existing tag cache at {tag_dir}")
            downloads.setdefault("tags", {})[tag] = {
                "mode": "tag-archive",
                "ref": tag,
                "archive_path": str(archive_path),
                "local_path": str(tag_dir),
                "downloaded_at": utc_now(),
            }
    else:
        fetch_clone(record, repo_dir, downloads, force=force)

    refresh_record_summary(runtime, record)


def fetch_clone(record: dict, repo_dir: Path, downloads: dict, force: bool) -> None:
    clone_dir = repo_dir / "sources" / "github" / "clone"
    if force:
        remove_if_exists(clone_dir)

    if not clone_dir.exists():
        clone_dir.parent.mkdir(parents=True, exist_ok=True)
        url = record.get("git_remote_url") or f"https://github.com/{record['github_full_name']}.git"
        run_command(
            [
                "git",
                "clone",
                "--depth",
                "1",
                "--single-branch",
                "--filter=blob:none",
                url,
                str(clone_dir),
            ]
        )
        print(f"[OK] Cloned {record['canonical_name']} into {clone_dir}")
    else:
        print(f"[INFO] Reusing existing clone cache at {clone_dir}")

    downloads["clone"] = {
        "mode": "clone",
        "ref": "default-branch",
        "archive_path": None,
        "local_path": str(clone_dir),
        "downloaded_at": utc_now(),
    }


def refresh_github_metadata(record: dict) -> None:
    full_name = record.get("github_full_name")
    if not full_name:
        raise CatalogError(f"{record['canonical_name']} does not have a GitHub identity.")

    result = run_command(
        [
            "gh",
            "api",
            "--paginate",
            f"repos/{full_name}/tags?per_page=100",
            "--jq",
            ".[].name",
        ]
    )
    tags = [line.strip() for line in result.stdout.splitlines() if line.strip()]
    record["available_tags"] = tags
    record["tags_refreshed_at"] = utc_now()
    record["git_remote_url"] = record.get("git_remote_url") or f"https://github.com/{full_name}.git"


def register_local_source(
    runtime: RuntimePaths,
    catalog: dict,
    source_path: Path,
    aliases: list[str],
    update_existing: bool,
) -> dict:
    metadata = inspect_source_root(source_path)
    record = match_local_record(catalog, metadata)

    if record is None:
        record = new_record(
            repo_id=make_repo_id("local", metadata["canonical_name"]),
            canonical_name=metadata["canonical_name"],
            display_name=metadata["display_name"],
            origin_kind="local",
            github_full_name=metadata.get("github_full_name"),
            git_remote_url=metadata.get("git_remote_url"),
            aliases=aliases,
        )
        catalog["records"].append(record)
    else:
        record["aliases"] = merge_aliases(record.get("aliases", []), aliases)
        if metadata.get("github_full_name") and not record.get("github_full_name"):
            record["github_full_name"] = metadata["github_full_name"]
            record["canonical_name"] = metadata["github_full_name"]
            record["display_name"] = metadata["github_full_name"]
            record["origin_kind"] = "hybrid"
        record["git_remote_url"] = metadata.get("git_remote_url") or record.get("git_remote_url")

    local_sources = record.setdefault("local_sources", [])
    existing = next((item for item in local_sources if item["path"] == str(source_path)), None)
    payload = {
        "path": str(source_path),
        "path_hash": path_hash(source_path),
        "added_at": utc_now(),
        "detected_root_kind": metadata["detected_root_kind"],
        "display_name": metadata["display_name"],
        "git_remote_url": metadata.get("git_remote_url"),
        "github_full_name": metadata.get("github_full_name"),
        "detected_version": metadata.get("detected_version"),
        "manifest_markers": metadata.get("manifest_markers", []),
    }
    if existing:
        if update_existing:
            existing.update(payload)
    else:
        local_sources.append(payload)

    record["detected_version"] = metadata.get("detected_version") or record.get("detected_version")
    record["updated_at"] = utc_now()
    refresh_record_summary(runtime, record)
    ensure_stub_manifest(runtime, record)
    return record


def inspect_source_root(source_path: Path) -> dict:
    if not source_path.exists():
        raise CatalogError(f"Local source path does not exist: {source_path}")
    if not source_path.is_dir():
        raise CatalogError(f"Local source path is not a directory: {source_path}")

    git_remote_url = get_git_remote_url(source_path)
    github_full_name = parse_github_remote(git_remote_url) if git_remote_url else None
    canonical_name = github_full_name or source_path.name
    manifest_markers = detect_manifest_markers(source_path)
    return {
        "display_name": github_full_name or source_path.name,
        "canonical_name": canonical_name,
        "git_remote_url": git_remote_url,
        "github_full_name": github_full_name,
        "detected_root_kind": "git" if (source_path / ".git").exists() else "manifest",
        "detected_version": detect_version(source_path),
        "manifest_markers": manifest_markers,
    }


def scan_local_sources(base_path: Path, max_depth: int, runtime: RuntimePaths) -> list[Path]:
    base_path = absolute_path(base_path)
    if not base_path.exists():
        raise CatalogError(f"Scan path does not exist: {base_path}")
    if not base_path.is_dir():
        raise CatalogError(f"Scan path is not a directory: {base_path}")
    if max_depth < 0:
        raise CatalogError("--max-depth must be zero or greater.")

    discovered: list[Path] = []
    seen: set[Path] = set()
    skip_paths = {absolute_path(runtime.state_dir), absolute_path(runtime.repos_dir)}
    bootstrap_state = absolute_path(runtime.bootstrap_state_dir)
    if bootstrap_state != absolute_path(runtime.state_dir):
        skip_paths.add(bootstrap_state)

    for current_root, dirnames, filenames in os.walk(base_path):
        current_path = absolute_path(current_root)
        rel_depth = depth_from(base_path, current_path)
        if rel_depth > max_depth:
            dirnames[:] = []
            continue

        dirnames[:] = [
            item
            for item in dirnames
            if item not in INTERNAL_SKIP_DIRS
            and not is_internal_child(current_path / item, skip_paths)
        ]

        if current_path != base_path and is_source_root(current_path, filenames):
            if current_path not in seen:
                discovered.append(current_path)
                seen.add(current_path)
            dirnames[:] = []

    return discovered


def is_source_root(path: Path, filenames: list[str]) -> bool:
    if (path / ".git").exists():
        return True

    if any(name.endswith(".csproj") for name in filenames):
        return True

    return any(marker in filenames for marker in PROJECT_MARKERS)


def has_local_source(catalog: dict, source_path: Path) -> bool:
    path_text = str(source_path)
    for record in catalog.get("records", []):
        for source in record.get("local_sources", []):
            if source.get("path") == path_text:
                return True
    return False


def match_local_record(catalog: dict, metadata: dict) -> dict | None:
    github_full_name = metadata.get("github_full_name")
    if github_full_name:
        existing = find_record_by_github_name(catalog, github_full_name)
        if existing:
            return existing

    canonical_name = normalize_text(metadata["canonical_name"])
    for record in catalog.get("records", []):
        if normalize_text(record.get("canonical_name", "")) == canonical_name:
            return record
    return None


def find_record_by_github_name(catalog: dict, full_name: str) -> dict | None:
    full_name = normalize_text(full_name)
    for record in catalog.get("records", []):
        if normalize_text(record.get("github_full_name", "")) == full_name:
            return record
    return None


def resolve_unique_record(runtime: RuntimePaths, catalog: dict, query: str) -> dict:
    resolved = resolve_record_query(runtime, catalog, query)
    if resolved["status"] == "missing":
        raise CatalogError(f"No repository matched '{query}'.")
    if resolved["status"] == "ambiguous":
        print_candidates(runtime, query, resolved["matches"])
        raise CatalogError("Query matched multiple repositories.")
    return resolved["record"]


def resolve_record_query(runtime: RuntimePaths, catalog: dict, query: str) -> dict:
    scored: list[tuple[int, dict]] = []
    for record in catalog.get("records", []):
        score = score_record(runtime, record, query)
        if score > 0:
            scored.append((score, record))

    if not scored:
        return {"status": "missing", "matches": []}

    scored.sort(key=lambda item: (-item[0], item[1]["canonical_name"].lower()))
    if len(scored) == 1:
        return {"status": "unique", "record": scored[0][1]}

    top_score = scored[0][0]
    second_score = scored[1][0]
    exact_matches = [record for score, record in scored if score >= 1000]
    if len(exact_matches) == 1:
        return {"status": "unique", "record": exact_matches[0]}
    if top_score >= second_score + 200:
        return {"status": "unique", "record": scored[0][1]}
    return {"status": "ambiguous", "matches": scored[:10]}


def score_record(runtime: RuntimePaths, record: dict, query: str) -> int:
    query_norm = normalize_text(query)
    if not query_norm:
        return 0

    best = 0
    for term in build_search_terms(runtime, record):
        term_norm = normalize_text(term)
        if not term_norm:
            continue
        if term_norm == query_norm:
            return 1000
        if term_norm.replace(" ", "") == query_norm.replace(" ", ""):
            best = max(best, 920)
        elif term_norm.startswith(query_norm):
            best = max(best, 760)
        elif query_norm in term_norm:
            best = max(best, 620)
        else:
            ratio = difflib.SequenceMatcher(None, query_norm, term_norm).ratio()
            if ratio >= 0.8:
                best = max(best, int(ratio * 500))
    return best


def build_search_terms(runtime: RuntimePaths, record: dict) -> list[str]:
    terms = {
        record.get("canonical_name", ""),
        record.get("display_name", ""),
        record.get("github_full_name", ""),
        record.get("repo_id", ""),
        *(record.get("aliases", []) or []),
    }

    for source in record.get("local_sources", []):
        source_path = source.get("path")
        if source_path:
            terms.add(Path(source_path).name)
        terms.update(source.get("manifest_markers", []))

    manifest = load_manifest(runtime, record)
    if manifest:
        for field in ("summary", "entry_points_or_public_api", "notes"):
            value = manifest.get(field)
            if isinstance(value, str):
                terms.add(value)
        for key in ("languages", "build_systems", "package_managers", "retrieval_keywords"):
            values = manifest.get(key)
            if isinstance(values, list):
                terms.update(str(item) for item in values)

    return [item for item in terms if item]


def print_catalog_summary(runtime: RuntimePaths, catalog: dict) -> None:
    records = [refresh_record_summary(runtime, record) for record in catalog.get("records", [])]
    if not records:
        print("[INFO] Catalog is empty.")
        return

    for record in sorted(records, key=lambda item: item["canonical_name"].lower()):
        preferred_path = record.get("preferred_path") or "not-downloaded"
        tag_count = len(record.get("available_tags", []))
        print(
            f"{record['canonical_name']} | kind={record['origin_kind']} | "
            f"tags={tag_count} | manifest={record['manifest_status']} | path={preferred_path}"
        )


def print_candidates(runtime: RuntimePaths, query: str, matches: list[tuple[int, dict]]) -> None:
    print(f"[INFO] Query '{query}' matched multiple repositories:")
    for score, record in matches:
        refreshed = refresh_record_summary(runtime, record)
        print(
            f"  - {refreshed['canonical_name']} "
            f"(score={score}, kind={refreshed['origin_kind']}, path={refreshed.get('preferred_path') or 'not-downloaded'})"
        )


def print_record_detail(runtime: RuntimePaths, record: dict) -> None:
    refreshed = refresh_record_summary(runtime, record)
    print(f"repo_id: {refreshed['repo_id']}")
    print(f"canonical_name: {refreshed['canonical_name']}")
    print(f"display_name: {refreshed['display_name']}")
    print(f"origin_kind: {refreshed['origin_kind']}")
    print(f"github_full_name: {refreshed.get('github_full_name') or '-'}")
    print(f"git_remote_url: {refreshed.get('git_remote_url') or '-'}")
    print(f"aliases: {', '.join(refreshed.get('aliases', [])) or '-'}")
    print(f"available_tags: {len(refreshed.get('available_tags', []))}")
    print(f"detected_version: {refreshed.get('detected_version') or '-'}")
    print(f"manifest_status: {refreshed.get('manifest_status') or 'missing'}")
    print(f"preferred_path: {refreshed.get('preferred_path') or 'not-downloaded'}")

    local_sources = refreshed.get("local_sources", [])
    if local_sources:
        print("local_sources:")
        for source in local_sources:
            print(f"  - {source['path']}")


def refresh_record_summary(runtime: RuntimePaths, record: dict) -> dict:
    downloads = record.setdefault("downloads", {"clone": None, "tags": {}})
    local_sources = dedupe_local_sources(record.setdefault("local_sources", []))
    record["local_sources"] = local_sources
    manifest_status = "missing"
    if get_manifest_path(runtime, record, enriched=True).exists():
        manifest_status = "enriched"
    elif get_manifest_path(runtime, record, enriched=False).exists():
        manifest_status = "stub"

    preferred_path = None
    if local_sources:
        local_sources.sort(key=lambda item: item.get("added_at", ""), reverse=True)
        preferred_path = local_sources[0]["path"]
    elif downloads.get("clone") and Path(downloads["clone"]["local_path"]).exists():
        preferred_path = downloads["clone"]["local_path"]
    else:
        tags = sorted(
            downloads.get("tags", {}).values(),
            key=lambda item: item.get("downloaded_at", ""),
            reverse=True,
        )
        for item in tags:
            local_path = item.get("local_path")
            if local_path and Path(local_path).exists():
                preferred_path = local_path
                break

    detected_version = record.get("detected_version")
    if not detected_version and local_sources:
        detected_version = local_sources[0].get("detected_version")

    record["preferred_path"] = preferred_path
    record["manifest_status"] = manifest_status
    record["detected_version"] = detected_version
    record["updated_at"] = utc_now()
    return record


def dedupe_local_sources(local_sources: list[dict]) -> list[dict]:
    if not local_sources:
        return []

    deduped: list[dict] = []
    seen_paths: set[str] = set()
    for source in sorted(local_sources, key=lambda item: item.get("added_at", ""), reverse=True):
        path = source.get("path")
        if not path or path in seen_paths:
            continue
        deduped.append(source)
        seen_paths.add(path)
    return deduped


def ensure_stub_manifest(runtime: RuntimePaths, record: dict) -> None:
    stub_path = get_manifest_path(runtime, record, enriched=False)
    repo_dir = stub_path.parent
    repo_dir.mkdir(parents=True, exist_ok=True)

    manifest = {
        "repo_id": record["repo_id"],
        "canonical_name": record["canonical_name"],
        "display_name": record["display_name"],
        "origin_kind": record["origin_kind"],
        "github_full_name": record.get("github_full_name"),
        "git_remote_url": record.get("git_remote_url"),
        "aliases": record.get("aliases", []),
        "available_tags": record.get("available_tags", []),
        "detected_version": record.get("detected_version"),
        "preferred_path": record.get("preferred_path"),
        "local_sources": record.get("local_sources", []),
        "analysis_status": "stub",
        "retrieval_keywords": build_stub_keywords(record),
        "generated_at": utc_now(),
    }
    write_json(stub_path, manifest)


def build_stub_keywords(record: dict) -> list[str]:
    keywords = {
        record.get("canonical_name", ""),
        record.get("display_name", ""),
        record.get("github_full_name", ""),
        *(record.get("aliases", []) or []),
    }
    for source in record.get("local_sources", []):
        keywords.update(source.get("manifest_markers", []))
        source_path = source.get("path")
        if source_path:
            keywords.add(Path(source_path).name)
    return sorted(item for item in keywords if item)


def load_manifest(runtime: RuntimePaths, record: dict) -> dict | None:
    enriched = get_manifest_path(runtime, record, enriched=True)
    stub = get_manifest_path(runtime, record, enriched=False)
    if enriched.exists():
        return load_json(enriched, default={})
    if stub.exists():
        return load_json(stub, default={})
    return None


def get_manifest_path(runtime: RuntimePaths, record: dict, enriched: bool) -> Path:
    name = MANIFEST_NAME if enriched else MANIFEST_STUB_NAME
    return get_repo_dir(runtime, record) / name


def get_repo_dir(runtime: RuntimePaths, record: dict) -> Path:
    return runtime.repos_dir / record["repo_id"]


def new_record(
    repo_id: str,
    canonical_name: str,
    display_name: str,
    origin_kind: str,
    github_full_name: str | None,
    git_remote_url: str | None,
    aliases: list[str],
) -> dict:
    return {
        "repo_id": repo_id,
        "display_name": display_name,
        "canonical_name": canonical_name,
        "aliases": merge_aliases([], aliases),
        "origin_kind": origin_kind,
        "github_full_name": github_full_name,
        "git_remote_url": git_remote_url,
        "local_sources": [],
        "downloads": {"clone": None, "tags": {}},
        "available_tags": [],
        "tags_refreshed_at": None,
        "detected_version": None,
        "preferred_path": None,
        "manifest_status": "missing",
        "updated_at": utc_now(),
    }


def find_repo_root(start: Path) -> Path:
    for candidate in [start] + list(start.parents):
        if (candidate / ".agentfolder").exists():
            return candidate
    raise CatalogError("Unable to locate the repository root from the skill script path.")


def run_doctor(verbose: bool) -> bool:
    checks = [
        check_command_exists("gh"),
        check_gh_auth(),
        check_command_exists("git"),
        check_git_identity(),
    ]

    success = all(ok for ok, _ in checks)
    if verbose:
        for ok, message in checks:
            prefix = "[OK]" if ok else "[ERROR]"
            stream = sys.stdout if ok else sys.stderr
            print(f"{prefix} {message}", file=stream)
        if not success:
            print(
                textwrap.dedent(
                    """
                    Configure the missing tools before using this skill:
                    - Install GitHub CLI and authenticate with `gh auth login`
                    - Install Git and set identity with:
                      `git config --global user.name "Your Name"`
                      `git config --global user.email "you@example.com"`
                    """
                ).strip(),
                file=sys.stderr,
            )
    return success


def check_command_exists(name: str) -> tuple[bool, str]:
    path = shutil.which(name)
    if not path:
        return False, f"{name} is not installed or not on PATH."
    return True, f"{name} is available at {path}."


def check_gh_auth() -> tuple[bool, str]:
    if not shutil.which("gh"):
        return False, "gh is not installed."
    result = subprocess.run(
        ["gh", "auth", "status"],
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        return False, "gh is not authenticated. Run `gh auth login`."
    return True, "gh authentication is configured."


def check_git_identity() -> tuple[bool, str]:
    if not shutil.which("git"):
        return False, "git is not installed."

    name = subprocess.run(["git", "config", "--get", "user.name"], capture_output=True, text=True)
    email = subprocess.run(["git", "config", "--get", "user.email"], capture_output=True, text=True)
    if name.returncode != 0 or not name.stdout.strip():
        return False, "git user.name is not configured."
    if email.returncode != 0 or not email.stdout.strip():
        return False, "git user.email is not configured."
    return True, "git identity is configured."


def run_command(args: list[str], cwd: Path | None = None) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(
        args,
        cwd=str(cwd) if cwd else None,
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        command = " ".join(args)
        stderr = result.stderr.strip() or result.stdout.strip() or "Unknown error."
        raise CatalogError(f"Command failed: {command}\n{stderr}")
    return result


def download_github_tarball(full_name: str, tag: str, archive_path: Path) -> None:
    archive_path.parent.mkdir(parents=True, exist_ok=True)
    temp_path = archive_path.with_suffix(archive_path.suffix + f".{uuid.uuid4().hex}.tmp")

    with temp_path.open("wb") as handle:
        result = subprocess.run(
            ["gh", "api", f"repos/{full_name}/tarball/{tag}"],
            stdout=handle,
            stderr=subprocess.PIPE,
        )

    if result.returncode != 0:
        remove_if_exists(temp_path)
        stderr = result.stderr.decode("utf-8", errors="replace").strip()
        raise CatalogError(stderr or f"Unable to download tag {tag} from {full_name}.")

    temp_path.replace(archive_path)


def extract_tarball(archive_path: Path, destination: Path) -> None:
    temp_root = destination.parent / f".extract-{uuid.uuid4().hex}"
    remove_if_exists(destination)
    remove_if_exists(temp_root)
    temp_root.mkdir(parents=True, exist_ok=True)

    try:
        with tarfile.open(archive_path, "r:gz") as tar:
            members = tar.getmembers()
            validate_tar_members(members)
            tar.extractall(temp_root, filter="data")

        top_level_items = list(temp_root.iterdir())
        destination.mkdir(parents=True, exist_ok=True)

        if len(top_level_items) == 1 and top_level_items[0].is_dir():
            extracted_root = top_level_items[0]
            for item in extracted_root.iterdir():
                shutil.move(str(item), destination / item.name)
        else:
            for item in top_level_items:
                shutil.move(str(item), destination / item.name)
    finally:
        remove_if_exists(temp_root)


def validate_tar_members(members: list[tarfile.TarInfo]) -> None:
    for member in members:
        member_path = Path(member.name)
        if member_path.is_absolute():
            raise CatalogError("Refusing to extract an archive with absolute paths.")
        if ".." in member_path.parts:
            raise CatalogError("Refusing to extract an archive with parent traversal.")


def parse_full_name(full_name: str) -> tuple[str, str]:
    match = re.fullmatch(r"\s*([A-Za-z0-9_.-]+)/([A-Za-z0-9_.-]+)\s*", full_name)
    if not match:
        raise CatalogError("Expected owner/repo format.")
    return match.group(1), match.group(2)


def parse_github_remote(remote_url: str | None) -> str | None:
    if not remote_url:
        return None

    patterns = [
        r"^https://github\.com/([^/]+)/([^/]+?)(?:\.git)?$",
        r"^git@github\.com:([^/]+)/([^/]+?)(?:\.git)?$",
        r"^ssh://git@github\.com/([^/]+)/([^/]+?)(?:\.git)?$",
    ]
    for pattern in patterns:
        match = re.match(pattern, remote_url)
        if match:
            return f"{match.group(1)}/{match.group(2)}"
    return None


def get_git_remote_url(source_path: Path) -> str | None:
    if not (source_path / ".git").exists():
        return None
    result = subprocess.run(
        ["git", "-C", str(source_path), "remote", "get-url", "origin"],
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        return None
    return result.stdout.strip() or None


def detect_manifest_markers(source_path: Path) -> list[str]:
    markers: list[str] = []
    for item in source_path.iterdir():
        if item.name in PROJECT_MARKERS or item.suffix == ".csproj":
            markers.append(item.name)
    return sorted(set(markers))


def detect_version(source_path: Path) -> str | None:
    tag_version = detect_git_version(source_path)
    if tag_version:
        return tag_version

    extractors = [
        detect_package_json_version,
        detect_pyproject_version,
        detect_cargo_version,
        detect_props_version,
        detect_csproj_version,
        detect_pom_version,
    ]
    for extractor in extractors:
        version = extractor(source_path)
        if version:
            return version
    return None


def detect_git_version(source_path: Path) -> str | None:
    if not (source_path / ".git").exists():
        return None
    for args in (
        ["git", "-C", str(source_path), "describe", "--tags", "--exact-match"],
        ["git", "-C", str(source_path), "describe", "--tags", "--always"],
    ):
        result = subprocess.run(args, capture_output=True, text=True)
        if result.returncode == 0 and result.stdout.strip():
            return result.stdout.strip()
    return None


def detect_package_json_version(source_path: Path) -> str | None:
    package_json = source_path / "package.json"
    if not package_json.exists():
        return None
    data = load_json(package_json, default={})
    version = data.get("version")
    return str(version) if version else None


def detect_pyproject_version(source_path: Path) -> str | None:
    pyproject = source_path / "pyproject.toml"
    if not pyproject.exists():
        return None
    with pyproject.open("rb") as handle:
        data = tomllib.load(handle)
    project = data.get("project", {})
    if isinstance(project, dict) and project.get("version"):
        return str(project["version"])
    poetry = data.get("tool", {}).get("poetry", {})
    if isinstance(poetry, dict) and poetry.get("version"):
        return str(poetry["version"])
    return None


def detect_cargo_version(source_path: Path) -> str | None:
    cargo = source_path / "Cargo.toml"
    if not cargo.exists():
        return None
    with cargo.open("rb") as handle:
        data = tomllib.load(handle)
    package = data.get("package", {})
    if isinstance(package, dict) and package.get("version"):
        return str(package["version"])
    return None


def detect_props_version(source_path: Path) -> str | None:
    props = source_path / "Directory.Build.props"
    if not props.exists():
        return None
    return detect_xml_version(props)


def detect_csproj_version(source_path: Path) -> str | None:
    for candidate in sorted(source_path.glob("*.csproj")):
        version = detect_xml_version(candidate)
        if version:
            return version
    return None


def detect_pom_version(source_path: Path) -> str | None:
    pom = source_path / "pom.xml"
    if not pom.exists():
        return None
    return detect_xml_version(pom)


def detect_xml_version(file_path: Path) -> str | None:
    try:
        tree = ET.parse(file_path)
    except ET.ParseError:
        return None

    root = tree.getroot()
    for element in root.iter():
        name = element.tag.rsplit("}", 1)[-1]
        if name in {"Version", "PackageVersion", "version"} and element.text and element.text.strip():
            return element.text.strip()
    return None


def normalize_source_path(raw_path: str) -> Path:
    path = Path(raw_path).expanduser()
    if not path.is_absolute():
        raise CatalogError("Local source paths must be absolute.")
    return absolute_path(path)


def load_json(path: Path, default):
    if not path.exists():
        return default
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temp_path = path.with_suffix(path.suffix + ".tmp")
    with temp_path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(payload, handle, ensure_ascii=True, indent=2, sort_keys=True)
        handle.write("\n")
    temp_path.replace(path)


def merge_aliases(existing: list[str], new_aliases: list[str]) -> list[str]:
    merged: list[str] = []
    seen: set[str] = set()
    for alias in [*(existing or []), *(new_aliases or [])]:
        normalized = alias.strip()
        if not normalized:
            continue
        key = normalized.casefold()
        if key not in seen:
            merged.append(normalized)
            seen.add(key)
    return merged


def make_repo_id(prefix: str, value: str) -> str:
    normalized = slugify(value)
    digest = hashlib.sha1(value.encode("utf-8")).hexdigest()[:10]
    return f"{prefix}--{normalized}--{digest}"


def slugify(value: str) -> str:
    normalized = re.sub(r"[^a-z0-9]+", "-", value.casefold())
    normalized = normalized.strip("-")
    return normalized or "repo"


def safe_file_name(value: str) -> str:
    return re.sub(r"[^A-Za-z0-9._-]+", "_", value)


def absolute_path(path: str | Path) -> Path:
    return Path(os.path.abspath(os.fspath(path)))


def path_hash(path: Path) -> str:
    return hashlib.sha1(str(path).encode("utf-8")).hexdigest()[:10]


def normalize_text(value: str | None) -> str:
    if not value:
        return ""
    return re.sub(r"[^a-z0-9]+", " ", value.casefold()).strip()


def utc_now() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat()


def remove_if_exists(path: Path) -> None:
    if path.is_symlink() or path.is_file():
        path.unlink()
    elif path.is_dir():
        shutil.rmtree(path)


def depth_from(base_path: Path, current_path: Path) -> int:
    if base_path == current_path:
        return 0
    return len(current_path.relative_to(base_path).parts)


def is_internal_child(candidate: Path, skip_paths: set[Path]) -> bool:
    absolute_candidate = absolute_path(candidate)
    return any(is_relative_to(absolute_candidate, parent) for parent in skip_paths)


def is_relative_to(path: Path, other: Path) -> bool:
    try:
        path.relative_to(other)
        return True
    except ValueError:
        return False


if __name__ == "__main__":
    sys.exit(main())
