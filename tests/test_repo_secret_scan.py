"""Unit tests for scripts/repo_secret_scan.py (task E01/F01/US01/T02).

Covers: the five-folder layout check, the secret-pattern matcher on
synthetic fixtures, the real `git ls-files` wiring against a throwaway repo,
and one end-to-end run of the script against this actual working tree (the
same invocation the task's definition of done relies on).

NOTE ON THIS FILE'S OWN FIXTURES: several tests below plant strings shaped
like real secrets (a fake AWS key id, a fake private-key block, ...) to
prove `find_secret_matches` detects them. Because this file is itself
git-tracked, scripts/repo_secret_scan.py would otherwise flag its own test
fixtures as "committed secrets" when it scans the working tree -- a false
positive against fixtures, not an actual leak. `repo_secret_scan.py`
excludes this file by name for exactly that reason (see
SECRET_SCAN_SELF_EXCLUDE); the fixtures below are deliberately unrealistic
(runs of a single repeated character) so nothing here is minable as a real
credential either way.

Run:
    python tests/test_repo_secret_scan.py -v
"""

from __future__ import annotations

import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(REPO_ROOT / "scripts"))

import repo_secret_scan as rss  # noqa: E402


class FindMissingDomainFoldersTests(unittest.TestCase):
    def test_all_present_returns_empty(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for folder in rss.DOMAIN_FOLDERS:
                (root / folder).mkdir()
            self.assertEqual(rss.find_missing_domain_folders(root), [])

    def test_reports_each_missing_folder(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "infra").mkdir()
            (root / "backend").mkdir()
            # web, mobile, .helix intentionally absent
            missing = rss.find_missing_domain_folders(root)
            self.assertEqual(set(missing), {"web", "mobile", ".helix"})

    def test_file_instead_of_directory_counts_as_missing(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            for folder in rss.DOMAIN_FOLDERS:
                (root / folder).mkdir()
            (root / "web").rmdir()
            (root / "web").write_text("not a directory")
            self.assertIn("web", rss.find_missing_domain_folders(root))


class FindSecretMatchesTests(unittest.TestCase):
    def test_clean_text_has_no_hits(self) -> None:
        text = "This is a normal README with no secrets in it.\n"
        self.assertEqual(rss.find_secret_matches("README.md", text), [])

    def test_azure_storage_account_key_detected(self) -> None:
        text = "AccountKey=" + ("A" * 40) + "==\n"
        hits = rss.find_secret_matches("infra/notes.txt", text)
        self.assertTrue(any("Azure Storage account key" in h for h in hits), hits)

    def test_connection_string_password_detected(self) -> None:
        text = "Server=tcp:x;Password=Sup3rSecretValue;\n"
        hits = rss.find_secret_matches("appsettings.json", text)
        self.assertTrue(any("connection-string password" in h for h in hits), hits)

    def test_sas_token_detected(self) -> None:
        text = "https://acct.blob.core.windows.net/c/b?sv=x&sig=" + ("a" * 30) + "\n"
        hits = rss.find_secret_matches("notes.md", text)
        self.assertTrue(any("Azure SAS token" in h for h in hits), hits)

    def test_github_token_detected(self) -> None:
        text = "token: ghp_" + ("a" * 36) + "\n"
        hits = rss.find_secret_matches("ci.yml", text)
        self.assertTrue(any("GitHub token" in h for h in hits), hits)

    def test_github_fine_grained_pat_detected(self) -> None:
        text = "github_pat_" + ("A" * 30) + "\n"
        hits = rss.find_secret_matches("ci.yml", text)
        self.assertTrue(any("GitHub fine-grained PAT" in h for h in hits), hits)

    def test_aws_access_key_detected(self) -> None:
        text = "AKIA" + ("B" * 16) + "\n"
        hits = rss.find_secret_matches("deploy.sh", text)
        self.assertTrue(any("AWS access key id" in h for h in hits), hits)

    def test_private_key_block_detected(self) -> None:
        text = "-----BEGIN RSA PRIVATE KEY-----\nMIIB...\n-----END RSA PRIVATE KEY-----\n"
        hits = rss.find_secret_matches("id_rsa", text)
        self.assertTrue(any("private key block" in h for h in hits), hits)

    def test_inline_api_key_assignment_detected(self) -> None:
        text = 'api_key: "' + ("x" * 24) + '"\n'
        hits = rss.find_secret_matches("config.yaml", text)
        self.assertTrue(
            any("inline api-key/secret/token assignment" in h for h in hits), hits
        )

    def test_reports_correct_line_number(self) -> None:
        text = "line1\nline2\nAKIA" + ("C" * 16) + "\nline4\n"
        hits = rss.find_secret_matches("f.txt", text)
        self.assertTrue(any(h.startswith("f.txt:3") for h in hits), hits)


class ScanTrackedFilesIntegrationTests(unittest.TestCase):
    """Exercises the real `git ls-files` wiring against a throwaway repo."""

    def _init_repo(self, root: Path) -> None:
        for args in (
            ["init", "-q"],
            ["config", "user.email", "test@example.com"],
            ["config", "user.name", "test"],
        ):
            subprocess.run(["git", *args], cwd=root, check=True, capture_output=True, text=True)

    def _commit_all(self, root: Path) -> None:
        subprocess.run(["git", "add", "."], cwd=root, check=True, capture_output=True, text=True)
        subprocess.run(
            ["git", "commit", "-q", "-m", "init"], cwd=root, check=True, capture_output=True, text=True
        )

    def test_clean_repo_has_no_hits(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._init_repo(root)
            (root / "README.md").write_text("nothing secret here\n")
            self._commit_all(root)
            hits, scanned = rss.scan_tracked_files_for_secrets(root)
            self.assertEqual(hits, [])
            self.assertEqual(scanned, 1)

    def test_planted_secret_is_flagged(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._init_repo(root)
            (root / "settings.ini").write_text("AccountKey=" + ("D" * 40) + "==\n")
            self._commit_all(root)
            hits, scanned = rss.scan_tracked_files_for_secrets(root)
            self.assertEqual(scanned, 1)
            self.assertTrue(any("settings.ini" in h for h in hits), hits)

    def test_untracked_secret_is_not_flagged(self) -> None:
        """A secret sitting on disk but never `git add`-ed must not fail the
        scan -- the check is 'no *committed* secrets', not 'no secrets
        anywhere on disk'."""
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._init_repo(root)
            (root / "README.md").write_text("clean\n")
            self._commit_all(root)
            (root / "leftover.env").write_text("AKIA" + ("E" * 16) + "\n")
            hits, scanned = rss.scan_tracked_files_for_secrets(root)
            self.assertEqual(hits, [])
            self.assertEqual(scanned, 1)

    def test_excluded_file_is_scanned_but_not_flagged(self) -> None:
        """A file on SECRET_SCAN_SELF_EXCLUDE still counts as tracked, it is
        just not scanned for content -- e.g. this repo's own scripts/tests
        that legitimately contain pattern-shaped text."""
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            self._init_repo(root)
            scripts_dir = root / "scripts"
            scripts_dir.mkdir()
            (scripts_dir / "repo_secret_scan.py").write_text(
                "AKIA" + ("F" * 16) + "\n"
            )
            self._commit_all(root)
            hits, scanned = rss.scan_tracked_files_for_secrets(root)
            self.assertEqual(hits, [])
            self.assertEqual(scanned, 0)


class CheckFunctionTests(unittest.TestCase):
    def test_check_domain_folders_reports_pass_and_fail(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            passed, _detail = rss.check_domain_folders(root)
            self.assertFalse(passed)
            for folder in rss.DOMAIN_FOLDERS:
                (root / folder).mkdir()
            passed, _detail = rss.check_domain_folders(root)
            self.assertTrue(passed)

    def test_check_no_committed_secrets_reports_pass_and_fail(self) -> None:
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            subprocess.run(["git", "init", "-q"], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(
                ["git", "config", "user.email", "test@example.com"],
                cwd=root, check=True, capture_output=True, text=True,
            )
            subprocess.run(
                ["git", "config", "user.name", "test"], cwd=root, check=True, capture_output=True, text=True
            )
            (root / "clean.txt").write_text("nothing to see\n")
            subprocess.run(["git", "add", "."], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(
                ["git", "commit", "-q", "-m", "init"], cwd=root, check=True, capture_output=True, text=True
            )
            passed, _detail = rss.check_no_committed_secrets(root)
            self.assertTrue(passed)

            (root / "dirty.txt").write_text("AKIA" + ("G" * 16) + "\n")
            subprocess.run(["git", "add", "."], cwd=root, check=True, capture_output=True, text=True)
            subprocess.run(
                ["git", "commit", "-q", "-m", "oops"], cwd=root, check=True, capture_output=True, text=True
            )
            passed, _detail = rss.check_no_committed_secrets(root)
            self.assertFalse(passed)


class MainEntryPointTests(unittest.TestCase):
    def test_main_passes_against_the_real_repo(self) -> None:
        """End-to-end: running the actual script against this real working
        tree exits 0 -- the same invocation the task's definition of done
        and the eventual CI status check both rely on."""
        proc = subprocess.run(
            [sys.executable, str(REPO_ROOT / "scripts" / "repo_secret_scan.py")],
            cwd=REPO_ROOT,
            text=True,
            capture_output=True,
            timeout=30,
        )
        self.assertEqual(proc.returncode, 0, msg=proc.stdout + proc.stderr)


if __name__ == "__main__":
    unittest.main()
