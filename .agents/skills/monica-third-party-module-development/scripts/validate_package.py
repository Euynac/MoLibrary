#!/usr/bin/env python3
"""Validate a repository against Monica Ecosystem Standard v1."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import struct
import sys
import xml.etree.ElementTree as ET
from dataclasses import asdict, dataclass
from pathlib import Path


ID_SEGMENT = r"[A-Za-z][A-Za-z0-9]*"
PACKAGE_ID_PATTERN = re.compile(
    rf"^(?P<publisher>{ID_SEGMENT})\.Monica\.{ID_SEGMENT}(?:\.{ID_SEGMENT})*$"
)
MODULE_KEY_PATTERN = PACKAGE_ID_PATTERN
MODULE_ATTRIBUTE_PATTERN = re.compile(
    r'\[ModuleKey\(\s*"(?P<key>[^"]+)"\s*\)\]\s*'
    r'(?:public\s+|internal\s+|sealed\s+|abstract\s+|partial\s+)*'
    r'class\s+(?P<class>Module[A-Za-z_][A-Za-z0-9_]*)',
    re.MULTILINE,
)
NAMESPACE_PATTERN = re.compile(r"\bnamespace\s+(?P<namespace>[A-Za-z_][A-Za-z0-9_.]*)\s*[;{]")
CONST_STRING_PATTERN_TEMPLATE = (
    r"\bconst\s+string\s+{name}\s*=\s*\"(?P<route>/[^\"]+)\"\s*;"
)
PROPERTY_PATTERN = re.compile(r"\$\((?P<name>[^)]+)\)")
SEMVER_PATTERN = re.compile(
    r"^(?:0|[1-9][0-9]*)\."
    r"(?:0|[1-9][0-9]*)\."
    r"(?:0|[1-9][0-9]*)"
    r"(?:-(?:[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?"
    r"(?:\+(?:[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$"
)
SOURCE_LINK_PACKAGES = {
    "Microsoft.SourceLink.GitHub": "github.com",
    "Microsoft.SourceLink.GitLab": "gitlab",
    "Microsoft.SourceLink.AzureRepos.Git": "dev.azure.com",
}
SOURCE_LINK_PROVIDERS = {
    "github": "Microsoft.SourceLink.GitHub",
    "gitlab": "Microsoft.SourceLink.GitLab",
    "azure-repos": "Microsoft.SourceLink.AzureRepos.Git",
}
MANAGED_TAGS = {"monica", "monica-module", "monica-ecosystem-v1", "monica-ui", "extension"}
COMPATIBILITY_MARK_SHA256 = "d7825fce56b42710468b95b76a689c512114e693a4d52d76dbb2ad6c96f27d7b"
COMPATIBILITY_NOTICE = (
    "Monica compatibility is self-attested by the publisher. This community package is "
    "independently maintained and is not affiliated with, endorsed by, or supported by "
    "the Monica project."
)


@dataclass(frozen=True)
class Finding:
    code: str
    message: str
    path: str


@dataclass(frozen=True)
class PackageReference:
    version: str
    private_assets: str


@dataclass(frozen=True)
class ModuleDeclaration:
    key: str
    class_name: str
    namespace: str
    path: Path


@dataclass(frozen=True)
class Invocation:
    method_name: str
    type_arguments: tuple[str, ...]
    arguments: tuple[str, ...]
    start: int
    end: int


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Validate third-party Monica package identity, metadata, and module keys."
    )
    parser.add_argument("--root", required=True, type=Path, help="Repository root.")
    parser.add_argument(
        "--project",
        action="append",
        type=Path,
        help="Packable project path relative to root. Repeat for multiple projects.",
    )
    parser.add_argument(
        "--package-version",
        help="Effective package version, such as a release-tag-derived PackageVersion override.",
    )
    parser.add_argument("--json", action="store_true", help="Emit machine-readable JSON.")
    return parser.parse_args()


def read_xml(path: Path) -> ET.Element | None:
    try:
        return ET.parse(path).getroot()
    except (ET.ParseError, OSError):
        return None


def property_files(root: Path, project: Path) -> list[Path]:
    directories: list[Path] = []
    current = project.parent.resolve()
    root = root.resolve()
    while current == root or root in current.parents:
        directories.append(current)
        if current == root:
            break
        current = current.parent

    files = [directory / "Directory.Build.props" for directory in reversed(directories)]
    files.append(project)
    return [path for path in files if path.is_file()]


def expand(value: str, properties: dict[str, str]) -> str:
    result = value
    for _ in range(10):
        updated = PROPERTY_PATTERN.sub(lambda match: properties.get(match.group("name"), match.group(0)), result)
        if updated == result:
            break
        result = updated
    return result.strip()


def load_properties(root: Path, project: Path) -> dict[str, str]:
    project_name = project.stem
    properties: dict[str, str] = {
        "MSBuildProjectName": project_name,
        "AssemblyName": project_name,
        "RootNamespace": project_name,
        "PackageId": project_name,
    }

    for path in property_files(root, project):
        xml_root = read_xml(path)
        if xml_root is None:
            continue
        for group in xml_root.findall("PropertyGroup"):
            if group.get("Condition"):
                continue
            for element in group:
                if element.get("Condition") or element.text is None:
                    continue
                properties[element.tag] = expand(element.text, properties)

    for key, value in list(properties.items()):
        properties[key] = expand(value, properties)
    return properties


def nearest_file(root: Path, start: Path, filename: str) -> Path | None:
    current = start.resolve()
    root = root.resolve()
    while current == root or root in current.parents:
        candidate = current / filename
        if candidate.is_file():
            return candidate
        if current == root:
            return None
        current = current.parent
    return None


def package_references(root: Path, project: Path) -> dict[str, PackageReference]:
    properties = load_properties(root, project)
    central_versions: dict[str, str] = {}
    central = nearest_file(root, project.parent, "Directory.Packages.props")
    if central:
        xml_root = read_xml(central)
        if xml_root is not None:
            for item in xml_root.findall(".//PackageVersion"):
                package = item.get("Include") or item.get("Update")
                version = item.get("Version")
                if package and version:
                    central_versions[package] = expand(version, properties)

    references: dict[str, PackageReference] = {}
    xml_root = read_xml(project)
    if xml_root is not None:
        for item in xml_root.findall(".//PackageReference"):
            package = item.get("Include") or item.get("Update")
            version = item.get("Version") or (item.findtext("Version") or "")
            private_assets = item.get("PrivateAssets") or (item.findtext("PrivateAssets") or "")
            if package:
                references[package] = PackageReference(
                    version=expand(version or central_versions.get(package, ""), properties),
                    private_assets=private_assets,
                )
    return references


def load_package_contract(root: Path) -> dict[str, object] | None:
    path = root / "package.manifest.json"
    if not path.is_file():
        return None
    try:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
    except (json.JSONDecodeError, OSError):
        return None
    return payload if isinstance(payload, dict) else None


def find_packable_projects(root: Path) -> list[Path]:
    projects: list[Path] = []
    for project in sorted(root.rglob("*.csproj")):
        if any(part in {"bin", "obj", ".git"} for part in project.parts):
            continue
        properties = load_properties(root, project)
        if properties.get("IsTestProject", "").lower() == "true":
            continue
        if properties.get("IsPackable", "").lower() == "false":
            continue
        package_id = properties.get("PackageId", project.stem)
        if PACKAGE_ID_PATTERN.fullmatch(package_id):
            projects.append(project)
    return projects


def bool_property(properties: dict[str, str], name: str) -> bool:
    return properties.get(name, "").strip().lower() == "true"


def resolve_artifact(root: Path, project: Path, value: str) -> Path | None:
    if not value or "$" in value:
        return None
    normalized = value.replace("\\", "/")
    candidates = [project.parent / normalized, root / normalized, root / "assets" / normalized]
    return next((candidate.resolve() for candidate in candidates if candidate.is_file()), None)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    digest.update(path.read_bytes())
    return digest.hexdigest()


def png_icon_error(path: Path) -> str | None:
    payload = path.read_bytes()
    if len(payload) < 33 or payload[:8] != b"\x89PNG\r\n\x1a\n" or payload[12:16] != b"IHDR":
        return "PackageIcon must be a valid PNG file."
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
        return f"PackageIcon must be at least 64x64; found {width}x{height}."
    if not has_transparency:
        return "PackageIcon must support transparency."
    return None


def find_module_declarations(project: Path) -> list[ModuleDeclaration]:
    declarations: list[ModuleDeclaration] = []
    for source in project.parent.rglob("*.cs"):
        if any(part in {"bin", "obj"} for part in source.parts):
            continue
        text = source.read_text(encoding="utf-8-sig")
        namespace_match = NAMESPACE_PATTERN.search(text)
        namespace = namespace_match.group("namespace") if namespace_match else ""
        for match in MODULE_ATTRIBUTE_PATTERN.finditer(text):
            declarations.append(
                ModuleDeclaration(match.group("key"), match.group("class"), namespace, source)
            )
    return declarations


def mask_csharp_comments(source: str) -> str:
    """Replace comments with spaces while preserving source offsets and string literals."""
    masked = list(source)
    index = 0
    while index < len(source):
        if source.startswith("//", index):
            end = source.find("\n", index + 2)
            end = len(source) if end < 0 else end
            for position in range(index, end):
                masked[position] = " "
            index = end
            continue
        if source.startswith("/*", index):
            end = source.find("*/", index + 2)
            end = len(source) if end < 0 else end + 2
            for position in range(index, end):
                if masked[position] not in "\r\n":
                    masked[position] = " "
            index = end
            continue
        if source[index] in {'"', "'"}:
            index = csharp_literal_end(source, index)
            continue
        index += 1
    return "".join(masked)


def mask_csharp_non_code(source: str) -> str:
    """Mask comments and literals so invocation names are discovered only in executable code."""
    comments_masked = mask_csharp_comments(source)
    masked = list(comments_masked)
    index = 0
    while index < len(comments_masked):
        if comments_masked[index] not in {'"', "'"}:
            index += 1
            continue
        end = csharp_literal_end(comments_masked, index)
        for position in range(index, end):
            if masked[position] not in "\r\n":
                masked[position] = " "
        index = end
    return "".join(masked)


def csharp_literal_end(source: str, quote_index: int) -> int:
    quote = source[quote_index]
    if quote == '"' and source.startswith('"""', quote_index):
        quote_count = 3
        while quote_index + quote_count < len(source) and source[quote_index + quote_count] == '"':
            quote_count += 1
        delimiter = '"' * quote_count
        end = source.find(delimiter, quote_index + quote_count)
        return len(source) if end < 0 else end + quote_count

    is_verbatim = quote == '"' and quote_index > 0 and source[quote_index - 1] == "@"
    index = quote_index + 1
    while index < len(source):
        if is_verbatim and source.startswith('""', index):
            index += 2
            continue
        if source[index] == quote:
            return index + 1
        if not is_verbatim and source[index] == "\\":
            index += 2
            continue
        index += 1
    return len(source)


