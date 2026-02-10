# Phase 2: Monica.AI RAG Module Implementation Plan

> Source: `.pending/001-rag-module/phase-2-rag-module.md`
> Created: 2026-02-10

## Current State Analysis

**What exists:**
- `EmbeddingModelInfo` class in `AIModelInfo.cs` (has nullable `Dimensions`)
- `AIModelCatalog` with `GetModel()` method
- `IAIProvider` interface (no embedding support yet)
- `OpenAIProvider` / `AnthropicProvider` implementations
- `OpenAIReservedModels` (LLM models only, no embedding models)
- `Monica.Markdown` project with `IMarkdownDocumentProvider` abstraction
- Module pattern: `MoModule<T, TOption, TGuide>` + `MoModuleGuide` + `MoModuleOption`

**What needs to be built (Phase 2):**
- Embedding support on `IAIProvider` + implementations
- Embedding models in `OpenAIReservedModels`
- Enhance `EmbeddingModelInfo` (add `MaxInputTokens`, `CostPerMillionTokens`)
- RAG models, abstractions, services
- `ModuleRAG` module registration
- NuGet dependency additions

---

## Phases

### Phase A: EmbeddingModelInfo Enhancement & Reserved Models
- **Status:** completed
- [x] A1. Enhance `EmbeddingModelInfo` in `AIModelInfo.cs`
- [x] A2. Add embedding models to `OpenAIReservedModels.cs`

### Phase B: IAIProvider Embedding Extension
- **Status:** completed
- [x] B1. Add `GetEmbeddingGenerator()` to `IAIProvider` interface
- [x] B2. Implement in `OpenAIProvider` with `ConcurrentDictionary` caching
- [x] B3. Implement in `AnthropicProvider` (throw `NotSupportedException`)

### Phase C: RAG Models
- **Status:** completed
- [x] C1. Create `Monica.AI/RAG/Models/KnowledgeBase.cs`
- [x] C2. Create `Monica.AI/RAG/Models/TextSearchResult.cs`
- [x] C3. Create `Monica.AI/RAG/Models/RAGSearchOptions.cs` (includes enum)
- [x] C4. Create `Monica.AI/RAG/Models/IndexingProgress.cs`

### Phase D: RAG Abstractions
- **Status:** completed
- [x] D1. Create `Monica.AI/RAG/Abstractions/IDocumentChunker.cs`
- [x] D2. Create `Monica.AI/RAG/Abstractions/IKnowledgeBaseStore.cs`

### Phase E: RAG Services
- **Status:** completed
- [x] E1. Create `Monica.AI/RAG/Services/MarkdownDocumentChunker.cs`
- [x] E2. Create `Monica.AI/RAG/Services/RAGService.cs` (core orchestrator)

### Phase F: Module Registration & Dependencies
- **Status:** completed
- [x] F1. Update `Monica.AI.csproj` (add VectorData + InMemory packages + Markdown ref)
- [x] F2. Create `Monica.AI/Modules/ModuleRAG.cs` (Module + Option + Guide + Extensions)
- [x] F3. Add `RAG` to `EMoModuleKey` enum

### Phase G: Build Verification
- **Status:** completed
- [x] G1. Run `dotnet build` — succeeded (0 errors)
- [x] G2. Verified downstream `Monica.AI.UI` also builds successfully

---

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| `EmbeddingModelInfo.Dimensions` | `required int` (non-nullable) | Design doc specifies `required int`; current code has `int?` — upgrade needed |
| Vector store schema | Dynamic `VectorStoreCollectionDefinition` | Agent-framework pattern; no attribute-based model class needed |
| Module dependency | Independent `ModuleRAG` | Not a sub-module of `ModuleAI`; follows Monica conventions |
| SK dependency scope | Only `InMemoryVectorStore` | No SK programming model used |
| Embedding default resolution | First `EmbeddingModelInfo` from provider's models | Consistent with design doc pattern |

## Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| `VectorStoreCollection` requires generic type args | **Resolved**: Used `VectorStoreCollection<string, Dictionary<string, object?>>` for dynamic schema |
| `SearchAsync` API signature | **Resolved**: Used `SearchAsync<string>(query, top)` which auto-embeds via configured generator |
| `InMemoryVectorStore` constructor API | **Verified**: `new InMemoryVectorStore(new() { EmbeddingGenerator = ... })` works |
