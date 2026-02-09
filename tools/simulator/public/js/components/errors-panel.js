'use strict';

import { api } from '../services/api-client.js';
import { formatTimestamp, escapeHtml } from '../utils/formatter.js';

let _errorData = null;

export function initErrorsPanel() {
  document.getElementById('errors-refresh-btn')?.addEventListener('click', loadErrors);
  document.getElementById('errors-hours-filter')?.addEventListener('change', loadErrors);
}

async function loadErrors() {
  const hours = document.getElementById('errors-hours-filter')?.value || '24';

  try {
    const data = await api.getLogErrors(parseInt(hours, 10));

    if (data.error) {
      renderErrorMessage(data.message || 'Backend not reachable');
      return;
    }

    _errorData = data;
    renderErrorChart();
    renderErrorTotal();
  } catch (err) {
    renderErrorMessage(err.message);
  }
}

function renderErrorMessage(msg) {
  const chart = document.getElementById('error-chart');
  if (chart) chart.innerHTML = `<p class="text-red-400 text-sm text-center py-4">${escapeHtml(msg)}</p>`;
}

function renderErrorChart() {
  const chart = document.getElementById('error-chart');
  if (!chart || !_errorData) return;

  const buckets = _errorData.buckets || [];
  if (buckets.length === 0) {
    chart.innerHTML = '<p class="text-slate-500 text-sm text-center py-8">Hata bulunamadi</p>';
    return;
  }

  const maxCount = Math.max(...buckets.map(b => b.count || b.Count || 0), 1);

  chart.innerHTML = `
    <div class="flex items-end gap-1 h-32">
      ${buckets.map(b => {
        const count = b.count || b.Count || 0;
        const height = Math.max((count / maxCount) * 100, 2);
        const hour = new Date(b.hour || b.Hour).getHours();
        return `
          <div class="flex flex-col items-center flex-1 min-w-0" title="${count} errors at ${hour}:00">
            <span class="text-xs text-slate-500 mb-1">${count || ''}</span>
            <div class="w-full bg-red-500/80 rounded-t" style="height: ${height}%"></div>
            <span class="text-xs text-slate-600 mt-1">${String(hour).padStart(2, '0')}</span>
          </div>`;
      }).join('')}
    </div>`;
}

function renderErrorTotal() {
  const el = document.getElementById('error-total');
  if (el && _errorData) {
    el.textContent = `Total: ${_errorData.total || 0} errors`;
  }
}

export function renderErrorsPanel() {
  return `
    <div class="space-y-3">
      <div class="flex items-center justify-between">
        <h3 class="text-sm font-semibold text-slate-300 flex items-center gap-2">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/></svg>
          Error Timeline
        </h3>
        <div class="flex items-center gap-2">
          <span id="error-total" class="text-xs text-slate-500"></span>
          <select id="errors-hours-filter"
            class="bg-slate-900 border border-slate-600 rounded px-2 py-1 text-xs text-white">
            <option value="1">1 saat</option>
            <option value="6">6 saat</option>
            <option value="12">12 saat</option>
            <option value="24" selected>24 saat</option>
          </select>
          <button id="errors-refresh-btn"
            class="px-3 py-1 text-xs bg-blue-600 hover:bg-blue-700 text-white rounded">Refresh</button>
        </div>
      </div>
      <div id="error-chart" class="bg-slate-800 rounded-lg p-4 border border-slate-700">
        <p class="text-slate-500 text-sm text-center py-8">Refresh ile hatalari yukleyin</p>
      </div>
    </div>`;
}
