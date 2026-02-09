'use strict';

import { formatTimestamp, formatDuration, escapeHtml, truncate } from '../utils/formatter.js';

let _entries = [];
const MAX_ENTRIES = 200;
let _autoScroll = true;

export function initTrafficFeed(wsClient) {
  wsClient.on('webhook_sent', (data) => {
    addEntry('sent', data);
  });

  wsClient.on('callback_received', (data) => {
    addEntry('callback', data);
  });

  wsClient.on('callback_timeout', (data) => {
    addEntry('timeout', data);
  });

  wsClient.on('health_update', (data) => {
    // Optionally show health updates in feed
    const okCount = (data.services || []).filter(s => s.status === 'ok').length;
    const total = (data.services || []).length;
    if (okCount < total) {
      addEntry('health_warning', { services: data.services, ok: okCount, total });
    }
  });

  // Track auto-scroll
  const feed = document.getElementById('traffic-feed-list');
  if (feed) {
    feed.addEventListener('scroll', () => {
      _autoScroll = feed.scrollTop <= 10;
    });
  }

  // Clear button
  document.getElementById('traffic-clear-btn')?.addEventListener('click', () => {
    _entries = [];
    renderFeed();
  });
}

function addEntry(type, data) {
  _entries.unshift({ type, data, timestamp: new Date().toISOString() });
  if (_entries.length > MAX_ENTRIES) _entries.pop();
  renderFeed();
}

function renderFeed() {
  const container = document.getElementById('traffic-feed-list');
  if (!container) return;

  if (_entries.length === 0) {
    container.innerHTML = '<p class="text-slate-500 text-sm text-center py-8">Trafik bekleniyor...</p>';
    return;
  }

  container.innerHTML = _entries.map(e => {
    switch (e.type) {
      case 'sent': return renderSentEntry(e);
      case 'callback': return renderCallbackEntry(e);
      case 'timeout': return renderTimeoutEntry(e);
      case 'health_warning': return renderHealthWarning(e);
      default: return '';
    }
  }).join('');
}

function renderSentEntry(e) {
  const d = e.data;
  const statusClass = d.error ? 'text-red-400' :
    (d.status_code === 202 || d.status_code === 200) ? 'text-emerald-400' : 'text-amber-400';
  const statusText = d.error ? 'ERR' : d.status_code;
  const msgText = d.webhook_body?.data?.message_text || d.event_type || '-';

  return `
    <div class="flex items-start gap-2 py-1.5 border-b border-slate-800 text-xs">
      <span class="text-emerald-500 font-mono w-4 mt-0.5">&#8593;</span>
      <div class="flex-1 min-w-0">
        <div class="flex items-center gap-2">
          <span class="font-medium text-emerald-400">SENT</span>
          <span class="${statusClass}">${statusText}</span>
          <span class="text-slate-500">${formatDuration(d.duration_ms)}</span>
          <span class="text-slate-600 ml-auto">${formatTimestamp(e.timestamp)}</span>
        </div>
        <div class="text-slate-400 truncate">${escapeHtml(truncate(msgText, 80))}</div>
      </div>
    </div>`;
}

function renderCallbackEntry(e) {
  const d = e.data;
  const actionColors = {
    send_message: 'text-emerald-400',
    suggest_reply: 'text-blue-400',
    handoff_to_human: 'text-amber-400',
    no_action: 'text-slate-500'
  };
  const color = actionColors[d.action] || 'text-slate-400';

  return `
    <div class="flex items-start gap-2 py-1.5 border-b border-slate-800 text-xs">
      <span class="text-blue-500 font-mono w-4 mt-0.5">&#8595;</span>
      <div class="flex-1 min-w-0">
        <div class="flex items-center gap-2">
          <span class="font-medium text-blue-400">RECV</span>
          <span class="${color}">${d.action || '-'}</span>
          ${d.processing_time_ms != null ? `<span class="text-slate-500">proc:${d.processing_time_ms}ms</span>` : ''}
          ${d.round_trip_ms != null ? `<span class="text-slate-500">rt:${d.round_trip_ms}ms</span>` : ''}
          <span class="text-slate-600 ml-auto">${formatTimestamp(e.timestamp)}</span>
        </div>
        <div class="text-slate-400 truncate">${escapeHtml(truncate(d.full_body?.data?.message_text || d.full_body?.data?.suggested_reply || '', 80))}</div>
      </div>
    </div>`;
}

function renderTimeoutEntry(e) {
  return `
    <div class="flex items-start gap-2 py-1.5 border-b border-slate-800 text-xs bg-red-900/10">
      <span class="text-red-500 font-mono w-4 mt-0.5">&#10007;</span>
      <div class="flex-1">
        <div class="flex items-center gap-2">
          <span class="font-medium text-red-400">TIMEOUT</span>
          <span class="text-slate-500">req: ${e.data.request_id?.substring(0, 12)}...</span>
          <span class="text-slate-600 ml-auto">${formatTimestamp(e.timestamp)}</span>
        </div>
        <div class="text-red-400/70">No callback within ${e.data.timeout_seconds}s</div>
      </div>
    </div>`;
}

function renderHealthWarning(e) {
  const down = (e.data.services || []).filter(s => s.status !== 'ok').map(s => s.name.replace('Invekto.', ''));
  return `
    <div class="flex items-start gap-2 py-1.5 border-b border-slate-800 text-xs bg-amber-900/10">
      <span class="text-amber-500 font-mono w-4 mt-0.5">!</span>
      <div class="flex-1">
        <span class="text-amber-400">HEALTH</span>
        <span class="text-slate-400 ml-2">${down.join(', ')} down</span>
        <span class="text-slate-600 ml-auto">${formatTimestamp(e.timestamp)}</span>
      </div>
    </div>`;
}

export function renderTrafficFeed() {
  return `
    <div class="bg-slate-800 rounded-lg border border-slate-700 h-full flex flex-col">
      <div class="p-3 border-b border-slate-700 flex items-center justify-between">
        <h3 class="text-sm font-semibold text-slate-300 flex items-center gap-2">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 7h8m0 0v8m0-8l-8 8-4-4-6 6"/></svg>
          Live Traffic
        </h3>
        <button id="traffic-clear-btn" class="text-xs text-slate-500 hover:text-slate-300">Clear</button>
      </div>
      <div id="traffic-feed-list" class="flex-1 overflow-y-auto p-2 font-mono" style="max-height: calc(100vh - 280px);">
        <p class="text-slate-500 text-sm text-center py-8">Trafik bekleniyor...</p>
      </div>
    </div>`;
}