def find_matching_delimiter(source: str, opening_index: int, opening: str, closing: str) -> int | None:
    depth = 0
    index = opening_index
    while index < len(source):
        if source[index] in {'"', "'"}:
            index = csharp_literal_end(source, index)
            continue
        if source[index] == opening:
            depth += 1
        elif source[index] == closing:
            depth -= 1
            if depth == 0:
                return index
        index += 1
    return None


def split_top_level(source: str) -> tuple[str, ...]:
    parts: list[str] = []
    stack: list[str] = []
    pair = {')': '(', ']': '[', '}': '{', '>': '<'}
    start = 0
    index = 0
    while index < len(source):
        character = source[index]
        if character in {'"', "'"}:
            index = csharp_literal_end(source, index)
            continue
        if character in "([{<":
            stack.append(character)
        elif character in pair:
            if stack and stack[-1] == pair[character]:
                stack.pop()
        elif character == "," and not stack:
            parts.append(source[start:index].strip())
            start = index + 1
        index += 1
    tail = source[start:].strip()
    if tail:
        parts.append(tail)
    return tuple(parts)


def find_generic_invocations(source: str, method_name: str) -> list[Invocation]:
    delimiters = mask_csharp_comments(source)
    searchable = mask_csharp_non_code(source)
    pattern = re.compile(rf"\b{re.escape(method_name)}\s*<")
    invocations: list[Invocation] = []
    for match in pattern.finditer(searchable):
        opening_angle = searchable.find("<", match.start(), match.end())
        closing_angle = find_matching_delimiter(delimiters, opening_angle, "<", ">")
        if closing_angle is None:
            continue
        opening_parenthesis = closing_angle + 1
        while opening_parenthesis < len(searchable) and searchable[opening_parenthesis].isspace():
            opening_parenthesis += 1
        if opening_parenthesis >= len(searchable) or searchable[opening_parenthesis] != "(":
            continue
        closing_parenthesis = find_matching_delimiter(
            delimiters,
            opening_parenthesis,
            "(",
            ")",
        )
        if closing_parenthesis is None:
            continue
        invocations.append(
            Invocation(
                method_name=method_name,
                type_arguments=split_top_level(source[opening_angle + 1:closing_angle]),
                arguments=split_top_level(source[opening_parenthesis + 1:closing_parenthesis]),
                start=match.start(),
                end=closing_parenthesis + 1,
            )
        )
    return invocations


