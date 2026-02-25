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
let knowledgeBases = [
  { id: 'kb1', name: 'Product Documentation', desc: 'Official product docs and API references', docs: 4, chunks: 189, created: '2025-12-15',
    documents: [
      { id: 'doc1', name: 'getting-started.md', chunks: 24, indexed: '2025-12-15',
        chunkList: [
          { index: 0, section: 'Introduction', content: 'Welcome to our product! This guide will help you get started quickly with the basic setup and configuration.' },
          { index: 1, section: 'Installation', content: 'To install the product, run: npm install @product/core. Make sure you have Node.js 18+ installed.' },
          { index: 2, section: 'Configuration', content: 'Create a config.json file in your project root with the following structure: { "apiKey": "your-key", "endpoint": "https://api.example.com" }' },
        ]},
      { id: 'doc2', name: 'api-reference.md', chunks: 89, indexed: '2025-12-16',
        chunkList: [
          { index: 0, section: 'Authentication', content: 'All API requests require authentication using Bearer tokens. Include the token in the Authorization header.' },
          { index: 1, section: 'Endpoints > Users', content: 'GET /api/users - List all users. Supports pagination with ?page=1&limit=20 query parameters.' },
        ]},
      { id: 'doc3', name: 'configuration-guide.md', chunks: 45, indexed: '2025-12-16', chunkList: [] },
      { id: 'doc4', name: 'troubleshooting.md', chunks: 31, indexed: '2025-12-17', chunkList: [] },
    ]},
  { id: 'kb2', name: 'Developer Notes', desc: 'Internal development notes', docs: 3, chunks: 128, created: '2026-01-08', documents: [] },
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
let expandedDocId = null;
let debugSelectedKbs = new Set();
let indexingQueue = [];
let currentGroupKey = null;
let selectedGroupDocs = new Set();

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
  const tbody = document.getElementById('kb-list-body');
  const empty = document.getElementById('kb-empty');
  if (knowledgeBases.length === 0) {
    tbody.innerHTML = '';
    empty.classList.remove('hidden'); empty.classList.add('flex');
    return;
  }
  empty.classList.add('hidden'); empty.classList.remove('flex');
  tbody.innerHTML = knowledgeBases.map(kb => `
    <tr class="kb-row ${kb.id === selectedKbId ? 'selected' : ''}" onclick="selectKb('${kb.id}')">
      <td class="px-3 py-2.5">
        <div class="font-medium text-gray-800 dark:text-gray-200">${kb.name}</div>
        <div class="text-xs text-gray-400 dark:text-gray-500 truncate max-w-[180px]">${kb.desc}</div>
      </td>
      <td class="text-center px-2 py-2.5 text-gray-500 dark:text-gray-400">${kb.docs}</td>
      <td class="text-center px-2 py-2.5 text-gray-500 dark:text-gray-400">${kb.chunks}</td>
      <td class="text-right px-3 py-2.5">
        <button onclick="event.stopPropagation();deleteKb('${kb.id}')" class="p-1 rounded hover:bg-red-50 dark:hover:bg-red-900/20 text-gray-400 hover:text-error transition-colors">
          <i data-lucide="trash-2" class="w-3.5 h-3.5"></i>
        </button>
      </td>
    </tr>
  `).join('');
  lucide.createIcons();
}

