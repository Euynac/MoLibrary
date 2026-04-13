# Monica Documentation Information Architecture

## Table of Contents

1. [Goals](#1-goals)
2. [Root layout](#2-root-layout)
3. [Page families](#3-page-families)
4. [Module slug rules](#4-module-slug-rules)
5. [Module documentation pack layout](#5-module-documentation-pack-layout)
6. [Shared assets](#6-shared-assets)

## 1. Goals

Use a docs tree that is:

- Easy to scan
- Stable for future migration
- Friendly to bilingual maintenance
- Organized around **Monica modules and concepts**, not random historical folders

## 2. Root layout

```text
docs/
├── zh-CN/
│   ├── index.md
│   ├── getting-started/
│   ├── concepts/
│   ├── modules/
│   └── scenarios/
├── en-US/
│   ├── index.md
│   ├── getting-started/
│   ├── concepts/
│   ├── modules/
│   └── scenarios/
└── shared/
    └── attachments/
```

Keep `zh-CN` and `en-US` fully mirrored.

## 3. Page families

### `getting-started/`

Use for onboarding pages:

- introduction
- installation
- first Monica module
- first Monica UI module

### `concepts/`

Use for Monica-wide concepts:

- module pattern
- Guide and options
- Facade vs Service vs Provider
- infrastructure vs UI module relationship
- localization, result model, or similar framework concepts

### `modules/`

Use for user-facing documentation about a specific Monica module.

Every public module gets its own folder.

### `scenarios/`

Use for cross-module recipes such as:

- building an admin dashboard
- combining AI + RAG + UI
- setting up a distributed environment

Scenario guides should link back to module packs instead of duplicating all module details.

## 4. Module slug rules

Document **modules**, not projects.

Derive the module slug from the public module / registration name in kebab-case.

Examples:

- `Mo.AddConfiguration()` → `modules/configuration/`
- `Mo.AddProjectUnits()` → `modules/project-units/`
- `Mo.AddRAG()` → `modules/rag/`
- `Mo.AddRAGUI()` → `modules/rag-ui/`

Rules:

- If one project exposes multiple modules, give each module a separate folder
- If an infra module and UI module both exist, document them in separate folders and cross-link them
- Do not force project-name grouping when it hurts discoverability

## 5. Module documentation pack layout

```text
docs/{locale}/modules/{module-slug}/
├── index.md
├── quick-start.md
├── configuration.md
├── guide-and-providers.md
└── scenarios.md
```

Use this pack by default for a real Monica module.

## 6. Shared assets

Use these rules:

- Page-local asset → keep it close to the page when practical
- Cross-page or cross-locale asset → place it under `docs/shared/attachments/`
- Reference assets through relative paths from the markdown file

Avoid a giant flat attachment dumping ground when assets have clear page ownership.
