from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from io import StringIO
from pathlib import Path
from unittest import mock


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]


def load_sync_script():
    script_path = REPOSITORY_ROOT / "eng" / "sync-template-version.py"
    spec = importlib.util.spec_from_file_location("sync_template_version", script_path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules["sync_template_version"] = module
    spec.loader.exec_module(module)
    return module


sync_template_version = load_sync_script()


class TemplateVersionGuardTests(unittest.TestCase):
    def write_fixture(self, root: Path, *, repository_version: str) -> dict[str, Path]:
        paths = {
            "root": root,
            "props": root / "Directory.Build.props",
            "project": root / "MonicaStarter.csproj",
            "readme": root / "README.md",
            "configuration": root / "template.json",
        }
        paths["props"].write_text(
            f"<Project><PropertyGroup><Version>{repository_version}</Version>"
            "</PropertyGroup></Project>\n",
            encoding="utf-8",
        )
        paths["project"].write_text(
            "<Project><PropertyGroup><MonicaVersion>1.2.3</MonicaVersion>"
            "</PropertyGroup></Project>\n",
            encoding="utf-8",
        )
        paths["readme"].write_text(
            "`dotnet new install Monica.Templates@1.2.3`\n",
            encoding="utf-8",
        )
        paths["configuration"].write_text(
            json.dumps(
                {
                    "symbols": {
                        "frameworkVersion": {
                            "defaultValue": "1.2.3",
                            "replaces": "1.2.3",
                        }
                    }
                }
            ),
            encoding="utf-8",
        )
        return paths

    def patched_paths(self, paths: dict[str, Path]):
        return mock.patch.multiple(
            sync_template_version,
            ROOT=paths["root"],
            DIRECTORY_BUILD_PROPS=paths["props"],
            TEMPLATE_PROJECT=paths["project"],
            TEMPLATE_README=paths["readme"],
            TEMPLATE_CONFIGURATION=paths["configuration"],
        )

    def test_explicit_release_version_matches_source_and_templates(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            paths = self.write_fixture(
                Path(temporary_directory),
                repository_version="1.2.3",
            )
            with self.patched_paths(paths), redirect_stdout(StringIO()):
                sync_template_version.check_versions("1.2.3", "--version")

    def test_explicit_release_version_rejects_source_version_mismatch(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            paths = self.write_fixture(
                Path(temporary_directory),
                repository_version="1.2.2",
            )
            with (
                self.patched_paths(paths),
                redirect_stderr(StringIO()),
                self.assertRaises(SystemExit),
            ):
                sync_template_version.check_versions("1.2.3", "--version")


if __name__ == "__main__":
    unittest.main()
