from __future__ import annotations

import errno
import importlib.util
import io
import json
import sys
import tempfile
import unittest
import zipfile
from contextlib import redirect_stdout
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace
from unittest import mock


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]


def load_script(name: str):
    script_path = REPOSITORY_ROOT / "scripts" / f"{name}.py"
    spec = importlib.util.spec_from_file_location(name, script_path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


sync = load_script("sync_agent_skills")
validator = load_script("validate_agent_skills")
release = load_script("build_agent_skill_release")
installed_verifier = load_script("verify_installed_agent_skill")


class AgentSkillInfrastructureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.catalog = json.loads(
            (REPOSITORY_ROOT / ".monica" / "agent-skill-catalog.json").read_text(
                encoding="utf-8"
            )
        )

    def test_catalog_manages_every_canonical_directory(self) -> None:
        directories = {
            path.name
            for path in (REPOSITORY_ROOT / "skills").iterdir()
            if path.is_dir()
        }
        self.assertEqual(directories, set(self.catalog["skills"]))
        self.assertTrue(all(name.startswith("monica-") for name in directories))

    def test_required_profile_lists_are_closed(self) -> None:
        for profile_name, profile in self.catalog["profiles"].items():
            declared = set(profile["skills"]["required"])
            closure = validator.required_closure(self.catalog["skills"], declared)
            self.assertEqual(
                declared,
                closure,
                f"{profile_name} omits {sorted(closure - declared)}",
            )

    def test_profile_closure_uses_explicit_buckets_and_required_edges_only(self) -> None:
        framework = self.catalog["profiles"]["framework-contributor"]
        framework_default = validator.profile_selection_closure(
            self.catalog["skills"], framework
        )
        self.assertNotIn("monica-docs-authoring", framework_default)
        framework_recommended = validator.profile_selection_closure(
            self.catalog["skills"], framework, include_recommended=True
        )
        self.assertIn("monica-docs-authoring", framework_recommended)
        self.assertNotIn(
            "monica-application",
            framework_recommended,
            "skill-level recommendations must not recursively widen a profile",
        )

        application = self.catalog["profiles"]["application"]
        application_default = validator.profile_selection_closure(
            self.catalog["skills"], application
        )
        self.assertNotIn("monica-application-microservice", application_default)
        self.assertNotIn("monica-application-modular-monolith", application_default)

        application_microservice = validator.profile_selection_closure(
            self.catalog["skills"], application, capabilities=["microservice"]
        )
        self.assertIn("monica-application-microservice", application_microservice)
        self.assertNotIn(
            "monica-application-modular-monolith", application_microservice
        )

        application_modular = validator.profile_selection_closure(
            self.catalog["skills"], application, capabilities=["modular-monolith"]
        )
        self.assertIn("monica-application-modular-monolith", application_modular)
        self.assertNotIn("monica-application-microservice", application_modular)

        application_ui = validator.profile_selection_closure(
            self.catalog["skills"], application, capabilities=["ui"]
        )
        self.assertIn("monica-ui-development", application_ui)
        self.assertIn("monica-development", application_ui)
        self.assertNotIn("monica-framework", application_ui)

    def test_bootstrap_prompt_manifest_covers_every_locale_host_and_goal(self) -> None:
        bootstrap = json.loads(
            (
                REPOSITORY_ROOT
                / "skills"
                / "monica-guide"
                / "assets"
                / "bootstrap-prompts.json"
            ).read_text(encoding="utf-8")
        )
        schema_path = (
            REPOSITORY_ROOT
            / "skills"
            / "monica-guide"
            / "assets"
            / "bootstrap-prompts.schema.json"
        )
        validation = validator.Validation()
        validator.validate_bootstrap_prompts(
            validation,
            self.catalog,
            bootstrap,
            schema_path,
        )
        self.assertEqual([], validation.errors)
        self.assertEqual(12, len(list(validator.bootstrap_prompt_entries(bootstrap))))

    def test_bootstrap_prompt_manifest_rejects_apply_in_init_command(self) -> None:
        bootstrap = json.loads(
            (
                REPOSITORY_ROOT
                / "skills"
                / "monica-guide"
                / "assets"
                / "bootstrap-prompts.json"
            ).read_text(encoding="utf-8")
        )
        prompt = bootstrap["locales"]["en-US"]["hosts"]["codex"]["goals"][
            "application"
        ]
        prompt["prompt"] += " Run `init --apply` immediately."
        validation = validator.Validation()
        validator.validate_bootstrap_prompts(
            validation,
            self.catalog,
            bootstrap,
            REPOSITORY_ROOT
            / "skills"
            / "monica-guide"
            / "assets"
            / "bootstrap-prompts.schema.json",
        )
        self.assertTrue(
            any("preview-only" in error for error in validation.errors),
            validation.errors,
        )

    def test_bootstrap_prompt_manifest_requires_user_skill_directory_disclosure(self) -> None:
        bootstrap = json.loads(
            (
                REPOSITORY_ROOT
                / "skills"
                / "monica-guide"
                / "assets"
                / "bootstrap-prompts.json"
            ).read_text(encoding="utf-8")
        )
        prompt = bootstrap["locales"]["en-US"]["hosts"]["codex"]["goals"][
            "application"
        ]
        prompt["prompt"] = prompt["prompt"].replace(
            "This global installation changes your user-level skill directory. ",
            "",
        )
        validation = validator.Validation()
        validator.validate_bootstrap_prompts(
            validation,
            self.catalog,
            bootstrap,
            REPOSITORY_ROOT
            / "skills"
            / "monica-guide"
            / "assets"
            / "bootstrap-prompts.schema.json",
        )
        self.assertTrue(
            any("user-directory" in error for error in validation.errors),
            validation.errors,
        )

    def test_source_resolver_distribution_is_exact_and_profile_scoped(self) -> None:
        distribution = self.catalog["externalSkills"]["inspect-dependency-source"][
            "distribution"
        ]
        self.assertEqual(
            "bffe59e69be1d3e217783d41a0b84100ac5c3997",
            distribution["commit"],
        )
        self.assertEqual(
            "https://github.com/Tairitsua/inspect-dependency-source-skill/tree/"
            + distribution["commit"],
            distribution["immutableSkillUrl"],
        )
        self.assertRegex(distribution["digest"], r"^sha256:[0-9a-f]{64}$")
        self.assertEqual(
            {"extension-author", "docs-contributor"},
            set(distribution["requiredByProfiles"]),
        )

    def test_router_skill_references_exactly_match_catalog_routes(self) -> None:
        for skill_name, entry in self.catalog["skills"].items():
            if entry["role"] != "router":
                continue
            skill_file = REPOSITORY_ROOT / entry["path"] / "SKILL.md"
            references = {
                match.group(1)
                for match in validator.SKILL_REFERENCE_PATTERN.finditer(
                    skill_file.read_text(encoding="utf-8")
                )
                if match.group(1) in self.catalog["skills"]
                and match.group(1) != skill_name
            }
            self.assertEqual(
                set(entry["routes"]),
                references,
                f"{skill_name} router documentation and catalog routes differ",
            )

    def test_retired_alias_scan_allows_only_explicit_diagnostic_locations(self) -> None:
        alias = next(iter(self.catalog["aliases"]))
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            source = root / "AGENTS.md"
            source.write_text(f"Use `{alias}`.\n", encoding="utf-8")
            self.assertEqual(
                [f"AGENTS.md:1:{alias}"],
                validator.find_retired_alias_occurrences(root, [alias]),
            )
            self.assertEqual(
                [],
                validator.find_retired_alias_occurrences(
                    root,
                    [alias],
                    allowed_files=(source,),
                ),
            )

    def test_required_dependency_graph_is_acyclic(self) -> None:
        self.assertEqual([], validator.find_required_cycles(self.catalog["skills"]))

    def test_release_and_validator_skill_tree_digests_match(self) -> None:
        self.assertEqual(
            validator.tree_digest(self.catalog),
            release.skill_tree_digest(release.skill_files(self.catalog)),
        )

    def test_file_manifest_digest_sorts_normalized_relative_paths(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            application = root / "skills" / "monica-application" / "SKILL.md"
            microservice = (
                root
                / "skills"
                / "monica-application-microservice"
                / "SKILL.md"
            )
            application.parent.mkdir(parents=True)
            microservice.parent.mkdir(parents=True)
            application.write_bytes(b"application\n")
            microservice.write_bytes(b"microservice\n")

            paths = [application, microservice]
            self.assertEqual(application, sorted(paths)[0])
            expected_manifest = "".join(
                (
                    f"{release.sha256_bytes(path.read_bytes()).removeprefix('sha256:')}  "
                    f"{path.relative_to(root).as_posix()}\n"
                )
                for path in (microservice, application)
            )
            expected_digest = release.sha256_bytes(expected_manifest.encode("utf-8"))
            self.assertEqual(
                expected_digest,
                release.file_manifest_digest(paths, relative_to=root),
            )
            self.assertEqual(
                expected_digest,
                release.file_manifest_digest(list(reversed(paths)), relative_to=root),
            )

    def test_release_parity_rejects_invalid_aggregate_tree_digest(self) -> None:
        archived_files = {
            "skills/monica-application/SKILL.md": b"application\n",
            "skills/monica-application-microservice/SKILL.md": b"microservice\n",
        }
        release_entry = {
            "skillDigests": {
                skill_name: release.digest_file_manifest(
                    [("SKILL.md", archived_files[f"skills/{skill_name}/SKILL.md"])]
                )
                for skill_name in (
                    "monica-application",
                    "monica-application-microservice",
                )
            },
            "skillTreeDigest": release.digest_file_manifest(archived_files.items()),
        }
        release.verify_archived_skill_digests(archived_files, release_entry)

        release_entry["skillTreeDigest"] = "sha256:" + "0" * 64
        with self.assertRaisesRegex(release.ReleaseError, "Aggregate skill-tree digest"):
            release.verify_archived_skill_digests(archived_files, release_entry)

    def test_projection_diff_reports_missing_unexpected_and_changed(self) -> None:
        expected = {"a": "1", "b": "2"}
        actual = {"b": "3", "c": "4"}
        self.assertEqual(
            ["missing a", "unexpected c", "changed b"],
            sync._describe_diff(expected, actual),
        )

    def test_projection_write_preserves_external_content_and_removes_exact_aliases(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            canonical = root / "canonical" / "monica-alpha"
            canonical.mkdir(parents=True)
            (canonical / "SKILL.md").write_text("canonical\n", encoding="utf-8")
            skill_roots = {"monica-alpha": canonical}
            expected = sync.build_manifest(skill_roots)

            projection = root / "projection"
            (projection / "monica-alpha").mkdir(parents=True)
            (projection / "monica-alpha" / "SKILL.md").write_text(
                "stale\n", encoding="utf-8"
            )
            external_cache = projection / "external-skill" / "__pycache__" / "cache.pyc"
            external_cache.parent.mkdir(parents=True)
            external_cache.write_bytes(b"external-cache")
            external_file = projection / "external-settings.json"
            external_file.write_text("external\n", encoding="utf-8")
            reserved_file = projection / "monica-notes.txt"
            reserved_file.write_text("not a skill directory\n", encoding="utf-8")
            (projection / "mo-alpha").mkdir()
            personal_skill = projection / "monica-personal" / "state.json"
            personal_skill.parent.mkdir()
            personal_skill.write_text("user-owned\n", encoding="utf-8")

            sync.write_projections(
                skill_roots,
                expected,
                {"mo-alpha"},
                projection_paths=(projection,),
            )

            self.assertEqual("canonical\n", (projection / "monica-alpha" / "SKILL.md").read_text())
            self.assertEqual(b"external-cache", external_cache.read_bytes())
            self.assertEqual("external\n", external_file.read_text())
            self.assertEqual("not a skill directory\n", reserved_file.read_text())
            self.assertEqual("user-owned\n", personal_skill.read_text())
            self.assertFalse((projection / "mo-alpha").exists())

    def test_projection_check_ignores_external_trees_but_diagnoses_owned_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            canonical = root / "canonical" / "monica-alpha"
            canonical.mkdir(parents=True)
            (canonical / "SKILL.md").write_text("canonical\n", encoding="utf-8")
            skill_roots = {"monica-alpha": canonical}
            expected = sync.build_manifest(skill_roots)
            projection = root / "projection"
            sync.write_projections(
                skill_roots,
                expected,
                {"mo-alpha"},
                projection_paths=(projection,),
            )

            external_cache = projection / "external-skill" / "__pycache__" / "cache.pyc"
            external_cache.parent.mkdir(parents=True)
            external_cache.write_bytes(b"ignored")
            (projection / "external-file").write_text("ignored\n", encoding="utf-8")
            self.assertTrue(
                sync.check_projections(
                    expected,
                    set(skill_roots),
                    {"mo-alpha"},
                    projection_paths=(projection,),
                )
            )

            (projection / "mo-alpha").mkdir()
            personal_cache = projection / "monica-personal" / "__pycache__" / "cache.pyc"
            personal_cache.parent.mkdir(parents=True)
            personal_cache.write_bytes(b"ignored")
            differences = sync._projection_differences(
                projection, expected, set(skill_roots), {"mo-alpha"}
            )
            self.assertIn("retired alias directory mo-alpha", differences)
            self.assertFalse(any("monica-personal" in item for item in differences))

            managed_cache = projection / "monica-alpha" / "__pycache__" / "cache.pyc"
            managed_cache.parent.mkdir()
            managed_cache.write_bytes(b"invalid")
            managed_differences = sync._projection_differences(
                projection, expected, set(skill_roots), {"mo-alpha"}
            )
            self.assertTrue(
                any("invalid managed skill directory monica-alpha" in item for item in managed_differences)
            )

    def test_projection_write_rolls_back_all_owned_directories_on_failure(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            skill_roots: dict[str, Path] = {}
            projection = root / "projection"
            projection.mkdir()
            for name in ("monica-alpha", "monica-beta"):
                canonical = root / "canonical" / name
                canonical.mkdir(parents=True)
                (canonical / "SKILL.md").write_text(f"new-{name}\n", encoding="utf-8")
                skill_roots[name] = canonical
                projected = projection / name
                projected.mkdir()
                (projected / "SKILL.md").write_text(f"old-{name}\n", encoding="utf-8")
            (projection / "mo-alpha").mkdir()
            external = projection / "external-skill" / "state.json"
            external.parent.mkdir()
            external.write_text("preserved\n", encoding="utf-8")

            real_replace = sync.os.replace

            def fail_second_install(source, destination):
                source_path = Path(source)
                if source_path.parent.name == "staging" and source_path.name == "monica-beta":
                    raise OSError("simulated replacement failure")
                return real_replace(source, destination)

            with mock.patch.object(sync.os, "replace", side_effect=fail_second_install):
                with self.assertRaises(sync.ProjectionError):
                    sync._replace_projection(projection, skill_roots, {"mo-alpha"})

            for name in skill_roots:
                self.assertEqual(
                    f"old-{name}\n",
                    (projection / name / "SKILL.md").read_text(encoding="utf-8"),
                )
            self.assertTrue((projection / "mo-alpha").is_dir())
            self.assertEqual("preserved\n", external.read_text(encoding="utf-8"))

    def test_projection_write_retries_transient_windows_sharing_failure(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            canonical = root / "canonical" / "monica-alpha"
            canonical.mkdir(parents=True)
            (canonical / "SKILL.md").write_text("new\n", encoding="utf-8")
            projection = root / "projection"
            (projection / "monica-alpha").mkdir(parents=True)
            (projection / "monica-alpha" / "SKILL.md").write_text("old\n", encoding="utf-8")
            real_replace = sync.os.replace
            attempts = 0

            def fail_once(source, destination):
                nonlocal attempts
                source_path = Path(source)
                if source_path.parent.name == "staging" and attempts == 0:
                    attempts += 1
                    raise PermissionError(errno.EACCES, "simulated sharing violation")
                return real_replace(source, destination)

            with mock.patch.object(sync.os, "replace", side_effect=fail_once):
                with mock.patch.object(sync.time, "sleep"):
                    sync._replace_projection(projection, {"monica-alpha": canonical}, set())

            self.assertEqual(1, attempts)
            self.assertEqual("new\n", (projection / "monica-alpha" / "SKILL.md").read_text(encoding="utf-8"))

    def test_release_index_materialization_is_immutable(self) -> None:
        base = {
            "$schema": "./schemas/agent-skill-index.schema.json",
            "schemaVersion": 2,
            "channels": {"stable": None, "preview": None},
            "versions": {},
            "releases": {},
        }
        release_index = release.materialized_index(
            base,
            version="1.2.3-rc.1",
            tag="v1.2.3-rc.1",
            commit="a" * 40,
            channel="preview",
            catalog_digest="sha256:" + "b" * 64,
            tree_digest="sha256:" + "c" * 64,
            skill_digests={"monica-guide": "sha256:" + "d" * 64},
            previous_tag=None,
            manifest_digest="sha256:" + "e" * 64,
            published_at="2026-08-05T00:00:00Z",
        )
        self.assertEqual("v1.2.3-rc.1", release_index["channels"]["preview"])
        self.assertIsNone(release_index["channels"]["stable"])
        self.assertEqual("a" * 40, release_index["releases"]["v1.2.3-rc.1"]["commit"])
        self.assertEqual(
            {"monica-guide": 1},
            release_index["releases"]["v1.2.3-rc.1"]["skillRevisions"],
        )
        self.assertEqual(
            {"monica-guide": "v1.2.3-rc.1"},
            release_index["releases"]["v1.2.3-rc.1"]["skillLastChangedIn"],
        )
        self.assertEqual(
            "https://github.com/Tairitsua/Monica/releases/download/v1.2.3-rc.1",
            release_index["releases"]["v1.2.3-rc.1"]["assetBaseUrl"],
        )
        self.assertEqual({}, base["releases"], "the checked-in base index must remain unchanged")

        merged = release.merge_verified_history(
            base,
            release_index,
            expected_previous_tag="v1.2.3-rc.1",
        )
        self.assertEqual(release_index, merged)

        rewritten = json.loads(json.dumps(release_index))
        rewritten["releases"]["v1.2.3-rc.1"]["commit"] = "e" * 40
        checked_with_history = json.loads(json.dumps(release_index))
        with self.assertRaises(release.ReleaseError):
            release.merge_verified_history(
                checked_with_history,
                rewritten,
                expected_previous_tag="v1.2.3-rc.1",
            )

        with self.assertRaises(release.ReleaseError):
            release.materialized_index(
                release_index,
                version="1.2.3-rc.1",
                tag="v1.2.3-rc.1",
                commit="d" * 40,
                channel="preview",
                catalog_digest="sha256:" + "b" * 64,
                tree_digest="sha256:" + "c" * 64,
                skill_digests={"monica-guide": "sha256:" + "d" * 64},
                previous_tag="v1.2.3-rc.1",
                manifest_digest="sha256:" + "e" * 64,
                published_at="2026-08-05T00:00:00Z",
            )

    def test_skill_revisions_follow_one_global_sequential_release_lineage(self) -> None:
        base = {
            "$schema": "./schemas/agent-skill-index.schema.json",
            "schemaVersion": 2,
            "channels": {"stable": None, "preview": None},
            "versions": {},
            "releases": {},
        }

        def materialize(
            index: dict,
            *,
            version: str,
            digests: dict[str, str],
            previous_tag: str | None,
            published_at: str,
        ) -> dict:
            return release.materialized_index(
                index,
                version=version,
                tag=f"v{version}",
                commit=version[-1] * 40,
                channel=release.release_channel_for_version(version),
                catalog_digest="sha256:" + "a" * 64,
                tree_digest="sha256:" + "b" * 64,
                skill_digests=digests,
                previous_tag=previous_tag,
                manifest_digest="sha256:" + "c" * 64,
                published_at=published_at,
            )

        first_tag = "v2.0.0-rc.1"
        first = materialize(
            base,
            version="2.0.0-rc.1",
            digests={"monica-guide": "sha256:" + "1" * 64},
            previous_tag=None,
            published_at="2026-08-05T00:00:00Z",
        )
        second_tag = "v2.0.0"
        second = materialize(
            first,
            version="2.0.0",
            digests={
                "monica-guide": "sha256:" + "1" * 64,
                "monica-new": "sha256:" + "2" * 64,
            },
            previous_tag=first_tag,
            published_at="2026-08-05T00:01:00Z",
        )
        third_tag = "v2.1.0-rc.1"
        third = materialize(
            second,
            version="2.1.0-rc.1",
            digests={
                "monica-guide": "sha256:" + "3" * 64,
                "monica-new": "sha256:" + "2" * 64,
            },
            previous_tag=second_tag,
            published_at="2026-08-05T00:02:00Z",
        )

        self.assertEqual(
            {"monica-guide": 1, "monica-new": 1},
            second["releases"][second_tag]["skillRevisions"],
        )
        self.assertEqual(
            {"monica-guide": first_tag, "monica-new": second_tag},
            second["releases"][second_tag]["skillLastChangedIn"],
        )
        self.assertEqual(
            {"monica-guide": 2, "monica-new": 1},
            third["releases"][third_tag]["skillRevisions"],
        )
        self.assertEqual(
            {"monica-guide": third_tag, "monica-new": second_tag},
            third["releases"][third_tag]["skillLastChangedIn"],
        )
        release.validate_index_payload(third, label="sequential fixture")

        skipped_revision = json.loads(json.dumps(third))
        skipped_revision["releases"][third_tag]["skillRevisions"]["monica-guide"] = 3
        with self.assertRaises(release.ReleaseError):
            release.validate_index_payload(skipped_revision, label="skipped revision fixture")
        with self.assertRaises(installed_verifier.ReleaseContractError):
            installed_verifier.validate_revision_history(
                skipped_revision,
                label="installed verifier fixture",
            )

        mismatched_keys = json.loads(json.dumps(third))
        del mismatched_keys["releases"][third_tag]["skillRevisions"]["monica-new"]
        with self.assertRaises(release.ReleaseError):
            release.validate_index_payload(mismatched_keys, label="key mismatch fixture")

        with self.assertRaises(release.ReleaseError):
            materialize(
                third,
                version="2.1.0-rc.2",
                digests={"monica-guide": "sha256:" + "3" * 64},
                previous_tag=second_tag,
                published_at="2026-08-05T00:03:00Z",
            )
        with self.assertRaises(release.ReleaseError):
            materialize(
                third,
                version="2.1.0-rc.2",
                digests={"monica-guide": "sha256:" + "3" * 64},
                previous_tag=third_tag,
                published_at="2026-08-05T00:02:00Z",
            )
        with self.assertRaises(release.ReleaseError):
            release.merge_verified_history(
                base,
                third,
                expected_previous_tag=second_tag,
            )

    def test_installed_verifier_reports_malformed_manifest_revision_maps(self) -> None:
        tag = "v1.0.0"
        digest = "sha256:" + "d" * 64
        release_entry = {
            "publishedAt": "2026-08-05T00:00:00Z",
            "skillDigestAlgorithm": release.SKILL_DIGEST_ALGORITHM,
            "skillDigests": {"monica-guide": digest},
            "skillRevisions": {"monica-guide": 1},
            "skillLastChangedIn": {"monica-guide": tag},
        }
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            index_path = root / "index.json"
            manifest_path = root / "manifest.json"
            catalog_path = root / "catalog.json"
            index_path.write_text(
                json.dumps({"schemaVersion": 2, "releases": {tag: release_entry}}),
                encoding="utf-8",
            )
            manifest_path.write_text(
                json.dumps(
                    {
                        "schemaVersion": 2,
                        "fileManifestScope": "release-payload-except-index-v1",
                        "skillDigests": {"monica-guide": digest},
                        "skillRevisions": [],
                        "skillLastChangedIn": {"monica-guide": tag},
                    }
                ),
                encoding="utf-8",
            )
            catalog_path.write_text("{}", encoding="utf-8")
            args = SimpleNamespace(
                index=index_path,
                manifest=manifest_path,
                catalog=catalog_path,
                tag=tag,
                skill="monica-guide",
                path=root,
                json=True,
            )

            with self.assertRaises(installed_verifier.ReleaseContractError):
                installed_verifier.verify(args)
            output = io.StringIO()
            with mock.patch.object(installed_verifier, "parse_args", return_value=args):
                with redirect_stdout(output):
                    self.assertEqual(1, installed_verifier.main())
            result = json.loads(output.getvalue())
            self.assertFalse(result["ok"])
            self.assertIn("invalid skill revision metadata", result["error"])

    def test_manifest_digest_is_path_sensitive_and_deterministic(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            first = root / "first"
            second = root / "second"
            first.write_text("same", encoding="utf-8")
            second.write_text("same", encoding="utf-8")
            manifest = sync.build_manifest({"example": root})
            self.assertEqual(manifest, sync.build_manifest({"example": root}))
            self.assertNotEqual(manifest["example/first"], "")
            self.assertEqual(manifest["example/first"], manifest["example/second"])

    def test_release_catalog_and_index_bytes_match_archive_and_digests(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            staging = Path(temporary_directory)
            empty_index = staging / "empty-index.json"
            empty_index.write_text(
                json.dumps(
                    {
                        "$schema": "./schemas/agent-skill-index.schema.json",
                        "schemaVersion": 2,
                        "channels": {"stable": None, "preview": None},
                        "versions": {},
                        "releases": {},
                    }
                ),
                encoding="utf-8",
            )
            args = SimpleNamespace(
                version="9.9.9-rc.1",
                tag="v9.9.9-rc.1",
                commit="a" * 40,
                channel="preview",
                published_at="2026-08-05T00:00:00Z",
                previous_index=None,
                previous_tag=None,
            )
            with mock.patch.object(release, "INDEX_PATH", empty_index):
                release.build_payload(
                    staging,
                    args,
                    datetime(2026, 8, 5, tzinfo=timezone.utc),
                )
            index = json.loads((staging / "agent-skill-index.json").read_text(encoding="utf-8"))
            manifest = json.loads((staging / "agent-skill-manifest.json").read_text(encoding="utf-8"))
            self.assertEqual(2, index["schemaVersion"])
            self.assertEqual(2, manifest["schemaVersion"])
            catalog_bytes = (staging / "agent-skill-catalog.json").read_bytes()
            self.assertEqual(release.sha256_bytes(catalog_bytes), manifest["catalogDigest"])
            self.assertEqual(
                manifest["catalogDigest"],
                index["releases"]["v9.9.9-rc.1"]["catalogDigest"],
            )
            manifest_bytes = (staging / "agent-skill-manifest.json").read_bytes()
            self.assertEqual(
                release.sha256_bytes(manifest_bytes),
                index["releases"]["v9.9.9-rc.1"]["manifestDigest"],
            )
            self.assertEqual(
                manifest["skillDigests"],
                index["releases"]["v9.9.9-rc.1"]["skillDigests"],
            )
            self.assertEqual(
                manifest["skillRevisions"],
                index["releases"]["v9.9.9-rc.1"]["skillRevisions"],
            )
            self.assertEqual(
                manifest["skillLastChangedIn"],
                index["releases"]["v9.9.9-rc.1"]["skillLastChangedIn"],
            )
            self.assertEqual(
                set(manifest["skillDigests"]),
                set(manifest["skillRevisions"]),
            )
            self.assertEqual(
                set(manifest["skillDigests"]),
                set(manifest["skillLastChangedIn"]),
            )
            self.assertEqual(
                release.per_skill_digests(self.catalog),
                manifest["skillDigests"],
            )
            with zipfile.ZipFile(staging / "monica-agent-skills-v9.9.9-rc.1.zip") as archive:
                archive_paths = [
                    name for name in archive.namelist() if not name.endswith("/")
                ]
                self.assertEqual(
                    sorted(archive_paths, key=release.utf8_path_key),
                    archive_paths,
                )
                self.assertLess(
                    archive_paths.index(
                        "skills/monica-application-microservice/SKILL.md"
                    ),
                    archive_paths.index("skills/monica-application/SKILL.md"),
                )
                self.assertEqual(catalog_bytes, archive.read(".monica/agent-skill-catalog.json"))
                self.assertEqual(
                    (staging / "agent-skill-index.json").read_bytes(),
                    archive.read(".monica/agent-skill-index.json"),
                )
                managed_tree = [
                    (path, archive.read(path))
                    for path in manifest["files"]
                    if path.startswith("skills/")
                ]
                self.assertEqual(
                    release.digest_file_manifest(managed_tree),
                    manifest["skillTreeDigest"],
                )
            verification = installed_verifier.verify(
                SimpleNamespace(
                    index=staging / "agent-skill-index.json",
                    manifest=staging / "agent-skill-manifest.json",
                    catalog=staging / "agent-skill-catalog.json",
                    tag="v9.9.9-rc.1",
                    skill="monica-guide",
                    path=REPOSITORY_ROOT / "skills" / "monica-guide",
                )
            )
            self.assertTrue(verification["ok"])
            self.assertEqual(
                manifest["skillDigests"]["monica-guide"],
                verification["skillDigest"],
            )
            self.assertEqual(1, verification["skillRevision"])
            self.assertEqual("v9.9.9-rc.1", verification["skillLastChangedIn"])
            application_verification = installed_verifier.verify(
                SimpleNamespace(
                    index=staging / "agent-skill-index.json",
                    manifest=staging / "agent-skill-manifest.json",
                    catalog=staging / "agent-skill-catalog.json",
                    tag="v9.9.9-rc.1",
                    skill="monica-application",
                    path=REPOSITORY_ROOT / "skills" / "monica-application",
                )
            )
            self.assertTrue(application_verification["ok"])
            self.assertEqual(
                manifest["skillDigests"]["monica-application"],
                application_verification["skillDigest"],
            )

    def test_release_inputs_support_full_semver_and_prerelease_channels(self) -> None:
        common = {
            "commit": "a" * 40,
            "published_at": "2026-08-05T00:00:00Z",
            "previous_index": None,
            "previous_tag": None,
            "output": REPOSITORY_ROOT / ".tmp" / "semver-contract-test",
        }
        stable = SimpleNamespace(
            **common,
            version="1.2.3+build-7",
            tag="v1.2.3+build-7",
            channel="stable",
        )
        release.validate_inputs(stable)
        stable_index = release.materialized_index(
            {
                "$schema": "./schemas/agent-skill-index.schema.json",
                "schemaVersion": 2,
                "channels": {"stable": None, "preview": None},
                "versions": {},
                "releases": {},
            },
            version=stable.version,
            tag=stable.tag,
            commit=stable.commit,
            channel=stable.channel,
            catalog_digest="sha256:" + "b" * 64,
            tree_digest="sha256:" + "c" * 64,
            skill_digests={"monica-guide": "sha256:" + "d" * 64},
            previous_tag=None,
            manifest_digest="sha256:" + "e" * 64,
            published_at=stable.published_at,
        )
        release.validate_index_payload(stable_index, label="build-metadata fixture")
        preview = SimpleNamespace(
            **common,
            version="1.2.3-rc.1+build-7",
            tag="v1.2.3-rc.1+build-7",
            channel="preview",
        )
        release.validate_inputs(preview)
        wrong_channel = SimpleNamespace(
            **common,
            version="1.2.3+build-7",
            tag="v1.2.3+build-7",
            channel="preview",
        )
        with self.assertRaises(release.ReleaseError):
            release.validate_inputs(wrong_channel)
        with self.assertRaises(release.ReleaseError):
            release.release_channel_for_version("01.2.3")


if __name__ == "__main__":
    unittest.main()
