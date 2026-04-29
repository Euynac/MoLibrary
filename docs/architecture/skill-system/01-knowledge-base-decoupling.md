# Doc 01 — Knowledge Base UI / Module Decoupling from RAG

> **Status.** Design proposal. Phase A of the implementation roadmap.
> **Audience.** UI module owner of `Monica.AI.UI/UIRAG`; `Monica.AI` module owner.
> **Self-contained.** Implementer of this phase does not need to read Docs 02–04.
> **Last revised.** 2026-04-29.

## 0. Why this doc exists

Today, the "knowledge base" tool in the chat page (`KnowledgeBaseSelector` in `ChatInputArea.razor`) requires a fully-configured RAG pipeline to function. A user who wants nothing more than to list KBs, browse a document tree, and read a document's source text still has to configure embedding providers, vector dimensions, and chunking. That is over-coupled — Knowledge Base is a *data store*, RAG is a *pipeline over that data store*, and the current code conflates the two.

This doc specifies the split: a new Knowledge Base feature inside `Monica.AI` with its own Facade and lookup-only Skill, paired with a new `Monica.AI.UI/UIKnowledgeBase` UI module that owns the KB management surface. RAG retains the chunking, embedding, indexing, and semantic-search pipeline; KB owns CRUD, document-inventory inspection, and the lookup-only Skill that works without RAG configured.

The companion docs (02 specifies the Skill base classes, 03 generalizes the Facade-to-Skill projection) reuse the new `KnowledgeBaseFacade` as a worked example. Doc 01 is independent of those — Phase A can land before Phase B if it ships first.

## 1. Current coupling — concrete points

Cited by file path with line context:

| File | Coupling |
|---|---|
| `Monica.AI.UI/UIRAG/Pages/RAGManagePage.razor` | Single page mixes KB CRUD + chunker config + indexing + queue management. |
| `Monica.AI.UI/UIRAG/Components/RAGManageKnowledgeBasePanel.razor` | KB list panel lives inside UIRAG. |
| `Monica.AI.UI/UIRAG/Components/CreateKnowledgeBaseDialog.razor` | KB create/edit form lives inside UIRAG. |
| `Monica.AI.UI/UIRAG/Components/KnowledgeBaseSelector.razor` | KB selection chip — lives in UIRAG but consumed by `Monica.AI.UI/UIChat/Components/ChatInputArea.razor` (cross-module dep). |
| `Monica.AI.UI/UIRAG/State/RAGManagePageState.KnowledgeBase.cs` | KB CRUD orchestration; injects `RAGFacade` and `EmbeddingModelFacade`. |
| `Monica.AI.UI/UIChat/State/ChatPageState.cs` | Chat state injects `RAGFacade` only to load the KB list — premature coupling. |
| `Monica.AI.UI/UIRAG/Modules/ModuleRAGUI.cs` | Hard `DependsOnModule<ModuleRAGGuide>` makes KB management transitively force RAG. |
| `Monica.AI/RAG/Facades/RAGFacade.cs` | Carries KB CRUD methods that should not live there: `CreateKnowledgeBaseAsync`, `UpdateKnowledgeBaseAsync`, `DeleteKnowledgeBaseAsync`, `GetKnowledgeBasesAsync`. |
| `Monica.AI/RAG/Models/KnowledgeBase.cs` | Peer concept currently nested under the RAG namespace. |
| `Monica.AI/RAG/Tools/KnowledgeSearchToolProvider.cs` | The agent-facing KB tool is fused with RAG semantic search. There is no lookup-only path. |

**Net effect.** Disabling RAG today disables KB management. Loading the chat page injects `RAGFacade` even when only the KB selector is needed. The KB selector lives in UIRAG but is a UIChat dependency — both modules share a hidden import.

## 2. Target topology

### 2.1 Backend — `Monica.AI/KnowledgeBase/`

A new feature folder `KnowledgeBase/` inside `Monica.AI` (sub-feature, not standalone package — see §6 open question (a)). Layout:

```
Monica.AI/
  KnowledgeBase/
    Abstractions/
      IKnowledgeBaseLookupService.cs     // contract behind the lookup-only Skill
    Models/
      KnowledgeBase.cs                    // moved from RAG/Models/
      DocumentQueueItem.cs                // moved if currently RAG-namespaced
    Facades/
      KnowledgeBaseFacade.cs              // CRUD + inventory + status
    Services/
      KnowledgeBaseService.cs             // backing implementation
      KnowledgeBaseLookupService.cs       // implements IKnowledgeBaseLookupService
    Skills/
      KnowledgeBaseLookupSkill.cs         // MoSkill<KnowledgeBaseLookupSkill>
    Modules/
      ModuleKnowledgeBase.cs              // module + Guide + Option
```

