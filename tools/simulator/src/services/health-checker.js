'use strict';

const config = require('../../config');
const wsManager = require('../ws/ws-manager');

/**
 * Health checker for InvektoServis microservices.
 * Architecture: All health checks go through Backend proxy.
 *   1. Backend /health - direct check (only externally accessible service)
 *   2. Backend /api/ops/health - aggregated check for all internal services
 * Internal services (Automation, ChatAnalysis, AgentAI) are localhost-only
 * on the server, so we cannot ping them directly from the simulator.
 */

let _interval = null;
let _lastResult = null;

async function checkBackendDirect() {
  const start = Date.now();
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), config.health.timeoutMs);

  try {
    const res = await fetch(`${config.services.backend.url}/health`, { signal: controller.signal });
    clearTimeout(timeout);
    return {
      name: 'Invekto.Backend',
      url: config.services.backend.url,
      status: res.ok ? 'ok' : 'error',
      status_code: res.status,
      response_time_ms: Date.now() - start
    };
  } catch (err) {
    clearTimeout(timeout);
    return {
      name: 'Invekto.Backend',
      url: config.services.backend.url,
      status: err.name === 'AbortError' ? 'timeout' : 'down',
      response_time_ms: Date.now() - start,
      error: err.name === 'AbortError'
        ? `Timeout after ${config.health.timeoutMs}ms`
        : err.code === 'ECONNREFUSED' ? 'Connection refused' : err.message
    };
  }
}

async function checkViaBackendProxy() {
  const start = Date.now();
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), config.health.timeoutMs + 5000);
  const authHeader = 'Basic ' + Buffer.from(`${config.ops.username}:${config.ops.password}`).toString('base64');

  try {
    const res = await fetch(`${config.services.backend.url}/api/ops/health`, {
      signal: controller.signal,
      headers: { 'Authorization': authHeader }
    });
    clearTimeout(timeout);

    if (!res.ok) {
      return null;
    }

    const data = await res.json();
    const elapsed = Date.now() - start;

    // Map Backend's response format to simulator's expected format
    // Backend returns: { services: [{ name, status: "ok"|"unavailable", responseTimeMs, error }] }
    // Simulator UI expects: { name, url, status: "ok"|"down"|"timeout"|"error", response_time_ms, error }
    const serviceUrlMap = {};
    for (const [key, svc] of Object.entries(config.services)) {
      serviceUrlMap[svc.name] = svc.url;
    }

    return (data.services || [])
      .filter(s => s.name !== 'Invekto.Backend')
      .map(s => ({
        name: s.name,
        url: serviceUrlMap[s.name] || 'localhost',
        status: s.status === 'ok' ? 'ok' : 'down',
        response_time_ms: s.responseTimeMs || null,
        error: s.error || null
      }));
  } catch (err) {
    clearTimeout(timeout);
    return null;
  }
}

async function checkAllServices() {
  // Step 1: Check Backend directly (it's the only externally accessible service)
  const backendResult = checkBackendDirect();

  // Step 2: If we can reach Backend, get aggregated internal service health
  const proxyResult = checkViaBackendProxy();

  const [backend, internalServices] = await Promise.all([backendResult, proxyResult]);

  const results = [backend];

  if (internalServices) {
    // Backend proxy returned internal service statuses
    results.push(...internalServices);
  } else {
    // Backend proxy failed or Backend is down - report internal services as unknown
    const internalServiceNames = [
      { name: 'Invekto.ChatAnalysis', key: 'chatAnalysis' },
      { name: 'Invekto.Automation', key: 'automation' },
      { name: 'Invekto.AgentAI', key: 'agentAI' }
    ];
    for (const svc of internalServiceNames) {
      results.push({
        name: svc.name,
        url: config.services[svc.key]?.url || 'localhost',
        status: backend.status === 'ok' ? 'error' : 'down',
        response_time_ms: null,
        error: backend.status === 'ok'
          ? 'Could not query via Backend proxy'
          : 'Backend unreachable'
      });
    }
  }

  _lastResult = {
    services: results,
    timestamp: new Date().toISOString()
  };
  return _lastResult;
}

function startPeriodicCheck() {
  // Initial check immediately
  checkAllServices().then(result => {
    wsManager.broadcast('health_update', result);
  });

  _interval = setInterval(async () => {
    const result = await checkAllServices();
    wsManager.broadcast('health_update', result);
  }, config.health.intervalSeconds * 1000);
}

function stopPeriodicCheck() {
  if (_interval) {
    clearInterval(_interval);
    _interval = null;
  }
}

function getLastResult() {
  return _lastResult;
}

module.exports = { checkAllServices, startPeriodicCheck, stopPeriodicCheck, getLastResult };
