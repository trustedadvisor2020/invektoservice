'use strict';

export function formatJson(obj) {
  try {
    return JSON.stringify(obj, null, 2);
  } catch {
    return String(obj);
  }
}

export function formatTimestamp(iso) {
  if (!iso) return '-';
  const d = new Date(iso);
  return d.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit', second: '2-digit' }) +
    '.' + String(d.getMilliseconds()).padStart(3, '0');
}

export function formatDuration(ms) {
  if (ms === null || ms === undefined) return '-';
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(2)}s`;
}

export function actionBadgeClass(action) {
  const map = {
    send_message: 'bg-emerald-100 text-emerald-800',
    suggest_reply: 'bg-blue-100 text-blue-800',
    handoff_to_human: 'bg-amber-100 text-amber-800',
    apply_tag: 'bg-purple-100 text-purple-800',
    no_action: 'bg-gray-100 text-gray-600'
  };
  return map[action] || 'bg-gray-100 text-gray-600';
}

export function statusDotClass(status) {
  const map = {
    ok: 'bg-emerald-500',
    down: 'bg-red-500',
    timeout: 'bg-gray-400',
    error: 'bg-red-500'
  };
  return map[status] || 'bg-gray-400';
}

export function truncate(str, max) {
  if (!str) return '';
  return str.length > max ? str.substring(0, max) + '...' : str;
}

export function escapeHtml(str) {
  if (!str) return '';
  return str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}