def find_method_invocations(source: str, method_name: str) -> list[Invocation]:
    delimiters = mask_csharp_comments(source)
    searchable = mask_csharp_non_code(source)
    pattern = re.compile(rf"\b{re.escape(method_name)}\s*\(")
    invocations: list[Invocation] = []
    for match in pattern.finditer(searchable):
        opening_parenthesis = searchable.find("(", match.start(), match.end())
        closing_parenthesis = find_matching_delimiter(
            delimiters,
            opening_parenthesis,
            "(",
            ")",
        )
        if closing_parenthesis is None:
            continue
        invocations.append(
            Invocation(
                method_name=method_name,
                type_arguments=(),
                arguments=split_top_level(source[opening_parenthesis + 1:closing_parenthesis]),
                start=match.start(),
                end=closing_parenthesis + 1,
            )
        )
    return invocations


def invocation_argument(invocation: Invocation, position: int, name: str) -> str | None:
    positional: list[str] = []
    named: dict[str, str] = {}
    for argument in invocation.arguments:
        match = re.match(r"^(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*(?P<value>.*)$", argument, re.DOTALL)
        if match:
            named[match.group("name")] = match.group("value").strip()
        else:
            positional.append(argument.strip())
    return named.get(name) or (positional[position] if position < len(positional) else None)


