#!/usr/bin/env python3
"""Canonical file-manifest ordering and digest helpers for Agent Skill releases."""

from __future__ import annotations

import hashlib
from collections.abc import Iterable
from pathlib import Path


FILE_MANIFEST_DIGEST_ALGORITHM = "sha256-file-manifest-v1"


def utf8_path_key(relative_path: str) -> bytes:
    """Return the contract's unsigned ordinal key for a serialized POSIX path."""

    return relative_path.encode("utf-8")


def ordered_relative_files(
    paths: Iterable[Path], *, relative_to: Path
) -> list[tuple[str, Path]]:
    """Normalize paths relative to one root before applying canonical ordering."""

    entries = [
        (path.relative_to(relative_to).as_posix(), path)
        for path in paths
    ]
    return sorted(entries, key=lambda entry: utf8_path_key(entry[0]))


def sha256_digest(content: bytes) -> str:
    return "sha256:" + hashlib.sha256(content).hexdigest()


def file_manifest_digest(entries: Iterable[tuple[str, bytes]]) -> str:
    """Digest ``<file sha256>  <POSIX relative path>\n`` records canonically."""

    materialized = list(entries)
    relative_paths = [relative_path for relative_path, _ in materialized]
    if len(relative_paths) != len(set(relative_paths)):
        raise ValueError("File manifest paths must be unique.")
    manifest = "".join(
        f"{hashlib.sha256(content).hexdigest()}  {relative_path}\n"
        for relative_path, content in sorted(
            materialized,
            key=lambda entry: utf8_path_key(entry[0]),
        )
    )
    return sha256_digest(manifest.encode("utf-8"))


def digest_files(paths: Iterable[Path], *, relative_to: Path) -> str:
    return file_manifest_digest(
        (relative_path, path.read_bytes())
        for relative_path, path in ordered_relative_files(paths, relative_to=relative_to)
    )
