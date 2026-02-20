#!/usr/bin/env python3
"""
Localization Validation Script for Monica Framework

This script validates localization keys bidirectionally:
1. Missing keys: Keys used in Razor files but not defined in JSON
2. Unused keys: Keys defined in JSON but never used in Razor files
3. Language sync: Keys present in one language but not another

Usage:
    python validate_localization.py [options]

Options:
    --strict        Treat unused keys as errors (exit code 1)
    --json          Output results in JSON format
    --summary       Show summary only
    --languages     Comma-separated list of languages (default: zh-CN,en-US)
    --root          Repository root path (default: current directory)

Exit Codes:
    0: All validations passed
    1: Missing keys or language sync issues (critical errors)
    2: Unused keys in strict mode (warnings as errors)
"""

import json
import re
import sys
import argparse
from pathlib import Path
from typing import Dict, List, Set, Tuple
from collections import defaultdict


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
        self.ui_projects = [
            'Monica.UI',
            'Monica.Framework.UI',
            'Monica.Configuration.UI',
            'Monica.JobScheduler.UI',
            'Monica.StateStore.UI',
            'Monica.AI.UI'
        ]
        self.localization_base = root_path / 'Monica.UI' / 'Localization' / 'SharedResource'
        
        # Results
        self.used_keys: Dict[str, List[Tuple[str, int]]] = defaultdict(list)  # key -> [(file, line), ...]
        self.defined_keys: Dict[str, Set[str]] = {}  # language -> set of keys
        self.missing_keys: Dict[str, List[Tuple[str, int]]] = defaultdict(list)
        self.unused_keys: Set[str] = set()
        self.sync_issues: Dict[str, Dict[str, bool]] = {}  # key -> {lang: exists}

    def scan_razor_files(self) -> None:
        """Extract @L["..."] and L["..."] patterns from all *.UI projects"""
        # Pattern to match @L["key"] or L["key"] (with optional @ prefix)
        pattern = re.compile(r'@?L\["([^"]+)"(?:\s*,\s*[^\]]+)?\]')
        
        for project in self.ui_projects:
            project_path = self.root_path / project
            if not project_path.exists():
                continue
            
            # Find all .razor files
            for razor_file in project_path.rglob('*.razor'):
                try:
                    with open(razor_file, 'r', encoding='utf-8') as f:
                        lines = f.readlines()
                        
                    for line_num, line in enumerate(lines, 1):
                        # Skip Razor comments
                        if '@*' in line or '*@' in line:
                            continue
                        
                        # Find all localization key usage
                        matches = pattern.findall(line)
                        for key in matches:
                            relative_path = razor_file.relative_to(self.root_path)
                            self.used_keys[key].append((str(relative_path), line_num))
                
                except Exception as e:
                    print(f"Warning: Could not read {razor_file}: {e}", file=sys.stderr)

    def load_json_keys(self) -> None:
        """Load and flatten JSON keys from language files"""
        for lang in self.languages:
            json_file = self.localization_base / f'{lang}.json'
            
            if not json_file.exists():
                print(f"Error: Localization file not found: {json_file}", file=sys.stderr)
                sys.exit(1)
            
            try:
                with open(json_file, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                
                # Flatten hierarchical structure
                keys = self._flatten_json(data.get('texts', {}))
                self.defined_keys[lang] = keys
            
            except json.JSONDecodeError as e:
                print(f"Error: Invalid JSON in {json_file}: {e}", file=sys.stderr)
                sys.exit(1)
            except Exception as e:
                print(f"Error: Could not load {json_file}: {e}", file=sys.stderr)
                sys.exit(1)

    def _flatten_json(self, obj: dict, parent_key: str = '') -> Set[str]:
        """Recursively flatten JSON structure to colon-separated keys"""
        keys = set()
        
        for key, value in obj.items():
            full_key = f"{parent_key}:{key}" if parent_key else key
            
            if isinstance(value, dict):
                # Recurse into nested objects
                keys.update(self._flatten_json(value, full_key))
            else:
                # Leaf node - this is a translatable string
                keys.add(full_key)
        
        return keys

    def validate_bidirectional(self) -> None:
        """Check used vs defined keys"""
        # Get all defined keys (union of all languages)
        all_defined = set()
        for keys in self.defined_keys.values():
            all_defined.update(keys)
        
        # Find missing keys (used but not defined)
        for key, locations in self.used_keys.items():
            if key not in all_defined:
                self.missing_keys[key] = locations
        
        # Find unused keys (defined but not used)
        all_used = set(self.used_keys.keys())
        self.unused_keys = all_defined - all_used

    def validate_language_sync(self) -> None:
        """Ensure all languages have the same keys"""
        if len(self.languages) < 2:
            return
        
        # Get all unique keys across all languages
        all_keys = set()
        for keys in self.defined_keys.values():
            all_keys.update(keys)
        
        # Check each key's presence in each language
        for key in all_keys:
            presence = {}
            for lang in self.languages:
                presence[lang] = key in self.defined_keys[lang]
            
            # If not all languages have this key, it's a sync issue
            if not all(presence.values()):
                self.sync_issues[key] = presence

    def generate_report(self, output_format: str = 'console', summary_only: bool = False) -> Dict:
        """Generate validation report"""
        report = {
            'missing_keys': dict(self.missing_keys),
            'unused_keys': list(self.unused_keys),
            'sync_issues': self.sync_issues,
            'summary': {
                'total_keys_defined': len(set().union(*self.defined_keys.values())),
                'total_keys_used': len(self.used_keys),
                'missing_count': len(self.missing_keys),
                'unused_count': len(self.unused_keys),
                'sync_issues_count': len(self.sync_issues),
                'status': 'PASSED' if not self.missing_keys and not self.sync_issues else 'FAILED'
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
            # Missing keys (ERROR)
            if self.missing_keys:
                print(f"{Colors.RED}{Colors.BOLD}[ERROR] Missing Keys (used but not defined):{Colors.END}")
                for key, locations in sorted(self.missing_keys.items()):
                    print(f"  {Colors.RED}✗{Colors.END} {key}")
                    for file_path, line_num in locations:
                        print(f"    Used in: {file_path}:{line_num}")
                print()
            
            # Unused keys (WARNING)
            if self.unused_keys:
                print(f"{Colors.YELLOW}{Colors.BOLD}[WARNING] Unused Keys (defined but not used):{Colors.END}")
                for key in sorted(self.unused_keys):
                    print(f"  {Colors.YELLOW}⚠{Colors.END} {key}")
                    langs = [lang for lang, keys in self.defined_keys.items() if key in keys]
                    print(f"    Defined in: {', '.join(langs)}")
                print()
            
            # Language sync issues (ERROR)
            if self.sync_issues:
                print(f"{Colors.RED}{Colors.BOLD}[ERROR] Language Sync Issues:{Colors.END}")
                for key, presence in sorted(self.sync_issues.items()):
                    print(f"  {Colors.RED}✗{Colors.END} {key}")
                    present_in = [lang for lang, exists in presence.items() if exists]
                    missing_in = [lang for lang, exists in presence.items() if not exists]
                    print(f"    Present in: {', '.join(present_in)}")
                    print(f"    Missing in: {', '.join(missing_in)}")
                print()
        
        # Summary
        summary = report['summary']
        print(f"{Colors.BOLD}Summary:{Colors.END}")
        print(f"  Total keys defined: {summary['total_keys_defined']}")
        print(f"  Total keys used: {summary['total_keys_used']}")
        print(f"  Missing keys: {Colors.RED if summary['missing_count'] > 0 else Colors.GREEN}{summary['missing_count']}{Colors.END}")
        print(f"  Unused keys: {Colors.YELLOW if summary['unused_count'] > 0 else Colors.GREEN}{summary['unused_count']}{Colors.END}")
        print(f"  Language sync issues: {Colors.RED if summary['sync_issues_count'] > 0 else Colors.GREEN}{summary['sync_issues_count']}{Colors.END}")
        
        status_color = Colors.GREEN if summary['status'] == 'PASSED' else Colors.RED
        print(f"  Status: {status_color}{Colors.BOLD}{summary['status']}{Colors.END}\n")


def main():
    parser = argparse.ArgumentParser(
        description='Validate localization keys in Monica Framework',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__
    )
    parser.add_argument('--strict', action='store_true',
                        help='Treat unused keys as errors (exit code 1)')
    parser.add_argument('--json', action='store_true',
                        help='Output results in JSON format')
    parser.add_argument('--summary', action='store_true',
                        help='Show summary only')
    parser.add_argument('--languages', type=str, default='zh-CN,en-US',
                        help='Comma-separated list of languages (default: zh-CN,en-US)')
    parser.add_argument('--root', type=str, default='.',
                        help='Repository root path (default: current directory)')
    
    args = parser.parse_args()
    
    # Parse arguments
    root_path = Path(args.root).resolve()
    languages = [lang.strip() for lang in args.languages.split(',')]
    
    # Validate root path
    if not root_path.exists():
        print(f"Error: Root path does not exist: {root_path}", file=sys.stderr)
        sys.exit(1)
    
    # Create validator
    validator = LocalizationValidator(root_path, languages)
    
    # Run validation
    try:
        validator.scan_razor_files()
        validator.load_json_keys()
        validator.validate_bidirectional()
        validator.validate_language_sync()
        
        # Generate report
        output_format = 'json' if args.json else 'console'
        report = validator.generate_report(output_format, args.summary)
        
        # Determine exit code
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
