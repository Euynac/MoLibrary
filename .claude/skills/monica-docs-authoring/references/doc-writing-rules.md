# Monica Documentation Writing Rules

## Table of Contents

1. [Audience and scope](#1-audience-and-scope)
2. [Sources of truth](#2-sources-of-truth)
3. [Required frontmatter](#3-required-frontmatter)
4. [Page structure and writing style](#4-page-structure-and-writing-style)
5. [Code sample rules](#5-code-sample-rules)
6. [Configuration and Guide tables](#6-configuration-and-guide-tables)
7. [Links and asset rules](#7-links-and-asset-rules)
8. [Locale scope rules](#8-locale-scope-rules)
9. [Do not do these things](#9-do-not-do-these-things)

## 1. Audience and scope

Write for **Monica users** who want to adopt or configure the framework.

Focus on:

- What the module / concept provides
- When to use it
- How to register and configure it
- Which Guide methods and providers matter
- What the public contract looks like

Do not optimize user docs for internal maintainers. Internal design details belong in architecture or design docs, not in the default user-facing pages.

## 2. Sources of truth

Source priority is strict:

1. Current source code in this repository
2. Current architecture guidance, especially `$monica-architecture`
3. Existing pages under `../Monica.Docs/docs`, but only after re-verifying them against current code
4. Current README or package README content, only if it matches code

`../Monica.Docs/docs` is the current documentation project, not a legacy dump. If a page there conflicts with code, follow the code and rewrite the page in place.

## 3. Required frontmatter

Use this frontmatter by default:

```yaml
---
title: Module or page title
description: One-sentence summary for navigation and search
sidebar_position: 1
---
```

Rules:

- Prefer file-path-based routing; do **not** add `slug` unless the site explicitly needs an override
- Keep `title` and `description` human-friendly
- Use `sidebar_position` for pages that appear in ordered navigation groups

## 4. Page structure and writing style

Use this default page flow:

1. **One-paragraph summary**
2. **When to use it**
3. **Smallest correct example**
4. **Detailed configuration / behavior / variants**
5. **Related pages / next steps**

Style rules:

- Put the answer first, then detail
- Prefer short sections and tables over long prose
- Keep identifiers, API names, types, namespaces, and code in English
- Keep narrative language in Chinese unless the user explicitly requests another locale
- Explain defaults and tradeoffs, not just names
- Be explicit about required vs optional setup

## 5. Code sample rules

- Use real current APIs from source code
- Use `Mo.Add*()` naming, not obsolete registration names
- Prefer the smallest copy-pasteable sample that communicates the concept
- Use `bash` for installation and `csharp` for Monica code samples
- If a sample omits unrelated lines, omit them clearly rather than inventing scaffolding
- Do not label pseudocode as production-ready code

Good sample goals:

- Show the correct package
- Show the correct registration call
- Show the minimum required Guide or provider method calls
- Show one realistic option override when it teaches something important

## 6. Configuration and Guide tables

Use a configuration table like this:

| Property | Type | Default | Required | When to change | Notes |
|---|---|---|---|---|---|
| `ExampleOption` | `string` | `"value"` | No | Change when ... | ... |

Rules:

- Read defaults from the real property initializer
- Mark a setting as required only when the module truly cannot be used without it, or when Guide validation requires it
- Explain the practical impact of changing the option

Use a Guide table like this:

| Method | What it enables | Required | Typical use |
|---|---|---|---|
| `UseXxxProvider()` | Registers ... | Yes / No | Use when ... |

If the module uses `GetRequestedConfigMethodKeys()`, add a **Required setup** section that maps each requirement key to the public Guide methods that satisfy it.

## 7. Links and asset rules

- Use **relative links** for local markdown pages and local assets
- Do **not** hard-code Monica.Docs backend asset URLs such as `/api/docs/assets/...`
- Prefer page-local assets when only one page uses them
- If an asset is shared across multiple pages, place it under `docs/shared/attachments/`
- Prefer text over screenshots when the concept can be explained clearly without an image

The Monica.Docs backend rewrites relative local asset links automatically. Preserve that behavior by keeping the markdown source relative.

## 8. Locale scope rules

The default documentation target for this skill is:

- `../Monica.Docs/docs/zh-CN/...`

Rules:

- Update Chinese docs in `zh-CN` by default
- Do not create or maintain `en-US` mirrors unless the user explicitly asks for English documentation
- Keep narrative language in Chinese for Monica user docs unless the user explicitly requests another locale
- If an English page already exists but the task is not explicitly multi-locale, you still only update the Chinese source by default

## 9. Do not do these things

- Do not treat internal `Services/` as the public usage surface
- Do not copy old docs without re-verifying them against current code
- Do not invent Guide methods, option properties, or package names
- Do not leave placeholder headings or TODO text in delivered docs
- Do not describe private implementation classes as stable user contracts unless the public API exposes them intentionally
- Do not mix architecture criticism, migration notes, and user onboarding in the same page unless the page is explicitly about migration
