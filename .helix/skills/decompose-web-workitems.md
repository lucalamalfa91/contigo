# Decomposition — web delta only (epic-06 / wave 6+)

Append **web** work. Never rebuild R0–R4 backend.

## Locked baseline (do not rewrite)

```
reports/architecture/ADR-001*.md … ADR-017*.md
reports/workitems/epic-01-*/ … epic-05-*/
reports/plan/wave-spec.execution.yaml
reports/plan/slices/e01.yaml … e05.yaml
reports/plan/slice.current.yaml          # LIVE fan-out — do not write
```

A later run **appends**. It never renumbers epic-01…05 or slices e01…e05.

**Never `write_file` / `rm` these (launcher hash-locks them):**

```
reports/workitems/epic-01-*/ … epic-05-*/     # every file
reports/architecture/ADR-001*.md … ADR-017*.md
reports/plan/wave-spec.execution.yaml
reports/plan/slices/e01.yaml … e05.yaml
reports/plan/slices/INDEX.md                  # backend index — write INDEX-web.md
reports/plan/slices/MANIFEST.yaml             # write MANIFEST-web.yaml
reports/plan/slice.current.yaml
```

`BACKLOG.md` / architecture `INDEX.md`: **read full file, append a section, write the complete file back**. Never a replacement that drops epic-01…05 or ADR-001…017.

Never run `cut_nightly_slices.py`. Bash is only `python scripts/cut_web_slices.py`.

## What you write

```
reports/workitems/epic-06-<slug>/        # and epic-07+ if needed
reports/plan/wave-spec.web.yaml          # web DAG only
reports/plan/slices/e06.yaml             # and e07.yaml, …
reports/plan/slices/INDEX-web.md
reports/plan/slices/MANIFEST-web.yaml
```

Ids start at **E06/F01/US01/T01**. `layer: web`. `target_repo` / folder `web/`.

## Rules

- Every spec §16 row and §20 Day-1 step has a **web** story. “API exists” ≠ done.
- Last story of the last web epic is `us-NN-final-integration` (one task):
  browser Day-1 on `demo`.
- Each screen story cites `inputs/design/` (Claude Design handoff).
- Regen the TypeScript client when OpenAPI grew in E02–E05 — a chore story is fine.
- Thin API gap tasks are allowed only if an accepted web ADR named the gap.
- After writing `wave-spec.web.yaml`, run **only**:

```
python scripts/cut_web_slices.py
```

That script must not touch `slice.current.yaml` or `e01`–`e05`.

## Checker fail conditions (web)

- epic-06 missing, or any new task `layer: backend` that is not a named API gap
- a write to `slices/e01.yaml`–`e05.yaml` or `slice.current.yaml`
- `wave-spec.execution.yaml` overwritten
- fewer than one nightly web slice (`e06.yaml`)
- final-integration missing on the last web epic
- Claude Design path not cited on screen tasks
