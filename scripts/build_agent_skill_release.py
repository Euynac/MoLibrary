#!/usr/bin/env python3
"""Materialize immutable Monica Agent Skill release metadata and artifacts."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import uuid
import zipfile
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

try:
    import jsonschema
except ImportError:  # pragma: no cover - release environments install the pinned requirement
    jsonschema = None

try:
    from agent_skill_release_contract import (
        ReleaseContractError,
        derive_skill_revision_metadata,
        release_timestamps,
        validate_revision_history,
    )
except ModuleNotFoundError:  # pragma: no cover - supports import-by-path test runners
    from scripts.agent_skill_release_contract import (
        ReleaseContractError,
        derive_skill_revision_metadata,
        release_timestamps,
        validate_revision_history,
    )


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
CATALOG_PATH = REPOSITORY_ROOT / ".monica" / "agent-skill-catalog.json"
INDEX_PATH = REPOSITORY_ROOT / ".monica" / "agent-skill-index.json"
SCHEMAS_ROOT = REPOSITORY_ROOT / ".monica" / "schemas"
RELEASE_MANIFEST_SCHEMA_PATH = SCHEMAS_ROOT / "agent-skill-release-manifest.schema.json"
SKILL_DIGEST_ALGORITHM = "sha256-file-manifest-v1"
SEMVER_IDENTIFIER = r"(?:0|[1-9][0-9]*|[0-9A-Za-z-]*[A-Za-z-][0-9A-Za-z-]*)"
SEMVER_PATTERN = re.compile(
    rf"^(?P<major>0|[1-9][0-9]*)\."
    rf"(?P<minor>0|[1-9][0-9]*)\."
    rf"(?P<patch>0|[1-9][0-9]*)"
    rf"(?:-(?P<prerelease>{SEMVER_IDENTIFIER}(?:\.{SEMVER_IDENTIFIER})*))?"
    r"(?:\+(?P<build>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$"
)


ReleaseError = ReleaseContractError


def release_channel_for_version(version: str) -> str:
    match = SEMVER_PATTERN.fullmatch(version)
    if match is None:
        raise ReleaseError(f"Invalid semantic Monica version: {version!r}.")
    return "preview" if match.group("prerelease") is not None else "stable"


def sha256_bytes(content: bytes) -> str:
    return "sha256:" + hashlib.sha256(content).hexdigest()


def load_json(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise ReleaseError(f"{path} must contain a JSON object.")
    return payload


def validate_index_payload(index: dict[str, Any], *, label: str) -> None:
    if jsonschema is None:
        raise ReleaseError("The jsonschema package is required to validate release history.")
    schema = load_json(SCHEMAS_ROOT / "agent-skill-index.schema.json")
    validator = jsonschema.Draft202012Validator(
        schema, format_checker=jsonschema.FormatChecker()
    )
    errors = sorted(validator.iter_errors(index), key=lambda error: list(error.absolute_path))
    if errors:
        raise ReleaseError(f"{label} is invalid: {errors[0].message}")
    validate_revision_history(index, label=label)


def merge_verified_history(
    checked_in: dict[str, Any], previous: dict[str, Any], *, expected_previous_tag: str
) -> dict[str, Any]:
    """Use a verified prior release index without allowing history rewrites."""

    validate_index_payload(checked_in, label="Checked-in index")
    validate_index_payload(previous, label="Previous release index")
    for collection in ("releases", "versions"):
        for key, value in checked_in[collection].items():
            if previous[collection].get(key) != value:
                raise ReleaseError(
                    f"Previous release index rewrites or omits checked-in {collection} entry {key!r}."
                )

    expected_channel = release_channel_for_version(expected_previous_tag.removeprefix("v"))
    if previous["channels"].get(expected_channel) != expected_previous_tag:
        raise ReleaseError(
            f"Previous release index does not point {expected_channel} at {expected_previous_tag}."
        )
    release = previous["releases"].get(expected_previous_tag)
    if release is None:
        raise ReleaseError(f"Previous release index does not contain {expected_previous_tag}.")
    timestamps = release_timestamps(previous, label="Previous release index")
    latest_tag = max(timestamps, key=timestamps.__getitem__)
    if latest_tag != expected_previous_tag:
        raise ReleaseError(
            f"Previous release tag {expected_previous_tag} is not the latest published "
            f"release {latest_tag}."
        )
    expected_asset_base = (
        "https://github.com/Tairitsua/Monica/releases/download/"
        f"{expected_previous_tag}"
    )
    if release.get("assetBaseUrl") != expected_asset_base:
        raise ReleaseError(f"Previous release {expected_previous_tag} has an unexpected asset URL.")
    return json.loads(json.dumps(previous))


def skill_files(catalog: dict[str, Any]) -> list[Path]:
    files: list[Path] = []
    for skill_name, entry in sorted(catalog["skills"].items()):
        if entry.get("ownership") != "monica" or entry.get("managed") is not True:
            continue
        root = REPOSITORY_ROOT / entry["path"]
        if not (root / "SKILL.md").is_file():
            raise ReleaseError(f"Managed skill {skill_name!r} is missing SKILL.md.")
        for file_path in sorted(root.rglob("*")):
            if file_path.is_symlink():
                raise ReleaseError(f"Skill release trees must not contain symlinks: {file_path}")
            if file_path.name == "__pycache__" or file_path.suffix in {".pyc", ".pyo"}:
                raise ReleaseError(f"Generated Python cache is not releasable: {file_path}")
            if file_path.is_file():
                files.append(file_path)
    return files


def skill_file_groups(catalog: dict[str, Any]) -> dict[str, list[Path]]:
    groups: dict[str, list[Path]] = {}
    for skill_name, entry in sorted(catalog["skills"].items()):
        if entry.get("ownership") != "monica" or entry.get("managed") is not True:
            continue
        groups[skill_name] = skill_files({"skills": {skill_name: entry}})
    return groups


def file_manifest_digest(files: list[Path], *, relative_to: Path) -> str:
    manifest = "".join(
        f"{hashlib.sha256(path.read_bytes()).hexdigest()}  "
        f"{path.relative_to(relative_to).as_posix()}\n"
        for path in sorted(files)
    )
    return sha256_bytes(manifest.encode("utf-8"))


def per_skill_digests(catalog: dict[str, Any]) -> dict[str, str]:
    return {
        skill_name: file_manifest_digest(
            files,
            relative_to=REPOSITORY_ROOT / catalog["skills"][skill_name]["path"],
        )
        for skill_name, files in skill_file_groups(catalog).items()
    }


def skill_tree_digest(files: list[Path]) -> str:
    return file_manifest_digest(files, relative_to=REPOSITORY_ROOT)


def validate_inputs(args: argparse.Namespace) -> datetime:
    release_channel_for_version(args.version)
    if args.tag != f"v{args.version}":
        raise ReleaseError("--tag must equal v<version> exactly.")
    if not re.fullmatch(r"[0-9a-f]{40}", args.commit):
        raise ReleaseError("--commit must be a lowercase 40-character Git commit SHA.")
    try:
        published_at = datetime.fromisoformat(args.published_at.replace("Z", "+00:00"))
    except ValueError as exc:
        raise ReleaseError("--published-at must be an RFC 3339 timestamp.") from exc
    if published_at.tzinfo is None:
        raise ReleaseError("--published-at must include a timezone.")
    expected_channel = release_channel_for_version(args.version)
    if args.channel != expected_channel:
        raise ReleaseError(
            f"Version {args.version} must publish through the {expected_channel} channel."
        )
    if (args.previous_index is None) != (args.previous_tag is None):
        raise ReleaseError("--previous-index and --previous-tag must be supplied together.")

    output = args.output.resolve()
    forbidden = {REPOSITORY_ROOT.resolve(), Path.home().resolve(), Path("/").resolve()}
    if output in forbidden or REPOSITORY_ROOT.resolve() not in output.parents:
        raise ReleaseError("--output must be a dedicated directory inside the repository.")
    return published_at


def materialized_index(
    base_index: dict[str, Any],
    *,
    version: str,
    tag: str,
    commit: str,
    channel: str,
    catalog_digest: str,
    tree_digest: str,
    skill_digests: dict[str, str],
    previous_tag: str | None,
    manifest_digest: str,
    published_at: str,
) -> dict[str, Any]:
    index = json.loads(json.dumps(base_index))
    skill_revisions, skill_last_changed_in = derive_skill_revision_metadata(
        index,
        previous_tag=previous_tag,
        current_tag=tag,
        current_published_at=published_at,
        skill_digests=skill_digests,
    )
    existing = index["releases"].get(tag)
    asset_base_url = f"https://github.com/Tairitsua/Monica/releases/download/{tag}"
    release = {
        "monicaVersion": version,
        "tag": tag,
        "commit": commit,
        "catalogDigest": catalog_digest,
        "skillTreeDigest": tree_digest,
        "skillDigestAlgorithm": SKILL_DIGEST_ALGORITHM,
        "skillDigests": skill_digests,
        "skillRevisions": skill_revisions,
        "skillLastChangedIn": skill_last_changed_in,
        "manifestDigest": manifest_digest,
        "publishedAt": published_at,
        "assetBaseUrl": asset_base_url,
        "catalogUrl": f"{asset_base_url}/agent-skill-catalog.json",
        "manifestUrl": f"{asset_base_url}/agent-skill-manifest.json",
    }
    if existing is not None and existing != release:
        raise ReleaseError(f"Release {tag} already exists with different immutable metadata.")
    mapped = index["versions"].get(version)
    if mapped is not None and mapped != tag:
        raise ReleaseError(f"Monica version {version} is already mapped to {mapped}.")
    index["releases"][tag] = release
    index["versions"][version] = tag
    index["channels"][channel] = tag
    return index


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def json_bytes(payload: dict[str, Any]) -> bytes:
    return (json.dumps(payload, ensure_ascii=False, indent=2) + "\n").encode("utf-8")


def verify_release_artifact_parity(
    staging: Path, *, tag: str, catalog_digest: str
) -> None:
    """Prove that published top-level and archived contracts are byte-equivalent."""

    top_catalog = (staging / "agent-skill-catalog.json").read_bytes()
    top_index = (staging / "agent-skill-index.json").read_bytes()
    top_manifest = (staging / "agent-skill-manifest.json").read_bytes()
    if sha256_bytes(top_catalog) != catalog_digest:
        raise ReleaseError("Published catalog bytes do not match catalogDigest.")

    index = json.loads(top_index)
    manifest = json.loads(top_manifest)
    release = index["releases"].get(tag)
    if release is None:
        raise ReleaseError(f"Published index does not contain {tag}.")
    if sha256_bytes(top_manifest) != release["manifestDigest"]:
        raise ReleaseError("Published manifest bytes do not match manifestDigest.")
    if manifest.get("tag") != tag or manifest.get("resolvedCommit") != release.get("commit"):
        raise ReleaseError("Manifest identity does not match the release index.")
    for field in (
        "catalogDigest",
        "skillTreeDigest",
        "skillDigestAlgorithm",
        "skillDigests",
        "skillRevisions",
        "skillLastChangedIn",
    ):
        if manifest.get(field) != release.get(field):
            raise ReleaseError(f"Manifest and index disagree on {field}.")
    asset_base_url = release["assetBaseUrl"]
    if manifest.get("catalogUrl") != release.get("catalogUrl"):
        raise ReleaseError("Manifest and index disagree on catalogUrl.")
    if manifest.get("indexUrl") != f"{asset_base_url}/agent-skill-index.json":
        raise ReleaseError("Manifest indexUrl is not deterministic.")
    if manifest.get("archiveUrl") != f"{asset_base_url}/monica-agent-skills-{tag}.zip":
        raise ReleaseError("Manifest archiveUrl is not deterministic.")

    archive_path = staging / f"monica-agent-skills-{tag}.zip"
    with zipfile.ZipFile(archive_path) as archive:
        archived_catalog = archive.read(".monica/agent-skill-catalog.json")
        archived_index = archive.read(".monica/agent-skill-index.json")
        expected_archive_paths = set(manifest["files"]) | {".monica/agent-skill-index.json"}
        actual_archive_paths = {name for name in archive.namelist() if not name.endswith("/")}
        if actual_archive_paths != expected_archive_paths:
            raise ReleaseError("Archive file set does not match the release manifest scope.")
        for relative_path, expected_digest in manifest["files"].items():
            if sha256_bytes(archive.read(relative_path)) != expected_digest:
                raise ReleaseError(f"Archived file digest mismatch: {relative_path}")
    if archived_catalog != top_catalog:
        raise ReleaseError("Top-level and archived catalog assets differ.")
    if archived_index != top_index:
        raise ReleaseError("Top-level and archived index assets differ.")

    for skill_name, expected_digest in release["skillDigests"].items():
        prefix = f"skills/{skill_name}/"
        lines = "".join(
            f"{digest.removeprefix('sha256:')}  {path.removeprefix(prefix)}\n"
            for path, digest in sorted(manifest["files"].items())
            if path.startswith(prefix)
        )
        if sha256_bytes(lines.encode("utf-8")) != expected_digest:
            raise ReleaseError(f"Per-skill digest mismatch: {skill_name}")


def build_payload(staging: Path, args: argparse.Namespace, published_at: datetime) -> None:
    completed = subprocess.run(
        [sys.executable, str(REPOSITORY_ROOT / "scripts" / "validate_agent_skills.py")],
        cwd=REPOSITORY_ROOT,
        check=False,
    )
    if completed.returncode != 0:
        raise ReleaseError("Canonical Agent Skill validation failed.")

    catalog = load_json(CATALOG_PATH)
    base_index = load_json(INDEX_PATH)
    validate_index_payload(base_index, label="Checked-in index")
    if args.previous_index is not None and args.previous_tag is not None:
        previous_index = load_json(args.previous_index)
        base_index = merge_verified_history(
            base_index,
            previous_index,
            expected_previous_tag=args.previous_tag,
        )
    managed_files = skill_files(catalog)
    catalog_digest = sha256_bytes(CATALOG_PATH.read_bytes())
    tree_digest = skill_tree_digest(managed_files)
    skill_digests = per_skill_digests(catalog)
    previous_tag = args.previous_tag if args.previous_index is not None else None
    published_text = published_at.isoformat().replace("+00:00", "Z")
    skill_revisions, skill_last_changed_in = derive_skill_revision_metadata(
        base_index,
        previous_tag=previous_tag,
        current_tag=args.tag,
        current_published_at=published_text,
        skill_digests=skill_digests,
    )

    payload_root = staging / "payload"
    for source in managed_files:
        destination = payload_root / source.relative_to(REPOSITORY_ROOT)
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
    shutil.copytree(SCHEMAS_ROOT, payload_root / ".monica" / "schemas")
    shutil.copy2(CATALOG_PATH, payload_root / ".monica" / "agent-skill-catalog.json")

    file_hashes = {
        path.relative_to(payload_root).as_posix(): sha256_bytes(path.read_bytes())
        for path in sorted(payload_root.rglob("*"))
        if path.is_file()
    }
    asset_base_url = f"https://github.com/Tairitsua/Monica/releases/download/{args.tag}"
    release_manifest = {
        "schemaVersion": 2,
        "monicaVersion": args.version,
        "tag": args.tag,
        "resolvedCommit": args.commit,
        "catalogDigest": catalog_digest,
        "skillTreeDigest": tree_digest,
        "skillDigestAlgorithm": SKILL_DIGEST_ALGORITHM,
        "skillDigests": skill_digests,
        "skillRevisions": skill_revisions,
        "skillLastChangedIn": skill_last_changed_in,
        "publishedAt": published_text,
        "indexUrl": f"{asset_base_url}/agent-skill-index.json",
        "catalogUrl": f"{asset_base_url}/agent-skill-catalog.json",
        "archiveUrl": f"{asset_base_url}/monica-agent-skills-{args.tag}.zip",
        "fileManifestScope": "release-payload-except-index-v1",
        "files": file_hashes,
    }
    if jsonschema is None:
        raise ReleaseError("The jsonschema package is required to build release artifacts.")
    manifest_schema = load_json(RELEASE_MANIFEST_SCHEMA_PATH)
    manifest_validator = jsonschema.Draft202012Validator(
        manifest_schema, format_checker=jsonschema.FormatChecker()
    )
    manifest_errors = sorted(
        manifest_validator.iter_errors(release_manifest),
        key=lambda error: list(error.absolute_path),
    )
    if manifest_errors:
        raise ReleaseError(f"Release manifest is invalid: {manifest_errors[0].message}")
    manifest_content = json_bytes(release_manifest)
    manifest_digest = sha256_bytes(manifest_content)
    index = materialized_index(
        base_index,
        version=args.version,
        tag=args.tag,
        commit=args.commit,
        channel=args.channel,
        catalog_digest=catalog_digest,
        tree_digest=tree_digest,
        skill_digests=skill_digests,
        previous_tag=previous_tag,
        manifest_digest=manifest_digest,
        published_at=published_text,
    )
    validate_index_payload(index, label="Materialized release index")
    index_content = json_bytes(index)
    (payload_root / ".monica" / "agent-skill-index.json").write_bytes(index_content)
    shutil.copy2(CATALOG_PATH, staging / "agent-skill-catalog.json")
    (staging / "agent-skill-index.json").write_bytes(index_content)
    (staging / "agent-skill-manifest.json").write_bytes(manifest_content)

    archive_path = staging / f"monica-agent-skills-{args.tag}.zip"
    zip_timestamp = published_at.astimezone(timezone.utc).timetuple()[:6]
    # ZIP cannot represent timestamps before 1980.
    zip_timestamp = (max(zip_timestamp[0], 1980), *zip_timestamp[1:])
    with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as archive:
        for source in sorted(path for path in payload_root.rglob("*") if path.is_file()):
            relative = source.relative_to(payload_root).as_posix()
            info = zipfile.ZipInfo(relative, date_time=zip_timestamp)
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o100644 << 16
            archive.writestr(info, source.read_bytes(), compresslevel=9)
    verify_release_artifact_parity(
        staging,
        tag=args.tag,
        catalog_digest=catalog_digest,
    )


def replace_output(staging: Path, output: Path) -> None:
    backup = output.parent / f".{output.name}-backup-{uuid.uuid4().hex}"
    had_output = output.exists()
    try:
        if had_output:
            os.replace(output, backup)
        os.replace(staging, output)
    except OSError:
        if had_output and backup.exists() and not output.exists():
            os.replace(backup, output)
        raise
    else:
        if backup.exists():
            shutil.rmtree(backup)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", required=True)
    parser.add_argument("--tag", required=True)
    parser.add_argument("--commit", required=True)
    parser.add_argument("--channel", choices=("stable", "preview"), required=True)
    parser.add_argument("--published-at", required=True)
    parser.add_argument("--previous-index", type=Path)
    parser.add_argument("--previous-tag")
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        published_at = validate_inputs(args)
        args.output.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(
            dir=args.output.parent, prefix=f".{args.output.name}-staging-"
        ) as temporary_root:
            staged_output = Path(temporary_root) / args.output.name
            staged_output.mkdir()
            build_payload(staged_output, args, published_at)
            replace_output(staged_output, args.output)
        print(f"[ok] Built Agent Skill release artifacts in {args.output}")
        return 0
    except (OSError, ReleaseError, json.JSONDecodeError) as exc:
        print(f"[error] {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
