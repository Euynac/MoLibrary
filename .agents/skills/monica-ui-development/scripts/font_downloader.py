#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Download Google Fonts WOFF2 files with their runtime CSS metadata.

Examples:
  python font_downloader.py "https://fonts.googleapis.com/css2?family=Roboto:wght@400;500;700&display=swap"
  python font_downloader.py "CSS_URL" --weights 400,500 --subsets latin,latin-ext
  python font_downloader.py --download-all

The downloader keeps unicode-range shards separate and writes a companion
``font-faces.css`` manifest beside the downloaded files. Variable font ranges
remain variable in that manifest, and a requested weight selects a variable
face whenever the requested value falls inside its declared range.
"""

import argparse
import codecs
import os
import re
import sys
from pathlib import Path

import requests


PROJECT_FONT_URLS = (
    "https://fonts.googleapis.com/css2?family=Noto+Serif+SC:wght@300;400;500;600;700&display=swap",
    "https://fonts.googleapis.com/css2?family=Comfortaa:wght@300;400;500;600;700&display=swap",
    "https://fonts.googleapis.com/css2?family=Nunito:wght@300;400;500;600;700&display=swap",
    "https://fonts.googleapis.com/css2?family=Source+Sans+Pro:wght@300;400;500;600;700&display=swap",
    "https://fonts.googleapis.com/css2?family=Open+Sans:wght@300;400;500;600;700&display=swap",
    "https://fonts.googleapis.com/css2?family=Courier+Prime:wght@400;700&display=swap",
    "https://fonts.googleapis.com/css2?family=Playfair+Display:wght@400;500;600;700&display=swap",
    "https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@400;500;600&display=swap",
    "https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap",
)
DEFAULT_PROJECT_WEIGHTS = (400, 500, 600)
DEFAULT_MANIFEST_NAME = "font-faces.css"


if sys.platform.startswith("win"):
    sys.stdout = codecs.getwriter("utf-8")(sys.stdout.buffer, "strict")
    sys.stderr = codecs.getwriter("utf-8")(sys.stderr.buffer, "strict")


def parse_weight_list(value):
    """Parse and validate a comma-separated list of CSS weight values."""
    weights = []
    for item in value.split(","):
        candidate = item.strip()
        if not candidate:
            continue
        try:
            weight = int(candidate)
        except ValueError as exc:
            raise argparse.ArgumentTypeError(
                "weights must be comma-separated integers"
            ) from exc
        if not 1 <= weight <= 1000:
            raise argparse.ArgumentTypeError("font weights must be between 1 and 1000")
        weights.append(weight)

    if not weights:
        raise argparse.ArgumentTypeError("at least one font weight is required")
    return weights


class UniversalFontDownloader:
    """Download selected font faces and generate matching local CSS."""

    def __init__(self):
        self.session = requests.Session()
        self.session.headers.update({
            "User-Agent": (
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
                "AppleWebKit/537.36 (KHTML, like Gecko) "
                "Chrome/91.0.4472.124 Safari/537.36"
            )
        })
        self.project_fonts = PROJECT_FONT_URLS

    def download_css(self, url):
        """Download a Google Fonts stylesheet and return its content."""
        try:
            print(f"[INFO] Fetching CSS: {url}")
            response = self.session.get(url, timeout=30)
            response.raise_for_status()
            return response.text
        except Exception as exc:
            print(f"[ERROR] CSS download failed: {exc}")
            return None

    def extract_font_info_from_css(self, css_content):
        """Extract local-file and CSS metadata from every WOFF2 font face."""
        fonts = []

        # Google Fonts emits one @font-face per unicode range. Preserve the
        # preceding subset label so distinct ranges never overwrite each other.
        font_face_pattern = r"(?:/\*\s*([^*]+?)\s*\*/\s*)?@font-face\s*\{([^}]+)\}"

        for match in re.finditer(font_face_pattern, css_content, re.DOTALL | re.IGNORECASE):
            subset = self.normalize_subset_name(match.group(1))
            font_face_content = match.group(2)

            family_match = re.search(
                r"font-family\s*:\s*['\"]([^'\"]+)['\"]",
                font_face_content,
                re.IGNORECASE,
            )
            if not family_match:
                continue
            family_name = family_match.group(1)

            weight_min, weight_max = self.parse_weight_range(font_face_content)
            weight = self.format_weight_range(weight_min, weight_max)
            style = self.extract_descriptor(font_face_content, "font-style") or "normal"
            font_display = self.extract_descriptor(font_face_content, "font-display") or "swap"
            unicode_range = self.extract_descriptor(font_face_content, "unicode-range")

            url_match = re.search(
                r"url\(\s*['\"]?(https://[^)'\"\s]+\.woff2(?:\?[^)'\"\s]*)?)['\"]?\s*\)",
                font_face_content,
                re.IGNORECASE,
            )
            if not url_match:
                continue

            style_suffix = "" if style == "normal" else f"-{self.filename_component(style)}"
            weight_name = self.get_weight_name(weight_min, weight_max)
            base_filename = (
                f"{self.filename_component(family_name)}-{weight_name}{style_suffix}"
            )
            fonts.append({
                "family": family_name,
                "weight": weight,
                "weight_min": weight_min,
                "weight_max": weight_max,
                "style": style,
                "font_display": font_display,
                "unicode_range": unicode_range,
                "subset": subset,
                "url": url_match.group(1),
                "base_filename": base_filename,
            })

        self.assign_collision_safe_filenames(fonts)
        return fonts

    @staticmethod
    def extract_descriptor(font_face_content, descriptor):
        """Return a normalized CSS descriptor value without its semicolon."""
        match = re.search(
            rf"{re.escape(descriptor)}\s*:\s*([^;]+)",
            font_face_content,
            re.IGNORECASE,
        )
        return " ".join(match.group(1).split()) if match else None

    @staticmethod
    def parse_weight_range(font_face_content):
        """Return the inclusive minimum and maximum CSS font-weight values."""
        match = re.search(
            r"font-weight\s*:\s*(\d{1,4})(?:\s+(\d{1,4}))?\s*(?:;|$)",
            font_face_content,
            re.IGNORECASE,
        )
        if not match:
            return 400, 400

        weight_min = int(match.group(1))
        weight_max = int(match.group(2) or match.group(1))
        if not 1 <= weight_min <= weight_max <= 1000:
            raise ValueError(
                f"Invalid font-weight range: {weight_min} {weight_max}"
            )
        return weight_min, weight_max

    @staticmethod
    def format_weight_range(weight_min, weight_max):
        """Format a static or variable weight descriptor for generated CSS."""
        if weight_min == weight_max:
            return str(weight_min)
        return f"{weight_min} {weight_max}"

    @staticmethod
    def normalize_subset_name(label):
        """Normalize a Google Fonts subset comment for filtering and filenames."""
        if not label:
            return None

        normalized = re.sub(r"[^a-z0-9]+", "-", label.strip().lower()).strip("-")
        return normalized or None

    @staticmethod
    def filename_component(value):
        """Convert a CSS descriptor into a stable filename component."""
        normalized = re.sub(r"[^A-Za-z0-9]+", "", value)
        return normalized or "Font"

    @staticmethod
    def assign_collision_safe_filenames(fonts):
        """Assign unique names, adding subset and ordinal suffixes as needed."""
        collision_counts = {}
        for font in fonts:
            base_filename = font["base_filename"]
            collision_counts[base_filename] = collision_counts.get(base_filename, 0) + 1

        used_filenames = set()
        unnamed_subset_index = 0
        for font in fonts:
            base_filename = font.pop("base_filename")
            if collision_counts[base_filename] == 1:
                candidate = base_filename
            else:
                subset = font["subset"]
                if subset is None:
                    unnamed_subset_index += 1
                    subset = f"subset-{unnamed_subset_index}"
                    font["subset"] = subset
                candidate = f"{base_filename}-{subset.title().replace('-', '')}"

            unique_candidate = candidate
            ordinal = 2
            while f"{unique_candidate}.woff2" in used_filenames:
                unique_candidate = f"{candidate}-{ordinal}"
                ordinal += 1
            font["filename"] = f"{unique_candidate}.woff2"
            used_filenames.add(font["filename"])

    @staticmethod
    def get_weight_name(weight_min, weight_max=None):
        """Return a readable filename component for a weight or weight range."""
        weight_max = weight_min if weight_max is None else weight_max
        if weight_min != weight_max:
            return f"Variable{weight_min}To{weight_max}"

        weight_names = {
            100: "Thin",
            200: "ExtraLight",
            300: "Light",
            400: "Regular",
            500: "Medium",
            600: "SemiBold",
            700: "Bold",
            800: "ExtraBold",
            900: "Black",
        }
        return weight_names.get(weight_min, f"W{weight_min}")

    @staticmethod
    def matches_weight_filter(font_info, filter_weights):
        """Return whether any requested weight is covered by this font face."""
        if not filter_weights:
            return True
        return any(
            font_info["weight_min"] <= int(weight) <= font_info["weight_max"]
            for weight in filter_weights
        )

    def download_font_file(self, font_info, output_dir="."):
        """Download one WOFF2 file, treating an existing local file as success."""
        try:
            output_path = Path(output_dir)
            destination = output_path / font_info["filename"]

            if destination.exists():
                print(f"  [SKIP] Existing: {font_info['filename']}")
                return True

            print(f"  [INFO] Downloading: {font_info['filename']}")
            response = self.session.get(font_info["url"], timeout=60)
            response.raise_for_status()

            output_path.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(response.content)

            file_size = len(response.content) / 1024
            print(f"  [OK] Complete: {font_info['filename']} ({file_size:.1f} KB)")
            return True
        except Exception as exc:
            print(f"  [ERROR] Download failed for {font_info['filename']}: {exc}")
            return False

    def process_css_url(
        self,
        css_url,
        output_dir=".",
        filter_weights=None,
        filter_subsets=None,
    ):
        """Download selected faces from one Google Fonts CSS URL."""
        print(f"\n[PROCESS] Font CSS: {css_url}")
        css_content = self.download_css(css_url)
        if not css_content:
            return []

        fonts = self.extract_font_info_from_css(css_content)
        if not fonts:
            print("  [WARN] No WOFF2 font faces found")
            return []

        selected_fonts = [
            font
            for font in fonts
            if self.matches_weight_filter(font, filter_weights)
            and (not filter_subsets or font["subset"] in filter_subsets)
        ]
        if not selected_fonts:
            print("  [WARN] No font faces matched the requested filters")
            return []

        font_families = {}
        for font in selected_fonts:
            font_families.setdefault(font["family"], []).append(font)

        print(
            f"  [INFO] Found {len(font_families)} families and "
            f"{len(selected_fonts)} selected files"
        )
        downloaded_fonts = []
        for family_name, family_fonts in font_families.items():
            print(f"\n  [FAMILY] {family_name}")
            for font_info in family_fonts:
                if self.download_font_file(font_info, output_dir):
                    downloaded_fonts.append(font_info)

        print(f"\n  [SUMMARY] Downloaded or reused: {len(downloaded_fonts)} files")
        return downloaded_fonts

    def download_project_fonts(
        self,
        output_dir=".",
        filter_weights=None,
        filter_subsets=None,
    ):
        """Download the predefined project font collection."""
        print("[START] Downloading project fonts")
        print("=" * 60)
        effective_weights = (
            DEFAULT_PROJECT_WEIGHTS if filter_weights is None else filter_weights
        )

        downloaded_fonts = []
        successful_stylesheets = 0
        for css_url in self.project_fonts:
            current_fonts = self.process_css_url(
                css_url,
                output_dir,
                filter_weights=effective_weights,
                filter_subsets=filter_subsets,
            )
            if current_fonts:
                successful_stylesheets += 1
                downloaded_fonts.extend(current_fonts)

        print(
            "\n[COMPLETE] Processed "
            f"{successful_stylesheets}/{len(self.project_fonts)} font stylesheets"
        )
        return downloaded_fonts

    def write_css_manifest(
        self,
        fonts,
        output_dir=".",
        manifest_name=DEFAULT_MANIFEST_NAME,
    ):
        """Write local @font-face rules for the downloaded unicode shards."""
        output_path = Path(output_dir).resolve()
        manifest_path = Path(manifest_name)
        if not manifest_path.is_absolute():
            manifest_path = output_path / manifest_path
        manifest_path.parent.mkdir(parents=True, exist_ok=True)

        unique_fonts = {}
        for font in fonts:
            key = (
                font["family"],
                font["style"],
                font["weight_min"],
                font["weight_max"],
                font.get("unicode_range"),
                font["filename"],
            )
            unique_fonts[key] = font

        ordered_fonts = sorted(
            unique_fonts.values(),
            key=lambda font: (
                font["family"].lower(),
                font["style"],
                font["weight_min"],
                font["weight_max"],
                font.get("subset") or "",
                font["filename"],
            ),
        )

        lines = ["/* Generated by font_downloader.py. Do not hand-edit. */", ""]
        for index, font in enumerate(ordered_fonts):
            if font.get("subset"):
                lines.append(f"/* {font['subset']} */")

            font_path = output_path / font["filename"]
            relative_path = os.path.relpath(font_path, manifest_path.parent)
            relative_url = relative_path.replace(os.sep, "/")
            if not relative_url.startswith((".", "/")):
                relative_url = f"./{relative_url}"

            lines.extend([
                "@font-face {",
                f"  font-family: '{self.css_string(font['family'])}';",
                f"  font-style: {font['style']};",
                f"  font-weight: {font['weight']};",
                f"  font-display: {font['font_display']};",
                f"  src: url('{self.css_string(relative_url)}') format('woff2');",
            ])
            if font.get("unicode_range"):
                lines.append(f"  unicode-range: {font['unicode_range']};")
            lines.append("}")
            if index != len(ordered_fonts) - 1:
                lines.append("")

        manifest_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
        print(f"[MANIFEST] Wrote runtime CSS: {manifest_path}")
        return manifest_path

    @staticmethod
    def css_string(value):
        """Escape a value embedded in a single-quoted CSS string."""
        return value.replace("\\", "\\\\").replace("'", "\\'")


def main():
    parser = argparse.ArgumentParser(
        description="Download Google Fonts WOFF2 files with local runtime CSS",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  %(prog)s "https://fonts.googleapis.com/css2?family=Roboto:wght@300;400;500&display=swap"
  %(prog)s "CSS_URL" --weights 400,500 --subsets latin,latin-ext
  %(prog)s --download-all -o ./fonts
        """,
    )
    parser.add_argument("css_url", nargs="?", help="Google Fonts CSS URL")
    parser.add_argument(
        "--download-all",
        action="store_true",
        help="download the predefined project font collection",
    )
    parser.add_argument(
        "-o",
        "--output",
        default=".",
        help="output directory (default: current directory)",
    )
    parser.add_argument(
        "--weights",
        type=parse_weight_list,
        help=(
            "comma-separated requested weights; variable faces are selected when "
            "their range includes any requested value"
        ),
    )
    parser.add_argument(
        "--subsets",
        help="comma-separated unicode subset labels (for example: latin,latin-ext)",
    )
    parser.add_argument(
        "--manifest",
        default=DEFAULT_MANIFEST_NAME,
        help=(
            "companion CSS manifest path, relative to the output directory "
            f"(default: {DEFAULT_MANIFEST_NAME})"
        ),
    )
    args = parser.parse_args()

    if not args.css_url and not args.download_all:
        parser.print_help()
        return 1

    try:
        downloader = UniversalFontDownloader()
        filter_subsets = None
        if args.subsets:
            filter_subsets = [
                downloader.normalize_subset_name(value)
                for value in args.subsets.split(",")
                if value.strip()
            ]

        if args.download_all:
            downloaded_fonts = downloader.download_project_fonts(
                args.output,
                filter_weights=args.weights,
                filter_subsets=filter_subsets,
            )
        else:
            downloaded_fonts = downloader.process_css_url(
                args.css_url,
                args.output,
                filter_weights=args.weights,
                filter_subsets=filter_subsets,
            )

        if not downloaded_fonts:
            print("\n[ERROR] No font files were downloaded")
            return 1

        manifest_path = downloader.write_css_manifest(
            downloaded_fonts,
            args.output,
            args.manifest,
        )
        print(f"\n[SUCCESS] Fonts saved in: {Path(args.output).resolve()}")
        print(f"[SUCCESS] Load the generated CSS manifest: {manifest_path}")
        return 0
    except KeyboardInterrupt:
        print("\n\n[INTERRUPTED] Download interrupted")
        return 1
    except Exception as exc:
        print(f"\n[ERROR] {exc}")
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
