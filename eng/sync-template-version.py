#!/usr/bin/env python3
"""Synchronize Monica package versions embedded in the dotnet new templates."""

from __future__ import annotations

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Any, NoReturn


ROOT = Path(__file__).resolve().parents[1]
DIRECTORY_BUILD_PROPS = ROOT / "Directory.Build.props"
TEMPLATE_PROJECT = ROOT / "Monica.Templates" / "templates" / "monica-api" / "MonicaStarter.csproj"
TEMPLATE_README = ROOT / "Monica.Templates" / "README.md"
TEMPLATE_CONFIGURATION = (
    ROOT
    / "Monica.Templates"
    / "templates"
    / "monica-api"
    / ".template.config"
    / "template.json"
)
VERSION_PATTERN = re.compile(r"^[0-9A-Za-z][0-9A-Za-z.+-]*$")
PROJECT_VERSION_PATTERN = re.compile(r"(<MonicaVersion>)([^<]+)(</MonicaVersion>)")
TEMPLATE_INSTALL_VERSION_PATTERN = re.compile(
    r"(dotnet new install Monica\.Templates@)([^\s`]+)"
)


def fail(message: str) -> NoReturn:
    print(f"template version synchronization failed: {message}", file=sys.stderr)
    raise SystemExit(1)


def validate_version(version: str, source: str) -> str:
    normalized = version.strip()
    if normalized != version or not VERSION_PATTERN.fullmatch(normalized):
        fail(f"{source} contains invalid version '{version}'")
    return normalized


def read_directory_version() -> str:
    root = ET.parse(DIRECTORY_BUILD_PROPS).getroot()
    versions = [
        (element.text or "").strip()
        for element in root.iter()
        if element.tag.rsplit("}", 1)[-1] == "Version"
    ]
    if len(versions) != 1:
        fail(f"{DIRECTORY_BUILD_PROPS.relative_to(ROOT)} must contain exactly one Version element")
    return validate_version(versions[0], str(DIRECTORY_BUILD_PROPS.relative_to(ROOT)))


def read_project_version(contents: str) -> str:
    matches = list(PROJECT_VERSION_PATTERN.finditer(contents))
    if len(matches) != 1:
        fail(f"{TEMPLATE_PROJECT.relative_to(ROOT)} must contain exactly one MonicaVersion element")
    return validate_version(matches[0].group(2), str(TEMPLATE_PROJECT.relative_to(ROOT)))


def read_template_readme_version(contents: str) -> str:
    matches = list(TEMPLATE_INSTALL_VERSION_PATTERN.finditer(contents))
    if len(matches) != 1:
        fail(
            f"{TEMPLATE_README.relative_to(ROOT)} must contain exactly one "
            "versioned dotnet new install command"
        )
    return validate_version(matches[0].group(2), str(TEMPLATE_README.relative_to(ROOT)))


def read_template_configuration() -> tuple[dict[str, Any], str, str]:
    configuration: dict[str, Any] = json.loads(
        TEMPLATE_CONFIGURATION.read_text(encoding="utf-8")
    )
    try:
        framework_version = configuration["symbols"]["frameworkVersion"]
        default_value = framework_version["defaultValue"]
        replaces = framework_version["replaces"]
    except (KeyError, TypeError):
        fail(
            f"{TEMPLATE_CONFIGURATION.relative_to(ROOT)} must define "
            "symbols.frameworkVersion.defaultValue and replaces"
        )

    if not isinstance(default_value, str) or not isinstance(replaces, str):
        fail("frameworkVersion defaultValue and replaces must both be strings")

    return (
        configuration,
        validate_version(default_value, "frameworkVersion.defaultValue"),
        validate_version(replaces, "frameworkVersion.replaces"),
    )


def check_versions(expected_version: str, expected_source: str) -> None:
    project_contents = TEMPLATE_PROJECT.read_text(encoding="utf-8")
    project_version = read_project_version(project_contents)
    readme_version = read_template_readme_version(
        TEMPLATE_README.read_text(encoding="utf-8")
    )
    _, default_value, replaces = read_template_configuration()

    actual_versions = {
        "Directory.Build.props Version": read_directory_version(),
        "MonicaStarter.csproj MonicaVersion": project_version,
        "Monica.Templates README install version": readme_version,
        "template.json frameworkVersion.defaultValue": default_value,
        "template.json frameworkVersion.replaces": replaces,
    }
    mismatches = [
        f"{location} is {actual}, expected {expected_version}"
        for location, actual in actual_versions.items()
        if actual != expected_version
    ]
    if mismatches:
        fail("; ".join(mismatches))

    print(f"template version check passed against {expected_source}: {expected_version}")


def synchronize_version(version: str) -> None:
    project_contents = TEMPLATE_PROJECT.read_text(encoding="utf-8")
    read_project_version(project_contents)
    configuration, _, _ = read_template_configuration()
    readme_contents = TEMPLATE_README.read_text(encoding="utf-8")
    read_template_readme_version(readme_contents)

    updated_project = PROJECT_VERSION_PATTERN.sub(
        lambda match: f"{match.group(1)}{version}{match.group(3)}",
        project_contents,
        count=1,
    )
    framework_version = configuration["symbols"]["frameworkVersion"]
    framework_version["defaultValue"] = version
    framework_version["replaces"] = version
    updated_configuration = json.dumps(configuration, ensure_ascii=False, indent=2) + "\n"
    updated_readme = TEMPLATE_INSTALL_VERSION_PATTERN.sub(
        lambda match: f"{match.group(1)}{version}",
        readme_contents,
        count=1,
    )

    changed_files: list[str] = []
    if updated_project != project_contents:
        TEMPLATE_PROJECT.write_text(updated_project, encoding="utf-8")
        changed_files.append(str(TEMPLATE_PROJECT.relative_to(ROOT)))

    current_configuration = TEMPLATE_CONFIGURATION.read_text(encoding="utf-8")
    if updated_configuration != current_configuration:
        TEMPLATE_CONFIGURATION.write_text(updated_configuration, encoding="utf-8")
        changed_files.append(str(TEMPLATE_CONFIGURATION.relative_to(ROOT)))

    if updated_readme != readme_contents:
        TEMPLATE_README.write_text(updated_readme, encoding="utf-8")
        changed_files.append(str(TEMPLATE_README.relative_to(ROOT)))

    changed_summary = ", ".join(changed_files) if changed_files else "no file changes required"
    print(f"synchronized Monica template version to {version}: {changed_summary}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Synchronize or validate Monica package versions embedded in dotnet new templates."
        )
    )
    parser.add_argument("--version", help="Expected Monica package version.")
    parser.add_argument(
        "--check",
        action="store_true",
        help="Validate without writing. Defaults to the version in Directory.Build.props.",
    )
    args = parser.parse_args()
    if not args.check and args.version is None:
        parser.error("--version is required unless --check is used")
    return args


def main() -> None:
    args = parse_args()
    if args.check:
        if args.version is None:
            check_versions(read_directory_version(), "Directory.Build.props")
        else:
            check_versions(validate_version(args.version, "--version"), "--version")
        return

    synchronize_version(validate_version(args.version, "--version"))


if __name__ == "__main__":
    main()
