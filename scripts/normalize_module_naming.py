#!/usr/bin/env python3
"""
Monica Module Naming Normalization Script

This script normalizes module file naming in Monica projects:
1. Renames single-file modules (e.g., Clock.cs -> ModuleClock.cs)
2. Merges multi-file modules into single files (e.g., ModuleAuth*.cs -> ModuleAuth.cs)

Usage:
    python normalize_module_naming.py --dry-run          # Preview changes
    python normalize_module_naming.py                    # Apply changes
    python normalize_module_naming.py --project Monica.Core  # Process single project
"""

import argparse
import re
from pathlib import Path
from dataclasses import dataclass, field
from typing import Optional


@dataclass
class ModuleInfo:
    """Information about a detected module."""
    name: str
    pattern: str  # 'single_correct', 'single_incorrect', 'multi_file'
    files: list[Path] = field(default_factory=list)


@dataclass
class ParsedFile:
    """Parsed C# file content."""
    usings: set[str]
    namespace: Optional[str]
    type_definitions: list[str]  # Each item is a complete type definition with docs


def find_monica_projects(root: Path) -> list[Path]:
    """Find all Monica.* project directories."""
    projects = []
    for item in root.iterdir():
        if item.is_dir() and item.name.startswith('Monica.'):
            projects.append(item)
    # Also check for nested projects (like ServiceInvocation inside RegisterCentre)
    for project in list(projects):
        for subdir in project.iterdir():
            if subdir.is_dir() and (subdir / 'Modules').exists():
                if subdir not in projects:
                    projects.append(subdir)
    return sorted(projects, key=lambda p: p.name)


def extract_module_name_from_stem(stem: str) -> tuple[Optional[str], str]:
    """
    Extract module name from file stem.
    Returns (module_name, suffix_type) where suffix_type is one of:
    'BuilderExtensions', 'RegistrationExtensions', 'Option', 'Module', or 'Unknown'
    """
    if stem.startswith('Module'):
        rest = stem[6:]  # Remove 'Module' prefix

        if rest.endswith('BuilderExtensions'):
            return rest[:-17], 'BuilderExtensions'
        elif rest.endswith('RegistrationExtensions'):
            return rest.removesuffix('RegistrationExtensions'), 'RegistrationExtensions'
        elif rest.endswith('Option'):
            return rest[:-6], 'Option'
        else:
            return rest, 'Module'

    return None, 'Unknown'


def extract_module_name_from_content(content: str) -> Optional[str]:
    """
    Extract module name by scanning file content for class definitions.
    Looks for patterns like 'class Module{Name}' or 'class Module{Name}RegistrationExtensions'.
    """
    # Match class definitions with Module prefix
    pattern = r'(?:public|internal)\s+(?:static\s+)?(?:sealed\s+)?(?:abstract\s+)?(?:partial\s+)?class\s+Module(\w+?)(?:BuilderExtensions|RegistrationExtensions|Option)?\s*[:({\n]'
    match = re.search(pattern, content)
    if match:
        name = match.group(1)
        # Clean up - remove trailing suffixes if captured
        for suffix in ['BuilderExtensions', 'RegistrationExtensions', 'Option']:
            if name.endswith(suffix):
                name = name[:-len(suffix)]
        return name
    return None


def parse_cs_file(file_path: Path) -> ParsedFile:
    """Parse a C# file to extract usings, namespace, and type definitions."""
    try:
        content = file_path.read_text(encoding='utf-8-sig')
    except UnicodeDecodeError:
        content = file_path.read_text(encoding='utf-8')

    usings = set()
    namespace = None

    # Extract using statements
    for match in re.finditer(r'^using\s+([^;]+);', content, re.MULTILINE):
        usings.add(match.group(1).strip())

    # Extract namespace (file-scoped)
    ns_match = re.search(r'^namespace\s+([^;{]+);', content, re.MULTILINE)
    if ns_match:
        namespace = ns_match.group(1).strip()

    # Extract type definitions
    type_definitions = extract_type_definitions(content)

    return ParsedFile(usings=usings, namespace=namespace, type_definitions=type_definitions)