def csharp_string_value(expression: str | None) -> str | None:
    if expression is None:
        return None
    value = expression.strip()
    if value.startswith('@"') and value.endswith('"'):
        return value[2:-1].replace('""', '"')
    if not (value.startswith('"') and value.endswith('"')):
        return None
    try:
        decoded = json.loads(value)
    except json.JSONDecodeError:
        return None
    return decoded if isinstance(decoded, str) else None


def normalize_csharp_type(value: str) -> str:
    return re.sub(r"\s+", "", value).removeprefix("global::")


def assigned_identifier(source: str, invocation: Invocation) -> str | None:
    prefix = source[max(0, invocation.start - 240):invocation.start]
    match = re.search(
        r"\b(?:var|NavigationCategoryId)\s+(?P<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*"
        r"(?:(?:[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*)+$",
        prefix,
    )
    return match.group("name") if match else None


def is_integer_literal(expression: str | None) -> bool:
    return bool(expression and re.fullmatch(r"-?[0-9][0-9_]*", expression.strip()))


def localized_navigation_contract_errors(
    module_text: str,
    expected_category_id: str,
    expected_page_type: str,
) -> list[str]:
    errors: list[str] = []
    blocks = find_method_invocations(module_text, "RegisterUIComponents")
    categories = find_generic_invocations(module_text, "RegisterLocalizedCategory")
    pages = find_generic_invocations(module_text, "RegisterLocalizedPage")

    if len(blocks) != 1:
        errors.append("use exactly one RegisterUIComponents block")
    if len(categories) != 1:
        errors.append("register exactly one localized navigation category")
    if not pages:
        errors.append("register at least one localized page")
    if len(blocks) == 1:
        block = blocks[0]
        if any(not (block.start < item.start < item.end <= block.end) for item in [*categories, *pages]):
            errors.append("keep category and page registrations inside the RegisterUIComponents block")

    if len(categories) != 1:
        return errors

    category = categories[0]
    category_resource = (
        normalize_csharp_type(category.type_arguments[0])
        if len(category.type_arguments) == 1
        else None
    )
    if category_resource is None:
        errors.append("give RegisterLocalizedCategory exactly one resource type")
    if csharp_string_value(invocation_argument(category, 0, "categoryId")) != expected_category_id:
        errors.append(f"derive category id '{expected_category_id}' from the UI ModuleKey")
    if csharp_string_value(invocation_argument(category, 1, "displayNameKey")) != "Navigation:Category":
        errors.append("use Navigation:Category as the category resource key")
    if not is_integer_literal(invocation_argument(category, 2, "order")):
        errors.append("set an explicit integer category order")

    category_variable = assigned_identifier(module_text, category)
    if category_variable is None:
        errors.append("assign the registered category id to a local variable")

    found_primary_page = False
    for page in pages:
        page_types = tuple(normalize_csharp_type(value) for value in page.type_arguments)
        if len(page_types) != 2:
            errors.append("give RegisterLocalizedPage one page type and one resource type")
            continue
        page_type, page_resource = page_types
        is_primary_page = page_type == expected_page_type
        found_primary_page = found_primary_page or is_primary_page
        if category_resource is not None and page_resource != category_resource:
            errors.append("use the category resource type for every localized page")
        page_key = csharp_string_value(invocation_argument(page, 1, "displayNameKey"))
        if is_primary_page and page_key != "Navigation:Title":
            errors.append("use Navigation:Title for the primary page resource key")
        elif not is_primary_page and (page_key is None or not page_key.startswith("Navigation:")):
            errors.append("use a module-owned Navigation:* resource key for every additional page")
        category_expression = invocation_argument(page, 3, "categoryId")
        if category_variable is not None and (
            category_expression is None or category_expression.strip() != category_variable
        ):
            errors.append("pass the registered category variable through categoryId")
        add_to_nav = (invocation_argument(page, 4, "addToNav") or "").strip().casefold()
        nav_order = invocation_argument(page, 5, "navOrder")
        if is_primary_page:
            if add_to_nav != "true":
                errors.append("set addToNav to true for the primary page")
            if not is_integer_literal(nav_order):
                errors.append("set an explicit integer navigation order for the primary page")
        elif add_to_nav not in {"", "false", "true"}:
            errors.append("set addToNav to a boolean literal for every additional page")
        elif add_to_nav == "true" and not is_integer_literal(nav_order):
            errors.append("set an explicit integer navigation order for every additional navigation page")

    if pages and not found_primary_page:
        errors.append(f"register primary page type {expected_page_type}")

    return list(dict.fromkeys(errors))


