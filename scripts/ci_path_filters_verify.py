#!/usr/bin/env python3
"""Structural verification that the four per-folder CI/CD workflows are
correctly path-filtered and that the `mobile` lane is non-blocking (task
E01/F03/US02/T02, `ci-path-filters`).

Parent story `us-02-per-folder-workflows`:
  - AC-1 "A workflow for each deployable folder (infra, backend, web,
    mobile) with matching path filters."
  - AC-2 "`dev` deploy triggers on merge to `main`."
  - AC-3 "`mobile` lane is non-blocking (its failure does not block
    promotion)."

Task E01/F03/US02/T01 (already merged into this branch, `ci-cd-workflows`)
authored the four workflows at `.github/workflows/{infra,backend,web,
mobile}.yml` (root-relative -- the only location GitHub Actions can
discover them; see `reports/open-questions.md` OQ-impl-001). This task's
own coding objective ("Verify four workflows have correct path filters and
mobile non-blocking") is proof over that artifact, not a re-decision of
it. It structurally proves, per workflow:

  1. the file exists;
  2. both `pull_request` and `push` triggers are scoped to `branches:
     [main]` (ADR-014) and their `paths:` include that folder's own
     `<folder>/**` glob, with no other product folder's path leaking in
     (AC-1) -- a shared path such as `.github/actions/**` is not "another
     folder's path";
  3. for `infra`/`backend`/`web` (the folders with a real dev deploy):
     exactly one job is gated on `push` to `refs/heads/main`, and that
     job's `environment:` defaults to `'dev'` (AC-2);
  4. `mobile`'s `build` job is `continue-on-error: true` at job level, and
     every one of its steps except `actions/checkout` also declares
     `continue-on-error: true` (AC-3, mobile.yml's own documented
     "belt-and-suspenders" design);
  5. no *other* folder's job is marked `continue-on-error: true` -- only
     `mobile` is non-blocking (AC-3's scope, not a blanket exemption);
  6. `mobile` declares no job `environment:` (ADR-013: no mobile deploy
     target exists in either environment);
  7. `scripts/apply_github_branch_protection.py`'s
     `REQUIRED_STATUS_CHECK_CONTEXTS` never names a mobile-shaped context
     -- the exact regression mobile.yml's own header comment warns
     against.

Like every other verification task in this repo, this is a static/text
check: there is no GitHub Actions runner available in this harness, so
"the workflow behaves correctly" is proven here as "the workflow is
structurally correct," not as a live CI run. This is a small, targeted
parser for this repo's own GitHub Actions YAML shape (2-space indents,
flow-style `branches: [main]`, block-style `paths:` sequences) -- not a
general-purpose YAML parser, and deliberately dependency-free (stdlib
only), matching the rest of `scripts/`.

Usage:
    python scripts/ci_path_filters_verify.py

Exit 0 if every check passes. Non-zero otherwise, with a PASS/FAIL line
per check on stdout (the failure summary is also echoed to stderr).
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
WORKFLOWS_DIR = REPO_ROOT / ".github" / "workflows"
BRANCH_PROTECTION_SCRIPT = REPO_ROOT / "scripts" / "apply_github_branch_protection.py"

# Parent story us-02 AC-1's four deployable folders, in the order the ADRs
# and task files always list them.
ALL_FOLDERS = ("infra", "backend", "web", "mobile")
NON_BLOCKING_FOLDER = "mobile"  # ADR-013 / AC-3
DEPLOYING_FOLDERS = tuple(f for f in ALL_FOLDERS if f != NON_BLOCKING_FOLDER)  # AC-2 scope

TRUNK_BRANCH = "main"  # ADR-014: trunk-based, single protected mainline
DEV_ENV_FALLBACK = "'dev'"  # the `${{ inputs.target_environment || 'dev' }}` fallback literal

# A workflow may also legitimately react to its own file or a shared
# composite action without that counting as "another folder's path" (AC-1).
SHARED_PATH_PREFIXES = (".github/workflows/", ".github/actions/")

_CHECKOUT_STEP_RE = re.compile(r"actions/checkout@v\d+")
_REQUIRED_CONTEXTS_RE = re.compile(
    r"REQUIRED_STATUS_CHECK_CONTEXTS\s*:\s*list\[str\]\s*=\s*\[([^\]]*)\]", re.DOTALL
)


def _workflow_paths(workflows_dir: Path = WORKFLOWS_DIR) -> dict[str, Path]:
    return {folder: workflows_dir / f"{folder}.yml" for folder in ALL_FOLDERS}


# ---------------------------------------------------------------------------
# Minimal YAML block parsing -- see module docstring for scope/limits.
# ---------------------------------------------------------------------------

def _strip_comment_lines(text: str) -> str:
    """Drop whole-line YAML comments. Every workflow in this repo uses
    full-line `#` comments for its prose header, never a trailing inline
    one next to a key this script reads, so this avoids the harder
    "# inside a quoted string" ambiguity for no real benefit here."""
    return "\n".join(
        line for line in text.splitlines() if not line.strip().startswith("#")
    )


def _indent_of(line: str) -> int:
    return len(line) - len(line.lstrip(" "))


def _unquote(value: str) -> str:
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in "'\"":
        return value[1:-1]
    return value


def _block_after(lines: list[str], key: str, base_indent: int) -> list[str]:
    """Lines strictly more indented than `base_indent`, starting right
    after a `<key>:` line found at exactly `base_indent`, up to (but not
    including) the next line at or below `base_indent`."""
    out: list[str] = []
    in_block = False
    for line in lines:
        if not in_block:
            if _indent_of(line) == base_indent and line.strip() == f"{key}:":
                in_block = True
            continue
        if line.strip() == "":
            out.append(line)
            continue
        if _indent_of(line) <= base_indent:
            break
        out.append(line)
    return out


def _sub_block(lines: list[str], key: str, base_indent: int) -> list[str] | None:
    """Like `_block_after`, but None (not just empty) when `<key>:` is
    absent at `base_indent` -- lets callers tell "trigger not declared"
    apart from "trigger declared with nothing under it"."""
    present = any(_indent_of(l) == base_indent and l.strip() == f"{key}:" for l in lines)
    if not present:
        return None
    return _block_after(lines, key, base_indent)


def _flow_list(block_lines: list[str], key: str, base_indent: int) -> list[str] | None:
    """`<key>: [a, b]` flow-style sequence, e.g. this repo's `branches:
    [main]`. None if the key is absent."""
    for line in block_lines:
        if _indent_of(line) == base_indent:
            stripped = line.strip()
            if stripped.startswith(f"{key}:"):
                rest = stripped[len(key) + 1 :].strip()
                m = re.match(r"^\[(.*)\]$", rest)
                if not m:
                    return []
                return [_unquote(x) for x in m.group(1).split(",") if x.strip()]
    return None


def _block_seq(block_lines: list[str], key: str, base_indent: int) -> list[str]:
    """`<key>:` followed by a block sequence (`- item` lines), e.g. this
    repo's `paths:` lists."""
    seq_block = _block_after(block_lines, key, base_indent)
    return [_unquote(line.strip()[2:]) for line in seq_block if line.strip().startswith("- ")]


def _scalar_field(block_lines: list[str], key: str, base_indent: int) -> str | None:
    for line in block_lines:
        if _indent_of(line) == base_indent:
            stripped = line.strip()
            if stripped.startswith(f"{key}:"):
                return _unquote(stripped[len(key) + 1 :].strip())
    return None


def _parse_steps(steps_block: list[str]) -> list[dict]:
    """Split a job's `steps:` block into one entry per `- ` item at that
    block's own (minimum) indent, each reporting its header line and
    whether it declares its own `continue-on-error: true` anywhere in its
    body."""
    item_lines = [line for line in steps_block if line.strip().startswith("- ")]
    if not item_lines:
        return []
    item_indent = min(_indent_of(line) for line in item_lines)
    steps: list[list[str]] = []
    for line in steps_block:
        if _indent_of(line) == item_indent and line.strip().startswith("- "):
            steps.append([line])
        elif steps:
            steps[-1].append(line)
    return [
        {
            "header": step_lines[0].strip(),
            "continue_on_error": bool(re.search(r"continue-on-error:\s*true", "\n".join(step_lines))),
        }
        for step_lines in steps
    ]


def parse_workflow(text: str) -> dict:
    """Parse the subset of a GitHub Actions workflow this task cares
    about: `on.pull_request`/`on.push` (`branches`, `paths`) and each
    `jobs.<id>`'s `if`, `environment`, `continue-on-error`, and `steps`.
    Returns {"triggers": {...}, "jobs": {...}}."""
    lines = _strip_comment_lines(text).splitlines()

    on_block = _block_after(lines, "on", 0)
    triggers: dict[str, dict | None] = {}
    for trigger in ("pull_request", "push"):
        trig_block = _sub_block(on_block, trigger, 2)
        if trig_block is None:
            triggers[trigger] = None
            continue
        triggers[trigger] = {
            "branches": _flow_list(trig_block, "branches", 4) or [],
            "paths": _block_seq(trig_block, "paths", 4),
        }

    jobs_block = _block_after(lines, "jobs", 0)
    job_ids = [
        line.strip()[:-1]
        for line in jobs_block
        if _indent_of(line) == 2 and re.match(r"^[A-Za-z0-9_-]+:$", line.strip())
    ]
    jobs = {}
    for job_id in job_ids:
        job_block = _block_after(jobs_block, job_id, 2)
        jobs[job_id] = {
            "if": _scalar_field(job_block, "if", 4),
            "environment": _scalar_field(job_block, "environment", 4),
            "continue_on_error": _scalar_field(job_block, "continue-on-error", 4),
            "steps": _parse_steps(_block_after(job_block, "steps", 4)),
        }

    return {"triggers": triggers, "jobs": jobs}


# ---------------------------------------------------------------------------
# check_* -- each returns (passed, detail), mirroring the sibling
# structural-verification scripts in this repo.
# ---------------------------------------------------------------------------

def check_workflow_file_exists(folder: str, path: Path) -> tuple[bool, str]:
    if not path.is_file():
        return False, f"{path} does not exist"
    return True, f"{path} exists"


def check_trigger_scoped_to_own_folder(folder: str, trigger_name: str, workflow: dict) -> tuple[bool, str]:
    trig = workflow["triggers"].get(trigger_name)
    if trig is None:
        return False, f"{folder}.yml has no on.{trigger_name} trigger"

    own_path = f"{folder}/**"
    if own_path not in trig["paths"]:
        return False, f"{folder}.yml on.{trigger_name}.paths={trig['paths']!r} does not include {own_path!r} (AC-1)"

    if trig["branches"] != [TRUNK_BRANCH]:
        return False, (
            f"{folder}.yml on.{trigger_name}.branches={trig['branches']!r}, expected [{TRUNK_BRANCH!r}] (ADR-014)"
        )

    leaked = [
        p
        for p in trig["paths"]
        if p != own_path
        and not any(p.startswith(prefix) for prefix in SHARED_PATH_PREFIXES)
        and any(p.startswith(f"{other}/") for other in ALL_FOLDERS if other != folder)
    ]
    if leaked:
        return False, f"{folder}.yml on.{trigger_name}.paths leaks another folder's path(s): {leaked} (AC-1)"

    return True, (
        f"{folder}.yml on.{trigger_name} is scoped to {own_path!r} on branch {TRUNK_BRANCH!r}, "
        "no cross-folder leakage"
    )


def check_dev_deploy_on_merge_to_main(folder: str, workflow: dict) -> tuple[bool, str]:
    matches = [
        (job_id, job)
        for job_id, job in workflow["jobs"].items()
        if job["if"] and "push" in job["if"] and "refs/heads/main" in job["if"]
    ]
    if not matches:
        return False, f"{folder}.yml has no job gated on push to refs/heads/main (AC-2)"
    if len(matches) > 1:
        return False, (
            f"{folder}.yml has {len(matches)} jobs gated on push to main, expected exactly 1: "
            f"{[job_id for job_id, _ in matches]}"
        )
    job_id, job = matches[0]
    if not job["environment"] or DEV_ENV_FALLBACK not in job["environment"]:
        return False, (
            f"{folder}.yml job {job_id!r} environment={job['environment']!r}, "
            f"expected a fallback to {DEV_ENV_FALLBACK} (AC-2)"
        )
    return True, (
        f"{folder}.yml job {job_id!r} runs on push to {TRUNK_BRANCH!r}, environment defaults to {DEV_ENV_FALLBACK}"
    )


def check_mobile_build_non_blocking(workflow: dict) -> tuple[bool, str]:
    job = workflow["jobs"].get("build")
    if job is None:
        return False, "mobile.yml has no 'build' job"
    if job["continue_on_error"] != "true":
        return False, (
            f"mobile.yml build job continue-on-error={job['continue_on_error']!r}, expected 'true' (AC-3)"
        )
    guarded_steps = [s for s in job["steps"] if not _CHECKOUT_STEP_RE.search(s["header"])]
    if not guarded_steps:
        return False, "mobile.yml build job has no step besides checkout to guard"
    unguarded = [s["header"] for s in guarded_steps if not s["continue_on_error"]]
    if unguarded:
        return False, f"mobile.yml non-checkout step(s) missing continue-on-error: true: {unguarded}"
    return True, (
        f"mobile.yml build job is continue-on-error: true at job level and all {len(guarded_steps)} "
        "non-checkout step(s) also declare continue-on-error: true (AC-3)"
    )


def check_mobile_has_no_deploy_environment(workflow: dict) -> tuple[bool, str]:
    envs = {job_id: job["environment"] for job_id, job in workflow["jobs"].items() if job["environment"]}
    if envs:
        return False, f"mobile.yml job(s) declare a deploy 'environment:' (ADR-013: no mobile deploy target): {envs}"
    return True, "mobile.yml declares no job 'environment:' (no deploy target, per ADR-013)"


def check_only_mobile_non_blocking(parsed: dict[str, dict]) -> tuple[bool, str]:
    offenders = []
    for folder in DEPLOYING_FOLDERS:
        workflow = parsed.get(folder)
        if workflow is None:
            continue
        for job_id, job in workflow["jobs"].items():
            if job["continue_on_error"] == "true":
                offenders.append(f"{folder}.yml:{job_id}")
    if offenders:
        return False, (
            f"non-{NON_BLOCKING_FOLDER} job(s) marked continue-on-error: true (must stay blocking): {offenders}"
        )
    return True, (
        f"none of {', '.join(DEPLOYING_FOLDERS)} carry continue-on-error: true -- "
        f"only {NON_BLOCKING_FOLDER} is non-blocking (AC-3 scope)"
    )


def check_mobile_excluded_from_required_status_checks(path: Path = BRANCH_PROTECTION_SCRIPT) -> tuple[bool, str]:
    if not path.is_file():
        return False, f"{path} does not exist"
    text = path.read_text(encoding="utf-8")
    m = _REQUIRED_CONTEXTS_RE.search(text)
    if not m:
        return False, f"{path} has no 'REQUIRED_STATUS_CHECK_CONTEXTS: list[str] = [...]' declaration to check"
    contexts_src = m.group(1)
    if NON_BLOCKING_FOLDER in contexts_src.lower():
        return False, f"{path} REQUIRED_STATUS_CHECK_CONTEXTS mentions {NON_BLOCKING_FOLDER!r}: {contexts_src!r}"
    return True, f"{path} REQUIRED_STATUS_CHECK_CONTEXTS has no mobile-shaped context: {contexts_src!r}"


def run_all_checks(
    workflows_dir: Path = WORKFLOWS_DIR,
    branch_protection_script: Path = BRANCH_PROTECTION_SCRIPT,
) -> list[tuple[str, tuple[bool, str]]]:
    results: list[tuple[str, tuple[bool, str]]] = []
    parsed: dict[str, dict] = {}

    for folder, path in _workflow_paths(workflows_dir).items():
        exists = check_workflow_file_exists(folder, path)
        results.append((f"{folder}.yml exists", exists))
        if exists[0]:
            parsed[folder] = parse_workflow(path.read_text(encoding="utf-8"))

    for folder in ALL_FOLDERS:
        workflow = parsed.get(folder)
        if workflow is None:
            continue
        for trigger_name in ("pull_request", "push"):
            results.append(
                (
                    f"{folder}.yml on.{trigger_name} scoped to {folder}/ (AC-1)",
                    check_trigger_scoped_to_own_folder(folder, trigger_name, workflow),
                )
            )

    for folder in DEPLOYING_FOLDERS:
        workflow = parsed.get(folder)
        if workflow is not None:
            results.append(
                (
                    f"{folder}.yml dev deploy on merge to main (AC-2)",
                    check_dev_deploy_on_merge_to_main(folder, workflow),
                )
            )

    mobile_workflow = parsed.get(NON_BLOCKING_FOLDER)
    if mobile_workflow is not None:
        results.append(("mobile.yml build job non-blocking (AC-3)", check_mobile_build_non_blocking(mobile_workflow)))
        results.append(
            ("mobile.yml has no deploy environment (ADR-013)", check_mobile_has_no_deploy_environment(mobile_workflow))
        )

    results.append(("only mobile is marked continue-on-error (AC-3 scope)", check_only_mobile_non_blocking(parsed)))
    results.append(
        (
            "mobile excluded from required status checks",
            check_mobile_excluded_from_required_status_checks(branch_protection_script),
        )
    )
    return results


def main() -> int:
    ok = True
    for name, (passed, detail) in run_all_checks():
        print(f"[{'PASS' if passed else 'FAIL'}] {name}: {detail}")
        ok = ok and passed

    if ok:
        print(
            "[ci_path_filters_verify] PASS: infra/backend/web/mobile (ci-path-filters) are each "
            "path-filtered to their own folder (AC-1), infra/backend/web deploy to dev on merge to "
            "main (AC-2), and mobile is the sole non-blocking lane (AC-3)"
        )
        return 0
    print("[ci_path_filters_verify] FAIL: see above", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
