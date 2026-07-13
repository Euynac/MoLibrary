const sessionSelect = document.querySelector("#session-select");
const languageSelect = document.querySelector("#language-select");
const sessionMeta = document.querySelector("#session-meta");
const summary = document.querySelector("#summary");
const agentsRoot = document.querySelector("#agents");
const emptyState = document.querySelector("#empty-state");
const refreshTime = document.querySelector("#refresh-time");
const connectionText = document.querySelector("#connection-text");
const connectionDot = document.querySelector("#connection-dot");

const LANGUAGE_STORAGE_KEY = "supervise-subagents-language";
const translations = {
  en: {
    locale: "en-US",
    documentTitle: "Supervise Subagents",
    heroEyebrow: "Multi-agent supervision",
    pageTitle: "Supervise Subagents",
    language: "Language",
    connecting: "Connecting…",
    sessionHeading: "Task session",
    summaryLabel: "Agent lifecycle summary",
    delegatedEyebrow: "Delegated work",
    agentsHeading: "Agents",
    emptyTitle: "No registered agents",
    emptyDescription: "Agents will appear after the main agent registers a native child.",
    privacyFooter: "Native lifecycle and child-reported progress are shown separately. This dashboard never exposes private reasoning.",
    noUpdate: "No update",
    total: "Total",
    nativeLifecycle: "Native lifecycle",
    reportedProgress: "Reported progress",
    stale: "STALE",
    runtimeId: "Runtime ID",
    timeline: "Timeline",
    noEvents: "No progress events yet.",
    waitingForProgress: "Waiting for the first progress event.",
    noSessions: "No task sessions found.",
    connectedWaiting: "Connected · waiting for a session",
    created: "created",
    updated: "Updated",
    live: "Live · refreshes every 2 seconds",
    disconnected: "Disconnected",
    states: {
      pending: "Pending",
      running: "Running",
      idle: "Idle",
      completed: "Completed",
      failed: "Failed",
      interrupted: "Interrupted",
      unknown: "Unknown",
      in_progress: "In Progress",
      blocked: "Blocked",
      needs_input: "Needs Input",
      no_report: "No Report",
    },
  },
  "zh-CN": {
    locale: "zh-CN",
    documentTitle: "监督子代理",
    heroEyebrow: "多代理监督",
    pageTitle: "监督子代理",
    language: "语言",
    connecting: "正在连接…",
    sessionHeading: "任务会话",
    summaryLabel: "代理生命周期概览",
    delegatedEyebrow: "委派工作",
    agentsHeading: "子代理",
    emptyTitle: "暂无已注册的子代理",
    emptyDescription: "主代理注册原生子代理后，它们将显示在这里。",
    privacyFooter: "原生生命周期与子代理上报的进度会分别显示。此面板不会暴露私有推理过程。",
    noUpdate: "暂无更新",
    total: "总计",
    nativeLifecycle: "原生生命周期",
    reportedProgress: "上报进度",
    stale: "已过期",
    runtimeId: "运行时 ID",
    timeline: "时间线",
    noEvents: "暂无进度事件。",
    waitingForProgress: "正在等待第一条进度事件。",
    noSessions: "暂无任务会话。",
    connectedWaiting: "已连接 · 正在等待任务会话",
    created: "创建于",
    updated: "更新于",
    live: "实时 · 每 2 秒刷新",
    disconnected: "连接已断开",
    states: {
      pending: "等待中",
      running: "运行中",
      idle: "空闲",
      completed: "已完成",
      failed: "失败",
      interrupted: "已中断",
      unknown: "未知",
      in_progress: "进行中",
      blocked: "受阻",
      needs_input: "需要输入",
      no_report: "暂无上报",
    },
  },
};

const eventCache = new Map();
const openTimelines = new Set();
let selectedSessionId = "";
let refreshRunning = false;
let refreshQueued = false;
let currentLanguage = resolveInitialLanguage();