def resolve_registered_route(project: Path, module_source: Path) -> tuple[str | None, str | None]:
    module_text = module_source.read_text(encoding="utf-8-sig")
    invocations = find_generic_invocations(module_text, "RegisterLocalizedPage")
    if not invocations:
        return None, "localized route registration was not found"
    argument = invocation_argument(invocations[0], 0, "route")
    literal = csharp_string_value(argument)
    if literal is not None:
        return literal, None
    if argument is None or re.fullmatch(
        r"[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)+",
        argument,
    ) is None:
        return None, "route argument is neither a literal nor a qualified const string"

    qualifier, constant_name = argument.rsplit(".", 1)
    type_name = qualifier.rsplit(".", 1)[-1]
    constant_pattern = re.compile(
        CONST_STRING_PATTERN_TEMPLATE.format(name=re.escape(constant_name))
    )
    routes: set[str] = set()
    for suffix in ("*.cs", "*.razor"):
        for candidate in project.parent.rglob(suffix):
            if any(part in {"bin", "obj"} for part in candidate.parts):
                continue
            if not (
                candidate.name == f"{type_name}.razor"
                or candidate.name == f"{type_name}.cs"
                or candidate.name.startswith(f"{type_name}.")
            ):
                continue
            text = candidate.read_text(encoding="utf-8-sig")
            routes.update(item.group("route") for item in constant_pattern.finditer(text))
    if len(routes) == 1:
        return routes.pop(), None
    if not routes:
        return None, f"could not resolve constant route argument '{argument}' to a literal const string"
    return None, f"constant route argument '{argument}' resolves to multiple values: {', '.join(sorted(routes))}"


def is_valid_semver(value: str) -> bool:
    if SEMVER_PATTERN.fullmatch(value) is None:
        return False
    prerelease = value.partition("-")[2].partition("+")[0]
    return not any(
        identifier.isdigit() and len(identifier) > 1 and identifier.startswith("0")
        for identifier in prerelease.split(".")
        if identifier
    )


def kebab_case(value: str) -> str:
    tokens = re.findall(r"[A-Z]+(?=[A-Z][a-z]|[0-9]|$)|[A-Z]?[a-z]+|[0-9]+", value)
    return "-".join(token.casefold() for token in tokens)


def package_route_prefix(package_id: str) -> str:
    package_segments = package_id.split(".")[2:]
    if len(package_segments) > 1 and package_segments[-1].casefold() == "ui":
        package_segments = package_segments[:-1]
    segments = [kebab_case(segment) for segment in package_segments]
    return "/" + "-".join(segments)


