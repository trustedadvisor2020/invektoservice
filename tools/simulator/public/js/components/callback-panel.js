'use strict';

import { formatTimestamp, formatDuration, actionBadgeClass, formatJson, escapeHtml } from '../utils/formatter.js';

let _callbacks = [];
const MAX_CALLBACKS = 100;
let _wsInitialized = false;

export function initCallbackPanel(wsClient) {
  if (_wsInitialized) return;
  _wsInitialized = true;

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
    container.innerHTML = '<p class="text-slate-400 text-sm text-center py-8">Henuz callback gelmedi. Webhook gonderin ve bekleyin.</p>';
    return;
  }

  container.innerHTML = _callbacks.map((cb, i) => {
    const isTimeout = cb._isTimeout;
    const isError = cb.action === 'error';
    const badgeClass = isTimeout ? 'bg-red-100 text-red-800' : actionBadgeClass(cb.action);
    const errorMsg = cb.full_body?.data?.error_message;

    return `
      <div class="border-b border-slate-200 py-2 ${isTimeout || isError ? 'bg-red-50' : ''}">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-2">
            <span class="px-2 py-0.5 text-sm font-medium rounded ${badgeClass}">${cb.action || '-'}</span>
            <span class="text-sm text-slate-500">req: ${cb.request_id?.substring(0, 12) || '-'}...</span>
          </div>
          <div class="flex items-center gap-3 text-sm text-slate-500">
            ${cb.processing_time_ms != null ? `<span>proc: ${cb.processing_time_ms}ms</span>` : ''}
            ${cb.round_trip_ms != null ? `<span>rt: ${cb.round_trip_ms}ms</span>` : ''}
            <span>${formatTimestamp(cb.received_at)}</span>
          </div>
        </div>
        ${cb.confidence != null ? `<div class="mt-1 text-sm text-slate-500">confidence: ${cb.confidence} | intent: ${cb.full_body?.data?.intent || '-'}</div>` : ''}
        ${isTimeout ? `<div class="mt-1 text-sm text-red-600">Callback timeout (${cb.timeout_seconds}s). Servis down veya processing uzun surmus olabilir.</div>` : ''}
        ${isError && errorMsg ? `<div class="mt-1 text-sm text-red-600">${escapeHtml(errorMsg)}</div>` : ''}
        <details class="mt-1">
          <summary class="text-sm text-slate-500 cursor-pointer hover:text-slate-700">Full JSON</summary>
          <pre class="mt-1 text-sm text-slate-600 bg-slate-50 rounded p-2 max-h-40 overflow-auto">${escapeHtml(formatJson(cb.full_body || cb))}</pre>
        </details>
      </div>`;
  }).join('');
}

export function renderCallbackPanel() {
  return `
    <div class="bg-white rounded-lg border border-slate-200 shadow-sm flex flex-col">
      <div class="p-3 border-b border-slate-200 flex items-center justify-between">
        <h3 class="text-sm font-semibold text-slate-700 flex items-center gap-2">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>
          Callbacks
        </h3>
        <span id="callback-count" class="text-sm text-slate-500">${_callbacks.length} items</span>
      </div>
      <div id="callback-list" class="flex-1 overflow-y-auto p-3" style="max-height: 350px;">
        <p class="text-slate-400 text-sm text-center py-4">Henuz callback gelmedi. Webhook gonderin ve bekleyin.</p>
      </div>
    </div>`;
}
