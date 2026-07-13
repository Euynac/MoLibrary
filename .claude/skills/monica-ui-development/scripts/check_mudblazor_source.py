#!/usr/bin/env python3
"""Resolve MudBlazor source through the user-level inspect-dependency-source skill."""

from __future__ import annotations

import argparse
import json
import shlex
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from mudblazor_skill_state import (
    default_dependency_source_cli,
    dependency_source_cli_candidates,
)

DEPENDENCY_QUERY = "MudBlazor"
REQUIRED_RELATIVE_FILE = Path(
    "src/MudBlazor/Components/ThemeProvider/MudThemeProvider.razor.cs"
)
RESOLVE_TIMEOUT_SECONDS = 30
MAX_DIAGNOSTIC_TEXT_LENGTH = 2_000


@dataclass(frozen=True)
class MudBlazorResolution:
    """Validated result of one inspect-dependency-source resolution."""

    cli_path: Path | None
    checked_cli_paths: tuple[Path, ...]
    resolver_return_code: int | None
    resolver_payload: dict[str, Any] | None
    resolver_stdout: str
    resolver_stderr: str
    source_root: Path | None
    marker_path: Path | None
    error_code: str | None
    error_message: str | None

    @property
    def is_available(self) -> bool:
        return (
            self.error_code is None
            and self.source_root is not None
            and self.marker_path is not None
        )

    def to_dict(self) -> dict[str, Any]:
        return {
            "ok": self.is_available,
            "query": DEPENDENCY_QUERY,
            "source_path": str(self.source_root) if self.source_root else None,
            "marker_relative_path": REQUIRED_RELATIVE_FILE.as_posix(),
            "marker_path": str(self.marker_path) if self.marker_path else None,
            "error_code": self.error_code,
            "error_message": self.error_message,
            "resolver": {
                "cli_path": str(self.cli_path) if self.cli_path else None,
                "checked_cli_paths": [str(path) for path in self.checked_cli_paths],
                "return_code": self.resolver_return_code,
                "payload": self.resolver_payload,
                "stdout": (
                    _truncate(self.resolver_stdout)
                    if self.resolver_stdout and self.resolver_payload is None
                    else None
                ),
                "stderr": _truncate(self.resolver_stderr) if self.resolver_stderr else None,
            },
            "registration_commands": registration_commands(self.cli_path),
        }


def _truncate(value: str) -> str:
    text = value.strip()
    if len(text) <= MAX_DIAGNOSTIC_TEXT_LENGTH:
        return text
    return f"{text[:MAX_DIAGNOSTIC_TEXT_LENGTH]}... [truncated]"


def _resolver_error(payload: dict[str, Any] | None) -> tuple[str | None, str | None]:
    if not payload:
        return None, None

    error_code = payload.get("error_code")
    message = payload.get("message")
    error = payload.get("error")
    if isinstance(error, dict):
        error_code = error_code or error.get("code")
        message = message or error.get("message")
    elif isinstance(error, str):
        message = message or error

    return (
        str(error_code) if error_code else None,
        str(message) if message else None,
    )


def _result(
    *,
    cli_path: Path | None,
    checked_cli_paths: tuple[Path, ...],
    resolver_return_code: int | None = None,
    resolver_payload: dict[str, Any] | None = None,
    resolver_stdout: str = "",
    resolver_stderr: str = "",
    source_root: Path | None = None,
    marker_path: Path | None = None,
    error_code: str | None = None,
    error_message: str | None = None,
) -> MudBlazorResolution:
    return MudBlazorResolution(
        cli_path=cli_path,
        checked_cli_paths=checked_cli_paths,
        resolver_return_code=resolver_return_code,
        resolver_payload=resolver_payload,
        resolver_stdout=resolver_stdout,
        resolver_stderr=resolver_stderr,
        source_root=source_root,
        marker_path=marker_path,
        error_code=error_code,
        error_message=error_message,
    )


