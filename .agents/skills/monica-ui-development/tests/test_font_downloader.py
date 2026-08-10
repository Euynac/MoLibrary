#!/usr/bin/env python3
"""Regression tests for local font metadata and variable-weight selection."""

import importlib.util
import io
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest.mock import Mock


SCRIPTS_DIRECTORY = Path(__file__).resolve().parents[1] / "scripts"
SCRIPT_PATH = SCRIPTS_DIRECTORY / "font_downloader.py"
SPEC = importlib.util.spec_from_file_location("font_downloader", SCRIPT_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Could not load font downloader from {SCRIPT_PATH}")
FONT_DOWNLOADER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = FONT_DOWNLOADER
SPEC.loader.exec_module(FONT_DOWNLOADER)


VARIABLE_FONT_CSS = """
/* latin */
@font-face {
  font-family: 'Demo Variable';
  font-style: normal;
  font-weight: 100 700;
  font-display: swap;
  src: url(https://example.test/demo-latin.woff2) format('woff2');
  unicode-range: U+0000-00FF, U+0131;
}
/* cyrillic */
@font-face {
  font-family: 'Demo Variable';
  font-style: normal;
  font-weight: 100 700;
  font-display: swap;
  src: url('https://example.test/demo-cyrillic.woff2') format('woff2');
  unicode-range: U+0400-04FF;
}
"""


class FontDownloaderTests(unittest.TestCase):
    def setUp(self) -> None:
        self.downloader = FONT_DOWNLOADER.UniversalFontDownloader()

    def test_variable_range_and_unicode_metadata_are_preserved(self) -> None:
        fonts = self.downloader.extract_font_info_from_css(VARIABLE_FONT_CSS)

        self.assertEqual(2, len(fonts))
        self.assertEqual("100 700", fonts[0]["weight"])
        self.assertEqual((100, 700), (fonts[0]["weight_min"], fonts[0]["weight_max"]))
        self.assertEqual("U+0000-00FF, U+0131", fonts[0]["unicode_range"])
        self.assertEqual(
            "DemoVariable-Variable100To700-Latin.woff2",
            fonts[0]["filename"],
        )
        self.assertEqual(
            "DemoVariable-Variable100To700-Cyrillic.woff2",
            fonts[1]["filename"],
        )

    def test_weight_filter_selects_a_variable_face_by_inclusion(self) -> None:
        fonts = self.downloader.extract_font_info_from_css(VARIABLE_FONT_CSS)

        self.assertTrue(self.downloader.matches_weight_filter(fonts[0], [400]))
        self.assertTrue(self.downloader.matches_weight_filter(fonts[0], [100, 900]))
        self.assertFalse(self.downloader.matches_weight_filter(fonts[0], [800]))

        self.downloader.download_css = Mock(return_value=VARIABLE_FONT_CSS)
        self.downloader.download_font_file = Mock(return_value=True)
        with redirect_stdout(io.StringIO()):
            selected = self.downloader.process_css_url(
                "https://example.test/font.css",
                filter_weights=[400],
                filter_subsets=["latin"],
            )

        self.assertEqual([fonts[0]["filename"]], [font["filename"] for font in selected])
        self.downloader.download_font_file.assert_called_once()

    def test_manifest_uses_local_files_and_exact_weight_ranges(self) -> None:
        fonts = self.downloader.extract_font_info_from_css(VARIABLE_FONT_CSS)

        with tempfile.TemporaryDirectory() as temporary_directory:
            with redirect_stdout(io.StringIO()):
                manifest_path = self.downloader.write_css_manifest(
                    fonts,
                    temporary_directory,
                )
            manifest = manifest_path.read_text(encoding="utf-8")

        self.assertEqual("font-faces.css", manifest_path.name)
        self.assertEqual(2, manifest.count("font-weight: 100 700;"))
        self.assertIn("unicode-range: U+0000-00FF, U+0131;", manifest)
        self.assertIn("unicode-range: U+0400-04FF;", manifest)
        self.assertIn(
            "url('./DemoVariable-Variable100To700-Latin.woff2')",
            manifest,
        )
        self.assertNotIn("https://example.test", manifest)

    def test_weight_argument_rejects_invalid_values(self) -> None:
        self.assertEqual([400, 500, 700], FONT_DOWNLOADER.parse_weight_list("400,500,700"))
        with self.assertRaises(FONT_DOWNLOADER.argparse.ArgumentTypeError):
            FONT_DOWNLOADER.parse_weight_list("normal")
        with self.assertRaises(FONT_DOWNLOADER.argparse.ArgumentTypeError):
            FONT_DOWNLOADER.parse_weight_list("0")


if __name__ == "__main__":
    unittest.main()
