---
name: monica-ui-localization
description: This skill should be used when creating, modifying, validating, or reviewing Monica UI localization/i18n resources, replacing hardcoded user-facing text, adding IStringLocalizer usage, registering localized pages or navigation categories, synchronizing zh-CN/en-US JSON files, or running the Monica localization validator.
version: 1.0.0
---

# Monica UI Localization

This skill owns Monica UI localization and i18n validation.

All script paths in this document are relative to the `monica-ui-localization` skill directory.

## Required Workflow

1. Replace every user-facing UI string with localization. Do not hardcode labels, button text, helper text, dialog text, snackbar messages, placeholders, table headers, empty states, or navigation/AppBar text.
2. Use decentralized module resources with marker class + JSON resource files under the project root `Localization/` directory.
3. Keep `zh-CN.json` and `en-US.json` synchronized for every changed resource.
4. Finish every i18n change by running the strict validator from the repository root:

```bash
python .agents/skills/monica-ui-localization/scripts/validate_localization.py --strict
```

The strict result must have zero JSON integrity errors, missing keys, invalid navigation resource keys, unused keys, and language sync issues. A non-strict `PASSED` result with unused-key warnings is not acceptable for completed i18n work.

## Core Rules

- Prefer nested JSON objects and access them with colon-separated keys such as `Page:Title` or `RuntimeConfigDialog:Intro`.
- Do not use flat dot-style keys such as `Page.Title` for new Monica UI work.
- Prefer dependency-injected `IStringLocalizer<TResource>` in Razor components, pages, dialogs, state classes, support services, and any DI-created service.
- Never use ambient or static localization access. When DI is unavailable inside a helper or view model, accept an `IStringLocalizer` parameter or move the display behavior into a cohesive formatter that receives one.
- Inject `ILocalizationCatalog` only for scenarios that genuinely need generic resource lookup. At application-composition boundaries such as endpoint metadata configuration, resolve the localizer or catalog from the current host's service provider so localization state never crosses host boundaries.
- For page content, use the module-local resource marker and JSON files.
- Every localized page must use `RegisterLocalizedPage<TPage, TResource>(...)`; its title key belongs to the owning module resource and that resource must be registered through `AddResource<TResource>()`.
- Use `BuiltInNavigationCategoryIds` for the shell taxonomy, or register a publisher-qualified module category with `RegisterLocalizedCategory<TResource>(stableId, displayNameKey, order)` and pass the returned ID to its pages.
- `RegisterLocalizedComponent` and `UIRegistryResource` are obsolete and must not be reintroduced.
- Resource marker classes and JSON folders stay under the project root `Localization/` directory, not feature folders.

## Validation Commands

Basic validation:

```bash
python scripts/validate_localization.py
```

Strict validation:

```bash
python scripts/validate_localization.py --strict
```

JSON output:

```bash
python scripts/validate_localization.py --strict --json
```

Validate another root:

```bash
python scripts/validate_localization.py --root <project-root> --strict
```

## Reference

Read `references/localization-guide.md` when creating new resource files, debugging missing translations, adding parameterized strings, or fixing validator failures.
