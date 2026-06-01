const data = window.collectionEditorData;
const originalServices = JSON.parse(JSON.stringify(data.services));

const state = {
  mode: 'dictionary',
  selectedServiceKey: 'billing',
  selectedDbKey: 'main',
  staged: [],
  filter: ''
};

const els = {
  modeTabs: [...document.querySelectorAll('.mode-tab')],
  activePath: document.getElementById('active-path'),
  pathRail: document.getElementById('path-rail'),
  list: document.getElementById('collection-list'),
  editor: document.getElementById('editor-panel'),
  patchList: document.getElementById('patch-list'),
  stagedCount: document.getElementById('staged-count'),
  navigatorKicker: document.getElementById('navigator-kicker'),
  navigatorTitle: document.getElementById('navigator-title'),
  addItem: document.getElementById('add-item'),
  clearStaged: document.getElementById('clear-staged'),
  search: document.getElementById('collection-search'),
  footerSummary: document.getElementById('footer-summary'),
  footerDetail: document.getElementById('footer-detail'),
  stageButton: document.getElementById('stage-button')
};

function selectedService() {
  return data.services.find(service => service.key === state.selectedServiceKey) ?? data.services[0];
}

function selectedDb() {
  const service = selectedService();
  return service.connectedDbs.find(db => db.key === state.selectedDbKey) ?? service.connectedDbs[0];
}

function originalService(serviceKey = state.selectedServiceKey) {
  return originalServices.find(service => service.key === serviceKey);
}

function originalDb(serviceKey = state.selectedServiceKey, dbKey = state.selectedDbKey) {
  return originalService(serviceKey)?.connectedDbs.find(db => db.key === dbKey);
}

function pathForService(serviceKey = state.selectedServiceKey) {
  return `Services[$${serviceKey}]`;
}

function pathForDb(dbKey = state.selectedDbKey) {
  return `${pathForService()}.ConnectedDbs[#${dbKey}]`;
}

function setMode(mode) {
  state.mode = mode;
  els.modeTabs.forEach(tab => tab.classList.toggle('active', tab.dataset.mode === mode));
  render();
}

function setPath(path) {
  els.activePath.textContent = path;
  els.pathRail.classList.remove('pulse');
  requestAnimationFrame(() => els.pathRail.classList.add('pulse'));
}

function addStage(kind, path, oldValue, newValue, label) {
  const existingIndex = state.staged.findIndex(item => item.path === path && item.kind === kind);
  const entry = { kind, path, oldValue, newValue, label };
  if (existingIndex >= 0) {
    state.staged[existingIndex] = entry;
  } else {
    state.staged.unshift(entry);
  }
  renderPatch();
}

function render() {
  syncModeTabs();

  if (state.mode === 'dictionary') {
    renderDictionary();
  } else if (state.mode === 'list') {
    renderList();
  } else if (state.mode === 'json') {
    renderJson();
  } else {
    renderPatchMode();
  }
  renderPatch();
  lucide.createIcons();
}

function syncModeTabs() {
  els.modeTabs.forEach(tab => tab.classList.toggle('active', tab.dataset.mode === state.mode));
}

