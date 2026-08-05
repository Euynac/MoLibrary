#!/usr/bin/env python3
"""Inspect locally built companion OCI images against the repository manifest."""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from dataclasses import asdict, dataclass
from pathlib import Path


@dataclass(frozen=True)
class Finding:
    code: str
    message: str
    image: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Inspect built OCI images declared by monica.manifest.json.")
    parser.add_argument("--root", required=True, type=Path, help="Repository root.")
    parser.add_argument("--json", action="store_true", help="Emit machine-readable JSON.")
    return parser.parse_args()


def docker_inspect(reference: str) -> dict[str, object]:
    completed = subprocess.run(
        ["docker", "image", "inspect", reference],
        check=False,
        capture_output=True,
        text=True,
        timeout=60,
    )
    if completed.returncode != 0:
        detail = completed.stderr.strip() or completed.stdout.strip() or "unknown Docker error"
        raise ValueError(detail)
    payload = json.loads(completed.stdout)
    if not isinstance(payload, list) or len(payload) != 1 or not isinstance(payload[0], dict):
        raise ValueError("docker image inspect returned an unexpected payload.")
    return payload[0]


def validate(root: Path) -> list[Finding]:
    manifest = json.loads((root / "monica.manifest.json").read_text(encoding="utf-8-sig"))
    if not isinstance(manifest, dict) or manifest.get("schemaVersion") != 2:
        raise ValueError("monica.manifest.json must use schemaVersion 2.")
    version = os.environ.get("RELEASE_VERSION") or manifest.get("version")
    repository_url = manifest.get("repositoryUrl")
    images = manifest.get("ociImages", [])
    if not isinstance(version, str) or not isinstance(images, list):
        raise ValueError("Manifest version and ociImages are required.")

    findings: list[Finding] = []
    references: set[str] = set()
    for image in images:
        if not isinstance(image, dict):
            findings.append(Finding("MTI007", "Every OCI image declaration must be an object.", "monica.manifest.json"))
            continue
        repository = image.get("repository")
        companion = image.get("companionPackageId")
        targets = image.get("targets", [])
        if (
            not isinstance(repository, str)
            or not repository
            or not isinstance(companion, str)
            or not companion
            or not isinstance(targets, list)
            or not targets
        ):
            findings.append(
                Finding(
                    "MTI007",
                    "Every OCI image requires repository, companionPackageId, and a non-empty targets array.",
                    "monica.manifest.json",
                )
            )
            continue
        for target in targets:
            if not isinstance(target, dict):
                findings.append(Finding("MTI007", "Every OCI target must be an object.", repository))
                continue
            suffix = target.get("tagSuffix")
            accelerator = target.get("accelerator")
            platform = target.get("platform")
            if (
                not isinstance(suffix, str)
                or not suffix
                or accelerator not in {"cpu", "nvidia"}
                or platform not in {"linux/amd64", "linux/arm64"}
            ):
                findings.append(
                    Finding(
                        "MTI007",
                        "Every OCI target requires tagSuffix, a supported accelerator, and a supported platform.",
                        repository,
                    )
                )
                continue
            reference = f"{repository}:{version}-{suffix}"
            if reference.casefold() in references:
                findings.append(Finding("MTI007", "OCI target resolves to a duplicate image reference.", reference))
                continue
            references.add(reference.casefold())
            try:
                inspected = docker_inspect(reference)
            except (OSError, subprocess.SubprocessError, json.JSONDecodeError, ValueError) as exc:
                findings.append(Finding("MTI001", f"Image could not be inspected: {exc}", reference))
                continue
            config = inspected.get("Config", {})
            if not isinstance(config, dict):
                findings.append(Finding("MTI002", "Image Config is missing.", reference))
                continue
            expected_os, expected_architecture = platform.split("/", 1)
            actual_os = inspected.get("Os")
            actual_architecture = inspected.get("Architecture")
            if actual_os != expected_os or actual_architecture != expected_architecture:
                findings.append(
                    Finding(
                        "MTI008",
                        f"Image platform must be '{platform}', found '{actual_os}/{actual_architecture}'.",
                        reference,
                    )
                )
            user = str(config.get("User") or "").strip()
            if not user or user.casefold() in {"0", "root", "0:0", "root:root"}:
                findings.append(Finding("MTI003", "Runtime image must declare a non-root user.", reference))
            healthcheck = config.get("Healthcheck")
            if not isinstance(healthcheck, dict) or not healthcheck.get("Test"):
                findings.append(Finding("MTI004", "Runtime image must declare a health check.", reference))
            labels = config.get("Labels") or {}
            if not isinstance(labels, dict):
                labels = {}
            expected_labels = {
                "org.opencontainers.image.version": version,
                "io.monica.companion-package": companion,
                "io.monica.accelerator": accelerator,
            }
            if isinstance(repository_url, str):
                expected_labels["org.opencontainers.image.source"] = repository_url
            for name, expected in expected_labels.items():
                if labels.get(name) != expected:
                    findings.append(Finding("MTI005", f"Label '{name}' must be '{expected}'.", reference))
            source = manifest.get("source")
            if isinstance(source, dict) and source.get("available") is True and not labels.get("org.opencontainers.image.revision"):
                findings.append(Finding("MTI006", "Source-available images require org.opencontainers.image.revision.", reference))
    return findings


def main() -> int:
    args = parse_args()
    try:
        findings = validate(args.root.resolve())
    except (OSError, json.JSONDecodeError, ValueError) as exc:
        print(f"Image inspection failed: {exc}", file=sys.stderr)
        return 2
    if args.json:
        print(json.dumps({"status": "failed" if findings else "passed", "findings": [asdict(item) for item in findings]}, indent=2))
    elif findings:
        for finding in findings:
            print(f"[ERROR] {finding.code} {finding.image}: {finding.message}")
    else:
        print("[PASS] Inspected all declared companion OCI images.")
    return 1 if findings else 0


if __name__ == "__main__":
    raise SystemExit(main())
