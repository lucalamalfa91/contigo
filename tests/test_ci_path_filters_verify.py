"""Unit tests for scripts/ci_path_filters_verify.py (task E01/F03/US02/T02).

Covers the pure YAML-block-extraction/parsing functions against synthetic
fixtures shaped like the real workflows (no GitHub Actions runner, no
network), the `check_*` functions against both deliberately-good and
deliberately-broken fixtures (one per failure mode this scan exists to
catch), and finally two end-to-end proofs against this actual working
tree: running the `check_*`/`run_all_checks` functions directly against
the real `.github/workflows/` tree, and running
`scripts/ci_path_filters_verify.py` as a subprocess -- the same
invocation this task's own definition of done relies on.

Run:
    python tests/test_ci_path_filters_verify.py -v
"""

from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(REPO_ROOT / "scripts"))

import ci_path_filters_verify as cpf  # noqa: E402

# ---------------------------------------------------------------------------
# Fixture builders. Every knob a "broken" test needs is a parameter, not a
# string .replace() on a monolithic template -- avoids the two copies (the
# template and the mutation) silently drifting out of whitespace-sync.
# ---------------------------------------------------------------------------

_EXTRA_DEPLOY_JOB = """
  deploy2:
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    environment: ${{ inputs.target_environment || 'dev' }}
    steps:
      - uses: actions/checkout@v4
"""


def _deploying_workflow_text(
    folder: str,
    *,
    own_path: str | None = None,
    branches: str = "[main]",
    extra_paths: str = "",
    include_deploy_if: bool = True,
    deploy_environment: str = "${{ inputs.target_environment || 'dev' }}",
    build_continue_on_error: bool = False,
    extra_job: str = "",
) -> str:
    """Shape of the real infra.yml/backend.yml/web.yml: pull_request+push
    scoped to the folder, one job gated on push-to-main defaulting to dev."""
    own_path = own_path if own_path is not None else f"{folder}/**"
    if_line = (
        "    if: github.event_name == 'workflow_call' || "
        "(github.event_name == 'push' && github.ref == 'refs/heads/main')\n"
        if include_deploy_if
        else ""
    )
    build_coe_line = "    continue-on-error: true\n" if build_continue_on_error else ""
    return f"""name: {folder}

on:
  pull_request:
    branches: {branches}
    paths:
      - "{own_path}"
      - ".github/workflows/{folder}.yml"{extra_paths}
  push:
    branches: {branches}
    paths:
      - "{own_path}"
      - ".github/workflows/{folder}.yml"{extra_paths}

permissions:
  contents: read

jobs:
  build:
    name: build
    runs-on: ubuntu-latest
{build_coe_line}    steps:
      - uses: actions/checkout@v4
      - name: build
        run: echo build

  deploy:
    name: deploy
    needs: build
{if_line}    runs-on: ubuntu-latest
    environment: {deploy_environment}
    steps:
      - uses: actions/checkout@v4
      - name: deploy
        run: echo deploy
{extra_job}"""


_MOBILE_HEADER = """name: mobile

on:
  pull_request:
    branches: [main]
    paths:
      - "mobile/**"
      - ".github/workflows/mobile.yml"
  push:
    branches: [main]
    paths:
      - "mobile/**"
      - ".github/workflows/mobile.yml"

permissions:
  contents: read

jobs:
  build:
    name: build (non-blocking)
    runs-on: ubuntu-latest
"""

_MOBILE_GUARDED_STEPS = [
    "      - uses: actions/setup-node@v4\n"
    "        with:\n"
    '          node-version: "20.x"\n'
    "        continue-on-error: true\n",
    "      - name: npm ci\n"
    "        working-directory: mobile\n"
    "        run: npm ci --if-present\n"
    "        continue-on-error: true\n",
    "      - name: npm run build\n"
    "        working-directory: mobile\n"
    "        run: npm run build --if-present\n"
    "        continue-on-error: true\n",
    "      - name: npm test\n"
    "        working-directory: mobile\n"
    "        run: npm test --if-present\n"
    "        continue-on-error: true\n",
]


