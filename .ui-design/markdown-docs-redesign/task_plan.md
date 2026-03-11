# Task Plan: Markdown Document Search

## Goal
Implement toolbar-driven markdown document search with a MudBlazor dialog, cross-knowledge-base scope toggle, configurable matching algorithm, and in-document hit locating/highlighting.

## Current Phase
Phase 5

## Phases

### Phase 1: Requirements & Discovery
- [x] Understand user intent
- [x] Identify constraints
- [x] Document in findings.md
- **Status:** complete

### Phase 2: Planning & Structure
- [x] Define approach
- [x] Create project structure
- **Status:** complete

### Phase 3: Implementation
- [x] Execute the plan
- [x] Write to files before executing
- **Status:** complete

### Phase 4: Testing & Verification
- [x] Verify requirements met
- [x] Document test results
- **Status:** complete

### Phase 5: Delivery
- [x] Review outputs
- [x] Deliver to user
- **Status:** complete

## Decisions Made
| Decision | Rationale |
|----------|-----------|
| Keep full-text search in a dedicated infrastructure service instead of extending tree search | Avoid overloading `IMoMarkdownService` responsibilities and keep UI wrapper thin |
| Search defaults to current knowledge base with a dialog toggle for all knowledge bases | Matches the approved UX and reduces noise for large doc sets |
| Support two algorithms via an abstraction and configure the active one in `ModuleMarkdownUIOption` | Lets UI modules choose strict or loose fuzzy behavior without new UI complexity |
| Persist hit highlighting only in page session, not in URL | Matches approved scope and avoids expanding `MarkdownViewerLocation` |
| Keep one best hit per document in the returned result set | Matches the dialog prototype and keeps result counts document-oriented |

## Errors Encountered
| Error | Resolution |
|-------|------------|
