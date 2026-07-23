#!/usr/bin/env python3
"""Scaffold a multi-module Monica ecosystem package from a validated manifest."""

from __future__ import annotations

import argparse
import json
import re
import shutil
import struct
import sys
import textwrap
from dataclasses import dataclass
from datetime import UTC, datetime
from pathlib import Path
from typing import Any
from urllib.parse import urlsplit


SEGMENT_PATTERN = re.compile(r"^[A-Za-z][A-Za-z0-9]*$")
PACKAGE_PATTERN = re.compile(
    r"^(?P<publisher>[A-Za-z][A-Za-z0-9]*)\.Monica\."
    r"[A-Za-z][A-Za-z0-9]*(?:\.[A-Za-z][A-Za-z0-9]*)*$"
)
SEMVER_PATTERN = re.compile(
    r"^(?:0|[1-9][0-9]*)\."
    r"(?:0|[1-9][0-9]*)\."
    r"(?:0|[1-9][0-9]*)"
    r"(?:-(?:[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?"
    r"(?:\+(?:[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$"
)
TAG_PATTERN = re.compile(r"^[a-z0-9][a-z0-9-]*$")
SPDX_EXPRESSION_PATTERN = re.compile(r"^[A-Za-z0-9().+\-\s]+$")
SOURCE_LINK_PACKAGES = {
    "github": "Microsoft.SourceLink.GitHub",
    "gitlab": "Microsoft.SourceLink.GitLab",
    "azure-repos": "Microsoft.SourceLink.AzureRepos.Git",
}
SOURCE_PROVIDERS = {*SOURCE_LINK_PACKAGES, "none"}
PUBLISH_TARGETS = {"nuget.org", "private-feed", "none"}
ICON_KINDS = {"compatibility-mark", "publisher"}
UI_CATEGORY_ORDER_BASE = 450
UI_NAV_ORDER_BASE = 80
COMPATIBILITY_NOTICE = (
    "Monica compatibility is self-attested by the publisher. This community package is "
    "independently maintained and is not affiliated with, endorsed by, or supported by "
    "the Monica project."
)


@dataclass(frozen=True)
class ModuleSpec:
    name: str
    kind: str
    key: str
    depends_on: tuple[str, ...]

    @property
    def is_ui(self) -> bool:
        return self.kind == "ui"

    @property
    def base_name(self) -> str:
        return self.name[:-2] if self.is_ui and self.name.endswith("UI") else self.name

    @property
    def navigation_category_id(self) -> str:
        if not self.is_ui or not self.key.casefold().endswith(".ui"):
            raise ValueError(f"Module '{self.name}' is not a UI module with a final .UI key segment.")
        return self.key[:-3]


@dataclass(frozen=True)
class Manifest:
    source_path: Path
    package_id: str
    version: str
    description: str
    authors: str
    nuget_owner: str
    repository_url: str | None
    project_url: str
    support_url: str
    security_url: str
    target_framework: str
    monica_version: str
    source_available: bool
    source_provider: str
    source_link_version: str | None
    distribution: str
    publish_target: str
    publish_feed_url: str | None
    capability_tags: tuple[str, ...]
    open_source: bool
    license_expression: str | None
    license_file: Path | None
    icon_kind: str
    icon_file: Path | None
    show_open_source_badge: bool
    modules: tuple[ModuleSpec, ...]

    @property
    def has_ui(self) -> bool:
        return any(module.is_ui for module in self.modules)

    @property
    def has_web(self) -> bool:
        return any(module.kind == "web" for module in self.modules)

    @property
    def source_link_package(self) -> str | None:
        if not self.source_available:
            return None
        return SOURCE_LINK_PACKAGES[self.source_provider]

    @property
    def package_tags(self) -> tuple[str, ...]:
        tags = ["monica", "monica-module", "monica-ecosystem-v1", "extension"]
        if self.has_ui:
            tags.append("monica-ui")
        tags.extend(self.capability_tags)
        return tuple(dict.fromkeys(tags))

    @property
    def icon_name(self) -> str:
        if self.icon_kind == "compatibility-mark":
            return "monica-compatibility-mark.png"
        assert self.icon_file is not None
        return self.icon_file.name


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Create a Monica ecosystem package repository.")
    parser.add_argument("--manifest", required=True, type=Path, help="JSON manifest path.")
    parser.add_argument("--output", required=True, type=Path, help="New or empty output directory.")
    return parser.parse_args()


def require_text(data: dict[str, Any], name: str) -> str:
    value = data.get(name)
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"Manifest field '{name}' must be a non-empty string.")
    return value.strip()


def require_boolean(data: dict[str, Any], name: str) -> bool:
    value = data.get(name)
    if not isinstance(value, bool):
        raise ValueError(f"Manifest field '{name}' must be a boolean.")
    return value


def require_https_url(data: dict[str, Any], name: str) -> str:
    value = require_text(data, name)
    parsed = urlsplit(value)
    if parsed.scheme != "https" or not parsed.netloc or parsed.username or parsed.password:
        raise ValueError(
            f"Manifest field '{name}' must be an absolute HTTPS URL without embedded credentials."
        )
    return value


def require_object(data: dict[str, Any], name: str) -> dict[str, Any]:
    value = data.get(name)
    if not isinstance(value, dict):
        raise ValueError(f"Manifest field '{name}' must be an object.")
    return value


def validate_semver(value: str, name: str) -> str:
    if SEMVER_PATTERN.fullmatch(value) is None:
        raise ValueError(f"{name} must be a valid three-part SemVer value.")
    prerelease = value.partition("-")[2].partition("+")[0]
    if prerelease:
        for identifier in prerelease.split("."):
            if identifier.isdigit() and len(identifier) > 1 and identifier.startswith("0"):
                raise ValueError(f"{name} has a numeric prerelease identifier with a leading zero.")
    return value


def reject_unknown_fields(data: dict[str, Any], allowed: set[str], context: str) -> None:
    unknown = sorted(set(data) - allowed)
    if unknown:
        raise ValueError(f"{context} contains unsupported fields: {', '.join(unknown)}")


def resolve_manifest_file(manifest_path: Path, value: Any, field_name: str) -> Path:
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{field_name} must be a non-empty relative file path.")
    relative = Path(value)
    if relative.is_absolute() or ".." in relative.parts:
        raise ValueError(f"{field_name} must stay within the manifest directory.")
    resolved = (manifest_path.parent / relative).resolve()
    if not resolved.is_file():
        raise ValueError(f"{field_name} does not exist: {resolved}")
    return resolved


