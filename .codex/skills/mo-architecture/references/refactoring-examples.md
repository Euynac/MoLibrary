# Refactoring Examples & Prohibited Practices

## RAG Refactoring Example

Current `RAGService.cs` (800+ lines) → target structure:

```
Monica.AI/RAG/
├── Annotations/
│   └── RAGSourceAttribute.cs              # (if needed)
├── Abstractions/
│   ├── IDocumentChunker.cs              # Public
│   └── Internal/
│       ├── IDocumentIndexStateStore.cs
│       ├── IKnowledgeDocumentSourceStore.cs
│       └── IChunkerRoutingStore.cs
├── Models/
│   ├── KnowledgeBase.cs                 # Public
│   ├── TextSearchResult.cs
│   ├── DocumentIndexState.cs
│   ├── IndexingProgress.cs
│   └── Internal/
│       ├── RAGVectorRecord.cs
│       ├── DocumentQueueItem.cs
│       └── RAGEmbeddingBinding.cs
├── Facades/
│   └── RAGFacade.cs                     # ~150 lines
├── Services/
│   ├── KnowledgeBaseService.cs
│   ├── DocumentIndexingService.cs
│   ├── KnowledgeSearchService.cs
│   ├── DocumentQueueService.cs
│   └── Support/
│       ├── ChunkerRegistry.cs
│       ├── RAGEmbeddingBindingResolver.cs
│       ├── RAGIndexStateCoordinator.cs
│       └── RAGVectorCollectionCoordinator.cs
├── Providers/
│   ├── Stores/
│   │   ├── FileDocumentIndexStateStore.cs
│   │   ├── FileKnowledgeDocumentSourceStore.cs
│   │   └── FileChunkerRoutingStore.cs
│   └── Chunkers/
│       ├── ProductionMarkdownDocumentChunker.cs
│       └── SimpleMarkdownDocumentChunker.cs
└── Tools/
    └── KnowledgeSearchToolProvider.cs
```

Key decisions in this refactoring:
- Single 800-line service → 4 focused services + 4 support services
- Internal abstractions for store contracts (not needed by external modules)
- Public abstractions for extension points (`IDocumentChunker`)
- Providers grouped by capability (`Stores/`, `Chunkers/`)
- Facade stays thin (~150 lines), delegates to services

## Prohibited Practices

- A single service acting as facade + business logic + provider coordination + state recovery
- Internal services depending on Facades
- UI components depending on Providers directly
- Inventing custom folder names outside the standard layer names
- Flat root-level `Components/Services/Models` in UI modules
- Duplicating infrastructure Models in UI modules
- Complex business logic in Minimal API handlers or Razor pages
- Using `Helpers/`, `Tools/`, `Common/`, `Misc/` as folder names