Module key: `Monica.AI` already owns the existing `BuiltInModuleKey.AI` and `BuiltInModuleKey.RAG`. Add `BuiltInModuleKey.KnowledgeBase` (next to `RAG` in `BuiltInModuleKey.cs`). The module is registered via `Mo.AddKnowledgeBase(...)` in the standard Monica fluent style.

Rationale for keeping it inside `Monica.AI` rather than carving out `Monica.AI.KnowledgeBase` as a separate project: KB is conceptually part of the AI surface, has no consumers outside `Monica.AI`, and a separate project would invert the dependency arrow (RAG would have to reference KB) — which is correct, but the project graph already handles that internally without requiring a new `.csproj` file. See open question (a).

### 2.2 UI — `Monica.AI.UI/UIKnowledgeBase/`

A new UI feature folder mirroring the backend split. Layout:

```
Monica.AI.UI/
  UIKnowledgeBase/
    Components/
      KnowledgeBaseSelector.razor         // moved from UIRAG/Components/
      KnowledgeBaseSelector.razor.css
      CreateKnowledgeBaseDialog.razor     // moved
      EditKnowledgeBaseDialog.razor       // split from current Create dialog
      KnowledgeBaseListPanel.razor        // renamed from RAGManageKnowledgeBasePanel
    Pages/
      KnowledgeBaseManagePage.razor       // new top-level page; URL: /knowledge-base/manage
    State/
      KnowledgeBaseManagePageState.cs     // KB CRUD orchestration extracted
      KnowledgeBaseManagePageState.Documents.cs   // KB document inventory (read-only)
    Modules/
      ModuleKnowledgeBaseUI.cs            // UI module
```

Module key: add `BuiltInModuleKey.KnowledgeBaseUI` next to `RAGUI` in `BuiltInModuleKey.cs`. The new UI module depends on:

- `ModuleAIUIGuide` (for the chat-host integration that consumes `KnowledgeBaseSelector`).
- `ModuleKnowledgeBaseGuide` (backend Facade).
- *Not* `ModuleRAGUIGuide` — explicit goal of the decoupling.

`ModuleRAGUI` no longer carries the KB management page or its panel/dialog; `ModuleRAGUI` keeps the RAG-specific pages (`RAGDebugPage`, `RAGChunkersPage`, and a slimmer `RAGManagePage` focused on indexing + queue + chunking).

### 2.3 Dependency graph — before / after

```
BEFORE
                    ┌─────────────────┐
                    │   UIChat        │
                    └────────┬────────┘
                             │   (KnowledgeBaseSelector lives in UIRAG;
                             │    UIChat reaches into UIRAG components,
                             │    UIChat injects RAGFacade)
                             ▼
                    ┌─────────────────┐
                    │   UIRAG         │
                    │ (RAGManagePage  │
                    │  KB CRUD +      │
                    │  RAG pipeline)  │
                    └────────┬────────┘
                             ▼
                    ┌─────────────────┐
                    │   ModuleRAG     │
                    │  (RAGFacade has │
                    │   KB CRUD too)  │
                    └─────────────────┘

AFTER
   ┌─────────────────┐                ┌─────────────────────────┐
   │   UIChat        │                │   UIKnowledgeBase       │
   │  (consumes      │  ──────►       │  (KnowledgeBaseSelector,│
   │   KB selector)  │                │   KnowledgeBaseManage)  │
   └─────────────────┘                └────────────┬────────────┘
                                                   │
   ┌─────────────────┐                ┌────────────▼────────────┐
   │   UIRAG         │                │    ModuleKnowledgeBase  │
   │  (slimmer:      │   ─────►       │    (KnowledgeBaseFacade)│
   │   RAGManage     │                └────────────┬────────────┘
   │   indexing/     │                             │
   │   chunkers/     │   ─────►       ┌────────────▼────────────┐
   │   queue only)   │                │      ModuleAI           │
   └────────┬────────┘                └─────────────────────────┘
            │
   ┌────────▼────────┐
   │   ModuleRAG     │ ── DependsOn ─►  ModuleKnowledgeBase
   │  (slimmer:      │
   │   RAGFacade     │
   │   pipeline only)│
   └─────────────────┘
```

