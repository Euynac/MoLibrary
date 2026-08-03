from __future__ import annotations

import copy
import importlib.util
import json
import sys
import tempfile
import unittest
import zipfile
from collections.abc import Callable
from pathlib import Path


SKILL_ROOT = Path(__file__).resolve().parents[1]


def load_script(name: str):
    path = SKILL_ROOT / "scripts" / f"{name}.py"
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


scaffold = load_script("scaffold_package")
validator = load_script("validate_package")
inspector = load_script("inspect_package")


def valid_manifest() -> dict:
    return {
        "schemaVersion": 1,
        "packageId": "Acme.Monica.Example",
        "version": "0.1.0-alpha.1",
        "description": "Example capabilities for Monica.",
        "authors": "Acme Engineering",
        "nugetOwner": "Acme",
        "repositoryUrl": "https://github.com/acme/acme-monica-example",
        "projectUrl": "https://github.com/acme/acme-monica-example",
        "supportUrl": "https://github.com/acme/acme-monica-example/issues",
        "securityUrl": "https://github.com/acme/acme-monica-example/security/policy",
        "source": {
            "available": True,
            "provider": "github",
            "sourceLinkVersion": "10.0.102",
        },
        "distribution": "public",
        "publishing": {"target": "nuget.org"},
        "targetFramework": "net10.0",
        "monicaVersion": "1.0.0-rc.6",
        "capabilityTags": ["example"],
        "license": {"openSource": True, "expression": "MIT"},
        "branding": {
            "icon": {"kind": "compatibility-mark"},
            "showOpenSourceBadge": True,
        },
        "modules": [
            {
                "name": "Example",
                "kind": "infrastructure",
                "key": "Acme.Monica.Example",
            },
            {
                "name": "ExampleUI",
                "kind": "ui",
                "key": "Acme.Monica.Example.UI",
                "dependsOn": ["Example"],
            },
        ],
    }