// ===== Manage Page: Detail =====
function selectKb(id) {
  selectedKbId = id;
  expandedDocId = null;
  renderKbList();
  const kb = knowledgeBases.find(k => k.id === id);
  if (!kb) return;
  document.getElementById('detail-empty').classList.add('hidden');
  document.getElementById('detail-content').classList.remove('hidden');
  document.getElementById('detail-name').textContent = kb.name;
  document.getElementById('detail-desc').textContent = kb.desc;
  document.getElementById('detail-docs').querySelector('span:last-child').textContent = kb.docs + ' documents';
  document.getElementById('detail-chunks').querySelector('span:last-child').textContent = kb.chunks + ' chunks';
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
  count.textContent = kb.documents.length + ' documents';
  tbody.innerHTML = kb.documents.map(d => {
    const isExpanded = d.id === expandedDocId;
    return `
    <tr class="doc-row ${isExpanded ? 'expanded' : ''}" onclick="toggleDocExpand('${d.id}')">
      <td class="px-4 py-2.5 text-center">
        <i data-lucide="${isExpanded ? 'chevron-down' : 'chevron-right'}" class="w-4 h-4 text-gray-400"></i>
      </td>
      <td class="px-4 py-2.5">
        <div class="flex items-center gap-2">
          <i data-lucide="file-text" class="w-4 h-4 text-gray-400 flex-shrink-0"></i>
          <span class="text-gray-800 dark:text-gray-200">${d.name}</span>
        </div>
      </td>
      <td class="text-center px-3 py-2.5 text-gray-500 dark:text-gray-400">${d.chunks}</td>
      <td class="text-right px-3 py-2.5">
        <div class="flex items-center justify-end gap-1">
          <button onclick="event.stopPropagation();reindexDoc('${d.id}')" class="p-1 rounded hover:bg-blue-50 dark:hover:bg-blue-900/20 text-gray-400 hover:text-primary transition-colors" title="Re-index">
            <i data-lucide="refresh-cw" class="w-3.5 h-3.5"></i>
          </button>
          <button onclick="event.stopPropagation();deleteDoc('${d.id}')" class="p-1 rounded hover:bg-red-50 dark:hover:bg-red-900/20 text-gray-400 hover:text-error transition-colors" title="Delete">
            <i data-lucide="trash-2" class="w-3.5 h-3.5"></i>
          </button>
        </div>
      </td>
    </tr>
    ${isExpanded ? renderChunks(d) : ''}
  `}).join('');
  lucide.createIcons();
}

function renderChunks(doc) {
  if (!doc.chunkList || doc.chunkList.length === 0) {
    return `<tr class="chunk-row"><td colspan="4" class="px-4 py-3 text-sm text-gray-500 dark:text-gray-400 text-center">No chunks to display (click to load)</td></tr>`;
  }
  return doc.chunkList.map(c => `
    <tr class="chunk-row">
      <td class="px-4 py-2"></td>
      <td colspan="3" class="px-4 py-2">
        <div class="text-xs text-gray-500 dark:text-gray-400 mb-1">
          <span class="font-medium">Chunk ${c.index}</span> · ${c.section}
        </div>
        <div class="text-sm text-gray-700 dark:text-gray-300 bg-gray-50 dark:bg-gray-800/50 rounded px-3 py-2 border border-gray-200 dark:border-gray-700">
          ${c.content}
        </div>
      </td>
    </tr>
  `).join('');
}

function toggleDocExpand(docId) {
  expandedDocId = expandedDocId === docId ? null : docId;
  const kb = knowledgeBases.find(k => k.id === selectedKbId);
  if (kb) renderDocTable(kb);
}

function reindexDoc(docId) {
  if (confirm('Re-index this document? Existing chunks will be replaced.')) {
    alert('Re-indexing ' + docId + '...');
  }
}

