// Invekto Voice PoC — F0.5 browser controller
// Pipeline (F0 unchanged): getUserMedia (48k mono) → AudioWorklet (PCM16 LE 20ms frames)
//   → WS binary → /ws/voice/microphone?tenant_id=X&flow_id=Y → VoiceRuntime → OpenAI Realtime
//   ← WS binary (PCM16 LE 48k 20ms bot voice) → AudioBufferSourceNode playback queue
//   ← WS text JSON control (ready / transcript_user / transcript_bot / first_byte / barge_in /
//                           error / response_done / tool_call_started / tool_call_completed)
//
// F0.5 additions:
//   * Tenant + flow dropdowns: window.INVEKTO_VOICE_JWT (URL-bridged Dashboard admin JWT) → Backend
//     /api/ops/tenants + /api/ops/tenants/{tid}/flows (both ops-scoped on super.invekto.com; the
//     flow list goes through Backend, NOT Automation directly — AD-42). SAME token powers the WS
//     handshake. Browser data is DISPLAY ONLY (AD-25 defense-in-depth); VoiceRuntime re-fetches with
//     its own service JWT before building instructions. voice.invekto.com and app.invekto.com
//     are SEPARATE origins so localStorage is NOT shared — the URL bridge is the only portable
//     cross-origin channel (AD-33 iter 1 architectural fix).
//   * WS handshake forwards ?tenant_id=X&flow_id=Y so VoiceRuntime can mint per-tenant service
//     JWTs and search the right knowledge base when the model calls `search_knowledge_base`.
//   * Tool-call HUD rozet panel — tool_call_started / tool_call_completed frames render in the
//     sticky right column so Q can correlate the audio with the underlying KB fetch.

const FRAME_SAMPLES = 960;          // 20ms @ 48kHz
const FRAME_BYTES = FRAME_SAMPLES * 2; // PCM16 LE
const SAMPLE_RATE = 48000;
// Playback jitter buffer: schedule bot audio this far ahead of the audio clock so bursty/jittery
// WS frame delivery doesn't open micro-gaps between back-to-back chunks (audible as clicks).
// ~120ms is imperceptible latency for a demo/test tool. Re-established after any underrun. Does
// NOT slow barge-in: flushPlayback() stops scheduled sources immediately regardless of lead.
const PLAYBACK_LEAD_SEC = 0.12;
const MAX_LOG_NODES = 200;          // DOM growth cap (rolling window)
const MAX_TOOL_CALL_NODES = 5;      // AD-32: keep last 5 tool-call rozets visible
const TOOL_CALL_ARGS_PREVIEW_CAP = 120; // AD-29 contract — defensive clamp on browser side too
const TOAST_AUTO_DISMISS_MS = 4000;
const MAX_TOAST_NODES = 3;

// localStorage keys (AD-34 AHA-4 last-selection auto-fill — voice.invekto.com same-origin).
// NOTE: Dashboard JWT does NOT live in this origin's localStorage. It arrives via the URL
// bridge (?token=…) and lives in window.INVEKTO_VOICE_JWT for the page lifetime — see AD-33
// (Codex iter 0 architectural fix; voice.invekto.com and app.invekto.com are different
// origins so localStorage is not shared).
const LS_KEY_TENANT = 'voice-poc-tenant-id';
const LS_KEY_FLOW = 'voice-poc-flow-id';
const LS_KEY_VOICE = 'voice-poc-voice';
// OpenAI Realtime voice VALIDATION allowlist (full set of 10). Identical to the server-side
// VoicePocEndpoints.AllowedVoices — used only to guard localStorage restore/persist. NOTE: the
// <select id="voiceSelect"> in voice-poc.html deliberately exposes a CURATED SUBSET (the
// best-sounding ones); that subset must stay ⊆ this list, but the two are not meant to be equal.
const ALLOWED_VOICES = ['alloy', 'ash', 'ballad', 'coral', 'echo', 'sage', 'shimmer', 'verse', 'marin', 'cedar'];

// Client-side INV error codes — surfaced in event log + toast text so Q can correlate
// production failures with arch/errors.md INV-VR-CLIENT-* entries (added in Chunk D). These
// are display-only (browser-origin diagnostics), not authoritative server codes.
const CLIENT_ERR = {
  TOKEN_MISSING: 'INV-VR-CLIENT-001',     // window.INVEKTO_VOICE_JWT yok (Dashboard wrapper bridge fail)
  LOCALSTORAGE_READ: 'INV-VR-CLIENT-002', // localStorage SecurityError / private mode read fail
  LOCALSTORAGE_WRITE: 'INV-VR-CLIENT-003',// localStorage quota / SecurityError write fail
  TENANT_FETCH: 'INV-VR-CLIENT-004',      // Backend /api/ops/tenants HTTP/network fail
  FLOW_FETCH: 'INV-VR-CLIENT-005',        // Automation /api/v1/flows/{tid} HTTP/network fail
  TOOL_CALL_UNMATCHED: 'INV-VR-CLIENT-006', // tool_call_completed without prior tool_call_started
  WS_ERROR: 'INV-VR-CLIENT-007',          // WebSocket onerror low-level transport error
  WS_REJECTED: 'INV-VR-CLIENT-008',       // WS close with INV-VR-* server reason
  MIC_INIT: 'INV-VR-CLIENT-009',          // getUserMedia / AudioContext / AudioWorklet init failure (non-permission)
  SELECTION_MISSING: 'INV-VR-CLIENT-010', // start() called without tenant+flow selection (defensive — button is also disabled)
};

// Cross-origin Backend host. The ops Dashboard and its /api/ops endpoints are served by the Backend
// Kestrel host at super.invekto.com; Voice Test runs on voice.invekto.com:8443. CORS is enabled
// server-side via VoicePocCors (Chunk E + F0.5 defect fix). NOTE: app.invekto.com is the
// tenant-facing IIS app and returns 404 for /api/ops — the dropdowns MUST hit super.invekto.com
// (the earlier app.invekto.com value was defect #1). BOTH the tenant list and the flow list are
// Backend /api/ops endpoints; flows are NOT fetched from Automation directly (Automation:7108 is
// not externally routed and rejects the tenant=0 token — defect #2; see fetchFlows). On localhost
// dev we hit same-origin (window.location.host) so the dropdowns work without production CORS config.
const isLocalhost = ['localhost', '127.0.0.1', '::1'].includes(location.hostname);
const BACKEND_BASE = isLocalhost ? '' : 'https://super.invekto.com';