def validate_project(
    root: Path,
    project: Path,
    effective_package_version: str | None,
) -> list[Finding]:
    findings: list[Finding] = []
    properties = load_properties(root, project)
    relative_project = str(project.relative_to(root))
    package_id = properties.get("PackageId", project.stem)

    def add(code: str, message: str, path: Path = project) -> None:
        findings.append(Finding(code, message, str(path.relative_to(root))))

    match = PACKAGE_ID_PATTERN.fullmatch(package_id)
    if not match or len(package_id) > 100:
        add("MTP001", f"PackageId '{package_id}' does not match <Publisher>.Monica.<Package>[.<Variant>] or exceeds 100 characters.")
    elif match.group("publisher").casefold() == "monica":
        add("MTP002", "Publisher segment 'Monica' is reserved for first-party packages.")

    for property_name in ("AssemblyName", "RootNamespace"):
        if properties.get(property_name, project.stem) != package_id:
            add("MTP003", f"{property_name} must exactly match PackageId '{package_id}'.")
    if project.stem != package_id:
        add("MTP004", f"Project file must be named '{package_id}.csproj'.")

    required = {
        "Version": "Version is required.",
        "Authors": "Authors is required and must identify the independent publisher.",
        "NuGetOwner": "NuGetOwner is required and must identify the publishing account or organization.",
        "PackageDescription": "PackageDescription is required.",
        "PackageProjectUrl": "PackageProjectUrl is required.",
        "PackageReadmeFile": "PackageReadmeFile is required.",
        "PackageIcon": "PackageIcon is required.",
    }
    for property_name, message in required.items():
        if not properties.get(property_name):
            add("MTP005", message)

    repository_url = properties.get("RepositoryUrl", "")
    repository_type = properties.get("RepositoryType", "")
    if repository_url and not repository_url.startswith("https://"):
        add("MTP006", "RepositoryUrl must use HTTPS.")
    if repository_url and repository_type.lower() != "git":
        add("MTP007", "RepositoryType must be 'git'.")
    if not repository_url and repository_type:
        add("MTP007", "RepositoryType must be omitted when RepositoryUrl is omitted.")
    project_url = properties.get("PackageProjectUrl", "")
    if project_url and not project_url.startswith("https://"):
        add("MTP006", "PackageProjectUrl must use HTTPS.")

    contract = load_package_contract(root)
    source_available: bool | None = None
    source_provider: str | None = None
    contract_repository_url: str | None = None
    if contract is not None:
        source = contract.get("source")
        if isinstance(source, dict):
            available = source.get("available")
            provider = source.get("provider")
            source_available = available if isinstance(available, bool) else None
            source_provider = provider.casefold() if isinstance(provider, str) else None
        repository = contract.get("repositoryUrl")
        contract_repository_url = repository if isinstance(repository, str) else None

    if source_available is True:
        if not repository_url:
            add("MTP029", "Source-available packages must emit RepositoryUrl package metadata.")
        elif contract_repository_url and repository_url != contract_repository_url:
            add("MTP029", "RepositoryUrl package metadata must match package.manifest.json.")
    elif source_available is False and (repository_url or repository_type):
        add(
            "MTP029",
            "Source-unavailable packages must omit consumer-visible RepositoryUrl and RepositoryType metadata.",
        )

    license_expression = properties.get("PackageLicenseExpression", "")
    license_file = properties.get("PackageLicenseFile", "")
    if bool(license_expression) == bool(license_file):
        add("MTP008", "Configure exactly one of PackageLicenseExpression or PackageLicenseFile.")
    if license_file and resolve_artifact(root, project, license_file) is None:
        add("MTP009", f"PackageLicenseFile '{license_file}' does not exist.")

    readme = resolve_artifact(root, project, properties.get("PackageReadmeFile", ""))
    if properties.get("PackageReadmeFile") and readme is None:
        add("MTP010", f"PackageReadmeFile '{properties['PackageReadmeFile']}' does not exist.")
    icon = resolve_artifact(root, project, properties.get("PackageIcon", ""))
    if properties.get("PackageIcon") and icon is None:
        add("MTP011", f"PackageIcon '{properties['PackageIcon']}' does not exist.")
    elif icon is not None:
        icon_error = png_icon_error(icon)
        if icon_error:
            add("MTP011", icon_error, icon)

    uses_compatibility_mark = False
    if icon is not None:
        icon_matches_canonical_mark = sha256(icon) == COMPATIBILITY_MARK_SHA256
        claims_compatibility_mark = icon.name.casefold() == "monica-compatibility-mark.png"
        if claims_compatibility_mark and not icon_matches_canonical_mark:
            add(
                "MTP032",
                "monica-compatibility-mark.png must match the canonical Monica Compatibility Mark unchanged.",
                icon,
            )
        uses_compatibility_mark = icon_matches_canonical_mark
    if uses_compatibility_mark and readme is not None:
        readme_text = readme.read_text(encoding="utf-8-sig")
        if COMPATIBILITY_NOTICE not in readme_text:
            add(
                "MTP012",
                "README must include the Monica Compatibility Mark self-attestation and independence notice.",
                readme,
            )

    tags = {
        tag.casefold()
        for tag in re.split(r"[;,\s]+", properties.get("PackageTags", ""))
        if tag
    }
    for tag in ("monica", "monica-module", "monica-ecosystem-v1"):
        if tag not in tags:
            add("MTP013", f"PackageTags must include '{tag}'.")

    for property_name in ("GenerateDocumentationFile", "TreatWarningsAsErrors", "IncludeSymbols"):
        if not bool_property(properties, property_name):
            add("MTP014", f"{property_name} must be true.")
    if properties.get("SymbolPackageFormat", "").casefold() != "snupkg":
        add("MTP015", "SymbolPackageFormat must be 'snupkg'.")

    references = package_references(root, project)
    source_link_references = {
        package: reference
        for package, reference in references.items()
        if package in SOURCE_LINK_PACKAGES
    }
    if len(source_link_references) > 1:
        add("MTP016", "Reference only the Source Link provider that owns RepositoryUrl.")
    for package, reference in source_link_references.items():
        private_assets = {
            item.casefold()
            for item in re.split(r"[;,\s]+", reference.private_assets)
            if item
        }
        if "all" not in private_assets:
            add("MTP016", f"{package} must set PrivateAssets=All.")
        if not bool_property(properties, "PublishRepositoryUrl"):
            add("MTP014", "PublishRepositoryUrl must be true when Source Link is enabled.")
        if not bool_property(properties, "EmbedUntrackedSources"):
            add("MTP014", "EmbedUntrackedSources must be true when Source Link is enabled.")
        expected_host = SOURCE_LINK_PACKAGES[package]
        if expected_host not in repository_url.casefold():
            add("MTP016", f"{package} does not match RepositoryUrl '{repository_url}'.")
    if not source_link_references and (
        bool_property(properties, "PublishRepositoryUrl") or bool_property(properties, "EmbedUntrackedSources")
    ):
        add("MTP016", "Source Link properties are enabled without a supported Source Link package reference.")
    if source_available is True:
        expected_source_link = SOURCE_LINK_PROVIDERS.get(source_provider or "")
        if expected_source_link is None:
            add("MTP029", "A source-available package must declare a supported source provider in package.manifest.json.")
        elif expected_source_link not in source_link_references:
            add("MTP016", f"Source provider '{source_provider}' requires {expected_source_link}.")
    elif source_available is False:
        if source_link_references:
            add("MTP029", "Source-unavailable packages must not reference Source Link packages.")
        if bool_property(properties, "PublishRepositoryUrl") or bool_property(properties, "EmbedUntrackedSources"):
            add("MTP029", "Source-unavailable packages must not publish repository source metadata.")

    declared_package_version = properties.get("PackageVersion") or properties.get("Version", "")
    package_version = effective_package_version or declared_package_version
    if not is_valid_semver(package_version):
        add("MTP023", f"Effective package version '{package_version}' is not valid three-part SemVer.")
    for package, reference in references.items():
        if package.casefold().startswith("monica.") and not is_valid_semver(reference.version):
            add("MTP023", f"Monica dependency version '{package} {reference.version}' is not valid three-part SemVer.")
    prerelease_monica = [
        (package, reference.version)
        for package, reference in references.items()
        if package.casefold().startswith("monica.") and "-" in reference.version
    ]
    if prerelease_monica and "-" not in package_version:
        dependency_list = ", ".join(f"{package} {version}" for package, version in prerelease_monica)
        add("MTP017", f"Stable package version '{package_version}' cannot depend on prerelease Monica packages: {dependency_list}.")

    declarations = find_module_declarations(project)
    if not declarations:
        add("MTP018", "No third-party [ModuleKey(\"...\")] module declaration was found.")

    legacy_registration_sources: set[Path] = set()
    for source in project.parent.rglob("*.cs"):
        if any(part in {"bin", "obj"} for part in source.parts):
            continue
        source_text = source.read_text(encoding="utf-8-sig")
        if find_generic_invocations(source_text, "RegisterLocalizedComponent"):
            legacy_registration_sources.add(source)
            add(
                "MTP030",
                "RegisterLocalizedComponent is a legacy navigation API; register a stable localized category "
                "and use RegisterLocalizedPage instead.",
                source,
            )

    seen: dict[str, str] = {}
    package_prefix = package_id.casefold() + "."
    expected_namespace = f"{package_id}.Modules"
    has_ui_module = False
    for declaration in declarations:
        key = declaration.key
        class_name = declaration.class_name
        source = declaration.path
        if declaration.namespace != expected_namespace:
            add(
                "MTP024",
                f"Third-party module registrations must use namespace '{expected_namespace}', not '{declaration.namespace or '<missing>'}'.",
                source,
            )
        if not MODULE_KEY_PATTERN.fullmatch(key) or len(key) > 100:
            add("MTP019", f"Module key '{key}' is not a valid ecosystem key.", source)
            continue
        folded = key.casefold()
        if folded != package_id.casefold() and not folded.startswith(package_prefix):
            add("MTP020", f"Module key '{key}' must equal PackageId or start with '{package_id}.'.", source)
        previous = seen.get(folded)
        if previous and previous != class_name:
            add("MTP021", f"Module key '{key}' is used by both {previous} and {class_name}.", source)
        seen[folded] = class_name
        is_ui_key = folded.endswith(".ui")
        class_suffix = class_name.removeprefix("Module")
        is_exact_ui_class = class_suffix.endswith("UI")
        has_ui_module = has_ui_module or is_ui_key
        if is_ui_key:
            base_name = class_suffix[:-2] if is_exact_ui_class else ""
            if not is_exact_ui_class or not base_name or base_name.casefold().endswith("ui"):
                add(
                    "MTP022",
                    f"UI identity mismatch: {class_name} must end with one exact 'UI' suffix and have a non-empty base name.",
                    source,
                )
            if source in legacy_registration_sources:
                continue

            module_text = source.read_text(encoding="utf-8-sig")
            category_id = key[:-3]
            navigation_errors = localized_navigation_contract_errors(
                module_text,
                category_id,
                f"UI{base_name}Page",
            )
            if navigation_errors:
                add(
                    "MTP031",
                    f"{class_name} does not follow the stable localized navigation contract: "
                    + "; ".join(navigation_errors)
                    + ".",
                    source,
                )
            registered_route, route_error = resolve_registered_route(project, source)
            route_prefix = package_route_prefix(package_id)
            if registered_route is None:
                add("MTP028", f"Could not verify {class_name} UI route: {route_error}.", source)
            elif not (
                registered_route == route_prefix
                or registered_route.startswith(route_prefix + "-")
            ):
                add(
                    "MTP027",
                    f"UI route '{registered_route}' must equal '{route_prefix}' or start with '{route_prefix}-'.",
                    source,
                )
        elif class_suffix.casefold().endswith("ui"):
            add("MTP022", f"UI identity mismatch: {class_name} must use a final '.UI' key segment, and non-UI modules must not.", source)

    if has_ui_module and "monica-ui" not in tags:
        add("MTP025", "PackageTags must include 'monica-ui' when the package contains a UI module.")
    if not (tags - MANAGED_TAGS):
        add("MTP026", "PackageTags must include at least one package-specific capability or provider tag.")

    if findings:
        return findings

    return [Finding("OK", f"{package_id} complies with Monica Ecosystem Standard v1.", relative_project)]


