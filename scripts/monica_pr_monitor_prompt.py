#!/usr/bin/env python3
"""Pre-run detector for the Monica PR monitor cron job.

This script is intentionally cheap: it uses GitHub CLI JSON output and a small
state file to decide whether the expensive cron agent should wake up. If no
scoped PR has changed since the previous run, it prints a prompt instructing the
agent to return [SILENT]. If there is a new or updated PR/review/check signal,
it prints a focused prompt with the changed PRs and the handling workflow.
"""

from __future__ import annotations

import hashlib
import json
import os
import subprocess
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

REPO_DIR = Path(os.getenv("MONICA_REPO_DIR", "/root/.openclaw/workspace/MoLibrary"))
OWNER_REPO = os.getenv("MONICA_OWNER_REPO", "Tairitsua/Monica")
STATE_PATH = Path.home() / ".hermes" / "state" / "monica-pr-monitor-state.json"
BRANCH_PREFIXES = ("kou/", "codex/", "agent/", "fix/", "feat/")
ALWAYS_INCLUDE_NUMBERS = {6}


def run(args: list[str], *, cwd: Path = REPO_DIR, timeout: int = 45, allow_failure: bool = False) -> subprocess.CompletedProcess[str]:
    result = subprocess.run(
        args,
        cwd=str(cwd),
        text=True,
        capture_output=True,
        timeout=timeout,
    )
    if not allow_failure and result.returncode != 0:
        raise RuntimeError(
            f"Command failed ({result.returncode}): {' '.join(args)}\n"
            f"stdout:\n{result.stdout.strip()}\n"
            f"stderr:\n{result.stderr.strip()}"
        )
    return result


def gh_json(args: list[str], *, allow_failure: bool = False) -> Any:
    result = run(["gh", *args], timeout=60, allow_failure=allow_failure)
    if result.returncode != 0:
        return None
    text = result.stdout.strip()
    if not text:
        return None
    return json.loads(text)


def load_state() -> dict[str, Any]:
    if not STATE_PATH.exists():
        return {"version": 1, "prs": {}}
    try:
        return json.loads(STATE_PATH.read_text(encoding="utf-8"))
    except Exception:
        return {"version": 1, "prs": {}}


