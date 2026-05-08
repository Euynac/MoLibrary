---
name: monica-ui-localization
description: This skill should be used when creating, modifying, validating, or reviewing Monica UI localization/i18n resources, replacing hardcoded user-facing text, adding IStringLocalizer usage, adding RegisterLocalizedComponent navigation/AppBar keys, synchronizing zh-CN/en-US JSON files, or running the Monica localization validator.
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
python .claude/skills/monica-ui-localization/scripts/validate_localization.py --strict
```

The strict result must have zero JSON integrity errors, missing keys, invalid UI registry keys, unused keys, and language sync issues. A non-strict `PASSED` result with unused-key warnings is not acceptable for completed i18n work.

## Core Rules

- Prefer nested JSON objects and access them with colon-separated keys such as `Page:Title` or `RuntimeConfigDialog:Intro`.
- Do not use flat dot-style keys such as `Page.Title` for new Monica UI work.
- Prefer dependency-injected `IStringLocalizer<TResource>` in Razor components, pages, dialogs, state classes, support services, and any DI-created service.
- Use `LocalizationManager.Get/For` only where DI is unavailable, such as static helpers, view-model computed properties created outside DI, and module registration or endpoint metadata built outside a service instance.
- For page content, use the module-local resource marker and JSON files.
- For `RegisterLocalizedComponent(...)` navigation/AppBar text, `displayNameKey` and `categoryKey` must exist in `Monica.UI/Localization/UIRegistryResource/*.json`, because the UI registry resolves them with `IStringLocalizer<UIRegistryResource>`.
- When adding a new page to navigation, add the corresponding `Pages:*:Title` key to `UIRegistryResource` in addition to the page module resource when needed.
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
