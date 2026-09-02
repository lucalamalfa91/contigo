#!/usr/bin/env python3
"""fan_out.merge_verify — reject a barrier resolution that still has markers.

Runs in the integration worktree (product clone root). Must not require
MERGE_HEAD to be absent (the auto pass verifies before it commits).

Exit 0: no leftover conflict markers in tracked text files.
Exit 1: at least one tracked file still contains <<<<<<< or >>>>>>>.
"""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

MARKERS = ("<<<<<<<", ">>>>>>>")


def tracked_files(repo_root: Path) -> list[str]:
    out = subprocess.check_output(
        ["git", "ls-files", "-z"],
        cwd=repo_root,
        text=True,
    )
    return [p for p in out.split("\0") if p]


def files_with_markers(repo_root: Path, paths: list[str]) -> list[str]:
    hit: list[str] = []
    for rel in paths:
        path = repo_root / rel
        try:
            text = path.read_text(encoding="utf-8")
        except (OSError, UnicodeDecodeError):
            continue
        if any(marker in text for marker in MARKERS):
            hit.append(rel)
    return hit


def main() -> int:
    root = Path.cwd()
    dirty = files_with_markers(root, tracked_files(root))
    if dirty:
        print("merge_verify: leftover conflict markers:", file=sys.stderr)
        for rel in dirty:
            print(f"  {rel}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