function renderDictionary() {
  els.navigatorKicker.textContent = 'Dictionary keys';
  els.navigatorTitle.textContent = 'Services';
  els.addItem.innerHTML = '<i data-lucide="plus"></i>Add key';
  els.addItem.disabled = false;
  setPath(pathForService());

  const filtered = data.services.filter(service =>
    `${service.key} ${service.displayName}`.toLowerCase().includes(state.filter.toLowerCase()));

  els.list.innerHTML = filtered.map(service => `
    <button class="collection-item ${service.key === state.selectedServiceKey ? 'active' : ''} ${isPathStaged(pathForService(service.key)) ? 'pending' : ''}"
            data-select-service="${service.key}">
      <div class="item-topline">
        <strong>${service.displayName}</strong>
        <span class="chip ${service.enabled ? 'cyan' : 'amber'}">${service.enabled ? 'Enabled' : 'Disabled'}</span>
      </div>
      <div class="item-subtitle">${service.key} · ${service.connectedDbs.length} connected DBs</div>
    </button>
  `).join('');

  const service = selectedService();
  els.editor.innerHTML = `
    <article class="editor-card">
      <header class="editor-card-header">
        <div>
          <p class="eyebrow">Dictionary item</p>
          <h3>${service.displayName}</h3>
          <p class="muted">${pathForService(service.key)}</p>
        </div>
        <button class="small-action danger-button" data-remove-service="${service.key}">
          <i data-lucide="trash-2"></i>Remove key
        </button>
      </header>
      <div class="editor-card-body">
        <div class="field-grid">
          ${textField('Key', 'key', service.key, true)}
          ${textField('Display name', 'displayName', service.displayName)}
          ${textField('Base URL', 'baseUrl', service.baseUrl)}
          ${numberField('Timeout seconds', 'timeoutSeconds', service.timeoutSeconds)}
          ${selectField('Deployment slot', 'deploymentSlot', service.deploymentSlot, ['Production', 'Staging', 'Canary'])}
          ${toggleField('Enabled', 'enabled', service.enabled)}
        </div>
        <div>
          <div class="field-row mb-2">
            <div>
              <p class="eyebrow">Nested keyed list</p>
              <h3>Connected DBs</h3>
            </div>
            <button class="small-action" data-open-list><i data-lucide="list-tree"></i>Edit list</button>
          </div>
          <div class="nested-strip">
            ${service.connectedDbs.map(db => `<span class="nested-chip">#${db.key} · ${db.provider}</span>`).join('')}
          </div>
        </div>
      </div>
    </article>
  `;
}

function renderList() {
  const service = selectedService();
  if (!service.connectedDbs.some(db => db.key === state.selectedDbKey)) {
    state.selectedDbKey = service.connectedDbs[0]?.key ?? '';
  }

  els.navigatorKicker.textContent = 'Stable list item keys';
  els.navigatorTitle.textContent = `${service.displayName} DBs`;
  els.addItem.innerHTML = '<i data-lucide="plus"></i>Add item';
  els.addItem.disabled = false;
  setPath(state.selectedDbKey ? pathForDb() : `${pathForService()}.ConnectedDbs`);

  const filtered = service.connectedDbs.filter(db =>
    `${db.key} ${db.role} ${db.provider}`.toLowerCase().includes(state.filter.toLowerCase()));

  els.list.innerHTML = filtered.map((db, index) => `
    <button class="collection-item ${db.key === state.selectedDbKey ? 'active' : ''} ${isPathStaged(pathForDb(db.key)) ? 'pending' : ''}"
            data-select-db="${db.key}">
      <div class="item-topline">
        <strong>#${db.key}</strong>
        <span class="chip cyan">${db.provider}</span>
      </div>
      <div class="item-subtitle">${index + 1}. ${db.role} · ${db.readOnly ? 'read only' : 'read/write'}</div>
    </button>
  `).join('');

  const db = selectedDb();
  els.editor.innerHTML = `
    <article class="editor-card">
      <header class="editor-card-header">
        <div>
          <p class="eyebrow">Keyed list item</p>
          <h3>${db.role}</h3>
          <p class="muted">${pathForDb(db.key)}</p>
        </div>
        <div class="footer-actions">
          <button class="small-action ghost" data-move-db="-1"><i data-lucide="arrow-up"></i>Move up</button>
          <button class="small-action ghost" data-move-db="1"><i data-lucide="arrow-down"></i>Move down</button>
          <button class="small-action danger-button" data-remove-db="${db.key}"><i data-lucide="trash-2"></i>Remove</button>
        </div>
      </header>
      <div class="editor-card-body">
        <div class="field-grid">
          ${textField('Item key', 'key', db.key, true)}
          ${textField('Role', 'role', db.role)}
          ${selectField('Provider', 'provider', db.provider, ['SqlServer', 'Postgres', 'Sqlite', 'MySql'])}
          ${numberField('Max pool size', 'maxPoolSize', db.maxPoolSize)}
          ${textField('Connection string', 'connectionString', db.connectionString)}
          ${toggleField('Read only', 'readOnly', db.readOnly)}
        </div>
      </div>
    </article>
  `;
}

function renderJson() {
  els.navigatorKicker.textContent = 'Advanced';
  els.navigatorTitle.textContent = 'Raw subtree';
  els.addItem.innerHTML = '<i data-lucide="file-warning"></i>Replace';
  els.addItem.disabled = false;
  setPath('Services');
  els.list.innerHTML = `
    <div class="empty-state">
      Raw JSON creates an explicit Replace mutation for the selected subtree.
    </div>
  `;
  els.editor.innerHTML = `
    <article class="editor-card">
      <header class="editor-card-header">
        <div>
          <p class="eyebrow">Advanced fallback</p>
          <h3>Full subtree JSON</h3>
          <p class="muted">Use when a visual editor cannot express the shape.</p>
        </div>
        <button class="small-action" data-stage-json><i data-lucide="save"></i>Stage replace</button>
      </header>
      <div class="editor-card-body">
        <textarea class="json-block" id="raw-json">${JSON.stringify(data.rawJson, null, 2)}</textarea>
      </div>
    </article>
  `;
}

function renderPatchMode() {
  els.navigatorKicker.textContent = 'Audit';
  els.navigatorTitle.textContent = 'Mutations';
  els.addItem.innerHTML = '<i data-lucide="badge-info"></i>Info';
  els.addItem.disabled = true;
  setPath('Pending mutation group');
  els.list.innerHTML = state.staged.length
    ? state.staged.map(item => `
      <button class="collection-item pending">
        <div class="item-topline"><strong>${item.kind}</strong><span class="chip cyan">ready</span></div>
        <div class="item-subtitle">${item.path}</div>
      </button>
    `).join('')
    : '<div class="empty-state">No staged mutations yet.</div>';
  els.editor.innerHTML = `
    <article class="editor-card">
      <header class="editor-card-header">
        <div>
          <p class="eyebrow">Review before staging</p>
          <h3>Patch Preview</h3>
          <p class="muted">Every visual edit becomes a concrete mutation path.</p>
        </div>
      </header>
      <div class="editor-card-body">
        ${renderPatchCards(true)}
      </div>
    </article>
  `;
}

function renderPatch() {
  els.stagedCount.textContent = state.staged.length;
  els.patchList.innerHTML = renderPatchCards(false);
  els.footerSummary.textContent = state.staged.length
    ? `${state.staged.length} staged mutation${state.staged.length === 1 ? '' : 's'}`
    : 'No staged changes';
  els.footerDetail.textContent = state.staged.length
    ? 'Review patch preview, then stage into the configuration mutation group.'
    : 'Visual edits stage granular logical-path mutations.';
  els.stageButton.disabled = state.staged.length === 0;
  els.stageButton.style.opacity = state.staged.length === 0 ? '0.6' : '1';
}

function renderPatchCards(expanded) {
  if (!state.staged.length) {
    return '<div class="empty-state">Changes will appear here as Set, Remove, or Replace operations.</div>';
  }

  return state.staged.map(item => `
    <div class="patch-card ${item.kind.toLowerCase()}">
      <div class="patch-meta">
        <strong>${item.kind}</strong>
        <span class="chip ${item.kind === 'Remove' ? 'amber' : item.kind === 'Set' ? 'cyan' : 'green'}">${item.label}</span>
      </div>
      <code>${item.path}</code>
      ${expanded ? `
        <pre class="mt-3 text-sm text-slate-300">old: ${JSON.stringify(item.oldValue, null, 2)}
new: ${JSON.stringify(item.newValue, null, 2)}</pre>
      ` : ''}
    </div>
  `).join('');
}

function textField(label, field, value, readonly = false) {
  return `
    <div class="form-field">
      <label>${label}</label>
      <input data-field="${field}" value="${escapeHtml(value)}" ${readonly ? 'readonly' : ''}>
    </div>
  `;
}

function numberField(label, field, value) {
  return `
    <div class="form-field">
      <label>${label}</label>
      <input type="number" data-field="${field}" value="${value}">
    </div>
  `;
}

function selectField(label, field, value, options) {
  return `
    <div class="form-field">
      <label>${label}</label>
      <select data-field="${field}">
        ${options.map(option => `<option ${option === value ? 'selected' : ''}>${option}</option>`).join('')}
      </select>
    </div>
  `;
}

function toggleField(label, field, value) {
  return `
    <div class="form-field">
      <label>${label}</label>
      <div class="toggle-row">
        <button class="switch ${value ? 'on' : ''}" data-toggle="${field}" aria-label="${label}"></button>
        <span>${value ? 'Enabled' : 'Disabled'}</span>
      </div>
    </div>
  `;
}

function handleFieldChange(target) {
  const field = target.dataset.field;
  if (!field) return;

  if (state.mode === 'dictionary') {
    const service = selectedService();
    const oldValue = originalService(service.key)?.[field] ?? service[field];
    const newValue = target.type === 'number' ? Number(target.value) : target.value;
    service[field] = newValue;
    addStage('Set', `${pathForService()}.${toPascal(field)}`, oldValue, newValue, `Service ${service.key}`);
    return;
  }

  if (state.mode === 'list') {
    const db = selectedDb();
    const oldValue = originalDb(state.selectedServiceKey, db.key)?.[field] ?? db[field];
    const newValue = target.type === 'number' ? Number(target.value) : target.value;
    db[field] = newValue;
    addStage('Set', `${pathForDb()}.${toPascal(field)}`, oldValue, newValue, `DB #${db.key}`);
  }
}