const els = {
  tenantSelect: document.getElementById('tenantSelect'),
  flowSelect: document.getElementById('flowSelect'),
  voiceSelect: document.getElementById('voiceSelect'),
  selectorHint: document.getElementById('selectorHint'),
  startBtn: document.getElementById('startBtn'),
  stopBtn: document.getElementById('stopBtn'),
  status: document.getElementById('status'),
  latencyLast: document.getElementById('latencyLast'),
  latencyP95: document.getElementById('latencyP95'),
  bargeLatency: document.getElementById('bargeLatency'),
  turnCount: document.getElementById('turnCount'),
  transcriptLog: document.getElementById('transcriptLog'),
  eventLog: document.getElementById('eventLog'),
  toastContainer: document.getElementById('toastContainer'),
  toolCallList: document.getElementById('toolCallList'),
};

const state = {
  ws: null,
  audioCtx: null,
  workletNode: null,
  micStream: null,
  playbackTime: 0,
  // Scheduled AudioBufferSourceNodes for the current bot response. Tracked so a barge-in can
  // STOP them immediately — otherwise audio already queued (src.start at a future time) keeps
  // playing after the server cancels the response, so the bot "talks over" the new turn.
  playbackSources: [],
  firstByteSamples: [],
  turns: 0,
  currentBotTranscript: '',
  tenants: [],          // [{ tenantId, tenantName, sector, isActive }, ...]
  flows: [],            // [{ flow_id, flow_name, is_active, is_default }, ...]
  selectedTenantId: null,
  selectedFlowId: null,
  // Map<call_id, HTMLElement> so tool_call_completed can update the existing rozet rather
  // than appending a duplicate. Cleared on cleanup().
  toolCallElements: new Map(),
  // Rolling FIFO of call_id insertion order, used to evict the oldest rozet past MAX_TOOL_CALL_NODES.
  toolCallOrder: [],
};

// ====================================================================
// UI helpers
// ====================================================================

function setStatus(text, cls) {
  els.status.textContent = text;
  els.status.className = 'value ' + cls;
}

function trimLog(container) {
  while (container.childNodes.length > MAX_LOG_NODES) {
    container.removeChild(container.firstChild);
  }
}

function logEvent(text, cls = 'event-info') {
  const line = document.createElement('div');
  line.className = cls;
  // textContent (NOT innerHTML) — defense against XSS from server-side messages that might
  // include HTML-special characters. Same precaution applies to dropdown option labels below.
  line.textContent = `[${new Date().toLocaleTimeString()}] ${text}`;
  els.eventLog.appendChild(line);
  trimLog(els.eventLog);
  els.eventLog.scrollTop = els.eventLog.scrollHeight;
}

function logTranscript(text, who) {
  const line = document.createElement('div');
  line.className = who === 'user' ? 'turn-user' : 'turn-bot';
  line.textContent = text;
  els.transcriptLog.appendChild(line);
  trimLog(els.transcriptLog);
  els.transcriptLog.scrollTop = els.transcriptLog.scrollHeight;
}

function recordFirstByte(ms) {
  els.latencyLast.textContent = `${ms} ms`;
  state.firstByteSamples.push(ms);
  if (state.firstByteSamples.length > 100) state.firstByteSamples.shift();
  const sorted = [...state.firstByteSamples].sort((a, b) => a - b);
  const p95idx = Math.max(0, Math.ceil(0.95 * sorted.length) - 1);
  els.latencyP95.textContent = `${sorted[p95idx]} ms`;
}

function showToast(message, variant = 'info') {
  const toast = document.createElement('div');
  toast.className = `toast toast-${variant}`;
  toast.textContent = message;
  els.toastContainer.appendChild(toast);
  while (els.toastContainer.childNodes.length > MAX_TOAST_NODES) {
    els.toastContainer.removeChild(els.toastContainer.firstChild);
  }
  setTimeout(() => {
    if (toast.parentNode) toast.parentNode.removeChild(toast);
  }, TOAST_AUTO_DISMISS_MS);
}

function setSelectorHint(text, cls = '') {
  els.selectorHint.textContent = text;
  els.selectorHint.className = 'selector-hint' + (cls ? ' ' + cls : '');
}

// ====================================================================
// Dropdown auth + fetch (AD-25 / AD-33)
// ====================================================================

function getDashboardJwt() {
  // AD-33 (Codex iter 0 architectural fix): Dashboard JWT lives in window.INVEKTO_VOICE_JWT,
  // populated by the URL-bridge script in voice-poc.html (?token=<JWT> → window global →
  // history.replaceState removes from URL). voice.invekto.com:8443 and app.invekto.com are
  // SEPARATE origins, so the Dashboard SPA's localStorage.access_token is NOT readable here —
  // the URL bridge is the only portable cross-origin channel. The SAME token powers both the
  // dropdown fetch (Bearer header) AND the WS handshake (?token=… query) so production users
  // arriving via the Dashboard "Voice Test" link get a working page in one round-trip.
  return typeof window.INVEKTO_VOICE_JWT === 'string' && window.INVEKTO_VOICE_JWT.length > 0
    ? window.INVEKTO_VOICE_JWT
    : null;
}

