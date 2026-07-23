#!/usr/bin/env python3
"""Regression fixtures for Mud CSS validator discovery semantics."""

import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPTS_DIRECTORY = Path(__file__).resolve().parents[1] / "scripts"
sys.path.insert(0, str(SCRIPTS_DIRECTORY))
SCRIPT_PATH = SCRIPTS_DIRECTORY / "validate_mud_css_variables.py"
SPEC = importlib.util.spec_from_file_location("validate_mud_css_variables", SCRIPT_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Could not load validator from {SCRIPT_PATH}")
VALIDATOR = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = VALIDATOR
SPEC.loader.exec_module(VALIDATOR)


class MudCssVariableDiscoveryTests(unittest.TestCase):
    def test_explicit_root_inside_parent_tmp_is_scanned(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory) / ".tmp" / "explicit-root"
            root.mkdir(parents=True)
            (root / "component.razor.css").write_text(
                "color: var(--mud-palette-primary);",
                encoding="utf-8",
            )
            stale_directory = root / ".tmp"
            stale_directory.mkdir()
            (stale_directory / "stale.css").write_text(
                "color: var(--mud-palette-does-not-exist);",
                encoding="utf-8",
            )

            usages, counts = VALIDATOR.collect_usages(root)

            self.assertEqual({"css": 1, "razor": 0}, counts)
            self.assertEqual(["--mud-palette-primary"], list(usages))

    def test_zero_eligible_files_cannot_report_success(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            report = VALIDATOR.build_report(
                root=root,
                variables_file=root / "variables.json",
                scanned_counts={"css": 0, "razor": 0},
                usages={},
                unknown_usages={},
                fix_counts={},
                fixed_files=0,
            )

            self.assertEqual("FAILED", report["summary"]["status"])
            self.assertEqual(1, report["summary"]["discovery_errors_count"])
            self.assertIn("No eligible CSS or Razor files", report["discovery_errors"][0])


if __name__ == "__main__":
    unittest.main()
