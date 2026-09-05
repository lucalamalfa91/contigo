---
name: helix-process-authoring
description: How the Helix YAML works and how to express a process (a software-development lifecycle, a legacy-investigation flow, an incident loop) as a Helix artifact. Part 1 is the complete YAML surface block by block — models, mcp_servers, tools, skills, agents, harness, hooks, defaults, orchestrations, fan_out. Part 2 maps each process element (a phase, a gate, an I/O hand-off, a branch, a loop-back, a coding step, a per-item fan-out) to the pattern that reproduces it, or to the declared divergence when Helix v0.1 cannot. Grounded in the authoring manual, with the runtime code as the final authority.
---

# Helix process authoring — the YAML, and how to lower a process onto it

> **The manual is the guide** — read it, it is authoritative on *what the parser accepts and
> runs today* and it marks INERT fields explicitly:
> `/Users/christian.soliman/Repos/helix/Docs/Manuals/authoring-helix-artifacts.md`
> (referenced below as **`MANUAL §n`**).
>
> **The runtime code is the truth** when the two disagree: `$HELIX/src/backend/helix/`
> (`$HELIX` = `/Users/christian.soliman/Repos/helix`). The manual's own §15 closing table maps
> each topic to its source file — `contract/schema.py` (grammar), `contract/validate.py`
> (refs), `orchestration/builders.py` (topologies+edges), `orchestration/registry.py`
> (callables), `orchestration/marker_guard.py` (close gates),
> `orchestration/phase_context.py` (hand-off), `agent/tool_registry.py` (built-in tools),
> `coding_agent/binding.py` (DOC006), `runner/launch.py::_bind_one` (the entry point).
>
> Section numbers here follow the **current** manual (orchestrations are §12, harness §9,
> hooks §10). Anything citing §11 for orchestrations is a stale copy.

---

# Part 1 — The YAML surface

## §1 The mental model

One YAML document (`HelixDocument`). A run walks five phases:

```
parse  →  validate refs  →  bind to MAF objects  →  drive the run loop  →  collect text
```

- **parse** — Pydantic v2 grammar; every section rejects unknown keys (`extra="forbid"`), so a
  typo is a validation error, not a silently dropped field. The **only** permissive section is
  `harness` (§9).
- **validate refs** — ids unique across the whole document; every ref resolves. A dangling ref
  raises `ReferenceError` and the run never starts. For a `fan_out` document this step also
  touches the filesystem (the `over:` wave-spec must exist).
- **bind** → MAF objects; **run** → the loop; **collect** → text.

Running it:

```bash
cd $HELIX/src/backend
uv run helix run <artifact.yaml> --input "..."          # mono agent
uv run helix run <artifact.yaml> -o <orchestration-id>  # a topology
uv run helix run <artifact.yaml> --agent <agent-id>     # pick the mono agent
```