def _mobile_workflow_text(
    *,
    job_continue_on_error: bool = True,
    guarded_step_indexes: tuple = (0, 1, 2, 3),
    extra_job_fields: str = "",
) -> str:
    """Shape of the real mobile.yml: job-level continue-on-error, plus
    every non-checkout step also declaring it (belt-and-suspenders)."""
    parts = [_MOBILE_HEADER]
    if job_continue_on_error:
        parts.append("    continue-on-error: true\n")
    parts.append(extra_job_fields)
    parts.append("    steps:\n")
    parts.append("      - uses: actions/checkout@v4\n")
    for i, step in enumerate(_MOBILE_GUARDED_STEPS):
        if i in guarded_step_indexes:
            parts.append(step)
        else:
            parts.append(step.replace("        continue-on-error: true\n", ""))
    return "".join(parts)


_GOOD_MOBILE_WORKFLOW_TEXT = _mobile_workflow_text()


def _good_branch_protection_text(contexts: str = "") -> str:
    return f"REQUIRED_STATUS_CHECK_CONTEXTS: list[str] = [{contexts}]\n"


def _write_fixture_tree(
    root: Path,
    *,
    workflow_texts: dict | None = None,
    branch_protection_text: str | None = None,
) -> tuple:
    workflows_dir = root / ".github" / "workflows"
    workflows_dir.mkdir(parents=True, exist_ok=True)
    scripts_dir = root / "scripts"
    scripts_dir.mkdir(parents=True, exist_ok=True)

    texts = dict(workflow_texts or {})
    for folder in cpf.DEPLOYING_FOLDERS:
        texts.setdefault(folder, _deploying_workflow_text(folder))
    texts.setdefault(cpf.NON_BLOCKING_FOLDER, _GOOD_MOBILE_WORKFLOW_TEXT)

    for folder, text in texts.items():
        (workflows_dir / f"{folder}.yml").write_text(text)

    branch_protection_path = scripts_dir / "apply_github_branch_protection.py"
    branch_protection_path.write_text(
        branch_protection_text if branch_protection_text is not None else _good_branch_protection_text()
    )
    return workflows_dir, branch_protection_path


# ---------------------------------------------------------------------------
# Parsing-helper unit tests.
# ---------------------------------------------------------------------------

class FlowListTests(unittest.TestCase):
    def test_parses_single_branch(self) -> None:
        self.assertEqual(cpf._flow_list(["    branches: [main]"], "branches", 4), ["main"])

    def test_parses_multiple_branches(self) -> None:
        self.assertEqual(cpf._flow_list(["    branches: [main, release]"], "branches", 4), ["main", "release"])

    def test_missing_key_returns_none(self) -> None:
        self.assertIsNone(cpf._flow_list(["    paths:"], "branches", 4))


class BlockSeqTests(unittest.TestCase):
    def test_parses_path_items(self) -> None:
        lines = ["    paths:", '      - "infra/**"', '      - ".github/workflows/infra.yml"']
        self.assertEqual(cpf._block_seq(lines, "paths", 4), ["infra/**", ".github/workflows/infra.yml"])

    def test_missing_key_returns_empty_list(self) -> None:
        self.assertEqual(cpf._block_seq(["    branches: [main]"], "paths", 4), [])


class ScalarFieldTests(unittest.TestCase):
    def test_finds_value(self) -> None:
        self.assertEqual(cpf._scalar_field(["    environment: dev"], "environment", 4), "dev")

    def test_missing_key_returns_none(self) -> None:
        self.assertIsNone(cpf._scalar_field(["    environment: dev"], "if", 4))


class ParseStepsTests(unittest.TestCase):
    def test_checkout_step_and_guarded_step(self) -> None:
        steps_block = [
            "      - uses: actions/checkout@v4",
            "      - name: npm ci",
            "        run: npm ci",
            "        continue-on-error: true",
        ]
        steps = cpf._parse_steps(steps_block)
        self.assertEqual(len(steps), 2)
        self.assertFalse(steps[0]["continue_on_error"])
        self.assertTrue(steps[1]["continue_on_error"])

    def test_no_steps_returns_empty_list(self) -> None:
        self.assertEqual(cpf._parse_steps([]), [])


class ParseWorkflowTests(unittest.TestCase):
    def test_parses_triggers_and_jobs(self) -> None:
        workflow = cpf.parse_workflow(_deploying_workflow_text("infra"))
        self.assertEqual(workflow["triggers"]["push"]["paths"], ["infra/**", ".github/workflows/infra.yml"])
        self.assertEqual(workflow["triggers"]["push"]["branches"], ["main"])
        self.assertIn("deploy", workflow["jobs"])
        self.assertIn("refs/heads/main", workflow["jobs"]["deploy"]["if"])

    def test_missing_trigger_is_none(self) -> None:
        text = 'on:\n  push:\n    branches: [main]\n    paths:\n      - "infra/**"\njobs:\n  build:\n    runs-on: ubuntu-latest\n'
        workflow = cpf.parse_workflow(text)
        self.assertIsNone(workflow["triggers"]["pull_request"])