The new arrows: UIChat depends on UIKnowledgeBase (clean, single-purpose dep). ModuleRAG depends on ModuleKnowledgeBase (RAG composes over KB, not vice-versa). UIRAG no longer hosts KB-related components.

## 3. API classification — every existing `RAGFacade` method

For each public method on `Monica.AI/RAG/Facades/RAGFacade.cs`, the table assigns one of two dispositions: **`MOVE_TO_KB_FACADE`** (the method is removed from `RAGFacade` and added to `KnowledgeBaseFacade`, possibly with light renaming) or **`KEEP_ON_RAG`** (stays — purely RAG-pipeline concern). Per the Monica development-stage policy in `CLAUDE.md` ("Backward compatibility is not a concern"), the move is a clean breaking change in a single PR — no `[Obsolete]` shim period, no forwarding wrappers. Callers are migrated atomically in the same PR.

| Current method | Disposition | Target / Rationale |
|---|---|---|
| `GetKnowledgeBasesAsync()` | `MOVE_TO_KB_FACADE` | → `KnowledgeBaseFacade.GetAllAsync()`. Pure KB inventory query. |
| `CreateKnowledgeBaseAsync(id, name, description)` | `MOVE_TO_KB_FACADE` | → `KnowledgeBaseFacade.CreateAsync(id, name, description)`. Pure KB CRUD. |
| `UpdateKnowledgeBaseAsync(id, name, description)` | `MOVE_TO_KB_FACADE` | → `KnowledgeBaseFacade.UpdateAsync(id, name, description)`. Pure KB CRUD. |
| `DeleteKnowledgeBaseAsync(id)` | `MOVE_TO_KB_FACADE` | → `KnowledgeBaseFacade.DeleteAsync(id)`. KB CRUD; impl conditionally clears vectors when `RAGService` is registered. |
| `GetDocumentQueueAsync(kbId)` | `MOVE_TO_KB_FACADE` | → `KnowledgeBaseFacade.GetDocumentInventoryAsync(kbId)`. The "queue" is really the KB's document inventory; renamed for clarity. |
| `RemoveDocumentAsync(kbId, documentId)` | `MOVE_TO_KB_FACADE` | → `KnowledgeBaseFacade.RemoveDocumentAsync(kbId, documentId)`. Removes from inventory; impl conditionally clears vectors. |
| `ClearKnowledgeBaseDocumentsAsync(kbId)` | `MOVE_TO_KB_FACADE` | → `KnowledgeBaseFacade.ClearDocumentsAsync(kbId)`. |
| `GetKnowledgeBaseVectorValidationAsync(kbId)` | `KEEP_ON_RAG` | Vector validation is purely a RAG concern. |
| `SearchAsync(query, kbIds, topK, embeddingOverride)` | `KEEP_ON_RAG` | Semantic search — pure RAG pipeline. |
| `GetDocumentChunksAsync(kbId, documentId)` | `KEEP_ON_RAG` | Chunk viewer; chunks exist only when RAG has indexed. |
| `IndexMarkdownGroupAsync(kbId, groupKey, progress)` | `KEEP_ON_RAG` | Indexing into vector store. |
| `UploadAndIndexDocumentAsync(kbId, fileName, content)` | `KEEP_ON_RAG` | Indexing into vector store. |
| `AddDocumentsToQueueAsync(kbId, documentIds, sourceGroupKey)` | `KEEP_ON_RAG` | Queues docs for the *RAG indexing pipeline*. KB inventory writes happen as a side effect inside the RAG service. Doc declares: callers wanting pure inventory writes go through KB Facade; RAG owns the indexing-side queue. |
| `ClearDocumentQueueAsync(kbId)` | `KEEP_ON_RAG` | Clears the *pending-indexing* queue (pipeline state), distinct from inventory. |
| `ReindexDocumentAsync(kbId, documentId, progress)` | `KEEP_ON_RAG` | Reindexing — pure pipeline. |
| `ReindexKnowledgeBaseAsync(kbId)` | `KEEP_ON_RAG` | Reindexing — pure pipeline. |
| `StartDocumentIndexingAsync(...)` | `KEEP_ON_RAG` | Pipeline batch op. |
| `StartBatchIndexingAsync(...)` | `KEEP_ON_RAG` | Pipeline batch op. |
| `CancelBatchIndexing(kbId)` | `KEEP_ON_RAG` | Pipeline batch op. |
| `CancelBatchIndexingAsync(kbId, ct)` | `KEEP_ON_RAG` | Pipeline batch op. |
| `IsBatchIndexingActive(kbId)` | `KEEP_ON_RAG` | Pipeline state. |
| `GetMarkdownGroupsAsync()` | `KEEP_ON_RAG` | Returns markdown source groups available for indexing — pipeline-side concern. |
| `GetAvailableDocumentsAsync(groupKey)` | `KEEP_ON_RAG` | Returns markdown documents available for indexing. |