async function fetchTenants(jwt) {
  // Backend /api/ops/tenants returns { tenants: [{ tenantId, tenantName, isActive, sector,
  // planTier, createdAt }, ...] } (ASP.NET camelCase defaults; Bearer JWT path validates
  // Role==admin AND TenantId==0). Cross-origin from voice.invekto.com:8443 needs Chunk E CORS.
  const url = `${BACKEND_BASE}/api/ops/tenants`;
  const res = await fetch(url, {
    method: 'GET',
    headers: { 'Authorization': `Bearer ${jwt}`, 'Accept': 'application/json' },
    credentials: 'omit',
  });
  if (!res.ok) {
    throw new Error(`${CLIENT_ERR.TENANT_FETCH}: Backend /api/ops/tenants ${res.status} ${res.statusText}`);
  }
  const body = await res.json();
  const list = Array.isArray(body?.tenants) ? body.tenants : [];
  // Drop inactive tenants and self-tenant (tenantId 0). Sort by name for stable demo UX.
  return list
    .filter((t) => t && typeof t.tenantId === 'number' && t.tenantId > 0 && t.isActive !== false)
    .map((t) => ({
      tenantId: t.tenantId,
      tenantName: typeof t.tenantName === 'string' && t.tenantName.length > 0 ? t.tenantName : `Firma ${t.tenantId}`,
      sector: typeof t.sector === 'string' ? t.sector : null,
    }))
    .sort((a, b) => a.tenantName.localeCompare(b.tenantName, 'tr'));
}

async function fetchFlows(jwt, tenantId) {
  // F0.5 defect #2 fix: fetch through the Backend OPS endpoint, not Automation directly and not the
  // generic flow-builder proxy. Two constraints make those impossible for the Voice Test browser:
  //   (a) Automation:7108 is not externally routed (no host maps to it), so browser-direct fails.
  //   (b) Automation.GetValidatedTenant requires the JWT tenant_id == route tenantId (no cross-tenant
  //       bypass), and our voice-jwt is tenant=0 — so both a direct call AND the generic
  //       /api/v1/flow-builder/flows proxy (which forwards the caller token verbatim) get a 403.
  // Backend's GET /api/ops/tenants/{id}/flows (AD-42) validates ops auth, then mints a per-tenant
  // SERVICE token and proxies to Automation /api/v1/flows/{id}, returning the response verbatim:
  // an ARRAY of flow objects with snake_case keys
  //   { flow_id, flow_name, flow_description, is_active, is_default, config_version, ... }
  const url = `${BACKEND_BASE}/api/ops/tenants/${encodeURIComponent(tenantId)}/flows`;
  const res = await fetch(url, {
    method: 'GET',
    headers: { 'Authorization': `Bearer ${jwt}`, 'Accept': 'application/json' },
    credentials: 'omit',
  });
  if (!res.ok) {
    throw new Error(`${CLIENT_ERR.FLOW_FETCH}: Backend /api/ops/tenants/${tenantId}/flows ${res.status} ${res.statusText}`);
  }
  const body = await res.json();
  const list = Array.isArray(body) ? body : [];
  return list
    .filter((f) => f && typeof f.flow_id === 'number' && f.flow_id > 0)
    .map((f) => ({
      flowId: f.flow_id,
      flowName: typeof f.flow_name === 'string' && f.flow_name.length > 0 ? f.flow_name : `Akış ${f.flow_id}`,
      isActive: f.is_active !== false,
      isDefault: f.is_default === true,
    }))
    .sort((a, b) => {
      // Default flow first, then active flows alphabetical, inactive at the bottom.
      if (a.isDefault !== b.isDefault) return a.isDefault ? -1 : 1;
      if (a.isActive !== b.isActive) return a.isActive ? -1 : 1;
      return a.flowName.localeCompare(b.flowName, 'tr');
    });
}

function renderTenantOptions(tenants) {
  // Clear + repopulate. textContent on each Option avoids any HTML injection through
  // tenant_name / sector (defense-in-depth — Backend already sanitizes but renderer is local).
  els.tenantSelect.innerHTML = '';
  const placeholder = document.createElement('option');
  placeholder.value = '';
  placeholder.textContent = tenants.length === 0 ? 'Aktif firma yok' : '— Firma seçin —';
  els.tenantSelect.appendChild(placeholder);
  for (const t of tenants) {
    const opt = document.createElement('option');
    opt.value = String(t.tenantId);
    opt.textContent = t.sector ? `${t.tenantName} (${t.sector})` : t.tenantName;
    els.tenantSelect.appendChild(opt);
  }
  els.tenantSelect.disabled = tenants.length === 0;
}

function renderFlowOptions(flows) {
  els.flowSelect.innerHTML = '';
  const placeholder = document.createElement('option');
  placeholder.value = '';
  placeholder.textContent = flows.length === 0 ? 'Bu firmada akış yok' : '— Akış seçin —';
  els.flowSelect.appendChild(placeholder);
  for (const f of flows) {
    const opt = document.createElement('option');
    opt.value = String(f.flowId);
    const label = f.isDefault ? `${f.flowName} (varsayılan)` : f.flowName;
    // Inactive flows stay SELECTABLE — the whole point of Voice Test is to dry-run a flow
    // BEFORE activating it for production. The `[pasif]` label is a visual hint only, not a
    // gate (an earlier version disabled these, which blocked the primary test-before-go-live
    // use case). VoiceRuntime fetches the flow by id regardless of is_active.
    opt.textContent = f.isActive ? label : `${label} [pasif]`;
    els.flowSelect.appendChild(opt);
  }
  els.flowSelect.disabled = flows.length === 0;
}

function lsRead(key) {
  // Typed catch + log — Codex iter 0 CQ2/CQ5 fix. SecurityError can fire in private-browsing /
  // disabled-storage modes; DOMException can occur on certain quota states. Treat any failure
  // as "key absent" and log so debugging the auto-fill path stays possible.
  try {
    return localStorage.getItem(key);
  } catch (err) {
    logEvent(`${CLIENT_ERR.LOCALSTORAGE_READ}: localStorage read fail (${key}): ${err.name}: ${err.message}`, 'event-warn');
    return null;
  }
}

function lsWrite(key, val) {
  try {
    localStorage.setItem(key, val);
  } catch (err) {
    // Quota / private mode — non-fatal, just lose the auto-fill convenience.
    logEvent(`${CLIENT_ERR.LOCALSTORAGE_WRITE}: localStorage write fail (${key}): ${err.name}: ${err.message}`, 'event-warn');
  }
}

