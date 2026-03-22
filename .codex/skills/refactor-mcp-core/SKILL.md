---
name: refactor-mcp-core
description: "Use this skill when the user wants to work with refactor-mcp's common workflow: rename symbol, find usages, move type to a separate file, 查找引用, 查找用法, 重命名符号, or 将类型拆到单独文件. It also applies when the user needs to load a large solution with progress before using those tools."
---

# Refactor MCP Core

Use this skill for the common `refactor-mcp` workflow.

## Core workflow

1. Load the solution first.
If the solution may be slow to load, use `BeginLoadSolution` and poll `GetLoadSolutionStatus`. Only use `LoadSolution` when a blocking call is acceptable.

2. Inspect before changing.
Use `FindUsages` to confirm the symbol and see impact before renaming or moving code. Start with a small `maxResults` to save tokens, then increase it only if needed.

3. Rename semantically.
Use `RenameSymbol` instead of text replacement. Pass `line` and `column` when the same name appears multiple times in one file.

4. Split top-level types cleanly.
Use `MoveToSeparateFile` when a class, interface, struct, record, enum, or delegate should move into its own `<TypeName>.cs` file.

## Tool selection

- `FindUsages`
Use when the user asks "where is this used", "who calls this", "查找引用", or wants impact analysis before a refactor.

- `RenameSymbol`
Use when the user asks for a semantic rename across the solution. This is the default tool for identifiers, members, and top-level types.

- `MoveToSeparateFile`
Use when the user wants one top-level type extracted into its own file without changing the type itself.

## Practical notes

- `FindUsages` currently starts from a C# source file entry point.
- `RenameSymbol` is the strongest option when the repo contains multiple references that must stay consistent.
- For large solutions, prefer async loading and poll progress instead of waiting on one long blocking call.
