#!/usr/bin/env python3
"""Inspect a declared set of packed Monica ecosystem artifacts without executing them."""

from __future__ import annotations

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from dataclasses import asdict, dataclass
from pathlib import Path


ABSOLUTE_PATH_PATTERN = re.compile(
    rb"(?:[A-Za-z]:[\\/](?:Users|Code|src)[\\/]|/mnt/[a-z]/|file://)",
    re.IGNORECASE,
)
FORBIDDEN_SUFFIXES = {".csproj", ".sln", ".slnx", ".pfx", ".snk"}
FORBIDDEN_NAMES = {".env", "secrets.json"}
MARKDOWN_IMAGE_PATTERN = re.compile(rb"!\[[^\]]*\]\((?P<path>[^)\s]+)(?:\s+[^)]*)?\)")


@dataclass(frozen=True)
class Finding:
    code: str
    message: str
    artifact: str


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Inspect the exact manifest-declared .nupkg and .snupkg set.")
    parser.add_argument("--root", required=True, type=Path, help="Repository root.")
    parser.add_argument("--artifacts", required=True, type=Path, help="Artifact directory relative to root.")
    parser.add_argument("--package-version", help="Expected effective package version.")
    parser.add_argument("--json", action="store_true", help="Emit machine-readable JSON.")
    return parser.parse_args()


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def child_text(element: ET.Element, name: str) -> str:
    for child in element:
        if local_name(child.tag) == name:
            return (child.text or "").strip()
    return ""


def inspect_archive(
    path: Path,
    expected_version: str | None,
    expected_id: str | None = None,
    expected_internal_dependencies: set[str] | None = None,
    repository_package_ids: set[str] | None = None,
) -> list[Finding]:
    findings: list[Finding] = []

    def add(code: str, message: str) -> None:
        findings.append(Finding(code, message, path.name))

    try:
        archive = zipfile.ZipFile(path)
    except (OSError, zipfile.BadZipFile) as exc:
        add("MTPA001", f"Artifact is not a readable ZIP archive: {exc}")
        return findings

    with archive:
        names = archive.namelist()
        nuspec_names = [name for name in names if name.casefold().endswith(".nuspec")]
        if len(nuspec_names) != 1:
            add("MTPA002", f"Expected exactly one nuspec, found {len(nuspec_names)}.")
            return findings
        try:
            nuspec_payload = archive.read(nuspec_names[0])
            nuspec_root = ET.fromstring(nuspec_payload)
        except (KeyError, ET.ParseError) as exc:
            add("MTPA003", f"Nuspec is unreadable: {exc}")
            return findings

        metadata = next((item for item in nuspec_root.iter() if local_name(item.tag) == "metadata"), None)
        if metadata is None:
            add("MTPA004", "Nuspec metadata element is missing.")
            return findings

        package_id = child_text(metadata, "id")
        version = child_text(metadata, "version")
        if not package_id or not version:
            add("MTPA005", "Nuspec id and version metadata are required.")
        if expected_version and version != expected_version:
            add("MTPA005", f"Nuspec version '{version}' does not match expected '{expected_version}'.")
        if expected_id and package_id != expected_id:
            add("MTPA005", f"Nuspec id '{package_id}' does not match expected '{expected_id}'.")

        if expected_internal_dependencies is not None and repository_package_ids is not None:
            actual_internal_dependencies = {
                (item.get("id") or "").casefold()
                for item in metadata.iter()
                if local_name(item.tag) == "dependency"
                and (item.get("id") or "").casefold() in repository_package_ids
            }
            if actual_internal_dependencies != expected_internal_dependencies:
                add(
                    "MTPA022",
                    "Internal package dependencies do not match monica.manifest.json: "
                    f"expected {sorted(expected_internal_dependencies)}, found {sorted(actual_internal_dependencies)}.",
                )

        expected_name = f"{package_id}.{version}.nupkg".casefold()
        if path.name.casefold() != expected_name:
            add("MTPA006", f"Artifact file name does not match nuspec identity {package_id} {version}.")

        readme_name = child_text(metadata, "readme")
        for metadata_name in ("readme", "icon"):
            artifact_name = child_text(metadata, metadata_name)
            if not artifact_name:
                add("MTPA007", f"Nuspec {metadata_name} metadata is missing.")
            elif artifact_name not in names:
                add("MTPA008", f"Nuspec {metadata_name} '{artifact_name}' is not packed.")
            elif "/" in artifact_name or "\\" in artifact_name:
                add("MTPA009", f"Packed README/icon must use a package-root path: {artifact_name}.")

        if readme_name in names:
            readme_payload = archive.read(readme_name)
            for match in MARKDOWN_IMAGE_PATTERN.finditer(readme_payload):
                image_path = match.group("path").decode("utf-8", errors="replace")
                if image_path.startswith(("https://", "http://", "data:")):
                    continue
                if "/" in image_path or "\\" in image_path:
                    add("MTPA016", f"Packed README image must use a package-root path: {image_path}.")
                elif image_path not in names:
                    add("MTPA017", f"Packed README image is missing from the package root: {image_path}.")

        license_element = next(
            (item for item in metadata if local_name(item.tag) == "license"),
            None,
        )
        if license_element is None:
            add("MTPA010", "Nuspec license metadata is missing.")
        elif license_element.get("type") == "file":
            license_name = (license_element.text or "").strip()
            if license_name not in names:
                add("MTPA011", f"License file '{license_name}' is not packed.")

        for name in names:
            normalized = name.replace("\\", "/")
            parts = [part.casefold() for part in normalized.split("/")]
            suffix = Path(normalized).suffix.casefold()
            if suffix in FORBIDDEN_SUFFIXES or Path(normalized).name.casefold() in FORBIDDEN_NAMES:
                add("MTPA012", f"Forbidden source or secret-bearing file is packed: {name}.")
            if "bin" in parts or "obj" in parts:
                add("MTPA013", f"Build output path is packed: {name}.")
            try:
                payload = archive.read(name)
            except KeyError:
                continue
            if len(payload) <= 2_000_000 and ABSOLUTE_PATH_PATTERN.search(payload):
                add("MTPA014", f"Machine-specific absolute path found in packed file: {name}.")
            if repository_package_ids is not None and normalized.casefold().endswith(".dll"):
                assembly_name = Path(normalized).stem.casefold()
                sibling_ids = repository_package_ids - {package_id.casefold()}
                if assembly_name in sibling_ids:
                    add("MTPA023", f"Sibling package assembly is embedded instead of declared as a dependency: {name}.")

    return findings


