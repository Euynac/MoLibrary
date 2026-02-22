#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Localization Validation Script for Monica Framework

This script validates localization keys bidirectionally across all UI modules:
1. Missing keys: Keys used in Razor files but not defined in JSON
2. Unused keys: Keys defined in JSON but never used in Razor files
3. Language sync: Keys present in one language but not another

Auto-discovers all *.UI projects in the repository and validates their localization resources.
"""

import json
import re
import sys
import argparse
from pathlib import Path
from typing import Dict, List, Set, Tuple
from collections import defaultdict

# Force UTF-8 encoding for stdout/stderr on Windows
if sys.platform == 'win32':
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8')


class Colors:
    """ANSI color codes for terminal output"""
    RED = '\033[91m'
    YELLOW = '\033[93m'
    GREEN = '\033[92m'
    BLUE = '\033[94m'
    BOLD = '\033[1m'
    END = '\033[0m'


class LocalizationValidator:
    """Validates localization keys across Razor files and JSON resources"""

    def __init__(self, root_path: Path, languages: List[str]):
        self.root_path = root_path
        self.languages = languages
        self.ui_projects = self._discover_ui_projects()

        # Auto-discover all localization resources
        self.localization_resources = self._discover_localization_resources()

        # Results
        self.used_keys: Dict[str, List[Tuple[str, int]]] = defaultdict(list)
        self.defined_keys: Dict[str, Set[str]] = {}
        self.missing_keys: Dict[str, List[Tuple[str, int]]] = defaultdict(list)
        self.unused_keys: Set[str] = set()
        self.sync_issues: Dict[str, Dict[str, bool]] = {}
        self.json_errors: List[Tuple[Path, str]] = []

    def _discover_ui_projects(self) -> List[str]:
        """Auto-discover all *.UI projects in the repository"""
        ui_projects = []

        for item in self.root_path.iterdir():
            if item.is_dir() and item.name.endswith('.UI'):
                # Verify it's a valid project by checking for .csproj
                csproj_files = list(item.glob('*.csproj'))
                if csproj_files:
                    ui_projects.append(item.name)

        return sorted(ui_projects)

    def _discover_localization_resources(self) -> List[Path]:
        """Discover all localization resource directories in UI projects"""
        resources = []

        for project in self.ui_projects:
            project_path = self.root_path / project
            if not project_path.exists():
                continue

            # Look for Localization directories
            localization_dir = project_path / 'Localization'
            if not localization_dir.exists():
                continue

            # Find all subdirectories that contain JSON files
            for subdir in localization_dir.iterdir():
                if subdir.is_dir():
                    # Check if this directory contains language JSON files
                    has_json = any(subdir.glob('*.json'))
                    if has_json:
                        resources.append(subdir)

        return resources

    def scan_razor_files(self) -> None:
        """Extract @L[\"...\"] and L[\"...\"] patterns from all *.UI projects"""
        pattern = re.compile(r'@?L\[\"([^\"]+)\"(?:\s*,\s*[^\]]+)?\]')

        for project in self.ui_projects:
            project_path = self.root_path / project
            if not project_path.exists():
                continue

            for razor_file in project_path.rglob('*.razor'):
                try:
                    with open(razor_file, 'r', encoding='utf-8') as f:
                        lines = f.readlines()

                    for line_num, line in enumerate(lines, 1):
                        if '@*' in line or '*@' in line:
                            continue

                        matches = pattern.findall(line)
                        for key in matches:
                            relative_path = razor_file.relative_to(self.root_path)
                            self.used_keys[key].append((str(relative_path), line_num))

                except Exception as e:
                    print(f"Warning: Could not read {razor_file}: {e}", file=sys.stderr)

    def validate_json_integrity(self) -> bool:
        """Validate JSON syntax and structure for all localization files"""
        has_errors = False

        for lang in self.languages:
            for resource_dir in self.localization_resources:
                json_file = resource_dir / f'{lang}.json'

                if not json_file.exists():
                    continue

                try:
                    with open(json_file, 'r', encoding='utf-8') as f:
                        content = f.read()

                    # Try to parse JSON
                    data = json.loads(content)

                    # Validate structure: must be a dict at root level
                    if not isinstance(data, dict):
                        self.json_errors.append((json_file, "Root element must be a JSON object (dict), not array or primitive"))
                        has_errors = True
                        continue

                    # Validate no empty keys
                    if self._has_empty_keys(data):
                        self.json_errors.append((json_file, "Contains empty string keys"))
                        has_errors = True

                    # Validate all leaf values are strings
                    invalid_values = self._find_non_string_values(data)
                    if invalid_values:
                        error_msg = f"Contains non-string leaf values: {', '.join(invalid_values[:5])}"
                        if len(invalid_values) > 5:
                            error_msg += f" (and {len(invalid_values) - 5} more)"
                        self.json_errors.append((json_file, error_msg))
                        has_errors = True

                except json.JSONDecodeError as e:
                    self.json_errors.append((json_file, f"JSON syntax error: {e}"))
                    has_errors = True
                except Exception as e:
                    self.json_errors.append((json_file, f"Error reading file: {e}"))
                    has_errors = True

        return not has_errors

    def _has_empty_keys(self, obj: dict, path: str = '') -> bool:
        """Check if dict contains any empty string keys"""
        for key, value in obj.items():
            if key == '':
                return True
            if isinstance(value, dict):
                if self._has_empty_keys(value, f"{path}:{key}" if path else key):
                    return True
        return False

    def _find_non_string_values(self, obj: dict, path: str = '') -> List[str]:
        """Find all leaf values that are not strings"""
        non_strings = []

        for key, value in obj.items():
            full_path = f"{path}:{key}" if path else key

            if isinstance(value, dict):
                non_strings.extend(self._find_non_string_values(value, full_path))
            elif not isinstance(value, str):
                non_strings.append(f"{full_path} ({type(value).__name__})")

        return non_strings

    def load_json_keys(self) -> None:
        """Load and flatten JSON keys from all discovered localization resources"""
        for lang in self.languages:
            all_keys = set()

            for resource_dir in self.localization_resources:
                json_file = resource_dir / f'{lang}.json'

                if not json_file.exists():
                    continue

                try:
                    with open(json_file, 'r', encoding='utf-8') as f:
                        data = json.load(f)

                    keys = self._flatten_json(data)
                    all_keys.update(keys)

                except json.JSONDecodeError as e:
                    print(f"Error: Invalid JSON in {json_file}: {e}", file=sys.stderr)
                    sys.exit(1)
                except Exception as e:
                    print(f"Error: Could not load {json_file}: {e}", file=sys.stderr)
                    sys.exit(1)

            self.defined_keys[lang] = all_keys

    def _flatten_json(self, obj: dict, parent_key: str = '') -> Set[str]:
        """Recursively flatten JSON structure to colon-separated keys"""
        keys = set()

        for key, value in obj.items():
            full_key = f"{parent_key}:{key}" if parent_key else key

            if isinstance(value, dict):
                keys.update(self._flatten_json(value, full_key))
            else:
                keys.add(full_key)

        return keys

    def validate_bidirectional(self) -> None:
        """Check used vs defined keys"""
        all_defined = set()
        for keys in self.defined_keys.values():
            all_defined.update(keys)

        for key, locations in self.used_keys.items():
            if key not in all_defined:
                self.missing_keys[key] = locations

        all_used = set(self.used_keys.keys())
        self.unused_keys = all_defined - all_used

    def validate_language_sync(self) -> None:
        """Ensure all languages have the same keys"""
        if len(self.languages) < 2:
            return

        all_keys = set()
        for keys in self.defined_keys.values():
            all_keys.update(keys)

        for key in all_keys:
            presence = {}
            for lang in self.languages:
                presence[lang] = key in self.defined_keys[lang]

            if not all(presence.values()):
                self.sync_issues[key] = presence

    def generate_report(self, output_format: str = 'console', summary_only: bool = False) -> Dict:
        """Generate validation report"""
        report = {
            'json_errors': [(str(f), msg) for f, msg in self.json_errors],
            'missing_keys': dict(self.missing_keys),
            'unused_keys': list(self.unused_keys),
            'sync_issues': self.sync_issues,
            'summary': {
                'total_keys_defined': len(set().union(*self.defined_keys.values())) if self.defined_keys else 0,
                'total_keys_used': len(self.used_keys),
                'json_errors_count': len(self.json_errors),
                'missing_count': len(self.missing_keys),
                'unused_count': len(self.unused_keys),
                'sync_issues_count': len(self.sync_issues),
                'status': 'PASSED' if not self.json_errors and not self.missing_keys and not self.sync_issues else 'FAILED'
            }
        }

        if output_format == 'json':
            print(json.dumps(report, indent=2, ensure_ascii=False))
        else:
            self._print_console_report(report, summary_only)

        return report

    def _print_console_report(self, report: Dict, summary_only: bool) -> None:
        """Print colored console report"""
        print(f"\n{Colors.BOLD}=== Localization Validation Report ==={Colors.END}\n")

        if not summary_only:
            # JSON Integrity Errors (highest priority)
            if self.json_errors:
                print(f"{Colors.RED}{Colors.BOLD}[ERROR] JSON Integrity Issues:{Colors.END}")
                for json_file, error_msg in self.json_errors:
                    print(f"  {Colors.RED}✗{Colors.END} {json_file}")
                    print(f"    {error_msg}")
                print()

            if self.missing_keys:
                print(f"{Colors.RED}{Colors.BOLD}[ERROR] Missing Keys (used but not defined):{Colors.END}")
                for key, locations in sorted(self.missing_keys.items()):
                    print(f"  {Colors.RED}✗{Colors.END} {key}")
                    for file_path, line_num in locations:
                        print(f"    Used in: {file_path}:{line_num}")
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
        print(f"  Unused keys: {Colors.YELLOW if summary['unused_count'] > 0 else Colors.GREEN}{summary['unused_count']}{Colors.END}")
        print(f"  Language sync issues: {Colors.RED if summary['sync_issues_count'] > 0 else Colors.GREEN}{summary['sync_issues_count']}{Colors.END}")

        status_color = Colors.GREEN if summary['status'] == 'PASSED' else Colors.RED
        print(f"  Status: {status_color}{Colors.BOLD}{summary['status']}{Colors.END}\n")


def main():
    parser = argparse.ArgumentParser(description='Validate localization keys in Monica Framework')
    parser.add_argument('--strict', action='store_true', help='Treat unused keys as errors')
    parser.add_argument('--json', action='store_true', help='Output results in JSON format')
    parser.add_argument('--summary', action='store_true', help='Show summary only')
    parser.add_argument('--languages', type=str, default='zh-CN,en-US', help='Comma-separated list of languages')
    parser.add_argument('--root', type=str, default='.', help='Repository root path')

    args = parser.parse_args()

    root_path = Path(args.root).resolve()
    languages = [lang.strip() for lang in args.languages.split(',')]

    if not root_path.exists():
        print(f"Error: Root path does not exist: {root_path}", file=sys.stderr)
        sys.exit(1)

    validator = LocalizationValidator(root_path, languages)

    try:
        # Step 1: Validate JSON integrity first
        print("Validating JSON integrity...")
        json_valid = validator.validate_json_integrity()

        if not json_valid:
            print(f"\n{Colors.RED}JSON integrity validation failed. Fix JSON errors before proceeding.{Colors.END}\n")
            output_format = 'json' if args.json else 'console'
            report = validator.generate_report(output_format, args.summary)
            sys.exit(1)

        # Step 2: Scan and validate keys
        validator.scan_razor_files()
        validator.load_json_keys()
        validator.validate_bidirectional()
        validator.validate_language_sync()

        output_format = 'json' if args.json else 'console'
        report = validator.generate_report(output_format, args.summary)

        has_errors = report['summary']['missing_count'] > 0 or report['summary']['sync_issues_count'] > 0
        has_warnings = report['summary']['unused_count'] > 0

        if has_errors:
            sys.exit(1)
        elif has_warnings and args.strict:
            sys.exit(2)
        else:
            sys.exit(0)

    except KeyboardInterrupt:
        print("\nValidation interrupted by user", file=sys.stderr)
        sys.exit(130)
    except Exception as e:
        print(f"Error: {e}", file=sys.stderr)
        sys.exit(1)


if __name__ == '__main__':
    main()