function deleteDoc(docId) {
  if (confirm('Delete this document from the knowledge base?')) {
    const kb = knowledgeBases.find(k => k.id === selectedKbId);
    if (kb) {
      kb.documents = kb.documents.filter(d => d.id !== docId);
      kb.docs = kb.documents.length;
      kb.chunks = kb.documents.reduce((sum, d) => sum + d.chunks, 0);
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
  selectedGroupDocs.clear();
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
  const docs = markdownGroups[currentGroupKey] || [];
  const checked = Array.from(document.querySelectorAll('#group-docs-list input[type="checkbox"]:checked')).map(cb => cb.dataset.docId);
  checked.forEach(id => {
    const doc = docs.find(d => d.id === id);
    if (doc && !indexingQueue.find(q => q.id === id)) {
      indexingQueue.push({ id, name: doc.name, chunks: doc.chunks, status: 'pending', progress: 0 });
    }
  });
  closeGroupDocsDialog();
  renderQueue();
}

// ===== Indexing Queue =====
function renderQueue() {
  const section = document.getElementById('indexing-queue-section');
  const list = document.getElementById('queue-list');
  if (indexingQueue.length === 0) {
    section.classList.add('hidden');
    return;
  }
  section.classList.remove('hidden');
  list.innerHTML = indexingQueue.map((q, i) => {
    let statusColor = 'bg-gray-100 dark:bg-gray-800 text-gray-600 dark:text-gray-400';
    let statusText = 'Pending';
    if (q.status === 'indexing') { statusColor = 'bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300'; statusText = 'Indexing...'; }
    if (q.status === 'done') { statusColor = 'bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-300'; statusText = 'Done'; }
    if (q.status === 'error') { statusColor = 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-300'; statusText = 'Error'; }

    return `
    <div class="border border-gray-200 dark:border-gray-700 rounded-lg p-3 flex items-center gap-3">
      <div class="flex-1">
        <div class="flex items-center gap-2 mb-1">
          <span class="text-sm font-medium text-gray-800 dark:text-gray-200">${q.name}</span>
          <span class="text-xs px-2 py-0.5 rounded ${statusColor}">${statusText}</span>
        </div>
        ${q.status === 'indexing' ? `
        <div class="w-full bg-gray-200 dark:bg-gray-700 rounded-full h-1.5 overflow-hidden">
          <div class="bg-primary h-1.5 rounded-full transition-all duration-300" style="width:${q.progress}%"></div>
        </div>
        ` : ''}
      </div>
      ${q.status === 'pending' ? `
      <button onclick="removeFromQueue(${i})" class="p-1 rounded hover:bg-red-50 dark:hover:bg-red-900/20 text-gray-400 hover:text-error transition-colors">
        <i data-lucide="x" class="w-4 h-4"></i>
      </button>
      ` : ''}
    </div>`;
  }).join('');
  lucide.createIcons();
}

function removeFromQueue(index) {
  indexingQueue.splice(index, 1);
  renderQueue();
}

function clearQueue() {
  if (confirm('Clear all pending items from the queue?')) {
    indexingQueue = indexingQueue.filter(q => q.status === 'indexing');
    renderQueue();
  }
}

function startBatchIndexing() {
  const parallel = parseInt(document.getElementById('parallel-count').value) || 5;
  const pending = indexingQueue.filter(q => q.status === 'pending');
  if (pending.length === 0) return;

  let active = 0;
  let index = 0;

  function processNext() {
    if (index >= pending.length) return;
    if (active >= parallel) return;

    const item = pending[index++];
    item.status = 'indexing';
    item.progress = 0;
    active++;
    renderQueue();

    // Simulate indexing
    const interval = setInterval(() => {
      item.progress += 10;
      if (item.progress >= 100) {
        clearInterval(interval);
        item.status = 'done';
        item.progress = 100;
        active--;
        renderQueue();
        processNext();
      } else {
        renderQueue();
      }
    }, 200);

    processNext();
  }

  processNext();
}

function simulateUpload() {
  alert('File picker would open here. Selected file will be indexed into the current knowledge base.');
}

// ===== Debug Page: KB Sidebar =====
function renderDebugKbList() {
  const container = document.getElementById('debug-kb-list');
  container.innerHTML = knowledgeBases.map(kb => {
    const checked = debugSelectedKbs.has(kb.id);
    return `
    <label class="flex items-center gap-2.5 px-2 py-2 rounded hover:bg-gray-100 dark:hover:bg-gray-800 cursor-pointer transition-colors">
      <input type="checkbox" ${checked ? 'checked' : ''} onchange="toggleDebugKb('${kb.id}', this.checked)" class="w-4 h-4 rounded border-gray-300 text-primary accent-primary">
      <div class="flex-1 min-w-0">
        <div class="text-sm font-medium text-gray-800 dark:text-gray-200 truncate">${kb.name}</div>
        <div class="text-xs text-gray-400 dark:text-gray-500">${kb.docs} docs · ${kb.chunks} chunks</div>
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
  { score: 0.94, source: 'api-reference.md', section: 'Authentication > OAuth2 Flow', kb: 'Product Documentation',
    text: 'The OAuth2 authentication flow requires a valid client_id and client_secret. Tokens are issued with a default TTL of 3600 seconds.' },
  { score: 0.72, source: 'getting-started.md', section: 'Installation', kb: 'Product Documentation',
    text: 'To install the product, run: npm install @product/core. Make sure you have Node.js 18+ installed.' },
];

function doSearch() {
  const query = document.getElementById('search-input').value.trim();
  if (!query || debugSelectedKbs.size === 0) return;
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
      <div class="border border-gray-200 dark:border-gray-700 rounded-lg overflow-hidden bg-surface dark:bg-surface-dark">
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
lucide.createIcons();