function parsePositiveInt(raw) {
  // Codex iter 0 VQ-DATA-D2/VQ-PROCESS-D4 fix: Number.parseInt is prefix-based ("12abc" → 12,
  // "1e3" → 1), which lets corrupted localStorage values silently pass the auto-fill guard.
  // Strict regex: only digit-only positive integers, no leading zeros, no whitespace, no
  // exponent / hex. Dropdown <option value="…"> we set are always clean integer strings, so
  // this is purely a corruption / NaN-injection guard for the persisted state.
  if (raw === null || raw === undefined || raw === '') return null;
  const s = String(raw);
  if (!/^[1-9][0-9]*$/.test(s)) return null;
  const n = Number(s);
  return Number.isSafeInteger(n) && n > 0 ? n : null;
}

function updateStartButtonState() {
  els.startBtn.disabled = !(state.selectedTenantId && state.selectedFlowId);
  if (state.selectedTenantId && state.selectedFlowId) {
    setSelectorHint('Hazır — mikrofonu başlatabilirsiniz.', 'ready');
  } else if (state.selectedTenantId) {
    setSelectorHint('Şimdi akış seçin.');
  } else {
    setSelectorHint('Mikrofonu başlatmak için firma ve akış seçin.');
  }
}

async function loadAndPopulateFlows(jwt, tenantId, preselectFlowId) {
  els.flowSelect.disabled = true;
  els.flowSelect.innerHTML = '';
  const loading = document.createElement('option');
  loading.value = '';
  loading.textContent = 'Akışlar yükleniyor…';
  els.flowSelect.appendChild(loading);
  try {
    const flows = await fetchFlows(jwt, tenantId);
    state.flows = flows;
    renderFlowOptions(flows);
    if (preselectFlowId !== null && flows.some((f) => f.flowId === preselectFlowId)) {
      els.flowSelect.value = String(preselectFlowId);
      state.selectedFlowId = preselectFlowId;
    } else {
      // Pick default flow when present and no valid preselect remains.
      const def = flows.find((f) => f.isDefault && f.isActive);
      if (def) {
        els.flowSelect.value = String(def.flowId);
        state.selectedFlowId = def.flowId;
        lsWrite(LS_KEY_FLOW, String(def.flowId));
      } else {
        state.selectedFlowId = null;
      }
    }
  } catch (err) {
    state.flows = [];
    state.selectedFlowId = null;
    els.flowSelect.innerHTML = '';
    const fail = document.createElement('option');
    fail.value = '';
    fail.textContent = 'Akış listesi yüklenemedi';
    els.flowSelect.appendChild(fail);
    els.flowSelect.disabled = true;
    setSelectorHint(`${CLIENT_ERR.FLOW_FETCH}: Akış listesi yüklenemedi — Dashboard'da oturumun aktif olduğunu kontrol edin.`, 'error');
    showToast(`${CLIENT_ERR.FLOW_FETCH}: Akış listesi yüklenemedi (${err.message})`, 'error');
    logEvent(`${CLIENT_ERR.FLOW_FETCH}: Flow fetch fail (tenant=${tenantId}): ${err.message}`, 'event-err');
  }
  updateStartButtonState();
}

async function bootstrapDropdowns() {
  const jwt = getDashboardJwt();
  if (!jwt) {
    setSelectorHint(`${CLIENT_ERR.TOKEN_MISSING}: Dashboard oturumu bulunamadı. Lütfen app.invekto.com'da giriş yapın ve Voice Test linkini tekrar açın.`, 'error');
    showToast(`${CLIENT_ERR.TOKEN_MISSING}: Dashboard JWT bulunamadı (URL bridge boş) — listeler yüklenemiyor.`, 'error');
    logEvent(`${CLIENT_ERR.TOKEN_MISSING}: window.INVEKTO_VOICE_JWT undefined; tenant/flow fetch atlandı.`, 'event-err');
    els.tenantSelect.innerHTML = '';
    const opt = document.createElement('option');
    opt.value = '';
    opt.textContent = 'Dashboard JWT yok';
    els.tenantSelect.appendChild(opt);
    els.tenantSelect.disabled = true;
    return;
  }

  try {
    const tenants = await fetchTenants(jwt);
    state.tenants = tenants;
    renderTenantOptions(tenants);
    if (tenants.length === 0) {
      setSelectorHint('Aktif firma bulunamadı.', 'error');
      return;
    }
  } catch (err) {
    state.tenants = [];
    els.tenantSelect.innerHTML = '';
    const opt = document.createElement('option');
    opt.value = '';
    opt.textContent = 'Firma listesi yüklenemedi';
    els.tenantSelect.appendChild(opt);
    els.tenantSelect.disabled = true;
    setSelectorHint(`${CLIENT_ERR.TENANT_FETCH}: Firma listesi yüklenemedi — Dashboard JWT geçersiz veya CORS engellenmiş olabilir.`, 'error');
    showToast(`${CLIENT_ERR.TENANT_FETCH}: Firma listesi yüklenemedi (${err.message})`, 'error');
    logEvent(`${CLIENT_ERR.TENANT_FETCH}: Tenant fetch fail: ${err.message}`, 'event-err');
    return;
  }

  // AD-34: restore last selection — validate against the freshly-fetched list to avoid stale
  // tenant_id/flow_id (deleted/disabled tenants between sessions).
  const lastTenantId = parsePositiveInt(lsRead(LS_KEY_TENANT));
  const lastFlowId = parsePositiveInt(lsRead(LS_KEY_FLOW));
  if (lastTenantId && state.tenants.some((t) => t.tenantId === lastTenantId)) {
    els.tenantSelect.value = String(lastTenantId);
    state.selectedTenantId = lastTenantId;
    const jwt2 = getDashboardJwt(); // re-read in case localStorage rotated mid-bootstrap
    if (jwt2) await loadAndPopulateFlows(jwt2, lastTenantId, lastFlowId);
  }
  updateStartButtonState();
}

