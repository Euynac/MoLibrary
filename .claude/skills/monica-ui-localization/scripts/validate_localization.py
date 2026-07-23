#!/usr/bin/env python3
"""Run the canonical Monica localization validator from the legacy skill mirror."""

from pathlib import Path
import runpy


CANONICAL_VALIDATOR = (
    Path(__file__).resolve().parents[4]
    / ".agents"
    / "skills"
    / "monica-ui-localization"
    / "scripts"
    / "validate_localization.py"
)


if __name__ == "__main__":
    runpy.run_path(str(CANONICAL_VALIDATOR), run_name="__main__")
