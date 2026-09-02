#!/usr/bin/env python3
"""Structural scan for the Key Vault scope-grant chain (task E01/F02/US04/T02,
keyvault-scope-grants).

Parent story `us-04-entra-keyvault` AC-3 ("API/worker managed identities
granted get/list on their own env's vault only") and this task's own coding
objective ("Grant each env API/worker managed identity only its own Key
Vault") need more than the existence of a role assignment. Task
E01/F02/US04/T01 (already merged into this branch) created each
environment's workload identity and granted it "Key Vault Secrets User" on
that environment's own vault -- but never assigned that identity to the API
or worker Container Apps that are supposed to *present* it at runtime. A
grant to an identity nothing runs as is not a functioning grant: without an
`identity {}` block on the Container Apps, the API/worker would authenticate
as *no* identity at all, and T01's role assignment would be unreachable
dead configuration. This task closes exactly that gap:

  - `modules/identity/outputs.tf` gains `workload_identity_id` (the
    identity's ARM resource id -- a different shape than
    `workload_principal_id`, which is the AAD object id T01's role
    assignment already consumes).
  - `modules/containerapps` gains a required `workload_identity_id`
    variable and assigns it as a `UserAssigned` identity on both the "api"
    and "worker" Container Apps.
  - both `infra/environments/{dev,demo}/main.tf` wire that variable from
    their OWN `module.identity.workload_identity_id` output only -- never
    a literal, never the other environment's.

As with task T01's own scan (`scripts/entra_keyvault_provision_scan.py`),
a real `terraform plan` needs a live HCP Terraform login plus an
authenticated azurerm/azuread provider, neither available in this harness.
This script is the repeatable, credential-free proof of the shape a
`terraform plan` would otherwise show, and it re-asserts (rather than just
assuming) that T01's own grant is still scoped correctly, so this task's own
definition of done does not silently depend on a different task's test
file continuing to pass.

Checks, all read-only, no network, no `terraform` binary, no Azure/Entra
credentials required.

Usage:
    python scripts/keyvault_scope_grants_check.py

Exit 0 if every check passes. Non-zero otherwise, with a PASS/FAIL line per
check on stdout (failures also echoed to stderr).
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

SCRIPTS_ROOT = Path(__file__).resolve().parent
if str(SCRIPTS_ROOT) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_ROOT))

from terraform_env_roots_scan import _extract_block, _find_block, _strip_line_comments  # noqa: E402
import entra_keyvault_provision_scan as ekp  # noqa: E402
import repo_secret_scan as secret_scan  # noqa: E402

REPO_ROOT = SCRIPTS_ROOT.parent
INFRA_ROOT = REPO_ROOT / "infra"
MODULES_ROOT = INFRA_ROOT / "modules"
ENVIRONMENTS_ROOT = INFRA_ROOT / "environments"
IDENTITY_DIR = MODULES_ROOT / "identity"
KEYVAULT_DIR = MODULES_ROOT / "keyvault"
CONTAINERAPPS_DIR = MODULES_ROOT / "containerapps"

ENVS = ("dev", "demo")

FILES_TO_SECRET_SCAN_RELATIVE = (
    "modules/identity/outputs.tf",
    "modules/containerapps/main.tf",
    "modules/containerapps/variables.tf",
    "environments/dev/main.tf",
    "environments/demo/main.tf",
)


# ---------------------------------------------------------------------------
# check_* -- each returns (passed, detail), mirroring the sibling scan
# scripts (scripts/entra_keyvault_provision_scan.py and friends). Root
# paths are parameters defaulting to the real repo layout so tests can
# point them at a synthetic fixture tree.
# ---------------------------------------------------------------------------

def check_required_files_present(
    identity_dir: Path = IDENTITY_DIR,
    keyvault_dir: Path = KEYVAULT_DIR,
    containerapps_dir: Path = CONTAINERAPPS_DIR,
) -> tuple:
    missing = []
    for base, names in (
        (identity_dir, ("outputs.tf",)),
        (keyvault_dir, ("main.tf", "variables.tf")),
        (containerapps_dir, ("main.tf", "variables.tf")),
    ):
        for name in names:
            if not (base / name).is_file():
                missing.append(f"{base.name}/{name}")
    if missing:
        return False, f"missing file(s): {', '.join(missing)}"
    return True, "modules/identity, modules/keyvault and modules/containerapps have the files this check reads"


def check_containerapps_identity_variable(containerapps_dir: Path = CONTAINERAPPS_DIR) -> tuple:
    path = containerapps_dir / "variables.tf"
    if not path.is_file():
        return False, "modules/containerapps/variables.tf does not exist"
    text = _strip_line_comments(path.read_text(encoding="utf-8"))
    body = _find_block(text, r'variable\s+"workload_identity_id"\s*')
    if body is None:
        return False, 'modules/containerapps/variables.tf has no variable "workload_identity_id"'
    var_type = ekp.find_attr(body, "type")
    if var_type != "string":
        return False, f'variable "workload_identity_id" type={var_type!r}, expected "string"'
    if ekp.find_attr(body, "default") is not None:
        return False, (
            'variable "workload_identity_id" has a default -- it must be required so every '
            "caller wires it explicitly"
        )
    return True, 'modules/containerapps/variables.tf declares a required variable "workload_identity_id" (string, no default)'


def check_containerapps_identity_assignment(containerapps_dir: Path = CONTAINERAPPS_DIR) -> tuple:
    path = containerapps_dir / "main.tf"
    if not path.is_file():
        return False, "modules/containerapps/main.tf does not exist"
    text = _strip_line_comments(path.read_text(encoding="utf-8"))
    problems = []
    for name in ("api", "worker"):
        body = ekp.find_resource_block(text, "azurerm_container_app", name)
        if body is None:
            problems.append(f'no resource "azurerm_container_app" "{name}"')
            continue
        identity_body = _find_block(body, r"\bidentity\b")
        if identity_body is None:
            problems.append(f"azurerm_container_app.{name} has no identity {{}} block")
            continue
        id_type = ekp.find_attr(identity_body, "type")
        if id_type != '"UserAssigned"':
            problems.append(
                f"azurerm_container_app.{name} identity.type={id_type!r}, expected '\"UserAssigned\"'"
            )
        ids = ekp.find_attr(identity_body, "identity_ids")
        if ids != "[var.workload_identity_id]":
            problems.append(
                f"azurerm_container_app.{name} identity.identity_ids={ids!r}, "
                "expected '[var.workload_identity_id]'"
            )
    if problems:
        return False, "; ".join(problems)
    return True, (
        'azurerm_container_app.api and .worker both have identity { type = "UserAssigned", '
        "identity_ids = [var.workload_identity_id] }"
    )


def check_identity_module_exposes_identity_id(identity_dir: Path = IDENTITY_DIR) -> tuple:
    path = identity_dir / "outputs.tf"
    if not path.is_file():
        return False, "modules/identity/outputs.tf does not exist"
    text = _strip_line_comments(path.read_text(encoding="utf-8"))
    found = {}
    for m in re.finditer(r'output\s+"([A-Za-z0-9_-]+)"\s*{', text):
        body = _extract_block(text, m.end() - 1)
        found[m.group(1)] = ekp.find_attr(body, "value")
    value = found.get("workload_identity_id")
    if value is None:
        return False, 'modules/identity/outputs.tf has no output "workload_identity_id"'
    if value != "azurerm_user_assigned_identity.workload.id":
        return False, (
            f'output "workload_identity_id" value={value!r}, expected '
            "'azurerm_user_assigned_identity.workload.id'"
        )
    return True, 'modules/identity/outputs.tf output "workload_identity_id" = azurerm_user_assigned_identity.workload.id'


def check_env_root_wires_containerapps_identity(env: str, environments_root: Path = ENVIRONMENTS_ROOT) -> tuple:
    main_path = environments_root / env / "main.tf"
    if not main_path.is_file():
        return False, f"{env}/main.tf does not exist"
    text = _strip_line_comments(main_path.read_text(encoding="utf-8"))
    block = _find_block(text, r'module\s+"containerapps"\s*')
    if block is None:
        return False, f'{env}/main.tf has no module "containerapps" block'
    value = ekp.find_attr(block, "workload_identity_id")
    if value != "module.identity.workload_identity_id":
        return False, (
            f'{env}/main.tf module "containerapps".workload_identity_id={value!r}, '
            "expected 'module.identity.workload_identity_id' (this root's own identity module instance)"
        )
    return True, (
        f'{env}/main.tf module "containerapps" is assigned this root\'s own '
        "module.identity.workload_identity_id"
    )


def check_grant_and_assignment_share_identity_instance(
    env: str, environments_root: Path = ENVIRONMENTS_ROOT
) -> tuple:
    """The identity module.keyvault grants access FOR (workload_principal_id)
    and the identity module.containerapps is assigned to run AS
    (workload_identity_id) must resolve to the exact same module.identity
    instance in a given environment root. Each half already has to equal a
    fixed expression (checked separately above and in
    entra_keyvault_provision_scan), so this is not a new source of truth --
    it is the explicit, single assertion of the sentence AC-3 actually
    requires: the identity presented by the API/worker *is* the identity
    granted the vault, not two identities that merely happen to coexist.
    """
    main_path = environments_root / env / "main.tf"
    if not main_path.is_file():
        return False, f"{env}/main.tf does not exist"
    text = _strip_line_comments(main_path.read_text(encoding="utf-8"))
    kv_block = _find_block(text, r'module\s+"keyvault"\s*')
    ca_block = _find_block(text, r'module\s+"containerapps"\s*')
    if kv_block is None or ca_block is None:
        return False, f'{env}/main.tf missing module "keyvault" and/or "containerapps" block'
    kv_value = ekp.find_attr(kv_block, "workload_principal_id")
    ca_value = ekp.find_attr(ca_block, "workload_identity_id")
    kv_instance = kv_value.rsplit(".", 1)[0] if kv_value and "." in kv_value else None
    ca_instance = ca_value.rsplit(".", 1)[0] if ca_value and "." in ca_value else None
    if kv_instance != ca_instance or kv_instance != "module.identity":
        return False, (
            f'{env}/main.tf module "keyvault" reads {kv_value!r} (instance {kv_instance!r}), '
            f'module "containerapps" reads {ca_value!r} (instance {ca_instance!r}) -- both must '
            "resolve to the same 'module.identity' instance, or the grant and the assignment "
            "could silently drift onto two different identities"
        )
    return True, (
        f"{env}/main.tf grants module.keyvault access FOR, and assigns module.containerapps to "
        "run AS, the exact same module.identity instance"
    )


def check_keyvault_grant_still_scoped_to_own_vault(keyvault_dir: Path = KEYVAULT_DIR) -> tuple:
    """Re-runs task T01's own role-assignment check as part of *this* task's
    proof. This task's artifact (keyvault-scope-grants) is only true end to
    end if T01's grant is still scoped correctly -- this does not assume
    that stays true just because a different task's test file currently
    passes."""
    return ekp.check_keyvault_role_assignment(keyvault_dir)


def check_no_access_policy_block(keyvault_dir: Path = KEYVAULT_DIR) -> tuple:
    path = keyvault_dir / "main.tf"
    if not path.is_file():
        return False, "modules/keyvault/main.tf does not exist"
    text = _strip_line_comments(path.read_text(encoding="utf-8"))
    if re.search(r"\baccess_policy\s*{", text):
        return False, (
            "modules/keyvault/main.tf declares an access_policy {} block -- "
            "azurerm_key_vault.this.rbac_authorization_enabled=true means Azure ignores it, and "
            "its presence would misleadingly suggest a second, legacy grant mechanism is live"
        )
    return True, "modules/keyvault/main.tf has no access_policy {} block -- RBAC is the only grant mechanism"


def check_no_secret_literals(repo_root: Path = REPO_ROOT, infra_root: Path = INFRA_ROOT) -> tuple:
    hits = []
    scanned = 0
    for rel in FILES_TO_SECRET_SCAN_RELATIVE:
        path = infra_root / rel
        if not path.is_file():
            return False, f"infra/{rel} does not exist"
        try:
            display = str(path.relative_to(repo_root)).replace("\\", "/")
        except ValueError:
            display = f"infra/{rel}"
        text = path.read_text(encoding="utf-8", errors="ignore")
        scanned += 1
        hits.extend(secret_scan.find_secret_matches(display, text))
    if hits:
        return False, "; ".join(hits)
    return True, f"{scanned} file(s) scanned (identity outputs, containerapps module, dev/demo env roots), no secret-shaped strings found"


def run_all_checks(
    identity_dir: Path = IDENTITY_DIR,
    keyvault_dir: Path = KEYVAULT_DIR,
    containerapps_dir: Path = CONTAINERAPPS_DIR,
    environments_root: Path = ENVIRONMENTS_ROOT,
    repo_root: Path = REPO_ROOT,
    infra_root: Path = INFRA_ROOT,
) -> list:
    checks = [
        ("required files present", check_required_files_present(identity_dir, keyvault_dir, containerapps_dir)),
        ("containerapps workload_identity_id variable", check_containerapps_identity_variable(containerapps_dir)),
        ("containerapps identity assignment", check_containerapps_identity_assignment(containerapps_dir)),
        ("identity module exposes workload_identity_id", check_identity_module_exposes_identity_id(identity_dir)),
    ]
    for env in ENVS:
        checks.append(
            (f"{env} wires containerapps identity", check_env_root_wires_containerapps_identity(env, environments_root))
        )
    for env in ENVS:
        checks.append(
            (
                f"{env} grant + assignment share one identity instance",
                check_grant_and_assignment_share_identity_instance(env, environments_root),
            )
        )
    checks.append(("Key Vault grant still scoped to own vault", check_keyvault_grant_still_scoped_to_own_vault(keyvault_dir)))
    checks.append(("no legacy access_policy block", check_no_access_policy_block(keyvault_dir)))
    checks.append(("no secret literals", check_no_secret_literals(repo_root, infra_root)))
    return checks


def main() -> int:
    ok = True
    for name, (passed, detail) in run_all_checks():
        print(f"[{'PASS' if passed else 'FAIL'}] {name}: {detail}")
        ok = ok and passed

    if ok:
        print(
            "[keyvault_scope_grants_check] PASS: each env's API/worker Container Apps are assigned "
            "that env's own workload identity, which is granted 'Key Vault Secrets User' on that "
            "env's own Key Vault only (ADR-011)"
        )
        return 0
    print("[keyvault_scope_grants_check] FAIL: see above", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