# ---------------------------------------------------------------------------
# check_* unit tests: one good case + one case per failure mode.
# ---------------------------------------------------------------------------

class CheckTriggerScopedTests(unittest.TestCase):
    def test_good_workflow_passes(self) -> None:
        workflow = cpf.parse_workflow(_deploying_workflow_text("infra"))
        passed, detail = cpf.check_trigger_scoped_to_own_folder("infra", "push", workflow)
        self.assertTrue(passed, detail)

    def test_missing_own_path_fails(self) -> None:
        workflow = cpf.parse_workflow(_deploying_workflow_text("infra", own_path="infra-wrong/**"))
        passed, detail = cpf.check_trigger_scoped_to_own_folder("infra", "push", workflow)
        self.assertFalse(passed, detail)
        self.assertIn("infra/**", detail)

    def test_wrong_branch_fails(self) -> None:
        workflow = cpf.parse_workflow(_deploying_workflow_text("backend", branches="[develop]"))
        passed, detail = cpf.check_trigger_scoped_to_own_folder("backend", "push", workflow)
        self.assertFalse(passed, detail)

    def test_cross_folder_leak_fails(self) -> None:
        workflow = cpf.parse_workflow(_deploying_workflow_text("backend", extra_paths='\n      - "web/**"'))
        passed, detail = cpf.check_trigger_scoped_to_own_folder("backend", "push", workflow)
        self.assertFalse(passed, detail)
        self.assertIn("web/**", detail)

    def test_shared_action_path_is_not_a_leak(self) -> None:
        workflow = cpf.parse_workflow(
            _deploying_workflow_text("backend", extra_paths='\n      - ".github/actions/azure-login/**"')
        )
        passed, detail = cpf.check_trigger_scoped_to_own_folder("backend", "push", workflow)
        self.assertTrue(passed, detail)

    def test_missing_trigger_fails(self) -> None:
        text = 'on:\n  push:\n    branches: [main]\n    paths:\n      - "infra/**"\njobs:\n  build:\n    runs-on: ubuntu-latest\n'
        workflow = cpf.parse_workflow(text)
        passed, detail = cpf.check_trigger_scoped_to_own_folder("infra", "pull_request", workflow)
        self.assertFalse(passed, detail)


class CheckDevDeployTests(unittest.TestCase):
    def test_good_workflow_passes(self) -> None:
        workflow = cpf.parse_workflow(_deploying_workflow_text("web"))
        passed, detail = cpf.check_dev_deploy_on_merge_to_main("web", workflow)
        self.assertTrue(passed, detail)

    def test_no_gated_job_fails(self) -> None:
        workflow = cpf.parse_workflow(_deploying_workflow_text("web", include_deploy_if=False))
        passed, detail = cpf.check_dev_deploy_on_merge_to_main("web", workflow)
        self.assertFalse(passed, detail)

    def test_wrong_environment_default_fails(self) -> None:
        workflow = cpf.parse_workflow(_deploying_workflow_text("infra", deploy_environment="demo"))
        passed, detail = cpf.check_dev_deploy_on_merge_to_main("infra", workflow)
        self.assertFalse(passed, detail)

    def test_two_gated_jobs_fails(self) -> None:
        workflow = cpf.parse_workflow(_deploying_workflow_text("infra", extra_job=_EXTRA_DEPLOY_JOB))
        passed, detail = cpf.check_dev_deploy_on_merge_to_main("infra", workflow)
        self.assertFalse(passed, detail)
        self.assertIn("2 jobs", detail)