def validate_png_icon(path: Path) -> None:
    payload = path.read_bytes()
    if len(payload) < 33 or payload[:8] != b"\x89PNG\r\n\x1a\n" or payload[12:16] != b"IHDR":
        raise ValueError(f"Package icon is not a valid PNG file: {path}")
    width, height = struct.unpack(">II", payload[16:24])
    color_type = payload[25]
    has_transparency = color_type in {4, 6}
    offset = 8
    while offset + 12 <= len(payload):
        chunk_length = struct.unpack(">I", payload[offset:offset + 4])[0]
        chunk_type = payload[offset + 4:offset + 8]
        if chunk_type == b"tRNS":
            has_transparency = True
        offset += 12 + chunk_length
        if chunk_type == b"IEND":
            break
    if width < 64 or height < 64:
        raise ValueError("Package icon must be at least 64 by 64 pixels.")
    if not has_transparency:
        raise ValueError("Package icon must support transparency.")


def validate_dependency_graph(modules: list[ModuleSpec]) -> None:
    by_name = {module.name: module for module in modules}
    visiting: list[str] = []
    visited: set[str] = set()

    def visit(name: str) -> None:
        if name in visited:
            return
        if name in visiting:
            cycle = visiting[visiting.index(name):] + [name]
            raise ValueError(f"Module dependency cycle detected: {' -> '.join(cycle)}")
        visiting.append(name)
        for dependency in by_name[name].depends_on:
            visit(dependency)
        visiting.pop()
        visited.add(name)

    for module_name in by_name:
        visit(module_name)


