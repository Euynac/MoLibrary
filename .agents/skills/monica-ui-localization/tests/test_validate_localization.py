#!/usr/bin/env python3
"""Focused fixtures for resource-aware Monica localization validation."""

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


SCRIPT_PATH = (
    Path(__file__).resolve().parents[1]
    / 'scripts'
    / 'validate_localization.py'
)
SPEC = importlib.util.spec_from_file_location('validate_localization', SCRIPT_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f'Could not load validator from {SCRIPT_PATH}')
VALIDATOR_MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VALIDATOR_MODULE)
LocalizationValidator = VALIDATOR_MODULE.LocalizationValidator
run_validation = VALIDATOR_MODULE.run_validation


class LocalizationValidatorTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary_directory.name) / 'repository'
        self.root.mkdir(parents=True)
        self._write('Fixture.slnx', '<Solution />')

    def tearDown(self) -> None:
        self.temporary_directory.cleanup()

    def test_overlapping_keys_do_not_hide_resource_specific_missing_or_unused_keys(self) -> None:
        self._add_project(
            'MixedPackage',
            resources={
                'ResourceA': {
                    'en-US': {'Shared': {'Title': 'A'}, 'OnlyA': {'Title': 'A only'}},
                    'zh-CN': {'Shared': {'Title': '甲'}, 'OnlyA': {'Title': '仅甲'}},
                },
                'ResourceB': {
                    'en-US': {'Shared': {'Title': 'B'}},
                    'zh-CN': {'Shared': {'Title': '乙'}},
                },
            },
            sources={
                'Pages/PageA.razor': (
                    '@inject IStringLocalizer<ResourceA> A\n'
                    '<h1>@A["Shared:Title"]</h1>\n'
                    '<p>@A["OnlyA:Title"]</p>\n'
                ),
                'Pages/PageB.razor': (
                    '@inject IStringLocalizer<ResourceB> B\n'
                    '<h1>@B["OnlyA:Title"]</h1>\n'
                ),
            },
        )

        validator, report = self._validate()
        resource_a = self._resource_path('MixedPackage', 'ResourceA')
        resource_b = self._resource_path('MixedPackage', 'ResourceB')

        self.assertIn((resource_b, 'OnlyA:Title'), validator.missing_keys)
        self.assertNotIn((resource_a, 'OnlyA:Title'), validator.missing_keys)
        self.assertIn((resource_b, 'Shared:Title'), validator.unused_keys)
        self.assertNotIn((resource_a, 'Shared:Title'), validator.unused_keys)
        self.assertEqual(1, report['summary']['missing_count'])

    def test_language_sync_is_scoped_to_each_resource(self) -> None:
        self._add_project(
            'MixedPackage',
            resources={
                'ResourceA': {
                    'en-US': {'Shared': {'Title': 'A'}},
                    'zh-CN': {'Shared': {'Title': '甲'}},
                },
                'ResourceB': {
                    'en-US': {'Shared': {'Title': 'B'}},
                    'zh-CN': {},
                },
            },
            sources={
                'Pages/Page.razor': (
                    '@inject IStringLocalizer<ResourceB> L\n'
                    '<h1>@L["Shared:Title"]</h1>\n'
                ),
            },
        )

        validator, _ = self._validate()
        resource_a = self._resource_path('MixedPackage', 'ResourceA')
        resource_b = self._resource_path('MixedPackage', 'ResourceB')

        self.assertNotIn((resource_a, 'Shared:Title'), validator.sync_issues)
        self.assertEqual(
            {'zh-CN': False, 'en-US': True},
            validator.sync_issues[(resource_b, 'Shared:Title')],
        )

    def test_navigation_keys_are_checked_against_explicit_registered_resource(self) -> None:
        self._add_project(
            'MixedPackage',
            resources={
                'ResourceA': {
                    'en-US': {
                        'Navigation': {'Page': 'Page', 'Category': 'Category'},
                    },
                    'zh-CN': {
                        'Navigation': {'Page': '页面', 'Category': '分类'},
                    },
                },
                'ResourceB': {
                    'en-US': {'Other': {'Key': 'Other'}},
                    'zh-CN': {'Other': {'Key': '其他'}},
                },
            },
            sources={
                'Modules/ModuleDashboard.cs': (
                    '[ModuleKey("Acme.Monica.MixedPackage.UI")]\n'
                    'public sealed class ModuleDashboard\n'
                    '{\n'
                    '    void Register(INavigationRegistryBuilder registry)\n'
                    '    {\n'
                    '        localization.AddResource<ResourceB>();\n'
                    '        var category = registry.RegisterLocalizedCategory<ResourceB>(\n'
                    '            "Acme.Monica.MixedPackage",\n'
                    '            "Navigation:Category");\n'
                    '        registry.RegisterLocalizedPage<Page, ResourceB>(\n'
                    '            "/page",\n'
                    '            "Navigation:Page",\n'
                    '            categoryId: category);\n'
                    '    }\n'
                    '    ModuleLocalizationGuide localization = default!;\n'
                    '}\n'
                ),
            },
            razor_sdk=False,
        )

        validator, _ = self._validate()
        resource_b = self._resource_path('MixedPackage', 'ResourceB')

        self.assertIn(
            (resource_b, 'Navigation:Page'),
            validator.module_resource_missing_keys,
        )
        self.assertIn(
            (resource_b, 'Navigation:Category'),
            validator.module_resource_missing_keys,
        )

    def test_localized_page_without_explicit_resource_is_rejected(self) -> None:
        self._add_project(
            'MixedPackage',
            resources={
                'ResourceA': {
                    'en-US': {'Navigation': {'Page': 'Page'}},
                    'zh-CN': {'Navigation': {'Page': '页面'}},
                },
            },
            sources={
                'Modules/ModuleDashboard.cs': (
                    '[ModuleKey("Acme.Monica.MixedPackage.UI")]\n'
                    'public sealed class ModuleDashboard\n'
                    '{\n'
                    '    void Register(INavigationRegistryBuilder registry)\n'
                    '    {\n'
                    '        registry.RegisterLocalizedPage<Page>("/page", "Navigation:Page");\n'
                    '    }\n'
                    '}\n'
                ),
            },
            razor_sdk=False,
        )

        _, report = self._validate()

        self.assertTrue(any(
            'must declare both TPage and TResource' in message
            for message in report['resource_resolution_errors']
        ))

    def test_navigation_resource_must_be_registered_through_add_resource(self) -> None:
        self._add_project(
            'MixedPackage',
            resources={
                'ResourceA': {
                    'en-US': {'Navigation': {'Page': 'Page'}},
                    'zh-CN': {'Navigation': {'Page': '页面'}},
                },
            },
            sources={
                'Modules/ModuleDashboard.cs': (
                    '[ModuleKey("Acme.Monica.MixedPackage.UI")]\n'
                    'public sealed class ModuleDashboard\n'
                    '{\n'
                    '    void Register(INavigationRegistryBuilder registry)\n'
                    '    {\n'
                    '        registry.RegisterLocalizedPage<Page, ResourceA>(\n'
                    '            "/page", "Navigation:Page");\n'
                    '    }\n'
                    '}\n'
                ),
            },
            razor_sdk=False,
        )

        _, report = self._validate()

        self.assertTrue(any(
            'is not registered through AddResource<ResourceA>()' in message
            for message in report['resource_resolution_errors']
        ))

    def test_legacy_localized_component_registration_is_rejected(self) -> None:
        self._add_project(
            'MixedPackage',
            resources={
                'ResourceA': {
                    'en-US': {'Navigation': {'Page': 'Page'}},
                    'zh-CN': {'Navigation': {'Page': '页面'}},
                },
            },
            sources={
                'Modules/ModuleDashboard.cs': (
                    '[ModuleKey("Acme.Monica.MixedPackage.UI")]\n'
                    'public sealed class ModuleDashboard\n'
                    '{\n'
                    '    void Register(INavigationRegistryBuilder registry)\n'
                    '    {\n'
                    '        registry.RegisterLocalizedComponent<Page, ResourceA>(\n'
                    '            "/page", "Navigation:Page");\n'
                    '    }\n'
                    '}\n'
                ),
            },
            razor_sdk=False,
        )

        _, report = self._validate()

        self.assertTrue(any(
            'Legacy RegisterLocalizedComponent usage is not allowed' in message
            for message in report['resource_resolution_errors']
        ))

    def test_mixed_ui_project_does_not_require_ui_project_suffix(self) -> None:
        project = self._add_project(
            'Acme.MixedPackage',
            resources={
                'MixedResource': {
                    'en-US': {'Page': {'Title': 'Title'}},
                    'zh-CN': {'Page': {'Title': '标题'}},
                },
            },
            sources={
                'Modules/ModuleDashboard.cs': (
                    '[ModuleKey("Acme.Monica.MixedPackage.UI")]\n'
                    'public sealed class ModuleDashboard;\n'
                ),
            },
            razor_sdk=False,
        )

        validator = LocalizationValidator(self.root, ['zh-CN', 'en-US'])

        self.assertIn(project.resolve(), validator.ui_projects)

    def test_exclusions_are_relative_to_explicit_root_inside_parent_tmp(self) -> None:
        outer = Path(self.temporary_directory.name)
        self.root = outer / '.tmp' / 'explicit-root'
        self.root.mkdir(parents=True)
        self._write('Fixture.slnx', '<Solution />')
        included = self._add_project(
            'IncludedProject',
            resources={
                'IncludedResource': {
                    'en-US': {'Page': {'Title': 'Title'}},
                    'zh-CN': {'Page': {'Title': '标题'}},
                },
            },
            sources={
                'Page.razor': (
                    '@inject IStringLocalizer<IncludedResource> L\n'
                    '@L["Page:Title"]\n'
                ),
            },
        )
        stale = self._add_project(
            '.tmp/StaleProject',
            resources={
                'StaleResource': {
                    'en-US': {'Page': {'Title': 'Stale'}},
                    'zh-CN': {'Page': {'Title': '过期'}},
                },
            },
            sources={'Page.razor': '@inject IStringLocalizer<StaleResource> L\n'},
        )

        validator = LocalizationValidator(self.root, ['zh-CN', 'en-US'])

        self.assertIn(included.resolve(), validator.ui_projects)
        self.assertNotIn(stale.resolve(), validator.ui_projects)

    def test_empty_discovery_cannot_report_success(self) -> None:
        self._write(
            'EmptyUi/EmptyUi.csproj',
            '<Project Sdk="Microsoft.NET.Sdk.Razor" />',
        )

        _, report = self._validate()

        self.assertEqual('FAILED', report['summary']['status'])
        self.assertEqual(1, report['summary']['discovery_errors_count'])
        self.assertEqual(1, report['summary']['discovery_warnings_count'])

    def _validate(self):
        validator = LocalizationValidator(self.root, ['zh-CN', 'en-US'])
        report = run_validation(validator)
        return validator, report

    def _add_project(
        self,
        name: str,
        resources: dict,
        sources: dict,
        razor_sdk: bool = True,
    ) -> Path:
        project_path = self.root / name
        sdk = 'Microsoft.NET.Sdk.Razor' if razor_sdk else 'Microsoft.NET.Sdk'
        self._write(f'{name}/{Path(name).name}.csproj', f'<Project Sdk="{sdk}" />')
        for resource_name, cultures in resources.items():
            for culture, content in cultures.items():
                self._write(
                    f'{name}/Localization/{resource_name}/{culture}.json',
                    json.dumps(content, ensure_ascii=False),
                )
        for relative_path, content in sources.items():
            self._write(f'{name}/{relative_path}', content)
        return project_path

    def _resource_path(self, project: str, resource: str) -> str:
        return str((self.root / project / 'Localization' / resource).resolve())

    def _write(self, relative_path: str, content: str) -> None:
        target = self.root / relative_path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding='utf-8')


if __name__ == '__main__':
    unittest.main()
