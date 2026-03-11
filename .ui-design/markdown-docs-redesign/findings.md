# Findings & Decisions

## Requirements
- Toolbar search in `DocumentContentViewer` must open a dialog, not use the sidebar file tree filter.
- Search must cover markdown content across the current knowledge base by default, with an opt-in cross-knowledge-base switch.
- Matching must support two strategies: keyword fuzzy and loose subsequence.
- Search result selection must open the document, navigate near the hit, and highlight the matched content in the current session.

## Research Findings
- `UIMarkdownPage` already owns page-level navigation state (`group`, `document`, `hash`) and imports `markdown-layout.js`, so it is the best coordinator for dialog result handling and post-render JS location.
- `DocumentContentViewer` currently renders a disabled toolbar search trigger; it already exposes toolbar callbacks and can be extended with an open-search event.
- `IMoMarkdownService` provides groups, trees, document lookup, and content reads, but no full-text search capability.
- `MarkdownViewerLocation` only persists group/document/hash. Expanding it is unnecessary for the approved session-only highlight behavior.
- `MoMarkdownHeadingParser` can be reused for heading IDs, but search indexing still needs its own markdown-to-plain-text section projection.
- MudBlazor `MudTextField` already supports `DebounceInterval` and `OnDebounceIntervalElapsed`, which is a cleaner fit than a custom debounce timer in the dialog.

## Technical Decisions
| Decision | Rationale |
|----------|-----------|
| Build a lazy per-group search index keyed by document fingerprint (`RelativePath`, `LastModifiedUtc`, `FileSize`) | Keeps repeated dialog searches fast while staying in sync with refreshed markdown groups |
| Represent search hits with a locator containing anchor, matched text, and prefix/suffix context | Gives JS enough information to find the correct DOM range and wrap it with `<mark>` |
| Reuse `markdown-layout.js` for search-hit scrolling/highlighting | Centralizes markdown viewer DOM behavior in one module |
| Deduplicate section matches down to one highest-scoring result per document | Keeps the dialog aligned with a document-search UX instead of showing chunk-style duplicates |

## Issues Encountered
| Issue | Resolution |
|-------|------------|
| `DocumentSearchDialog` initially missed `ModuleMarkdownUIOption` and `Res<T>.IsFailed` namespaces | Added the missing `Monica.Modules` and `Monica.Tool.MoResponse` imports, then rebuilt successfully |

## Resources
- `Monica.Markdown/Pages/UIMarkdownPage.razor`
- `Monica.Markdown/UIMarkdown/Components/DocumentContentViewer.razor`
- `Monica.Markdown/UIMarkdown/Services/MarkdownUIService.cs`
- `Monica.Markdown/Services/MoMarkdownService.cs`
- `Monica.Markdown/Services/MarkdownDocumentSearchService.cs`
- `Monica.Markdown/wwwroot/js/markdown-layout.js`
