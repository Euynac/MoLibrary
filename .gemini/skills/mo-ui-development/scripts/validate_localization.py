#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Localization Validation Script for Monica Framework

This script validates localization keys bidirectionally across all UI modules:
1. Missing keys: Keys used in Razor files or C# files but not defined in JSON
2. Unused keys: Keys defined in JSON but never used
3. Language sync: Keys present in one language but not another
4. UI registry keys: Keys passed to RegisterLocalizedComponent must exist in UIRegistryResource

Auto-discovers Monica projects with UI modules and validates their localization resources.
"""

import argparse
import json
import re
import sys
from collections import defaultdict
from pathlib import Path
from typing import DefaultDict, Dict, List, Optional, Set, Tuple

# Force UTF-8 encoding for stdout/stderr on Windows
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')

COMMENT_PATTERNS = (
    re.compile(r'@\*.*?\*@', re.DOTALL),
    re.compile(r'<!--.*?-->', re.DOTALL),
    re.compile(r'/\*.*?\*/', re.DOTALL),
    re.compile(r'(?m)^[ \t]*//.*$'),
)
DIRECT_KEY_PATTERN = re.compile(
    r'["\'](?P<key>[A-Z][A-Za-z0-9]*(?::[A-Z][A-Za-z0-9]*)+)["\']',
)
INTERPOLATED_KEY_PATTERN = re.compile(
    r'(?P<prefix>\$@|@\$|@|\$)"(?P<template>[^"]*\{[^"]+\}[^"]*)"',
    re.DOTALL,
)
INTERPOLATION_PATTERN = re.compile(r'\{[^{}]+\}')


class Colors:
    """ANSI color codes for terminal output."""

    RED = '\033[91m'
    YELLOW = '\033[93m'
    GREEN = '\033[92m'
    BOLD = '\033[1m'
    END = '\033[0m'


class LocalizationValidator:
    """Validates localization keys across Razor files, C# files, and JSON resources."""

    def __init__(self, root_path: Path, languages: List[str]):
        self.root_path = root_path
        self.languages = languages
        self.ui_projects = self._discover_ui_projects()
        self.localization_resources = self._discover_localization_resources()
        self.ui_registry_resource_dir = self.root_path / 'Monica.UI' / 'Localization' / 'UIRegistryResource'

        self.used_keys: DefaultDict[str, List[Tuple[str, int]]] = defaultdict(list)
        self.defined_keys: Dict[str, Set[str]] = {}
        self.all_defined_keys: Set[str] = set()
        self.missing_keys: DefaultDict[str, List[Tuple[str, int]]] = defaultdict(list)
        self.unused_keys: Set[str] = set()
        self.sync_issues: Dict[str, Dict[str, bool]] = {}
        self.json_errors: List[Tuple[Path, str]] = []

        self.ui_registry_defined_keys: Dict[str, Set[str]] = {}
        self.ui_registry_used_keys: DefaultDict[str, List[Tuple[str, int, str]]] = defaultdict(list)
        self.ui_registry_missing_keys: DefaultDict[str, List[Tuple[str, int, str]]] = defaultdict(list)

    def _discover_ui_projects(self) -> List[str]:
        """Auto-discover Monica projects that contain UI modules."""
        ui_projects = []

        for item in self.root_path.iterdir():
            if not item.is_dir() or not item.name.startswith('Monica.'):
                continue

            csproj_files = list(item.glob('*.csproj'))
            if not csproj_files:
                continue

            if item.name.endswith('.UI'):
                ui_projects.append(item.name)
                continue

            ui_module_files = list(item.glob('Modules/Module*UI.cs'))
            if ui_module_files:
                ui_projects.append(item.name)

        return sorted(ui_projects)

    def _discover_localization_resources(self) -> List[Path]:
        """Discover all localization resource directories in UI projects."""
        resources = []

        for project in self.ui_projects:
            project_path = self.root_path / project
            localization_dir = project_path / 'Localization'
            if not localization_dir.exists():
                continue

            for subdir in localization_dir.iterdir():
                if subdir.is_dir() and any(subdir.glob('*.json')):
                    resources.append(subdir)

        return resources

    def scan_razor_files(self) -> None:
        """Extract localization keys from Razor files."""
        self._scan_source_files('*.razor')

    def scan_cs_files(self) -> None:
        """Extract localization keys from C# files."""
        self._scan_source_files('*.cs')

    def _scan_source_files(self, glob_pattern: str) -> None:
        """Extract localization keys from source files by scanning string literals."""
        for project in self.ui_projects:
            project_path = self.root_path / project
            if not project_path.exists():
                continue

            for source_file in project_path.rglob(glob_pattern):
                self._scan_source_file(source_file)

    def _scan_source_file(self, source_file: Path) -> None:
        """Scan a single source file for explicit or interpolated localization keys."""
        try:
            content = source_file.read_text(encoding='utf-8')
        except Exception as exc:
            print(f"Warning: Could not read {source_file}: {exc}", file=sys.stderr)
            return

        masked_content = self._mask_comments(content)
        relative_path = str(source_file.relative_to(self.root_path))

        for match in DIRECT_KEY_PATTERN.finditer(masked_content):
            line_num = masked_content.count('\n', 0, match.start()) + 1
            self.used_keys[match.group('key')].append((relative_path, line_num))

        for match in INTERPOLATED_KEY_PATTERN.finditer(masked_content):
            template = match.group('template').strip()
            if not template:
                continue

            line_num = masked_content.count('\n', 0, match.start()) + 1
            for key in self._extract_interpolated_keys(template):
                self.used_keys[key].append((relative_path, line_num))

    @staticmethod
    def _mask_comments(content: str) -> str:
        """Mask comments while preserving line numbers for diagnostics."""
        masked_content = content
        for pattern in COMMENT_PATTERNS:
            masked_content = pattern.sub(
                lambda match: re.sub(r'[^\n]', ' ', match.group(0)),
                masked_content,
            )
        return masked_content

    def _extract_interpolated_keys(self, template: str) -> Set[str]:
        """Resolve a simple interpolated template to matching defined localization keys."""
        if not self.all_defined_keys or '{' not in template or '}' not in template:
            return set()

        pattern = self._build_interpolated_key_pattern(template)
        if pattern is None:
            return set()

        keys: Set[str] = set()
        for defined_key in self.all_defined_keys:
            if pattern.fullmatch(defined_key):
                keys.add(defined_key)

        return keys

    @staticmethod
    def _build_interpolated_key_pattern(template: str) -> Optional[re.Pattern[str]]:
        """Build a regex that maps a simple interpolated key template to defined keys."""
        if ':' not in template or not template[:1].isupper():
            return None

        parts: List[str] = []
        last_index = 0
        placeholder_count = 0

        for match in INTERPOLATION_PATTERN.finditer(template):
            parts.append(re.escape(template[last_index:match.start()]))
            parts.append(r'[A-Za-z0-9]+')
            last_index = match.end()
            placeholder_count += 1

        if placeholder_count == 0:
            return None

        parts.append(re.escape(template[last_index:]))
        pattern = ''.join(parts)
        if ':' not in pattern:
            return None

        try:
            return re.compile(f'^{pattern}$')
        except re.error:
            return None

    def scan_ui_registry_keys(self) -> None:
        """Validate RegisterLocalizedComponent keys against UIRegistryResource."""
        for project in self.ui_projects:
            project_path = self.root_path / project
            if not project_path.exists():
                continue

            for cs_file in project_path.rglob('*.cs'):
                try:
                    content = cs_file.read_text(encoding='utf-8')
                except Exception as exc:
                    print(f"Warning: Could not read {cs_file}: {exc}", file=sys.stderr)
                    continue

                relative_path = str(cs_file.relative_to(self.root_path))
                for start_index, argument_block in self._extract_register_localized_component_calls(content):
                    line_num = content.count('\n', 0, start_index) + 1
                    args = self._split_top_level_args(argument_block)
                    if len(args) < 2:
                        continue

                    display_name_value = self._extract_argument_string(args[1], 'displayNameKey')
                    if display_name_value:
                        self.ui_registry_used_keys[display_name_value].append(
                            (relative_path, line_num, 'displayNameKey'))

                    category_value = None
                    if len(args) >= 4:
                        category_value = self._extract_argument_string(args[3], 'categoryKey')

                    if category_value:
                        self.ui_registry_used_keys[category_value].append(
                            (relative_path, line_num, 'categoryKey'))

    def _extract_register_localized_component_calls(self, content: str) -> List[Tuple[int, str]]:
        """Return the argument block for each RegisterLocalizedComponent invocation."""
        marker = 'RegisterLocalizedComponent<'
        results: List[Tuple[int, str]] = []
        index = 0

        while True:
            start = content.find(marker, index)
            if start == -1:
                break

            open_paren = content.find('(', start)
            if open_paren == -1:
                break

            close_paren = self._find_matching_parenthesis(content, open_paren)
            if close_paren == -1:
                break

            results.append((start, content[open_paren + 1:close_paren]))
            index = close_paren + 1

        return results

    @staticmethod
    def _find_matching_parenthesis(content: str, start_index: int) -> int:
        """Find the matching closing parenthesis for a call."""
        depth = 0
        in_string = False
        escape = False

        for index in range(start_index, len(content)):
            char = content[index]

            if in_string:
                if escape:
                    escape = False
                elif char == '\\':
                    escape = True
                elif char == '"':
                    in_string = False
                continue

            if char == '"':
                in_string = True
                continue

            if char == '(':
                depth += 1
                continue

            if char == ')':
                depth -= 1
                if depth == 0:
                    return index

        return -1

    @staticmethod
    def _split_top_level_args(argument_block: str) -> List[str]:
        """Split method arguments while respecting strings and nested delimiters."""
        args: List[str] = []
        current: List[str] = []
        in_string = False
        escape = False
        depth_parenthesis = 0
        depth_brace = 0
        depth_bracket = 0
        depth_angle = 0

        for char in argument_block:
            if in_string:
                current.append(char)
                if escape:
                    escape = False
                elif char == '\\':
                    escape = True
                elif char == '"':
                    in_string = False
                continue

            if char == '"':
                in_string = True
                current.append(char)
                continue

            if char == '(':
                depth_parenthesis += 1
            elif char == ')':
                depth_parenthesis -= 1
            elif char == '{':
                depth_brace += 1
            elif char == '}':
                depth_brace -= 1
            elif char == '[':
                depth_bracket += 1
            elif char == ']':
                depth_bracket -= 1
            elif char == '<':
                depth_angle += 1
            elif char == '>':
                depth_angle = max(0, depth_angle - 1)

            if char == ',' and depth_parenthesis == depth_brace == depth_bracket == depth_angle == 0:
                args.append(''.join(current).strip())
                current = []
                continue

            current.append(char)

        tail = ''.join(current).strip()
        if tail:
            args.append(tail)

        return args

    @staticmethod
    def _extract_argument_string(argument: str, expected_name: Optional[str] = None) -> Optional[str]:
        """Extract a string literal from an argument, optionally handling named arguments."""
        text = argument.strip()
        if expected_name and text.startswith(f'{expected_name}:'):
            text = text.split(':', 1)[1].strip()

        if text.startswith('"') and text.endswith('"') and len(text) >= 2:
            return text[1:-1]

        return None

    def validate_json_integrity(self) -> bool:
        """Validate JSON syntax and structure for all localization files."""
        has_errors = False

        for lang in self.languages:
            for resource_dir in self.localization_resources:
                json_file = resource_dir / f'{lang}.json'
                if not json_file.exists():
                    continue

                try:
                    data = json.loads(json_file.read_text(encoding='utf-8'))
                    if not isinstance(data, dict):
                        self.json_errors.append((json_file, 'Root element must be a JSON object (dict), not array or primitive'))
                        has_errors = True
                        continue

                    if self._has_empty_keys(data):
                        self.json_errors.append((json_file, 'Contains empty string keys'))
                        has_errors = True

                    invalid_values = self._find_non_string_values(data)
                    if invalid_values:
                        error_message = f"Contains non-string leaf values: {', '.join(invalid_values[:5])}"
                        if len(invalid_values) > 5:
                            error_message += f" (and {len(invalid_values) - 5} more)"
                        self.json_errors.append((json_file, error_message))
                        has_errors = True
                except json.JSONDecodeError as exc:
                    self.json_errors.append((json_file, f'JSON syntax error: {exc}'))
                    has_errors = True
                except Exception as exc:
                    self.json_errors.append((json_file, f'Error reading file: {exc}'))
                    has_errors = True

        return not has_errors

    def _has_empty_keys(self, obj: dict) -> bool:
        """Check if a JSON object contains empty string keys."""
        for key, value in obj.items():
            if key == '':
                return True
            if isinstance(value, dict) and self._has_empty_keys(value):
                return True
        return False

    def _find_non_string_values(self, obj: dict, path: str = '') -> List[str]:
        """Find all leaf values that are not strings."""
        invalid_values: List[str] = []

        for key, value in obj.items():
            full_path = f'{path}:{key}' if path else key
            if isinstance(value, dict):
                invalid_values.extend(self._find_non_string_values(value, full_path))
            elif not isinstance(value, str):
                invalid_values.append(f'{full_path} ({type(value).__name__})')

        return invalid_values

    def load_json_keys(self) -> None:
        """Load and flatten JSON keys from all discovered localization resources."""
        self.all_defined_keys = set()

        for lang in self.languages:
            all_keys: Set[str] = set()

            for resource_dir in self.localization_resources:
                json_file = resource_dir / f'{lang}.json'
                if not json_file.exists():
                    continue

                try:
                    data = json.loads(json_file.read_text(encoding='utf-8'))
                except json.JSONDecodeError as exc:
                    print(f'Error: Invalid JSON in {json_file}: {exc}', file=sys.stderr)
                    sys.exit(1)
                except Exception as exc:
                    print(f'Error: Could not load {json_file}: {exc}', file=sys.stderr)
                    sys.exit(1)

                all_keys.update(self._flatten_json(data))

            self.defined_keys[lang] = all_keys
            self.all_defined_keys.update(all_keys)

    def load_ui_registry_keys(self) -> None:
        """Load and flatten UIRegistryResource keys for targeted validation."""
        for lang in self.languages:
            json_file = self.ui_registry_resource_dir / f'{lang}.json'
            if not json_file.exists():
                self.ui_registry_defined_keys[lang] = set()
                continue

            try:
                data = json.loads(json_file.read_text(encoding='utf-8'))
            except json.JSONDecodeError as exc:
                print(f'Error: Invalid JSON in {json_file}: {exc}', file=sys.stderr)
                sys.exit(1)
            except Exception as exc:
                print(f'Error: Could not load {json_file}: {exc}', file=sys.stderr)
                sys.exit(1)

            self.ui_registry_defined_keys[lang] = self._flatten_json(data)

    def _flatten_json(self, obj: dict, parent_key: str = '') -> Set[str]:
        """Recursively flatten a JSON object to colon-separated keys."""
        keys: Set[str] = set()

        for key, value in obj.items():
            full_key = f'{parent_key}:{key}' if parent_key else key
            if isinstance(value, dict):
                keys.update(self._flatten_json(value, full_key))
            else:
                keys.add(full_key)

        return keys

    def validate_bidirectional(self) -> None:
        """Check used keys against all defined resource keys."""
        all_defined: Set[str] = set()
        for keys in self.defined_keys.values():
            all_defined.update(keys)

        for key, locations in self.used_keys.items():
            if key not in all_defined:
                self.missing_keys[key] = locations

        all_used = set(self.used_keys.keys())
        self.unused_keys = all_defined - all_used

    def validate_language_sync(self) -> None:
        """Ensure all languages have the same defined keys."""
        if len(self.languages) < 2:
            return

        all_keys: Set[str] = set()
        for keys in self.defined_keys.values():
            all_keys.update(keys)

        for key in all_keys:
            presence = {lang: key in self.defined_keys.get(lang, set()) for lang in self.languages}
            if not all(presence.values()):
                self.sync_issues[key] = presence

    def validate_ui_registry_usage(self) -> None:
        """Ensure UI navigation keys resolve from UIRegistryResource specifically."""
        all_ui_registry_keys: Set[str] = set()
        for keys in self.ui_registry_defined_keys.values():
            all_ui_registry_keys.update(keys)

        for key, locations in self.ui_registry_used_keys.items():
            if key not in all_ui_registry_keys:
                self.ui_registry_missing_keys[key] = locations

    def generate_report(self, output_format: str = 'console', summary_only: bool = False) -> Dict:
        """Generate the final validation report."""
        report = {
            'json_errors': [(str(path), message) for path, message in self.json_errors],
            'missing_keys': dict(self.missing_keys),
            'unused_keys': sorted(self.unused_keys),
            'sync_issues': self.sync_issues,
            'ui_registry_missing_keys': {
                key: [
                    {
                        'file': file_path,
                        'line': line_num,
                        'role': role
                    }
                    for file_path, line_num, role in locations
                ]
                for key, locations in self.ui_registry_missing_keys.items()
            },
            'summary': {
                'total_keys_defined': len(set().union(*self.defined_keys.values())) if self.defined_keys else 0,
                'total_keys_used': len(self.used_keys),
                'json_errors_count': len(self.json_errors),
                'missing_count': len(self.missing_keys),
                'unused_count': len(self.unused_keys),
                'sync_issues_count': len(self.sync_issues),
                'ui_registry_missing_count': len(self.ui_registry_missing_keys),
                'status': 'PASSED' if not self.json_errors and not self.missing_keys and not self.sync_issues and not self.ui_registry_missing_keys else 'FAILED'
            }
        }

        if output_format == 'json':
            print(json.dumps(report, indent=2, ensure_ascii=False))
        else:
            self._print_console_report(report, summary_only)

        return report

    def _print_console_report(self, report: Dict, summary_only: bool) -> None:
        """Print a colored console report."""
        print(f"\n{Colors.BOLD}=== Localization Validation Report ==={Colors.END}\n")

        if not summary_only:
            if self.json_errors:
                print(f"{Colors.RED}{Colors.BOLD}[ERROR] JSON Integrity Issues:{Colors.END}")
                for json_file, error_message in self.json_errors:
                    print(f"  {Colors.RED}✗{Colors.END} {json_file}")
                    print(f"    {error_message}")
                print()

            if self.missing_keys:
                print(f"{Colors.RED}{Colors.BOLD}[ERROR] Missing Keys (used but not defined):{Colors.END}")
                for key, locations in sorted(self.missing_keys.items()):
                    print(f"  {Colors.RED}✗{Colors.END} {key}")
                    for file_path, line_num in locations:
                        print(f"    Used in: {file_path}:{line_num}")
                print()

            if self.ui_registry_missing_keys:
                print(f"{Colors.RED}{Colors.BOLD}[ERROR] Invalid UI Registry Keys (must exist in UIRegistryResource):{Colors.END}")
                for key, locations in sorted(self.ui_registry_missing_keys.items()):
                    print(f"  {Colors.RED}✗{Colors.END} {key}")
                    for file_path, line_num, role in locations:
                        print(f"    Used in: {file_path}:{line_num} ({role})")
                print()

            if self.unused_keys:
                print(f"{Colors.YELLOW}{Colors.BOLD}[WARNING] Unused Keys (defined but not used):{Colors.END}")
                for key in sorted(self.unused_keys):
                    print(f"  {Colors.YELLOW}⚠{Colors.END} {key}")
                    langs = [lang for lang, keys in self.defined_keys.items() if key in keys]
                    print(f"    Defined in: {', '.join(langs)}")
                print()

            if self.sync_issues:
                print(f"{Colors.RED}{Colors.BOLD}[ERROR] Language Sync Issues:{Colors.END}")
                for key, presence in sorted(self.sync_issues.items()):
                    print(f"  {Colors.RED}✗{Colors.END} {key}")
                    present_in = [lang for lang, exists in presence.items() if exists]
                    missing_in = [lang for lang, exists in presence.items() if not exists]
                    print(f"    Present in: {', '.join(present_in)}")
                    print(f"    Missing in: {', '.join(missing_in)}")
                print()

        summary = report['summary']
        print(f"{Colors.BOLD}Summary:{Colors.END}")
        print(f"  Total keys defined: {summary['total_keys_defined']}")
        print(f"  Total keys used: {summary['total_keys_used']}")
        print(f"  JSON integrity errors: {Colors.RED if summary['json_errors_count'] > 0 else Colors.GREEN}{summary['json_errors_count']}{Colors.END}")
        print(f"  Missing keys: {Colors.RED if summary['missing_count'] > 0 else Colors.GREEN}{summary['missing_count']}{Colors.END}")
        print(f"  Invalid UI registry keys: {Colors.RED if summary['ui_registry_missing_count'] > 0 else Colors.GREEN}{summary['ui_registry_missing_count']}{Colors.END}")
        print(f"  Unused keys: {Colors.YELLOW if summary['unused_count'] > 0 else Colors.GREEN}{summary['unused_count']}{Colors.END}")
        print(f"  Language sync issues: {Colors.RED if summary['sync_issues_count'] > 0 else Colors.GREEN}{summary['sync_issues_count']}{Colors.END}")

        status_color = Colors.GREEN if summary['status'] == 'PASSED' else Colors.RED
        print(f"  Status: {status_color}{Colors.BOLD}{summary['status']}{Colors.END}\n")