els.tenantSelect.addEventListener('change', async () => {
  const tid = parsePositiveInt(els.tenantSelect.value);
  state.selectedTenantId = tid;
  state.selectedFlowId = null;
  els.flowSelect.value = '';
  if (tid === null) {
    els.flowSelect.innerHTML = '';
    const opt = document.createElement('option');
    opt.value = '';
    opt.textContent = '— Önce firma seçin —';
    els.flowSelect.appendChild(opt);
    els.flowSelect.disabled = true;
    updateStartButtonState();
    return;
  }
  lsWrite(LS_KEY_TENANT, String(tid));
  // Wipe flow selection cache so a stale flow_id from a different tenant doesn't auto-fill.
  lsWrite(LS_KEY_FLOW, '');
  const jwt = getDashboardJwt();
  if (!jwt) {
    showToast(`${CLIENT_ERR.TOKEN_MISSING}: Dashboard JWT bulunamadı.`, 'error');
    logEvent(`${CLIENT_ERR.TOKEN_MISSING}: tenant change handler aborted — no JWT.`, 'event-err');
    return;
  }
  await loadAndPopulateFlows(jwt, tid, null);
});

els.flowSelect.addEventListener('change', () => {
  const fid = parsePositiveInt(els.flowSelect.value);
  state.selectedFlowId = fid;
  if (fid !== null) lsWrite(LS_KEY_FLOW, String(fid));
  updateStartButtonState();
});

// Voice picker: restore the last-used voice (validated against ALLOWED_VOICES) + persist on change.
// No start-button gating — the dropdown always has a value (defaults to the first <option>).
// The chosen voice is sent on the WS handshake (?voice=) and the server re-validates it.
{
  const savedVoice = lsRead(LS_KEY_VOICE);
  if (savedVoice && ALLOWED_VOICES.includes(savedVoice)) els.voiceSelect.value = savedVoice;
}
els.voiceSelect.addEventListener('change', () => {
  if (ALLOWED_VOICES.includes(els.voiceSelect.value)) lsWrite(LS_KEY_VOICE, els.voiceSelect.value);
});

// ====================================================================
// Tool-call HUD (AD-29 / AD-32)
// ====================================================================

function clearToolCalls() {
  state.toolCallElements.clear();
  state.toolCallOrder.length = 0;
  els.toolCallList.innerHTML = '';
  const empty = document.createElement('div');
  empty.className = 'tool-call-empty';
  empty.textContent = 'Henüz çağrı yok. Bot konuştukça burada görünecek.';
  els.toolCallList.appendChild(empty);
}

function ensureToolCallEmptyRemoved() {
  const empty = els.toolCallList.querySelector('.tool-call-empty');
  if (empty) empty.remove();
}

function trimToolCalls() {
  while (state.toolCallOrder.length > MAX_TOOL_CALL_NODES) {
    const oldestId = state.toolCallOrder.shift();
    const node = state.toolCallElements.get(oldestId);
    if (node && node.parentNode) node.parentNode.removeChild(node);
    state.toolCallElements.delete(oldestId);
  }
}

function handleToolCallStarted(msg) {
  // Server contract (voice-runtime-ws.md): { type, call_id, name, args_preview }.
  // call_id is required; defense fallback to a synthetic id so the rozet still renders.
  const callId = typeof msg.call_id === 'string' && msg.call_id.length > 0
    ? msg.call_id
    : `unknown-${Date.now()}`;
  const name = typeof msg.name === 'string' ? msg.name : 'unknown_tool';
  let argsPreview = typeof msg.args_preview === 'string' ? msg.args_preview : '';
  if (argsPreview.length > TOOL_CALL_ARGS_PREVIEW_CAP) {
    argsPreview = argsPreview.slice(0, TOOL_CALL_ARGS_PREVIEW_CAP - 1) + '…';
  }

  ensureToolCallEmptyRemoved();

  // If the same call_id arrives twice (server retry quirk), update in place instead of duplicating.
  let card = state.toolCallElements.get(callId);
  if (!card) {
    card = document.createElement('div');
    card.className = 'tool-call pending';
    els.toolCallList.appendChild(card);
    state.toolCallElements.set(callId, card);
    state.toolCallOrder.push(callId);
    trimToolCalls();
  } else {
    card.className = 'tool-call pending';
  }
  card.innerHTML = '';
  const head = document.createElement('div');
  head.className = 'tool-call-head';
  head.textContent = name;
  card.appendChild(head);
  if (argsPreview) {
    const argsEl = document.createElement('div');
    argsEl.className = 'tool-call-args';
    argsEl.textContent = argsPreview;
    card.appendChild(argsEl);
  }
  const meta = document.createElement('div');
  meta.className = 'tool-call-meta';
  meta.textContent = 'Çalışıyor…';
  card.appendChild(meta);

  els.toolCallList.scrollTop = els.toolCallList.scrollHeight;
  logEvent(`tool_call_started: ${name}${argsPreview ? ' ' + argsPreview : ''}`);
}

