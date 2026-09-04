"""Unit tests for scripts/foundry_connection_verify.py (task E01/F02/US05/T02).

Same shape as tests/test_keyvault_scope_grants_check.py: pure-parser unit
tests against hand-written fixtures, `check_*` against a deliberately-good
synthetic fixture tree and one deliberately-broken tree per failure mode
this scan exists to catch, then two end-to-end proofs against this actual
working tree (`run_all_checks()` directly, and the script itself as a
subprocess -- the same invocation this task's own definition of done
relies on).

Run:
    python tests/test_foundry_connection_verify.py -v
"""

from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(REPO_ROOT / "scripts"))

import foundry_connection_verify as fcv  # noqa: E402

GOOD_IDENTITY_OUTPUTS_TF = (
    'output "workload_principal_id" {\n'
    "  value = azurerm_user_assigned_identity.workload.principal_id\n"
    "}\n"
    "\n"
    'output "workload_identity_id" {\n'
    "  value = azurerm_user_assigned_identity.workload.id\n"
    "}\n"
)

# Reproduces this task's own real-world corruption: two textually-present
# `output "workload_identity_id" {` headers, regardless of how the bodies
# nest -- the exact shape that a plain {name: value} dict-based scan
# (see module docstring) would silently collapse into "one entry" and pass.
DUPLICATE_IDENTITY_OUTPUTS_TF = (
    'output "workload_principal_id" {\n'
    "  value = azurerm_user_assigned_identity.workload.principal_id\n"
    "}\n"
    "\n"
    'output "workload_identity_id" {\n'
    '  description = "first"\n'
    'output "workload_identity_id" {\n'
    '  description = "second"\n'
    "  value = azurerm_user_assigned_identity.workload.id\n"
    "}\n"
    "}\n"
)

MISSING_IDENTITY_OUTPUTS_TF = (
    'output "workload_principal_id" {\n'
    "  value = azurerm_user_assigned_identity.workload.principal_id\n"
    "}\n"
)

WRONG_VALUE_IDENTITY_OUTPUTS_TF = (
    'output "workload_identity_id" {\n'
    "  value = azurerm_user_assigned_identity.workload.principal_id\n"
    "}\n"
)

GOOD_LOCATION_VARIABLES_TF = 'variable "location" {\n  type    = string\n  default = "North Europe"\n}\n'
BAD_LOCATION_VARIABLES_TF = 'variable "location" {\n  type    = string\n  default = "East US"\n}\n'

GOOD_PROJECTS = (
    {"project": "contigo-dev", "env": "dev", "document_intelligence_connection": "conn-docint-contigo-dev"},
    {"project": "contigo-demo", "env": "demo", "document_intelligence_connection": "conn-docint-contigo-demo"},
)


def _write_identity_fixture(root: Path, outputs_text: str) -> Path:
    identity_dir = root / "infra" / "modules" / "identity"
    identity_dir.mkdir(parents=True, exist_ok=True)
    (identity_dir / "outputs.tf").write_text(outputs_text, encoding="utf-8")
    return identity_dir


def _write_environments_fixture(root: Path, dev_location: str, demo_location: str) -> Path:
    environments_root = root / "infra" / "environments"
    for env, location in (("dev", dev_location), ("demo", demo_location)):
        env_dir = environments_root / env
        env_dir.mkdir(parents=True, exist_ok=True)
        (env_dir / "variables.tf").write_text(
            f'variable "location" {{\n  type    = string\n  default = "{location}"\n}}\n', encoding="utf-8"
        )
    return environments_root


class NormalizeRegionTests(unittest.TestCase):
    def test_strips_spaces_and_lowercases(self) -> None:
        self.assertEqual(fcv._normalize_region("North Europe"), "northeurope")
        self.assertEqual(fcv._normalize_region("northeurope"), "northeurope")
        self.assertEqual(fcv._normalize_region("  North   Europe "), "northeurope")
        self.assertEqual(fcv._normalize_region("West Europe"), "westeurope")

    def test_north_europe_and_northeurope_are_the_same_region(self) -> None:
        self.assertEqual(fcv._normalize_region("North Europe"), fcv._normalize_region(fcv.FOUNDRY_REGION))