function resolveInitialLanguage() {
  const stored = localStorage.getItem(LANGUAGE_STORAGE_KEY);
  if (stored && translations[stored]) return stored;
  return navigator.language.toLowerCase().startsWith("zh") ? "zh-CN" : "en";
}

function text(key) {
  return translations[currentLanguage][key];
}

function stateText(state) {
  const key = state || "no_report";
  return translations[currentLanguage].states[key] || key.replaceAll("_", " ");
}

function element(tagName, className, value) {
  const node = document.createElement(tagName);
  if (className) node.className = className;
  if (value !== undefined && value !== null) node.textContent = String(value);
  return node;
}

function applyStaticLanguage() {
  document.documentElement.lang = currentLanguage;
  document.title = text("documentTitle");
  languageSelect.value = currentLanguage;
  languageSelect.setAttribute("aria-label", text("language"));
  document.querySelector("#hero-eyebrow").textContent = text("heroEyebrow");
  document.querySelector("#page-title").textContent = text("pageTitle");
  document.querySelector("#language-label").textContent = text("language");
  document.querySelector("#session-heading").textContent = text("sessionHeading");
  summary.setAttribute("aria-label", text("summaryLabel"));
  document.querySelector("#delegated-eyebrow").textContent = text("delegatedEyebrow");
  document.querySelector("#agents-heading").textContent = text("agentsHeading");
  document.querySelector("#empty-title").textContent = text("emptyTitle");
  document.querySelector("#empty-description").textContent = text("emptyDescription");
  document.querySelector("#privacy-footer").textContent = text("privacyFooter");
}

function formatTimestamp(value) {
  if (!value) return text("noUpdate");
  const date = new Date(value);
  return Number.isNaN(date.getTime())
    ? value
    : date.toLocaleString(translations[currentLanguage].locale);
}

function setConnection(state, value) {
  connectionDot.className = `connection-dot ${state}`;
  connectionText.textContent = value;
}

async function fetchJson(url) {
  const response = await fetch(url, { cache: "no-store" });
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
  return response.json();
}

function renderSessions(sessions) {
  const previous = selectedSessionId || sessionSelect.value;
  sessionSelect.replaceChildren();
  for (const session of sessions) {
    const option = document.createElement("option");
    option.value = session.session_id;
    option.textContent = `${session.task} · ${formatTimestamp(session.created_at)}`;
    sessionSelect.append(option);
  }
  selectedSessionId = sessions.some((session) => session.session_id === previous)
    ? previous
    : sessions[0]?.session_id || "";
  sessionSelect.value = selectedSessionId;
  sessionSelect.disabled = sessions.length === 0;
}

function renderSummary(counts, total) {
  summary.replaceChildren();
  const entries = [["total", total], ...Object.entries(counts).sort()];
  for (const [label, count] of entries) {
    const card = element("div", "summary-card");
    card.append(
      element("span", "summary-value", count),
      element("span", "summary-label", label === "total" ? text("total") : stateText(label)),
    );
    summary.append(card);
  }
}

function badge(state) {
  const value = state || "no_report";
  return element("span", `badge ${value}`, stateText(value));
}

function statusBlock(label, state, isFresh, timestamp) {
  const block = element("div", "status-block");
  block.append(element("span", "status-label", label));
  const line = element("div");
  line.append(badge(state));
  if (!isFresh) line.append(element("span", "freshness", text("stale")));
  block.append(line, element("div", "timestamp", formatTimestamp(timestamp)));
  return block;
}

function eventKey(sessionId, agentName) {
  return `${sessionId}/${agentName}`;
}

async function updateEvents(sessionId, agent) {
  const key = eventKey(sessionId, agent.agent_name);
  const existing = eventCache.get(key) || [];
  const after = existing.at(-1)?.sequence || 0;
  const payload = await fetchJson(
    `/api/sessions/${encodeURIComponent(sessionId)}/agents/${encodeURIComponent(agent.agent_name)}/events?after=${after}`,
  );
  if (payload.events.length) eventCache.set(key, [...existing, ...payload.events]);
  else if (!eventCache.has(key)) eventCache.set(key, []);
}

