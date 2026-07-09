#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import subprocess
import sys
import tempfile
from pathlib import Path
from typing import Iterable


REPO_ROOT = Path(__file__).resolve().parents[4]
DEFAULT_SOURCE_FONT = REPO_ROOT / ".tmp" / "monica-ui-font-sources" / "zcool_qingke_huangyou.ttf"
DEFAULT_TARGET_FONT = REPO_ROOT / "Monica.UI" / "wwwroot" / "fonts" / "Komi-ZCOOL-QingKe-HuangYou.woff2"
EXCLUDED_DEFAULT_LOCALIZATION_PROJECTS = {
    "Monica.UnitTests",
}

COMMON_UI_TEXT = (
    "ABCDEFGHIJKLMNOPQRSTUVWXYZ"
    "abcdefghijklmnopqrstuvwxyz"
    "0123456789"
    " .,;:!?()[]{}<>+-=*/\\|_@#$%^&~`'\""
    "，。！？、；：（）【】《》“”‘’…—-·"
)


def discover_default_localization_dirs() -> list[Path]:
    return [
        path
        for path in sorted(REPO_ROOT.glob("Monica*/Localization"))
        if path.is_dir() and path.parent.name not in EXCLUDED_DEFAULT_LOCALIZATION_PROJECTS
    ]


def walk_strings(value: object) -> Iterable[str]:
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for item in value.values():
            yield from walk_strings(item)
    elif isinstance(value, list):
        for item in value:
            yield from walk_strings(item)


def collect_localized_text(localization_dirs: Iterable[Path]) -> str:
    chars: set[str] = set(COMMON_UI_TEXT)

    for directory in localization_dirs:
        if not directory.exists():
            continue

        for path in sorted(directory.rglob("*.json")):
            data = json.loads(path.read_text(encoding="utf-8"))
            for text in walk_strings(data):
                chars.update(text)

    return "".join(sorted(chars))


def run_subset(source_font: Path, target_font: Path, text: str) -> None:
    target_font.parent.mkdir(parents=True, exist_ok=True)

    with tempfile.NamedTemporaryFile("w", encoding="utf-8", delete=False) as text_file:
        text_file.write(text)
        text_path = Path(text_file.name)

    try:
        subprocess.run(
            [
                sys.executable,
                "-m",
                "fontTools.subset",
                str(source_font),
                f"--text-file={text_path}",
                "--flavor=woff2",
                f"--output-file={target_font}",
                "--layout-features=*",
                "--name-IDs=*",
                "--name-legacy",
                "--name-languages=*",
                "--glyph-names",
                "--symbol-cmap",
                "--legacy-cmap",
                "--notdef-glyph",
                "--notdef-outline",
                "--recommended-glyphs",
            ],
            check=True,
        )
    finally:
        text_path.unlink(missing_ok=True)


def read_extra_text(args: argparse.Namespace) -> str:
    parts: list[str] = []

    if args.extra_text:
        parts.append(args.extra_text)

    for path in args.extra_text_file:
        parts.append(Path(path).read_text(encoding="utf-8"))

    return "".join(parts)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate or validate Monica UI WOFF2 font subsets from localization resources."
    )
    parser.add_argument(
        "--mode",
        choices=("generate", "check"),
        default="generate",
        help="generate writes the target font; check verifies the committed target is current.",
    )
    parser.add_argument("--source-font", type=Path, default=DEFAULT_SOURCE_FONT)
    parser.add_argument("--target-font", type=Path, default=DEFAULT_TARGET_FONT)
    parser.add_argument(
        "--localization-dir",
        type=Path,
        action="append",
        default=discover_default_localization_dirs(),
        help=(
            "Localization directory to scan recursively. Can be provided multiple times. "
            "Defaults to all top-level Monica*/Localization directories except test projects."
        ),
    )
    parser.add_argument("--extra-text", default="")
    parser.add_argument("--extra-text-file", type=Path, action="append", default=[])
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    source_font = args.source_font.resolve()
    target_font = args.target_font.resolve()

    if not source_font.exists():
        print(
            "Source font not found. Copy the upstream TTF/OTF to "
            f"{DEFAULT_SOURCE_FONT.relative_to(REPO_ROOT)} or pass --source-font. "
            "Do not place source TTF/OTF files under Monica.UI because they can be packed into NuGet.",
            file=sys.stderr,
        )
        return 2

    text = collect_localized_text(args.localization_dir) + read_extra_text(args)

    if args.mode == "generate":
        previous_size = target_font.stat().st_size if target_font.exists() else 0
        run_subset(source_font, target_font, text)
        print(
            f"Generated {target_font.relative_to(REPO_ROOT)} "
            f"from {len(set(text))} unique characters "
            f"({previous_size:,} -> {target_font.stat().st_size:,} bytes)."
        )
        return 0

    with tempfile.TemporaryDirectory() as temp_dir:
        generated = Path(temp_dir) / target_font.name
        run_subset(source_font, generated, text)

        if not target_font.exists():
            print(f"Target font is missing: {target_font.relative_to(REPO_ROOT)}", file=sys.stderr)
            return 1

        if generated.read_bytes() != target_font.read_bytes():
            print(
                f"Font subset is stale: {target_font.relative_to(REPO_ROOT)}. "
                "Run this script with --mode generate.",
                file=sys.stderr,
            )
            return 1

    print(f"Font subset is current: {target_font.relative_to(REPO_ROOT)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
