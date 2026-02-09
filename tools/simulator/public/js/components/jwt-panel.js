'use strict';

import { api } from '../services/api-client.js';
import { formatJson, formatTimestamp } from '../utils/formatter.js';

let _currentToken = null;
let _onTokenChange = null;

export function initJwtPanel(onTokenChange) {
  _onTokenChange = onTokenChange;

  document.getElementById('jwt-form')?.addEventListener('submit', async (e) => {
    e.preventDefault();
    await generateToken();
  });
}

async function generateToken() {
  const tenantId = document.getElementById('jwt-tenant-id')?.value;
  const userId = document.getElementById('jwt-user-id')?.value;
  const role = document.getElementById('jwt-role')?.value;
  const expiry = document.getElementById('jwt-expiry')?.value;

  const resultEl = document.getElementById('jwt-result');
  const tokenEl = document.getElementById('jwt-token-display');
  const claimsEl = document.getElementById('jwt-claims-display');

  if (!tenantId || !userId) {
    if (resultEl) resultEl.innerHTML = '<p class="text-red-400 text-sm">tenant_id ve user_id zorunlu</p>';
    return;
  }

  try {
    const data = await api.generateToken({
      tenant_id: parseInt(tenantId, 10),
      user_id: parseInt(userId, 10),
      role,
      expires_in_minutes: parseInt(expiry, 10) || 60
    });

    if (data.error) {
      if (resultEl) resultEl.innerHTML = `<p class="text-red-400 text-sm">${data.error}</p>`;
      return;
    }

    _currentToken = data.token;
    if (_onTokenChange) _onTokenChange(data.token);

    if (tokenEl) {
      tokenEl.value = data.token;
      tokenEl.classList.remove('hidden');
    }
    if (claimsEl) {
      claimsEl.textContent = formatJson(data.decoded);
      claimsEl.classList.remove('hidden');
    }
    if (resultEl) {
      resultEl.innerHTML = `<p class="text-emerald-400 text-sm">Token generated. Expires: ${formatTimestamp(data.expires_at)}</p>`;
    }
  } catch (err) {
    if (resultEl) resultEl.innerHTML = `<p class="text-red-400 text-sm">${err.message}</p>`;
  }
}

export function getCurrentToken() {
  return _currentToken;
}

export function renderJwtPanel() {
  return `
    <div class="bg-slate-800 rounded-lg p-4 border border-slate-700">
      <h3 class="text-sm font-semibold text-slate-300 mb-3 flex items-center gap-2">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 7a2 2 0 012 2m4 0a6 6 0 01-7.743 5.743L11 17H9v2H7v2H4a1 1 0 01-1-1v-2.586a1 1 0 01.293-.707l5.964-5.964A6 6 0 1121 9z"/></svg>
        JWT Token
      </h3>
      <form id="jwt-form" class="space-y-2">
        <div class="grid grid-cols-2 gap-2">
          <div>
            <label class="text-xs text-slate-400">Tenant ID</label>
            <input type="number" id="jwt-tenant-id" value="1" min="1"
              class="w-full bg-slate-900 border border-slate-600 rounded px-2 py-1 text-sm text-white focus:border-blue-500 focus:outline-none"/>
          </div>
          <div>
            <label class="text-xs text-slate-400">User ID</label>
            <input type="number" id="jwt-user-id" value="1" min="1"
              class="w-full bg-slate-900 border border-slate-600 rounded px-2 py-1 text-sm text-white focus:border-blue-500 focus:outline-none"/>
          </div>
        </div>
        <div class="grid grid-cols-2 gap-2">
          <div>
            <label class="text-xs text-slate-400">Role</label>
            <select id="jwt-role"
              class="w-full bg-slate-900 border border-slate-600 rounded px-2 py-1 text-sm text-white focus:border-blue-500 focus:outline-none">
              <option value="agent">agent</option>
              <option value="admin">admin</option>
              <option value="service">service</option>
            </select>
          </div>
          <div>
            <label class="text-xs text-slate-400">Expiry (dk)</label>
            <input type="number" id="jwt-expiry" value="60" min="1"
              class="w-full bg-slate-900 border border-slate-600 rounded px-2 py-1 text-sm text-white focus:border-blue-500 focus:outline-none"/>
          </div>
        </div>
        <button type="submit"
          class="w-full bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium py-1.5 rounded transition-colors">
          Generate Token
        </button>
      </form>
      <div id="jwt-result" class="mt-2"></div>
      <textarea id="jwt-token-display" readonly rows="2"
        class="hidden w-full mt-2 bg-slate-900 border border-slate-600 rounded px-2 py-1 text-xs text-emerald-400 font-mono resize-none cursor-pointer"
        onclick="this.select()"></textarea>
      <pre id="jwt-claims-display"
        class="hidden mt-2 bg-slate-900 border border-slate-600 rounded px-2 py-1 text-xs text-slate-300 font-mono overflow-x-auto max-h-24"></pre>
    </div>`;
}
