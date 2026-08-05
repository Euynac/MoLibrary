#!/usr/bin/env python3
"""Verify one installed Monica skill against immutable release assets."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path
from typing import Any

try:
    from agent_skill_release_contract import (
        ReleaseContractError,
        release_skill_metadata,
        validate_revision_history,
    )
    from agent_skill_file_manifest import (
        FILE_MANIFEST_DIGEST_ALGORITHM,
        file_manifest_digest as digest_file_manifest,
        ordered_relative_files,
        utf8_path_key,
    )
except ModuleNotFoundError:  # pragma: no cover - supports import-by-path test runners
    from scripts.agent_skill_release_contract import (
        ReleaseContractError,
        release_skill_metadata,
        validate_revision_history,
    )
    from scripts.agent_skill_file_manifest import (
        FILE_MANIFEST_DIGEST_ALGORITHM,
        file_manifest_digest as digest_file_manifest,
        ordered_relative_files,
        utf8_path_key,
    )


DIGEST_ALGORITHM = FILE_MANIFEST_DIGEST_ALGORITHM


class VerificationError(ReleaseContractError):
    """Raised when release assets or installed bytes do not match."""


def digest(content: bytes) -> str:
    return "sha256:" + hashlib.sha256(content).hexdigest()


def load_object(path: Path) -> dict[str, Any]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(payload, dict):
        raise VerificationError(f"{path} must contain a JSON object.")
    return payload


def installed_files(root: Path) -> dict[str, bytes]:
    try:
        resolved_root = root.resolve(strict=True)
    except OSError as exc:
        raise VerificationError(f"Installed skill path is unavailable: {root}") from exc
    if not resolved_root.is_dir():
        raise VerificationError(f"Installed skill path is not a directory: {resolved_root}")

    candidates: list[Path] = []
    for candidate in resolved_root.rglob("*"):
        if candidate.is_symlink():
            raise VerificationError(f"Installed skill contains an unexpected symlink: {candidate}")
        if candidate.is_file():
            candidates.append(candidate)
    return {
        relative_path: candidate.read_bytes()
        for relative_path, candidate in ordered_relative_files(
            candidates,
            relative_to=resolved_root,
        )
    }


def file_manifest_digest(files: dict[str, bytes]) -> str:
    return digest_file_manifest(files.items())


def verify(args: argparse.Namespace) -> dict[str, Any]:
    index_bytes = args.index.read_bytes()
    manifest_bytes = args.manifest.read_bytes()
    catalog_bytes = args.catalog.read_bytes()
    index = json.loads(index_bytes)
    manifest = json.loads(manifest_bytes)
    catalog = json.loads(catalog_bytes)

    if index.get("schemaVersion") != 2 or manifest.get("schemaVersion") != 2:
        raise VerificationError("Unsupported Agent Skill release contract schema version.")
    validate_revision_history(index, label="Release index")

    release = index.get("releases", {}).get(args.tag)
    if not isinstance(release, dict):
        raise VerificationError(f"Release index does not contain {args.tag}.")
    if release.get("skillDigestAlgorithm") != DIGEST_ALGORITHM:
        raise VerificationError("Unsupported per-skill digest algorithm.")
    if manifest.get("fileManifestScope") != "release-payload-except-index-v1":
        raise VerificationError("Unsupported release file-manifest scope.")
    for owner, payload in (("release index", release), ("release manifest", manifest)):
        release_skill_metadata(payload, tag=args.tag, label=owner)
    if digest(manifest_bytes) != release.get("manifestDigest"):
        raise VerificationError("Release manifest bytes do not match manifestDigest.")
    if digest(catalog_bytes) != release.get("catalogDigest"):
        raise VerificationError("Release catalog bytes do not match catalogDigest.")
    if (
        manifest.get("files", {}).get(".monica/agent-skill-catalog.json")
        != release.get("catalogDigest")
    ):
        raise VerificationError("Manifest file contract does not bind the released catalog bytes.")
    for field in (
        "monicaVersion",
        "tag",
        "catalogDigest",
        "skillTreeDigest",
        "skillDigestAlgorithm",
        "skillDigests",
        "skillRevisions",
        "skillLastChangedIn",
        "publishedAt",
    ):
        if release.get(field) != manifest.get(field):
            raise VerificationError(f"Release index and manifest disagree on {field}.")
    if manifest.get("resolvedCommit") != release.get("commit"):
        raise VerificationError("Release index and manifest disagree on commit.")

    asset_base_url = release.get("assetBaseUrl", "")
    if release.get("catalogUrl") != f"{asset_base_url}/agent-skill-catalog.json":
        raise VerificationError("Release catalog URL is not deterministic.")
    if release.get("manifestUrl") != f"{asset_base_url}/agent-skill-manifest.json":
        raise VerificationError("Release manifest URL is not deterministic.")
    if manifest.get("catalogUrl") != release.get("catalogUrl"):
        raise VerificationError("Manifest and index disagree on catalogUrl.")
    if manifest.get("indexUrl") != f"{asset_base_url}/agent-skill-index.json":
        raise VerificationError("Manifest indexUrl is not deterministic.")
    if manifest.get("archiveUrl") != f"{asset_base_url}/monica-agent-skills-{args.tag}.zip":
        raise VerificationError("Manifest archiveUrl is not deterministic.")
    catalog_entry = catalog.get("skills", {}).get(args.skill)
    if not isinstance(catalog_entry, dict) or catalog_entry.get("path") != f"skills/{args.skill}":
        raise VerificationError(f"Catalog does not manage {args.skill} at its canonical path.")

    prefix = f"skills/{args.skill}/"
    expected_files = {
        relative_path.removeprefix(prefix): expected_digest
        for relative_path, expected_digest in manifest.get("files", {}).items()
        if relative_path.startswith(prefix)
    }
    if not expected_files:
        raise VerificationError(f"Release manifest contains no files for {args.skill}.")
    actual_files = installed_files(args.path)
    if set(actual_files) != set(expected_files):
        missing = sorted(set(expected_files) - set(actual_files), key=utf8_path_key)
        unexpected = sorted(set(actual_files) - set(expected_files), key=utf8_path_key)
        raise VerificationError(
            f"Installed file set differs (missing={missing}, unexpected={unexpected})."
        )
    for relative_path, content in actual_files.items():
        if digest(content) != expected_files[relative_path]:
            raise VerificationError(f"Installed file digest mismatch: {relative_path}")

    installed_digest = file_manifest_digest(actual_files)
    expected_skill_digest = release.get("skillDigests", {}).get(args.skill)
    if installed_digest != expected_skill_digest:
        raise VerificationError(f"Installed {args.skill} tree digest does not match the release index.")
    return {
        "ok": True,
        "tag": args.tag,
        "commit": release["commit"],
        "skill": args.skill,
        "path": str(args.path.resolve()),
        "digestAlgorithm": DIGEST_ALGORITHM,
        "skillDigest": installed_digest,
        "skillRevision": release["skillRevisions"][args.skill],
        "skillLastChangedIn": release["skillLastChangedIn"][args.skill],
        "fileCount": len(actual_files),
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--index", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--catalog", type=Path, required=True)
    parser.add_argument("--tag", required=True)
    parser.add_argument("--skill", required=True)
    parser.add_argument("--path", type=Path, required=True)
    parser.add_argument("--json", action="store_true")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    try:
        result = verify(args)
        if args.json:
            print(json.dumps(result, indent=2))
        else:
            print(
                f"[ok] {result['skill']} matches {result['tag']} "
                f"({result['skillDigest']}, {result['fileCount']} files)."
            )
        return 0
    except (OSError, json.JSONDecodeError, ReleaseContractError) as exc:
        payload = {"ok": False, "error": str(exc)}
        if args.json:
            print(json.dumps(payload, indent=2))
        else:
            print(f"[error] {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
