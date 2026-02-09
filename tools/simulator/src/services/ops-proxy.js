'use strict';

const config = require('../../config');

/**
 * Proxy client for Backend /api/ops/* endpoints.
 * Adds Basic Auth header automatically.
 */

function getBasicAuthHeader() {
  const credentials = Buffer.from(`${config.ops.username}:${config.ops.password}`).toString('base64');
  return `Basic ${credentials}`;
}

async function proxyOpsRequest(path, queryParams) {
  const backendUrl = config.services.backend.url;
  const url = new URL(path, backendUrl);

  if (queryParams) {
    for (const [key, value] of Object.entries(queryParams)) {
      if (value !== undefined && value !== null && value !== '') {
        url.searchParams.set(key, value);
      }
    }
  }

  try {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 10000);

    const res = await fetch(url.toString(), {
      headers: {
        'Authorization': getBasicAuthHeader(),
        'Accept': 'application/json'
      },
      signal: controller.signal
    });

    clearTimeout(timeout);

    if (!res.ok) {
      const text = await res.text();
      return {
        error: true,
        status_code: res.status,
        message: `Backend returned ${res.status}: ${text.substring(0, 500)}`
      };
    }

    const data = await res.json();
    return { error: false, data };
  } catch (err) {
    if (err.code === 'ECONNREFUSED') {
      return {
        error: true,
        status_code: 0,
        message: `Backend service not reachable at ${backendUrl}`
      };
    }
    if (err.name === 'AbortError') {
      return {
        error: true,
        status_code: 0,
        message: 'Backend request timeout (10s)'
      };
    }
    return {
      error: true,
      status_code: 0,
      message: err.message
    };
  }
}

module.exports = { proxyOpsRequest };
