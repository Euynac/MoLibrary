---
name: monica-framework
description: Router skill for Monica framework development. Use when working on Monica infrastructure modules, module architecture, Blazor UI modules, documentation, requirement design, UI audits, or Monica test conventions and choose the correct Monica framework companion skills.
---

# Monica Framework

Use this skill as the entry point for work on Monica itself: infrastructure modules, shared UI, framework documentation, requirements, and test conventions.

## Routing

- Module architecture, folder boundaries, Facades, Providers, public/internal placement, page decomposition: use `$monica-architecture`.
- Module registration, `Res` and `Res<T>`, services, Guide methods, hosted services, runtime module behavior: use `$monica-development`.
- Blazor or MudBlazor implementation, CSS isolation, themes, localization, browser storage: use `$monica-ui-development`.
- UI concept design or interactive HTML prototypes before implementation: use `$monica-ui-design`.
- Theme-first component compliance and hardcoded-style audits: use `$monica-ui-audit`.
- Monica user documentation, module docs, guides, and zh-CN documentation: use `$monica-docs-authoring`.
- Requirement gathering and architecture planning for Monica modules: use `$monica-requirement-design`.
- Monica unit-test layout, shared test infrastructure, xUnit, or bUnit conventions: use `$monica-unit-testing`.
- Bridge-based UI inspection through a runnable host: use `$monica-ui-bridge-debug`.

## Working Rule

Load only the granular skill that matches the active task. Combine skills when the task crosses boundaries, such as `$monica-architecture` plus `$monica-ui-development` for a new UI module.