function handleToolCallCompleted(msg) {
  const callId = typeof msg.call_id === 'string' ? msg.call_id : '';
  const name = typeof msg.name === 'string' ? msg.name : 'unknown_tool';
  const status = msg.status === 'error' ? 'error' : 'ok';
  const durationMs = Number.isFinite(msg.duration_ms) ? Math.max(0, Math.floor(msg.duration_ms)) : null;
  const resultCount = Number.isFinite(msg.result_count) ? Math.max(0, Math.floor(msg.result_count)) : null;
  const errorCode = typeof msg.error_code === 'string' ? msg.error_code : null;

  const card = callId ? state.toolCallElements.get(callId) : null;
  if (!card) {
    // No prior "started" rozet — render a completed-only card so Q still sees the call.
    // Codex iter 0 CQ2 fix: log the unmatched path explicitly so server↔browser frame ordering
    // drift is debuggable (silent synth-start would hide the contract violation).
    logEvent(`${CLIENT_ERR.TOOL_CALL_UNMATCHED}: tool_call_completed without prior tool_call_started (call_id='${callId}', name='${name}') — synthesizing rozet.`, 'event-warn');
    handleToolCallStarted({ call_id: callId, name, args_preview: '' });
  }
  const target = state.toolCallElements.get(callId);
  if (!target) {
    // Defensive: handleToolCallStarted may have refused to render (e.g. empty call_id falls
    // through to synthetic id, but if Map insertion failed for any reason we surface it).
    logEvent(`${CLIENT_ERR.TOOL_CALL_UNMATCHED}: tool_call_completed could not bind to any rozet (call_id='${callId}').`, 'event-err');
    return;
  }

  target.classList.remove('pending');
  target.classList.add(status === 'ok' ? 'ok' : 'error');

  // Replace meta line with final metric. Keep head + args nodes intact.
  const oldMeta = target.querySelector('.tool-call-meta');
  if (oldMeta) oldMeta.remove();
  const meta = document.createElement('div');
  meta.className = 'tool-call-meta';
  if (status === 'ok') {
    const okSpan = document.createElement('span');
    okSpan.className = 'tool-call-status-ok';
    okSpan.textContent = resultCount === null ? 'tamam' : `${resultCount} sonuç`;
    meta.appendChild(okSpan);
    if (durationMs !== null) {
      meta.appendChild(document.createTextNode(` · ${durationMs}ms`));
    }
  } else {
    const errSpan = document.createElement('span');
    errSpan.className = 'tool-call-status-err';
    errSpan.textContent = errorCode || 'hata';
    meta.appendChild(errSpan);
    if (durationMs !== null) {
      meta.appendChild(document.createTextNode(` · ${durationMs}ms`));
    }
  }
  target.appendChild(meta);

  els.toolCallList.scrollTop = els.toolCallList.scrollHeight;
  logEvent(
    `tool_call_completed: ${name} status=${status}` +
      (resultCount !== null ? ` results=${resultCount}` : '') +
      (durationMs !== null ? ` ${durationMs}ms` : '') +
      (errorCode ? ` code=${errorCode}` : ''),
    status === 'ok' ? 'event-info' : 'event-err'
  );
}

// ====================================================================
// Mic / WS lifecycle
// ====================================================================

async function start() {
  if (!state.selectedTenantId || !state.selectedFlowId) {
    // Defensive guard — start button is also disabled until both selections exist. Reaching
    // here implies a UI state race (very unlikely) so we log it under an INV code.
    showToast(`${CLIENT_ERR.SELECTION_MISSING}: Önce firma ve akış seçin.`, 'error');
    logEvent(`${CLIENT_ERR.SELECTION_MISSING}: start() called without tenant_id+flow_id selection.`, 'event-warn');
    return;
  }
  els.startBtn.disabled = true;
  els.tenantSelect.disabled = true;
  els.flowSelect.disabled = true;
  setStatus('Mikrofon izni isteniyor...', 'status-listening');
  clearToolCalls();

  try {
    state.micStream = await navigator.mediaDevices.getUserMedia({
      audio: {
        channelCount: 1,
        sampleRate: SAMPLE_RATE,
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: true,
      },
    });
  } catch (err) {
    const isPermissionDenied = err.name === 'NotAllowedError' || err.name === 'NotFoundError';
    const code = isPermissionDenied ? 'INV-VR-009' : CLIENT_ERR.MIC_INIT;
    setStatus(isPermissionDenied ? 'Mikrofon reddedildi' : 'Mikrofon başlatılamadı', 'status-error');
    logEvent(`${code}: ${err.name}: ${err.message}`, 'event-err');
    restoreSelectorsAfterFailure();
    return;
  }

  state.audioCtx = new AudioContext({ sampleRate: SAMPLE_RATE });
  state.playbackTime = state.audioCtx.currentTime;
  // Master gain: ALL bot audio routes through this so barge-in can mute everything instantly
  // (gain=0) regardless of how many BufferSourceNodes are scheduled ahead. OpenAI streams audio
  // far faster than realtime (e.g. 23s buffered after 6s), and stop() on dozens of future-scheduled
  // sources does not reliably silence playback — a gain cut does. Restored to 1 on the next
  // response's first byte. (Barge-in F1 fix.)
  state.masterGain = state.audioCtx.createGain();
  state.masterGain.gain.value = 1;
  state.masterGain.connect(state.audioCtx.destination);

  if (state.audioCtx.sampleRate !== SAMPLE_RATE) {
    // Sample-rate mismatch is a hardware/policy issue (browser refused 48kHz). It's the same
    // family as MIC_INIT (non-permission mic init failure), so we reuse INV-VR-CLIENT-009.
    logEvent(`${CLIENT_ERR.MIC_INIT}: Donanım AudioContext ${state.audioCtx.sampleRate} Hz verdi, ${SAMPLE_RATE} Hz bekleniyordu — F0 PCM pipeline çalışmaz.`, 'event-err');
    cleanup();
    // Codex iter 0 CQ1 fix: set status AFTER cleanup so the error label survives the cleanup
    // status reset. cleanup() resets state but the user-visible status must reflect what failed.
    setStatus('Örnekleme hızı uyumsuzluğu', 'status-error');
    return;
  }

  try {
    await state.audioCtx.audioWorklet.addModule('audio-worklet.js');
  } catch (err) {
    // AudioWorklet module load failure (network, SecurityError, missing file). Surfaced under
    // INV-VR-CLIENT-009 (browser-side mic-pipeline init failure family).
    logEvent(`${CLIENT_ERR.MIC_INIT}: AudioWorklet load fail — ${err.name}: ${err.message}`, 'event-err');
    cleanup();
    setStatus('AudioWorklet yüklenemedi', 'status-error');
    return;
  }

  const source = state.audioCtx.createMediaStreamSource(state.micStream);
  state.workletNode = new AudioWorkletNode(state.audioCtx, 'pcm-frame-processor', {
    processorOptions: { frameSamples: FRAME_SAMPLES },
  });
  state.workletNode.port.onmessage = (e) => {
    if (state.ws && state.ws.readyState === WebSocket.OPEN) {
      state.ws.send(e.data);
    }
  };
  source.connect(state.workletNode);

  // WS handshake — F0.5 strict mode requires tenant_id + flow_id + JWT (or dev=1 on localhost).
  const proto = location.protocol === 'https:' ? 'wss' : 'ws';
  const queryParts = [
    'locale=tr-TR',
    `tenant_id=${encodeURIComponent(state.selectedTenantId)}`,
    `flow_id=${encodeURIComponent(state.selectedFlowId)}`,
    `voice=${encodeURIComponent(els.voiceSelect.value)}`,
  ];
  if (isLocalhost) {
    queryParts.push('dev=1');
  } else {
    // Production WS handshake re-uses window.INVEKTO_VOICE_JWT — the SAME URL-bridged Dashboard
    // JWT that powers the dropdown fetch (AD-33). No localStorage fallback: the Dashboard
    // wrapper is the single source of truth for cross-origin auth. Bookmarked direct visits
    // without the wrapper will fail here (intentional — no impersonation path without bridge).
    const tok = window.INVEKTO_VOICE_JWT;
    if (!tok) {
      logEvent(`${CLIENT_ERR.TOKEN_MISSING}: Production sayfada window.INVEKTO_VOICE_JWT yok (Dashboard wrapper bridge fail).`, 'event-err');
      showToast(`${CLIENT_ERR.TOKEN_MISSING}: Dashboard JWT bulunamadı — Voice Test linkini Dashboard'dan tekrar açın.`, 'error');
      cleanup();
      // CQ1 fix: status must outlive cleanup() — set AFTER reset so user sees the actual error.
      setStatus('Token bulunamadı', 'status-error');
      return;
    }
    queryParts.push(`token=${encodeURIComponent(tok)}`);
  }
  const url = `${proto}://${location.host}/ws/voice/microphone?${queryParts.join('&')}`;
  state.ws = new WebSocket(url);
  state.ws.binaryType = 'arraybuffer';

  state.ws.onopen = () => {
    setStatus('Bağlı, konuşabilirsin', 'status-listening');
    const maskedUrl = url.replace(/token=[^&]+/, 'token=***MASKED***');
    logEvent(`WebSocket bağlandı: ${maskedUrl}`);
    els.stopBtn.disabled = false;
  };

  state.ws.onerror = () => {
    setStatus('Bağlantı hatası', 'status-error');
    logEvent(`${CLIENT_ERR.WS_ERROR}: WebSocket transport error (browser onerror — detay yok, onclose ile takip edin).`, 'event-err');
  };

  state.ws.onclose = (e) => {
    // INV-VR-xxx server-side error codes arrive in close.reason — surface them to the user
    // BEFORE cleanup() so the status label reflects what actually happened.
    const reason = e.reason ? e.reason : '-';
    const serverInv = reason && /INV-VR-\d{3}/.test(reason);
    logEvent(`WebSocket kapandı (code=${e.code} reason=${reason})`);
    if (e.code !== 1000 && e.code !== 1001 && serverInv) {
      showToast(`${CLIENT_ERR.WS_REJECTED}: Oturum kapandı (${reason})`, 'error');
      logEvent(`${CLIENT_ERR.WS_REJECTED}: server rejected handshake — ${reason}`, 'event-err');
    }
    cleanup();
    if (serverInv) {
      setStatus(`Oturum reddedildi (${reason})`, 'status-error');
    } else {
      setStatus('Bağlantı kapandı', 'status-idle');
    }
  };

  state.ws.onmessage = (e) => {
    if (typeof e.data === 'string') {
      let parsed;
      try {
        parsed = JSON.parse(e.data);
      } catch (err) {
        logEvent(`Geçersiz kontrol mesajı (JSON parse hatası): ${err.message}`, 'event-err');
        return;
      }
      handleControl(parsed);
    } else {
      handleAudio(e.data);
    }
  };
}