def save_state(state: dict[str, Any]) -> None:
    STATE_PATH.parent.mkdir(parents=True, exist_ok=True)
    tmp = STATE_PATH.with_suffix(".tmp")
    tmp.write_text(json.dumps(state, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    tmp.replace(STATE_PATH)


def is_scoped_pr(pr: dict[str, Any]) -> bool:
    number = int(pr.get("number") or 0)
    head = str(pr.get("headRefName") or "")
    author = ((pr.get("author") or {}).get("login") or "").lower()
    if number in ALWAYS_INCLUDE_NUMBERS:
        return True
    if head.startswith(BRANCH_PREFIXES):
        return True
    if "bot" in author or "agent" in author or author == "tairitsuabot":
        return True
    return False


def compact_reviews(pr: dict[str, Any]) -> list[dict[str, str]]:
    reviews = pr.get("reviews") or []
    compact: list[dict[str, str]] = []
    for review in reviews[-8:]:
        compact.append(
            {
                "author": ((review.get("author") or {}).get("login") or ""),
                "state": str(review.get("state") or ""),
                "submittedAt": str(review.get("submittedAt") or ""),
                "body": str(review.get("body") or "")[:800],
            }
        )
    return compact


def compact_comments(pr: dict[str, Any]) -> list[dict[str, str]]:
    comments = pr.get("comments") or []
    compact: list[dict[str, str]] = []
    for comment in comments[-8:]:
        compact.append(
            {
                "author": ((comment.get("author") or {}).get("login") or ""),
                "createdAt": str(comment.get("createdAt") or ""),
                "body": str(comment.get("body") or "")[:800],
            }
        )
    return compact


def fetch_inline_comments(number: int) -> list[dict[str, str]]:
    data = gh_json(["api", f"repos/{OWNER_REPO}/pulls/{number}/comments"], allow_failure=True) or []
    compact: list[dict[str, str]] = []
    for comment in data[-20:]:
        compact.append(
            {
                "author": ((comment.get("user") or {}).get("login") or ""),
                "path": str(comment.get("path") or ""),
                "line": str(comment.get("line") or comment.get("original_line") or ""),
                "updatedAt": str(comment.get("updated_at") or ""),
                "body": str(comment.get("body") or "")[:1000],
            }
        )
    return compact


def compact_checks(pr: dict[str, Any]) -> str:
    checks = pr.get("statusCheckRollup") or []
    compact: list[str] = []
    for check in checks:
        typename = str(check.get("__typename") or "")
        if typename == "CheckRun":
            name = str(check.get("name") or "")
            status = str(check.get("status") or "")
            conclusion = str(check.get("conclusion") or "")
            details_url = str(check.get("detailsUrl") or "")
            compact.append("\t".join(part for part in (name, status, conclusion, details_url) if part))
        elif typename == "StatusContext":
            context = str(check.get("context") or "")
            state = str(check.get("state") or "")
            target_url = str(check.get("targetUrl") or "")
            compact.append("\t".join(part for part in (context, state, target_url) if part))
    return "\n".join(compact)[:3000]


def pr_signal(pr: dict[str, Any], inline_comments: list[dict[str, str]], checks: str) -> str:
    payload = {
        "number": pr.get("number"),
        "title": pr.get("title"),
        "url": pr.get("url"),
        "state": pr.get("state"),
        "headRefName": pr.get("headRefName"),
        "baseRefName": pr.get("baseRefName"),
        "updatedAt": pr.get("updatedAt"),
        "reviewDecision": pr.get("reviewDecision"),
        "mergeStateStatus": pr.get("mergeStateStatus"),
        "latestReviews": compact_reviews(pr),
        "latestComments": compact_comments(pr),
        "inlineComments": inline_comments,
        "checks": checks,
    }
    raw = json.dumps(payload, sort_keys=True, ensure_ascii=False)
    return hashlib.sha256(raw.encode("utf-8")).hexdigest()


def print_silent(reason: str) -> None:
    print(
        "No new scoped Monica PR activity was detected.\n"
        f"Reason: {reason}\n\n"
        "Return exactly [SILENT] and do not inspect the repository or call tools."
    )


def build_action_prompt(changed: list[dict[str, Any]], all_scoped: list[dict[str, Any]]) -> str:
    summary = {
        "changed_prs": changed,
        "all_scoped_open_prs": [
            {
                "number": pr["number"],
                "title": pr["title"],
                "url": pr["url"],
                "headRefName": pr["headRefName"],
                "updatedAt": pr["updatedAt"],
                "reviewDecision": pr.get("reviewDecision"),
            }
            for pr in all_scoped
        ],
    }
    print(
        "New or updated scoped Monica PR activity was detected. Follow this prompt exactly.\n\n"
        "Context JSON:\n"
        f"{json.dumps(summary, indent=2, ensure_ascii=False)}\n\n"
        "Task:\n"
        "Monitor open Monica pull requests created from local agent branches and handle straightforward review feedback.\n\n"
        "Scope:\n"
        f"- Repository: {REPO_DIR}\n"
        f"- GitHub repo: {OWNER_REPO}\n"
        "- Focus only on changed_prs above unless another scoped PR is clearly required for context.\n\n"
        "Hard rules:\n"
        "- Do NOT create or schedule cron jobs from this run.\n"
        "- Do NOT merge PRs.\n"
        "- Do NOT make broad product/design decisions silently.\n"
        "- Do NOT touch unrelated untracked files such as examples/README.md unless the feedback explicitly requires it.\n"
        "- Before editing Monica code, follow project-local guidance: read AGENTS.md and relevant .codex skills.\n"
        "- For Blazor/MudBlazor UI changes, apply .codex/skills/mo-ui-development/SKILL.md guidance.\n"
        "- For UI visual verification/testing, use Monica's Debug skill and start Monica.Docs as the bridge project when practical.\n"
        "- Prefer delegating non-trivial Monica development fixes to Codex; explicitly tell Codex to load AGENTS.md/.codex skills and use the Debug skill + Monica.Docs bridge workflow when testing UI changes.\n"
        "- Keep responses concise but include PR URL, actions taken, commits, verification, and anything needing the user's decision.\n\n"
        "Workflow:\n"
        "1. Inspect changed_prs details. Fetch fresh data only for those PR numbers.\n"
        "2. Classify feedback as straightforward/safe or ambiguous/design-related.\n"
        "3. If straightforward/safe: check out the PR branch, apply only the scoped fix, verify, commit, push, and comment on the PR.\n"
        "4. If CI fails and root cause is clear within PR scope: fix/test/commit/push/comment.\n"
        "5. If ambiguous/design-related, do not edit; report the concern and recommended options.\n\n"
        "Final response format:\n"
        "- Checked: PR links/numbers\n"
        "- Action taken: commits/comments or none\n"
        "- Verification: commands/results if changes were made\n"
        "- Needs user decision: only if applicable\n"
    )


def main() -> int:
    try:
        if not REPO_DIR.exists():
            print_silent(f"repository path does not exist: {REPO_DIR}")
            return 0

        prs = gh_json(
            [
                "pr",
                "list",
                "--repo",
                OWNER_REPO,
                "--state",
                "open",
                "--limit",
                "50",
                "--json",
                "number,title,url,state,headRefName,baseRefName,author,updatedAt,reviewDecision,mergeStateStatus,comments,reviews,statusCheckRollup",
            ]
        ) or []
        scoped = [pr for pr in prs if is_scoped_pr(pr)]
        if not scoped:
            state = load_state()
            state["prs"] = {}
            state["last_checked_at"] = datetime.now(timezone.utc).isoformat()
            save_state(state)
            print_silent("no open scoped PRs")
            return 0

        state = load_state()
        old_prs = state.get("prs") or {}
        new_prs: dict[str, Any] = {}
        changed: list[dict[str, Any]] = []

        for pr in scoped:
            number = int(pr["number"])
            inline = fetch_inline_comments(number)
            checks = compact_checks(pr)
            signal = pr_signal(pr, inline, checks)
            key = str(number)
            new_prs[key] = {
                "signal": signal,
                "title": pr.get("title"),
                "url": pr.get("url"),
                "headRefName": pr.get("headRefName"),
                "updatedAt": pr.get("updatedAt"),
                "last_seen_at": datetime.now(timezone.utc).isoformat(),
            }
            if old_prs.get(key, {}).get("signal") != signal:
                changed.append(
                    {
                        "number": number,
                        "title": pr.get("title"),
                        "url": pr.get("url"),
                        "headRefName": pr.get("headRefName"),
                        "baseRefName": pr.get("baseRefName"),
                        "updatedAt": pr.get("updatedAt"),
                        "reviewDecision": pr.get("reviewDecision"),
                        "mergeStateStatus": pr.get("mergeStateStatus"),
                        "latestReviews": compact_reviews(pr),
                        "latestComments": compact_comments(pr),
                        "inlineComments": inline,
                        "checks": checks,
                    }
                )

        state = {
            "version": 1,
            "repo": OWNER_REPO,
            "last_checked_at": datetime.now(timezone.utc).isoformat(),
            "prs": new_prs,
        }
        save_state(state)

        if not changed:
            print_silent(f"{len(scoped)} scoped PR(s) unchanged")
            return 0

        build_action_prompt(changed, scoped)
        return 0
    except Exception as exc:
        print(
            "The Monica PR monitor pre-run script failed. Report this to the user and do not modify code until fixed.\n\n"
            f"Error: {type(exc).__name__}: {exc}"
        )
        return 0


if __name__ == "__main__":
    sys.exit(main())
