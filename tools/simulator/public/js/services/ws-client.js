'use strict';

/**
 * WebSocket client with auto-reconnect (exponential backoff 1s - 30s).
 */

class WsClient {
  constructor() {
    this._ws = null;
    this._listeners = new Map();
    this._reconnectDelay = 1000;
    this._maxDelay = 30000;
    this._connected = false;
  }

  connect() {
    const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:';
    const url = `${protocol}//${location.host}/ws`;

    try {
      this._ws = new WebSocket(url);
    } catch (err) {
      console.warn('WebSocket connection failed:', err.message);
      this._emit('_error', { message: `WebSocket connection failed: ${err.message}` });
      this._scheduleReconnect();
      return;
    }

    this._ws.onopen = () => {
      this._connected = true;
      this._reconnectDelay = 1000;
      this._emit('_connection', { connected: true });
    };

    this._ws.onclose = () => {
      this._connected = false;
      this._emit('_connection', { connected: false });
      this._scheduleReconnect();
    };

    this._ws.onerror = (err) => {
      console.warn('WebSocket error:', err.type || 'unknown');
      this._emit('_error', { message: 'WebSocket connection error' });
    };

    this._ws.onmessage = (event) => {
      try {
        const msg = JSON.parse(event.data);
        this._emit(msg.type, msg.data, msg.timestamp);
      } catch (err) {
        console.warn('WebSocket message parse error:', err.message, 'raw:', event.data?.substring?.(0, 100));
      }
    };
  }

  on(type, callback) {
    if (!this._listeners.has(type)) {
      this._listeners.set(type, []);
    }
    this._listeners.get(type).push(callback);
    return this; // chainable
  }

  off(type, callback) {
    const listeners = this._listeners.get(type);
    if (listeners) {
      const idx = listeners.indexOf(callback);
      if (idx !== -1) listeners.splice(idx, 1);
    }
    return this;
  }

  isConnected() {
    return this._connected;
  }

  _emit(type, data, timestamp) {
    const listeners = this._listeners.get(type) || [];
    for (const cb of listeners) {
      try { cb(data, timestamp); } catch (err) { console.error('WS listener error:', err); }
    }
    // Also emit to wildcard listeners
    const wildcards = this._listeners.get('*') || [];
    for (const cb of wildcards) {
      try { cb(type, data, timestamp); } catch (err) { console.error('WS wildcard listener error:', err); }
    }
  }

  _scheduleReconnect() {
    setTimeout(() => {
      this.connect();
    }, this._reconnectDelay);
    this._reconnectDelay = Math.min(this._reconnectDelay * 2, this._maxDelay);
  }
}

export const wsClient = new WsClient();
