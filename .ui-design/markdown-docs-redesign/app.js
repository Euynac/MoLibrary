(function () {
  const data = window.markdownDocsPrototypeData;
  const expandedFoldersByGroup = new Map();
  const state = {
    currentGroupId: data.defaultGroupId,
    currentDocId: data.defaultDocId,
    treeQuery: '',
    searchQuery: '',
    activeHeadingId: null,
    showToc: true,
    groupMenuOpen: false,
    searchOverlayOpen: false,
    isDesktop: window.matchMedia('(min-width: 1024px)').matches,
    sidebarVisible: window.matchMedia('(min-width: 1024px)').matches
  };

  let sectionObserver = null;

  const dom = {};

  document.addEventListener('DOMContentLoaded', init);

  function init() {
    cacheDom();
    seedExpandedFolders();
    bindEvents();

    const currentDoc = getCurrentDoc();
    state.activeHeadingId = currentDoc?.sections?.[0]?.id ?? null;

    renderAll(true);
  }

  function cacheDom() {
    dom.groupSwitcherButton = document.getElementById('groupSwitcherButton');
    dom.groupMenu = document.getElementById('groupMenu');
    dom.groupBadge = document.getElementById('groupBadge');
    dom.groupName = document.getElementById('groupName');
    dom.groupDescription = document.getElementById('groupDescription');
    dom.treeSearchInput = document.getElementById('treeSearchInput');
    dom.treeRoot = document.getElementById('treeRoot');
    dom.treeDocCount = document.getElementById('treeDocCount');
    dom.sidebarDockToggle = document.getElementById('sidebarDockToggle');
    dom.sidebarFooterToggle = document.getElementById('sidebarFooterToggle');
    dom.sidebarScrim = document.getElementById('sidebarScrim');
    dom.breadcrumbs = document.getElementById('breadcrumbs');
    dom.searchTrigger = document.getElementById('searchTrigger');
    dom.tocToggle = document.getElementById('tocToggle');
    dom.updateStamp = document.getElementById('updateStamp');
    dom.articleScroll = document.getElementById('articleScroll');
    dom.articleBody = document.getElementById('articleBody');
    dom.tocCard = document.getElementById('tocCard');
    dom.tocList = document.getElementById('tocList');
    dom.searchOverlay = document.getElementById('searchOverlay');
    dom.searchBackdrop = document.getElementById('searchBackdrop');
    dom.searchClose = document.getElementById('searchClose');
    dom.globalSearchInput = document.getElementById('globalSearchInput');
    dom.searchResults = document.getElementById('searchResults');
  }

  function seedExpandedFolders() {
    data.groups.forEach(group => {
      expandedFoldersByGroup.set(group.id, new Set(collectFolderIds(group.tree)));
    });
  }

  function collectFolderIds(nodes, result = []) {
    nodes.forEach(node => {
      if (node.type !== 'folder') {
        return;
      }

      result.push(node.id);
      collectFolderIds(node.children ?? [], result);
    });

    return result;
  }

  function bindEvents() {
    dom.groupSwitcherButton.addEventListener('click', () => {
      state.groupMenuOpen = !state.groupMenuOpen;
      renderGroupSwitcher();
    });

    dom.groupMenu.addEventListener('click', event => {
      const option = event.target.closest('[data-group-id]');
      if (!option) {
        return;
      }

      const groupId = option.dataset.groupId;
      if (groupId && groupId !== state.currentGroupId) {
        state.currentGroupId = groupId;
        const nextDoc = findFirstDocumentId(getCurrentGroup().tree);
        if (nextDoc) {
          state.currentDocId = nextDoc;
          state.activeHeadingId = getCurrentDoc()?.sections?.[0]?.id ?? null;
        }
      }

      state.groupMenuOpen = false;
      renderAll(true);
    });

    dom.treeSearchInput.addEventListener('input', event => {
      state.treeQuery = event.target.value.trim();
      renderTree();
    });

    dom.treeRoot.addEventListener('click', event => {
      const folderButton = event.target.closest('[data-folder-id]');
      if (folderButton) {
        const folderId = folderButton.dataset.folderId;
        toggleFolder(folderId);
        renderTree();
        return;
      }

      const docButton = event.target.closest('[data-doc-id]');
      if (!docButton) {
        return;
      }

      const docId = docButton.dataset.docId;
      if (docId) {
        selectDocument(docId);
      }
    });

    dom.sidebarDockToggle.addEventListener('click', toggleSidebar);
    dom.sidebarFooterToggle.addEventListener('click', toggleSidebar);
    dom.sidebarScrim.addEventListener('click', () => {
      if (!state.isDesktop) {
        state.sidebarVisible = false;
        syncBodyState();
      }
    });

    dom.tocToggle.addEventListener('click', () => {
      state.showToc = !state.showToc;
      renderToolbar();
      syncBodyState();

      if (state.showToc) {
        window.requestAnimationFrame(updateTocRail);
      }
    });

    dom.searchTrigger.addEventListener('click', openSearchOverlay);
    dom.searchClose.addEventListener('click', closeSearchOverlay);
    dom.searchBackdrop.addEventListener('click', closeSearchOverlay);

    dom.globalSearchInput.addEventListener('input', event => {
      state.searchQuery = event.target.value.trim();
      renderSearchResults();
    });

    dom.searchResults.addEventListener('click', event => {
      const resultButton = event.target.closest('[data-doc-id]');
      if (!resultButton) {
        return;
      }

      const docId = resultButton.dataset.docId;
      if (docId) {
        selectDocument(docId);
        closeSearchOverlay();
      }
    });

    dom.tocList.addEventListener('click', event => {
      const button = event.target.closest('[data-heading-id]');
      if (!button) {
        return;
      }

      const headingId = button.dataset.headingId;
      if (!headingId) {
        return;
      }

      const target = dom.articleBody.querySelector(`#${CSS.escape(headingId)}`);
      if (target) {
        target.scrollIntoView({ behavior: 'smooth', block: 'start' });
        state.activeHeadingId = headingId;
        renderBreadcrumbs();
        renderToc();
      }
    });

    document.addEventListener('keydown', event => {
      const isShortcut = (event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k';
      const isSlashShortcut = event.key === '/' && !isInputLike(event.target);

      if (isShortcut || isSlashShortcut) {
        event.preventDefault();
        openSearchOverlay();
        return;
      }

      if (event.key === 'Escape') {
        if (state.searchOverlayOpen) {
          closeSearchOverlay();
          return;
        }

        if (state.groupMenuOpen) {
          state.groupMenuOpen = false;
          renderGroupSwitcher();
          return;
        }

        if (!state.isDesktop && state.sidebarVisible) {
          state.sidebarVisible = false;
          syncBodyState();
        }
      }
    });

    document.addEventListener('click', event => {
      if (!event.target.closest('.group-switcher-wrap')) {
        if (state.groupMenuOpen) {
          state.groupMenuOpen = false;
          renderGroupSwitcher();
        }
      }
    });

    window.addEventListener('resize', handleResize);
  }

  function handleResize() {
    const nextDesktop = window.matchMedia('(min-width: 1024px)').matches;
    if (nextDesktop !== state.isDesktop) {
      state.isDesktop = nextDesktop;
      state.sidebarVisible = nextDesktop;
      syncBodyState();
    }

    window.requestAnimationFrame(updateTocRail);
  }

  function toggleSidebar() {
    state.sidebarVisible = !state.sidebarVisible;
    syncBodyState();
  }

  function openSearchOverlay() {
    state.searchOverlayOpen = true;
    syncBodyState();
    renderSearchResults();

    window.setTimeout(() => {
      dom.globalSearchInput.focus();
      dom.globalSearchInput.select();
    }, 30);
  }

  function closeSearchOverlay() {
    state.searchOverlayOpen = false;
    syncBodyState();
  }

  function syncBodyState() {
    document.body.classList.toggle('is-search-open', state.searchOverlayOpen);
    document.body.classList.toggle('is-toc-hidden', !state.showToc);
    document.body.classList.toggle('is-sidebar-visible', state.sidebarVisible);
    dom.searchOverlay.setAttribute('aria-hidden', String(!state.searchOverlayOpen));
  }

  function renderAll(resetScroll) {
    renderGroupSwitcher();
    renderTree();
    renderToolbar();
    renderArticle(resetScroll);
    renderToc();
    renderSearchResults();
    syncBodyState();
    refreshIcons();
  }

  function renderGroupSwitcher() {
    const group = getCurrentGroup();
    dom.groupBadge.textContent = group.badge;
    dom.groupName.textContent = group.name;
    dom.groupDescription.textContent = group.description;
    dom.treeDocCount.textContent = `${countDocs(group.tree)} docs`;

    dom.groupMenu.classList.toggle('is-open', state.groupMenuOpen);
    dom.groupMenu.innerHTML = data.groups.map(item => `
      <button class="group-option ${item.id === state.currentGroupId ? 'is-active' : ''}" data-group-id="${item.id}" type="button">
        <span class="group-option-badge">${escapeHtml(item.badge)}</span>
        <span class="group-option-name">${escapeHtml(item.name)}</span>
        <span class="group-option-description">${escapeHtml(item.description)}</span>
      </button>
    `).join('');
  }

  function renderTree() {
    const group = getCurrentGroup();
    const filteredNodes = filterNodes(group.tree, state.treeQuery);

    if (!filteredNodes.length) {
      dom.treeRoot.innerHTML = `
        <div class="tree-empty">
          <p>No documents match <strong>${escapeHtml(state.treeQuery)}</strong>.</p>
        </div>
      `;
      refreshIcons();
      return;
    }

    dom.treeRoot.innerHTML = renderTreeNodes(filteredNodes, 0, Boolean(state.treeQuery));
    refreshIcons();
  }

  function renderTreeNodes(nodes, depth, forceExpand) {
    return nodes.map(node => {
      if (node.type === 'folder') {
        const expanded = forceExpand || Boolean(node.forceOpen) || isFolderExpanded(node.id);
        const childDocs = countDocs(node.children ?? []);

        return `
          <div class="tree-node">
            <button class="tree-row tree-folder-row ${node.isMatch ? 'is-matched' : ''}"
                    data-folder-id="${node.id}"
                    style="--depth:${depth};"
                    type="button"
                    aria-expanded="${expanded}">
              <span class="tree-caret">
                <i data-lucide="${expanded ? 'chevron-down' : 'chevron-right'}" class="icon-16"></i>
              </span>
              <span class="tree-icon">
                <i data-lucide="folder" class="icon-16"></i>
              </span>
              <span class="tree-label-shell">
                <span class="tree-label">${escapeHtml(node.label)}</span>
                <span class="tree-tag">${childDocs}</span>
              </span>
            </button>
            ${expanded ? `<div class="tree-children">${renderTreeNodes(node.children ?? [], depth + 1, forceExpand || Boolean(node.forceOpen))}</div>` : ''}
          </div>
        `;
      }

      const doc = data.documents[node.docId];
      const isActive = node.docId === state.currentDocId;

      return `
        <button class="tree-row tree-doc-row ${isActive ? 'is-active' : ''} ${node.isMatch ? 'is-matched' : ''}"
                data-doc-id="${node.docId}"
                style="--depth:${depth};"
                type="button">
          <span class="tree-icon">
            <i data-lucide="file-text" class="icon-16"></i>
          </span>
          <span class="tree-label-shell">
            <span class="tree-label">${escapeHtml(doc.title)}</span>
            ${isActive ? '<span class="tree-tag">Open</span>' : ''}
          </span>
        </button>
      `;
    }).join('');
  }

  function renderToolbar() {
    const doc = getCurrentDoc();
    const sidebarIcon = state.sidebarVisible ? 'panel-left-close' : 'panel-left-open';
    const sidebarTitle = state.sidebarVisible ? 'Hide navigation' : 'Show navigation';

    dom.sidebarDockToggle.innerHTML = `
      <i data-lucide="panel-left-open" class="icon-16"></i>
      <span>Library map</span>
    `;
    dom.sidebarDockToggle.title = sidebarTitle;
    dom.sidebarFooterToggle.innerHTML = `<i data-lucide="${sidebarIcon}" class="icon-16"></i>`;
    dom.sidebarFooterToggle.title = sidebarTitle;

    dom.tocToggle.classList.toggle('is-active', state.showToc);
    dom.tocToggle.querySelector('span').textContent = state.showToc ? 'Hide outline' : 'Show outline';
    dom.updateStamp.textContent = `${doc.updated} · ${doc.readingTime}`;

    renderBreadcrumbs();
    refreshIcons();
  }

  function renderBreadcrumbs() {
    const group = getCurrentGroup();
    const doc = getCurrentDoc();
    const activeSection = doc.sections.find(section => section.id === state.activeHeadingId);
    const items = [group.name, ...doc.path];

    if (activeSection && activeSection.title !== doc.title) {
      items.push(activeSection.title);
    }

    const breadcrumbParts = [
      '<span class="breadcrumb-item breadcrumb-home"><i data-lucide="house" class="icon-16"></i></span>',
      '<span class="breadcrumb-separator">/</span>'
    ];

    items.forEach((item, index) => {
      breadcrumbParts.push(`
        <span class="breadcrumb-item ${index === items.length - 1 ? 'is-current' : ''}">${escapeHtml(item)}</span>
      `);

      if (index < items.length - 1) {
        breadcrumbParts.push('<span class="breadcrumb-separator">/</span>');
      }
    });

    dom.breadcrumbs.innerHTML = breadcrumbParts.join('');
  }

  function renderArticle(resetScroll) {
    const doc = getCurrentDoc();

    dom.articleBody.innerHTML = `
      <div class="article-document">
        <p class="doc-eyebrow">${escapeHtml(doc.eyebrow)}</p>
        <h1 class="doc-title">${escapeHtml(doc.title)}</h1>
        <p class="doc-summary">${escapeHtml(doc.summary)}</p>

        <div class="doc-meta-grid">
          ${doc.meta.map(item => `
            <div class="doc-chip">
              <span class="doc-chip-label">${escapeHtml(item.label)}</span>
              <span class="doc-chip-value">${escapeHtml(item.value)}</span>
            </div>
          `).join('')}
        </div>

        <div class="doc-note">${escapeHtml(doc.note)}</div>

        ${doc.sections.map(section => renderSection(section)).join('')}
      </div>
    `;

    if (resetScroll) {
      dom.articleScroll.scrollTop = 0;
    }

    setupSectionObserver();
    refreshIcons();
  }

  function renderSection(section) {
    return `
      <section class="article-section" id="${section.id}">
        <h2 class="article-heading level-${section.level}">${escapeHtml(section.title)}</h2>
        ${section.paragraphs.map(paragraph => `<p class="article-paragraph">${escapeHtml(paragraph)}</p>`).join('')}
        ${section.points ? `
          <ul class="article-list">
            ${section.points.map(point => `<li>${escapeHtml(point)}</li>`).join('')}
          </ul>
        ` : ''}
        ${section.code ? `
          <div class="code-block">
            <div class="code-block-label">${escapeHtml(section.codeTitle || 'Example')}</div>
            <pre><code>${escapeHtml(section.code.join('\n'))}</code></pre>
          </div>
        ` : ''}
      </section>
    `;
  }

  function setupSectionObserver() {
    if (sectionObserver) {
      sectionObserver.disconnect();
      sectionObserver = null;
    }

    const sections = Array.from(dom.articleBody.querySelectorAll('.article-section'));
    if (!sections.length) {
      return;
    }

    sectionObserver = new IntersectionObserver(entries => {
      const visible = entries
        .filter(entry => entry.isIntersecting)
        .sort((a, b) => b.intersectionRatio - a.intersectionRatio || a.boundingClientRect.top - b.boundingClientRect.top);

      if (!visible.length) {
        return;
      }

      const nextHeadingId = visible[0].target.id;
      if (nextHeadingId === state.activeHeadingId) {
        return;
      }

      state.activeHeadingId = nextHeadingId;
      renderBreadcrumbs();
      renderToc();
    }, {
      root: dom.articleScroll,
      rootMargin: '-14% 0px -58% 0px',
      threshold: [0.25, 0.45, 0.7]
    });

    sections.forEach(section => sectionObserver.observe(section));
  }

  function renderToc() {
    const doc = getCurrentDoc();
    const headings = doc.sections.filter(section => section.level <= 3);

    if (!headings.length) {
      dom.tocList.innerHTML = `
        <div class="toc-empty">
          This document has no outlineable headings yet. The custom Monica TOC would stay hidden or show a light empty state.
        </div>
      `;
      setTocRailState(null);
      return;
    }

    dom.tocList.innerHTML = headings.map((section, index) => `
      <div class="toc-item ${section.id === state.activeHeadingId ? 'is-active' : ''}">
        <button class="toc-item-button"
                data-heading-id="${section.id}"
                style="--toc-level:${section.level};"
                type="button">
          <span class="toc-copy">${escapeHtml(section.title)}</span>
        </button>
      </div>
    `).join('');

    window.requestAnimationFrame(updateTocRail);
  }

  function updateTocRail() {
    const activeItem = dom.tocList.querySelector('.toc-item.is-active');
    if (!activeItem) {
      setTocRailState(null);
      return;
    }

    const top = activeItem.offsetTop + 8;
    const height = Math.max(30, activeItem.offsetHeight - 16);
    setTocRailState({ top, height });
    ensureTocActiveItemVisible(activeItem);
  }

  function ensureTocActiveItemVisible(activeItem) {
    const viewportTop = dom.tocList.scrollTop;
    const viewportBottom = viewportTop + dom.tocList.clientHeight;
    const itemTop = activeItem.offsetTop;
    const itemBottom = itemTop + activeItem.offsetHeight;
    const padding = 18;

    if (itemTop < viewportTop + padding || itemBottom > viewportBottom - padding) {
      activeItem.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    }
  }

  function setTocRailState(metrics) {
    if (!metrics) {
      dom.tocList.style.setProperty('--toc-active-opacity', '0');
      return;
    }

    dom.tocList.style.setProperty('--toc-active-top', `${metrics.top}px`);
    dom.tocList.style.setProperty('--toc-active-height', `${metrics.height}px`);
    dom.tocList.style.setProperty('--toc-active-opacity', '1');
  }

  function renderSearchResults() {
    const matches = findSearchMatches(state.searchQuery);

    if (!matches.length) {
      dom.searchResults.innerHTML = `
        <div class="toc-empty">
          No matching documents were found for <strong>${escapeHtml(state.searchQuery)}</strong>.
        </div>
      `;
      return;
    }

    dom.searchResults.innerHTML = matches.map(doc => `
      <button class="search-result-card" data-doc-id="${doc.id}" type="button">
        <div class="search-result-topline">
          <span class="search-result-group">${escapeHtml(getGroupById(doc.groupId).name)}</span>
          <span class="search-result-path">${escapeHtml(doc.path.join(' / '))}</span>
        </div>
        <h3 class="search-result-title">${escapeHtml(doc.title)}</h3>
        <p class="search-result-summary">${escapeHtml(doc.summary)}</p>
      </button>
    `).join('');
  }

  function findSearchMatches(query) {
    const docs = Object.values(data.documents);
    if (!query) {
      return docs
        .sort((a, b) => {
          if (a.id === state.currentDocId) {
            return -1;
          }
          if (b.id === state.currentDocId) {
            return 1;
          }
          if (a.groupId === state.currentGroupId && b.groupId !== state.currentGroupId) {
            return -1;
          }
          if (b.groupId === state.currentGroupId && a.groupId !== state.currentGroupId) {
            return 1;
          }
          return a.title.localeCompare(b.title);
        })
        .slice(0, 5);
    }

    const normalized = query.toLowerCase();
    return docs
      .map(doc => ({ doc, score: scoreDocument(doc, normalized) }))
      .filter(item => item.score > 0)
      .sort((a, b) => b.score - a.score || a.doc.title.localeCompare(b.doc.title))
      .map(item => item.doc);
  }

  function scoreDocument(doc, normalizedQuery) {
    const title = doc.title.toLowerCase();
    const path = doc.path.join(' / ').toLowerCase();
    const summary = doc.summary.toLowerCase();
    const groupName = getGroupById(doc.groupId).name.toLowerCase();

    let score = 0;
    if (title.includes(normalizedQuery)) {
      score += 5;
    }
    if (path.includes(normalizedQuery)) {
      score += 3;
    }
    if (summary.includes(normalizedQuery)) {
      score += 2;
    }
    if (groupName.includes(normalizedQuery)) {
      score += 1;
    }

    return score;
  }

  function selectDocument(docId) {
    const doc = data.documents[docId];
    if (!doc) {
      return;
    }

    state.currentGroupId = doc.groupId;
    state.currentDocId = docId;
    state.activeHeadingId = doc.sections?.[0]?.id ?? null;
    state.groupMenuOpen = false;

    if (!state.isDesktop) {
      state.sidebarVisible = false;
    }

    renderAll(true);
  }

  function toggleFolder(folderId) {
    const expandedSet = expandedFoldersByGroup.get(state.currentGroupId);
    if (!expandedSet || !folderId) {
      return;
    }

    if (expandedSet.has(folderId)) {
      expandedSet.delete(folderId);
      return;
    }

    expandedSet.add(folderId);
  }

  function isFolderExpanded(folderId) {
    return expandedFoldersByGroup.get(state.currentGroupId)?.has(folderId) ?? false;
  }

  function filterNodes(nodes, query) {
    if (!query) {
      return nodes;
    }

    const normalized = query.toLowerCase();
    return nodes.reduce((result, node) => {
      if (node.type === 'folder') {
        const folderMatches = node.label.toLowerCase().includes(normalized);
        const filteredChildren = filterNodes(node.children ?? [], query);
        if (folderMatches || filteredChildren.length) {
          result.push({
            ...node,
            isMatch: folderMatches,
            forceOpen: true,
            children: folderMatches ? node.children : filteredChildren
          });
        }
        return result;
      }

      const doc = data.documents[node.docId];
      const haystack = [
        doc.title,
        doc.summary,
        doc.eyebrow,
        doc.path.join(' / ')
      ].join(' ').toLowerCase();

      if (haystack.includes(normalized)) {
        result.push({
          ...node,
          isMatch: true
        });
      }

      return result;
    }, []);
  }

  function countDocs(nodes) {
    return nodes.reduce((count, node) => {
      if (node.type === 'doc') {
        return count + 1;
      }

      return count + countDocs(node.children ?? []);
    }, 0);
  }

  function findFirstDocumentId(nodes) {
    for (const node of nodes) {
      if (node.type === 'doc') {
        return node.docId;
      }

      const nested = findFirstDocumentId(node.children ?? []);
      if (nested) {
        return nested;
      }
    }

    return null;
  }

  function getCurrentGroup() {
    return data.groups.find(group => group.id === state.currentGroupId) ?? data.groups[0];
  }

  function getCurrentDoc() {
    return data.documents[state.currentDocId] ?? data.documents[data.defaultDocId];
  }

  function getGroupById(groupId) {
    return data.groups.find(group => group.id === groupId) ?? data.groups[0];
  }

  function refreshIcons() {
    if (window.lucide) {
      window.lucide.createIcons();
    }
  }

  function isInputLike(target) {
    if (!(target instanceof HTMLElement)) {
      return false;
    }

    return Boolean(target.closest('input, textarea, [contenteditable="true"]'));
  }

  function escapeHtml(value) {
    return String(value)
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#39;');
  }
})();