`Monica.AI/RAG/Facades/EmbeddingModelFacade.cs` stays in RAG entirely — every method on it is about binding an embedding model to a KB, which is a RAG-pipeline configuration concern. The KB simply has a `EmbeddingProviderId` / `EmbeddingModelName` field that the RAG pipeline reads. The `KnowledgeBaseFacade.UpdateAsync` does *not* touch those fields; only `EmbeddingModelFacade.SetKnowledgeBaseEmbeddingModelAsync` does.

`Monica.AI/RAG/Facades/ChunkerFacade.cs` stays in RAG entirely.

### 3.1 Method signatures on `KnowledgeBaseFacade`

```csharp
namespace Monica.AI.KnowledgeBase.Facades;

/// <summary>
/// Host-facing facade for knowledge-base CRUD and document-inventory operations.
/// Works without RAG configured — methods that touch vectors do so conditionally,
/// only when <see cref="RAGService"/> is present in the service container.
/// </summary>
public class KnowledgeBaseFacade(
    IServiceProvider serviceProvider,
    IKnowledgeBaseStore store,
    ILogger<KnowledgeBaseFacade> logger)
{
    /// <summary>Returns all knowledge bases visible to the current user.</summary>
    public Task<Res<IReadOnlyList<KnowledgeBase>>> GetAllAsync();

    /// <summary>Returns one knowledge base by id, or Fail if not found.</summary>
    public Task<Res<KnowledgeBase>> GetByIdAsync(string id);

    /// <summary>Creates a new knowledge base.</summary>
    public Task<Res<KnowledgeBase>> CreateAsync(
        string id,
        string name,
        string? description = null);

    /// <summary>Updates name and description on an existing knowledge base.</summary>
    public Task<Res<KnowledgeBase>> UpdateAsync(
        string id,
        string name,
        string? description = null);

    /// <summary>
    /// Deletes a knowledge base. If RAGService is registered, also clears the vector
    /// index for the KB. Otherwise removes only the inventory record.
    /// </summary>
    public Task<Res> DeleteAsync(string id);

    /// <summary>
    /// Returns the list of documents recorded in the KB's inventory.
    /// Independent of indexing state — the inventory exists with or without RAG.
    /// </summary>
    public Task<Res<IReadOnlyList<DocumentQueueItem>>> GetDocumentInventoryAsync(string kbId);

    /// <summary>
    /// Removes one document from a KB's inventory. If RAGService is registered,
    /// also removes the document's vectors.
    /// </summary>
    public Task<Res> RemoveDocumentAsync(string kbId, string documentId);

    /// <summary>
    /// Clears all documents from a KB's inventory. If RAGService is registered,
    /// also clears the KB's vector index.
    /// </summary>
    public Task<Res<int>> ClearDocumentsAsync(string kbId);
}
```

Internal `IServiceProvider`-based resolution of `RAGService?` (nullable) keeps RAG strictly optional. When RAG isn't loaded, vector-related operations are no-ops and the Facade still works.

### 3.2 Atomic migration on `RAGFacade`

The seven `MOVE_TO_KB_FACADE` methods are **deleted** from `RAGFacade` in the same PR that adds `KnowledgeBaseFacade`. Callers are migrated atomically — `ChatPageState`, `RAGManagePageState.KnowledgeBase.cs`, and any other in-tree consumer flip to `KnowledgeBaseFacade` references in the same commit.

There is no shim period, no `[Obsolete]` forwarding, no grace release. Per `CLAUDE.md`'s development-stage policy, breaking changes are preferred over migration ceremony when the consumer set is bounded and known. The PR's diff is reviewable as a single coherent breaking change.

## 4. The lookup-only Skill

### 4.1 Contract