function handleToggle(target) {
  const field = target.dataset.toggle;
  if (!field) return;

  if (state.mode === 'dictionary') {
    const service = selectedService();
    const oldValue = originalService(service.key)?.[field] ?? service[field];
    service[field] = !service[field];
    addStage('Set', `${pathForService()}.${toPascal(field)}`, oldValue, service[field], `Service ${service.key}`);
  } else if (state.mode === 'list') {
    const db = selectedDb();
    const oldValue = originalDb(state.selectedServiceKey, db.key)?.[field] ?? db[field];
    db[field] = !db[field];
    addStage('Set', `${pathForDb()}.${toPascal(field)}`, oldValue, db[field], `DB #${db.key}`);
  }

  render();
}

function addCurrentModeItem() {
  if (state.mode === 'dictionary') {
    const nextIndex = data.services.length + 1;
    const key = `service-${nextIndex}`;
    const service = {
      key,
      displayName: `New Service ${nextIndex}`,
      enabled: false,
      baseUrl: 'https://new-service.internal.monica.local',
      timeoutSeconds: 10,
      deploymentSlot: 'Staging',
      tags: [],
      connectedDbs: []
    };
    data.services.push(service);
    state.selectedServiceKey = key;
    addStage('Set', pathForService(key), null, service, `New key ${key}`);
  } else if (state.mode === 'list') {
    const service = selectedService();
    const nextIndex = service.connectedDbs.length + 1;
    const key = `db-${nextIndex}`;
    const db = {
      key,
      role: `Database ${nextIndex}`,
      provider: 'SqlServer',
      connectionString: 'Server=new-db;Database=App;',
      maxPoolSize: 50,
      readOnly: false
    };
    service.connectedDbs.push(db);
    state.selectedDbKey = key;
    addStage('Set', pathForDb(key), null, db, `New item #${key}`);
  } else if (state.mode === 'json') {
    stageJsonReplace();
  }
  render();
}