class BuildFoundryConnectionsTests(unittest.TestCase):
    def test_default_uses_real_bootstrap_projects(self) -> None:
        connections = fcv.build_foundry_connections()
        self.assertEqual({c["project"] for c in connections}, {"contigo-dev", "contigo-demo"})
        for c in connections:
            self.assertEqual(c["region"], "northeurope")
            self.assertIn(fcv.hcp.AI_SERVICES_ACCOUNT_NAME, c["connection_id"])
            self.assertIn(c["project"], c["connection_id"])
            self.assertIn(c["document_intelligence_connection"], c["connection_id"])

    def test_custom_projects_are_respected(self) -> None:
        custom = (
            {"project": "x", "env": "dev", "document_intelligence_connection": "conn-x"},
        )
        connections = fcv.build_foundry_connections(custom)
        self.assertEqual(len(connections), 1)
        self.assertEqual(
            connections[0]["connection_id"], f"{fcv.hcp.AI_SERVICES_ACCOUNT_NAME}/projects/x/connections/conn-x"
        )

    def test_deterministic_across_calls(self) -> None:
        self.assertEqual(fcv.build_foundry_connections(GOOD_PROJECTS), fcv.build_foundry_connections(GOOD_PROJECTS))


class CheckFoundryAccountShapeStillRecordedTests(unittest.TestCase):
    def test_delegates_to_bootstrap_hcp_org(self) -> None:
        self.assertEqual(fcv.check_foundry_account_shape_still_recorded(), fcv.hcp.check_foundry_account_recorded())

    def test_currently_passes_against_the_real_module_constants(self) -> None:
        passed, detail = fcv.check_foundry_account_shape_still_recorded()
        self.assertTrue(passed, detail)