```csharp
namespace Monica.AI.KnowledgeBase.Abstractions;

/// <summary>
/// Lookup-only operations over knowledge bases. Works without a RAG pipeline
/// configured. Provides KB listing, document tree browsing, and source content
/// reads — but no semantic search or chunking.
/// </summary>
public interface IKnowledgeBaseLookupService
{
    Task<IReadOnlyList<KnowledgeBaseSummary>> ListAsync(
        CancellationToken ct = default);

    Task<KnowledgeBaseSummary?> GetSummaryAsync(
        string kbId,
        CancellationToken ct = default);

    Task<IReadOnlyList<KnowledgeDocumentSummary>> BrowseDocumentsAsync(
        string kbId,
        string? directoryPath = null,
        int maxResults = 50,
        CancellationToken ct = default);

    Task<KnowledgeDocumentTreeNode?> GetDocumentTreeAsync(
        string kbId,
        string? directoryPath = null,
        int maxDepth = 3,
        CancellationToken ct = default);

    Task<KnowledgeDocumentContent?> GetDocumentContentAsync(
        string kbId,
        string documentId,
        int maxCharacters = 8000,
        int startCharacterIndex = 0,
        CancellationToken ct = default);
}
```

`KnowledgeBaseSummary`, `KnowledgeDocumentSummary`, `KnowledgeDocumentTreeNode`, and `KnowledgeDocumentContent` are simple records in `Monica.AI/KnowledgeBase/Models/`.

### 4.2 The Skill

`KnowledgeBaseLookupSkill` lives at `Monica.AI/KnowledgeBase/Skills/KnowledgeBaseLookupSkill.cs`. It inherits `MoSkill<KnowledgeBaseLookupSkill>` (specified in Doc 02). Skeleton:

```csharp
namespace Monica.AI.KnowledgeBase.Skills;

public sealed class KnowledgeBaseLookupSkill(
    IKnowledgeBaseLookupService lookup,
    ILogger<KnowledgeBaseLookupSkill> logger)
    : MoSkill<KnowledgeBaseLookupSkill>
{
    public override AgentSkillFrontmatter Frontmatter { get; } = new(
        name: "knowledge-base-lookup",
        description: "List, browse, and read knowledge-base documents " +
                     "without semantic search.");

    protected override string Instructions =>
        "Use these scripts when the user wants to discover what knowledge bases " +
        "exist, see what documents a KB contains, navigate a document hierarchy, " +
        "or read raw document text. This skill does NOT perform semantic search " +
        "— for that, look for the 'rag-knowledge' skill (only available when RAG " +
        "is configured). Cite sources by document title and source link.";

    public override IEnumerable<ModuleKey> RequiredModules => [BuiltInModuleKey.KnowledgeBase];

    [MoAITool(
        Name = "list-knowledge-bases",
        Description =
            "List all knowledge bases the current user can access. Returns id, " +
            "display name, description, and document count for each.")]
    public async Task<string> ListAsync(CancellationToken ct)
    {
        var result = await lookup.ListAsync(ct);
        return JsonSerializer.Serialize(result, _toolJsonOptions);
    }

    [MoAITool(
        Name = "browse-knowledge-documents",
        Description = "List documents in a knowledge base, optionally filtered to a directory.")]
    public async Task<string> BrowseDocumentsAsync(
        [MoAITool(Description = "Knowledge base id.")]
        string kbId,
        [MoAITool(Description = "Optional directory path filter.")]
        string? directoryPath = null,
        [MoAITool(Description = "Max documents to return; default 50.")]
        int maxResults = 50,
        CancellationToken ct = default)
    {
        var result = await lookup.BrowseDocumentsAsync(kbId, directoryPath, maxResults, ct);
        return JsonSerializer.Serialize(result, _toolJsonOptions);
    }

    [MoAITool(
        Name = "browse-knowledge-document-tree",
        Description =
            "Browse the directory tree of a knowledge base. Useful when you only " +
            "know part of a path.")]
    public async Task<string> GetDocumentTreeAsync(
        [MoAITool(Description = "Knowledge base id.")] string kbId,
        [MoAITool(Description = "Optional directory path to root the tree at.")]
        string? directoryPath = null,
        [MoAITool(Description = "Max tree depth; default 3.")]
        int maxDepth = 3,
        CancellationToken ct = default)
    {
        var result = await lookup.GetDocumentTreeAsync(kbId, directoryPath, maxDepth, ct);
        return JsonSerializer.Serialize(result, _toolJsonOptions);
    }

    [MoAITool(
        Name = "get-knowledge-document-content",
        Description =
            "Read a specific document's source content by id. Use the start " +
            "character index and max characters to paginate.")]
    public async Task<string> GetDocumentContentAsync(
        [MoAITool(Description = "Knowledge base id.")] string kbId,
        [MoAITool(Description = "Document id, returned by browse / tree scripts.")]
        string documentId,
        [MoAITool(Description = "Max characters to return; default 8000.")]
        int maxCharacters = 8000,
        [MoAITool(Description = "Continue reading from this character index.")]
        int startCharacterIndex = 0,
        CancellationToken ct = default)
    {
        var result = await lookup.GetDocumentContentAsync(
            kbId, documentId, maxCharacters, startCharacterIndex, ct);
        return JsonSerializer.Serialize(result, _toolJsonOptions);
    }

    private static readonly JsonSerializerOptions _toolJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
```

