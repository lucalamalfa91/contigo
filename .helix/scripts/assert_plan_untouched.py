#!/usr/bin/env python3
"""Snapshot / verify that the live R0–R4 plan was not rewritten.

Hash-locks e01–e05, wave-spec.execution.yaml, ADR-001…017, and epic-01…05.
Does NOT lock slice.current.yaml (live fan-out may write it).
INDEX.md / BACKLOG.md may be appended; we only require existing rows to remain.

Usage (cwd = .helix):
  python scripts/assert_plan_untouched.py snapshot
  python scripts/assert_plan_untouched.py verify
"""

from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parents[1]
SNAP = HERE / "reports" / "plan" / ".web-protect-snapshot.json"

SLICE_IDS = ("e01", "e02", "e03", "e04", "e05")
EPIC_GLOBS = ("epic-01-*", "epic-02-*", "epic-03-*", "epic-04-*", "epic-05-*")


def _sha256(path: Path) -> str:
    h = hashlib.sha256()
    h.update(path.read_bytes())
    return h.hexdigest()


def _rel(path: Path) -> str:
    return path.relative_to(HERE).as_posix()


def _locked_files() -> list[Path]:
    out: list[Path] = [
        HERE / "reports" / "plan" / "wave-spec.execution.yaml",
    ]
    slices = HERE / "reports" / "plan" / "slices"
    for sid in SLICE_IDS:
        out.append(slices / f"{sid}.yaml")
    arch = HERE / "reports" / "architecture"
    for n in range(1, 18):
        matches = sorted(arch.glob(f"ADR-{n:03d}*.md"))
        out.extend(p for p in matches if p.parent == arch)
    work = HERE / "reports" / "workitems"
    for pat in EPIC_GLOBS:
        for epic_dir in sorted(work.glob(pat)):
            if epic_dir.is_dir():
                out.extend(sorted(p for p in epic_dir.rglob("*") if p.is_file()))
    return out


def snapshot() -> int:
    files = _locked_files()
    missing = [str(_rel(p)) for p in files if not p.is_file()]
    if missing:
        print("snapshot: missing protected files:", file=sys.stderr)
        for m in missing:
            print(f"  {m}", file=sys.stderr)
        return 1
    payload = {
        "files": {_rel(p): _sha256(p) for p in files},
        "index_must_contain": [f"ADR-{n:03d}" for n in range(1, 18)],
        "backlog_must_contain": [
            "epic-01",
            "epic-02",
            "epic-03",
            "epic-04",
            "epic-05",
        ],
    }
    SNAP.parent.mkdir(parents=True, exist_ok=True)
    SNAP.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    print(f"snapshot: {len(payload['files'])} protected files")
    return 0


def verify() -> int:
    if not SNAP.is_file():
        print("verify: missing snapshot — run snapshot first", file=sys.stderr)
        return 1
    payload = json.loads(SNAP.read_text(encoding="utf-8"))
    broken: list[str] = []
    for rel, expected in payload["files"].items():
        path = HERE / rel
        if not path.is_file():
            broken.append(f"DELETED {rel}")
            continue
        actual = _sha256(path)
        if actual != expected:
            broken.append(f"CHANGED {rel}")
    index = HERE / "reports" / "architecture" / "INDEX.md"
    if index.is_file():
        text = index.read_text(encoding="utf-8")
        for needle in payload.get("index_must_contain", []):
            if needle not in text:
                broken.append(f"INDEX.md lost {needle}")
    else:
        broken.append("DELETED reports/architecture/INDEX.md")
    backlog = HERE / "reports" / "workitems" / "BACKLOG.md"
    if backlog.is_file():
        text = backlog.read_text(encoding="utf-8")
        for needle in payload.get("backlog_must_contain", []):
            if needle not in text:
                broken.append(f"BACKLOG.md lost {needle}")
    if broken:
        print("verify: live plan was mutated — abort", file=sys.stderr)
        for b in broken:
            print(f"  {b}", file=sys.stderr)
        return 1
    print("verify: protected R0–R4 plan unchanged")
    return 0


def main() -> int:
    if len(sys.argv) != 2 or sys.argv[1] not in {"snapshot", "verify"}:
        print("usage: assert_plan_untouched.py snapshot|verify", file=sys.stderr)
        return 2
    return snapshot() if sys.argv[1] == "snapshot" else verify()


if __name__ == "__main__":
    raise SystemExit(main())