def resolve_mudblazor_source() -> MudBlazorResolution:
    """Invoke the global resolver and validate its source path and MudBlazor marker."""

    checked_cli_paths = dependency_source_cli_candidates()
    cli_path = next((path for path in checked_cli_paths if path.is_file()), None)
    if cli_path is None:
        return _result(
            cli_path=None,
            checked_cli_paths=checked_cli_paths,
            error_code="dependency_source_cli_not_found",
            error_message=(
                "The user-level inspect-dependency-source CLI is not installed "
                "in any supported location."
            ),
        )

    command = [sys.executable, str(cli_path), "resolve", DEPENDENCY_QUERY, "--json"]
    try:
        completed = subprocess.run(
            command,
            capture_output=True,
            check=False,
            text=True,
            timeout=RESOLVE_TIMEOUT_SECONDS,
        )
    except subprocess.TimeoutExpired as exc:
        return _result(
            cli_path=cli_path,
            checked_cli_paths=checked_cli_paths,
            resolver_stdout=exc.stdout or "",
            resolver_stderr=exc.stderr or "",
            error_code="dependency_source_resolve_timeout",
            error_message=f"The dependency resolver exceeded {RESOLVE_TIMEOUT_SECONDS} seconds.",
        )
    except OSError as exc:
        return _result(
            cli_path=cli_path,
            checked_cli_paths=checked_cli_paths,
            error_code="dependency_source_cli_failed",
            error_message=f"Could not start the dependency resolver: {exc}",
        )

    stdout = completed.stdout.strip()
    stderr = completed.stderr.strip()
    try:
        payload = json.loads(stdout)
    except json.JSONDecodeError as exc:
        return _result(
            cli_path=cli_path,
            checked_cli_paths=checked_cli_paths,
            resolver_return_code=completed.returncode,
            resolver_stdout=stdout,
            resolver_stderr=stderr,
            error_code="dependency_source_invalid_json",
            error_message=f"The dependency resolver did not return valid JSON: {exc}",
        )

    if not isinstance(payload, dict):
        return _result(
            cli_path=cli_path,
            checked_cli_paths=checked_cli_paths,
            resolver_return_code=completed.returncode,
            resolver_stdout=stdout,
            resolver_stderr=stderr,
            error_code="dependency_source_invalid_json",
            error_message="The dependency resolver returned a JSON value that is not an object.",
        )

    resolver_error_code, resolver_error_message = _resolver_error(payload)
    if completed.returncode != 0 or payload.get("status") != "ok":
        return _result(
            cli_path=cli_path,
            checked_cli_paths=checked_cli_paths,
            resolver_return_code=completed.returncode,
            resolver_payload=payload,
            resolver_stdout=stdout,
            resolver_stderr=stderr,
            error_code=(
                resolver_error_code
                or (
                    "dependency_source_resolve_failed"
                    if completed.returncode != 0
                    else "dependency_source_invalid_status"
                )
            ),
            error_message=(
                resolver_error_message
                or (
                    "The dependency resolver could not resolve MudBlazor."
                    if completed.returncode != 0
                    else "The dependency resolver did not return status 'ok'."
                )
            ),
        )

    source_path = payload.get("source_path")
    if not isinstance(source_path, str) or not source_path.strip():
        return _result(
            cli_path=cli_path,
            checked_cli_paths=checked_cli_paths,
            resolver_return_code=completed.returncode,
            resolver_payload=payload,
            resolver_stdout=stdout,
            resolver_stderr=stderr,
            error_code="dependency_source_path_missing",
            error_message="The resolver response does not contain a non-empty source_path.",
        )

    source_root = Path(source_path).expanduser()
    if not source_root.is_absolute():
        return _result(
            cli_path=cli_path,
            checked_cli_paths=checked_cli_paths,
            resolver_return_code=completed.returncode,
            resolver_payload=payload,
            resolver_stdout=stdout,
            resolver_stderr=stderr,
            error_code="dependency_source_path_not_absolute",
            error_message=f"The resolver returned a non-absolute source path: {source_root}",
        )

    try:
        source_root = source_root.resolve(strict=True)
    except OSError as exc:
        return _result(
            cli_path=cli_path,
            checked_cli_paths=checked_cli_paths,
            resolver_return_code=completed.returncode,
            resolver_payload=payload,
            resolver_stdout=stdout,
            resolver_stderr=stderr,
            error_code="dependency_source_path_unavailable",
            error_message=f"The resolved source path is unavailable: {source_root} ({exc})",
        )

    if not source_root.is_dir():
        return _result(
            cli_path=cli_path,
            checked_cli_paths=checked_cli_paths,
            resolver_return_code=completed.returncode,
            resolver_payload=payload,
            resolver_stdout=stdout,
            resolver_stderr=stderr,
            source_root=source_root,
            error_code="dependency_source_path_not_directory",
            error_message=f"The resolved source path is not a directory: {source_root}",
        )

    marker_path = source_root / REQUIRED_RELATIVE_FILE
    if not marker_path.is_file():
        return _result(
            cli_path=cli_path,
            checked_cli_paths=checked_cli_paths,
            resolver_return_code=completed.returncode,
            resolver_payload=payload,
            resolver_stdout=stdout,
            resolver_stderr=stderr,
            source_root=source_root,
            error_code="mudblazor_marker_missing",
            error_message=f"The resolved tree does not contain the required marker: {marker_path}",
        )

    return _result(
        cli_path=cli_path,
        checked_cli_paths=checked_cli_paths,
        resolver_return_code=completed.returncode,
        resolver_payload=payload,
        resolver_stdout=stdout,
        resolver_stderr=stderr,
        source_root=source_root,
        marker_path=marker_path,
    )