def inspect_symbol_archive(path: Path) -> list[Finding]:
    findings: list[Finding] = []

    def add(code: str, message: str) -> None:
        findings.append(Finding(code, message, path.name))

    try:
        archive = zipfile.ZipFile(path)
    except (OSError, zipfile.BadZipFile) as exc:
        add("MTPA018", f"Symbol artifact is not a readable ZIP archive: {exc}")
        return findings

    with archive:
        names = archive.namelist()
        if len([name for name in names if name.casefold().endswith(".nuspec")]) != 1:
            add("MTPA019", "Symbol artifact must contain exactly one nuspec.")
        if not any(name.casefold().endswith(".pdb") for name in names):
            add("MTPA020", "Symbol artifact does not contain a PDB.")
        for name in names:
            normalized = name.replace("\\", "/")
            suffix = Path(normalized).suffix.casefold()
            if suffix in FORBIDDEN_SUFFIXES or Path(normalized).name.casefold() in FORBIDDEN_NAMES:
                add("MTPA021", f"Forbidden source or secret-bearing file is packed: {name}.")

    return findings


def main() -> int:
    args = parse_args()
    root = args.root.resolve()
    artifacts = args.artifacts if args.artifacts.is_absolute() else root / args.artifacts
    if not artifacts.is_dir():
        print(f"Artifact directory does not exist: {artifacts}", file=sys.stderr)
        return 2

    manifest_path = root / "monica.manifest.json"
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exc:
        print(f"Could not read monica.manifest.json: {exc}", file=sys.stderr)
        return 2
    if not isinstance(manifest, dict) or manifest.get("schemaVersion") != 2:
        print("monica.manifest.json must use schemaVersion 2.", file=sys.stderr)
        return 2
    raw_packages = manifest.get("packages")
    if not isinstance(raw_packages, list) or not raw_packages:
        print("monica.manifest.json packages must contain at least one package.", file=sys.stderr)
        return 2
    declared: dict[str, set[str]] = {}
    for raw in raw_packages:
        if not isinstance(raw, dict) or not isinstance(raw.get("packageId"), str):
            print("Every manifest package requires packageId.", file=sys.stderr)
            return 2
        package_id = raw["packageId"]
        dependencies = raw.get("packageDependencies", [])
        if not isinstance(dependencies, list) or not all(isinstance(item, str) for item in dependencies):
            print(f"Invalid packageDependencies for {package_id}.", file=sys.stderr)
            return 2
        declared[package_id] = {item.casefold() for item in dependencies}

    expected_version = args.package_version or manifest.get("version")
    if not isinstance(expected_version, str) or not expected_version:
        print("An expected package version is required.", file=sys.stderr)
        return 2

    packages = sorted(path for path in artifacts.glob("*.nupkg") if not path.name.endswith(".snupkg"))

    findings: list[Finding] = []
    expected_names = {
        f"{package_id}.{expected_version}.nupkg".casefold(): package_id
        for package_id in declared
    }
    actual_names = {package.name.casefold(): package for package in packages}
    expected_symbol_names = {
        f"{package_id}.{expected_version}.snupkg".casefold()
        for package_id in declared
    }
    actual_symbol_names = {
        package.name.casefold()
        for package in artifacts.glob("*.snupkg")
    }
    for missing in sorted(set(expected_names) - set(actual_names)):
        findings.append(Finding("MTPA024", "Declared package artifact is missing.", missing))
    for extra in sorted(set(actual_names) - set(expected_names)):
        findings.append(Finding("MTPA025", "Undeclared package artifact was produced.", actual_names[extra].name))
    for extra in sorted(actual_symbol_names - expected_symbol_names):
        findings.append(Finding("MTPA026", "Undeclared symbol artifact was produced.", extra))

    repository_ids = {package_id.casefold() for package_id in declared}
    inspected_count = 0
    for expected_name, expected_id in expected_names.items():
        package = actual_names.get(expected_name)
        if package is None:
            continue
        inspected_count += 1
        findings.extend(
            inspect_archive(
                package,
                expected_version,
                expected_id,
                declared[expected_id],
                repository_ids,
            )
        )
        symbol_package = package.with_suffix(".snupkg")
        if not symbol_package.is_file():
            findings.append(Finding("MTPA015", "Matching .snupkg is missing.", package.name))
        else:
            findings.extend(inspect_symbol_archive(symbol_package))

    if args.json:
        print(json.dumps({"status": "failed" if findings else "passed", "findings": [asdict(item) for item in findings]}, indent=2))
    elif findings:
        for finding in findings:
            print(f"[ERROR] {finding.code} {finding.artifact}: {finding.message}")
        print(f"\nInspected {inspected_count} declared package(s): {len(findings)} error(s).")
    else:
        print(f"[PASS] Inspected {inspected_count} declared package(s) and matching symbol artifacts.")

    return 1 if findings else 0


if __name__ == "__main__":
    raise SystemExit(main())
