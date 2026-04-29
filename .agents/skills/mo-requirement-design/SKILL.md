---
name: mo-requirement-design
description: This skill should be used when the user asks to "brainstorm requirement", "refine requirement", "new requirement", "design requirement", "enter design phase", "create design", "design module", "requirement analysis", "需求分析", "需求完善", "进入设计阶段", "设计阶段", or needs guidance on requirement gathering, iterative requirement refinement, or architectural design planning for Monica modules.
version: 1.0.0
---

# Monica Requirement & Design Workflow

This skill provides a structured two-phase workflow for requirement gathering and architectural design in the Monica framework.

## Overview

| Phase | Trigger | Output |
|-------|---------|--------|
| Requirements | User provides a brief requirement idea | `.pending/{NNN}-{name}/requirements.md` |
| Design | User requests design for an existing requirement | `.pending/{NNN}-{name}/design.md` |

## Phase 1: Requirements Gathering

### Initiation

When the user provides a brief requirement or idea:

1. Create the `.pending/` directory at project root if it does not exist.
2. Determine the next folder number by finding the highest existing number in `.pending/` and incrementing by one. If no folders exist, start at `001`. Format as three-digit zero-padded number (e.g., `001`, `002`, `013`).
3. Derive a brief kebab-case name from the requirement (e.g., `user-auth`, `distributed-cache`, `markdown-export`).
4. Create the subfolder: `.pending/{NNN}-{brief-name}/`
5. Create `requirements.md` using the template from `references/requirements-template.md`.
6. Populate the Overview section with the user's initial description.

### Iterative Refinement

Conduct requirement refinement through iterative questioning. Follow these principles:

**Progressive disclosure**: Ask 1-3 focused questions per iteration. Do not overwhelm the user with all questions at once. Prioritize questions by impact.

**Question categories** (use in approximate order):

1. **Scope clarification**: What is in scope vs. out of scope? What problem does this solve?
2. **User stories**: Who are the consumers of this feature? What are the primary use cases?
3. **Integration points**: Which existing Monica modules does this interact with? Does it depend on or extend existing modules?
4. **Constraints**: Performance requirements? Compatibility requirements? Technology constraints?
5. **Edge cases**: What happens when things go wrong? What are the boundary conditions?
6. **Non-goals**: What should this explicitly NOT do? What is deferred to future work?

**Industry best practices to apply**:

- Think about scalability, extensibility, and separation of concerns.
- Consider similar solutions in well-known frameworks or libraries.
- Identify potential architectural patterns (e.g., CQRS, event sourcing, mediator, repository).
- Suggest improvements or alternatives the user may not have considered.

**After each user response**:
- Update `requirements.md` with the refined information.
- Summarize what was captured.
- Ask the next round of questions (if refinement is not complete).

### Completion

Stop the requirements phase when the user signals completion with phrases such as:
- "可以了", "够了", "就这样", "完成", "OK"
- "done", "looks good", "that's it", "enough", "complete"

Upon completion:
- Perform a final update to `requirements.md` ensuring all sections are populated.
- Present a concise summary of the complete requirements.
- Inform the user they can enter the design phase later by invoking this skill with the requirement name.

## Phase 2: Architectural Design

### Initiation

When the user requests design for an existing requirement:

1. Identify the requirement folder in `.pending/` by name or number. If no matching folder is found, list available requirement folders and ask the user to clarify, or offer to start a new requirements gathering phase.
2. Read the existing `requirements.md` from that folder.
3. Analyze the current project structure:
   - Scan existing Monica modules (`Monica.*/`) to understand the module landscape.
   - Identify related modules the new feature may depend on or extend.
   - Review relevant patterns from the `mo-development` skill references.

### Analysis

Before writing the design document, perform the following analysis:

1. **Module placement**: Determine whether this requires a new module (`Monica.{Name}/`), an extension to an existing module, or a cross-cutting concern.
2. **Pattern selection**: Determine which Monica patterns apply:
   - Standard infrastructure module (Module, Option, Guide, BuilderExtensions)
   - UI module (Mixed, Standalone, or Framework pattern)
   - Hosted service (MoBackgroundService or CoordinatedLeaderService)
   - Service layer (UI with `Res<T>` or infrastructure with exceptions)
3. **Dependency mapping**: Identify which existing modules this depends on and which modules may depend on it.
4. **Interface design**: Define the public abstractions (interfaces, base classes) that consumers will use.
5. **Directory structure**: Plan the file and folder layout following Monica conventions.

### Design Document Creation

Create `design.md` in the requirement folder using the template from `references/design-template.md`. Populate all sections based on the analysis.

The design document must:
- Follow Monica module conventions (refer to `mo-development` skill for patterns).
- Use primary constructors for DI classes.
- Separate UI services (`Res<T>`) from infrastructure services (exceptions) per project policy.
- Include concrete interface definitions with XML doc comments in English.
- Specify the complete directory structure with file purposes.
- List implementation steps in dependency order.

### Design Review

After creating the design document:
- Present a summary of the architectural decisions.
- Highlight any trade-offs or alternatives considered.
- Ask the user if any adjustments are needed.
- Update `design.md` based on feedback.

## Project Context

### Monica Module Landscape

To understand the current module landscape, scan directories matching `Monica.*/` at the project root. Group them by category (Core, Infrastructure, Communication, Domain, Scheduling, UI, Specialized, Code Generation) based on their names and contents.

### Related Skills

- **mo-development** — Module patterns, Res type, hosted services
- **mo-ui-development** — UI module structure, Blazor components, MudBlazor

## Additional Resources

### Reference Files

- **`references/requirements-template.md`** — Template for requirements.md with all standard sections
- **`references/design-template.md`** — Template for design.md with architecture and implementation sections
