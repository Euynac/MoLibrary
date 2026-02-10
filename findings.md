# Findings - Phase 2 RAG Module

> Updated: 2026-02-10

## Codebase Analysis

### Existing EmbeddingModelInfo (needs enhancement)
- **File:** `Monica.AI/Models/AIModelInfo.cs:79-85`
- Current: `int? Dimensions` (nullable)
- Design requires: `required int Dimensions` + `MaxInputTokens` + `CostPerMillionTokens`

### IAIProvider Interface
- **File:** `Monica.AI/Abstractions/IAIProvider.cs`
- No embedding support. Methods: `GetChatClient`, `TestConnectionAsync`, `GetAvailableModelsAsync`, `UpdateSystemPrompt`
- Uses `Res` return type for `TestConnectionAsync` and `GetAvailableModelsAsync`

### OpenAI Provider
- **File:** `Monica.AI/Providers/OpenAI/OpenAIProvider.cs`
- Uses `OpenAI` SDK v2 (`OpenAIClient`)
- Chat client caching via `ConcurrentDictionary<string, IChatClient>`
- Model resolution via `AIProviderModelResolver.ResolveModels()`
- `_models` field holds resolved `IReadOnlyList<AIModelInfo>`

### OpenAI Reserved Models
- **File:** `Monica.AI/Providers/OpenAI/OpenAIReservedModels.cs`
- Only LLM models (gpt-4o, gpt-4o-mini, o1, o1-mini)
- No embedding models yet

### Module Pattern
- Module: `MoModule<TSelf, TOption, TGuide>` with `ConfigureServices(IServiceCollection)`
- Guide: `MoModuleGuide<TModule, TOption, TSelf>` with fluent API
- Option: `MoModuleOption<TModule>` with logging/disable support
- Builder extension: `extension(Mo)` with `public static TGuide AddXxx()`

### Monica.Markdown Integration
- `IMarkdownDocumentProvider` provides `ScanGroupAsync` and `GetDocumentContentAsync`
- `MarkdownDocument` has `GroupKey`, `Title`, `FilePath`, `RelativePath`
- `DocumentGroupRegistration` has `Key`, `Title`, `BasePath`
- RAGService's `IndexMarkdownGroupAsync` will use these abstractions

### NuGet Dependencies Needed
- `Microsoft.Extensions.VectorData.Abstractions` (9.*)
- `Microsoft.SemanticKernel.Connectors.InMemory` (1.*-*)
- Project reference to `Monica.Markdown`

### Key API Questions
- `OpenAIClient.GetEmbeddingClient()` returns `EmbeddingClient`
- `Microsoft.Extensions.AI.OpenAI` should provide `.AsIEmbeddingGenerator()` extension
- `InMemoryVectorStore` constructor options need verification