def main() -> None:
    parser = argparse.ArgumentParser(description='Validate localization keys in Monica Framework')
    parser.add_argument('--strict', action='store_true', help='Treat unused keys as errors')
    parser.add_argument('--json', action='store_true', help='Output results in JSON format')
    parser.add_argument('--summary', action='store_true', help='Show summary only')
    parser.add_argument('--languages', type=str, default='zh-CN,en-US', help='Comma-separated list of languages')
    parser.add_argument('--root', type=str, default='.', help='Repository root path')

    args = parser.parse_args()

    root_path = Path(args.root).resolve()
    languages = [lang.strip() for lang in args.languages.split(',') if lang.strip()]

    if not root_path.exists():
        print(f'Error: Root path does not exist: {root_path}', file=sys.stderr)
        sys.exit(1)

    is_root_directory = any(root_path.glob('*.slnx')) or any(root_path.glob('*.sln'))
    if not is_root_directory:
        print(f"{Colors.RED}Error: This script must be run from the repository root directory.{Colors.END}", file=sys.stderr)
        print(f'Current directory: {root_path}', file=sys.stderr)
        print('Please navigate to the repository root (where .slnx or .sln file is located) and run the script again.', file=sys.stderr)
        sys.exit(1)

    validator = LocalizationValidator(root_path, languages)

    try:
        print('Validating JSON integrity...')
        json_valid = validator.validate_json_integrity()

        if not json_valid:
            print(f"\n{Colors.RED}JSON integrity validation failed. Fix JSON errors before proceeding.{Colors.END}\n")
            output_format = 'json' if args.json else 'console'
            validator.generate_report(output_format, args.summary)
            sys.exit(1)

        validator.load_json_keys()
        validator.load_ui_registry_keys()
        print('Scanning Razor files...')
        validator.scan_razor_files()
        print('Scanning C# files...')
        validator.scan_cs_files()
        print('Scanning UI registry keys...')
        validator.scan_ui_registry_keys()
        validator.validate_bidirectional()
        validator.validate_language_sync()
        validator.validate_ui_registry_usage()

        output_format = 'json' if args.json else 'console'
        report = validator.generate_report(output_format, args.summary)

        summary = report['summary']
        has_errors = (
            summary['missing_count'] > 0
            or summary['sync_issues_count'] > 0
            or summary['ui_registry_missing_count'] > 0
        )
        has_warnings = summary['unused_count'] > 0

        if has_errors:
            sys.exit(1)
        if has_warnings and args.strict:
            sys.exit(2)

        sys.exit(0)
    except KeyboardInterrupt:
        print('\nValidation interrupted by user', file=sys.stderr)
        sys.exit(130)
    except Exception as exc:
        print(f'Error: {exc}', file=sys.stderr)
        sys.exit(1)


if __name__ == '__main__':
    main()