function restoreSelectorsAfterFailure() {
  // start() flipped these to disabled — re-enable so Q can retry without reloading.
  els.tenantSelect.disabled = state.tenants.length === 0;
  els.flowSelect.disabled = state.flows.length === 0;
  updateStartButtonState();
}

function handleControl(msg) {
  switch (msg.type) {
    case 'ready':
      logEvent(`Session hazır: ${msg.session_id}`);
      break;
    case 'transcript_user': {
      // Chronological ordering fix: Whisper transcription of the user's speech arrives a beat
      // AFTER the model has already started its audio response (server VAD reacts to the audio
      // immediately; the STT text lags). Appending naively renders the user line BELOW the bot
      // reply it triggered. So if a bot bubble is currently streaming (the reply to THIS very
      // utterance, i.e. last .turn-bot is not yet marked final), insert the user line BEFORE it.
      const userLine = document.createElement('div');
      userLine.className = 'turn-user';
      userLine.textContent = msg.text;
      const streamingBot = els.transcriptLog.querySelector('.turn-bot:last-child');
      if (streamingBot && !streamingBot.dataset.final) {
        // insertBefore keeps the bot bubble as :last-child, so transcript_bot deltas keep
        // updating it correctly.
        els.transcriptLog.insertBefore(userLine, streamingBot);
      } else {
        els.transcriptLog.appendChild(userLine);
      }
      trimLog(els.transcriptLog);
      els.transcriptLog.scrollTop = els.transcriptLog.scrollHeight;
      state.turns += 1;
      els.turnCount.textContent = state.turns;
      break;
    }
    case 'transcript_bot': {
      state.currentBotTranscript += msg.delta;
      const lastLine = els.transcriptLog.querySelector('.turn-bot:last-child');
      if (lastLine && !lastLine.dataset.final) {
        lastLine.textContent = state.currentBotTranscript;
      } else {
        const line = document.createElement('div');
        line.className = 'turn-bot';
        line.textContent = state.currentBotTranscript;
        els.transcriptLog.appendChild(line);
      }
      els.transcriptLog.scrollTop = els.transcriptLog.scrollHeight;
      break;
    }
    case 'response_done': {
      const lastBot = els.transcriptLog.querySelector('.turn-bot:last-child');
      if (lastBot) lastBot.dataset.final = '1';
      state.currentBotTranscript = '';
      setStatus('Bağlı, konuşabilirsin', 'status-listening');
      break;
    }
    case 'first_byte':
      // A new response is starting to speak — lift any barge-in mute so this turn is audible.
      if (state.masterGain) state.masterGain.gain.value = 1;
      recordFirstByte(msg.elapsed_ms);
      setStatus('Bot konuşuyor', 'status-bot-speaking');
      logEvent(`İlk byte: ${msg.elapsed_ms}ms`);
      break;
    case 'barge_in': {
      // Server cancelled the in-flight response because the user started talking. Stop the
      // already-queued bot audio NOW so it doesn't talk over the new turn.
      flushPlayback();
      // Finalize the partial bot bubble so the next response renders as a fresh turn instead of
      // appending onto the interrupted one.
      const interrupted = els.transcriptLog.querySelector('.turn-bot:last-child');
      if (interrupted && !interrupted.dataset.final) interrupted.dataset.final = '1';
      state.currentBotTranscript = '';
      els.bargeLatency.textContent = `${msg.elapsed_ms} ms`;
      logEvent(`Barge-in tepkisi: ${msg.elapsed_ms}ms`, 'event-warn');
      setStatus('Bağlı, konuşabilirsin', 'status-listening');
      break;
    }
    case 'tool_call_started':
      handleToolCallStarted(msg);
      break;
    case 'tool_call_completed':
      handleToolCallCompleted(msg);
      break;
    case 'error':
      setStatus('Hata', 'status-error');
      logEvent(`${msg.code || 'ERROR'}: ${msg.message || JSON.stringify(msg)}`, 'event-err');
      break;
    default:
      logEvent(`unknown control: ${JSON.stringify(msg)}`);
  }
}