Flags that matter: `-i/--input`, `-a/--agent`, `-o/--orchestration`, `--base-url`,
`--api-key`, `--model`, `--session-mode` (`new|continue|auto`, fan-out), `--session`,
`-w/--working-dir` (default: the artifact's own directory).

Other subcommands: `helix shell` (REPL over the CWD), `helix validate-wavespec <file>`,
`helix compile <flow.md> [--dry-run]` (NL→YAML generator). **There is no `helix validate` for
an artifact** — use the loader one-liner in "Verification" below.

> **All relative paths** (`instructions_file`, `output_dir`, `tools[].path`, `fan_out.over`,
> hook `command`, and every native file tool) resolve **relative to the artifact's own
> folder**, not the CWD you launch from.

## §2 Document skeleton

```yaml
version: "0.1"          # REQUIRED — free-form string
runtime: maf            # REQUIRED — only "maf"

runtime_options: {}     # optional; only `egress_allow` is read (§9.5)
output_dir: reports     # optional; artifact-relative, must not be absolute or contain ".."
defaults:               # optional; only `harness` allowed (§11)
  harness: { ... }

models:        []       # §3
mcp_servers:   []       # §4  (optional)
tools:         []       # §5
skills:        []       # §6
agents:        []       # §7
orchestrations: []      # §12
```

Only `version` and `runtime` are mandatory; every list defaults to `[]`.

**Two golden rules** (MANUAL §2):

1. **Unique ids across the whole document** — a model and a tool sharing an id is a
   duplicate-id error.
2. **No dangling refs** — `agents[].model`, `tools`, `skills`, `server`, `participants`,
   `start`, `manager`, `edges` endpoints, callable refs, hook `matcher` ids. One bad ref
   rejects the whole run before it binds.

## §3 `models[]` — the LLM endpoints

```yaml
models:
  - id: qwen-local
    provider: openai            # provider INTENT, never a concrete class
    model: qwen2.5-72b
    options:
      base_url: http://127.0.0.1:8081/v1
      api_key: local
      api: chat_completions
      context_window_tokens: 32768
```

| `provider` | Binds to | Status |
|---|---|---|
| `openai` | any OpenAI-compatible wire (OpenAI, llama.cpp, Ollama, LiteLLM, LM Studio) | wired |
| `azure_openai` | Azure OpenAI (`deployment_name` → model, `endpoint` → `azure_endpoint`) | wired |
| `anthropic` | `AnthropicClient`, import-guarded optional extra | wired (guarded) |
| any other string | **non-binding intent**, lineage only — what an agent on an external-backend path uses (`claude-code`, `github-copilot`, `foundry`) | external-backend only |

An unknown provider on a *chat-client* agent raises `ProviderError`; on an agent with a
`harness.backend` block the provider is never resolved, so any string is legal.

`options` worth knowing (§3):

- `api`: `responses` (default) or `chat_completions`. **Local servers need
  `chat_completions`** — they reject the Responses API's `previous_response_id` in multi-turn,
  i.e. in every orchestration.
- `context_window_tokens` (+ `max_output_tokens`, default 8192): fits a MAF compaction
  strategy. **This is the fix for "request exceeds the available context size" on long
  councils.** Absent → off.
- `normalize_turns`, `tool_call_content` (`ensure`|`strip`), `tool_turn_cue`,
  `strip_reasoning`, `timeout` (120s), `max_retries` (0 by design — the drive layer owns
  retries).
- **No `temperature` / `top_p` / `max_tokens`** — the constructors reject them; putting them in
  `options` breaks the bind with `ProviderError`.

Endpoint precedence for `base_url`/`api_key`: **CLI flags > `models[].options` > env**
(`OPENAI_BASE_URL`/`OPENAI_API_KEY`), so one artifact runs against OpenAI or a local endpoint
with no edits.

## §4 `mcp_servers[]` — external tool servers

```yaml
mcp_servers:
  - id: firecrawl
    transport: stdio                      # only stdio is wired; http/websocket raise at bind
    command: npx
    args: ["-y", "firecrawl-mcp"]
    env:
      FIRECRAWL_API_KEY: "${FIRECRAWL_API_KEY}"
```

`${VAR}` expansion happens at bind time; an **unset var is a hard error** — never inline a
secret. Referenced from `tools[].server`.

## §5 `tools[]` — the capabilities

```yaml
tools:
  - { id: search,  kind: function, ref: grep }                  # code-as-action
  - { id: helper,  kind: plugin,   path: plugins/h.py, ref: fn } # artifact-relative .py
  - { id: web,     kind: mcp,      server: firecrawl,
      allowed_tools: [firecrawl_search], requires_approval: false }
```

The built-in function registry (`@register_tool`, `agent/tool_registry.py` — the manual's §5
table is the authority; **it is larger than older skills claim**):

| `ref` | Notes |
|---|---|
| `read_file` | Artifact-relative, `..` refused, `max_chars` 100 000. The sentinel path `transcript.session.md` resolves to **this run's** transcript — a later phase can read the whole prior discussion. |
| `read_files` | Many paths at once; a bad path yields its error inline (partial results survive). |
| `list_dir` | Directories suffixed `/`. |
| `glob` | `pattern` (recursive `**`), `path`, `max_results` 1000. Returns paths that feed straight into `read_file`. |
| `grep` | Regex over contents; `include` glob filter, `ignore_case`, `max_matches` 200; skips `.git`/`node_modules`/`__pycache__`/`.venv`, binaries, huge files. Returns `path:line: text`. **The workhorse of an investigation phase.** |
| `write_file` | Parents created; `overwrite: false` refuses an existing file. |
| `append_report` | Per-round transcript writer: the agent passes only its own turn, the function appends `## Round N` without overwriting earlier rounds. |
| `bash` | `command`, `timeout` (60s), `workdir` (artifact-relative). Returns `Exit code: N` + combined output. Hard-disabled under mesh/multi-tenant isolation. **`workdir` is a starting directory, not a sandbox.** |

`kind: function` resolves **registry-first**, then as a dotted import path. `kind: plugin` is
path-contained (checked at validate *and* at bind, including a resolved-path containment check
that catches a symlink escape) — but ⚠️ **`requires_approval` does nothing on a plugin**; only
the MCP path reads it. Treat a plugin as trusted code.

## §6 `skills[]` — instruction + tool packages (Helix-only)

```yaml
skills:
  - { id: kb-contract, instructions_file: skills/kb-contract.md }
  - { id: research, load: eager, instructions: "Always cite the source.", tools: [web] }
```

A skill has no MAF type: at bind, its Markdown body is concatenated **after** the agent's base
prompt in declaration order, and its `tools[]` merge into the agent's tool set. It is never a
runtime participant — it shares the acquiring agent's identity, thread and run.

- `load: eager` is the default; **`on_demand` parses then raises `NotImplementedError` at
  bind** (only if some agent actually references it).
- On the coding-agent / copilot paths, skills fold **instructions only** — their tools are
  resolved (so a bad MCP env var still fails) and then discarded.

## §7 `agents[]`

```yaml
agents:
  - id: investigator
    model: qwen-local              # → models[].id
    instructions_file: agents/investigator.md   # XOR with `instructions`
    skills: [kb-contract]
    tools: [search, web]
    harness: { ... }               # §9 — absent ⇒ the plain in-process MAF loop
    default: false                 # Studio UI hint, NOT runtime
```

**Exactly one** of `instructions` / `instructions_file`. Final prompt = base prompt + each
eager skill's body in declaration order. At most **one** target across `agents` +
`orchestrations` may set `default: true`.

## §8 The `agents/` and `skills/` Markdown files

Plain Markdown, no frontmatter — the file *is* the prompt. Three conventions the runtime
relies on (MANUAL §8):

1. **Role prefix per turn** — in a `group_chat` each agent opens every message with its label
   on its own line (`INVESTIGATOR:`, `REVIEWER:`); selectors and terminations key on it.
2. **Termination marker on its own line** — a line-anchored terminator or edge condition only
   sees a marker that *leads a line*, never one buried in prose.
3. **Self-contained hand-off line** — a nested orchestration emits only its final turn under
   the default `context_handoff: final_turn_only`, so the last line of each phase must carry
   everything the next phase needs, not "we agree".

## §9 `harness` — external backends and permissive keys

The **only** permissive section (`extra="allow"`), with four typed islands: `backend`,
`workspace`, `result_contract`, `hooks`.

### §9.1–9.2 The coding-agent path (DOC006)

Claude Code *is* the harness: no chat client, no Helix native tools — its own
Read/Write/Edit/Bash governed by `cwd` + `permission_mode`.

```yaml
  - id: remediator
    model: claude-sonnet           # a model whose provider is the non-binding `claude-code`
    instructions_file: agents/remediator.md
    harness:
      backend: { kind: external-coding-agent, provider: claude-code, mode: harness, transport: agent-sdk }
      coding_agent:                # permissive sub-block
        workspace_dir: workspace   # created at bind, pins cwd; absent → the artifact dir
        permission_mode: acceptEdits
        allowed_tools: [Read, Write, Edit, Bash]
        max_turns: 60
```

⚠️ **`transport` is ignored on the local coding-agent path**: the resolver keys only on
`kind: external-coding-agent` and always returns the agent-SDK backend — `headless-cli` parses
but silently behaves as `agent-sdk`. It becomes mandatory and validated only under
`placement: tektona-sandbox` (§9.9, which also needs `$TEKTONA_API_KEY`).

Other paths: `kind: copilot-agent` (knobs in `harness.copilot_agent`, needs a logged-in Copilot
CLI) and `kind: managed-agent` (§9.4 — the **only** working route is `transport: a2a` +
`harness.managed_agent.wire: json-rpc`; `foundry`+`azure-sdk` is not usable from YAML; an
omitted transport silently yields a **test double**).

### §9.8 What is live vs. inert under `harness`

| Block | Status |
|---|---|
| `governance.policy.model_allowlist` | ✅ **ENFORCED fail-closed.** Absent = opt-out; **present (even empty)** denies any agent whose model is not listed — `PolicyDenied` before the model call. |
| `governance.policy.require_approval: true` | ✅ enforced — and there is no reachable approval channel, so it always resolves to **deny**. Setting it blocks the run. |
| `governance.hooks.allow_external` | ✅ ENFORCED — the gate that admits a `command` hook (§10.5). |
| `memory.scope` | ✅ read: `session` (default) / `none`; `persistent` raises. Nothing else under `memory` is read. |
| `observability` | ❌ INERT — OTel is env-controlled (`OTEL_ENABLED`, `OTEL_EXPORTER_OTLP_ENDPOINT`). |
| `governance.hitl` / `audit` / `cost` | ❌ parsed, not enforced/emitted. No declarative approval gate, no budget ceiling. |
| `workspace` (§9.6) | ⚠️ typed and validated, **no binder reads it**. The only thing that builds a workspace is the fan-out runner, from `fan_out.isolation`. |
| `result_contract` (§9.7) | ⚠️ typed, **not a switch** — results are always coerced through one seam. |
| `middleware`, `context_provider` | ❌ parse-only. |

## §10 `hooks[]` — the lifecycle surface

A typed island inside `harness` (`extra="forbid"`), so a misspelled event fails validation
instead of failing open into a silently-dropped guardrail.

**Three homes** (§10.1): `agents[].harness.hooks[]` (one agent),
`defaults.harness.hooks[]` (every agent, concatenated **ahead of** its own), and
`orchestrations[].hooks[]` (the topology run — `defaults.harness.hooks` do **not** reach it).

**Eleven events, closed enum** (§10.2). Harness home: `on_run_start`, `on_input`,
`on_run_stop`, `pre_model`, `pre_tool`, `post_tool`, `on_turn`. Orchestration home:
`on_run_start`, `on_input`, `on_run_stop`, `on_participant_stop`, `on_orchestration_start`,
`on_orchestration_stop`, `on_orchestration_error`. Wrong home = `SpecError`. The three
run-level events fire only for the **run target's** own bindings — a participant agent's
`on_run_start` never fires inside an orchestration run.

**Decisions** (§10.3) — `continue` | `deny` | `inject`; a content *rewrite* is deliberately
unrepresentable. `on_input` denies → aborts; `pre_model` denies → aborts; **`pre_tool` denies →
result-override**: the call is blocked, a reason goes back to the model, the run continues.
Handlers see a projected JSON envelope, never a live MAF object.

```yaml
      hooks:
        - { event: pre_tool,  handler: deny_tool,  matcher: { tool: shell } }
        - { event: post_tool, handler: audit_jsonl, params: { path: reports/tool-audit.jsonl } }
        - { event: on_orchestration_start, handler: inject_static,
            params: { text: "House rule: cite a file path for every claim." } }
```

Binding rules (§10.4–10.5): **exactly one** of `handler` (a registered ref name) /`command`;
`args`/`timeout_s` only with `command`; `params` only with `handler`, a flat bag of scalars.
`matcher: {tool?, agent?, orchestration?}` is **exact ids, conjunctive — never glob/regex**,
and an empty matcher is rejected. Built-ins: `audit_jsonl{path?}`, `deny_tool{}` (its
*matcher* is its parameterization), `inject_static{text!}`. A `command` hook needs
`governance.hooks.allow_external: true` (only the literal boolean opens it), must be
artifact-relative and must already exist — and can never alter control flow. Custom handlers
enter only via `@register_hook`.

## §11 `defaults`

`defaults.harness` is deep-merged into every agent; `harness` is the only allowed field.

```yaml
defaults:
  harness:
    memory: { scope: session }
    governance:
      policy: { model_allowlist: [qwen-local, claude-sonnet] }
      hooks:  { allow_external: false }
    hooks:
      - { event: on_turn, handler: audit_jsonl, params: { path: reports/turns.jsonl } }
```

## §12 `orchestrations[]`

Exactly one of `topology` or `fan_out`. Both, or neither, is a parse error.

| Field | Applies to | Notes |
|---|---|---|
| `participants` | topologies | Ordered; each an `agents[].id` **or** an `orchestrations[].id` (the nesting lever, §12.4). |
| `selection` | group_chat | Registered selector ref (`round_robin`). |
| `aggregator` | concurrent | Registered aggregator ref (`concat`). |
| `max_rounds` | group_chat / magentic | A **raw turn cap**, not a rounds field. |
| `termination` | group_chat | Registered ref. |
| `marker` / `marker_line_anchored` | group_chat + `on_marker` | The literal sentinel (matched as data, `re.escape`d) and whether it must begin a line. |
| `close_requires_glob` / `close_requires_marker` / `reset_globs` | group_chat | The close gates (§12.3). |
| `limits` | group_chat / workflow | `{max_steps?, max_identical_turns?}` (§12.5). |
| `context_handoff` | nested | `final_turn_only` (default) \| `full_thread`. |
| `start` / `manager` / `edges` | handoff+workflow / magentic / workflow | §12.1, §12.3. |
| `hooks` | all | The orchestration hook home (§10.1). |

**The six topologies** (§12.1):

| `topology` | Behavior | Fields bound |
|---|---|---|
| `sequential` | in order, each output feeds the next; every stage's turn surfaced (`output_from="all"`) | `participants` |
| `concurrent` | in parallel, results aggregated | `participants`, `aggregator` |
| `group_chat` | turn-taking; a selector picks the speaker; ends on `max_rounds` or `termination` | `selection`, `max_rounds`, `termination`, `marker`, close gates, `limits` |
| `handoff` | a starting agent hands off control | `participants`, `start` |
| `magentic` | a manager coordinates specialists | `participants`, `manager` |
| `workflow` | static executor graph with conditional edges | `participants`, `start`, `edges`, `limits` |

**The registered callable catalog** (§12.2) — selectors: `round_robin`. Aggregators: `concat`.
Terminations: `on_marker` (the generic one), `on_approved` (alias for substring `APPROVED:`),
`on_approved_or_halted` (adds an explicit `HALTED:` abort — the guardrail form for a phase
whose precondition failed, so it stops instead of inventing work), `on_stage0_deliberation`.
Edge conditions: `always`, `reproduced_ok`, `scope_localized`, `scope_sweeping`,
`decomposition_complete`, `needs_remediation`, `deliberation_consensus`, `tooling_complete`,
`tooling_needs_redeliberation`, `deliberation_final_approve`, and the configurable `on_marker`.
`on_council_consensus` **no longer exists** — replace it with `on_marker` + your own marker.

Custom callables enter only through the in-process decorators (`register_selector`,
`register_aggregator`, `register_termination`, `register_edge_condition`); they must be
**synchronous** and read the neutral projected view.

> ⚠️ Every ref (`selection`/`aggregator`/`termination`/`start`/`manager`, and **every element
> of a positional `edges` tuple**) is a **registered ref name** matching
> `^[a-z0-9][a-z0-9_-]*$` — never a `module:function` path. That pattern is why a tuple element
> cannot carry a sentinel like `DEFECT_REMAINS:`.

**§12.8 — what is not usable from YAML**: `expose_as_agent`, `intermediate_output_from`,
`hitl`, handoff HITL/`add_handoff` maps, magentic plan-review/`max_round_count`,
checkpointing, `fan_out.as_input`, `fan_out.write_back`.

---

# Part 2 — Lowering a process onto the contract

## Pattern A — A phase → an orchestration

- **One producer, ungated output** → a solo agent in a `sequential`.
- **Produce → critique → revise** → a `group_chat` `[producer, critic]`, `round_robin`. The
  default. A phase whose output is gated **must** have a critic, or it is a producer with no
  quality control.
- **A phase with internal gates** → a nested `sequential` of its own gated `group_chat`s
  (Pattern B + C).

Keep the phase identity in the `id` (`recon-loop`, `diagnosis-loop`, `remediation-loop`).

## Pattern B — A gate → a gated group_chat (the load-bearing recipe)

```yaml
  - id: diagnosis-gate
    topology: group_chat
    participants: [diagnostician, evidence-critic]   # producer first, then the critic
    selection: round_robin
    max_rounds: 12                  # = 6 table rounds × 2 participants — DERIVE it
    termination: on_marker
    marker: "DIAGNOSIS_APPROVED:"
    marker_line_anchored: true      # MANDATORY
    limits: { max_identical_turns: 3 }
```

Five rules:

1. `termination: on_marker` **and** `marker:` — one without the other is inert or fails.
2. `marker_line_anchored: true` — **always**. Truthy → `^{re.escape(marker)}` MULTILINE.
   Falsy/absent → **upper-cased substring anywhere in a turn**, so the gate closes on the
   sentinel quoted mid-prose. The single most common defect in existing artifacts.
3. The sentinel appears **in the critic's prompt**, never the producer's. A producer that
   certifies its own work is not a gate.
4. `max_rounds = table_rounds × participants` (MANUAL §12 idiom). Show the arithmetic.
5. A **full, substring-distinct** sentinel — never the bare `APPROVED:`.

### The close gates — consensus is not delivery

A council can reach its marker and never write the file the next phase reads (§12.3):

```yaml
    close_requires_glob: "kb/diagnosis/*.md"      # ≥1 FRESH file (mtime ≥ session start)
    close_requires_marker: "DIAGNOSIS_WRITTEN:"   # emitted when ALL artifacts are written
    reset_globs: ["kb/diagnosis/*.md"]            # wiped at run start — no stale-file pass
```

`on_marker` will not fire until the glob is satisfied; if `max_rounds` runs out with the gate
unmet, closing raises `OrchestrationError` (`closed_without_required_artifact`) instead of
silently succeeding. **Use them on every phase whose output a later phase consumes.**

## Pattern C — The chain → a sequential of phase orchestrations (nesting lever)

```yaml
  - id: legacy-investigation
    topology: sequential
    participants: [recon-loop, diagnosis-loop, remediation-loop, verification-loop]
```

The runner resolves each participant orchestration, builds it, and exposes it via
`workflow.as_agent()` (§12.4). `context_handoff` controls how much thread the inner run sees:
`final_turn_only` (default — hence the self-contained hand-off line) or `full_thread` (the
whole parent thread, at a context cost). **Prefer file hand-offs (Pattern D) to
`full_thread`**: files survive compaction, threads don't.

## Pattern D — The I/O contract → a kb layout + file tools

A phase is reproduced by **consuming the right input and producing the right output**, not by
having an agent.

- Producers get `write_file`; consumers get `read_file`/`read_files`/`list_dir`/`glob`/`grep`.
  A read-only critic gets neither `write_file` nor `bash`.
- Write a `kb-contract` skill fixing the canonical paths (`kb/recon/…`, `kb/diagnosis/…`,
  `kb/remediation/…`) and declare it on every agent that touches the kb.
- **Prove the chain closes**: output path of N = input path of N+1. An artefact nothing reads,
  or a read with no writer, is a broken chain.
- Everything is artifact-relative with `..` refused. To reach a repo outside the artifact dir,
  either launch with `-w/--working-dir`, use the coding-agent path (Pattern E), or mount an MCP.

## Pattern E — A repo-touching phase → the coding-agent harness

See §9.2 above for the block. Rules of thumb:

- A **reviewer** on this path gets read-only `allowed_tools: [Read, Grep, Glob, Bash]` — never
  `Write`/`Edit` (MANUAL §14.3 uses `[Read]`).
- An **analysis/authoring** phase runs on a chat model + native tools; if it must execute
  commands, grant the `bash` function tool or mount a shell MCP.
- **Model substitution is a fidelity decision**: on a chat model the coding-agent vocabulary
  degrades (`Edit` → `write_file`, `Read` → `read_file`, `Grep`/`Glob` → the native `grep`/
  `glob`). Declare the substitution and the degradation; never absorb them silently.

## Pattern F — A branch or a loop-back → `topology: workflow` with conditional edges

**This is wired** (`orchestration/builders.py`, MANUAL §12.3): a `sequential` is strictly
forward, but a `workflow` is a real graph — cycles included.

```yaml
  - id: investigation-graph
    topology: workflow
    participants: [reproducer, diagnostician, localized-fixer, sweeping-fixer, verifier]
    start: reproducer
    edges:
      - [reproducer, diagnostician, reproduced_ok]     # tuple + registered condition
      - [diagnostician, localized-fixer, scope_localized]
      - [diagnostician, sweeping-fixer, scope_sweeping]
      - [localized-fixer, verifier]                    # unconditional
      - from: verifier                                 # object form → the BACK-EDGE
        to: diagnostician
        condition: on_marker
        marker: "DEFECT_REMAINS:"
    limits: { max_steps: 400 }                         # the only cycle brake
```

- The workflow **ends when no outgoing edge fires**.
- A positional tuple element is a ref-name and **cannot carry a sentinel** — use the object
  form `{from, to, condition: on_marker, marker: "…"}` for your own markers.
- Edge-condition matching is **line-anchored and whole-token**, always: the marker must lead a
  line, followed by whitespace or EOL. Keying on a bare `FOO` does **not** match a `FOO:` line —
  declare the full sentinel including the colon.
- MAF edge ids are `source->target`: two YAML edges on the same pair coalesce into one ORed
  predicate, and mixing an unconditional with a conditional edge on that pair is an
  `OrchestrationError`.
- **Always set `limits.max_steps`** on a cyclic workflow.
- A marker-consuming condition without a `marker`, or a `marker` on a non-`on_marker`
  condition, is a fail-closed validation error.

## Pattern G — Per-item work → `fan_out` over a wave-spec

A higher-order operator, **not a 7th topology** (§12.6):

```yaml
  - id: per-module-council      # the INSTANCE: what runs for ONE item
    topology: group_chat
    participants: [investigator, reviewer]
    selection: round_robin
    max_rounds: 20
    termination: on_approved_or_halted

  - id: sweep                   # the fan-out (NO topology)
    fan_out:
      over: reports/wave-spec.yaml   # artifact-relative, MUST exist at validate time
      instance: per-module-council
      isolation: git-worktree        # REQUIRED for anything git-related
      max_parallel: 4                # absent → 1 (sequential), never unbounded
      merge_between_phases: true     # default true
      merge_auto: true
      merge_verify: "mvn -q test"    # non-zero REJECTS the merge
      on_task_failure: block-dependents
      max_task_attempts: 2           # clamped to 1 without git-worktree isolation
      auto_level_phases: true
      resume_completed: true
```

Each task runs the instance with **`task.prompt` as the literal input string** (conventionally
a path to a task `.md` the agent then reads), `cwd` = that task's worktree. `as_input` and
`write_back` are **INERT**. `base_branch` is a *mode switch*: absent → a fresh dedicated
per-session repo under `output_dir`; declared → legacy ambient-repo mode. Worktrees are torn
down after a clean barrier merge; **branches are never deleted**.

The wave-spec is a separate file (§12.7) with five fail-closed checks — acyclic,
phase-consistency, producer-completeness, fork-resolution, live-set closure. Only
`status: live` tasks are dispatched. A fork with `decision: null` **blocks the whole run
before any git state exists**; there is no CLI to resolve it — hand-edit the YAML, then:

```bash
uv run helix validate-wavespec reports/wave-spec.yaml
```

---

# Part 3 — Declare what Helix v0.1 cannot do

Reproduce what the contract carries; **declare** the rest in `PROCESS.md` /
`open-questions.md`. A divergence recorded is a result; a divergence absorbed is a defect.

| You want | Status | Author as |
|---|---|---|
| Human-in-the-loop gate | `governance.hitl` parsed, not enforced | An LLM critic in-band + declare |
| Approval gate via `require_approval` | enforced — and always resolves to **deny** | Do not set it |
| Model restriction | ✅ `governance.policy.model_allowlist`, fail-closed | Use it |
| Budget ceiling / declared audit stream | not enforced / not emitted | Declare; use `audit_jsonl` hooks for a real trail |
| Tracing from the YAML | inert | Env-controlled (`OTEL_ENABLED`) |
| Declared workspace / result contract | typed, not driven | Declared intent only |
| Deterministic DoR/DoD engine, artefact schema validation | no gate hook | Fold into the critic prompt + a prose kb-contract skill + declare |
| Auto-commit per step | no post-turn hook surface | Agents write files; `bash` can commit explicitly |
| Lazy skills, checkpointing, magentic plan-review, handoff HITL | not reachable from YAML | Omit + declare |

---

# Part 4 — Verification (run it; never claim done on an un-run check)

**1. Parses & valid.** No `helix validate` subcommand exists for artifacts — use the runtime's
own loader (verified working against the current tree):

```bash
cd /Users/christian.soliman/Repos/helix/src/backend
uv run python -c "
import sys, yaml
from helix.contract.schema import HelixDocument
from helix.contract.validate import validate_references
d = HelixDocument.model_validate(yaml.safe_load(open(sys.argv[1]).read()))
validate_references(d)
print('OK', [o.id for o in d.orchestrations])
" <path/to/artifact.yaml>
```

Stub or export any `${VAR}` first — an unset var makes the loader fail closed. Read the error
straight back into a fix (MANUAL §13 lists every rule): `SpecError` = shape (unknown key, both
or neither `topology`/`fan_out`, missing `instructions`, two `default: true`, an escaping
`output_dir`, a malformed hook binding); `ReferenceError` = duplicate id, dangling ref, unknown
topology, unregistered callable, a `start`/`manager` that is not a participant, a `fan_out.over`
that is not on disk. `validate_references` runs **every** check and reports **all** violations,
not just the first. Expected output: `OK [ids…]`.

**2. Every phase present** — one orchestration (or agent set) per phase, or a divergence
declared in writing. A silently missing phase is the defect this pattern set exists to prevent.

**3. I/O flow closed** — output path of N = input path of N+1 for every N; every branch and
back-edge either expressed as a `workflow` edge (Pattern F) or declared.

**4. Prompt ↔ wiring coherence** — every tool a prompt uses is granted in that agent's
`tools:`; every marker a prompt emits equals the orchestration's or the edge's `marker:`; no
producer's prompt contains a sentinel only the critic may emit; every line-anchored marker is
documented in the prompt as "on its own line".

A red check means the artifact is a **draft**. Say so.

## Worked examples to read (not to clone blindly)

- `$HELIX/Docs/examples/05-advanced/software-process/software-process.yaml` — nesting lever +
  MCP + file skills + coding-agent harness in one commented file; **it validates today**
  (`OK ['discovery-council', 'story-authoring', 'implementation', 'software-process']`).
- MANUAL §14 cookbook: 14.1 mono · 14.2 group_chat · 14.3 coding agent · **14.4 close gates** ·
  14.5 fan-out · 14.6 multi-phase · 14.7 guardrails (policy gate + hooks).
- ⚠️ There is **no runnable `fan_out` example** in `Docs/examples` (a documented gap);
  `fanout_worktrees.py` there is an *external driver*, not the declarative block.
- Older in-repo artifacts carry known defects — an unanchored `APPROVED:` gate is the classic.
  **Re-express and repair; never clone.**
