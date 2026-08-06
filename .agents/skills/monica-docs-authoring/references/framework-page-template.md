# Framework Page Templates

Use these templates for Monica-wide onboarding or concept pages.

## Template A — Getting Started Page

````md
---
title: Getting Started with Monica
description: Install Monica and register your first module.
sidebar_position: 1
---

# Getting Started with Monica

Monica is agent-governed application architecture for observable .NET backends. Start with the Stable packages you need, then compose one explicit host-bound module graph.

## What you will do

- Install the package
- Register the module
- Run the application with Monica enabled

## Install

```bash
dotnet add package Monica.Example
```

## Minimal setup

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.AddMonica(monica =>
{
    monica.AddExample();
});

var app = builder.Build();
app.UseMonica();
app.MapMonica();
app.Run();
```

## Next steps

- Configure module options
- Choose providers when needed
- Read the module documentation pack
````

## Template B — Concept Page

````md
---
title: Monica Module Pattern
description: Understand how Monica modules expose registration, options, and fluent feature extensions.
sidebar_position: 2
---

# Monica Module Pattern

Monica modules follow a consistent public pattern built around `builder.AddMonica(...)`, `monica.Add*()`, `ModuleOptions<TModule>`, and `ModuleRegistration<TModule, TOptions>`.

## Why this pattern exists

Explain the Monica-specific reason for the abstraction.

## Public parts

- `builder.AddMonica(...)` host boundary
- `monica.Add*()` registration entry
- `ModuleOption`
- Fluent `ModuleRegistration<TModule, TOptions>` extensions
- Public Facades / Abstractions / Models when relevant

## Example

```csharp
builder.AddMonica(monica =>
{
    monica.AddExample(o =>
    {
        o.SomeSetting = true;
    })
    .UseSomeProvider();
});
```

## Key rules

- Use current APIs
- Explain required configuration explicitly
- Link to related module pages
````

## Usage notes

- Keep framework pages conceptual and navigational
- Do not bury module-specific details here if they belong in a module pack
- Cross-link to the relevant `modules/{module-slug}/` pages instead of duplicating them