def load_manifest(path: Path) -> Manifest:
    path = path.resolve()
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(data, dict):
        raise ValueError("Manifest root must be a JSON object.")
    if data.get("schemaVersion") != 1:
        raise ValueError("Only schemaVersion 1 is supported.")
    reject_unknown_fields(
        data,
        {
            "schemaVersion", "packageId", "version", "description", "authors", "nugetOwner",
            "repositoryUrl", "projectUrl", "supportUrl", "securityUrl", "targetFramework",
            "monicaVersion", "source", "distribution", "publishing", "capabilityTags",
            "license", "branding", "modules",
        },
        "Manifest",
    )

    package_id = require_text(data, "packageId")
    package_match = PACKAGE_PATTERN.fullmatch(package_id)
    if package_match is None or len(package_id) > 100:
        raise ValueError("packageId must match <Publisher>.Monica.<Package>[.<Variant>] and be at most 100 characters.")
    if package_match.group("publisher").casefold() == "monica":
        raise ValueError("The Monica publisher segment is reserved for first-party packages.")

    version = validate_semver(require_text(data, "version"), "version")
    monica_version = validate_semver(require_text(data, "monicaVersion"), "monicaVersion")
    if "-" in monica_version and "-" not in version:
        raise ValueError("A package depending on prerelease Monica must itself use a prerelease version.")

    project_url = require_https_url(data, "projectUrl")
    support_url = require_https_url(data, "supportUrl")
    security_url = require_https_url(data, "securityUrl")

    source_data = require_object(data, "source")
    reject_unknown_fields(source_data, {"available", "provider", "sourceLinkVersion"}, "source")
    source_available = require_boolean(source_data, "available")
    source_provider = require_text(source_data, "provider").casefold()
    if source_provider not in SOURCE_PROVIDERS:
        raise ValueError(f"source.provider must be one of: {', '.join(sorted(SOURCE_PROVIDERS))}.")
    if source_available and source_provider == "none":
        raise ValueError("source.provider cannot be 'none' when source.available is true.")
    repository_url = None
    if source_provider != "none":
        repository_url = require_https_url(data, "repositoryUrl")
    elif "repositoryUrl" in data:
        raise ValueError("repositoryUrl must be omitted when source.provider is 'none'.")
    source_link_version_value = source_data.get("sourceLinkVersion")
    source_link_version = None
    if source_available:
        if not isinstance(source_link_version_value, str):
            raise ValueError("source.sourceLinkVersion is required when source.available is true.")
        source_link_version = validate_semver(source_link_version_value.strip(), "source.sourceLinkVersion")
    elif source_link_version_value is not None:
        raise ValueError("source.sourceLinkVersion is only valid when source.available is true.")

    distribution = require_text(data, "distribution").casefold()
    if distribution not in {"public", "private"}:
        raise ValueError("distribution must be 'public' or 'private'.")

    publishing_data = require_object(data, "publishing")
    reject_unknown_fields(publishing_data, {"target", "feedUrl"}, "publishing")
    publish_target = require_text(publishing_data, "target").casefold()
    if publish_target not in PUBLISH_TARGETS:
        raise ValueError(f"publishing.target must be one of: {', '.join(sorted(PUBLISH_TARGETS))}.")
    feed_value = publishing_data.get("feedUrl")
    publish_feed_url = None
    if publish_target == "private-feed":
        if not isinstance(feed_value, str) or not feed_value.startswith("https://"):
            raise ValueError("publishing.feedUrl must be an HTTPS URL for a private-feed target.")
        publish_feed_url = feed_value.strip()
    elif feed_value is not None:
        raise ValueError("publishing.feedUrl is only valid when publishing.target is 'private-feed'.")
    if publish_target == "nuget.org" and distribution != "public":
        raise ValueError("publishing.target 'nuget.org' requires distribution 'public'.")
    if publish_target == "private-feed" and distribution != "private":
        raise ValueError("publishing.target 'private-feed' requires distribution 'private'.")

    raw_capability_tags = data.get("capabilityTags")
    if not isinstance(raw_capability_tags, list) or not raw_capability_tags:
        raise ValueError("capabilityTags must contain at least one package-specific tag.")
    if not all(isinstance(tag, str) and TAG_PATTERN.fullmatch(tag) for tag in raw_capability_tags):
        raise ValueError("Every capability tag must use lowercase ASCII letters, digits, and hyphens.")
    folded_capability_tags = [tag.casefold() for tag in raw_capability_tags]
    if len(folded_capability_tags) != len(set(folded_capability_tags)):
        raise ValueError("capabilityTags must not contain duplicates.")
    reserved_tags = {"monica", "monica-module", "monica-ecosystem-v1", "monica-ui", "extension"}
    if reserved_tags.intersection(folded_capability_tags):
        raise ValueError("capabilityTags must contain package-specific tags, not scaffold-managed ecosystem tags.")

    license_data = require_object(data, "license")
    reject_unknown_fields(license_data, {"openSource", "expression", "file"}, "license")
    open_source = require_boolean(license_data, "openSource")
    expression = license_data.get("expression")
    file_value = license_data.get("file")
    if (expression is None) == (file_value is None):
        raise ValueError("license must contain exactly one of expression or file.")
    license_expression = None
    license_file = None
    if expression is not None:
        if not isinstance(expression, str) or not expression.strip():
            raise ValueError("license.expression must be a non-empty string.")
        license_expression = expression.strip()
        if SPDX_EXPRESSION_PATTERN.fullmatch(license_expression) is None:
            raise ValueError("license.expression contains characters that are not valid in an SPDX expression.")
    else:
        license_file = resolve_manifest_file(path, file_value, "license.file")
    if open_source and license_expression is None:
        raise ValueError("license.openSource requires a NuGet SPDX license expression.")

    branding_data = require_object(data, "branding")
    reject_unknown_fields(branding_data, {"icon", "showOpenSourceBadge"}, "branding")
    show_open_source_badge = require_boolean(branding_data, "showOpenSourceBadge")
    if show_open_source_badge and not (open_source and license_expression):
        raise ValueError("branding.showOpenSourceBadge requires license.openSource=true and a license expression.")
    icon_data = require_object(branding_data, "icon")
    reject_unknown_fields(icon_data, {"kind", "file"}, "branding.icon")
    icon_kind = require_text(icon_data, "kind").casefold()
    if icon_kind not in ICON_KINDS:
        raise ValueError(f"branding.icon.kind must be one of: {', '.join(sorted(ICON_KINDS))}.")
    icon_value = icon_data.get("file")
    icon_file = None
    if icon_kind == "publisher":
        icon_file = resolve_manifest_file(path, icon_value, "branding.icon.file")
        if icon_file.suffix.casefold() != ".png":
            raise ValueError("branding.icon.file must be a PNG file.")
        if icon_file.name.casefold() in {
            "readme.md", "license", "package.manifest.json", "monica-open-source-badge.svg"
        }:
            raise ValueError("branding.icon.file uses a reserved scaffold file name.")
        if license_file and icon_file.name.casefold() == license_file.name.casefold():
            raise ValueError("branding.icon.file and license.file must use different file names.")
        validate_png_icon(icon_file)
    elif icon_value is not None:
        raise ValueError("branding.icon.file is only valid for a publisher icon.")

    raw_modules = data.get("modules")
    if not isinstance(raw_modules, list) or not raw_modules:
        raise ValueError("modules must contain at least one module definition.")

    modules: list[ModuleSpec] = []
    names: set[str] = set()
    keys: set[str] = set()
    package_prefix = package_id.casefold() + "."
    for raw_module in raw_modules:
        if not isinstance(raw_module, dict):
            raise ValueError("Every module definition must be an object.")
        reject_unknown_fields(raw_module, {"name", "kind", "key", "dependsOn"}, "Module definition")
        name = require_text(raw_module, "name")
        if SEGMENT_PATTERN.fullmatch(name) is None:
            raise ValueError(f"Module name '{name}' must begin with an ASCII letter and contain only letters or digits.")
        kind = require_text(raw_module, "kind").casefold()
        if kind not in {"infrastructure", "web", "ui"}:
            raise ValueError(f"Module '{name}' kind must be infrastructure, web, or ui.")
        if kind == "ui":
            if not name.endswith("UI"):
                raise ValueError(f"UI module '{name}' must end with the exact suffix 'UI'.")
            base_name = name[:-2]
            if not base_name or SEGMENT_PATTERN.fullmatch(base_name) is None or base_name.casefold().endswith("ui"):
                raise ValueError(f"UI module '{name}' must have a non-empty base name that does not itself end in UI.")
        elif name.casefold().endswith("ui"):
            raise ValueError(f"Non-UI module '{name}' must not end in UI.")
        key = require_text(raw_module, "key")
        if PACKAGE_PATTERN.fullmatch(key) is None or len(key) > 100:
            raise ValueError(f"Module key '{key}' does not follow the Monica ecosystem grammar.")
        folded_key = key.casefold()
        if folded_key != package_id.casefold() and not folded_key.startswith(package_prefix):
            raise ValueError(f"Module key '{key}' must equal packageId or begin with '{package_id}.'.")
        if (kind == "ui") != folded_key.endswith(".ui"):
            raise ValueError(f"Module '{name}' and key '{key}' disagree about the final .UI identity segment.")
        if name.casefold() in names:
            raise ValueError(f"Duplicate module name: {name}")
        if folded_key in keys:
            raise ValueError(f"Duplicate module key: {key}")

        depends_on_value = raw_module.get("dependsOn", [])
        if not isinstance(depends_on_value, list) or not all(isinstance(item, str) for item in depends_on_value):
            raise ValueError(f"Module '{name}' dependsOn must be a string array.")
        folded_dependencies = [item.casefold() for item in depends_on_value]
        if len(folded_dependencies) != len(set(folded_dependencies)):
            raise ValueError(f"Module '{name}' contains duplicate dependsOn entries.")
        names.add(name.casefold())
        keys.add(folded_key)
        modules.append(ModuleSpec(name, kind, key, tuple(depends_on_value)))

    declared_names = {module.name for module in modules}
    for module in modules:
        unknown = sorted(set(module.depends_on) - declared_names)
        if unknown:
            raise ValueError(f"Module '{module.name}' depends on undeclared modules: {', '.join(unknown)}")
        if module.name in module.depends_on:
            raise ValueError(f"Module '{module.name}' cannot depend on itself.")
    validate_dependency_graph(modules)

    return Manifest(
        source_path=path,
        package_id=package_id,
        version=version,
        description=require_text(data, "description"),
        authors=require_text(data, "authors"),
        nuget_owner=require_text(data, "nugetOwner"),
        repository_url=repository_url,
        project_url=project_url,
        support_url=support_url,
        security_url=security_url,
        target_framework=require_text(data, "targetFramework"),
        monica_version=monica_version,
        source_available=source_available,
        source_provider=source_provider,
        source_link_version=source_link_version,
        distribution=distribution,
        publish_target=publish_target,
        publish_feed_url=publish_feed_url,
        capability_tags=tuple(folded_capability_tags),
        open_source=open_source,
        license_expression=license_expression,
        license_file=license_file,
        icon_kind=icon_kind,
        icon_file=icon_file,
        show_open_source_badge=show_open_source_badge,
        modules=tuple(modules),
    )


