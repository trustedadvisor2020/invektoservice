'use strict';

import { wsClient } from './services/ws-client.js';
import { initHeader } from './components/header.js';
import { initJwtPanel, renderJwtPanel } from './components/jwt-panel.js';
import { initWebhookPanel, renderWebhookPanel } from './components/webhook-panel.js';
import { initCallbackPanel, renderCallbackPanel } from './components/callback-panel.js';
import { initHealthPanel, renderHealthPanel } from './components/health-panel.js';
import { initTrafficFeed, renderTrafficFeed } from './components/traffic-feed.js';
import { initLogsPanel, renderLogsPanel } from './components/logs-panel.js';
import { initErrorsPanel, renderErrorsPanel } from './components/errors-panel.js';
import { initScenarioPanel, renderScenarioPanel } from './components/scenario-panel.js';

// --- Tab routing ---
const tabs = {
  test: {
    label: 'Test',
    render: () => `
      <div class="grid grid-cols-1 lg:grid-cols-5 gap-4 h-full">
        <div class="lg:col-span-2 space-y-4">
          ${renderJwtPanel()}
          ${renderWebhookPanel()}
        </div>
        <div class="lg:col-span-3 space-y-4">
          ${renderTrafficFeed()}
          ${renderCallbackPanel()}
        </div>
      </div>`,
    init: () => {
      initJwtPanel((token) => {
        // Token changed callback - could enable buttons etc.
      });
      initWebhookPanel();
      initTrafficFeed(wsClient);
      initCallbackPanel(wsClient);

      // Show/hide custom URL field
      document.getElementById('wh-target-service')?.addEventListener('change', (e) => {
        const row = document.getElementById('custom-url-row');
        if (row) row.classList.toggle('hidden', e.target.value !== 'custom');
      });
    }
  },
  scenarios: {
    label: 'Scenarios',
    render: () => renderScenarioPanel(),
    init: () => initScenarioPanel(wsClient)
  },
  health: {
    label: 'Health',
    render: () => renderHealthPanel(),
    init: () => initHealthPanel(wsClient)
  },
  logs: {
    label: 'Logs',
    render: () => renderLogsPanel(),
    init: () => initLogsPanel()
  },
  errors: {
    label: 'Errors',
    render: () => renderErrorsPanel(),
    init: () => initErrorsPanel()
  }
};

let _currentTab = 'test';

function switchTab(tabKey) {
  if (!tabs[tabKey]) return;
  _currentTab = tabKey;

  // Update tab nav active state
  document.querySelectorAll('[data-tab]').forEach(el => {
    const isActive = el.dataset.tab === tabKey;
    el.className = isActive
      ? 'px-4 py-2.5 text-sm font-medium text-blue-700 border-b-2 border-blue-600'
      : 'px-4 py-2.5 text-sm font-medium text-slate-500 hover:text-slate-800 border-b-2 border-transparent';
  });

  // Render tab content
  const content = document.getElementById('tab-content');
  if (content) {
    content.innerHTML = tabs[tabKey].render();
    tabs[tabKey].init();
  }
}

// --- Init ---
document.addEventListener('DOMContentLoaded', () => {
  // Render tab navigation
  const tabNav = document.getElementById('tab-nav');
  if (tabNav) {
    tabNav.innerHTML = Object.entries(tabs).map(([key, tab]) =>
      `<button data-tab="${key}" class="px-4 py-2.5 text-sm font-medium text-slate-500 hover:text-slate-800 border-b-2 border-transparent">${tab.label}</button>`
    ).join('');

    tabNav.addEventListener('click', (e) => {
      const btn = e.target.closest('[data-tab]');
      if (btn) switchTab(btn.dataset.tab);
    });
  }

  // Connect WebSocket
  wsClient.connect();
  initHeader(wsClient);

  // Also listen for callback events on all tabs (for header badge etc.)
  initCallbackPanel(wsClient);
  initHealthPanel(wsClient);

  // Show default tab
  switchTab('test');
});

// Make switchTab available globally for any inline handlers
window.__switchTab = switchTab;
