'use strict';

const BASE = '';

async function request(method, path, body) {
  const opts = {
    method,
    headers: { 'Content-Type': 'application/json' }
  };
  if (body && method !== 'GET') {
    opts.body = JSON.stringify(body);
  }
  const res = await fetch(`${BASE}${path}`, opts);
  const data = await res.json();
  if (!res.ok && !data.error) data._httpStatus = res.status;
  return data;
}

export const api = {
  // JWT
  generateToken: (params) => request('POST', '/api/jwt/generate', params),
  decodeToken: (token) => request('POST', '/api/jwt/decode', { token }),

  // Webhook
  sendWebhook: (payload, jwt_token, target_url, target_service) =>
    request('POST', '/api/webhook/send', { payload, jwt_token, target_url, target_service }),
  sendRequest: (params) => request('POST', '/api/webhook/request', params),
  getServices: () => request('GET', '/api/webhook/services'),

  // Health
  checkHealth: () => request('GET', '/api/health/check'),
  getHealthStatus: () => request('GET', '/api/health/status'),

  // Traffic
  getTraffic: (limit) => request('GET', `/api/traffic?limit=${limit || 100}`),
  getPending: () => request('GET', '/api/traffic/pending'),
  clearTraffic: () => request('DELETE', '/api/traffic'),

  // Scenarios
  getScenarios: () => request('GET', '/api/scenarios'),
  runScenario: (name, jwt_token, tenant_id) =>
    request('POST', '/api/scenarios/run', { name, jwt_token, tenant_id }),
  stopScenario: () => request('POST', '/api/scenarios/stop'),

  // Logs (proxied from Backend)
  getLogs: (params) => {
    const qs = new URLSearchParams(params).toString();
    return request('GET', `/api/logs/stream?${qs}`);
  },
  getLogErrors: (hours) => request('GET', `/api/logs/errors?hours=${hours || 24}`),
  getLogsGrouped: (params) => {
    const qs = new URLSearchParams(params).toString();
    return request('GET', `/api/logs/grouped?${qs}`);
  }
};