Note that the Skill takes its dependencies through the primary constructor (Monica's standard pattern); `IKnowledgeBaseLookupService` is registered by `ModuleKnowledgeBase` so the Skill resolves cleanly. No per-session knowledge-base ID list is needed — lookup operates on the whole KB inventory and the agent specifies the `kbId` per script call.

### 4.3 Coexistence with the existing RAG-search Skill

After Phase B (Doc 02) lands, `Monica.AI/RAG/Skills/RAGKnowledgeSkill.cs` is the migrated successor of `KnowledgeSearchToolProvider`. It carries `RequiredModules => [BuiltInModuleKey.RAG]` so it surfaces only when RAG is loaded. Its scripts include `search-knowledge-base` (the semantic search). When both the lookup-only Skill and the RAG-search Skill are active in the same session, the agent sees two distinct entries in the system prompt — `knowledge-base-lookup` and `rag-knowledge` — and chooses based on the user's intent.

The agent-facing instructions on each Skill explicitly cross-reference the other so the agent knows when to switch:

- `knowledge-base-lookup` instructions say "for semantic search, use rag-knowledge if available."
- `rag-knowledge` instructions say "for raw document content reads, use knowledge-base-lookup."

This is sufficient — Microsoft's progressive disclosure handles activation, and the agent picks based on intent + the cross-references in the L2 instructions.

## 5. Migration plan — file moves and code edits

### 5.1 Backend file moves

| Source | Destination |
|---|---|
| `Monica.AI/RAG/Models/KnowledgeBase.cs` | `Monica.AI/KnowledgeBase/Models/KnowledgeBase.cs` (namespace `Monica.AI.KnowledgeBase.Models`) |
| `Monica.AI/RAG/Models/DocumentQueueItem.cs` (if currently RAG-namespaced) | `Monica.AI/KnowledgeBase/Models/DocumentQueueItem.cs` |
| Eight `RAGFacade` methods — copy to `KnowledgeBaseFacade.cs` | New file at `Monica.AI/KnowledgeBase/Facades/KnowledgeBaseFacade.cs` |

### 5.2 Backend new files

```
Monica.AI/KnowledgeBase/Abstractions/IKnowledgeBaseLookupService.cs
Monica.AI/KnowledgeBase/Abstractions/IKnowledgeBaseStore.cs
Monica.AI/KnowledgeBase/Facades/KnowledgeBaseFacade.cs
Monica.AI/KnowledgeBase/Models/KnowledgeBase.cs               (moved)
Monica.AI/KnowledgeBase/Models/KnowledgeBaseSummary.cs
Monica.AI/KnowledgeBase/Models/KnowledgeDocumentSummary.cs
Monica.AI/KnowledgeBase/Models/KnowledgeDocumentTreeNode.cs
Monica.AI/KnowledgeBase/Models/KnowledgeDocumentContent.cs
Monica.AI/KnowledgeBase/Services/KnowledgeBaseService.cs
Monica.AI/KnowledgeBase/Services/KnowledgeBaseLookupService.cs
Monica.AI/KnowledgeBase/Skills/KnowledgeBaseLookupSkill.cs
Monica.AI/KnowledgeBase/Modules/ModuleKnowledgeBase.cs
Monica.AI/KnowledgeBase/Modules/ModuleKnowledgeBaseGuide.cs
Monica.AI/KnowledgeBase/Modules/ModuleKnowledgeBaseOption.cs
```

`Monica.Core/Modularity/Models/BuiltInModuleKey.cs` adds two enum entries: `KnowledgeBase`, `KnowledgeBaseUI` (next to `RAG`, `RAGUI`).

### 5.3 Backend edits to existing files

- `Monica.AI/RAG/Modules/ModuleRAG.cs` — `ClaimDependencies` adds `DependsOnModule<ModuleKnowledgeBaseGuide>().Register()`. The seven `MOVE_TO_KB_FACADE` methods are removed from `RAGFacade` outright; in-tree callers are migrated to `KnowledgeBaseFacade` in the same PR.
- `Monica.AI/RAG/Services/RAGService.cs` — KB inventory state previously co-managed inside `RAGService` is moved to `KnowledgeBaseService`. `RAGService` becomes a consumer of `IKnowledgeBaseStore` for inventory reads/writes during indexing.
- `Monica.AI/RAG/Tools/KnowledgeSearchToolProvider.cs` — gets `IKnowledgeBaseLookupService` injected for KB metadata reads (replacing `RAGService.GetKnowledgeBaseByIdAsync`). This is incremental decoupling; the full migration to `RAGKnowledgeSkill` happens in Phase B.

### 5.4 UI file moves

| Source | Destination |
|---|---|
| `Monica.AI.UI/UIRAG/Components/KnowledgeBaseSelector.razor` (+ `.cs`/`.css` if any) | `Monica.AI.UI/UIKnowledgeBase/Components/KnowledgeBaseSelector.razor` |
| `Monica.AI.UI/UIRAG/Components/CreateKnowledgeBaseDialog.razor` | `Monica.AI.UI/UIKnowledgeBase/Components/CreateKnowledgeBaseDialog.razor` |
| `Monica.AI.UI/UIRAG/Components/RAGManageKnowledgeBasePanel.razor` | `Monica.AI.UI/UIKnowledgeBase/Components/KnowledgeBaseListPanel.razor` (renamed) |
| `Monica.AI.UI/UIRAG/State/RAGManagePageState.KnowledgeBase.cs` | Split into `Monica.AI.UI/UIKnowledgeBase/State/KnowledgeBaseManagePageState.cs` (KB CRUD) and removed lines from `RAGManagePageState` |
| `Monica.AI.UI/UIRAG/Models/KnowledgeBaseDialogResult.cs` | `Monica.AI.UI/UIKnowledgeBase/Models/KnowledgeBaseDialogResult.cs` |

### 5.5 UI new files

```
Monica.AI.UI/UIKnowledgeBase/Pages/KnowledgeBaseManagePage.razor
Monica.AI.UI/UIKnowledgeBase/Modules/ModuleKnowledgeBaseUI.cs
Monica.AI.UI/UIKnowledgeBase/Modules/ModuleKnowledgeBaseUIGuide.cs
Monica.AI.UI/UIKnowledgeBase/Modules/ModuleKnowledgeBaseUIOption.cs
Monica.AI.UI/UIKnowledgeBase/_Imports.razor
Monica.AI.UI/UIKnowledgeBase/Localization/   (mirror UIRAG localization layout)
```

### 5.6 UI edits to existing files

- `Monica.AI.UI/UIRAG/Pages/RAGManagePage.razor` — KB-related markup removed. Page slimmed to RAG-pipeline concerns: indexing controls, queue display, embedding model selector, chunker config. The KB selector inside the page is replaced with a `<MudLink>` to the new `KnowledgeBaseManagePage`.
- `Monica.AI.UI/UIRAG/State/RAGManagePageState.cs` — removes injection of KB CRUD methods; keeps RAG-pipeline state. The `_kbFacade` field is deleted.
- `Monica.AI.UI/UIRAG/Modules/ModuleRAGUI.cs` — `ClaimDependencies` adds `DependsOnModule<ModuleKnowledgeBaseUIGuide>().Register()`. Page registrations remove KB-management entries; navOrder of remaining pages is renumbered.
- `Monica.AI.UI/UIChat/State/ChatPageState.cs` — replaces `RAGFacade _ragFacade` with `KnowledgeBaseFacade _kbFacade`. The single use site (KB list load on page enter) flips to `await _kbFacade.GetAllAsync()`. `ChatPageState` no longer depends on RAG at all.
- `Monica.AI.UI/UIChat/Components/ChatInputArea.razor` — `<KnowledgeBaseSelector />` import path updates from `Monica.AI.UI.UIRAG.Components` to `Monica.AI.UI.UIKnowledgeBase.Components`.
- `Monica.AI.UI/UIChat/Modules/ModuleAIUI.cs` — `ClaimDependencies` adds `DependsOnModule<ModuleKnowledgeBaseUIGuide>().Register()`; removes `DependsOnModule<ModuleRAGUIGuide>` if it existed transitively only for the KB selector.

## 6. Open questions and resolutions

| # | Question | Resolution |
|---|---|---|
| (a) | Standalone `Monica.AI.KnowledgeBase` package vs sub-feature inside `Monica.AI`? | **Sub-feature.** No KB consumer outside `Monica.AI` justifies a separate `.csproj`; the dependency arrow `RAG → KB` stays clean inside one project, and the model and Facade move with no public-API breakage. Revisit only if KB later acquires consumers outside `Monica.AI`. |
| (b) | `KnowledgeBaseSelector` cross-module home — UIKnowledgeBase or a third "shared AI primitives" UI module? | **UIKnowledgeBase.** It is unambiguously a KB component. UIChat consumes it as a downstream UI module — that is the normal cross-module-component pattern in Monica (e.g., `ModuleStateStoreUI` re-exports components consumed by other UIs). A third "primitives" UI module would be premature abstraction. |
| (c) | KB persistence backend ownership — stays in current RAG persistence project or moves with the model? | **Moves with the model.** A new `IKnowledgeBaseStore` abstraction lives at `Monica.AI/KnowledgeBase/Abstractions/IKnowledgeBaseStore.cs`. Default implementation `InMemoryKnowledgeBaseStore` lives in `Monica.AI/KnowledgeBase/Services/`. RAG's persistence project keeps the *vector* store (indexed chunks); the KB inventory store is separate, optional, and pluggable. Existing KB persistence code in the RAG project moves with the inventory schema. |
| (d) | Back-compat for existing component imports? | **Not provided.** Per `CLAUDE.md` development-stage policy, the move is a breaking change. Old `Monica.AI.UI/UIRAG/Components/KnowledgeBaseSelector.razor` is deleted; in-tree imports update in the same PR. |

## 7. Acceptance criteria for Phase A

When Phase A is implemented and PR merged:

1. `Monica.AI/KnowledgeBase/` feature folder exists with all directories listed in §2.1 and §5.2 populated.
2. `Monica.AI.UI/UIKnowledgeBase/` feature folder exists with all directories listed in §2.2 and §5.5 populated.
3. `BuiltInModuleKey.cs` has `KnowledgeBase` and `KnowledgeBaseUI` entries.
4. `Mo.AddKnowledgeBase()` and `Mo.AddKnowledgeBaseUI()` extension methods are callable from a host that does not register `Mo.AddRAG()`. The KB Manage page renders, KB CRUD works, KB Selector works in the chat page — all without RAG.
5. `KnowledgeSearchToolProvider` continues to work unchanged in this phase (its full Skill migration is Phase B). KB list load in `ChatPageState` uses `KnowledgeBaseFacade`, not `RAGFacade`.
6. `RAGFacade` no longer carries any KB-CRUD methods. In-tree callers were migrated to `KnowledgeBaseFacade` in the same PR.
7. `ModuleRAG` declares `DependsOnModule<ModuleKnowledgeBaseGuide>()`. `ModuleRAGUI` declares `DependsOnModule<ModuleKnowledgeBaseUIGuide>()` and no longer registers KB management pages.
8. Solution builds with **zero new warnings** (per `CLAUDE.md` build-warning policy).
9. UI smoke test: load chat page with `Mo.AddRAG()` not configured → KB selector still renders → user can select a KB → chat starts normally (with no RAG-search Skill available, only the lookup-only Skill from Phase B; in Phase A pre-merge, no Skill at all is fine).

## 8. Cross-doc references

- Doc 02 (`02-skill-tool-mcp-base.md`) — defines `MoSkill<TSelf>`, `[MoAITool]`, the description-priority chain, and the discovery host that the `KnowledgeBaseLookupSkill` in §4.2 depends on.
- Doc 03 (`03-monica-facade-skill-provider.md`) — uses `KnowledgeBaseFacade` from this doc as its primary worked example for Facade-to-Skill projection.
- Doc 04 (`04-projectunit-skill-provider.md`) — independent of Doc 01.

---

**End of Doc 01.**
