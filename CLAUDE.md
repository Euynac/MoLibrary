# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MoLibrary is a modular .NET infrastructure library designed for flexibility and performance. Each module can be used independently without requiring the entire framework.

## Skills

Proactively invoke these skills when encountering relevant development patterns:

### /mo-development

Invoke when:
- Writing service layer methods with `Res` or `Res<T>` return types
- Uncertain about Res implicit conversions or IsFailed pattern
- Creating modules (Module{Name}, Option, Guide, BuilderExtensions)
- Configuring module registration or dependencies
- Implementing hosted services (MoBackgroundService, RecordState)

### /mo-ui-development

Invoke when:
- Creating or modifying Blazor components
- Styling MudBlazor components (CSS isolation, ::deep selector)
- Working with MudBlazor APIs or component properties
- Implementing theme customization or dark mode support
- Handling component lifecycle (OnAfterRenderAsync)

**Current MudBlazor version**: 8.9.0

### /microsoft-docs:microsoft-code-reference

Invoke when:
- Working with Azure SDKs, .NET libraries, or Microsoft APIs
- Need to verify method signatures or find correct class/method names
- Looking for working code samples before writing SDK code
- Troubleshooting errors like "method not found", wrong signatures, or deprecated patterns

### /microsoft-docs:microsoft-docs

Invoke when:
- Understanding Azure/Microsoft concepts, architecture, or service behavior
- Finding tutorials, quickstarts, or step-by-step guides
- Looking up configuration options, limits, or quotas
- Need official best practices for Azure/.NET development

### Context7 (MCP Tool)

Use `resolve-library-id` then `get-library-docs` when:
- Working with third-party libraries (e.g., Serilog, MediatR, FluentValidation, Polly)
- Need up-to-date API documentation or code examples for external packages
- Encountering API changes, deprecated methods, or version-specific behavior
- Uncertain about correct usage patterns for NuGet packages

## Code Quality Principles

- Write reusable, low-coupling and high-cohesion implementations with multiple abstractions
- Split files to avoid overly large single files
- Instead of just fixing errors, use simplified thinking and refactor whenever possible
- If you feel the design is inadequate or lacks necessary information, you may raise concerns and propose improvements for user confirmation before proceeding.

## Dependency Injection Guidelines

- Always use primary constructor when creating a class with single constructor using dependency injection
  - More details can be read in @rules\primary-constructor.mdc
- After defining `Module{Name}Option`, to use the module options, simply inject `IOptions<TModuleOption>` or `IOptionsSnapshot<TModuleOption>` for usage.

## Development Phase & Optimization Policy

- **Development Stage**: This project is in internal development and has not been released. Backward compatibility is not a concern unless explicitly instructed otherwise.
- **Optimization First**: Always prioritize the most optimal design and implementation approaches. Proactively identify and propose refactoring or redesign opportunities when improvements are possible.
- **Testing Policy**: Unit testing is not required during this phase. Do not include testing-related tasks in planning or implementation unless explicitly requested.