def main() -> int:
    args = parse_args()
    root = args.root.resolve()
    if not root.is_dir():
        print(f"Repository root does not exist: {root}", file=sys.stderr)
        return 2
    if args.package_version is not None and not is_valid_semver(args.package_version):
        print(f"Invalid --package-version: {args.package_version}", file=sys.stderr)
        return 2

    if args.project:
        projects = [(root / project).resolve() if not project.is_absolute() else project.resolve() for project in args.project]
    else:
        projects = find_packable_projects(root)

    missing = [project for project in projects if not project.is_file()]
    if missing:
        for project in missing:
            print(f"Project does not exist: {project}", file=sys.stderr)
        return 2
    if not projects:
        print("No packable third-party Monica project was found.", file=sys.stderr)
        return 2

    results = [
        finding
        for project in projects
        for finding in validate_project(root, project, args.package_version)
    ]
    failures = [finding for finding in results if finding.code != "OK"]

    if args.json:
        print(json.dumps({"status": "failed" if failures else "passed", "findings": [asdict(item) for item in results]}, indent=2))
    else:
        for finding in results:
            prefix = "PASS" if finding.code == "OK" else "ERROR"
            print(f"[{prefix}] {finding.code} {finding.path}: {finding.message}")
        print(f"\nValidated {len(projects)} package project(s): {len(failures)} error(s).")

    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