function handleAudio(arrayBuffer) {
  if (!state.audioCtx) return;
  const view = new DataView(arrayBuffer);
  const samples = arrayBuffer.byteLength / 2;
  const floatArr = new Float32Array(samples);
  for (let i = 0; i < samples; i++) {
    floatArr[i] = view.getInt16(i * 2, true) / 32768;
  }
  const buf = state.audioCtx.createBuffer(1, samples, SAMPLE_RATE);
  buf.copyToChannel(floatArr, 0);
  const src = state.audioCtx.createBufferSource();
  src.buffer = buf;
  src.connect(state.masterGain || state.audioCtx.destination);

  const now = state.audioCtx.currentTime;
  // Keep a small lead. On start or after an underrun (playbackTime fell to ~now) we schedule
  // PLAYBACK_LEAD_SEC into the future to rebuild the jitter buffer; once ahead, chunks chain
  // back-to-back sample-accurately (no reset, no gap).
  if (state.playbackTime < now + PLAYBACK_LEAD_SEC) state.playbackTime = now + PLAYBACK_LEAD_SEC;
  // Track the source so barge-in can stop it; self-remove from the list when it finishes
  // playing so the array doesn't grow unbounded over a long session.
  state.playbackSources.push(src);
  src.onended = () => {
    const idx = state.playbackSources.indexOf(src);
    if (idx !== -1) state.playbackSources.splice(idx, 1);
  };
  src.start(state.playbackTime);
  state.playbackTime += buf.duration;
}

// Stop and discard every queued/playing bot-audio source immediately, then reset the playback
// cursor to "now". Called on barge-in (server cancelled the response because the user started
// talking) and on cleanup. Without this the browser keeps playing audio it already scheduled,
// so the bot talks over the user's new turn.
function flushPlayback() {
  for (const s of state.playbackSources) {
    try { s.onended = null; s.stop(); } catch (_) { /* already stopped/ended */ }
    try { s.disconnect(); } catch (_) { /* already disconnected */ }
  }
  state.playbackSources = [];
  if (state.audioCtx) state.playbackTime = state.audioCtx.currentTime;
  // Instant hard-mute via DIRECT .value assignment. setValueAtTime() only schedules automation and
  // does NOT silence synchronously (it left audio playing); a direct .value=0 mutes everything routed
  // through masterGain immediately — needed because OpenAI streams TTS faster than realtime, so the
  // browser can hold many seconds of bot audio that stop() on future-scheduled sources won't reliably
  // kill. Restored to 1 on the next response's first_byte.
  if (state.masterGain) state.masterGain.gain.value = 0;
}

function stop() {
  // User-initiated stop — onclose handler will set "Bağlantı kapandı". If WS is already
  // closed/closing, we fall through to cleanup() and explicitly reset to idle so the user
  // sees the page return to a ready state (no error label since this was intentional).
  if (state.ws && state.ws.readyState === WebSocket.OPEN) {
    state.ws.close();
    return;
  }
  cleanup();
  resetIdleStatus();
}

function cleanup() {
  // Codex iter 0 CQ1 fix: cleanup() releases resources and re-enables UI BUT does NOT touch
  // status. Callers (ws.onclose, error paths) set status themselves so error labels survive
  // the cleanup pass. F0 used to reset status to "Hazır" here, which masked actionable errors.
  // Stop any queued bot audio before tearing down the context (must run while audioCtx is alive).
  flushPlayback();
  if (state.micStream) {
    state.micStream.getTracks().forEach((t) => t.stop());
    state.micStream = null;
  }
  if (state.audioCtx) {
    state.audioCtx.close().catch((err) => {
      logEvent(`AudioContext.close uyarısı: ${err.name}: ${err.message}`, 'event-warn');
    });
    state.audioCtx = null;
  }
  state.workletNode = null;
  // Codex iter 0 VQ-LIFECYCLE-D3 fix: purge tool-call HUD Map + DOM so a stale rozet from a
  // previous session doesn't bleed into the next start() (especially after a server-side WS
  // rejection where the rozet may have been mid-pending).
  clearToolCalls();
  state.currentBotTranscript = '';
  els.stopBtn.disabled = true;
  // Re-enable selectors so Q can pick a different tenant/flow without reloading.
  els.tenantSelect.disabled = state.tenants.length === 0;
  els.flowSelect.disabled = state.flows.length === 0;
  updateStartButtonState();
}

function resetIdleStatus() {
  // Helper for paths that genuinely want the "Hazır" idle label (the stop button click path,
  // not the error paths). Kept separate so error labels are never accidentally overwritten.
  setStatus('Hazır', 'status-idle');
}

els.startBtn.addEventListener('click', start);
els.stopBtn.addEventListener('click', stop);

// Kick off dropdown bootstrap as soon as the module loads. Errors degrade gracefully
// (toast + disabled selectors), so this is fire-and-forget.
bootstrapDropdowns();
