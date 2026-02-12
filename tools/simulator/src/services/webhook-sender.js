'use strict';

const { v4: uuidv4 } = require('uuid');
const config = require('../../config');
const trafficStore = require('./traffic-store');
const wsManager = require('../ws/ws-manager');

let _sequenceCounter = 1000;

/**
 * Sends webhook events to InvektoServis via Backend proxy.
 * All traffic goes through Backend:5000 → Automation (localhost-only).
 * Mirrors IncomingWebhookEvent contract from arch/contracts/integration-webhook.json.
 */

async function sendWebhook(payload, jwtToken, targetUrl) {
  const requestId = uuidv4().replace(/-/g, '');
  const url = targetUrl || `${config.services.backend.url}/api/v1/automation/webhook`;

  // Auto-fill defaults
  const webhookBody = {
    event_type: payload.event_type || 'new_message',
    sequence_id: payload.sequence_id || ++_sequenceCounter,
    chat_id: payload.chat_id || 1,
    channel: payload.channel || 'whatsapp',
    data: payload.data || {},
    timestamp: payload.timestamp || new Date().toISOString(),
    callback_url: `http://${config.callbackHost}:${config.port}/api/callback`
  };

  const headers = {
    'Content-Type': 'application/json',
    'X-Request-Id': requestId
  };

  if (jwtToken) {
    headers['Authorization'] = `Bearer ${jwtToken}`;
  }

  const start = Date.now();
  let result;

  try {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 10000);

    const res = await fetch(url, {
      method: 'POST',
      headers,
      body: JSON.stringify(webhookBody),
      signal: controller.signal
    });

    clearTimeout(timeout);

    let responseBody;
    const rawText = await res.text();
    try {
      responseBody = JSON.parse(rawText);
    } catch {
      responseBody = rawText;
    }

    result = {
      request_id: requestId,
      target_url: url,
      status_code: res.status,
      response_body: responseBody,
      duration_ms: Date.now() - start,
      webhook_body: webhookBody,
      timestamp: new Date().toISOString()
    };
  } catch (err) {
    result = {
      request_id: requestId,
      target_url: url,
      status_code: 0,
      error: err.code === 'ECONNREFUSED'
        ? `Connection refused: ${url}`
        : err.name === 'AbortError'
          ? 'Request timeout (10s)'
          : err.message,
      duration_ms: Date.now() - start,
      webhook_body: webhookBody,
      timestamp: new Date().toISOString()
    };
  }

  // Record in traffic store + broadcast via WebSocket
  trafficStore.addSentWebhook(result);
  wsManager.broadcast('webhook_sent', result);

  return result;
}

/**
 * Sends a generic HTTP request to any service endpoint.
 * Used for testing non-webhook endpoints (health, flows, FAQ, etc.).
 */
async function sendRequest({ method, url, headers: extraHeaders, body, jwtToken }) {
  const requestId = uuidv4().replace(/-/g, '');

  const headers = {
    'Content-Type': 'application/json',
    'X-Request-Id': requestId,
    ...extraHeaders
  };

  if (jwtToken) {
    headers['Authorization'] = `Bearer ${jwtToken}`;
  }

  const start = Date.now();

  try {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 10000);

    const options = { method: method || 'GET', headers, signal: controller.signal };
    if (body && method !== 'GET') {
      options.body = typeof body === 'string' ? body : JSON.stringify(body);
    }

    const res = await fetch(url, options);
    clearTimeout(timeout);

    let responseBody;
    const rawText = await res.text();
    const contentType = res.headers.get('content-type') || '';
    if (contentType.includes('json')) {
      try { responseBody = JSON.parse(rawText); } catch { responseBody = rawText; }
    } else {
      responseBody = rawText;
    }

    return {
      request_id: requestId,
      target_url: url,
      method: method || 'GET',
      status_code: res.status,
      response_body: responseBody,
      duration_ms: Date.now() - start,
      timestamp: new Date().toISOString()
    };
  } catch (err) {
    return {
      request_id: requestId,
      target_url: url,
      method: method || 'GET',
      status_code: 0,
      error: err.code === 'ECONNREFUSED'
        ? `Connection refused: ${url}`
        : err.name === 'AbortError'
          ? 'Request timeout (10s)'
          : err.message,
      duration_ms: Date.now() - start,
      timestamp: new Date().toISOString()
    };
  }
}

module.exports = { sendWebhook, sendRequest };
