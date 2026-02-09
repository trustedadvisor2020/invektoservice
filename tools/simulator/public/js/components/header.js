'use strict';

import { statusDotClass } from '../utils/formatter.js';

let _healthData = null;

export function initHeader(wsClient) {
  wsClient.on('health_update', (data) => {
    _healthData = data;
    renderHealthSummary();
  });

  wsClient.on('_connection', ({ connected }) => {
    const dot = document.getElementById('ws-status-dot');
    const text = document.getElementById('ws-status-text');
    if (dot && text) {
      dot.className = `w-2 h-2 rounded-full inline-block ${connected ? 'bg-emerald-500' : 'bg-red-500'}`;
      text.textContent = connected ? 'Connected' : 'Reconnecting...';
    }
  });
}

function renderHealthSummary() {
  const container = document.getElementById('health-summary');
  if (!container || !_healthData) return;

  const services = _healthData.services || [];
  const okCount = services.filter(s => s.status === 'ok').length;
  const total = services.length;

  container.innerHTML = services.map(s => `
    <span class="inline-flex items-center gap-1 text-xs">
      <span class="w-2 h-2 rounded-full ${statusDotClass(s.status)}"></span>
      <span class="${s.status === 'ok' ? 'text-gray-300' : 'text-red-400'}">${s.name.replace('Invekto.', '')}</span>
    </span>
  `).join('');
}
