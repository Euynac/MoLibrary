# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MoLibrary is a modular .NET infrastructure library designed for flexibility and performance. Each module can be used independently without requiring the entire framework.

## Skills Reference

### MoLibrary Development

For module architecture, registration patterns, and the unified response model `Res`, use the **MoLibrary Development** skill.

The skill covers:
- Module pattern (Module{Name}, Option, Guide, BuilderExtensions)
- Module registration and dependencies
- Unified response model `Res<T>` and `Res`
- Service layer patterns and error handling
- Key architectural decisions

### Blazor and MudBlazor UI Development

For Blazor UI development with MudBlazor, use the **MoLibrary UI Development** skill.

The skill covers:
- CSS isolation patterns and `::deep` selector usage
- MudBlazor component best practices (Icon prefix, type parameters)
- Component lifecycle (OnAfterRenderAsync patterns)
- Theme customization and CSS variables
- MudBlazor 8.9.0 migration guide
- Offline/intranet requirements

**Current MudBlazor version**: 8.9.0

## Code Quality Principles

- Write reusable, low-coupling and high-cohesion implementations with multiple abstractions
- Split files to avoid overly large single files
- Instead of just fixing errors, use simplified thinking and refactor whenever possible
- If you feel the design is inadequate or lacks necessary information, you may raise concerns and propose improvements for user confirmation before proceeding.

## Dependency Injection Guidelines

- Always use primary constructor when creating a class with single constructor using dependency injection
  - More details can be read in @rules\primary-constructor.mdc
- After defining `Module{Name}Option`, to use the module options, simply inject `IOptions<TModuleOption>` or `IOptionsSnapshot<TModuleOption>` for usage.

## Available MCP Servers

- **mcp__microsoft-docs__microsoft_docs_search**: MCP Server for searching Microsoft/Azure official documentation. This is particularly useful for finding ASP.NET Core, Blazor, and related documentation and best practices.

## Development Phase & Optimization Policy

- **Development Stage**: This project is in internal development and has not been released. Backward compatibility is not a concern unless explicitly instructed otherwise.
- **Optimization First**: Always prioritize the most optimal design and implementation approaches. Proactively identify and propose refactoring or redesign opportunities when improvements are possible.
- **Testing Policy**: Unit testing is not required during this phase. Do not include testing-related tasks in planning or implementation unless explicitly requested.
