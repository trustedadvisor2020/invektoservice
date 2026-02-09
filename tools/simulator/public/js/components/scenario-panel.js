'use strict';

import { api } from '../services/api-client.js';
import { getCurrentToken } from './jwt-panel.js';
import { formatTimestamp, escapeHtml } from '../utils/formatter.js';

let _scenarios = [];
let _runningScenario = null;
let _steps = [];

export async function initScenarioPanel(wsClient) {
  wsClient.on('scenario_step', (data) => {
    _steps.unshift(data);
    if (_steps.length > 50) _steps.pop();

    if (data.status === 'completed' || data.status === 'stopped' || data.status === 'error') {
      _runningScenario = null;
    }

    renderScenarioProgress();
  });

  // Load scenarios
  try {
    const result = await api.getScenarios();
    _scenarios = result.scenarios || [];
    renderScenarioCards();
  } catch (err) {
    console.error('Failed to load scenarios:', err);
  }
}

async function runScenario(key) {
  const token = getCurrentToken();
  if (!token) {
    alert('Once JWT token olusturun');
    return;
  }

  _steps = [];
  _runningScenario = key;
  renderScenarioCards();
  renderScenarioProgress();

  try {
    const tenantId = document.getElementById('jwt-tenant-id')?.value;
    await api.runScenario(key, token, tenantId ? parseInt(tenantId, 10) : undefined);
  } catch (err) {
    _runningScenario = null;
    renderScenarioCards();
    console.error('Scenario error:', err);
  }
}

async function stopScenario() {
  try {
    await api.stopScenario();
    _runningScenario = null;
    renderScenarioCards();
  } catch (err) {
    console.error('Stop scenario error:', err);
  }
}

function renderScenarioCards() {
  const container = document.getElementById('scenario-cards');
  if (!container) return;

  container.innerHTML = _scenarios.map(s => {
    const isRunning = _runningScenario === s.key;
    const isDisabled = _runningScenario && !isRunning;

    return `
      <div class="bg-slate-800 rounded-lg p-4 border ${isRunning ? 'border-blue-500' : 'border-slate-700'}">
        <h4 class="text-sm font-medium text-white">${s.name}</h4>
        <p class="text-xs text-slate-400 mt-1">${s.description}</p>
        ${s.note ? `<p class="text-xs text-amber-400/70 mt-1">${s.note}</p>` : ''}
        <div class="flex items-center justify-between mt-3">
          <span class="text-xs text-slate-500">${s.step_count} adim</span>
          ${isRunning
            ? `<button onclick="window.__stopScenario()" class="px-3 py-1 text-xs bg-red-600 hover:bg-red-700 text-white rounded">Stop</button>`
            : `<button onclick="window.__runScenario('${s.key}')" ${isDisabled ? 'disabled' : ''}
                class="px-3 py-1 text-xs ${isDisabled ? 'bg-slate-700 text-slate-500 cursor-not-allowed' : 'bg-blue-600 hover:bg-blue-700 text-white'} rounded">Run</button>`
          }
        </div>
      </div>`;
  }).join('');
}

function renderScenarioProgress() {
  const container = document.getElementById('scenario-progress');
  if (!container) return;

  if (_steps.length === 0) {
    container.innerHTML = '<p class="text-slate-500 text-sm text-center py-4">Senaryo calistirin...</p>';
    return;
  }

  container.innerHTML = _steps.map(step => {
    const statusColors = {
      sending: 'text-blue-400',
      sent: 'text-emerald-400',
      completed: 'text-emerald-400',
      failed: 'text-red-400',
      error: 'text-red-400',
      stopped: 'text-amber-400'
    };
    const color = statusColors[step.status] || 'text-slate-400';
    const icon = step.status === 'sending' ? '&#8987;' :
      step.status === 'sent' || step.status === 'completed' ? '&#10003;' :
      step.status === 'failed' || step.status === 'error' ? '&#10007;' :
      step.status === 'stopped' ? '&#9632;' : '&#8226;';

    return `
      <div class="flex items-start gap-2 py-1 text-xs">
        <span class="${color} w-4">${icon}</span>
        <div class="flex-1">
          <span class="${color} font-medium">[${step.step_index}/${step.step_total}]</span>
          <span class="text-slate-300 ml-1">${escapeHtml(step.message || '')}</span>
          ${step.expected ? `<span class="text-slate-500 ml-1">-- expected: ${step.expected.action || step.expected.description}</span>` : ''}
          ${step.result ? `<span class="text-slate-500 ml-1">(${step.result.status_code}, ${step.result.duration_ms}ms)</span>` : ''}
        </div>
      </div>`;
  }).join('');
}

// Expose to global for onclick handlers
window.__runScenario = runScenario;
window.__stopScenario = stopScenario;

export function renderScenarioPanel() {
  return `
    <div class="space-y-4">
      <h3 class="text-sm font-semibold text-slate-300 flex items-center gap-2">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M14.752 11.168l-3.197-2.132A1 1 0 0010 9.87v4.263a1 1 0 001.555.832l3.197-2.132a1 1 0 000-1.664z"/><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>
        Preset Scenarios
      </h3>
      <div id="scenario-cards" class="grid grid-cols-2 lg:grid-cols-3 gap-3">
        <div class="bg-slate-800 rounded-lg p-4 border border-slate-700 animate-pulse"><div class="h-4 bg-slate-700 rounded w-32"></div></div>
      </div>
      <div class="bg-slate-800 rounded-lg p-4 border border-slate-700">
        <h4 class="text-sm font-medium text-slate-300 mb-2">Execution Log</h4>
        <div id="scenario-progress" class="max-h-64 overflow-y-auto font-mono">
          <p class="text-slate-500 text-sm text-center py-4">Senaryo calistirin...</p>
        </div>
      </div>
    </div>`;
}