function renderTimeline(sessionId, agent) {
  const key = eventKey(sessionId, agent.agent_name);
  const events = eventCache.get(key) || [];
  const details = document.createElement("details");
  details.dataset.timelineKey = key;
  details.open = openTimelines.has(key);
  details.addEventListener("toggle", () => {
    if (details.open) openTimelines.add(key);
    else openTimelines.delete(key);
  });
  details.append(element("summary", "", `${text("timeline")} (${events.length})`));
  const timeline = element("ol", "timeline");
  for (const event of [...events].reverse()) {
    const item = document.createElement("li");
    item.append(
      badge(event.state),
      element("span", "timestamp", ` ${formatTimestamp(event.timestamp)}`),
      element("p", "timeline-message", event.message),
    );
    timeline.append(item);
  }
  if (!events.length) timeline.append(element("li", "timeline-message", text("noEvents")));
  details.append(timeline);
  return details;
}

function renderAgents(sessionId, agents) {
  agentsRoot.replaceChildren();
  emptyState.hidden = agents.length !== 0;
  for (const agent of agents) {
    const card = element("article", "agent-card");
    const header = element("div", "agent-header");
    const identity = element("div");
    identity.append(
      element("h3", "", agent.agent_name),
      element("div", "agent-id", `${text("runtimeId")}: ${agent.agent_id}`),
    );
    if (agent.closed_at) header.append(identity, badge("completed"));
    else header.append(identity);

    const statuses = element("div", "status-row");
    statuses.append(
      statusBlock(text("nativeLifecycle"), agent.native_status, agent.native_fresh, agent.native_updated_at),
      statusBlock(text("reportedProgress"), agent.reported_state, agent.reported_fresh, agent.reported_updated_at),
    );

    card.append(
      header,
      element("p", "task-text", agent.task),
      statuses,
      element("p", "latest-message", agent.latest_message || text("waitingForProgress")),
      renderTimeline(sessionId, agent),
    );
    agentsRoot.append(card);
  }
}

async function refresh() {
  if (refreshRunning) {
    refreshQueued = true;
    return;
  }
  refreshRunning = true;
  try {
    const sessionPayload = await fetchJson("/api/sessions");
    renderSessions(sessionPayload.sessions);
    if (!selectedSessionId) {
      sessionMeta.textContent = text("noSessions");
      renderSummary({}, 0);
      renderAgents("", []);
      setConnection("online", text("connectedWaiting"));
      return;
    }

    const refreshSessionId = selectedSessionId;
    const snapshot = await fetchJson(`/api/sessions/${encodeURIComponent(refreshSessionId)}`);
    await Promise.all(snapshot.agents.map((agent) => updateEvents(refreshSessionId, agent)));
    if (selectedSessionId !== refreshSessionId) {
      refreshQueued = true;
      return;
    }
    sessionMeta.textContent = `${snapshot.session_id} · ${text("created")} ${formatTimestamp(snapshot.created_at)}`;
    renderSummary(snapshot.counts, snapshot.agents.length);
    renderAgents(refreshSessionId, snapshot.agents);
    refreshTime.textContent = `${text("updated")} ${new Date().toLocaleTimeString(translations[currentLanguage].locale)}`;
    setConnection("online", text("live"));
  } catch (error) {
    setConnection("offline", `${text("disconnected")} · ${error.message}`);
  } finally {
    refreshRunning = false;
    if (refreshQueued) {
      refreshQueued = false;
      void refresh();
    }
  }
}

sessionSelect.addEventListener("change", () => {
  selectedSessionId = sessionSelect.value;
  refresh();
});

languageSelect.addEventListener("change", () => {
  currentLanguage = languageSelect.value;
  localStorage.setItem(LANGUAGE_STORAGE_KEY, currentLanguage);
  applyStaticLanguage();
  setConnection("", text("connecting"));
  refresh();
});

applyStaticLanguage();
setConnection("", text("connecting"));
refresh();
setInterval(refresh, 2000);
