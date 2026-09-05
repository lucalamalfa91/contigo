#!/usr/bin/env python3
"""Cut wave-spec.web.yaml into slices/e06.yaml, e07.yaml, … only.

Never writes slice.current.yaml (live fan-out).
Never overwrites slices/e01.yaml–e05.yaml or wave-spec.execution.yaml.

Usage (cwd = .helix):
  python scripts/cut_web_slices.py
"""

from __future__ import annotations

import sys
from pathlib import Path

HERE = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(HERE / "scripts"))

import cut_nightly_slices as base  # noqa: E402

MASTER = HERE / "reports" / "plan" / "wave-spec.web.yaml"
OUT_DIR = HERE / "reports" / "plan" / "slices"
INDEX_WEB = OUT_DIR / "INDEX-web.md"
MANIFEST_WEB = OUT_DIR / "MANIFEST-web.yaml"
PROTECTED_SLICES = frozenset({"e01", "e02", "e03", "e04", "e05"})


def main() -> int:
    if not MASTER.exists():
        print(f"missing {MASTER}", file=sys.stderr)
        return 1
    text = MASTER.read_text(encoding="utf-8")
    if "waveId: placeholder" in text or "phases: []" in text:
        print("wave-spec.web.yaml is still a placeholder; skip slice cut", file=sys.stderr)
        return 1
    rows = base.parse_master(text)
    live = [(ph, t) for ph, t in rows if t["status"] == "live"]
    if not live:
        print("no live tasks in wave-spec.web.yaml", file=sys.stderr)
        return 1
    for _, t in live:
        epic = base.epic_id_of(t["id"])
        n = int(epic[1:])
        if n < 6:
            print(
                f"refusing to slice {t['id']}: web cutter starts at E06",
                file=sys.stderr,
            )
            return 1
    packed, _warnings = base.pack_all(live, omit_features=frozenset())
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    written: list[str] = []
    for sl in packed:
        if sl.slice_id in PROTECTED_SLICES:
            print(f"refusing to overwrite protected slice {sl.slice_id}", file=sys.stderr)
            return 1
        selected = base.drop_external_deps(sl.selected)
        body = base.emit_yaml(sl.slice_id, sl.title, selected, sl.tokens)
        path = OUT_DIR / f"{sl.slice_id}.yaml"
        path.write_text(body, encoding="utf-8")
        written.append(sl.slice_id)

    lines = [
        "# Web-delta nightly slices (e06+). Backend e01–e05 are owned by",
        "# cut_nightly_slices.py / wave-spec.execution.yaml — do not mix.",
        "",
        f"mode: web-delta",
        f"master: reports/plan/wave-spec.web.yaml",
        "slices:",
    ]
    for sl in packed:
        lines.append(f"  - id: {sl.slice_id}")
        lines.append(f"    title: {sl.title}")
        lines.append(f"    tokens: {sl.tokens}")
        lines.append(f"    tasks: {len(sl.selected)}")
    MANIFEST_WEB.write_text("\n".join(lines) + "\n", encoding="utf-8")

    idx = [
        "# Web slices (wave 6+)",
        "",
        "Produced by `python scripts/cut_web_slices.py`.",
        "Launch later (when the live backend wave is idle):",
        "",
        "```",
        "./run.ps1 -Max -Slice e06 -o execution-fanout",
        "```",
        "",
        "Do not point this cutter at `slice.current.yaml`.",
        "",
        "## Files",
        "",
    ]
    for sid in written:
        idx.append(f"- `{sid}.yaml`")
    INDEX_WEB.write_text("\n".join(idx) + "\n", encoding="utf-8")
    print("wrote", ", ".join(written))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
