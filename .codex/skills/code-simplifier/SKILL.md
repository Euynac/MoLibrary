---
name: code-simplifier
description: Improve the quality of C#/.NET code without changing behavior. Use when the user asks to simplify or refactor code, review current git changes, clean up AI-generated code, rework a specific class or method, use git diff as the starting point for broader related refactoring, increase cohesion, move behavior onto the object that owns the data/state, or make code more readable and maintainable while preserving exact functionality.
---

# Code Simplifier

## Overview

Refine C#/.NET code so it is clearer, more cohesive, and easier to maintain without changing behavior. Start from the user-specified scope when provided; otherwise use recent changes as the entry point, inspect the surrounding area and related collaborators, form a refactoring plan, then simplify toward explicit, object-centered design.

## Workflow

1. Determine the target scope from the user request. Use the explicitly named class, method, file, or module when provided.
2. If the scope comes from `git diff`, treat the diff as the entry point, not the boundary. Inspect the surrounding types, related collaborators, and nearby responsibilities.
3. Unless the user explicitly limits scope, look beyond the changed lines to find additional refactoring opportunities in the same area.
4. Before editing, analyze the design as a whole: identify responsibility boundaries, state ownership, behavior that can move onto the owning object, and the safest refactoring order.
5. Form a concise refactoring plan first, then implement the refactor.
6. Preserve exact behavior, outputs, exceptions, and side effects.
7. Prefer the simplest design that makes responsibilities obvious.
8. Apply local repository instructions before generic .NET heuristics.

## Simplification Priorities

- Prefer rich models over anemic models.
- Keep behavior on the type that owns the data or state.
- Let services orchestrate collaborators instead of directly mutating another object's internals.
- Use `Move Method`, `Encapsulation`, and `Tell, Don't Ask` when a helper mainly reads and rewrites one object's fields.
- Reduce unnecessary nesting, temporary state, duplication, and one-off abstractions.
- Choose clarity over brevity; avoid dense one-liners or clever compacting that hurts readability.
- Keep names explicit and intention-revealing.
- Remove comments that only restate obvious code.

## Guardrails

- Never change observable behavior just to make the code look cleaner.
- Preserve existing API shape unless the user explicitly asks for a breaking redesign.
- Keep refactors debuggable; do not collapse too many concerns into one method or type.
- Do not jump straight into edits. Understand the area first, decide on the target design, and refactor in a deliberate sequence.
- Follow repository-specific guidance from `AGENTS.md`, nested `AGENTS.md`, `CLAUDE.md`, or equivalent local instructions when present.

## Example Pattern

When a method mostly updates one object's internal state, that is a signal that the behavior may belong on the object itself.

```csharp
private static void CompleteSync(
    GitRepositoryRuntimeState state,
    Repository repository,
    string resolvedLocalPath,
    string targetBranchName,
    GitSyncTrigger trigger,
    bool hasChanges,
    bool wasDirty,
    string? refreshMessage)
{
    lock (state.SyncRoot)
    {
        state.State = GitRepositorySyncState.Ready;
        state.IsAvailable = true;
        state.IsDirty = repository.RetrieveStatus().IsDirty;
        state.CurrentCommit = repository.Head.Tip?.Sha;
        state.LastSyncAtUtc = DateTimeOffset.UtcNow;
        state.ResolvedBranch = targetBranchName;
        state.LastSyncMessage = BuildSyncMessage(trigger, hasChanges, wasDirty, refreshMessage);
        state.LastError = null;
        state.ProgressStage = null;
        state.ProgressPercent = null;
        state.WorkingDirectorySizeBytes = TryGetWorkingDirectorySizeBytes(
            resolvedLocalPath,
            state.WorkingDirectorySizeBytes);
    }
}
```

This is a good candidate for moving behavior onto `GitRepositoryRuntimeState`, because the method's main responsibility is a state transition of that object. The service can then tell the object to complete synchronization instead of manually assigning many fields. That usually increases cohesion, reduces procedural mutation, and makes the transition easier to reuse and test.

## Output Expectations

When reporting refinements, describe only meaningful structural changes, call out behavior-preserving decisions, and mention any assumption that limited the refactor.
