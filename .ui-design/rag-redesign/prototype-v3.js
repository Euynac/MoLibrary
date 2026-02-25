// ===== Theme =====
const html = document.documentElement;
const saved = localStorage.getItem('mo-theme');
if (saved === 'dark' || (!saved && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
  html.classList.add('dark'); html.setAttribute('data-theme', 'dark');
}
function toggleTheme() {
  const isDark = html.classList.toggle('dark');
  html.setAttribute('data-theme', isDark ? 'dark' : 'light');
  localStorage.setItem('mo-theme', isDark ? 'dark' : 'light');
}

// ===== Sample Data =====
const EMBEDDING_MODELS = [
  { id: 'text-embedding-3-large', name: 'text-embedding-3-large', dimensions: 3072, provider: 'OpenAI' },
  { id: 'text-embedding-3-small', name: 'text-embedding-3-small', dimensions: 1536, provider: 'OpenAI' },
  { id: 'text-embedding-ada-002', name: 'text-embedding-ada-002', dimensions: 1536, provider: 'OpenAI' },
  { id: 'bge-large-zh-v1.5', name: 'bge-large-zh-v1.5', dimensions: 1024, provider: 'BAAI' },
];

let CURRENT_EMBEDDING_MODEL = 'text-embedding-3-large';

function getCurrentModelInfo() {
  return EMBEDDING_MODELS.find(m => m.id === CURRENT_EMBEDDING_MODEL) || EMBEDDING_MODELS[0];
}

function setEmbeddingModel(modelId) {
  CURRENT_EMBEDDING_MODEL = modelId;
  updateEmbeddingModelDisplays();
  // Refresh views to show warnings if needed
  if (selectedKbId) {
    selectKb(selectedKbId);
  }
  renderDebugKbList();
}

function updateEmbeddingModelDisplays() {
  const model = getCurrentModelInfo();
  const displays = document.querySelectorAll('.current-embedding-display');
  displays.forEach(el => {
    el.textContent = model.name;
  });
  const dimDisplays = document.querySelectorAll('.embedding-dimensions-display');
  dimDisplays.forEach(el => {
    el.textContent = `${model.dimensions}d`;
  });
  const selects = document.querySelectorAll('.embedding-model-select');
  selects.forEach(sel => {
    sel.value = CURRENT_EMBEDDING_MODEL;
  });
}

let knowledgeBases = [
  { id: 'kb1', name: 'Product Documentation', desc: 'Official product docs and API references', docs: 4, chunks: 189, created: '2025-12-15', embeddingModel: 'text-embedding-3-large',
    documents: [
      { id: 'doc1', name: 'getting-started.md', status: 'done', chunks: 24, indexed: '2025-12-15',
        originalText: `# Getting Started

Welcome to our product! This guide will help you get started quickly with the basic setup and configuration.

## Installation

To install the product, run: npm install @product/core. Make sure you have Node.js 18+ installed.

You can verify the installation by running: npm list @product/core

## Configuration

Create a config.json file in your project root with the following structure: { "apiKey": "your-key", "endpoint": "https://api.example.com" }

The configuration file supports multiple environments. You can create separate config files for development, staging, and production.

## First Steps

After installation and configuration, you can start using the product by importing it in your code:

import { Product } from '@product/core';

const product = new Product(config);

## Next Steps

Check out the API reference for detailed information about available methods and options. Visit our documentation portal for tutorials and examples.`,
        chunkList: [
          { index: 0, start: 20, end: 130, section: 'Introduction' },
          { index: 1, start: 150, end: 280, section: 'Installation' },
          { index: 2, start: 380, end: 550, section: 'Configuration' },
        ]},
      { id: 'doc2', name: 'api-reference.md', status: 'done', chunks: 89, indexed: '2025-12-16',
        originalText: `# API Reference

## Authentication

All API requests require authentication using Bearer tokens. Include the token in the Authorization header.

Example: Authorization: Bearer your-token-here

Tokens expire after 24 hours and must be refreshed using the refresh endpoint.

## Endpoints

### Users

GET /api/users - List all users. Supports pagination with ?page=1&limit=20 query parameters.

POST /api/users - Create a new user. Requires name and email fields in the request body.

### Posts

POST /api/posts - Create a new post. Requires title and content fields in the request body.

GET /api/posts/:id - Retrieve a specific post by ID.

DELETE /api/posts/:id - Delete a post. Requires admin privileges.`,
        chunkList: [
          { index: 0, start: 30, end: 180, section: 'Authentication' },
          { index: 1, start: 250, end: 380, section: 'Endpoints > Users' },
          { index: 2, start: 450, end: 600, section: 'Endpoints > Posts' },
        ]},
      { id: 'doc3', name: 'configuration-guide.md', status: 'done', chunks: 45, indexed: '2025-12-16',
        originalText: 'Configuration guide content with multiple sections...',
        chunkList: [] },
      { id: 'doc4', name: 'troubleshooting.md', status: 'done', chunks: 31, indexed: '2025-12-17',
        originalText: 'Troubleshooting guide with common issues and solutions...',
        chunkList: [] },
    ]},
  { id: 'kb2', name: 'Developer Notes', desc: 'Internal development notes', docs: 3, chunks: 128, created: '2026-01-08', embeddingModel: 'text-embedding-ada-002', documents: [] },
];

const markdownGroups = {
  'api-docs': [
    { id: 'g1', name: 'authentication.md', indexed: false, chunks: 12 },
    { id: 'g2', name: 'endpoints.md', indexed: true, chunks: 34 },
    { id: 'g3', name: 'webhooks.md', indexed: false, chunks: 18 },
    { id: 'g4', name: 'rate-limiting.md', indexed: false, chunks: 9 },
  ],
  'user-guide': [
    { id: 'g5', name: 'quick-start.md', indexed: false, chunks: 15 },
    { id: 'g6', name: 'features.md', indexed: false, chunks: 28 },
  ],
};

let selectedKbId = null;
let debugSelectedKbs = new Set();
let currentGroupKey = null;

// ===== Page Switching =====
function switchPage(page) {
  document.getElementById('page-manage').classList.toggle('hidden', page !== 'manage');
  document.getElementById('page-debug').classList.toggle('hidden', page !== 'debug');
  document.querySelectorAll('.tab-btn').forEach(btn => {
    btn.classList.remove('border-primary', 'text-primary', 'dark:text-primary-light', 'dark:border-primary-light');
    btn.classList.add('border-transparent', 'text-gray-500', 'dark:text-gray-400');
  });
  const active = document.getElementById('tab-' + page);
  active.classList.remove('border-transparent', 'text-gray-500', 'dark:text-gray-400');
  active.classList.add('border-primary', 'text-primary', 'dark:text-primary-light', 'dark:border-primary-light');
  if (page === 'debug') renderDebugKbList();
}

// ===== Manage Page: KB List =====
function renderKbList() {
  const container = document.getElementById('kb-list-body');
  const empty = document.getElementById('kb-empty');
  if (knowledgeBases.length === 0) {
    container.innerHTML = '';
    empty.classList.remove('hidden'); empty.classList.add('flex');
    return;
  }
  empty.classList.add('hidden'); empty.classList.remove('flex');
  container.innerHTML = knowledgeBases.map(kb => `
    <div class="kb-row ${kb.id === selectedKbId ? 'selected' : ''} mb-2 p-3 rounded-lg border border-gray-200 dark:border-gray-700 hover:border-primary dark:hover:border-primary-light transition-colors" onclick="selectKb('${kb.id}')">
      <div class="flex items-start justify-between gap-2">
        <div class="flex-1 min-w-0">
          <div class="font-medium text-gray-800 dark:text-gray-200 text-sm mb-1">${kb.name}</div>
          <div class="text-xs text-gray-500 dark:text-gray-400 line-clamp-2">${kb.desc || 'No description'}</div>
        </div>
        <button onclick="event.stopPropagation();deleteKb('${kb.id}')" class="p-1 rounded hover:bg-red-50 dark:hover:bg-red-900/20 text-gray-400 hover:text-error transition-colors flex-shrink-0">
          <i data-lucide="trash-2" class="w-3.5 h-3.5"></i>
        </button>
      </div>
    </div>
  `).join('');
  lucide.createIcons();
}

// ===== Manage Page: Detail =====
function selectKb(id) {
  selectedKbId = id;
  renderKbList();
  const kb = knowledgeBases.find(k => k.id === id);
  if (!kb) return;
  document.getElementById('detail-empty').classList.add('hidden');
  document.getElementById('detail-content').classList.remove('hidden');
  document.getElementById('detail-name').textContent = kb.name;
  document.getElementById('detail-desc').textContent = kb.desc;
  document.getElementById('detail-docs').querySelector('span:last-child').textContent = kb.docs + ' documents';
  document.getElementById('detail-chunks').querySelector('span:last-child').textContent = kb.chunks + ' chunks';
  document.getElementById('detail-embedding').querySelector('span:last-child').textContent = kb.embeddingModel || 'Not set';

  // Show embedding model warning if mismatch
  const warning = document.getElementById('embedding-warning');
  if (kb.embeddingModel && kb.embeddingModel !== CURRENT_EMBEDDING_MODEL) {
    warning.classList.remove('hidden');
    document.getElementById('current-model-display').textContent = CURRENT_EMBEDDING_MODEL;
    document.getElementById('kb-model-display').textContent = kb.embeddingModel;
  } else {
    warning.classList.add('hidden');
  }

  renderDocTable(kb);
  lucide.createIcons();
}

function renderDocTable(kb) {
  const tbody = document.getElementById('detail-doc-table');
  const empty = document.getElementById('detail-doc-empty');
  const count = document.getElementById('detail-doc-count');
  if (!kb.documents || kb.documents.length === 0) {
    tbody.innerHTML = '';
    empty.classList.remove('hidden');
    count.textContent = '';
    return;
  }
  empty.classList.add('hidden');
  count.textContent = kb.documents.length + ' total';

  tbody.innerHTML = kb.documents.map(d => {
    let statusBadge = '';
    let statusColor = '';
    let progressBar = '';

    if (d.status === 'pending') {
      statusBadge = '<span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-400">Pending</span>';
    } else if (d.status === 'indexing') {
      statusBadge = '<span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300">Indexing</span>';
      progressBar = `<div class="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-1 mt-1"><div class="bg-primary h-1 rounded-full transition-all" style="width:${d.progress || 0}%"></div></div>`;
    } else if (d.status === 'done') {
      statusBadge = '<span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-300">Done</span>';
    } else if (d.status === 'error') {
      statusBadge = '<span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-300">Error</span>';
    }

    const canViewChunks = d.status === 'done' || d.status === 'pending';
    const canReindex = d.status === 'done';
    const canDelete = d.status !== 'indexing';

    return `
    <tr class="doc-row" onclick="${canViewChunks ? `viewChunks('${d.id}')` : ''}">
      <td class="px-4 py-2.5">
        <div class="flex items-center gap-2">
          <i data-lucide="file-text" class="w-4 h-4 text-gray-400 flex-shrink-0"></i>
          <div class="flex-1">
            <div class="text-gray-800 dark:text-gray-200">${d.name}</div>
            ${progressBar}
          </div>
        </div>
      </td>
      <td class="text-center px-3 py-2.5">${statusBadge}</td>
      <td class="text-center px-3 py-2.5 text-gray-500 dark:text-gray-400">${d.chunks || '-'}</td>
      <td class="text-right px-3 py-2.5">
        <div class="flex items-center justify-end gap-1">
          ${canViewChunks ? `
          <button onclick="event.stopPropagation();viewChunks('${d.id}')" class="p-1 rounded hover:bg-blue-50 dark:hover:bg-blue-900/20 text-gray-400 hover:text-primary transition-colors" title="View Chunks">
            <i data-lucide="eye" class="w-3.5 h-3.5"></i>
          </button>` : ''}
          ${canReindex ? `
          <button onclick="event.stopPropagation();reindexDoc('${d.id}')" class="p-1 rounded hover:bg-blue-50 dark:hover:bg-blue-900/20 text-gray-400 hover:text-primary transition-colors" title="Re-index">
            <i data-lucide="refresh-cw" class="w-3.5 h-3.5"></i>
          </button>` : ''}
          ${canDelete ? `
          <button onclick="event.stopPropagation();deleteDoc('${d.id}')" class="p-1 rounded hover:bg-red-50 dark:hover:bg-red-900/20 text-gray-400 hover:text-error transition-colors" title="Delete">
            <i data-lucide="trash-2" class="w-3.5 h-3.5"></i>
          </button>` : ''}
        </div>
      </td>
    </tr>
  `}).join('');
  lucide.createIcons();
}

function viewChunks(docId, matchedChunkIndex = null) {
  const kb = knowledgeBases.find(k => k.id === selectedKbId);
  if (!kb) return;
  const doc = kb.documents.find(d => d.id === docId);
  if (!doc) return;

  document.getElementById('chunks-doc-name').textContent = doc.name;
  document.getElementById('chunks-doc-info').textContent = `${doc.chunks} chunks · ${doc.status === 'done' ? 'Indexed ' + doc.indexed : 'Pending indexing'}`;

  const container = document.getElementById('chunks-original-text');

  if (!doc.originalText || !doc.chunkList || doc.chunkList.length === 0) {
    container.innerHTML = '<div class="text-center py-8 text-gray-400 dark:text-gray-500 text-sm">No original text or chunks available</div>';
  } else {
    // Sort chunks by start position
    const sortedChunks = [...doc.chunkList].sort((a, b) => a.start - b.start);

    // Build HTML with highlighted chunks
    let html = '';
    let lastPos = 0;

    sortedChunks.forEach(chunk => {
      // Add text before chunk
      if (chunk.start > lastPos) {
        html += escapeHtml(doc.originalText.substring(lastPos, chunk.start));
      }

      // Add highlighted chunk (matched chunk gets different styling)
      const chunkText = doc.originalText.substring(chunk.start, chunk.end);
      const isMatched = matchedChunkIndex !== null && chunk.index === matchedChunkIndex;
      const matchedClass = isMatched ? ' matched' : '';
      html += `<span class="chunk-highlight${matchedClass}" title="Chunk ${chunk.index}: ${chunk.section}${isMatched ? ' (Matched)' : ''}">${escapeHtml(chunkText)}<span class="chunk-badge${matchedClass}">#${chunk.index}</span></span>`;

      lastPos = chunk.end;
    });

    // Add remaining text after last chunk
    if (lastPos < doc.originalText.length) {
      html += escapeHtml(doc.originalText.substring(lastPos));
    }

    container.innerHTML = `<div class="whitespace-pre-wrap">${html}</div>`;
  }

  document.getElementById('chunks-dialog').classList.remove('hidden');
  document.getElementById('chunks-dialog').classList.add('flex');
  lucide.createIcons();
}

function escapeHtml(text) {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}

function viewSearchResultChunks(docId, matchedChunkIndex) {
  // Find the document across all knowledge bases
  let foundDoc = null;
  let foundKb = null;

  for (const kb of knowledgeBases) {
    const doc = kb.documents.find(d => d.id === docId);
    if (doc) {
      foundDoc = doc;
      foundKb = kb;
      break;
    }
  }

  if (!foundDoc || !foundKb) return;

  // Temporarily set selectedKbId for the dialog
  const previousKbId = selectedKbId;
  selectedKbId = foundKb.id;

  // Open the chunks viewer with the matched chunk highlighted
  viewChunks(docId, matchedChunkIndex);

  // Restore previous selection
  selectedKbId = previousKbId;
}

function closeChunksDialog() {
  document.getElementById('chunks-dialog').classList.add('hidden');
  document.getElementById('chunks-dialog').classList.remove('flex');
}

function reindexDoc(docId) {
  if (confirm('Re-index this document? Existing chunks will be replaced.')) {
    const kb = knowledgeBases.find(k => k.id === selectedKbId);
    if (!kb) return;
    const doc = kb.documents.find(d => d.id === docId);
    if (!doc) return;
    doc.status = 'indexing';
    doc.progress = 0;
    renderDocTable(kb);

    // Simulate indexing
    const interval = setInterval(() => {
      doc.progress += 10;
      if (doc.progress >= 100) {
        clearInterval(interval);
        doc.status = 'done';
        doc.progress = 100;
      }
      renderDocTable(kb);
    }, 200);
  }
}

function deleteDoc(docId) {
  if (confirm('Delete this document from the knowledge base?')) {
    const kb = knowledgeBases.find(k => k.id === selectedKbId);
    if (kb) {
      kb.documents = kb.documents.filter(d => d.id !== docId);
      kb.docs = kb.documents.length;
      kb.chunks = kb.documents.reduce((sum, d) => sum + (d.chunks || 0), 0);
      renderKbList();
      renderDocTable(kb);
    }
  }
}

// ===== Create / Delete KB =====
function openCreateDialog() {
  document.getElementById('create-dialog').classList.remove('hidden');
  document.getElementById('create-dialog').classList.add('flex');
  document.getElementById('create-name').value = '';
  document.getElementById('create-desc').value = '';
  document.getElementById('create-name').focus();
}
function closeCreateDialog() {
  document.getElementById('create-dialog').classList.add('hidden');
  document.getElementById('create-dialog').classList.remove('flex');
}
function createKb() {
  const name = document.getElementById('create-name').value.trim();
  if (!name) return;
  const desc = document.getElementById('create-desc').value.trim();
  const id = 'kb' + Date.now();
  knowledgeBases.push({ id, name, desc: desc || '', docs: 0, chunks: 0, created: new Date().toISOString().slice(0,10), documents: [] });
  closeCreateDialog();
  renderKbList();
  selectKb(id);
}
function deleteKb(id) {
  if (!confirm('Delete this knowledge base? This action cannot be undone.')) return;
  knowledgeBases = knowledgeBases.filter(k => k.id !== id);
  if (selectedKbId === id) {
    selectedKbId = null;
    document.getElementById('detail-empty').classList.remove('hidden');
    document.getElementById('detail-content').classList.add('hidden');
  }
  debugSelectedKbs.delete(id);
  renderKbList();
}

// ===== Markdown Group Selection =====
function loadGroupDocs() {
  const sel = document.getElementById('md-group-select').value;
  currentGroupKey = sel;
  document.getElementById('select-docs-btn').disabled = !sel;
}

function openGroupDocsDialog() {
  if (!currentGroupKey) return;
  const docs = markdownGroups[currentGroupKey] || [];
  const list = document.getElementById('group-docs-list');
  list.innerHTML = docs.map(d => `
    <label class="flex items-center gap-2 px-2 py-1.5 rounded hover:bg-gray-100 dark:hover:bg-gray-800 cursor-pointer">
      <input type="checkbox" data-doc-id="${d.id}" onchange="updateSelectedCount()" class="w-4 h-4 rounded border-gray-300 text-primary accent-primary">
      <div class="flex-1 flex items-center justify-between">
        <span class="text-sm text-gray-800 dark:text-gray-200">${d.name}</span>
        <div class="flex items-center gap-2">
          <span class="text-xs text-gray-400">${d.chunks} chunks</span>
          ${d.indexed ? '<span class="text-xs px-1.5 py-0.5 bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-300 rounded">Indexed</span>' : ''}
        </div>
      </div>
    </label>
  `).join('');
  document.getElementById('group-docs-dialog').classList.remove('hidden');
  document.getElementById('group-docs-dialog').classList.add('flex');
  updateSelectedCount();
  lucide.createIcons();
}

function closeGroupDocsDialog() {
  document.getElementById('group-docs-dialog').classList.add('hidden');
  document.getElementById('group-docs-dialog').classList.remove('flex');
}

function toggleAllDocs(checked) {
  document.querySelectorAll('#group-docs-list input[type="checkbox"]').forEach(cb => cb.checked = checked);
  updateSelectedCount();
}

function updateSelectedCount() {
  const checked = document.querySelectorAll('#group-docs-list input[type="checkbox"]:checked').length;
  document.getElementById('selected-count').textContent = checked + ' selected';
}

function addToQueue() {
  const kb = knowledgeBases.find(k => k.id === selectedKbId);
  if (!kb) return;

  const docs = markdownGroups[currentGroupKey] || [];
  const checked = Array.from(document.querySelectorAll('#group-docs-list input[type="checkbox"]:checked')).map(cb => cb.dataset.docId);

  checked.forEach(id => {
    const doc = docs.find(d => d.id === id);
    if (doc && !kb.documents.find(d => d.id === id)) {
      kb.documents.push({
        id,
        name: doc.name,
        status: 'pending',
        chunks: doc.chunks,
        progress: 0,
        originalText: `# ${doc.name}\n\nThis is a preview of the original document text. Actual content will be loaded during indexing.\n\nSection 1: Introduction\nLorem ipsum dolor sit amet, consectetur adipiscing elit.\n\nSection 2: Details\nSed do eiusmod tempor incididunt ut labore et dolore magna aliqua.`,
        chunkList: [
          { index: 0, start: 0, end: 100, section: 'Preview' }
        ]
      });
    }
  });

  kb.docs = kb.documents.length;
  closeGroupDocsDialog();
  renderKbList();
  renderDocTable(kb);
}

// ===== Batch Indexing =====
function startBatchIndexing() {
  const kb = knowledgeBases.find(k => k.id === selectedKbId);
  if (!kb) return;

  // Set embedding model on first indexing
  if (!kb.embeddingModel) {
    kb.embeddingModel = CURRENT_EMBEDDING_MODEL;
    renderKbList();
    selectKb(kb.id); // Refresh detail view
  }

  const parallel = parseInt(document.getElementById('parallel-count').value) || 5;
  const pending = kb.documents.filter(d => d.status === 'pending');
  if (pending.length === 0) return;

  let active = 0;
  let index = 0;

  function processNext() {
    if (index >= pending.length) return;
    if (active >= parallel) return;

    const doc = pending[index++];
    doc.status = 'indexing';
    doc.progress = 0;
    active++;
    renderDocTable(kb);

    const interval = setInterval(() => {
      doc.progress += 10;
      if (doc.progress >= 100) {
        clearInterval(interval);
        doc.status = 'done';
        doc.progress = 100;
        doc.indexed = new Date().toISOString().slice(0, 10);
        // Generate sample original text and chunks with positions
        doc.originalText = `# ${doc.name}

## Introduction

This is the introduction section of ${doc.name}. It provides an overview of the content and sets the context for the reader.

## Main Content

The main content section contains detailed information about the topic. This includes examples, explanations, and best practices.

Here are some key points:
- Point 1: Important concept
- Point 2: Another important concept
- Point 3: Additional details

## Advanced Topics

This section covers advanced topics and edge cases. It's designed for users who need deeper understanding.

## Conclusion

The conclusion summarizes the key takeaways and provides next steps for the reader.`;

        // Generate chunks with realistic positions
        const sections = [
          { start: 0, end: 180, section: 'Introduction' },
          { start: 200, end: 450, section: 'Main Content' },
          { start: 470, end: 620, section: 'Advanced Topics' },
        ];
        doc.chunkList = sections.map((s, i) => ({ index: i, start: s.start, end: s.end, section: s.section }));

        active--;
        kb.chunks = kb.documents.reduce((sum, d) => sum + (d.chunks || 0), 0);
        renderKbList();
        renderDocTable(kb);
        processNext();
      } else {
        renderDocTable(kb);
      }
    }, 200);

    processNext();
  }

  processNext();
}

function simulateUpload() {
  alert('File picker would open here. Selected file will be added to the queue as pending.');
}

// ===== Debug Page: KB Sidebar =====
function renderDebugKbList() {
  const container = document.getElementById('debug-kb-list');
  container.innerHTML = knowledgeBases.map(kb => {
    const checked = debugSelectedKbs.has(kb.id);
    const modelMismatch = kb.embeddingModel && kb.embeddingModel !== CURRENT_EMBEDDING_MODEL;
    return `
    <label class="flex items-center gap-2.5 px-2 py-2 rounded hover:bg-gray-100 dark:hover:bg-gray-800 cursor-pointer transition-colors">
      <input type="checkbox" ${checked ? 'checked' : ''} onchange="toggleDebugKb('${kb.id}', this.checked)" class="w-4 h-4 rounded border-gray-300 text-primary accent-primary">
      <div class="flex-1 min-w-0">
        <div class="flex items-center gap-1.5">
          <div class="text-sm font-medium text-gray-800 dark:text-gray-200 truncate">${kb.name}</div>
          ${modelMismatch ? '<i data-lucide="alert-triangle" class="w-3.5 h-3.5 text-warning flex-shrink-0" title="Embedding model mismatch"></i>' : ''}
        </div>
        <div class="text-xs text-gray-400 dark:text-gray-500">${kb.docs} docs · ${kb.chunks} chunks</div>
        ${kb.embeddingModel ? `<div class="text-xs text-gray-500 dark:text-gray-400 font-mono mt-0.5">${kb.embeddingModel}</div>` : ''}
      </div>
    </label>`;
  }).join('');
  updateToggleAllBtn();
}

function toggleDebugKb(id, checked) {
  if (checked) debugSelectedKbs.add(id); else debugSelectedKbs.delete(id);
  updateToggleAllBtn();
}

function toggleAllKbs() {
  if (debugSelectedKbs.size === knowledgeBases.length) debugSelectedKbs.clear();
  else knowledgeBases.forEach(kb => debugSelectedKbs.add(kb.id));
  renderDebugKbList();
}

function updateToggleAllBtn() {
  const btn = document.getElementById('toggle-all-btn');
  btn.textContent = debugSelectedKbs.size === knowledgeBases.length ? 'Deselect All' : 'Select All';
}

// ===== Debug Page: Search =====
const sampleResults = [
  { score: 0.94, source: 'api-reference.md', section: 'Authentication > OAuth2 Flow', kb: 'Product Documentation', docId: 'doc2', chunkIndex: 0,
    text: 'The OAuth2 authentication flow requires a valid client_id and client_secret. Tokens are issued with a default TTL of 3600 seconds.' },
  { score: 0.72, source: 'getting-started.md', section: 'Installation', kb: 'Product Documentation', docId: 'doc1', chunkIndex: 1,
    text: 'To install the product, run: npm install @product/core. Make sure you have Node.js 18+ installed.' },
];

function doSearch() {
  const query = document.getElementById('search-input').value.trim();
  if (!query || debugSelectedKbs.size === 0) return;

  // Check for embedding model mismatches
  const selectedKbsList = Array.from(debugSelectedKbs).map(id => knowledgeBases.find(k => k.id === id)).filter(Boolean);
  const mismatchedKbs = selectedKbsList.filter(kb => kb.embeddingModel && kb.embeddingModel !== CURRENT_EMBEDDING_MODEL);

  const warningEl = document.getElementById('embedding-mismatch-warning');
  if (mismatchedKbs.length > 0) {
    warningEl.classList.remove('hidden');
    const details = mismatchedKbs.map(kb => `${kb.name} (${kb.embeddingModel})`).join(', ');
    document.getElementById('mismatch-details').textContent = `The following knowledge bases use different embedding models: ${details}. Search results may be inaccurate.`;
  } else {
    warningEl.classList.add('hidden');
  }

  const container = document.getElementById('search-results');
  const empty = document.getElementById('search-empty');
  empty.classList.add('hidden');
  const filtered = sampleResults.filter(r => {
    const kb = knowledgeBases.find(k => k.name === r.kb);
    return kb && debugSelectedKbs.has(kb.id);
  });
  if (filtered.length === 0) {
    container.innerHTML = '';
    empty.classList.remove('hidden');
    return;
  }
  container.innerHTML = `<p class="text-xs text-gray-400 dark:text-gray-500 mb-1">${filtered.length} results</p>` +
    filtered.map(r => {
      const pct = Math.round(r.score * 100);
      let color, bg, barColor;
      if (r.score >= 0.8) { color = 'text-green-700 dark:text-green-300'; bg = 'bg-green-100 dark:bg-green-900/30'; barColor = 'bg-success'; }
      else if (r.score >= 0.5) { color = 'text-yellow-700 dark:text-yellow-300'; bg = 'bg-yellow-100 dark:bg-yellow-900/30'; barColor = 'bg-warning'; }
      else { color = 'text-gray-600 dark:text-gray-400'; bg = 'bg-gray-100 dark:bg-gray-800'; barColor = 'bg-gray-400'; }
      return `
      <div class="border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden bg-surface dark:bg-surface-dark cursor-pointer hover:border-primary dark:hover:border-primary-light transition-colors" onclick="viewSearchResultChunks('${r.docId}', ${r.chunkIndex})">
        <div class="px-4 py-2.5 flex items-center gap-3 border-b border-gray-100 dark:border-gray-700/50">
          <span class="inline-flex items-center px-2 py-0.5 rounded text-xs font-bold ${color} ${bg}">${pct}%</span>
          <div class="flex-1 h-1.5 bg-gray-200 dark:bg-gray-700 rounded-full overflow-hidden">
            <div class="${barColor} h-full rounded-full progress-animate" style="width:${pct}%"></div>
          </div>
        </div>
        <div class="px-4 py-3">
          <div class="flex items-center gap-2 mb-2 flex-wrap">
            <span class="inline-flex items-center gap-1 text-xs font-medium text-gray-700 dark:text-gray-300">
              <i data-lucide="file-text" class="w-3 h-3"></i>${r.source}
            </span>
            <span class="text-gray-300 dark:text-gray-600">·</span>
            <span class="text-xs text-gray-500 dark:text-gray-400">${r.section}</span>
            <span class="text-gray-300 dark:text-gray-600">·</span>
            <span class="text-xs text-blue-600 dark:text-blue-400 hover:underline">View in document</span>
          </div>
          <p class="text-sm text-gray-700 dark:text-gray-300 leading-relaxed">${r.text}</p>
        </div>
      </div>`;
    }).join('');
  lucide.createIcons();
}

// ===== Init =====
switchPage('manage');
renderKbList();
knowledgeBases.forEach(kb => debugSelectedKbs.add(kb.id));
updateEmbeddingModelDisplays();
lucide.createIcons();
