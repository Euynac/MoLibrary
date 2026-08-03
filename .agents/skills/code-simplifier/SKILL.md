---
name: code-simplifier
description: Simplify recently modified or explicitly selected code for clarity and maintainability while preserving the active task's intended behavior. Use after implementation or when asked to clean up, simplify, or review code for unnecessary complexity. Default to a narrow, coherent scope; do not use for broad architecture redesign, module-boundary changes, or breaking API redesign.
---

# Code Simplifier

Reduce cognitive load and unnecessary concepts without changing behavior beyond the active task.

## Workflow

1. Respect the request mode. For review or report requests, inspect and report only. For cleanup or refactor requests, implement the requested simplifications.
2. Use the scope named by the user or active task. Otherwise, use the current diff to identify recently modified code while treating pre-existing or unrelated worktree changes as out of scope. If no clear target remains, establish one before acting; do not roam through unrelated code.
3. Read callers, tests, contracts, and directly coupled code only as needed to understand behavior, invariants, side effects, concurrency, and repository conventions.
4. Edit the narrowest coherent responsibility. Expand beyond changed code only to preserve correctness, compilation, or a directly related invariant, or to complete a concrete simplification within that responsibility that reduces net complexity.
5. Prefer established repository patterns and direct implementations over new concepts.
6. Run the most relevant repository-required verification and review the final diff for behavior drift and unrelated edits.
7. Stop when the target is locally clear and further changes would mainly express style or anticipate hypothetical requirements.

## Simplification Priorities

Apply these choices in order when they improve clarity:

- Remove provably redundant code and abstractions.
- Reduce unnecessary nesting, temporary state, indirection, and semantic duplication.
- Make control flow, names, and ownership explicit.
- Reuse an existing project pattern or helper when it is clearer than a local implementation.
- Extract or move behavior only when it names a meaningful operation, owns a real invariant, or lowers total coupling.
- Introduce a new abstraction only for a current, demonstrated need and only when it removes more concepts, branching, duplication, or volatility than it adds.

Do not add configurability, extension points, wrappers, factories, providers, events, or generic frameworks for hypothetical future use.

## Boundaries

- Preserve the active task's intended behavior and avoid additional changes to public APIs, return values, exceptions, cancellation, ordering, side effects, and concurrency semantics.
- Keep external, expensive, or fallible work outside critical sections. Encapsulate atomic state transitions and invariant enforcement behind the state-owning abstraction when that makes ownership clearer.
- Do not move I/O or collaborator dependencies onto a model merely because a method assigns many of that model's fields. Rich models are a tool, not a goal; DTOs, configuration, snapshots, and transport models may remain data-focused.
- Do not split files or methods solely to reduce length, collapse distinct responsibilities to reduce line count, or replace clear code with clever one-liners.
- Remove comments that repeat the code, but retain comments that explain non-obvious intent, invariants, concurrency, or external constraints.
- Treat this bounded workflow as the applicable refinement mode when the skill is active. General repository encouragement to refactor broadly does not expand the scope unless the user explicitly requests broader redesign.

## Output

Keep reporting proportional to the work and any active collaboration instructions. Report the meaningful simplification, behavior-sensitive decisions, and verification performed. Mention broader risks separately without implementing them outside the requested scope.
