#!/usr/bin/env python3
"""Validate Monica's public package maturity catalog against the project tree."""

from __future__ import annotations

import argparse
import json
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CATALOG_PATH = ROOT / "eng" / "package-tiers.json"
VALID_TIER_IDS = ("stable", "integrations", "labs")
EXPECTED_DEPENDENCY_POLICY = {
    "stable": ["stable"],
    "integrations": ["stable", "integrations"],
    "labs": ["stable", "integrations", "labs"],
}


def fail(message: str) -> None:
    print(f"package tier validation failed: {message}", file=sys.stderr)
    raise SystemExit(1)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate Monica's public package maturity catalog."
    )
    parser.add_argument(
        "--list-public-projects",
        action="store_true",
        help="Print validated public package project paths instead of the summary.",
    )
    return parser.parse_args()


def read_project(project_path: Path) -> tuple[str, bool]:
    project = ET.parse(project_path).getroot()
    properties = {
        child.tag: (child.text or "").strip()
        for group in project.findall("PropertyGroup")
        for child in group
    }
    package_id = properties.get("PackageId") or project_path.stem
    is_packable = properties.get("IsPackable", "true").lower() != "false"
    return package_id, is_packable


def read_project_references(project_path: Path) -> list[Path]:
    project = ET.parse(project_path).getroot()
    references: list[Path] = []
    for reference in project.findall(".//ProjectReference"):
        include = reference.get("Include")
        if not include:
            continue
        references.append((project_path.parent / include.replace("\\", "/")).resolve())
    return references


def main() -> None:
    args = parse_args()
    catalog = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))
    if catalog.get("schemaVersion") != 1:
        fail("schemaVersion must be 1")

    tiers = catalog.get("tiers")
    if not isinstance(tiers, list) or [tier.get("id") for tier in tiers] != list(VALID_TIER_IDS):
        fail(f"tiers must appear exactly in this order: {', '.join(VALID_TIER_IDS)}")

    dependency_policy = catalog.get("dependencyPolicy")
    if dependency_policy != EXPECTED_DEPENDENCY_POLICY:
        fail("dependencyPolicy must preserve Stable -> Stable, Integrations -> Stable/Integrations, and Labs -> any tier")

    package_to_tier: dict[str, str] = {}
    for tier in tiers:
        packages = tier.get("packages")
        if not isinstance(packages, list) or not all(isinstance(package, str) for package in packages):
            fail(f"tier {tier['id']} must contain a string package list")
        if packages != sorted(packages):
            fail(f"packages in tier {tier['id']} must be alphabetically sorted")
        for package in packages:
            previous_tier = package_to_tier.setdefault(package, tier["id"])
            if previous_tier != tier["id"]:
                fail(f"{package} appears in both {previous_tier} and {tier['id']}")

    unpublished = catalog.get("unpublishedProjects")
    if not isinstance(unpublished, list):
        fail("unpublishedProjects must be a list")
    unpublished_to_tier = {entry["project"]: entry["tier"] for entry in unpublished}
    if len(unpublished_to_tier) != len(unpublished):
        fail("unpublishedProjects contains duplicate project names")
    if any(tier not in VALID_TIER_IDS for tier in unpublished_to_tier.values()):
        fail("every unpublished project must use a known tier")

    project_paths = sorted(ROOT.glob("Monica.*/*.csproj"))
    project_packages: dict[str, Path] = {}
    non_packable_projects: set[str] = set()
    for project_path in project_paths:
        package_id, is_packable = read_project(project_path)
        if is_packable:
            project_packages[package_id] = project_path
        else:
            non_packable_projects.add(project_path.stem)

    missing = sorted(set(project_packages) - set(package_to_tier))
    extra = sorted(set(package_to_tier) - set(project_packages))
    if missing:
        fail(f"packable projects missing from the catalog: {', '.join(missing)}")
    if extra:
        fail(f"catalog packages without a packable project: {', '.join(extra)}")

    missing_unpublished = sorted(non_packable_projects - set(unpublished_to_tier))
    extra_unpublished = sorted(set(unpublished_to_tier) - non_packable_projects)
    if missing_unpublished:
        fail(f"non-packable projects missing from unpublishedProjects: {', '.join(missing_unpublished)}")
    if extra_unpublished:
        fail(f"unpublishedProjects entries that are packable or absent: {', '.join(extra_unpublished)}")

    project_tiers: dict[Path, str] = {
        path.resolve(): package_to_tier[package_id]
        for package_id, path in project_packages.items()
    }
    project_tiers.update(
        {
            path.resolve(): unpublished_to_tier[path.stem]
            for path in project_paths
            if path.stem in non_packable_projects
        }
    )

    invalid_edges: list[str] = []
    for project_path in project_paths:
        source_tier = project_tiers[project_path.resolve()]
        for dependency_path in read_project_references(project_path):
            dependency_tier = project_tiers.get(dependency_path)
            if dependency_tier is None:
                continue
            if dependency_tier not in dependency_policy[source_tier]:
                invalid_edges.append(
                    f"{project_path.stem} ({source_tier}) -> "
                    f"{dependency_path.stem} ({dependency_tier})"
                )

    if invalid_edges:
        fail(
            "package dependency maturity flows in the wrong direction: "
            + "; ".join(sorted(invalid_edges))
        )

    if args.list_public_projects:
        for _, project_path in sorted(project_packages.items()):
            print(project_path.relative_to(ROOT).as_posix())
        return

    print(
        "package tier validation passed: "
        f"{len(project_packages)} public packages and {len(non_packable_projects)} unpublished projects"
    )


if __name__ == "__main__":
    main()