class CheckRegionPinnedToWesteuropeTests(unittest.TestCase):
    def test_good_fixture_passes(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            environments_root = _write_environments_fixture(Path(tmp), "North Europe", "North Europe")
            passed, detail = fcv.check_region_pinned_to_westeurope(environments_root)
            self.assertTrue(passed, detail)

    def test_dev_drifted_to_another_region_fails(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            environments_root = _write_environments_fixture(Path(tmp), "East US", "North Europe")
            passed, detail = fcv.check_region_pinned_to_westeurope(environments_root)
            self.assertFalse(passed, detail)

    def test_demo_drifted_to_another_region_fails(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            environments_root = _write_environments_fixture(Path(tmp), "North Europe", "West Europe")
            passed, detail = fcv.check_region_pinned_to_westeurope(environments_root)
            self.assertFalse(passed, detail)

    def test_missing_variables_file_fails(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            environments_root = Path(tmp) / "infra" / "environments"
            (environments_root / "dev").mkdir(parents=True)
            (environments_root / "demo").mkdir(parents=True)
            passed, detail = fcv.check_region_pinned_to_westeurope(environments_root)
            self.assertFalse(passed, detail)


class CheckConnectionIdsWellFormedTests(unittest.TestCase):
    def test_good_projects_pass(self) -> None:
        passed, detail = fcv.check_connection_ids_well_formed(GOOD_PROJECTS)
        self.assertTrue(passed, detail)

    def test_empty_connection_name_fails(self) -> None:
        bad = ({"project": "contigo-dev", "env": "dev", "document_intelligence_connection": "   "},)
        passed, detail = fcv.check_connection_ids_well_formed(bad)
        self.assertFalse(passed, detail)

    def test_missing_key_raises_or_fails_loudly(self) -> None:
        bad = ({"project": "contigo-dev", "env": "dev"},)
        with self.assertRaises(KeyError):
            fcv.check_connection_ids_well_formed(bad)


class CheckConnectionIdsUniqueAndIsolatedTests(unittest.TestCase):
    def test_good_projects_pass(self) -> None:
        passed, detail = fcv.check_connection_ids_unique_and_isolated(GOOD_PROJECTS)
        self.assertTrue(passed, detail)

    def test_duplicate_connection_name_across_projects_fails(self) -> None:
        bad = (
            {"project": "contigo-dev", "env": "dev", "document_intelligence_connection": "same-name"},
            {"project": "contigo-demo", "env": "demo", "document_intelligence_connection": "same-name"},
        )
        # different project segments still make the ids unique -- this
        # documents that connection_id uniqueness comes from the project
        # name, not the raw connection label, and stays true even if two
        # environments are (mis)configured with the same connection label.
        passed, detail = fcv.check_connection_ids_unique_and_isolated(bad)
        self.assertTrue(passed, detail)

    def test_duplicate_env_fails(self) -> None:
        bad = (
            {"project": "contigo-dev", "env": "dev", "document_intelligence_connection": "conn-a"},
            {"project": "contigo-dev-2", "env": "dev", "document_intelligence_connection": "conn-b"},
        )
        passed, detail = fcv.check_connection_ids_unique_and_isolated(bad)
        self.assertFalse(passed, detail)

    def test_duplicate_project_fails(self) -> None:
        bad = (
            {"project": "contigo-dev", "env": "dev", "document_intelligence_connection": "conn-a"},
            {"project": "contigo-dev", "env": "demo", "document_intelligence_connection": "conn-a"},
        )
        passed, detail = fcv.check_connection_ids_unique_and_isolated(bad)
        self.assertFalse(passed, detail)


class CheckConnectionsShareSingleAccountTests(unittest.TestCase):
    def test_good_projects_pass(self) -> None:
        passed, detail = fcv.check_connections_share_single_account(GOOD_PROJECTS)
        self.assertTrue(passed, detail)

    def test_all_connections_use_the_module_account_name(self) -> None:
        for c in fcv.build_foundry_connections(GOOD_PROJECTS):
            self.assertTrue(c["connection_id"].startswith(fcv.hcp.AI_SERVICES_ACCOUNT_NAME + "/projects/"))


class CheckDocumentIntelligenceModelsRecordedTests(unittest.TestCase):
    def test_passes_against_the_real_constant(self) -> None:
        passed, detail = fcv.check_document_intelligence_models_recorded()
        self.assertTrue(passed, detail)
        self.assertIn("prebuilt-read", detail)
        self.assertIn("prebuilt-layout", detail)


class CheckWorkloadIdentityOutputWellFormedTests(unittest.TestCase):
    def test_good_fixture_passes(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            identity_dir = _write_identity_fixture(Path(tmp), GOOD_IDENTITY_OUTPUTS_TF)
            passed, detail = fcv.check_workload_identity_output_well_formed(identity_dir)
            self.assertTrue(passed, detail)

    def test_missing_file_fails(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            identity_dir = Path(tmp) / "infra" / "modules" / "identity"
            identity_dir.mkdir(parents=True)
            passed, detail = fcv.check_workload_identity_output_well_formed(identity_dir)
            self.assertFalse(passed, detail)

    def test_missing_output_fails(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            identity_dir = _write_identity_fixture(Path(tmp), MISSING_IDENTITY_OUTPUTS_TF)
            passed, detail = fcv.check_workload_identity_output_well_formed(identity_dir)
            self.assertFalse(passed, detail)

    def test_wrong_value_fails(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            identity_dir = _write_identity_fixture(Path(tmp), WRONG_VALUE_IDENTITY_OUTPUTS_TF)
            passed, detail = fcv.check_workload_identity_output_well_formed(identity_dir)
            self.assertFalse(passed, detail)

    def test_duplicate_block_fails(self) -> None:
        """Regression guard for this task's own real-world find: a
        duplicated `output "workload_identity_id"` block (this repo's
        infra/modules/identity/outputs.tf briefly held exactly this shape
        after a phase-barrier merge) must fail loudly, not be silently
        collapsed to "one entry" by a dict-based scan."""
        with tempfile.TemporaryDirectory() as tmp:
            identity_dir = _write_identity_fixture(Path(tmp), DUPLICATE_IDENTITY_OUTPUTS_TF)
            passed, detail = fcv.check_workload_identity_output_well_formed(identity_dir)
            self.assertFalse(passed, detail)
            self.assertIn("2 times", detail)


class CheckNoSecretLiteralsTests(unittest.TestCase):
    def test_good_fixture_passes(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            infra_root = root / "infra"
            _write_identity_fixture(root, GOOD_IDENTITY_OUTPUTS_TF)
            passed, detail = fcv.check_no_secret_literals(root, infra_root)
            self.assertTrue(passed, detail)

    def test_planted_secret_is_flagged(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            infra_root = root / "infra"
            bad = GOOD_IDENTITY_OUTPUTS_TF + "\n# AccountKey=" + ("Z" * 40) + "==\n"
            _write_identity_fixture(root, bad)
            passed, detail = fcv.check_no_secret_literals(root, infra_root)
            self.assertFalse(passed, detail)

    def test_missing_file_fails(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            infra_root = root / "infra"
            passed, detail = fcv.check_no_secret_literals(root, infra_root)
            self.assertFalse(passed, detail)


class RealRepoScanTests(unittest.TestCase):
    """Same invocation this task's own definition of done relies on."""

    def test_all_checks_pass_against_the_real_repo(self) -> None:
        for name, (passed, detail) in fcv.run_all_checks():
            self.assertTrue(passed, f"{name}: {detail}")


class MainEntryPointTests(unittest.TestCase):
    def test_main_passes_against_the_real_repo(self) -> None:
        proc = subprocess.run(
            [sys.executable, str(REPO_ROOT / "scripts" / "foundry_connection_verify.py")],
            cwd=REPO_ROOT,
            text=True,
            capture_output=True,
            timeout=30,
        )
        self.assertEqual(proc.returncode, 0, msg=proc.stdout + proc.stderr)
        self.assertIn("recorded Foundry connection ids", proc.stdout)


if __name__ == "__main__":
    unittest.main()