class ScaffoldTests(unittest.TestCase):
    def write_manifest(self, root: Path, payload: dict) -> Path:
        path = root / "manifest.json"
        path.write_text(json.dumps(payload), encoding="utf-8")
        return path

    def test_scaffold_emits_explicit_mixed_package_contract(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest = scaffold.load_manifest(self.write_manifest(root, valid_manifest()))
            output = root / "output"

            scaffold.create_repository(manifest, output)

            readme = (output / "README.md").read_text(encoding="utf-8")
            project = (output / "src/Acme.Monica.Example/Acme.Monica.Example.csproj").read_text(encoding="utf-8")
            ui_module = (output / "src/Acme.Monica.Example/Modules/ModuleExampleUI.cs").read_text(encoding="utf-8")
            tests = (output / "tests/Test.Acme.Monica.Example/Modules/ModuleRegistrationTests.cs").read_text(encoding="utf-8")

            self.assertIn("## Install", readme)
            self.assertNotIn("assets/monica", readme)
            self.assertIn("![Package icon](monica-compatibility-mark.png)", readme)
            self.assertIn(scaffold.COMPATIBILITY_NOTICE, readme)
            self.assertTrue((output / "monica-open-source-badge.svg").is_file())
            self.assertEqual(
                (SKILL_ROOT / "assets/monica-compatibility-mark.png").read_bytes(),
                (output / "monica-compatibility-mark.png").read_bytes(),
            )
            self.assertEqual(
                (SKILL_ROOT / "assets/monica-compatibility-mark.svg").read_bytes(),
                (output / "monica-compatibility-mark.svg").read_bytes(),
            )
            self.assertIn("monica-ui;example", project)
            self.assertIn("<RepositoryUrl>", project)
            self.assertIn("<RepositoryType>git</RepositoryType>", project)
            self.assertIn("<PublishRepositoryUrl>true</PublishRepositoryUrl>", project)
            self.assertIn('PackagePath=""', project)
            self.assertNotIn('PackagePath="\\"', project)
            self.assertIn("namespace Acme.Monica.Example.Modules;", ui_module)
            self.assertIn("using Monica.Modules;", ui_module)
            self.assertIn('"/example"', ui_module)
            self.assertEqual(1, ui_module.count("RegisterUIComponents"))
            self.assertIn("RegisterLocalizedCategory<ExampleResource>", ui_module)
            self.assertIn('"Acme.Monica.Example"', ui_module)
            self.assertIn('"Navigation:Category"', ui_module)
            self.assertIn("order: 450", ui_module)
            self.assertIn("RegisterLocalizedPage<UIExamplePage, ExampleResource>", ui_module)
            self.assertIn("categoryId: category", ui_module)
            self.assertIn("navOrder: 80", ui_module)
            self.assertNotIn("RegisterLocalizedComponent", ui_module)
            self.assertIn("builder.AddExampleUI();", tests)
            self.assertNotIn("builder.AddExample();", tests)
            self.assertIn("scope.Resolve<IPageCatalog>()", tests)
            self.assertNotIn("IPageRegistry", tests)
            self.assertIn('NavigationCategoryId.Create("Acme.Monica.Example")', tests)
            self.assertIn('DisplayName.Key.Should().Be("Navigation:Category")', tests)
            self.assertIn('DisplayName.Key.Should().Be("Navigation:Title")', tests)
            self.assertIn("category0.Order.Should().Be(450)", tests)
            self.assertIn("navigationItem0.Order.Should().Be(80)", tests)
            self.assertNotIn("page0.CategoryId", tests)
            self.assertNotIn("navigationItem0.Disabled", tests)
            self.assertNotIn("GetComponentType", tests)
            self.assertTrue((output / "tests/README.md").is_file())
            self.assertTrue((output / "package.manifest.json").is_file())

    def test_scaffold_gives_each_ui_module_its_own_category_contract(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            payload = valid_manifest()
            payload["packageId"] = "Acme.Monica.Toolkit"
            payload["modules"] = [
                {
                    "name": "Toolkit",
                    "kind": "infrastructure",
                    "key": "Acme.Monica.Toolkit",
                },
                {
                    "name": "AuditUI",
                    "kind": "ui",
                    "key": "Acme.Monica.Toolkit.Audit.UI",
                    "dependsOn": ["Toolkit"],
                },
                {
                    "name": "ReportsUI",
                    "kind": "ui",
                    "key": "Acme.Monica.Toolkit.Reports.UI",
                    "dependsOn": ["Toolkit"],
                },
            ]
            manifest = scaffold.load_manifest(self.write_manifest(root, payload))
            output = root / "output"

            scaffold.create_repository(manifest, output)

            audit_module = (output / "src/Acme.Monica.Toolkit/Modules/ModuleAuditUI.cs").read_text(
                encoding="utf-8"
            )
            reports_module = (output / "src/Acme.Monica.Toolkit/Modules/ModuleReportsUI.cs").read_text(
                encoding="utf-8"
            )
            tests = (
                output / "tests/Test.Acme.Monica.Toolkit/Modules/ModuleRegistrationTests.cs"
            ).read_text(encoding="utf-8")
            findings = validator.validate_project(
                output,
                output / "src/Acme.Monica.Toolkit/Acme.Monica.Toolkit.csproj",
                None,
            )

            self.assertIn('"Acme.Monica.Toolkit.Audit"', audit_module)
            self.assertIn("order: 450", audit_module)
            self.assertIn("navOrder: 80", audit_module)
            self.assertIn('"Acme.Monica.Toolkit.Reports"', reports_module)
            self.assertIn("order: 451", reports_module)
            self.assertIn("navOrder: 81", reports_module)
            self.assertEqual(1, audit_module.count("RegisterUIComponents"))
            self.assertEqual(1, reports_module.count("RegisterUIComponents"))
            self.assertIn('NavigationCategoryId.Create("Acme.Monica.Toolkit.Audit")', tests)
            self.assertIn('NavigationCategoryId.Create("Acme.Monica.Toolkit.Reports")', tests)
            self.assertIn("category1.Order.Should().Be(451)", tests)
            self.assertIn("navigationItem1.Order.Should().Be(81)", tests)
            self.assertEqual(["OK"], [finding.code for finding in findings])

    def test_private_feed_does_not_emit_nuget_workflow_or_oss_branding(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            payload = valid_manifest()
            payload["source"] = {"available": False, "provider": "github"}
            payload["distribution"] = "private"
            payload["publishing"] = {
                "target": "private-feed",
                "feedUrl": "https://packages.example.test/nuget/v3/index.json",
            }
            payload["license"] = {"openSource": False, "expression": "MIT"}
            payload["branding"]["showOpenSourceBadge"] = False
            manifest = scaffold.load_manifest(self.write_manifest(root, payload))
            output = root / "output"

            scaffold.create_repository(manifest, output)

            self.assertTrue((output / ".github/workflows/ci.yml").is_file())
            self.assertFalse((output / ".github/workflows/publish.yml").exists())
            self.assertFalse((output / "monica-open-source-badge.svg").exists())
            project = (output / "src/Acme.Monica.Example/Acme.Monica.Example.csproj").read_text(encoding="utf-8")
            self.assertNotIn("Microsoft.SourceLink", project)
            self.assertNotIn("RepositoryUrl", project)
            self.assertNotIn("RepositoryType", project)
            self.assertNotIn("PublishRepositoryUrl", project)
            self.assertNotIn("- [Repository]", (output / "README.md").read_text(encoding="utf-8"))
            findings = validator.validate_project(
                output,
                output / "src/Acme.Monica.Example/Acme.Monica.Example.csproj",
                None,
            )
            self.assertEqual(["OK"], [finding.code for finding in findings])

    def test_provider_none_omits_repository_and_github_automation(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            payload = valid_manifest()
            payload.pop("repositoryUrl")
            payload["source"] = {"available": False, "provider": "none"}
            payload["distribution"] = "private"
            payload["publishing"] = {"target": "none"}
            payload["license"] = {"openSource": False, "expression": "MIT"}
            payload["branding"]["showOpenSourceBadge"] = False
            manifest = scaffold.load_manifest(self.write_manifest(root, payload))
            output = root / "output"

            scaffold.create_repository(manifest, output)

            project = (output / "src/Acme.Monica.Example/Acme.Monica.Example.csproj").read_text(encoding="utf-8")
            contract = json.loads((output / "package.manifest.json").read_text(encoding="utf-8"))
            self.assertFalse((output / ".github").exists())
            self.assertNotIn("repositoryUrl", contract)
            self.assertNotIn("RepositoryUrl", project)
            self.assertNotIn("Microsoft.SourceLink", project)

    def test_manifest_rejects_invalid_graph_identity_version_and_branding(self) -> None:
        cases: list[tuple[str, Callable[[dict], object]]] = [
            ("ui suffix", lambda value: value["modules"][1].update(name="ExampleDashboard")),
            ("non-ui suffix", lambda value: value["modules"][0].update(name="ExampleUI")),
            ("duplicate dependency", lambda value: value["modules"][1].update(dependsOn=["Example", "Example"])),
            ("dependency cycle", lambda value: value["modules"][0].update(dependsOn=["ExampleUI"])),
            ("invalid version", lambda value: value.update(version="01.0.0")),
            ("typed license", lambda value: value.update(license={"openSource": True, "expression": 123})),
            ("implicit badge", lambda value: value.update(license={"openSource": False, "expression": "MIT"})),
            ("missing Source Link version", lambda value: value["source"].pop("sourceLinkVersion")),
            (
                "configured provider without repository URL",
                lambda value: value.pop("repositoryUrl"),
            ),
            (
                "provider none with repository URL",
                lambda value: value.update(source={"available": False, "provider": "none"}),
            ),
            (
                "source available without provider",
                lambda value: value.update(source={"available": True, "provider": "none", "sourceLinkVersion": "10.0.102"}),
            ),
        ]
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for name, mutate in cases:
                with self.subTest(name=name):
                    payload = copy.deepcopy(valid_manifest())
                    mutate(payload)
                    with self.assertRaises(ValueError):
                        scaffold.load_manifest(self.write_manifest(root, payload))

    def test_constant_ui_route_is_resolved_for_validation(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            project_root = Path(temporary) / "src/Acme.Monica.GachaPool"
            module_source = project_root / "Modules/ModuleGachaPoolUI.cs"
            page_source = project_root / "Pages/UIGachaPoolPage.razor"
            module_source.parent.mkdir(parents=True)
            page_source.parent.mkdir(parents=True)
            project = project_root / "Acme.Monica.GachaPool.csproj"
            project.write_text("<Project />", encoding="utf-8")
            module_source.write_text(
                "registry.RegisterLocalizedPage<UIGachaPoolPage, GachaPoolResource>(\n"
                "    UIGachaPoolPage.PAGE_URL,\n"
                "    \"Navigation:Title\");\n",
                encoding="utf-8",
            )
            page_source.write_text(
                'public const string PAGE_URL = "/gacha-pool";\n',
                encoding="utf-8",
            )

            route, error = validator.resolve_registered_route(project, module_source)

            self.assertEqual("/gacha-pool", route)
            self.assertIsNone(error)

    def test_navigation_contract_parser_handles_named_arguments_and_additional_hidden_page(self) -> None:
        module_text = """
// RegisterLocalizedComponent<LegacyPage, LegacyResource>("/ignored", "Ignored");
var legacyDocumentation = "RegisterLocalizedComponent<LegacyPage, LegacyResource>";
shellGuide.RegisterUIComponents
(
    registry =>
    {
        var category =
            registry
                .RegisterLocalizedCategory<AuditResource>
                (
                    categoryId: "Acme.Monica.Toolkit.Audit",
                    displayNameKey: "Navigation:Category",
                    order: 4_00
                );
        registry.RegisterLocalizedPage<UIAuditPage, AuditResource>
        (
            route: "/toolkit-audit",
            displayNameKey: "Navigation:Title",
            icon: null,
            categoryId: category,
            addToNav: true,
            navOrder: 80
        );
        registry.RegisterLocalizedPage<UIAuditDetailsPage, AuditResource>(
            route: "/toolkit-audit-details",
            displayNameKey: "Navigation:Details",
            categoryId: category);
    }
);
"""

        errors = validator.localized_navigation_contract_errors(
            module_text,
            "Acme.Monica.Toolkit.Audit",
            "UIAuditPage",
        )

        self.assertEqual([], errors)
        self.assertEqual(
            [],
            validator.find_generic_invocations(module_text, "RegisterLocalizedComponent"),
        )

    def test_camel_case_package_segment_uses_readable_package_family_route(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            payload = valid_manifest()
            payload["packageId"] = "Tairitsua.Monica.GachaPool"
            payload["repositoryUrl"] = "https://github.com/Tairitsua/MoLibrary.GachaPool"
            payload["modules"] = [
                {
                    "name": "GachaPool",
                    "kind": "infrastructure",
                    "key": "Tairitsua.Monica.GachaPool",
                },
                {
                    "name": "GachaPoolUI",
                    "kind": "ui",
                    "key": "Tairitsua.Monica.GachaPool.UI",
                    "dependsOn": ["GachaPool"],
                },
            ]
            manifest = scaffold.load_manifest(self.write_manifest(root, payload))
            output = root / "output"
            scaffold.create_repository(manifest, output)
            project = output / "src/Tairitsua.Monica.GachaPool/Tairitsua.Monica.GachaPool.csproj"
            module = output / "src/Tairitsua.Monica.GachaPool/Modules/ModuleGachaPoolUI.cs"

            self.assertIn('"/gacha-pool"', module.read_text(encoding="utf-8"))
            findings = validator.validate_project(
                output,
                project,
                None,
            )
            self.assertEqual(["OK"], [finding.code for finding in findings])

    def test_separately_shipped_ui_variant_does_not_duplicate_route_segments(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            payload = valid_manifest()
            payload["packageId"] = "Acme.Monica.Analytics.UI"
            payload["modules"] = [
                {
                    "name": "AnalyticsUI",
                    "kind": "ui",
                    "key": "Acme.Monica.Analytics.UI",
                },
            ]
            manifest = scaffold.load_manifest(self.write_manifest(root, payload))
            output = root / "output"

            scaffold.create_repository(manifest, output)

            project = output / "src/Acme.Monica.Analytics.UI/Acme.Monica.Analytics.UI.csproj"
            module = output / "src/Acme.Monica.Analytics.UI/Modules/ModuleAnalyticsUI.cs"
            self.assertIn('"/analytics"', module.read_text(encoding="utf-8"))
            findings = validator.validate_project(
                output,
                project,
                None,
            )
            self.assertEqual(["OK"], [finding.code for finding in findings])

    def test_validator_expands_property_based_conditional_package_versions(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest = scaffold.load_manifest(self.write_manifest(root, valid_manifest()))
            output = root / "output"
            scaffold.create_repository(manifest, output)
            build_props = output / "Directory.Build.props"
            build_props.write_text(
                build_props.read_text(encoding="utf-8").replace(
                    "<TargetFramework>net10.0</TargetFramework>",
                    "<TargetFramework>net10.0</TargetFramework>\n    <MonicaVersion>1.0.0-rc.6</MonicaVersion>",
                ),
                encoding="utf-8",
            )
            packages = output / "Directory.Packages.props"
            packages.write_text(
                packages.read_text(encoding="utf-8").replace(
                    'Version="1.0.0-rc.6"',
                    'Version="$(MonicaVersion)"',
                ),
                encoding="utf-8",
            )
            project = output / "src/Acme.Monica.Example/Acme.Monica.Example.csproj"

            references = validator.package_references(output, project)
            findings = validator.validate_project(
                output,
                project,
                None,
            )

            self.assertEqual("1.0.0-rc.6", references["Monica.UI"].version)
            self.assertNotIn("MTP023", {finding.code for finding in findings})
            self.assertEqual(["OK"], [finding.code for finding in findings])

    def test_validator_enforces_source_link_namespace_ui_tag_and_effective_version(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest = scaffold.load_manifest(self.write_manifest(root, valid_manifest()))
            output = root / "output"
            scaffold.create_repository(manifest, output)
            project = output / "src/Acme.Monica.Example/Acme.Monica.Example.csproj"
            project.write_text(
                project.read_text(encoding="utf-8")
                .replace(' PrivateAssets="All"', "")
                .replace(";monica-ui", ""),
                encoding="utf-8",
            )
            module = output / "src/Acme.Monica.Example/Modules/ModuleExampleUI.cs"
            module.write_text(
                module.read_text(encoding="utf-8")
                .replace("namespace Acme.Monica.Example.Modules;", "namespace Monica.Modules;"),
                encoding="utf-8",
            )

            findings = validator.validate_project(
                output,
                project,
                "1.0.0",
            )
            codes = {finding.code for finding in findings}

            self.assertTrue({"MTP016", "MTP017", "MTP024", "MTP025"}.issubset(codes))

    def test_validator_requires_self_attestation_with_compatibility_mark(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest = scaffold.load_manifest(self.write_manifest(root, valid_manifest()))
            output = root / "output"
            scaffold.create_repository(manifest, output)
            readme = output / "README.md"
            readme.write_text(
                readme.read_text(encoding="utf-8").replace(
                    "Monica compatibility is self-attested by the publisher. ",
                    "",
                ),
                encoding="utf-8",
            )

            findings = validator.validate_project(
                output,
                output / "src/Acme.Monica.Example/Acme.Monica.Example.csproj",
                None,
            )

            self.assertIn("MTP012", {finding.code for finding in findings})

    def test_validator_rejects_modified_compatibility_mark(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest = scaffold.load_manifest(self.write_manifest(root, valid_manifest()))
            output = root / "output"
            scaffold.create_repository(manifest, output)
            mark = output / "monica-compatibility-mark.png"
            mark.write_bytes(mark.read_bytes() + b"modified")

            findings = validator.validate_project(
                output,
                output / "src/Acme.Monica.Example/Acme.Monica.Example.csproj",
                None,
            )

            self.assertIn("MTP032", {finding.code for finding in findings})

    def test_validator_rejects_legacy_localized_component_registration(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest = scaffold.load_manifest(self.write_manifest(root, valid_manifest()))
            output = root / "output"
            scaffold.create_repository(manifest, output)
            module = output / "src/Acme.Monica.Example/Modules/ModuleExampleUI.cs"
            module.write_text(
                module.read_text(encoding="utf-8").replace(
                    "RegisterLocalizedPage<UIExamplePage, ExampleResource>",
                    "RegisterLocalizedComponent<UIExamplePage, ExampleResource>",
                ),
                encoding="utf-8",
            )

            findings = validator.validate_project(
                output,
                output / "src/Acme.Monica.Example/Acme.Monica.Example.csproj",
                None,
            )

            self.assertIn("MTP030", {finding.code for finding in findings})

    def test_validator_rejects_category_identity_and_resource_key_drift(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            manifest = scaffold.load_manifest(self.write_manifest(root, valid_manifest()))
            output = root / "output"
            scaffold.create_repository(manifest, output)
            module = output / "src/Acme.Monica.Example/Modules/ModuleExampleUI.cs"
            module.write_text(
                module.read_text(encoding="utf-8")
                .replace('"Acme.Monica.Example",', '"Acme.Monica.Shared",')
                .replace('"Navigation:Category",', '"Navigation:Group",'),
                encoding="utf-8",
            )

            findings = validator.validate_project(
                output,
                output / "src/Acme.Monica.Example/Acme.Monica.Example.csproj",
                None,
            )
            contract_findings = [finding for finding in findings if finding.code == "MTP031"]

            self.assertEqual(1, len(contract_findings))
            self.assertIn("derive category id 'Acme.Monica.Example'", contract_findings[0].message)
            self.assertIn("Navigation:Category", contract_findings[0].message)

    def test_artifact_inspector_accepts_root_assets_and_valid_symbol_package(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            artifacts = Path(temporary)
            package = artifacts / "Acme.Monica.Example.0.1.0-alpha.1.nupkg"
            symbols = artifacts / "Acme.Monica.Example.0.1.0-alpha.1.snupkg"
            nuspec = """<?xml version="1.0" encoding="utf-8"?>
<package>
  <metadata>
    <id>Acme.Monica.Example</id>
    <version>0.1.0-alpha.1</version>
    <readme>README.md</readme>
    <icon>monica-compatibility-mark.png</icon>
    <license type="expression">MIT</license>
  </metadata>
</package>
"""
            with zipfile.ZipFile(package, "w") as archive:
                archive.writestr("Acme.Monica.Example.nuspec", nuspec)
                archive.writestr(
                    "README.md",
                    "![Icon](monica-compatibility-mark.png)\n\n"
                    "![Open source](monica-open-source-badge.svg)\n",
                )
                archive.writestr("monica-compatibility-mark.png", b"png")
                archive.writestr("monica-open-source-badge.svg", b"svg")
                archive.writestr("lib/net10.0/Acme.Monica.Example.dll", b"assembly")
            with zipfile.ZipFile(symbols, "w") as archive:
                archive.writestr("Acme.Monica.Example.nuspec", nuspec)
                archive.writestr("lib/net10.0/Acme.Monica.Example.pdb", b"portable-pdb")

            self.assertEqual([], inspector.inspect_archive(package, "0.1.0-alpha.1"))
            self.assertEqual([], inspector.inspect_symbol_archive(symbols))


if __name__ == "__main__":
    unittest.main()