class CheckMobileBuildNonBlockingTests(unittest.TestCase):
    def test_good_mobile_passes(self) -> None:
        workflow = cpf.parse_workflow(_GOOD_MOBILE_WORKFLOW_TEXT)
        passed, detail = cpf.check_mobile_build_non_blocking(workflow)
        self.assertTrue(passed, detail)

    def test_missing_job_level_flag_fails(self) -> None:
        workflow = cpf.parse_workflow(_mobile_workflow_text(job_continue_on_error=False))
        passed, detail = cpf.check_mobile_build_non_blocking(workflow)
        self.assertFalse(passed, detail)

    def test_missing_step_level_flag_fails(self) -> None:
        workflow = cpf.parse_workflow(_mobile_workflow_text(guarded_step_indexes=(0, 1, 2)))
        passed, detail = cpf.check_mobile_build_non_blocking(workflow)
        self.assertFalse(passed, detail)
        self.assertIn("npm test", detail)

    def test_checkout_step_itself_is_exempt_from_the_guard(self) -> None:
        workflow = cpf.parse_workflow(_GOOD_MOBILE_WORKFLOW_TEXT)
        checkout_steps = [s for s in workflow["jobs"]["build"]["steps"] if cpf._CHECKOUT_STEP_RE.search(s["header"])]
        self.assertEqual(len(checkout_steps), 1)
        self.assertFalse(checkout_steps[0]["continue_on_error"])
        # And the overall check still passes despite that.
        passed, _ = cpf.check_mobile_build_non_blocking(workflow)
        self.assertTrue(passed)


class CheckMobileNoDeployEnvironmentTests(unittest.TestCase):
    def test_good_mobile_passes(self) -> None:
        workflow = cpf.parse_workflow(_GOOD_MOBILE_WORKFLOW_TEXT)
        passed, detail = cpf.check_mobile_has_no_deploy_environment(workflow)
        self.assertTrue(passed, detail)

    def test_environment_field_fails(self) -> None:
        workflow = cpf.parse_workflow(_mobile_workflow_text(extra_job_fields="    environment: dev\n"))
        passed, detail = cpf.check_mobile_has_no_deploy_environment(workflow)
        self.assertFalse(passed, detail)


class CheckOnlyMobileNonBlockingTests(unittest.TestCase):
    def test_good_tree_passes(self) -> None:
        parsed = {
            "infra": cpf.parse_workflow(_deploying_workflow_text("infra")),
            "backend": cpf.parse_workflow(_deploying_workflow_text("backend")),
            "web": cpf.parse_workflow(_deploying_workflow_text("web")),
            "mobile": cpf.parse_workflow(_GOOD_MOBILE_WORKFLOW_TEXT),
        }
        passed, detail = cpf.check_only_mobile_non_blocking(parsed)
        self.assertTrue(passed, detail)

    def test_non_mobile_continue_on_error_fails(self) -> None:
        parsed = {
            "infra": cpf.parse_workflow(_deploying_workflow_text("infra")),
            "backend": cpf.parse_workflow(_deploying_workflow_text("backend", build_continue_on_error=True)),
            "web": cpf.parse_workflow(_deploying_workflow_text("web")),
            "mobile": cpf.parse_workflow(_GOOD_MOBILE_WORKFLOW_TEXT),
        }
        passed, detail = cpf.check_only_mobile_non_blocking(parsed)
        self.assertFalse(passed, detail)
        self.assertIn("backend.yml:build", detail)