def extract_type_definitions(content: str) -> list[str]:
    """
    Extract complete type definitions from C# content.
    Handles classes, interfaces, structs, enums, records with their docs and attributes.
    """
    # Remove using statements and namespace declaration first
    lines = content.split('\n')

    # Find where the actual code starts (after usings and namespace)
    code_start_idx = 0
    for i, line in enumerate(lines):
        stripped = line.strip()
        if stripped.startswith('namespace ') and stripped.endswith(';'):
            code_start_idx = i + 1
            break
        elif stripped.startswith('namespace ') and not stripped.endswith(';'):
            # Block-scoped namespace - find the opening brace
            code_start_idx = i + 1
            break

    # Skip empty lines after namespace
    while code_start_idx < len(lines) and not lines[code_start_idx].strip():
        code_start_idx += 1

    # Now extract type definitions
    types = []
    type_start_pattern = re.compile(
        r'^(\s*)(public|internal|private|protected|file)?\s*(static\s+)?(sealed\s+)?(abstract\s+)?(partial\s+)?'
        r'(class|interface|struct|enum|record)\s+'
    )

    i = code_start_idx
    while i < len(lines):
        line = lines[i]
        stripped = line.strip()

        # Skip empty lines
        if not stripped:
            i += 1
            continue

        # Check if this could be start of a type (including docs/attributes)
        if stripped.startswith('///') or stripped.startswith('[') or type_start_pattern.match(line):
            # Collect doc comments and attributes
            type_lines = []

            # Collect leading doc comments
            while i < len(lines) and lines[i].strip().startswith('///'):
                type_lines.append(lines[i])
                i += 1

            # Collect attributes
            while i < len(lines) and lines[i].strip().startswith('['):
                type_lines.append(lines[i])
                i += 1
                # Handle multi-line attributes
                while i < len(lines) and not lines[i-1].strip().endswith(']'):
                    type_lines.append(lines[i])
                    i += 1

            # Now check if we have a type definition
            if i < len(lines) and type_start_pattern.match(lines[i]):
                type_lines.append(lines[i])
                brace_count = lines[i].count('{') - lines[i].count('}')
                i += 1

                # If no opening brace yet, keep reading until we find it
                while i < len(lines) and brace_count == 0:
                    line = lines[i]
                    type_lines.append(line)
                    brace_count += line.count('{') - line.count('}')
                    i += 1
                    # If we found opening brace, continue to collect the body
                    if brace_count > 0:
                        break
                    # If line is empty or just whitespace, continue
                    if not line.strip():
                        continue
                    # If we hit another type definition start, we went too far - break
                    if type_start_pattern.match(line):
                        i -= 1  # Back up
                        type_lines.pop()  # Remove this line
                        break

                # Collect until braces balance
                while i < len(lines) and brace_count > 0:
                    type_lines.append(lines[i])
                    brace_count += lines[i].count('{') - lines[i].count('}')
                    i += 1

                types.append('\n'.join(type_lines))
            else:
                i += 1
        else:
            # Handle standalone comments that aren't part of a type
            if stripped.startswith('//') and not stripped.startswith('///'):
                i += 1
                continue
            # Handle #region, #endregion
            if stripped.startswith('#'):
                i += 1
                continue
            i += 1

    return types


def sort_usings(usings: set[str]) -> list[str]:
    """Sort using statements: System first, then others alphabetically."""
    system_usings = sorted([u for u in usings if u.startswith('System')])
    microsoft_usings = sorted([u for u in usings if u.startswith('Microsoft')])
    other_usings = sorted([u for u in usings if not u.startswith('System') and not u.startswith('Microsoft')])
    return system_usings + microsoft_usings + other_usings


def classify_type_definition(type_def: str, module_name: str) -> str:
    """
    Classify a type definition into one of: 'BuilderExtensions', 'RegistrationExtensions', 'Module', 'Option', 'Auxiliary'
    """
    # Check for specific class names
    if f'class Module{module_name}BuilderExtensions' in type_def:
        return 'BuilderExtensions'
    if f'static class Module{module_name}BuilderExtensions' in type_def:
        return 'BuilderExtensions'
    if re.search(rf'class\s+Module{module_name}RegistrationExtensions\s*[:({{]', type_def):
        return 'RegistrationExtensions'
    if re.search(rf'class\s+Module{module_name}Option\s*[:(]', type_def):
        return 'Option'
    if re.search(rf'class\s+Module{module_name}\s*[:(]', type_def):
        return 'Module'

    return 'Auxiliary'


def merge_module_files(files: list[Path], module_name: str) -> str:
    """
    Merge multiple module files into a single file.
    Order: BuilderExtensions -> Module -> RegistrationExtensions -> Option -> Auxiliary
    """
    all_usings = set()
    namespace = None
    categorized_types: dict[str, list[str]] = {
        'BuilderExtensions': [],
        'Module': [],
        'RegistrationExtensions': [],
        'Option': [],
        'Auxiliary': []
    }

    for file in files:
        parsed = parse_cs_file(file)
        all_usings.update(parsed.usings)
        if parsed.namespace:
            namespace = parsed.namespace

        for type_def in parsed.type_definitions:
            category = classify_type_definition(type_def, module_name)
            categorized_types[category].append(type_def)

    # Build merged content
    result_lines = []

    # Add sorted usings
    sorted_usings = sort_usings(all_usings)
    for using in sorted_usings:
        result_lines.append(f'using {using};')

    result_lines.append('')
    result_lines.append(f'namespace {namespace};')
    result_lines.append('')

    # Add types in order
    order = ['BuilderExtensions', 'Module', 'RegistrationExtensions', 'Option', 'Auxiliary']
    for category in order:
        for type_def in categorized_types[category]:
            result_lines.append(type_def)
            result_lines.append('')

    return '\n'.join(result_lines)


