#!/usr/bin/env python3
"""Helix `on_orchestration_stop` hook: write the wave-close summary + HITL.

Studio green means the orchestration finished, not that a PR exists or that
there were no warnings. This hook always writes
`reports/execution/wave-close.md`. Open points also go to the predefined HITL
channel: a GitHub issue on the product remote (label `hitl`). Optional
`CONTIGO_HITL_WEBHOOK_URL` (only present when the operator runs this script
outside the stripped hook env).

Observation hook: empty stdout, log on stderr, non-zero is fail-open.
"""

from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import urllib.error
import urllib.request
from datetime import UTC, datetime
from pathlib import Path

from _product_repo import ARTIFACT, _git, _origin, product_repo

CURRENT = ARTIFACT / "reports" / "plan" / "slice.current.yaml"
OUT_DIR = ARTIFACT / "reports" / "execution"
SUMMARY = OUT_DIR / "wave-close.md"
HITL_LABEL = "hitl"
WEBHOOK_ENV = "CONTIGO_HITL_WEBHOOK_URL"


def _log(msg: str) -> None:
    print(msg, file=sys.stderr)


def _slice_id() -> str:
    if not CURRENT.is_file():
        return "slice"
    for line in CURRENT.read_text(encoding="utf-8").splitlines():
        if line.startswith("waveId:"):
            return line.split(":", 1)[1].strip().strip("'\"") or "slice"
    return "slice"


def _gh(repo: Path, *args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["gh", *args],
        cwd=repo,
        check=False,
        text=True,
        capture_output=True,
    )


def _pr_url(repo: Path) -> str:
    if shutil.which("gh") is None:
        return ""
    proc = _gh(
        repo,
        "pr",
        "list",
        "--base",
        "main",
        "--head",
        "integration",
        "--state",
        "open",
        "--json",
        "url",
        "--jq",
        ".[0].url // empty",
    )
    return (proc.stdout or "").strip()


def _commits(repo: Path) -> list[str]:
    proc = _git(repo, "log", "--oneline", "origin/main..integration")
    if proc.returncode != 0:
        proc = _git(repo, "log", "--oneline", "main..integration")
    if proc.returncode != 0:
        return []
    return [line for line in (proc.stdout or "").splitlines() if line.strip()]


def _hcp_pending(repo: Path) -> str | None:
    script = repo / "scripts" / "hcp_vcs_wiring.py"
    if not script.is_file():
        return None
    env = os.environ.copy()
    proc = subprocess.run(
        [sys.executable, str(script), "--check-only"],
        cwd=repo,
        check=False,
        text=True,
        capture_output=True,
        env=env,
    )
    blob = (proc.stdout or "") + (proc.stderr or "")
    if "pending" in blob.lower() or "WARN" in blob:
        return blob.strip()[:2000] or "VCS wiring pending (see script output)"
    return None


def _collect(repo: Path, slice_id: str) -> tuple[str, list[str]]:
    pr = _pr_url(repo)
    commits = _commits(repo)
    open_points: list[str] = []
    if not pr:
        open_points.append(
            "No open PR `integration` → `main` on the product remote. "
            "Studio green does not imply the stop-hook opened one."
        )
    hcp = _hcp_pending(repo)
    if hcp:
        open_points.append(
            "HCP VCS still pending (no GitHub oauth-client on the org). "
            "Human step: connect GitHub in HCP Terraform, then re-run "
            "`python scripts/hcp_vcs_wiring.py`.\n\n```\n"
            + hcp
            + "\n```"
        )

    lines = [
        f"# Wave close — `{slice_id}`",
        "",
        f"- **When**: {datetime.now(UTC).isoformat()}",
        f"- **Product repo**: `{repo}`",
        f"- **Origin**: `{_origin(repo) or '(none)'}`",
        f"- **PR**: {pr or '**missing**'}",
        f"- **Open points**: {len(open_points)}",
        "",
        "## Commits on `integration` not on `origin/main`",
        "",
    ]
    if commits:
        lines.extend(f"- `{c}`" for c in commits)
    else:
        lines.append("- (none, or `integration` / `origin/main` missing)")
    lines += ["", "## Open points", ""]
    if open_points:
        for i, point in enumerate(open_points, 1):
            lines.append(f"{i}. {point}")
            lines.append("")
    else:
        lines.append("None. PR is open and no scripted warnings fired.")
        lines.append("")
    lines += [
        "## How to read Studio",
        "",
        "Green on `execution-fanout` means the orchestration finished "
        "(`failed_task_ids` empty). It does **not** mean a PR exists, and it "
        "does **not** mean there were zero warnings. `on_orchestration_stop` "
        "is observation-only (fail-open): a hook error is recorded and the "
        "wave still completes. This file is the close record; HITL is the "
        "human channel when open points exist.",
        "",
    ]
    return "\n".join(lines), open_points


def _ensure_label(repo: Path) -> None:
    _gh(
        repo,
        "label",
        "create",
        HITL_LABEL,
        "--description",
        "Helix wave-close open points (human step required)",
        "--color",
        "B60205",
        "--force",
    )


def _open_github_issue(repo: Path, slice_id: str, body: str) -> str:
    if shutil.which("gh") is None:
        _log("[close_wave] gh not on PATH; cannot open HITL issue")
        return ""
    if not _origin(repo):
        _log("[close_wave] product repo has no origin; cannot open HITL issue")
        return ""
    _ensure_label(repo)
    title = f"[HITL] {slice_id}: open points after a green wave"
    proc = _gh(
        repo,
        "issue",
        "create",
        "--title",
        title,
        "--body",
        body,
        "--label",
        HITL_LABEL,
    )
    url = (proc.stdout or "").strip()
    if proc.returncode != 0 or not url:
        _log(proc.stderr or proc.stdout or "[close_wave] gh issue create failed")
        return ""
    _log(f"[close_wave] HITL issue: {url}")
    return url


def _post_webhook(slice_id: str, n_open: int, summary: str) -> None:
    url = (os.environ.get(WEBHOOK_ENV) or "").strip()
    if not url:
        return
    payload = {
        "text": f"HITL {slice_id}: {n_open} open point(s) after a green Helix wave.",
        "slice_id": slice_id,
        "open_points": n_open,
        "summary": summary[:8000],
    }
    req = urllib.request.Request(
        url,
        data=json.dumps(payload).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=15) as resp:
            _log(f"[close_wave] webhook status {resp.status}")
    except (urllib.error.URLError, TimeoutError) as exc:
        _log(f"[close_wave] webhook failed: {exc}")


def main() -> int:
    if not sys.stdin.isatty():
        sys.stdin.read()
    repo = product_repo()
    slice_id = _slice_id()
    _log(f"[close_wave] product_repo={repo} slice={slice_id}")
    text, open_points = _collect(repo, slice_id)
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    SUMMARY.write_text(text, encoding="utf-8")
    _log(f"[close_wave] wrote {SUMMARY}")
    if open_points:
        _open_github_issue(repo, slice_id, text)
        _post_webhook(slice_id, len(open_points), text)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
