#!/usr/bin/env python3
"""Deterministic cost rollup. Never trust LLM arithmetic for totals.

Reads reports/costs/{infra,product-ai,coding-agent}.json (and optional
cost-lines.json for coverage checks). Writes reports/costs/rollup.json.

Exit 1 if a spoke file is missing, JSON is invalid, or a priced line has
amount set without source_url+source_date (invented prices).
TODO/null amounts are allowed and listed -- fail closed into open questions,
not into fake totals.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

AXES = ("infra", "product-ai", "coding-agent")
ROOT = Path(__file__).resolve().parent.parent
COSTS = ROOT / "reports" / "costs"


def _load(name: str) -> dict:
    path = COSTS / name
    if not path.is_file():
        raise SystemExit(f"missing {path.relative_to(ROOT)}")
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        raise SystemExit(f"invalid JSON {path.name}: {exc}") from exc
    if not isinstance(data, dict):
        raise SystemExit(f"{path.name}: expected object")
    return data


def _num(value) -> float | None:
    if value is None:
        return None
    if isinstance(value, bool):
        raise SystemExit("boolean is not a price")
    if isinstance(value, (int, float)):
        return float(value)
    raise SystemExit(f"non-numeric amount: {value!r}")


def _axis_totals(data: dict, expected_axis: str) -> dict:
    axis = data.get("axis")
    if axis != expected_axis:
        raise SystemExit(f"{expected_axis}.json axis={axis!r}")
    currency = data.get("currency") or "EUR"
    lines = data.get("lines") or []
    if not isinstance(lines, list):
        raise SystemExit(f"{expected_axis}: lines must be a list")

    monthly = 0.0
    monthly_idle = 0.0
    monthly_expected = 0.0
    one_off = 0.0
    priced = 0
    todos = list(data.get("todos") or [])
    defects: list[str] = []

    for line in lines:
        if not isinstance(line, dict):
            defects.append("non-object line")
            continue
        lid = line.get("id") or "?"
        amount = _num(line.get("amount"))
        url = (line.get("source_url") or "").strip()
        date = (line.get("source_date") or "").strip()
        if amount is not None and not (url and date):
            defects.append(f"{lid}: amount set without source_url+source_date")
            continue
        if amount is None:
            todos.append(f"{lid}: unpriced")
            continue
        priced += 1
        cadence = (line.get("cadence") or "monthly").lower()
        if cadence == "one_off":
            one_off += amount
        else:
            monthly += amount
            idle = _num(line.get("idle_amount"))
            expected = _num(line.get("expected_amount"))
            monthly_idle += idle if idle is not None else amount
            monthly_expected += expected if expected is not None else amount

    if defects:
        raise SystemExit("price defects:\n  - " + "\n  - ".join(defects))

    return {
        "axis": expected_axis,
        "currency": currency,
        "as_of": data.get("as_of"),
        "priced_lines": priced,
        "todo_count": len(todos),
        "todos": todos,
        "assumptions": data.get("assumptions") or [],
        "monthly": round(monthly, 4),
        "monthly_idle": round(monthly_idle, 4),
        "monthly_expected": round(monthly_expected, 4),
        "one_off": round(one_off, 4),
    }


def main() -> int:
    COSTS.mkdir(parents=True, exist_ok=True)
    axes = {name: _axis_totals(_load(f"{name}.json"), name) for name in AXES}
    currencies = {axes[n]["currency"] for n in AXES}
    if len(currencies) > 1:
        raise SystemExit(f"mixed currencies: {sorted(currencies)}")
    currency = next(iter(currencies))

    rollup = {
        "currency": currency,
        "infra": axes["infra"],
        "product_ai": axes["product-ai"],
        "coding_agent": axes["coding-agent"],
        "monthly_run_idle": round(
            axes["infra"]["monthly_idle"] + axes["product-ai"]["monthly_idle"], 4
        ),
        "monthly_run_expected": round(
            axes["infra"]["monthly_expected"] + axes["product-ai"]["monthly_expected"], 4
        ),
        "one_off_build": axes["coding-agent"]["one_off"],
        "todo_count": sum(axes[n]["todo_count"] for n in AXES),
    }
    out = COSTS / "rollup.json"
    out.write_text(json.dumps(rollup, indent=2, ensure_ascii=True) + "\n", encoding="utf-8")
    print(json.dumps(rollup, indent=2, ensure_ascii=True))
    print(f"wrote {out.relative_to(ROOT)}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