function removeService(key) {
  const index = data.services.findIndex(service => service.key === key);
  if (index < 0) return;
  const [removed] = data.services.splice(index, 1);
  state.selectedServiceKey = data.services[Math.max(0, index - 1)]?.key ?? '';
  addStage('Remove', pathForService(key), removed, null, `Remove ${key}`);
  render();
}

function removeDb(key) {
  const service = selectedService();
  const index = service.connectedDbs.findIndex(db => db.key === key);
  if (index < 0) return;
  const [removed] = service.connectedDbs.splice(index, 1);
  state.selectedDbKey = service.connectedDbs[Math.max(0, index - 1)]?.key ?? '';
  addStage('Remove', pathForDb(key), removed, null, `Remove #${key}`);
  render();
}

function moveDb(direction) {
  const service = selectedService();
  const index = service.connectedDbs.findIndex(db => db.key === state.selectedDbKey);
  const next = index + direction;
  if (index < 0 || next < 0 || next >= service.connectedDbs.length) return;
  const [item] = service.connectedDbs.splice(index, 1);
  service.connectedDbs.splice(next, 0, item);
  addStage(
    'Replace',
    `${pathForService()}.ConnectedDbs`,
    'previous order',
    service.connectedDbs.map(db => db.key),
    'Reorder list');
  render();
}

function stageJsonReplace() {
  const textarea = document.getElementById('raw-json');
  if (!textarea) return;
  try {
    const parsed = JSON.parse(textarea.value);
    addStage('Replace', 'Services', data.rawJson, parsed, 'Raw JSON');
  } catch {
    textarea.style.borderColor = 'var(--red)';
  }
  renderPatch();
}

function isPathStaged(path) {
  return state.staged.some(item => item.path === path || item.path.startsWith(`${path}.`));
}

function toPascal(value) {
  return value.charAt(0).toUpperCase() + value.slice(1);
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}

document.addEventListener('click', event => {
  const target = event.target.closest('button');
  if (!target) return;

  if (target.dataset.mode) {
    setMode(target.dataset.mode);
  } else if (target.dataset.selectService) {
    state.selectedServiceKey = target.dataset.selectService;
    state.mode = 'dictionary';
    render();
  } else if (target.dataset.selectDb) {
    state.selectedDbKey = target.dataset.selectDb;
    render();
  } else if (target.dataset.openList !== undefined) {
    state.mode = 'list';
    render();
  } else if (target.dataset.removeService) {
    removeService(target.dataset.removeService);
  } else if (target.dataset.removeDb) {
    removeDb(target.dataset.removeDb);
  } else if (target.dataset.moveDb) {
    moveDb(Number(target.dataset.moveDb));
  } else if (target.dataset.toggle) {
    handleToggle(target);
  } else if (target.dataset.stageJson !== undefined) {
    stageJsonReplace();
  } else if (target.id === 'add-item') {
    addCurrentModeItem();
  } else if (target.id === 'clear-staged') {
    state.staged = [];
    renderPatch();
    render();
  }
});

document.addEventListener('input', event => {
  if (event.target.id === 'collection-search') {
    state.filter = event.target.value;
    render();
    els.search.value = state.filter;
    return;
  }
  handleFieldChange(event.target);
});

render();
