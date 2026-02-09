'use strict';

import { api } from '../services/api-client.js';
import { statusDotClass, formatDuration } from '../utils/formatter.js';

let _healthData = null;

export function initHealthPanel(wsClient) {
  wsClient.on('health_update', (data) => {
    _healthData = data;
    renderHealthCards();
  });
}

async function refreshHealth() {
  try {
    _healthData = await api.checkHealth();
    renderHealthCards();
  } catch (err) {
    console.error('Health check failed:', err);
  }
}

function renderHealthCards() {
  const container = document.getElementById('health-cards');
  if (!container || !_healthData) return;

  const services = _healthData.services || [];

  container.innerHTML = services.map(s => {
    const dotClass = statusDotClass(s.status);
    const statusText = s.status === 'ok' ? 'Healthy' : s.status === 'down' ? 'Down' : s.status === 'timeout' ? 'Timeout' : 'Error';
    const statusColor = s.status === 'ok' ? 'text-emerald-400' : 'text-red-400';

    return `
      <div class="bg-slate-800 rounded-lg p-4 border border-slate-700">
        <div class="flex items-center justify-between mb-2">
          <h4 class="text-sm font-medium text-white">${s.name.replace('Invekto.', '')}</h4>
          <span class="w-3 h-3 rounded-full ${dotClass}"></span>
        </div>
        <p class="${statusColor} text-sm font-medium">${statusText}</p>
        <p class="text-xs text-slate-500 mt-1">${s.url}</p>
        <p class="text-xs text-slate-500">${formatDuration(s.response_time_ms)}</p>
        ${s.error ? `<p class="text-xs text-red-400 mt-1">${s.error}</p>` : ''}
      </div>`;
  }).join('');
}

export function renderHealthPanel() {
  return `
    <div class="space-y-3">
      <div class="flex items-center justify-between">
        <h3 class="text-sm font-semibold text-slate-300 flex items-center gap-2">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z"/></svg>
          Service Health
        </h3>
        <button onclick="document.dispatchEvent(new Event('refresh-health'))"
          class="text-xs text-blue-400 hover:text-blue-300">Refresh</button>
      </div>
      <div id="health-cards" class="grid grid-cols-3 gap-3">
        <div class="bg-slate-800 rounded-lg p-4 border border-slate-700 animate-pulse"><div class="h-4 bg-slate-700 rounded w-24"></div></div>
        <div class="bg-slate-800 rounded-lg p-4 border border-slate-700 animate-pulse"><div class="h-4 bg-slate-700 rounded w-24"></div></div>
        <div class="bg-slate-800 rounded-lg p-4 border border-slate-700 animate-pulse"><div class="h-4 bg-slate-700 rounded w-24"></div></div>
      </div>
    </div>`;
}
