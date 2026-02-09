'use strict';

import { formatTimestamp, formatDuration, actionBadgeClass, formatJson, escapeHtml } from '../utils/formatter.js';

let _callbacks = [];
const MAX_CALLBACKS = 100;

export function initCallbackPanel(wsClient) {
  wsClient.on('callback_received', (data) => {
    _callbacks.unshift({ ...data, received_at: new Date().toISOString() });
    if (_callbacks.length > MAX_CALLBACKS) _callbacks.pop();
    renderCallbackList();
  });

  wsClient.on('callback_timeout', (data) => {
    _callbacks.unshift({
      ...data,
      action: 'TIMEOUT',
      received_at: new Date().toISOString(),
      _isTimeout: true
    });
    if (_callbacks.length > MAX_CALLBACKS) _callbacks.pop();
    renderCallbackList();
  });
}

function renderCallbackList() {
  const container = document.getElementById('callback-list');
  if (!container) return;

  if (_callbacks.length === 0) {
    container.innerHTML = '<p class="text-slate-500 text-sm text-center py-8">Henuz callback gelmedi. Webhook gonderin ve bekleyin.</p>';
    return;
  }

  container.innerHTML = _callbacks.map((cb, i) => {
    const isTimeout = cb._isTimeout;
    const badgeClass = isTimeout ? 'bg-red-100 text-red-800' : actionBadgeClass(cb.action);

    return `
      <div class="border-b border-slate-700 py-2 ${isTimeout ? 'bg-red-900/20' : ''}">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <span class="px-2 py-0.5 text-xs font-medium rounded ${badgeClass}">${cb.action || '-'}</span>
            <span class="text-xs text-slate-400">req: ${cb.request_id?.substring(0, 12) || '-'}...</span>
          </div>
          <div class="flex items-center gap-3 text-xs text-slate-500">
            ${cb.processing_time_ms != null ? `<span>proc: ${cb.processing_time_ms}ms</span>` : ''}
            ${cb.round_trip_ms != null ? `<span>rt: ${cb.round_trip_ms}ms</span>` : ''}
            <span>${formatTimestamp(cb.received_at)}</span>
          </div>
        </div>
        ${cb.confidence != null ? `<div class="mt-1 text-xs text-slate-400">confidence: ${cb.confidence} | intent: ${cb.full_body?.data?.intent || '-'}</div>` : ''}
        ${isTimeout ? `<div class="mt-1 text-xs text-red-400">Callback timeout (${cb.timeout_seconds}s). Servis down veya processing uzun surmus olabilir.</div>` : ''}
        <details class="mt-1">
          <summary class="text-xs text-slate-500 cursor-pointer hover:text-slate-300">Full JSON</summary>
          <pre class="mt-1 text-xs text-slate-400 bg-slate-900 rounded p-2 max-h-40 overflow-auto">${escapeHtml(formatJson(cb.full_body || cb))}</pre>
        </details>
      </div>`;
  }).join('');
}

export function renderCallbackPanel() {
  return `
    <div class="bg-slate-800 rounded-lg border border-slate-700 h-full flex flex-col">
      <div class="p-3 border-b border-slate-700 flex items-center justify-between">
        <h3 class="text-sm font-semibold text-slate-300 flex items-center gap-2">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>
          Callbacks Received
        </h3>
        <span id="callback-count" class="text-xs text-slate-500">${_callbacks.length} items</span>
      </div>
      <div id="callback-list" class="flex-1 overflow-y-auto p-3">
        <p class="text-slate-500 text-sm text-center py-8">Henuz callback gelmedi. Webhook gonderin ve bekleyin.</p>
      </div>
    </div>`;
}
