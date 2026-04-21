---
name: mo-docs-authoring
description: This skill should be used when the user asks to "write docs", "document module", "rewrite docs", "migrate docs", "quick start", "configuration reference", "guide method docs", "双语文档", "编写文档", "模块文档", "文档迁移", or needs to create, revise, migrate, or standardize Monica user documentation from current source code, including Monica.Docs/docs pages, framework guides, module documentation packs, configuration tables, provider guides, and zh-CN documentation pages.
---

# Monica Docs Authoring

Standardize how Codex writes Monica user documentation. Treat current source code and `$mo-architecture` as the source of truth. The Monica documentation project lives in `../Monica.Docs`, and the markdown source lives under `../Monica.Docs/docs`.

## Workflow

### 1. Classify the documentation target

Start by choosing one of these output types:

- **Framework / getting-started page** — onboarding, installation, first module, overall Monica workflow
- **Concept page** — module pattern, Guide/options, Facade vs Service vs Provider, localization, UI module usage
- **Module documentation pack** — the default for a specific Monica module
- **Scenario guide** — cross-module integration or a real-world recipe

### 2. Rebuild truth from source before writing

Follow `references/source-of-truth-checklist.md`.

At minimum, inspect:

- The target project's `.csproj`
- `Modules/Module{Name}.cs`
- Public `Abstractions/`, `Models/`, `Facades/`, `Events/`, `Exceptions/`, `Annotations/`
- Related UI module registration file when an infra/UI pair exists
- Existing framework-level docs only as secondary confirmation

Always extract and verify:

- NuGet package / project name
- Public `Mo.Add*()` registration entry
- `ModuleOption` and extra option properties with real default values
- `ModuleGuide` methods and what each one enables
- `GetRequestedConfigMethodKeys()` requirements when present
- Declared module dependencies
- Host-facing public surface and notable providers

Existing docs under `../Monica.Docs/docs` are the current publication target. Revise them in place when they already exist, but never trust them over current code.

### 3. Choose the output shape

Use `references/docs-information-architecture.md`.

Default rules:

- Keep documentation under `../Monica.Docs/docs/`
- Default all doc authoring work to `../Monica.Docs/docs/zh-CN/`
- Do **not** create or maintain `en-US` mirrors unless the user explicitly asks for English documentation
- Use **module-level slugs** in kebab-case, derived from the public module name / registration name
- Give each module its own documentation pack instead of mixing multiple modules into one large page

### 4. Use the Monica writing rules

Use `references/doc-writing-rules.md`.

Important defaults:

- Write for **Monica users**, not internal maintainers
- Explain the **public contract**, not private implementation details
- Show the **smallest correct example first**
- Use current `Mo.Add*()` APIs; do not revive obsolete `AddMoModule*` names
- Keep local asset links relative so Monica.Docs can rewrite them correctly

### 5. Apply the correct template

- Framework / onboarding pages: `references/framework-page-template.md`
- Module docs: `references/module-doc-pack-template.md`

When the user asks for “module docs”, default to the module pack template unless they explicitly want a single-page variant.

## Monica-specific documentation rules

- Document **public entry points** first: `Mo.Add*()`, `ModuleGuide`, public Facades, public Abstractions, public Models
- Mention internal Services or Providers only to explain behavior or provider choices exposed through public Guide methods
- If a module has required Guide configuration keys, add a **Required setup** section that maps each requirement to the methods that satisfy it
- If a project exposes multiple modules, document them as **separate module packs**
- If an infra module has a related UI module, cross-link them; do not merge them by default
- Prefer file-path-based slugs; do not add frontmatter `slug` unless the site explicitly needs an override
- Write and revise Chinese docs in `zh-CN` only unless the user explicitly expands scope to another locale

## References

- **`references/doc-writing-rules.md`** — tone, frontmatter, tables, code sample rules, asset rules, and locale-scope rules
- **`references/docs-information-architecture.md`** — canonical docs folder layout and page ownership
- **`references/framework-page-template.md`** — template for intro, installation, concept, and onboarding pages
- **`references/module-doc-pack-template.md`** — default 5-page module documentation pack template
- **`references/source-of-truth-checklist.md`** — exact discovery workflow before writing or migrating docs

## Output expectation

When creating or revising docs, produce content that another Monica user can follow without reading the source code first, while still staying fully grounded in the real source.
