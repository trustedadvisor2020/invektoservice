'use strict';

import { api } from '../services/api-client.js';
import { getCurrentToken } from './jwt-panel.js';
import { escapeHtml } from '../utils/formatter.js';

export function initWebhookPanel() {
  document.getElementById('webhook-form')?.addEventListener('submit', async (e) => {
    e.preventDefault();
    await sendWebhook();
  });

  // Quick-fill buttons
  document.querySelectorAll('[data-quick-fill]').forEach(btn => {
    btn.addEventListener('click', () => {
      const input = document.getElementById('wh-message-text');
      if (input) input.value = btn.dataset.quickFill;
    });
  });
}

async function sendWebhook() {
  const token = getCurrentToken();
  const resultEl = document.getElementById('webhook-result');

  if (!token) {
    if (resultEl) resultEl.innerHTML = '<p class="text-red-400 text-sm">Once JWT token olusturun</p>';
    return;
  }

  const payload = {
    event_type: document.getElementById('wh-event-type')?.value || 'new_message',
    chat_id: parseInt(document.getElementById('wh-chat-id')?.value || '1', 10),
    channel: document.getElementById('wh-channel')?.value || 'whatsapp',
    data: {
      phone: document.getElementById('wh-phone')?.value || '+905551234567',
      customer_name: document.getElementById('wh-customer-name')?.value || 'Test Musteri',
      message_text: document.getElementById('wh-message-text')?.value || '',
      message_source: document.getElementById('wh-message-source')?.value || 'CUSTOMER'
    }
  };

  const targetService = document.getElementById('wh-target-service')?.value || 'automation';
  let targetUrl = null;

  if (targetService === 'custom') {
    targetUrl = document.getElementById('wh-custom-url')?.value;
  }

  const btn = document.getElementById('webhook-send-btn');
  if (btn) { btn.disabled = true; btn.textContent = 'Sending...'; }

  try {
    const result = await api.sendWebhook(payload, token, targetUrl);

    if (resultEl) {
      const statusClass = result.error ? 'text-red-400' :
        (result.status_code === 202 || result.status_code === 200) ? 'text-emerald-400' : 'text-amber-400';
      const statusText = result.error ? `Error: ${result.error}` : `HTTP ${result.status_code}`;

      resultEl.innerHTML = `
        <div class="text-sm space-y-1">
          <p class="${statusClass}">${statusText} (${result.duration_ms}ms)</p>
          <p class="text-xs text-slate-500">Request ID: ${result.request_id}</p>
          ${result.response_body ? `<pre class="text-xs text-slate-400 bg-slate-900 rounded p-1 max-h-20 overflow-auto">${escapeHtml(typeof result.response_body === 'string' ? result.response_body : JSON.stringify(result.response_body, null, 2))}</pre>` : ''}
        </div>`;
    }
  } catch (err) {
    if (resultEl) resultEl.innerHTML = `<p class="text-red-400 text-sm">${err.message}</p>`;
  } finally {
    if (btn) { btn.disabled = false; btn.textContent = 'Send Webhook'; }
  }
}

