#!/usr/bin/env python3
"""Helix `on_orchestration_stop` hook: push `integration` and open a PR to `main`.

Bound on `execution-fanout` (success path only). `fan_out.write_back` is inert.

Helix runs this as an external command hook (manual §10.5):
  - envelope JSON on stdin (ignored; observation hook)
  - empty stdout = continue; any non-JSON stdout is a handler failure
  - log on stderr
  - child env is PATH/HOME/LANG/TMPDIR only — `gh auth login` must live under HOME
  - observation event: a non-zero exit is recorded; the wave still completed

Does NOT merge onto local `main`. Review happens on the GitHub PR.
"""

from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parents[1]
CURRENT = HERE / "reports" / "plan" / "slice.current.yaml"


def _log(msg: str) -> None:
    print(msg, file=sys.stderr)


def _git(*args: str, check: bool = True) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", *args],
        cwd=HERE,
        check=check,
        text=True,
        capture_output=True,
    )


def _slice_id() -> str:
    if not CURRENT.is_file():
        return "slice"
    for line in CURRENT.read_text(encoding="utf-8").splitlines():
        if line.startswith("waveId:"):
            value = line.split(":", 1)[1].strip().strip("'\"")
            return value or "slice"
    return "slice"


def main() -> int:
    sys.stdin.read()  # drain the hook envelope; do not echo it

    top = _git("rev-parse", "--show-toplevel", check=False)
    if top.returncode != 0:
        _log("ERROR: not a git repo — fan-out worktrees need a local clone.")
        return 1

    has_integration = _git(
        "rev-parse", "-q", "--verify", "refs/heads/integration", check=False
    )
    if has_integration.returncode != 0:
        _log("[open_fanout_pr] no integration branch; nothing to open")
        return 0

    vs_main = _git("rev-list", "--count", "main..integration", check=False)
    if vs_main.returncode != 0 or vs_main.stdout.strip() == "0":
        _log("[open_fanout_pr] integration has no commits ahead of main; skip PR")
        return 0

    origin = _git("remote", "get-url", "origin", check=False)
    if origin.returncode != 0 or not origin.stdout.strip():
        _log(
            "ERROR: no `origin` remote. Add the GitHub clone:\n"
            "  git remote add origin git@github.com:<org>/<repo>.git"
        )
        return 1

    if shutil.which("gh") is None:
        _log("ERROR: `gh` is not on PATH. Install GitHub CLI and run `gh auth login`.")
        return 1

    push = _git("push", "-u", "origin", "integration", check=False)
    if push.returncode != 0:
        _log(push.stderr or push.stdout)
        _log("ERROR: could not push integration to origin.")
        return 1
    _log("[open_fanout_pr] pushed origin/integration")

    existing = subprocess.run(
        [
            "gh",
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
        ],
        cwd=HERE,
        check=False,
        text=True,
        capture_output=True,
    )
    url = (existing.stdout or "").strip()
    if url:
        _log(f"[open_fanout_pr] existing PR: {url}")
        return 0

    slice_id = _slice_id()
    created = subprocess.run(
        [
            "gh",
            "pr",
            "create",
            "--base",
            "main",
            "--head",
            "integration",
            "--title",
            f"slice {slice_id}: integration → main",
            "--body",
            "Helix `execution-fanout` completed this slice. "
            "Phase-barrier merges are on `integration`. Review here; "
            "do not land onto local `main` until this PR merges.",
        ],
        cwd=HERE,
        check=False,
        text=True,
        capture_output=True,
    )
    if created.returncode != 0:
        _log(created.stderr or created.stdout)
        _log("ERROR: gh pr create failed.")
        return 1

    _log(f"[open_fanout_pr] opened PR: {(created.stdout or '').strip()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