def write(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    normalized = textwrap.dedent(content).strip() + "\n"
    path.write_text(normalized, encoding="utf-8", newline="\n")


def xml_escape(value: str) -> str:
    return value.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;")


def csharp_escape(value: str) -> str:
    return value.replace("\\", "\\\\").replace('"', '\\"')


def kebab_case(value: str) -> str:
    tokens = re.findall(r"[A-Z]+(?=[A-Z][a-z]|[0-9]|$)|[A-Z]?[a-z]+|[0-9]+", value)
    return "-".join(token.casefold() for token in tokens)


def package_route_segments(package_id: str) -> list[str]:
    segments = package_id.split(".")[2:]
    if len(segments) > 1 and segments[-1].casefold() == "ui":
        segments = segments[:-1]
    return segments


def ui_route(manifest: Manifest, module: ModuleSpec) -> str:
    package_segments = package_route_segments(manifest.package_id)
    if not any(segment.casefold() == module.base_name.casefold() for segment in package_segments):
        package_segments.append(module.base_name)
    return "/" + "-".join(kebab_case(segment) for segment in package_segments)


def create_directory_build_props(root: Path, manifest: Manifest) -> None:
    write(
        root / "Directory.Build.props",
        f"""
        <Project>
          <PropertyGroup>
            <TargetFramework>{xml_escape(manifest.target_framework)}</TargetFramework>
            <LangVersion>preview</LangVersion>
            <Nullable>enable</Nullable>
            <ImplicitUsings>enable</ImplicitUsings>
            <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
            <GenerateDocumentationFile>true</GenerateDocumentationFile>
            <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
          </PropertyGroup>
        </Project>
        """,
    )


def create_directory_packages_props(root: Path, manifest: Manifest) -> None:
    versions = [
        ("Monica.Core", manifest.monica_version),
        *(([("Monica.UI", manifest.monica_version)]) if manifest.has_ui else []),
        ("Monica.Testing", manifest.monica_version),
        ("AwesomeAssertions", "9.4.0"),
        ("coverlet.collector", "8.0.1"),
        ("Microsoft.NET.Test.Sdk", "18.4.0"),
        *(([(manifest.source_link_package, manifest.source_link_version)]) if manifest.source_link_package else []),
        ("xunit.runner.visualstudio", "3.1.5"),
        ("xunit.v3", "3.2.2"),
    ]
    lines = [
        "<Project>",
        "  <PropertyGroup>",
        "    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>",
        "  </PropertyGroup>",
        "  <ItemGroup>",
        *[
            f'    <PackageVersion Include="{package}" Version="{xml_escape(version)}" />'
            for package, version in versions
        ],
        "  </ItemGroup>",
        "</Project>",
    ]
    write(root / "Directory.Packages.props", "\n".join(lines))


def create_project(root: Path, manifest: Manifest) -> Path:
    project_dir = root / "src" / manifest.package_id
    sdk = "Microsoft.NET.Sdk.Razor" if manifest.has_ui else "Microsoft.NET.Sdk"
    monica_reference = "Monica.UI" if manifest.has_ui else "Monica.Core"
    monica_project = "Monica.UI\\Monica.UI.csproj" if manifest.has_ui else "Monica.Core\\Monica.Core.csproj"
    license_property = (
        f"<PackageLicenseExpression>{xml_escape(manifest.license_expression or '')}</PackageLicenseExpression>"
        if manifest.license_expression
        else f"<PackageLicenseFile>{xml_escape(manifest.license_file.name)}</PackageLicenseFile>"
    )
    package_items = [
        '..\\..\\README.md',
        f'..\\..\\{xml_escape(manifest.icon_name)}',
    ]
    if manifest.show_open_source_badge:
        package_items.append('..\\..\\monica-open-source-badge.svg')
    if manifest.icon_kind == "compatibility-mark":
        package_items.append('..\\..\\monica-compatibility-mark.svg')
    if manifest.license_file:
        package_items.append(f'..\\..\\{xml_escape(manifest.license_file.name)}')

    repository_properties: list[str] = []
    if manifest.source_available:
        assert manifest.repository_url is not None
        repository_properties = [
            f"    <RepositoryUrl>{xml_escape(manifest.repository_url)}</RepositoryUrl>",
            "    <RepositoryType>git</RepositoryType>",
        ]

    lines = [
        f'<Project Sdk="{sdk}">',
        "  <PropertyGroup>",
        f"    <PackageId>{manifest.package_id}</PackageId>",
        f"    <AssemblyName>{manifest.package_id}</AssemblyName>",
        f"    <RootNamespace>{manifest.package_id}</RootNamespace>",
        f"    <Version>{manifest.version}</Version>",
        f"    <Authors>{xml_escape(manifest.authors)}</Authors>",
        f"    <PackageDescription>{xml_escape(manifest.description)}</PackageDescription>",
        f"    <PackageProjectUrl>{xml_escape(manifest.project_url)}</PackageProjectUrl>",
        *repository_properties,
        f"    {license_property}",
        f"    <PackageIcon>{xml_escape(manifest.icon_name)}</PackageIcon>",
        "    <PackageReadmeFile>README.md</PackageReadmeFile>",
        f"    <PackageTags>{';'.join(manifest.package_tags)}</PackageTags>",
        "    <IncludeSymbols>true</IncludeSymbols>",
        "    <SymbolPackageFormat>snupkg</SymbolPackageFormat>",
        f"    <NuGetOwner>{xml_escape(manifest.nuget_owner)}</NuGetOwner>",
    ]
    if manifest.source_link_package:
        lines.extend(
            [
                "    <PublishRepositoryUrl>true</PublishRepositoryUrl>",
                "    <EmbedUntrackedSources>true</EmbedUntrackedSources>",
            ]
        )
    lines.append("  </PropertyGroup>")
    if manifest.source_link_package or manifest.has_web:
        lines.append("  <ItemGroup>")
        if manifest.source_link_package:
            lines.append(
                f'    <PackageReference Include="{manifest.source_link_package}" PrivateAssets="All" />'
            )
        if manifest.has_web:
            lines.append('    <FrameworkReference Include="Microsoft.AspNetCore.App" />')
        lines.append("  </ItemGroup>")
    lines.extend(
        [
            "  <ItemGroup Condition=\"'$(MonicaSourceRoot)' == ''\">",
            f'    <PackageReference Include="{monica_reference}" />',
            "  </ItemGroup>",
            "  <ItemGroup Condition=\"'$(MonicaSourceRoot)' != ''\">",
            f'    <ProjectReference Include="$(MonicaSourceRoot)\\{monica_project}" />',
            "  </ItemGroup>",
            "  <ItemGroup>",
            *[
                f'    <None Include="{item}" Pack="true" PackagePath="" />'
                for item in package_items
            ],
            "  </ItemGroup>",
        ]
    )
    if manifest.has_ui:
        lines.extend(
            [
                "  <ItemGroup>",
                '    <EmbeddedResource Include="Localization\\**\\*.json" />',
                "  </ItemGroup>",
            ]
        )
    lines.append("</Project>")
    write(project_dir / f"{manifest.package_id}.csproj", "\n".join(lines))
    return project_dir


def module_dependencies(module: ModuleSpec) -> list[str]:
    return [f"DependsOnModule<Module{name}Guide>().Register();" for name in module.depends_on]


def create_module(project_dir: Path, manifest: Manifest, module: ModuleSpec, ui_index: int) -> None:
    dependencies = module_dependencies(module)
    base_type = "ModuleBase"
    guide_type = "ModuleGuide"
    option_type = "ModuleOptions"
    additional_usings: list[str] = []
    registration = ""

    if module.kind == "web":
        base_type = "WebModuleBase"
        guide_type = "WebModuleGuide"
        option_type = "MinimalApiModuleOptions"
    elif module.is_ui:
        resource_name = f"{module.base_name}Resource"
        page_name = f"UI{module.base_name}Page"
        route = ui_route(manifest, module)
        dependencies.extend(
            [
                f"DependsOnModule<ModuleLocalizationGuide>().Register().AddResource<{resource_name}>();",
                "var shellGuide = DependsOnModule<ModuleShellUIGuide>().Register();",
            ]
        )
        registration = textwrap.dedent(
            f"""
            shellGuide.RegisterUIComponents(registry =>
            {{
                var category = registry.RegisterLocalizedCategory<{resource_name}>(
                    "{csharp_escape(module.navigation_category_id)}",
                    "Navigation:Category",
                    order: {UI_CATEGORY_ORDER_BASE + ui_index});
                registry.RegisterLocalizedPage<{page_name}, {resource_name}>(
                    "{route}",
                    "Navigation:Title",
                    Icons.Material.Filled.Extension,
                    categoryId: category,
                    addToNav: true,
                    navOrder: {UI_NAV_ORDER_BASE + ui_index});
            }});
            """
        ).strip()
        dependencies.append(registration)
        additional_usings.extend(
            [
                f"using {manifest.package_id}.Localization;",
                f"using {manifest.package_id}.Pages;",
                "using Monica.Modules;",
                "using MudBlazor;",
            ]
        )
        create_ui_files(project_dir, manifest, module, route)

    dependency_method = ""
    if dependencies:
        statements = "\n\n".join(textwrap.indent(statement, "        ") for statement in dependencies)
        dependency_method = f"""
    /// <inheritdoc />
    public override void ClaimDependencies()
    {{
{statements}
    }}
""".rstrip()

    usings = [
        "using Monica.Core;",
        "using Monica.Core.Modularity;",
        "using Monica.Core.Modularity.Abstractions;",
        "using Monica.Core.Modularity.Annotations;",
        "using Monica.Core.Modularity.Models;",
        *additional_usings,
    ]
    code = f"""{chr(10).join(usings)}

namespace {manifest.package_id}.Modules;

/// <summary>
/// Registers the independently maintained {module.base_name}{' UI' if module.is_ui else ''} capability.
/// </summary>
[ModuleKey("{csharp_escape(module.key)}")]
public sealed class Module{module.name}(Module{module.name}Option option)
    : {base_type}<Module{module.name}, Module{module.name}Option, Module{module.name}Guide>(option)
{{{dependency_method}
}}

/// <summary>
/// Provides fluent configuration for <see cref="Module{module.name}"/>.
/// </summary>
public sealed class Module{module.name}Guide
    : {guide_type}<Module{module.name}, Module{module.name}Option, Module{module.name}Guide>
{{
}}

/// <summary>
/// Configures <see cref="Module{module.name}"/>.
/// </summary>
public sealed class Module{module.name}Option : {option_type}<Module{module.name}>
{{
}}

/// <summary>
/// Adds the {module.base_name}{' UI' if module.is_ui else ''} module to a Monica host.
/// </summary>
public static class Module{module.name}BuilderExtensions
{{
    extension(IMonicaBuilder builder)
    {{
        /// <summary>
        /// Registers <see cref="Module{module.name}"/> and applies optional configuration.
        /// </summary>
        /// <param name="configure">Optional module-options configuration.</param>
        /// <returns>The module guide.</returns>
        public Module{module.name}Guide Add{module.name}(Action<Module{module.name}Option>? configure = null)
        {{
            return builder.AddModule<Module{module.name}, Module{module.name}Option, Module{module.name}Guide>(configure);
        }}
    }}
}}
"""
    write(project_dir / "Modules" / f"Module{module.name}.cs", code)


def create_ui_files(project_dir: Path, manifest: Manifest, module: ModuleSpec, route: str) -> None:
    resource_name = f"{module.base_name}Resource"
    page_name = f"UI{module.base_name}Page"
    write(
        project_dir / "Localization" / f"{resource_name}.cs",
        f"""
        using Monica.Core.Localization.Abstractions;

        namespace {manifest.package_id}.Localization;

        /// <summary>
        /// Identifies localization resources for the {module.base_name} UI.
        /// </summary>
        public sealed class {resource_name} : ILocalizationResource
        {{
        }}
        """,
    )
    en = {
        "Navigation": {"Title": module.base_name, "Category": "Extensions"},
        "Page": {
            "Eyebrow": "Community extension",
            "Title": module.base_name,
            "Description": manifest.description,
            "Status": "The module is registered and ready for its package-owned experience.",
        },
    }
    zh = {
        "Navigation": {"Title": module.base_name, "Category": "扩展模块"},
        "Page": {
            "Eyebrow": "社区扩展",
            "Title": module.base_name,
            "Description": "这是一个由独立开发者维护的 Monica 扩展模块。",
            "Status": "模块已注册，可以开始使用该包提供的功能。",
        },
    }
    resource_dir = project_dir / "Localization" / resource_name
    write(resource_dir / "en-US.json", json.dumps(en, ensure_ascii=False, indent=2))
    write(resource_dir / "zh-CN.json", json.dumps(zh, ensure_ascii=False, indent=2))

    write(
        project_dir / "_Imports.razor",
        f"""
        @using Microsoft.Extensions.Localization
        @using MudBlazor
        @using {manifest.package_id}.Localization
        """,
    )
    write(
        project_dir / "Pages" / f"{page_name}.razor",
        f"""
        @page "{route}"
        @inject IStringLocalizer<{resource_name}> L

        <div class="extension-page">
            <MudPaper Class="extension-hero" Elevation="2">
                <MudStack Spacing="3">
                    <MudChip T="string" Color="Color.Success" Variant="Variant.Outlined" Icon="@Icons.Material.Filled.Extension">
                        @L["Page:Eyebrow"]
                    </MudChip>
                    <MudText Typo="Typo.h3">@L["Page:Title"]</MudText>
                    <MudText Typo="Typo.body1" Color="Color.Secondary">@L["Page:Description"]</MudText>
                    <MudAlert Severity="Severity.Success" Variant="Variant.Filled" Dense="true">
                        @L["Page:Status"]
                    </MudAlert>
                </MudStack>
            </MudPaper>
        </div>
        """,
    )
    write(
        project_dir / "Pages" / f"{page_name}.razor.css",
        """
        .extension-page {
            display: grid;
            min-height: 100%;
            padding: clamp(1rem, 4vw, 3rem);
            place-items: center;
        }

        .extension-page ::deep .extension-hero {
            width: min(100%, 52rem);
            padding: clamp(1.5rem, 5vw, 4rem);
            border: 1px solid var(--mud-palette-lines-default);
            border-radius: var(--mud-default-borderradius);
            background:
                radial-gradient(circle at top right, color-mix(in srgb, var(--mud-palette-success) 18%, transparent), transparent 42%),
                var(--mud-palette-surface);
        }

        @media (max-width: 600px) {
            .extension-page {
                align-items: start;
                padding: 1rem;
            }
        }
        """,
    )


def create_tests(root: Path, manifest: Manifest) -> None:
    test_name = f"Test.{manifest.package_id}"
    test_dir = root / "tests" / test_name
    test_sdk = "Microsoft.NET.Sdk.Razor" if manifest.has_ui else "Microsoft.NET.Sdk"
    project_lines = [
        f'<Project Sdk="{test_sdk}">',
        "  <PropertyGroup>",
        "    <IsPackable>false</IsPackable>",
        "    <IsTestProject>true</IsTestProject>",
        "    <GenerateDocumentationFile>false</GenerateDocumentationFile>",
        f"    <RootNamespace>{test_name}</RootNamespace>",
    ]
    if manifest.has_ui:
        project_lines.extend(
            [
                "    <AddRazorSupportForMvc>true</AddRazorSupportForMvc>",
                "    <GenerateStaticWebAssetsManifest>true</GenerateStaticWebAssetsManifest>",
            ]
        )
    project_lines.extend(
        [
            "  </PropertyGroup>",
            "  <ItemGroup>",
            f'    <ProjectReference Include="..\\..\\src\\{manifest.package_id}\\{manifest.package_id}.csproj" />',
            '    <PackageReference Include="AwesomeAssertions" />',
            '    <PackageReference Include="coverlet.collector" PrivateAssets="All" />',
            '    <PackageReference Include="Microsoft.NET.Test.Sdk" />',
            '    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="All" />',
            '    <PackageReference Include="xunit.v3" />',
            "  </ItemGroup>",
            "  <ItemGroup Condition=\"'$(MonicaSourceRoot)' == ''\">",
            '    <PackageReference Include="Monica.Testing" />',
            "  </ItemGroup>",
            "  <ItemGroup Condition=\"'$(MonicaSourceRoot)' != ''\">",
            '    <ProjectReference Include="$(MonicaSourceRoot)\\Monica.Testing\\Monica.Testing.csproj" />',
            "  </ItemGroup>",
            "</Project>",
        ]
    )
    write(test_dir / f"{test_name}.csproj", "\n".join(project_lines))
    depended_on = {dependency for module in manifest.modules for dependency in module.depends_on}
    entry_modules = [module for module in manifest.modules if module.name not in depended_on]
    registrations = "\n".join(f"        builder.Add{module.name}();" for module in entry_modules)
    assertions = "\n".join(
        f"        scope.Resolve<IOptions<Module{module.name}Option>>().Value.Should().NotBeNull();"
        for module in manifest.modules
    )
    ui_modules = [module for module in manifest.modules if module.is_ui]
    ui_usings = ""
    ui_setup = ""
    ui_assertions = ""
    if ui_modules:
        ui_usings = f"""using {manifest.package_id}.Localization;
using {manifest.package_id}.Pages;
using Monica.Core.Localization.Abstractions;
using Monica.UI.Shell.Models;
using Monica.UI.Shell.Support;
"""
        ui_setup = """
        var pageCatalog = scope.Resolve<IPageCatalog>();
        var localizationCatalog = scope.Resolve<ILocalizationCatalog>();
""".rstrip()
        ui_assertion_blocks: list[str] = []
        for index, module in enumerate(ui_modules):
            route = ui_route(manifest, module).lstrip("/")
            category_id = module.navigation_category_id
            ui_assertion_blocks.append(
                f"""        var categoryId{index} = NavigationCategoryId.Create("{csharp_escape(category_id)}");
        var page{index} = pageCatalog.GetRegisteredPages()
            .Single(page => page.ComponentType == typeof(UI{module.base_name}Page));
        var category{index} = pageCatalog.GetNavigationCategories()
            .Single(category => category.Id == categoryId{index});
        var navigationItem{index} = pageCatalog.GetNavItems()
            .Single(item => item.Href == "{route}");

        page{index}.Route.Should().Be("{route}");
        page{index}.ComponentType.Should().Be(typeof(UI{module.base_name}Page));
        page{index}.DisplayName.ResourceType.Should().Be(typeof({module.base_name}Resource));
        page{index}.DisplayName.Key.Should().Be("Navigation:Title");
        category{index}.Id.Should().Be(categoryId{index});
        category{index}.DisplayName.ResourceType.Should().Be(typeof({module.base_name}Resource));
        category{index}.DisplayName.Key.Should().Be("Navigation:Category");
        category{index}.Order.Should().Be({UI_CATEGORY_ORDER_BASE + index});
        navigationItem{index}.Href.Should().Be("{route}");
        navigationItem{index}.Text.ResourceType.Should().Be(typeof({module.base_name}Resource));
        navigationItem{index}.Text.Key.Should().Be("Navigation:Title");
        navigationItem{index}.CategoryId.Should().Be(categoryId{index});
        navigationItem{index}.Order.Should().Be({UI_NAV_ORDER_BASE + index});
        localizationCatalog.For<{module.base_name}Resource>().Should().NotBeNull();"""
            )
        ui_assertions = "\n\n" + "\n\n".join(ui_assertion_blocks)
    write(
        test_dir / "Modules" / "ModuleRegistrationTests.cs",
        f"""using {manifest.package_id}.Modules;
{ui_usings}using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Monica.Core.Modularity.Abstractions;
using Monica.Testing.Hosting;
using Xunit;

namespace {test_name}.Modules;

public sealed class PackageTestApplicationFactory : MonicaTestApplicationFactory<Module{manifest.modules[0].name}>
{{
    protected override void ConfigureMonica(IMonicaBuilder builder)
    {{
{registrations}
    }}
}}

public sealed class ModuleRegistrationTests(PackageTestApplicationFactory factory)
    : IClassFixture<PackageTestApplicationFactory>
{{
    [Fact]
    public async Task CreateAsync_WhenEntryModulesAreRegistered_ShouldComposeDependenciesAndUiResources()
    {{
        await using var application = await factory.CreateAsync(
            cancellationToken: TestContext.Current.CancellationToken);
        await using var scope = application.CreateScope(TestContext.Current.CancellationToken);

{assertions}
{ui_setup}{ui_assertions}
    }}
}}
""",
    )
    write(
        root / "tests" / "README.md",
        f"""
        # Tests

        This independently published package keeps its runnable tests in `Test.{manifest.package_id}`.
        That publisher-first name intentionally overrides Monica's first-party `Test.Monica.*` repository convention.

        `ModuleRegistrationTests` registers only graph entry modules. Dependencies must compose transitively,
        and each UI contribution must reach the host-owned read-only page catalog with its module-key-derived category,
        module resource marker, localized page/navigation keys, deterministic orders, and navigation item.

        Run one test process at a time. Under WSL, pass the Windows project path:

        ```bash
        dotnet test '<windows-path-to-repository>\\tests\\{test_name}\\{test_name}.csproj'
        ```
        """,
    )


def create_readme(root: Path, manifest: Manifest) -> None:
    module_rows = "\n".join(
        f"| `Module{module.name}` | `{module.key}` | `{module.kind}` | `monica.Add{module.name}()` |"
        for module in manifest.modules
    )
    depended_on = {dependency for module in manifest.modules for dependency in module.depends_on}
    entry_modules = [module for module in manifest.modules if module.name not in depended_on]
    registrations = "\n".join(f"    monica.Add{module.name}();" for module in entry_modules)
    license_text = manifest.license_expression or manifest.license_file.name
    lines = [
        f"# {manifest.package_id}",
        "",
        f"![Package icon]({manifest.icon_name})",
    ]
    if manifest.show_open_source_badge:
        lines.extend(["", "![Monica Open Source](monica-open-source-badge.svg)"])
    lines.extend(["", manifest.description, ""])
    if manifest.icon_kind == "compatibility-mark":
        lines.extend([COMPATIBILITY_NOTICE, ""])
    lines.extend(
        [
            "## Install",
            "",
            "```bash",
            f"dotnet add package {manifest.package_id} --version {manifest.version}",
            "```",
            "",
            "## Modules",
            "",
            "| Module | Module key | Kind | Registration |",
            "|---|---|---|---|",
            module_rows,
            "",
            "One NuGet package may contain multiple coherent Monica modules. The registration below adds only graph entry modules; declared dependencies compose transitively.",
            "",
            "## Register",
            "",
            "```csharp",
            f"using {manifest.package_id}.Modules;",
            "",
            "var builder = WebApplication.CreateBuilder(args);",
            "",
            "builder.AddMonica(monica =>",
            "{",
            registrations,
            "});",
            "```",
            "",
            "The scaffold validates identity metadata, localization resources, module composition tests, and packed archive structure. It is not a releasable capability by itself: replace the sample shell with real behavior and complete a clean local-feed consumer test before publishing.",
            "",
            "## Package contract",
            "",
            f"- NuGet owner: `{manifest.nuget_owner}`",
            f"- Distribution: `{manifest.distribution}`",
            f"- Publishing target: `{manifest.publish_target}`",
            f"- Source available: `{'yes' if manifest.source_available else 'no'}`",
            f"- Source provider: `{manifest.source_provider}`",
            f"- Monica minimum version: `{manifest.monica_version}`",
            "- Monica Ecosystem Standard: `v1`",
            f"- License: `{license_text}`",
            "",
            "## Project links",
            "",
            f"- [Project]({manifest.project_url})",
            f"- [Support]({manifest.support_url})",
            f"- [Security policy]({manifest.security_url})",
            "",
            "## Independence",
            "",
            f"This package is maintained by {manifest.authors} for the NuGet owner {manifest.nuget_owner}. It is independent of the Monica project.",
        ]
    )
    if manifest.source_available and manifest.repository_url:
        project_link_index = lines.index(f"- [Project]({manifest.project_url})") + 1
        lines.insert(project_link_index, f"- [Repository]({manifest.repository_url})")
    write(root / "README.md", "\n".join(lines))


def create_license(root: Path, manifest: Manifest) -> None:
    if manifest.license_file:
        shutil.copy2(manifest.license_file, root / manifest.license_file.name)
        return
    if manifest.license_expression != "MIT":
        return
    year = datetime.now(UTC).year
    write(
        root / "LICENSE",
        f"""
        MIT License

        Copyright (c) {year} {manifest.authors}

        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
        """,
    )


def create_workflows(root: Path, manifest: Manifest) -> None:
    if manifest.source_provider != "github":
        return

    localization_step = (
        "\n              - run: python scripts/validate_localization.py --root . --strict"
        if manifest.has_ui
        else ""
    )
    write(
        root / ".github" / "workflows" / "ci.yml",
        f"""
        name: CI

        on:
          push:
            branches: [main]
          pull_request:

        permissions:
          contents: read

        jobs:
          validate:
            runs-on: ubuntu-latest
            steps:
              - uses: actions/checkout@v4
              - uses: actions/setup-dotnet@v4
                with:
                  dotnet-version: "10.0.x"
              - run: python scripts/validate_package.py --root .{localization_step}
              - run: dotnet restore
              - run: dotnet build --configuration Release --no-restore
              - run: dotnet test --configuration Release --no-build
              - run: dotnet pack --configuration Release --no-build --output artifacts
              - run: python scripts/inspect_package.py --root . --artifacts artifacts
              - uses: actions/upload-artifact@v4
                with:
                  name: packages
                  path: artifacts/*.*nupkg
        """,
    )
    if manifest.publish_target != "nuget.org":
        return

    write(
        root / ".github" / "workflows" / "publish.yml",
        """
        name: Publish

        on:
          push:
            tags: ["v*"]

        permissions:
          contents: read
          id-token: write

        jobs:
          publish:
            runs-on: ubuntu-latest
            environment: release
            steps:
              - uses: actions/checkout@v4
              - uses: actions/setup-dotnet@v4
                with:
                  dotnet-version: "10.0.x"
              - name: Derive package version
                id: version
                shell: bash
                run: echo "value=${GITHUB_REF_NAME#v}" >> "$GITHUB_OUTPUT"
              - run: python scripts/validate_package.py --root . --package-version "${{ steps.version.outputs.value }}"
              - run: dotnet restore
              - run: dotnet build --configuration Release --no-restore
              - run: dotnet test --configuration Release --no-build
              - run: dotnet pack --configuration Release --no-build --output artifacts -p:PackageVersion=${{ steps.version.outputs.value }}
              - run: python scripts/inspect_package.py --root . --artifacts artifacts --package-version "${{ steps.version.outputs.value }}"
              - name: Exchange OIDC token for a temporary NuGet key
                uses: NuGet/login@v1
                id: nuget-login
                with:
                  user: ${{ secrets.NUGET_USER }}
              - name: Publish package
                run: dotnet nuget push artifacts/*.nupkg --api-key "${{ steps.nuget-login.outputs.NUGET_API_KEY }}" --source https://api.nuget.org/v3/index.json
        """,
    )


def create_solution(root: Path, manifest: Manifest) -> None:
    test_name = f"Test.{manifest.package_id}"
    write(
        root / f"{manifest.package_id}.slnx",
        f"""
        <Solution>
          <Folder Name="/src/">
            <Project Path="src/{manifest.package_id}/{manifest.package_id}.csproj" />
          </Folder>
          <Folder Name="/tests/">
            <Project Path="tests/{test_name}/{test_name}.csproj" />
          </Folder>
        </Solution>
        """,
    )


def create_package_contract(root: Path, manifest: Manifest) -> None:
    source: dict[str, Any] = {
        "available": manifest.source_available,
        "provider": manifest.source_provider,
    }
    if manifest.source_link_version:
        source["sourceLinkVersion"] = manifest.source_link_version

    publishing: dict[str, Any] = {"target": manifest.publish_target}
    if manifest.publish_feed_url:
        publishing["feedUrl"] = manifest.publish_feed_url

    license_data: dict[str, Any] = {"openSource": manifest.open_source}
    if manifest.license_expression:
        license_data["expression"] = manifest.license_expression
    else:
        assert manifest.license_file is not None
        license_data["file"] = manifest.license_file.name

    icon: dict[str, Any] = {"kind": manifest.icon_kind}
    if manifest.icon_file:
        icon["file"] = manifest.icon_file.name

    payload = {
        "schemaVersion": 1,
        "packageId": manifest.package_id,
        "version": manifest.version,
        "description": manifest.description,
        "authors": manifest.authors,
        "nugetOwner": manifest.nuget_owner,
        "projectUrl": manifest.project_url,
        "supportUrl": manifest.support_url,
        "securityUrl": manifest.security_url,
        "source": source,
        "distribution": manifest.distribution,
        "publishing": publishing,
        "targetFramework": manifest.target_framework,
        "monicaVersion": manifest.monica_version,
        "capabilityTags": list(manifest.capability_tags),
        "license": license_data,
        "branding": {
            "icon": icon,
            "showOpenSourceBadge": manifest.show_open_source_badge,
        },
        "modules": [
            {
                "name": module.name,
                "kind": module.kind,
                "key": module.key,
                **({"dependsOn": list(module.depends_on)} if module.depends_on else {}),
            }
            for module in manifest.modules
        ],
    }
    if manifest.repository_url:
        payload["repositoryUrl"] = manifest.repository_url
    write(root / "package.manifest.json", json.dumps(payload, ensure_ascii=False, indent=2))


def create_repository(manifest: Manifest, output: Path) -> None:
    output = output.resolve()
    if output.exists() and any(output.iterdir()):
        raise ValueError(f"Output directory must be new or empty: {output}")
    output.mkdir(parents=True, exist_ok=True)

    create_directory_build_props(output, manifest)
    create_directory_packages_props(output, manifest)
    project_dir = create_project(output, manifest)
    ui_index = 0
    for module in manifest.modules:
        create_module(project_dir, manifest, module, ui_index)
        if module.is_ui:
            ui_index += 1
    create_tests(output, manifest)
    create_readme(output, manifest)
    create_license(output, manifest)
    create_workflows(output, manifest)
    create_solution(output, manifest)
    create_package_contract(output, manifest)

    skill_root = Path(__file__).resolve().parent.parent
    skill_assets = skill_root / "assets"
    if manifest.icon_kind == "compatibility-mark":
        shutil.copy2(skill_assets / "monica-compatibility-mark.png", output / "monica-compatibility-mark.png")
        shutil.copy2(skill_assets / "monica-compatibility-mark.svg", output / "monica-compatibility-mark.svg")
    else:
        assert manifest.icon_file is not None
        shutil.copy2(manifest.icon_file, output / manifest.icon_name)
    if manifest.show_open_source_badge:
        shutil.copy2(skill_assets / "monica-open-source-badge.svg", output / "monica-open-source-badge.svg")

    generated_scripts = output / "scripts"
    generated_scripts.mkdir(parents=True, exist_ok=True)
    shutil.copy2(skill_root / "scripts" / "validate_package.py", generated_scripts / "validate_package.py")
    shutil.copy2(skill_root / "scripts" / "inspect_package.py", generated_scripts / "inspect_package.py")
    if manifest.has_ui:
        localization_validator = skill_root.parent / "monica-ui-localization" / "scripts" / "validate_localization.py"
        if not localization_validator.is_file():
            raise ValueError(f"Required companion localization validator was not found: {localization_validator}")
        shutil.copy2(localization_validator, generated_scripts / "validate_localization.py")

    write(
        output / ".gitignore",
        """
        **/bin/
        **/obj/
        .idea/
        .vs/
        .vscode/
        TestResults/
        artifacts/
        *.user
        *.suo
        """,
    )


def main() -> int:
    args = parse_args()
    try:
        manifest = load_manifest(args.manifest)
        create_repository(manifest, args.output)
    except (ValueError, OSError, json.JSONDecodeError) as exc:
        print(f"Scaffold failed: {exc}", file=sys.stderr)
        return 1

    print(f"Created {manifest.package_id} at {args.output.resolve()}")
    print("Implement the real capability, then run validate_package.py, build, test, pack, and a local-feed consumer test.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
