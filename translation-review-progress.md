# Monica translation review log

- Branch: `kou/review-comment-translation-quality`
- Base branch: `dev`
- Goal: Review and improve the recent repository-wide translation from Chinese comments/annotations to English, preserving or improving original semantic context.
- Status: ongoing

## Plan

1. Identify the latest translation-focused change set.
2. Review changed files in manageable batches.
3. Correct low-quality / misleading / context-losing translations.
4. Record progress, decisions, and notable fixes here.

## Progress

- [x] Synced latest remote source.
- [x] Created dedicated review branch.
- [x] Located translation commit/range.
- [x] Completed batch 1 review (`Monica.AI/*`).
- [x] Completed batch 2 review (`Monica.AutoModel/*`, `Monica.Configuration*`, `Monica.DataChannel/*`).
- [x] Completed batch 3 review (`Monica.Core/*`, `Monica.Dapr/*`, `Monica.DependencyInjection/*`, `Monica.EventBus/*`).
- [x] Completed batch 4 review (`Monica.DomainDrivenDesign/*`).
- [ ] Completed full repository translation review.

## Findings

- Translation commit under review: `3238f77a` (`refactor: translate Chinese comments to English across codebase`).
- Early sampling found mistranslations that need correction, for example:
  - `助手消息` -> `helper messages` (corrected to `assistant messages`)
  - `工具消息` -> `tool news` (corrected to `tool message`)
  - `输入 Token 数量` -> `Enter the number of Tokens` (corrected to `Number of input tokens`)
- Batch 1 focus: `Monica.AI/*` files with obvious machine-translation artifacts and unnatural API/XML-doc phrasing.
- Review strategy: prioritize files with obviously weak machine-translation patterns first, then continue module-by-module until the entire commit range is covered.
- Batch 2 observations:
  - Some XML docs were grammatically correct but semantically awkward in framework/API contexts; these are being rewritten for idiomatic .NET documentation style rather than left as literal translations.
  - A few untranslated Chinese strings still remained inside developer-facing attributes/messages (for example `Obsolete` text), which are being normalized to English on the review branch.
  - Several comments used unnatural wording such as "Configure the client API provider..." or "registration center"; these are being corrected to clearer domain language such as "client-side configuration API provider" and "registration-state manager".
- AutoModel follow-up observations:
  - Terminology such as "activation name", "field selection", "snapshot", and "fuzzy match" needed another consistency pass so the module reads like native English instead of literal machine translation.
  - The remaining AutoModel `Obsolete` attribute messages were still Chinese and have been normalized to `Not implemented yet.`.
  - PR `#4` was checked on 2026-03-28; its only recorded review feedback was the earlier Monica.AI wording feedback already addressed in `1f7f556c`, with no new actionable comments for the remaining modules.
- Batch 3 focus:
  - Next review batch is the smaller infrastructure slice: `Monica.Core/*`, `Monica.Dapr/*`, `Monica.DependencyInjection/*`, and `Monica.EventBus/*`.
  - Early review findings in this batch include awkward “Using X as provider” summaries, literal TODO/comment translations, and a few leftover Chinese developer-facing strings in endpoint metadata, logs, and exception text.
- Batch 3 observations:
  - Dapr module docs needed idiomatic builder/facade wording such as “Registers Dapr as the ... provider” instead of literal “Using Dapr as ...”.
  - A few developer-facing strings had been missed by the original translation commit, including Dapr endpoint metadata, service-invocation error text, DI auto-registration logs, and one core module initialization exception.
  - EventBus files in this batch mostly needed small cleanup passes for XML-doc polish and comment readability rather than semantic rewrites.
  - `git diff --check` passed after the batch; `dotnet` and `wslpath` are still unavailable in the environment, so build verification remains unavailable.
- Batch 4 observations:
  - `Monica.DomainDrivenDesign/*` still contained a mix of literal builder-summary translations and entirely untranslated developer-facing strings in CRUD response templates, validation errors, and auto-controller registration logs.
  - The `OverrideServiceAttribute` and related auto-controller comments needed simplification to describe the contract-generation behavior directly instead of mirroring the original Chinese structure.
  - A repository-wide Chinese-text scan for `Monica.DomainDrivenDesign/*` returned clean after the batch, and `git diff --check` still passed.