class CheckMobileExcludedFromRequiredStatusChecksTests(unittest.TestCase):
    def _write(self, tmp_path: Path, text: str) -> Path:
        path = tmp_path / "apply_github_branch_protection.py"
        path.write_text(text)
        return path

    def test_empty_contexts_passes(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = self._write(Path(tmp), _good_branch_protection_text())
            passed, detail = cpf.check_mobile_excluded_from_required_status_checks(path)
            self.assertTrue(passed, detail)

    def test_non_mobile_contexts_pass(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = self._write(Path(tmp), _good_branch_protection_text('"backend build", "web build"'))
            passed, detail = cpf.check_mobile_excluded_from_required_status_checks(path)
            self.assertTrue(passed, detail)

    def test_mobile_context_fails(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = self._write(Path(tmp), _good_branch_protection_text('"mobile build"'))
            passed, detail = cpf.check_mobile_excluded_from_required_status_checks(path)
            self.assertFalse(passed, detail)

    def test_missing_file_fails(self) -> None:
        passed, detail = cpf.check_mobile_excluded_from_required_status_checks(Path("/nonexistent/apply.py"))
        self.assertFalse(passed, detail)

    def test_missing_declaration_fails(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            path = self._write(Path(tmp), "# nothing here\n")
            passed, detail = cpf.check_mobile_excluded_from_required_status_checks(path)
            self.assertFalse(passed, detail)


# ---------------------------------------------------------------------------
# Full-tree fixture tests.
# ---------------------------------------------------------------------------

class GoodFixtureTreeTests(unittest.TestCase):
    """Every check_* (via run_all_checks) must pass against a deliberately
    correct fixture tree."""

    def setUp(self) -> None:
        self._tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self._tmp.cleanup)
        self.workflows_dir, self.branch_protection_path = _write_fixture_tree(Path(self._tmp.name))

    def test_all_checks_pass(self) -> None:
        for name, (passed, detail) in cpf.run_all_checks(self.workflows_dir, self.branch_protection_path):
            self.assertTrue(passed, f"{name}: {detail}")


class BrokenFixtureTreeTests(unittest.TestCase):
    """One deliberately-broken fixture tree per failure mode this scan must
    catch, run through the full run_all_checks() pipeline (not just the
    isolated check_* call) to prove nothing upstream crashes either."""

    def _tree(self, **kwargs) -> tuple:
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        return _write_fixture_tree(Path(tmp.name), **kwargs)

    def test_missing_workflow_file_reported_and_does_not_crash(self) -> None:
        workflows_dir, branch_protection_path = self._tree()
        (workflows_dir / "infra.yml").unlink()
        results = dict(cpf.run_all_checks(workflows_dir, branch_protection_path))
        self.assertFalse(results["infra.yml exists"][0])
        self.assertTrue(results["backend.yml exists"][0])

    def test_leaked_path_reported(self) -> None:
        workflows_dir, branch_protection_path = self._tree(
            workflow_texts={"backend": _deploying_workflow_text("backend", extra_paths='\n      - "web/**"')}
        )
        results = dict(cpf.run_all_checks(workflows_dir, branch_protection_path))
        passed, detail = results["backend.yml on.push scoped to backend/ (AC-1)"]
        self.assertFalse(passed, detail)

    def test_missing_dev_deploy_reported(self) -> None:
        workflows_dir, branch_protection_path = self._tree(
            workflow_texts={"web": _deploying_workflow_text("web", include_deploy_if=False)}
        )
        results = dict(cpf.run_all_checks(workflows_dir, branch_protection_path))
        passed, detail = results["web.yml dev deploy on merge to main (AC-2)"]
        self.assertFalse(passed, detail)

    def test_mobile_missing_non_blocking_reported(self) -> None:
        workflows_dir, branch_protection_path = self._tree(
            workflow_texts={"mobile": _mobile_workflow_text(job_continue_on_error=False)}
        )
        results = dict(cpf.run_all_checks(workflows_dir, branch_protection_path))
        passed, detail = results["mobile.yml build job non-blocking (AC-3)"]
        self.assertFalse(passed, detail)

    def test_non_mobile_marked_non_blocking_reported(self) -> None:
        workflows_dir, branch_protection_path = self._tree(
            workflow_texts={"infra": _deploying_workflow_text("infra", build_continue_on_error=True)}
        )
        results = dict(cpf.run_all_checks(workflows_dir, branch_protection_path))
        passed, detail = results["only mobile is marked continue-on-error (AC-3 scope)"]
        self.assertFalse(passed, detail)

    def test_mobile_marked_in_required_checks_reported(self) -> None:
        workflows_dir, branch_protection_path = self._tree(
            branch_protection_text=_good_branch_protection_text('"mobile build"')
        )
        results = dict(cpf.run_all_checks(workflows_dir, branch_protection_path))
        self.assertFalse(results["mobile excluded from required status checks"][0])


class RealRepoStructuralScanTests(unittest.TestCase):
    """Same invocation this task's own definition of done relies on."""

    def test_all_checks_pass_against_the_real_repo(self) -> None:
        for name, (passed, detail) in cpf.run_all_checks():
            self.assertTrue(passed, f"{name}: {detail}")


class MainEntryPointTests(unittest.TestCase):
    def test_main_passes_against_the_real_repo(self) -> None:
        """End-to-end: running the actual script against this real working
        tree exits 0 -- the same invocation the task's definition of done
        and any future CI status check both rely on."""
        proc = subprocess.run(
            [sys.executable, str(REPO_ROOT / "scripts" / "ci_path_filters_verify.py")],
            cwd=REPO_ROOT,
            text=True,
            capture_output=True,
            timeout=30,
        )
        self.assertEqual(proc.returncode, 0, msg=proc.stdout + proc.stderr)


if __name__ == "__main__":
    unittest.main()
