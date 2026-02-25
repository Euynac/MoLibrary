# RAG UI Redesign v3 — Design Document

## Overview

Split the monolithic RAG debug page into two purpose-built pages:
1. **Knowledge Base Management** (`/ai/rag/manage`) — KB CRUD, unified document/queue table, chunk viewer dialog
2. **RAG Debug/Search** (`/ai/rag/debug`) — focused search with enhanced score visualization

## Page 1: Knowledge Base Management

### Layout
Master-detail split: left panel (KB list) + right panel (selected KB detail)

### Key Features

#### Embedding Model Management
- **Model selector** at top of detail panel (dropdown with available models)
- Shows current model name and vector dimensions (e.g., "3072d")
- Switching model triggers warning if KB has different model
- Model is locked to KB on first indexing
- Warning banner if current model differs from KB's model

### Key Design Changes (v3)

#### 1. Unified Documents & Queue Table
- **Single table** shows both indexed documents (Done) and pending documents (Pending)
- **Status column** with color-coded badges:
  - **Pending** (gray) - Document queued for indexing
  - **Indexing** (blue) - Currently being indexed with progress bar
  - **Done** (green) - Successfully indexed
  - **Error** (red) - Indexing failed
- **Progress bars** shown inline for documents in "Indexing" state
- **Parallel control** in table header: configure concurrent indexing (1-10, default 5) + "Start" button

#### 2. Chunk Viewer Dialog (Original Text with Highlights)
- Click document row or eye icon → opens **modal dialog**
- Dialog shows:
  - Document name and metadata in header
  - **Original document text** in scrollable container
  - **Chunks highlighted** within the original text using:
    - Subtle background color (yellow/amber shadow)
    - Chunk index badge on hover or inline
    - Smooth transitions on hover
- **Scalable for hundreds of chunks** - no performance issues
- Better context understanding - see how chunks are distributed in the original document
- Works for both Pending and Done documents (preview vs. actual chunks)

### Modules

| Section | Description |
|---------|-------------|
| KB List (left) | DataGrid with Name, Docs, Chunks, Embedding Model columns. Create button opens dialog. |
| KB Detail Header (right) | Name, description, stats chips, embedding model badge |
| Embedding Model Warning | Alert banner if current embedding model differs from KB's model |
| Documents & Queue Table (right) | **Unified table** - Status badges, inline progress, actions (view/reindex/delete) |
| Chunk Viewer (dialog) | Modal with original text and highlighted chunks |
| Indexing Actions (right) | Markdown group selector → doc selection dialog → adds to queue as Pending |

### Workflow

1. **Select markdown group** → Click "Select Documents"
2. **Choose docs** in dialog (shows indexed status, chunk preview)
3. **Add to queue** → Documents appear in table with "Pending" status
4. **View chunks** anytime (click row or eye icon) → Opens dialog with preview/actual chunks
5. **Configure parallel** count (default 5)
6. **Start indexing** → Pending docs become "Indexing" with progress bars
7. **Done** → Status changes to green "Done" badge, chunks available for viewing

### Component Mapping

| Prototype | MudBlazor |
|-----------|-----------|
| KB table | `MudDataGrid<KnowledgeBase>` |
| Create dialog | `MudDialog` via `IDialogService` |
| Delete confirm | `MudMessageBox` |
| Detail header | `MudText` + `MudChip` for stats |
| Documents list | `MudSimpleTable` or `MudList` |
| Markdown group select | `MudSelect<string>` |
| File upload | `MudFileUpload<IBrowserFile>` |
| Progress | `MudProgressLinear` |

## Page 2: RAG Debug/Search

### Layout
Left sidebar (KB checkboxes) + main content (search + results)

### Key Features

#### Embedding Model Management
- **Model selector** at top of search area (dropdown with available models)
- Shows current model name and vector dimensions (e.g., "3072d")
- Switching model updates search context
- Warning if selected KBs use different models

#### Embedding Model Display
- Show current embedding model at top of search area
- Warning badge if selected KBs have mismatched embedding models
- Tooltip explaining the mismatch issue

#### Search Result Interaction
- Click on any search result card → opens **chunk viewer dialog**
- Dialog shows the source document's original text with:
  - All chunks from that document highlighted (yellow/amber)
  - The matched chunk highlighted differently (green/success color)
  - Chunk index badges for identification

### Modules

| Section | Description |
|---------|-------------|
| KB Sidebar (left) | Checkbox list with select all/deselect all toggle, shows embedding model per KB |
| Embedding Model Info (right) | Display current model, warning if mismatched with selected KBs |
| Search Bar (right) | Text input + Top-K selector + search button |
| Results (right) | Cards with score badge + progress bar header, content below, clickable to view chunks |
| Chunk Viewer Dialog | Shows source document with all chunks highlighted, matched chunk in different color |

### Component Mapping

| Prototype | MudBlazor |
|-----------|-----------|
| KB sidebar | `MudNavMenu` or custom list with `MudCheckBox` |
| Search input | `MudTextField` |
| Top-K select | `MudSelect<int>` |
| Result card | `MudCard` with `MudProgressLinear` |
| Score badge | `MudChip` (color-coded) |
| Metadata | `MudChip` (outlined, small) |

### Score Color Thresholds
- ≥ 80%: Green (success)
- ≥ 50%: Yellow (warning)
- < 50%: Gray (default)
