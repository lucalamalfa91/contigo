#!/usr/bin/env python3
"""Ensure `.helix` is its own git toplevel on `main` (fan-out isolation).

`execution-fanout` uses `isolation: git-worktree` + `base_branch: main`.
Helix walks up from the artifact dir (`run_repo.py::require_git_repo`). If
this folder is not a repo, that walk finds `helix-artifacts` and would
provision worktrees of the whole artifacts monorepo.

Dedicated-run-repo (no `base_branch`) is the wrong mode here: `output_dir`
is `reports/`, so the session repo would not contain `workspace/<repo>/`.

Usage (cwd may be anywhere):
  python scripts/ensure_artifact_git.py
"""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parents[1]
_HELIX_GIT_EMAIL = "helix@localhost"
_HELIX_GIT_NAME = "Helix Runner"


def _git(*args: str, check: bool = True) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", *args],
        cwd=HERE,
        check=check,
        text=True,
        capture_output=True,
    )


def _toplevel() -> Path | None:
    proc = _git("rev-parse", "--show-toplevel", check=False)
    if proc.returncode != 0:
        return None
    return Path(proc.stdout.strip())


def main() -> int:
    top = _toplevel()
    here = HERE.resolve()
    if top is not None and top.resolve() == here:
        branches = _git("branch", "--list", "main").stdout.strip()
        if not branches:
            print(
                "ERROR: .helix is a git repo but has no 'main' branch "
                "(execution-fanout base_branch: main).",
                file=sys.stderr,
            )
            return 1
        print("[ensure_artifact_git] .helix is its own git toplevel on main")
        return 0

    if top is not None:
        print(
            f"[ensure_artifact_git] nested init: parent toplevel is {top}",
        )

    _git("init", "-b", "main")
    _git("config", "user.email", _HELIX_GIT_EMAIL)
    _git("config", "user.name", _HELIX_GIT_NAME)
    _git("add", "-A")
    status = _git("status", "--porcelain").stdout.strip()
    if status:
        _git(
            "commit",
            "-m",
            "helix: initialize artifact repo for fan-out worktrees",
        )
    else:
        _git(
            "commit",
            "--allow-empty",
            "-m",
            "helix: initialize artifact repo for fan-out worktrees",
        )
    print("[ensure_artifact_git] initialized .helix git repo on main")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