def detect_modules_in_directory(modules_dir: Path, verbose: bool = False) -> dict[str, ModuleInfo]:
    """
    Detect all modules in a Modules directory and classify their patterns.
    """
    cs_files = list(modules_dir.glob('*.cs'))

    # Group files by module name
    module_files: dict[str, list[tuple[Path, str]]] = {}  # module_name -> [(file, suffix_type)]

    for file in cs_files:
        stem = file.stem
        module_name, suffix_type = extract_module_name_from_stem(stem)

        if module_name is None:
            # Try to extract from content
            try:
                content = file.read_text(encoding='utf-8-sig')
            except:
                content = file.read_text(encoding='utf-8')
            module_name = extract_module_name_from_content(content)
            if module_name:
                suffix_type = 'Module'  # Assume it's the main module file

        if module_name:
            if module_name not in module_files:
                module_files[module_name] = []
            module_files[module_name].append((file, suffix_type))

    # Classify each module
    modules: dict[str, ModuleInfo] = {}

    for module_name, file_info_list in module_files.items():
        files = [f for f, _ in file_info_list]
        suffixes = {s for _, s in file_info_list}

        if len(files) == 1:
            file = files[0]
            if file.stem == f'Module{module_name}':
                pattern = 'single_correct'
            else:
                pattern = 'single_incorrect'
        else:
            # Multiple files - check if it's a standard 4-file pattern
            pattern = 'multi_file'

        modules[module_name] = ModuleInfo(name=module_name, pattern=pattern, files=files)

    return modules


def process_module(module: ModuleInfo, modules_dir: Path, dry_run: bool, verbose: bool) -> bool:
    """
    Process a single module based on its pattern.
    Returns True if successful.
    """
    if module.pattern == 'single_correct':
        if verbose:
            print(f"  [OK] Module{module.name} - already correct")
        return True

    elif module.pattern == 'single_incorrect':
        old_file = module.files[0]
        new_name = f'Module{module.name}.cs'
        new_file = modules_dir / new_name

        print(f"  [RENAME] {old_file.name} -> {new_name}")

        if not dry_run:
            old_file.rename(new_file)
        return True

    elif module.pattern == 'multi_file':
        target_file = modules_dir / f'Module{module.name}.cs'
        files_to_delete = [f for f in module.files if f != target_file]

        print(f"  [MERGE] {len(module.files)} files -> Module{module.name}.cs")
        for f in files_to_delete:
            print(f"    - Delete: {f.name}")

        if not dry_run:
            merged_content = merge_module_files(module.files, module.name)
            target_file.write_text(merged_content, encoding='utf-8')
            for f in files_to_delete:
                f.unlink()
        return True

    return False


def main():
    parser = argparse.ArgumentParser(
        description='Normalize Monica module naming patterns',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__
    )
    parser.add_argument('--root', type=str, default='.',
                        help='Monica root directory (default: current directory)')
    parser.add_argument('--dry-run', action='store_true',
                        help='Preview changes without executing')
    parser.add_argument('--verbose', '-v', action='store_true',
                        help='Show detailed output including skipped files')
    parser.add_argument('--project', type=str,
                        help='Process specific project only (e.g., Monica.Core)')

    args = parser.parse_args()
    root = Path(args.root).resolve()

    print("Normalizing Monica module naming patterns...")
    print(f"Root: {root}")
    print(f"Mode: {'DRY-RUN' if args.dry_run else 'EXECUTE'}")
    print()

    projects = find_monica_projects(root)

    stats = {
        'projects_scanned': 0,
        'renames': 0,
        'merges': 0,
        'already_correct': 0,
        'errors': 0
    }

    for project in projects:
        if args.project and project.name != args.project:
            continue

        modules_dir = project / 'Modules'
        if not modules_dir.exists():
            continue

        print(f"Processing: {project.name}")
        stats['projects_scanned'] += 1

        modules = detect_modules_in_directory(modules_dir, args.verbose)

        for module_name, module_info in sorted(modules.items()):
            try:
                success = process_module(module_info, modules_dir, args.dry_run, args.verbose)
                if success:
                    if module_info.pattern == 'single_correct':
                        stats['already_correct'] += 1
                    elif module_info.pattern == 'single_incorrect':
                        stats['renames'] += 1
                    elif module_info.pattern == 'multi_file':
                        stats['merges'] += 1
                else:
                    stats['errors'] += 1
            except Exception as e:
                print(f"  [ERROR] Failed to process Module{module_name}: {e}")
                stats['errors'] += 1

        print()

    # Print summary
    print("=" * 50)
    print("Summary:")
    print(f"  Projects scanned: {stats['projects_scanned']}")
    print(f"  Renames: {stats['renames']}")
    print(f"  Merges: {stats['merges']}")
    print(f"  Already correct: {stats['already_correct']}")
    if stats['errors'] > 0:
        print(f"  Errors: {stats['errors']}")


if __name__ == '__main__':
    main()