def registration_commands(cli_path: Path | None = None) -> list[str]:
    command_path = cli_path or default_dependency_source_cli()
    return [
        " ".join(
            (
                shlex.quote(sys.executable),
                shlex.quote(str(command_path)),
                "repo add-local <mudblazor-source-root> --alias MudBlazor",
            )
        )
    ]


def print_failure_details(resolution: MudBlazorResolution) -> None:
    if resolution.error_code:
        print(f"Error code:       {resolution.error_code}")
    if resolution.error_message:
        print(f"Reason:           {resolution.error_message}")
    if resolution.cli_path:
        print(f"Resolver CLI:     {resolution.cli_path}")
    else:
        print("Checked CLI paths:")
        for path in resolution.checked_cli_paths:
            print(f"  - {path}")
    if resolution.resolver_stderr:
        print(f"Resolver stderr:  {_truncate(resolution.resolver_stderr)}")
    if resolution.resolver_stdout and resolution.resolver_payload is None:
        print(f"Resolver output:  {_truncate(resolution.resolver_stdout)}")
    if resolution.source_root:
        print(f"Resolved path:    {resolution.source_root}")

    print()
    print("Source-dependent work must stop here.")
    print("Install or register MudBlazor with the user-level $inspect-dependency-source skill,")
    print("then rerun this check. Suggested command:")
    for command in registration_commands(resolution.cli_path):
        print(f"  {command}")


def _payload_label(payload: dict[str, Any] | None, field: str) -> str | None:
    if not payload:
        return None
    value = payload.get(field)
    if isinstance(value, str):
        return value
    if isinstance(value, dict):
        for key in ("full_name", "canonical_name", "name", "id"):
            candidate = value.get(key)
            if candidate:
                return str(candidate)
    return None


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Check if MudBlazor source is available via inspect-dependency-source."
    )
    parser.add_argument("--json", action="store_true", help="Output machine-readable JSON.")
    args = parser.parse_args()

    resolution = resolve_mudblazor_source()
    if args.json:
        print(json.dumps(resolution.to_dict(), indent=2))
    elif resolution.is_available:
        print("[OK] MudBlazor source is available.")
        print(f"Resolver CLI:     {resolution.cli_path}")
        repository = _payload_label(resolution.resolver_payload, "repository")
        artifact = _payload_label(resolution.resolver_payload, "artifact")
        verification_state = _payload_label(resolution.resolver_payload, "verification_state")
        resolution_kind = _payload_label(resolution.resolver_payload, "resolution_kind")
        if repository:
            print(f"Repository:       {repository}")
        if artifact:
            print(f"Artifact:         {artifact}")
        if verification_state:
            print(f"Verification:     {verification_state}")
        if resolution_kind:
            print(f"Resolution kind:  {resolution_kind}")
        print(f"Resolved path:    {resolution.source_root}")
        print(f"Marker file:      {REQUIRED_RELATIVE_FILE.as_posix()}")
    else:
        print("[ERROR] MudBlazor source is not available through inspect-dependency-source.")
        print_failure_details(resolution)

    return 0 if resolution.is_available else 1


if __name__ == "__main__":
    sys.exit(main())
