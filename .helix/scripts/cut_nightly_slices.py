#!/usr/bin/env python3
"""Cut wave-spec.execution.yaml into one Helix wave-spec per epic.

One wave per epic (e01…e05). Effort S/M/L is mapped to tokens for an
estimate only — not a packing cap. Default omits E01/F01 and E01/F02
(already closed on integration). Pass --all to include them.

depends_on edges that leave the slice are dropped so producer-completeness
holds.

Usage (cwd = .helix):
  python scripts/cut_nightly_slices.py
  python scripts/cut_nightly_slices.py --all
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path

HERE = Path(__file__).resolve().parents[1]
MASTER = HERE / "reports" / "plan" / "wave-spec.execution.yaml"
OUT_DIR = HERE / "reports" / "plan" / "slices"
CURRENT = HERE / "reports" / "plan" / "slice.current.yaml"
MANIFEST = OUT_DIR / "MANIFEST.yaml"

TOKENS_BY_EFFORT = {"S": 500_000, "M": 1_000_000, "L": 1_800_000}
OMIT_CLOSED_FEATURES = ("E01/F01", "E01/F02")
EPIC_TITLE = {
    "E01": "platform",
    "E02": "extraction / contract 360",
    "E03": "renewal",
    "E04": "savings",
    "E05": "quotes / Day-1",
}

LAYER_MAP = {
    "infra": "backend",
    "backend": "backend",
    "web": "frontend",
    "mobile": "frontend",
    "frontend": "frontend",
}

TASK_RE = re.compile(
    r"\{id: (?P<id>[^,]+), prompt: (?P<prompt>[^,]+), produces: \[(?P<produces>[^\]]*)\], "
    r"depends_on: \[(?P<depends>[^\]]*)\], effort: (?P<effort>[SML]), layer: (?P<layer>\w+), "
    r"status: (?P<status>\w+)\}"
)
STORY_RE = re.compile(r"^(E\d+/F\d+/US\d+)/")
FEATURE_SLUG_RE = re.compile(r"/feature-\d+-([^/]+)/")


def _split_list(raw: str) -> list[str]:
    raw = raw.strip()
    if not raw:
        return []
    return [p.strip() for p in raw.split(",") if p.strip()]


def helix_layer(raw: str) -> str:
    mapped = LAYER_MAP.get(raw)
    if mapped is None:
        raise SystemExit(f"unknown layer {raw!r}; WaveSpecDef allows backend|frontend")
    return mapped


def normalize_master(text: str) -> str:
    """Rewrite master YAML to Helix WaveSpecDef (int phase id, name, backend|frontend)."""

    def phase_sub(match: re.Match[str]) -> str:
        n = match.group(1)
        return f"  - id: {n}\n    name: phase-{n}"

    out = re.sub(r"^  - id: p(\d+)\s*$", phase_sub, text, flags=re.M)

    def layer_sub(match: re.Match[str]) -> str:
        return f"layer: {helix_layer(match.group(1))}"

    out = re.sub(r"layer: (\w+)", layer_sub, out)
    return out


def parse_master(text: str) -> list[tuple[str, dict]]:
    """Return [(original_phase_id, task_dict), ...] in file order."""
    rows: list[tuple[str, dict]] = []
    current_phase = ""
    for line in text.splitlines():
        mphase = re.match(r"^  - id: (?:p)?(\d+)\s*$", line)
        if mphase:
            current_phase = mphase.group(1)
            continue
        mt = TASK_RE.search(line)
        if not mt:
            continue
        if not current_phase:
            raise SystemExit("task before any phase")
        g = mt.groupdict()
        rows.append(
            (
                current_phase,
                {
                    "id": g["id"].strip(),
                    "prompt": g["prompt"].strip(),
                    "produces": _split_list(g["produces"]),
                    "depends_on": _split_list(g["depends"]),
                    "effort": g["effort"],
                    "layer": helix_layer(g["layer"]),
                    "status": g["status"],
                },
            )
        )
    return rows


def task_tokens(task: dict) -> int:
    effort = task.get("effort") or "M"
    try:
        return TOKENS_BY_EFFORT[effort]
    except KeyError as exc:
        raise SystemExit(f"task {task['id']}: unknown effort {effort!r}") from exc


def story_id_of(task_id: str) -> str:
    m = STORY_RE.match(task_id)
    if not m:
        raise SystemExit(f"task id {task_id!r} is not E##/F##/US##/T##")
    return m.group(1)


def epic_id_of(task_id: str) -> str:
    return task_id.split("/", 1)[0]


def feature_id_of(task_id: str) -> str:
    parts = task_id.split("/")
    return f"{parts[0]}/{parts[1]}"


def story_sort_key(sid: str) -> tuple[int, int, int]:
    e, f, us = sid.split("/")
    return (int(e[1:]), int(f[1:]), int(us[2:]))


def letter_suffix(index: int) -> str:
    """0→a, 25→z, 26→aa."""
    if index < 26:
        return chr(ord("a") + index)
    return letter_suffix(index // 26 - 1) + chr(ord("a") + index % 26)


def fmt_millions(n: int) -> str:
    return f"{n / 1_000_000:.1f}M"


def feature_slug(task: dict) -> str:
    m = FEATURE_SLUG_RE.search(task["prompt"])
    if m:
        return m.group(1).replace("-", " ")
    return feature_id_of(task["id"])


def is_integration_story(tasks: list[dict]) -> bool:
    return any("final-integration" in t["prompt"] for t in tasks)


@dataclass
class Story:
    id: str
    tasks: list[tuple[str, dict]] = field(default_factory=list)

    @property
    def tokens(self) -> int:
        return sum(task_tokens(t) for _, t in self.tasks)

    @property
    def feature(self) -> str:
        return feature_id_of(self.id)

    @property
    def epic(self) -> str:
        return epic_id_of(self.id)

    @property
    def integration(self) -> bool:
        return is_integration_story([t for _, t in self.tasks])


@dataclass
class PackedSlice:
    slice_id: str
    title: str
    stories: list[Story]
    previous: str | None
    checks: list[str]

    @property
    def tokens(self) -> int:
        return sum(s.tokens for s in self.stories)

    @property
    def selected(self) -> list[tuple[str, dict]]:
        return [row for s in self.stories for row in s.tasks]


def drop_external_deps(selected: list[tuple[str, dict]]) -> list[tuple[str, dict]]:
    produced = {a for _, t in selected for a in t["produces"]}
    out: list[tuple[str, dict]] = []
    for ph, t in selected:
        t2 = dict(t)
        t2["depends_on"] = [d for d in t["depends_on"] if d in produced]
        out.append((ph, t2))
    return out


def dump_task(t: dict) -> str:
    prod = ", ".join(t["produces"])
    deps = ", ".join(t["depends_on"])
    return (
        f"      - {{id: {t['id']}, prompt: {t['prompt']}, produces: [{prod}], "
        f"depends_on: [{deps}], effort: {t['effort']}, layer: {t['layer']}, "
        f"status: {t['status']}}}"
    )


def emit_yaml(slice_id: str, title: str, selected: list[tuple[str, dict]], tokens: int) -> str:
    phases: dict[str, list[dict]] = defaultdict(list)
    order: list[str] = []
    for ph, t in selected:
        if ph not in phases:
            order.append(ph)
        phases[ph].append(t)
    lines = [
        f"waveId: wave-v1-epic-{slice_id}",
        "status: planned",
        f"# {title}. Prior-wave depends_on dropped (producer-completeness).",
        f"# estimated tokens: {fmt_millions(tokens)} (S/M/L mapped; not a packing cap).",
        f"# Launch: ./run.ps1 -Max -Slice {slice_id} -o execution-fanout",
        "phases:",
    ]
    for i, ph in enumerate(order, start=1):
        lines.append(f"  - id: {i}")
        lines.append(f"    name: phase-{i}")
        lines.append("    tasks:")
        for t in phases[ph]:
            lines.append(dump_task(t))
    lines.append("forks: []")
    lines.append("")
    return "\n".join(lines)


def slice_checks(*, is_first: bool, is_integration: bool, tasks: list[dict]) -> list[str]:
    """Launch-gate checks only — what Helix needs to *start*. Azure/HCP are
    per-task (`requires:` on the work-item). Never fail the slice because a
    later task needs a credential this task does not."""
    checks = ["github_auth", "github_org"]
    if not is_first:
        checks.append("github_repos")
        checks.append("hitl_previous")
    return checks


def title_for_epic(epic: str, stories: list[Story], *, omitted_closed: bool) -> str:
    label = EPIC_TITLE.get(epic, epic)
    if omitted_closed and epic == "E01":
        return f"{epic} {label} remainder (F03–F09; F01–F02 already closed)"
    return f"{epic} {label}"


def pack_all(
    rows: list[tuple[str, dict]],
    *,
    omit_features: frozenset[str],
) -> tuple[list[PackedSlice], list[str]]:
    live_rows = [
        (ph, t)
        for ph, t in rows
        if t["status"] == "live" and feature_id_of(t["id"]) not in omit_features
    ]
    stories_by_id: dict[str, Story] = {}
    story_order: list[str] = []
    for ph, t in live_rows:
        sid = story_id_of(t["id"])
        if sid not in stories_by_id:
            stories_by_id[sid] = Story(id=sid)
            story_order.append(sid)
        stories_by_id[sid].tasks.append((ph, t))

    by_epic: dict[str, list[Story]] = defaultdict(list)
    epic_order: list[str] = []
    for sid in sorted(story_order, key=story_sort_key):
        story = stories_by_id[sid]
        if story.epic not in epic_order:
            epic_order.append(story.epic)
        by_epic[story.epic].append(story)

    packed: list[PackedSlice] = []
    previous: str | None = None
    omitted_closed = bool(omit_features)
    for i, epic in enumerate(epic_order):
        group = by_epic[epic]
        slice_id = epic.lower()
        title = title_for_epic(epic, group, omitted_closed=omitted_closed)
        # F01 already landed: e01 is not the bootstrap slice.
        is_first = i == 0 and not omitted_closed
        checks = slice_checks(is_first=is_first, is_integration=False, tasks=[])
        packed.append(
            PackedSlice(
                slice_id=slice_id,
                title=title,
                stories=group,
                previous=previous,
                checks=checks,
            )
        )
        previous = slice_id
    return packed, []


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description="Cut one Helix wave-spec per epic")
    ap.add_argument(
        "--all",
        action="store_true",
        help="include E01/F01 and E01/F02 (default: omit; already closed)",
    )
    args = ap.parse_args(argv)
    omit = frozenset() if args.all else frozenset(OMIT_CLOSED_FEATURES)

    if not MASTER.exists():
        print(f"missing {MASTER}", file=sys.stderr)
        return 1
    text = normalize_master(MASTER.read_text(encoding="utf-8"))
    MASTER.write_text(text, encoding="utf-8")
    if "waveId: placeholder" in text or "phases: []" in text.split("waveId")[-1][:80]:
        print("master wave-spec is still a placeholder; skip slice cut", file=sys.stderr)
        return 1
    rows = parse_master(text)
    scoped_live = [
        t
        for _, t in rows
        if t["status"] == "live" and feature_id_of(t["id"]) not in omit
    ]
    packed, warnings = pack_all(rows, omit_features=omit)
    for w in warnings:
        print(f"WARNING: {w}", file=sys.stderr)

    assigned: dict[str, str] = {}
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    keep: set[str] = set()
    index_rows = [
        "# Epic waves",
        "",
        "Launch: `./run.ps1 -Max -Slice e01 -o execution-fanout`",
        "",
        "One Helix wave per epic. Default omits E01/F01 and E01/F02 "
        "(already closed). Re-include with `--all`.",
        "",
        f"Estimated tokens (S={fmt_millions(TOKENS_BY_EFFORT['S'])} "
        f"M={fmt_millions(TOKENS_BY_EFFORT['M'])} "
        f"L={fmt_millions(TOKENS_BY_EFFORT['L'])}); not a packing cap.",
        "",
        "| Slice | Tasks | Tokens | Title |",
        "|-------|-------|--------|-------|",
    ]
    manifest_slices: list[dict] = []
    for sl in packed:
        selected = drop_external_deps(sl.selected)
        if not selected:
            print(f"slice {sl.slice_id} matched 0 tasks", file=sys.stderr)
            return 1
        for _, t in selected:
            if t["id"] in assigned:
                print(f"task {t['id']} in {assigned[t['id']]} and {sl.slice_id}", file=sys.stderr)
                return 1
            assigned[t["id"]] = sl.slice_id
        path = OUT_DIR / f"{sl.slice_id}.yaml"
        path.write_text(
            emit_yaml(sl.slice_id, sl.title, selected, sl.tokens),
            encoding="utf-8",
        )
        keep.add(path.name)
        index_rows.append(
            f"| `{sl.slice_id}` | {len(selected)} | {fmt_millions(sl.tokens)} | {sl.title} |"
        )
        manifest_slices.append(
            {
                "id": sl.slice_id,
                "title": sl.title,
                "previous": sl.previous,
                "tokens": sl.tokens,
                "tasks": len(selected),
                "epic": sl.stories[0].epic,
                "integration": all(s.integration for s in sl.stories),
                "checks": sl.checks,
                "stories": [s.id for s in sl.stories],
            }
        )

    missing = [t["id"] for t in scoped_live if t["id"] not in assigned]
    extra = [tid for tid in assigned if tid not in {t["id"] for t in scoped_live}]
    if missing or extra:
        print(f"coverage error missing={missing} extra={extra}", file=sys.stderr)
        return 1

    for stale in list(OUT_DIR.glob("r*.yaml")) + list(OUT_DIR.glob("e*.yaml")):
        if stale.name not in keep:
            stale.unlink()

    (OUT_DIR / "INDEX.md").write_text("\n".join(index_rows) + "\n", encoding="utf-8")
    manifest = {
        "mode": "epic",
        "omit_features": sorted(omit),
        "tokens_s": TOKENS_BY_EFFORT["S"],
        "tokens_m": TOKENS_BY_EFFORT["M"],
        "tokens_l": TOKENS_BY_EFFORT["L"],
        "slices": manifest_slices,
    }
    MANIFEST.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    if packed:
        CURRENT.write_text(
            (OUT_DIR / f"{packed[0].slice_id}.yaml").read_text(encoding="utf-8"),
            encoding="utf-8",
        )
    print(
        f"wrote {len(packed)} epic waves covering {len(assigned)} live tasks "
        f"(omitted {sorted(omit) or 'none'}) -> {OUT_DIR}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
