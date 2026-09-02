"""Merge epic-02..05 Wave-spec entry blocks into reports/plan/wave-spec.execution.yaml."""
from __future__ import annotations

import re
from collections import defaultdict
from pathlib import Path

import yaml

ROOT = Path(__file__).resolve().parents[1]
WI = ROOT / "reports" / "workitems"
SPEC_PATH = ROOT / "reports" / "plan" / "wave-spec.execution.yaml"
BLOCK = re.compile(r"## Wave-spec entry\s*```yaml\s*(.*?)\s*```", re.S)


def main() -> None:
    spec = yaml.safe_load(SPEC_PATH.read_text(encoding="utf-8"))
    produced: dict[str, str] = {}
    for ph in spec["phases"]:
        for t in ph["tasks"]:
            for p in t["produces"]:
                produced[p] = t["id"]

    tasks: list[dict] = []
    missing_block: list[str] = []
    for md in sorted(WI.glob("epic-0[2-5]-*/**/tasks/*.md")):
        text = md.read_text(encoding="utf-8")
        m = BLOCK.search(text)
        if not m:
            if text.lstrip().startswith("# superseded"):
                continue
            missing_block.append(str(md.relative_to(ROOT)))
            continue
        block = yaml.safe_load(m.group(1))
        if isinstance(block, list):
            block = block[0]
        rel = md.relative_to(ROOT).as_posix()
        block["prompt"] = rel
        block.setdefault("status", "live")
        tasks.append(block)

    if missing_block:
        raise SystemExit("missing Wave-spec entry:\n" + "\n".join(missing_block))

    new_prod: dict[str, str] = {}
    for t in tasks:
        for p in t["produces"]:
            if p in produced or p in new_prod:
                raise SystemExit(f"duplicate produces {p!r} from {t['id']}")
            new_prod[p] = t["id"]

    all_prod = {**produced, **new_prod}
    unknown = [(t["id"], d) for t in tasks for d in (t.get("depends_on") or []) if d not in all_prod]
    if unknown:
        raise SystemExit("unknown depends_on: " + repr(unknown[:20]))

    level: dict[str, int] = {}
    remaining = {t["id"]: t for t in tasks}
    for _ in range(200):
        progressed = False
        done: list[str] = []
        for tid, t in remaining.items():
            lvls: list[int] = []
            ok = True
            for d in t.get("depends_on") or []:
                if d in produced:
                    lvls.append(0)
                elif d in new_prod:
                    src = new_prod[d]
                    if src not in level:
                        ok = False
                        break
                    lvls.append(level[src])
                else:
                    ok = False
                    break
            if not ok:
                continue
            level[tid] = (max(lvls) + 1) if lvls else 1
            done.append(tid)
            progressed = True
        for tid in done:
            remaining.pop(tid)
        if not remaining:
            break
        if not progressed:
            raise SystemExit("cycle/stuck: " + repr(list(remaining)[:10]))

    by_lvl: dict[int, list[dict]] = defaultdict(list)
    for t in tasks:
        by_lvl[level[t["id"]]].append(t)

    start = 12
    for i, lvl in enumerate(sorted(by_lvl)):
        spec["phases"].append({"id": f"p{start + i}", "tasks": by_lvl[lvl]})

    lines = ["waveId: wave-v1-demo-r0-r4", "status: planned", "phases:"]
    for ph in spec["phases"]:
        lines.append(f"  - id: {ph['id']}")
        lines.append("    tasks:")
        for t in ph["tasks"]:
            prod = t["produces"] if isinstance(t["produces"], list) else [t["produces"]]
            deps = t.get("depends_on") or []
            prod_s = "[" + ", ".join(prod) + "]"
            dep_s = "[" + ", ".join(deps) + "]"
            prompt = t["prompt"].replace("\\", "/")
            lines.append(
                "      - {id: "
                f"{t['id']}, prompt: {prompt}, produces: {prod_s}, "
                f"depends_on: {dep_s}, effort: {t.get('effort', 'M')}, "
                f"layer: {t.get('layer', 'backend')}, status: {t.get('status', 'live')}"
                "}"
            )
    lines.append("forks: []")
    lines.append("")
    SPEC_PATH.write_text("\n".join(lines), encoding="utf-8", newline="\n")
    print(f"wrote {SPEC_PATH} phases={len(spec['phases'])} r1r4_tasks={len(tasks)}")


if __name__ == "__main__":
    main()