export function renderWebhookPanel() {
  return `
    <div class="bg-slate-800 rounded-lg p-4 border border-slate-700">
      <h3 class="text-sm font-semibold text-slate-300 mb-3 flex items-center gap-2">
        <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 10V3L4 14h7v7l9-11h-7z"/></svg>
        Webhook Sender
      </h3>
      <form id="webhook-form" class="space-y-2">
        <div class="grid grid-cols-2 gap-2">
          <div>
            <label class="text-xs text-slate-400">Event Type</label>
            <select id="wh-event-type"
              class="w-full bg-slate-900 border border-slate-600 rounded px-2 py-1 text-sm text-white focus:border-blue-500 focus:outline-none">
              <option value="new_message">new_message</option>
              <option value="conversation_started">conversation_started</option>
              <option value="conversation_closed">conversation_closed</option>
              <option value="tag_changed">tag_changed</option>
              <option value="agent_assigned">agent_assigned</option>
            </select>
          </div>
          <div>
            <label class="text-xs text-slate-400">Target</label>
            <select id="wh-target-service"
              class="w-full bg-slate-900 border border-slate-600 rounded px-2 py-1 text-sm text-white focus:border-blue-500 focus:outline-none">
              <option value="automation">Automation (:7108)</option>
              <option value="custom">Custom URL</option>
            </select>
          </div>
        </div>
        <div id="custom-url-row" class="hidden">
          <label class="text-xs text-slate-400">Custom URL</label>
          <input type="text" id="wh-custom-url" placeholder="http://localhost:PORT/path"
            class="w-full bg-slate-900 border border-slate-600 rounded px-2 py-1 text-sm text-white focus:border-blue-500 focus:outline-none"/>
        </div>
        <div class="grid grid-cols-2 gap-2">
          <div>
            <label class="text-xs text-slate-400">Chat ID</label>
            <input type="number" id="wh-chat-id" value="1" min="1"
              class="w-full bg-slate-900 border border-slate-600 rounded px-2 py-1 text-sm text-white focus:border-blue-500 focus:outline-none"/>
          </div>
          <div>
            <label class="text-xs text-slate-400">Channel</label>
            <select id="wh-channel"
              class="w-full bg-slate-900 border border-slate-600 rounded px-2 py-1 text-sm text-white focus:border-blue-500 focus:outline-none">
              <option value="whatsapp">whatsapp</option>
              <option value="web">web</option>
              <option value="instagram">instagram</option>
              <option value="facebook">facebook</option>
              <option value="telegram">telegram</option>
            </select>
          </div>
        </div>
        <div class="grid grid-cols-2 gap-2">
          <div>
            <label class="text-xs text-slate-400">Phone</label>
            <input type="text" id="wh-phone" value="+905551234567"
              class="w-full bg-slate-900 border border-slate-600 rounded px-2 py-1 text-sm text-white focus:border-blue-500 focus:outline-none"/>
          </div>
          <div>
            <label class="text-xs text-slate-400">Customer Name</label>
            <input type="text" id="wh-customer-name" value="Test Musteri"
              class="w-full bg-slate-900 border border-slate-600 rounded px-2 py-1 text-sm text-white focus:border-blue-500 focus:outline-none"/>
          </div>
        </div>
        <div>
          <label class="text-xs text-slate-400">Message Source</label>
          <select id="wh-message-source"
            class="w-full bg-slate-900 border border-slate-600 rounded px-2 py-1 text-sm text-white focus:border-blue-500 focus:outline-none">
            <option value="CUSTOMER">CUSTOMER</option>
            <option value="AGENT">AGENT</option>
          </select>
        </div>
        <div>
          <label class="text-xs text-slate-400">Message Text</label>
          <textarea id="wh-message-text" rows="2" placeholder="Merhaba..."
            class="w-full bg-slate-900 border border-slate-600 rounded px-2 py-1 text-sm text-white focus:border-blue-500 focus:outline-none resize-none"></textarea>
        </div>
        <div class="flex flex-wrap gap-1">
          <button type="button" data-quick-fill="Merhaba" class="px-2 py-0.5 text-xs bg-slate-700 hover:bg-slate-600 text-slate-300 rounded">Merhaba</button>
          <button type="button" data-quick-fill="1" class="px-2 py-0.5 text-xs bg-slate-700 hover:bg-slate-600 text-slate-300 rounded">1</button>
          <button type="button" data-quick-fill="2" class="px-2 py-0.5 text-xs bg-slate-700 hover:bg-slate-600 text-slate-300 rounded">2</button>
          <button type="button" data-quick-fill="3" class="px-2 py-0.5 text-xs bg-slate-700 hover:bg-slate-600 text-slate-300 rounded">3</button>
          <button type="button" data-quick-fill="4" class="px-2 py-0.5 text-xs bg-slate-700 hover:bg-slate-600 text-slate-300 rounded">4</button>
          <button type="button" data-quick-fill="5" class="px-2 py-0.5 text-xs bg-slate-700 hover:bg-slate-600 text-slate-300 rounded">5</button>
          <button type="button" data-quick-fill="0" class="px-2 py-0.5 text-xs bg-slate-700 hover:bg-slate-600 text-slate-300 rounded">0</button>
        </div>
        <button type="submit" id="webhook-send-btn"
          class="w-full bg-emerald-600 hover:bg-emerald-700 text-white text-sm font-medium py-1.5 rounded transition-colors">
          Send Webhook
        </button>
      </form>
      <div id="webhook-result" class="mt-2"></div>
    </div>`;
}
