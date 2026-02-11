'use strict';

import { api } from '../services/api-client.js';
import { formatTimestamp, escapeHtml, truncate } from '../utils/formatter.js';

let _logs = [];
let _autoRefresh = false;
let _autoRefreshTimer = null;

export function initLogsPanel() {
  document.getElementById('logs-refresh-btn')?.addEventListener('click', loadLogs);
  document.getElementById('logs-auto-toggle')?.addEventListener('change', (e) => {
    _autoRefresh = e.target.checked;
    if (_autoRefresh) {
      _autoRefreshTimer = setInterval(loadLogs, 5000);
      loadLogs();
    } else {
      if (_autoRefreshTimer) clearInterval(_autoRefreshTimer);
    }
  });
  document.getElementById('logs-search-btn')?.addEventListener('click', loadLogs);
}

async function loadLogs() {
  const level = [];
  if (document.getElementById('log-level-info')?.checked) level.push('Information');
  if (document.getElementById('log-level-warn')?.checked) level.push('Warning');
  if (document.getElementById('log-level-error')?.checked) level.push('Error');

  const service = document.getElementById('log-service-filter')?.value || '';
  const search = document.getElementById('log-search-input')?.value || '';
  const limit = document.getElementById('log-limit')?.value || '50';

  try {
    const params = {};
    if (level.length > 0 && level.length < 3) params.level = level.join(',');
    if (service) params.service = service;
    if (search) params.search = search;
    params.limit = limit;

    const data = await api.getLogs(params);

    if (data.error) {
      renderLogError(data.message || 'Backend not reachable');
      return;
    }

    _logs = data.entries || [];
    renderLogEntries();
  } catch (err) {
    renderLogError(err.message);
  }
}

function renderLogError(msg) {
  const container = document.getElementById('logs-table-body');
  if (container) {
    container.innerHTML = `<tr><td colspan="5" class="text-center text-red-600 py-4 text-sm">${escapeHtml(msg)}</td></tr>`;
  }
}

function renderLogEntries() {
  const container = document.getElementById('logs-table-body');
  if (!container) return;

  if (_logs.length === 0) {
    container.innerHTML = '<tr><td colspan="5" class="text-center text-slate-400 py-4 text-sm">Log bulunamadi</td></tr>';
    return;
  }

  container.innerHTML = _logs.map(log => {
    const levelColors = {
      'Information': 'text-blue-600',
      'Warning': 'text-amber-600',
      'Error': 'text-red-600',
      'Debug': 'text-slate-400'
    };
    const color = levelColors[log.Level || log.level] || 'text-slate-500';
    const level = (log.Level || log.level || '-').substring(0, 4).toUpperCase();

    return `
      <tr class="border-b border-slate-100 hover:bg-slate-50">
        <td class="py-1.5 px-2 text-sm text-slate-500 font-mono">${formatTimestamp(log.Timestamp || log.timestamp)}</td>
        <td class="py-1.5 px-2 text-sm ${color} font-medium">${level}</td>
        <td class="py-1.5 px-2 text-sm text-slate-600">${escapeHtml((log.Service || log.service || '-').replace('Invekto.', ''))}</td>
        <td class="py-1.5 px-2 text-sm text-slate-700">${escapeHtml(truncate(log.Message || log.message || '-', 120))}</td>
        <td class="py-1.5 px-2 text-sm text-slate-500">${escapeHtml(log.Route || log.route || '')}</td>
      </tr>`;
  }).join('');
}

export function renderLogsPanel() {
  return `
    <div class="space-y-3">
      <div class="flex items-center justify-between">
        <h3 class="text-sm font-semibold text-slate-700 flex items-center gap-2">
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"/></svg>
          Service Logs (via Backend API)
        </h3>
      </div>
      <div class="flex flex-wrap items-center gap-3 bg-white rounded-lg p-3 border border-slate-200 shadow-sm">
        <div class="flex items-center gap-2">
          <label class="flex items-center gap-1 text-sm text-slate-600">
            <input type="checkbox" id="log-level-info" checked class="rounded border-slate-300"> INFO
          </label>
          <label class="flex items-center gap-1 text-sm text-amber-600">
            <input type="checkbox" id="log-level-warn" checked class="rounded border-slate-300"> WARN
          </label>
          <label class="flex items-center gap-1 text-sm text-red-600">
            <input type="checkbox" id="log-level-error" checked class="rounded border-slate-300"> ERR
          </label>
        </div>
        <select id="log-service-filter"
          class="bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-900">
          <option value="">All services</option>
          <option value="Invekto.Backend">Backend</option>
          <option value="Invekto.ChatAnalysis">ChatAnalysis</option>
          <option value="Invekto.Automation">Automation</option>
          <option value="Invekto.AgentAI">AgentAI</option>
        </select>
        <input type="text" id="log-search-input" placeholder="Search..."
          class="bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-900 w-32 focus:border-blue-500 focus:outline-none"/>
        <select id="log-limit" class="bg-slate-50 border border-slate-300 rounded px-2 py-1.5 text-sm text-slate-900">
          <option value="50">50</option>
          <option value="100">100</option>
          <option value="200">200</option>
        </select>
        <button id="logs-refresh-btn"
          class="px-3 py-1.5 text-sm bg-blue-600 hover:bg-blue-700 text-white rounded">Refresh</button>
        <button id="logs-search-btn"
          class="px-3 py-1.5 text-sm bg-slate-200 hover:bg-slate-300 text-slate-700 rounded">Search</button>
        <label class="flex items-center gap-1 text-sm text-slate-600 ml-auto">
          <input type="checkbox" id="logs-auto-toggle" class="rounded border-slate-300"> Auto (5s)
        </label>
      </div>
      <div class="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden">
        <table class="w-full">
          <thead>
            <tr class="bg-slate-50 text-sm text-slate-600">
              <th class="text-left py-2 px-2 w-28">Time</th>
              <th class="text-left py-2 px-2 w-14">Level</th>
              <th class="text-left py-2 px-2 w-24">Service</th>
              <th class="text-left py-2 px-2">Message</th>
              <th class="text-left py-2 px-2 w-32">Route</th>
            </tr>
          </thead>
          <tbody id="logs-table-body">
            <tr><td colspan="5" class="text-center text-slate-400 py-8 text-sm">Refresh ile loglari yukleyin</td></tr>
          </tbody>
        </table>
      </div>
    </div>`;
}
