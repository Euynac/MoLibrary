#!/usr/bin/env python3
"""Validate declared OCI targets against Docker Buildx Bake's normalized graph."""

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
    path: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate OCI targets declared by monica.manifest.json.")
    parser.add_argument("--root", required=True, type=Path, help="Repository root.")
    parser.add_argument("--json", action="store_true", help="Emit machine-readable JSON.")
    return parser.parse_args()


def load_manifest(root: Path) -> dict[str, object]:
    path = root / "monica.manifest.json"
    payload = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(payload, dict) or payload.get("schemaVersion") != 2:
        raise ValueError("monica.manifest.json must use schemaVersion 2.")
    return payload


def bake_graph(root: Path, bake_file: str, version: str) -> dict[str, object]:
    environment = os.environ.copy()
    environment["RELEASE_VERSION"] = version
    completed = subprocess.run(
        ["docker", "buildx", "bake", "--file", bake_file, "--print"],
        cwd=root,
        env=environment,
        check=False,
        capture_output=True,
        text=True,
        timeout=60,
    )
    if completed.returncode != 0:
        detail = completed.stderr.strip() or completed.stdout.strip() or "unknown Docker Buildx error"
        raise ValueError(f"docker buildx bake --print failed for {bake_file}: {detail}")
    payload = json.loads(completed.stdout)
    if not isinstance(payload, dict):
        raise ValueError(f"Docker Buildx returned a non-object graph for {bake_file}.")
    return payload


def validate(root: Path, manifest: dict[str, object]) -> list[Finding]:
    findings: list[Finding] = []
    version = os.environ.get("RELEASE_VERSION") or manifest.get("version")
    images = manifest.get("ociImages", [])
    if not isinstance(version, str):
        return [Finding("MTO001", "Manifest version is required.", "monica.manifest.json")]
    if not isinstance(images, list):
        return [Finding("MTO002", "ociImages must be an array.", "monica.manifest.json")]

    declarations_by_bake_file: dict[str, list[tuple[dict[str, object], dict[str, object]]]] = {}
    for image in images:
        if not isinstance(image, dict):
            findings.append(Finding("MTO002", "Every OCI image must be an object.", "monica.manifest.json"))
            continue
        bake_file = image.get("bakeFilePath")
        if not isinstance(bake_file, str):
            findings.append(Finding("MTO003", "OCI image requires bakeFilePath.", "monica.manifest.json"))
            continue
        raw_targets = image.get("targets", [])
        if not isinstance(raw_targets, list) or not raw_targets:
            findings.append(Finding("MTO002", "OCI targets must be an array.", "monica.manifest.json"))
            continue
        for declared in raw_targets:
            if not isinstance(declared, dict) or not isinstance(declared.get("bakeTarget"), str):
                findings.append(Finding("MTO006", "Every OCI target requires bakeTarget.", "monica.manifest.json"))
                continue
            declarations_by_bake_file.setdefault(bake_file, []).append((image, declared))

    for bake_file, declarations in declarations_by_bake_file.items():
        try:
            graph = bake_graph(root, bake_file, version)
        except (OSError, subprocess.SubprocessError, json.JSONDecodeError, ValueError) as exc:
            findings.append(Finding("MTO004", str(exc), bake_file))
            continue
        graph_targets = graph.get("target", {})
        if not isinstance(graph_targets, dict):
            findings.append(Finding("MTO005", "Bake graph target collection is missing.", bake_file))
            continue

        expected_target_names = [declared["bakeTarget"] for _, declared in declarations]
        expected_target_set = set(expected_target_names)
        actual_target_set = set(graph_targets)
        if len(expected_target_names) != len(expected_target_set) or actual_target_set != expected_target_set:
            findings.append(
                Finding(
                    "MTO012",
                    "Bake target collection must exactly match monica.manifest.json: "
                    f"expected {sorted(expected_target_set)}, found {sorted(actual_target_set)}.",
                    bake_file,
                )
            )

        groups = graph.get("group", {})
        default_group = groups.get("default") if isinstance(groups, dict) else None
        default_targets = default_group.get("targets") if isinstance(default_group, dict) else None
        if (
            not isinstance(default_targets, list)
            or not all(isinstance(value, str) for value in default_targets)
            or len(default_targets) != len(set(default_targets))
            or set(default_targets) != expected_target_set
        ):
            findings.append(
                Finding(
                    "MTO011",
                    "Bake group.default must contain every declared target exactly once: "
                    f"expected {sorted(expected_target_set)}, found {default_targets!r}.",
                    bake_file,
                )
            )

        for image, declared in declarations:
            context_path = image.get("contextPath")
            dockerfile_path = image.get("dockerfilePath")
            repository = image.get("repository")
            if not all(isinstance(value, str) for value in (context_path, dockerfile_path, repository)):
                findings.append(Finding("MTO003", "OCI paths and repository are required strings.", "monica.manifest.json"))
                continue
            try:
                expected_dockerfile = Path(dockerfile_path).relative_to(Path(context_path)).as_posix()
            except ValueError:
                expected_dockerfile = dockerfile_path
            target_name = declared["bakeTarget"]
            actual = graph_targets.get(target_name)
            if not isinstance(actual, dict):
                findings.append(Finding("MTO007", f"Declared bake target '{target_name}' was not found.", bake_file))
                continue
            expectations = {
                "context": context_path,
                "dockerfile": expected_dockerfile,
                "target": declared.get("stage"),
            }
            for field, expected in expectations.items():
                actual_value = actual.get(field)
                if actual_value != expected:
                    findings.append(
                        Finding("MTO008", f"Target '{target_name}' {field} must be '{expected}', found '{actual_value}'.", bake_file)
                    )
            platform = declared.get("platform")
            platforms = actual.get("platforms", [])
            if not isinstance(platforms, list) or platforms != [platform]:
                findings.append(Finding("MTO009", f"Target '{target_name}' platforms must be exactly ['{platform}'].", bake_file))
            expected_tag = f"{repository}:{version}-{declared.get('tagSuffix')}"
            tags = actual.get("tags", [])
            if not isinstance(tags, list) or tags != [expected_tag]:
                findings.append(Finding("MTO010", f"Target '{target_name}' tag must be '{expected_tag}'.", bake_file))

    return findings


def main() -> int:
    args = parse_args()
    root = args.root.resolve()
    try:
        manifest = load_manifest(root)
        findings = validate(root, manifest)
    except (OSError, json.JSONDecodeError, ValueError) as exc:
        print(f"OCI validation failed: {exc}", file=sys.stderr)
        return 2

    if args.json:
        print(json.dumps({"status": "failed" if findings else "passed", "findings": [asdict(item) for item in findings]}, indent=2))
    elif findings:
        for finding in findings:
            print(f"[ERROR] {finding.code} {finding.path}: {finding.message}")
    else:
        count = sum(len(image.get("targets", [])) for image in manifest.get("ociImages", []) if isinstance(image, dict))
        print(f"[PASS] Validated {count} declared OCI bake target(s).")
    return 1 if findings else 0


if __name__ == "__main__":
    raise SystemExit(main())
