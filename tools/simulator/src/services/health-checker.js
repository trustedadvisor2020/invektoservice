'use strict';

const config = require('../../config');
const wsManager = require('../ws/ws-manager');

/**
 * Periodic health checker for all InvektoServis microservices.
 * Calls /health on each service, broadcasts results via WebSocket.
 */

let _interval = null;
let _lastResult = null;

async function checkService(service) {
  const start = Date.now();
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), config.health.timeoutMs);

  try {
    const res = await fetch(`${service.url}/health`, { signal: controller.signal });
    clearTimeout(timeout);
    return {
      name: service.name,
      url: service.url,
      status: res.ok ? 'ok' : 'error',
      status_code: res.status,
      response_time_ms: Date.now() - start
    };
  } catch (err) {
    clearTimeout(timeout);
    if (err.name === 'AbortError') {
      return {
        name: service.name,
        url: service.url,
        status: 'timeout',
        response_time_ms: Date.now() - start,
        error: `Timeout after ${config.health.timeoutMs}ms`
      };
    }
    return {
      name: service.name,
      url: service.url,
      status: 'down',
      response_time_ms: Date.now() - start,
      error: err.code === 'ECONNREFUSED' ? 'Connection refused' : err.message
    };
  }
}

async function checkAllServices() {
  const services = Object.values(config.services);
  const results = await Promise.all(services.map(checkService));
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
