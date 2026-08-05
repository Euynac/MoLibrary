# Module Documentation Pack Template

This is the default output shape for a Monica module.

## File layout

```text
../Monica.Docs/docs/{locale}/modules/{module-slug}/
├── index.md
├── quick-start.md
├── configuration.md
├── guide-and-providers.md
└── scenarios.md
```

## 1. `index.md`

````md
---
title: {Module Display Name}
description: One-sentence summary of what the module provides.
sidebar_position: 1
---

# {Module Display Name}

{One-paragraph overview.}

## When to use this module

- Use when ...
- Especially useful for ...

## Package and registration

| Item | Value |
|---|---|
| Package | `Monica.{ProjectOrPackage}` |
| Registration | `monica.Add{Name}()` |
| Related UI module | `monica.Add{Name}UI()` / None |

## Public surface

- Public Facades: ...
- Public Abstractions: ...
- Public Models / events / exceptions: ...

## Related pages

- [Quick Start](./quick-start.md)
- [Configuration](./configuration.md)
- [Guide and Providers](./guide-and-providers.md)
- [Scenarios](./scenarios.md)
````

## 2. `quick-start.md`

````md
---
title: Quick Start
description: Install and register {Module Display Name}.
sidebar_position: 2
---

# Quick Start

## Install the package

```bash
dotnet add package Monica.{ProjectOrPackage}
```

## Minimal registration

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddMonica(monica =>
{
    monica.Add{Name}();
});

var app = builder.Build();
app.UseMonica();
app.MapMonica();
app.Run();
```

## First useful configuration

Explain the smallest non-default setup that real users usually need.

## What to read next

Link to configuration or provider pages.
````

## 3. `configuration.md`

````md
---
title: Configuration
description: Module options, defaults, and required setup for {Module Display Name}.
sidebar_position: 3
---

# Configuration

## Module options

| Property | Type | Default | Required | When to change | Notes |
|---|---|---|---|---|---|

## Extra options

Add this section only when the module exposes extra options types.

## Required setup

Add this section when `GetRequestedConfigMethodKeys()` or equivalent public requirements exist.

| Requirement | Satisfied by | Notes |
|---|---|---|
````

## 4. `guide-and-providers.md`

````md
---
title: Guide and Providers
description: Guide methods, provider choices, and dependency notes for {Module Display Name}.
sidebar_position: 4
---

# Guide and Providers

## Guide methods

| Method | What it enables | Required | Typical use |
|---|---|---|---|

## Provider choices

| Choice | How to enable it | When to use it |
|---|---|---|

## Module dependencies

List user-visible dependencies or strongly related modules here.
````

## 5. `scenarios.md`

````md
---
title: Scenarios
description: Common ways to use {Module Display Name} in real Monica applications.
sidebar_position: 5
---

# Scenarios

## Scenario 1 — {Common scenario}

Explain the goal, the registration pattern, and the key settings.

## Scenario 2 — {Another common scenario}

Add only scenarios grounded in the actual module behavior.

## Common mistakes

- Mistake 1
- Mistake 2
````

## Pack rules

- Keep the pack user-facing and source-grounded
- Remove empty sections instead of leaving placeholders
- Prefer cross-links over repeated explanations
- If the module is extremely small, collapsing the pack into fewer pages is acceptable only when the user explicitly prefers it
