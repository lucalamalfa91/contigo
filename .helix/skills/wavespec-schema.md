# wave-spec schema — `reports/plan/wave-spec.execution.yaml`

The wave-spec is the **full execution DAG** (cost hub + checker). Passata 2
does **not** walk this file. Overnight execution walks
`reports/plan/slice.current.yaml`, a copy of one `reports/plan/slices/<id>.yaml`
produced by `scripts/cut_nightly_slices.py`. Deleting the master still
breaks document load if anything still referenced it; `execution-fanout.over`
is the slice file and **must** exist (placeholder `nightly-slice-unset`).

## Grammar (unknown key fails the load)

```yaml
waveId: wave-v1-demo-r0-r4       # not wave-r0-foundation once R1–R4 exist
status: planned                 # planned | active | done
phases:
  - id: 1                       # integer (not p1 / phase-1)
    name: phase-1               # required
    tasks:
      - id: E01/F01/US01/T01
        prompt: reports/workitems/epic-01-<slug>/feature-01-<slug>/us-01-<slug>/tasks/task-01-<slug>.md
        produces: [github-org-repos]    # kebab-case ARTIFACT NAMES, never file paths
        depends_on: []                  # produced in a STRICTLY EARLIER phase
        effort: S                       # S | M | L only — never tokens:
        layer: backend                  # optional: backend | frontend
        status: live                    # live | skip | spike | gated
forks: []                       # a fork with decision: null BLOCKS the run
```

Helix `TaskNodeDef` is `_Strict` (`extra="forbid"`). Do **not** add a
`tokens:` key on tasks. Nightly packing maps `S|M|L` → tokens in
`scripts/cut_nightly_slices.py` (default 0.5 / 1.0 / 1.8 M, cap 8.0 M of
the Max 20x 10 M window). Token totals live in `slices/MANIFEST.yaml`
(not a HelixDocument) and as a `# tokens:` comment on each slice YAML.

## Loader rules

1. **Acyclic** — `produces` / `depends_on` form a DAG.
2. **Phase consistency** — every `depends_on` is produced in a **strictly earlier** phase.
3. **Producer completeness** — every `depends_on` is produced by exactly one task.
4. **Fork resolution** — `decision: null` blocks before any git state exists.
5. **Live-set closure** — no `live` task depends on an artifact only a
   `skip` / `spike` / `gated` task produces.

## `prompt` is artifact-relative

There is no plan-publisher and no `$BITFLOW_TARGET_REPO`. Implementer and
reviewer run with cwd = this artifact folder, so `prompt` points at
`reports/workitems/…/tasks/….md`. A `.bit-flow/` path here is a defect.

`produces` / `depends_on` are **names**, not paths.

## Placeholder (committed so the document loads)

```yaml
waveId: placeholder
status: planned
phases: []
forks: []
```

`auto_level_phases: true` on `execution-fanout` re-derives phases from the DAG.
Get the dependency edges right; the buckets follow.
