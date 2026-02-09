'use strict';

const config = require('../../config');

/**
 * In-memory traffic log for tracking sent webhooks + received callbacks.
 * Correlates callbacks to webhooks via request_id.
 * Max entries: config.callback.maxTrafficEntries (default 500, FIFO).
 */

class TrafficStore {
  constructor() {
    this._entries = [];
    this._pendingMap = new Map(); // request_id -> { entry, timer }
    this._maxEntries = config.callback.maxTrafficEntries;
    this._timeoutSeconds = config.callback.timeoutSeconds;
    this._wsManager = null;
  }

  setWsManager(wsManager) {
    this._wsManager = wsManager;
  }

  addSentWebhook(entry) {
    // entry: { request_id, event_type, chat_id, channel, status_code, duration_ms, response_body, timestamp }
    const record = {
      id: this._entries.length + 1,
      type: 'webhook_sent',
      request_id: entry.request_id,
      timestamp: entry.timestamp || new Date().toISOString(),
      data: entry,
      callback: null,
      round_trip_ms: null,
      status: 'pending_callback'
    };

    this._entries.push(record);
    this._trimEntries();

    // Start callback timeout timer
    const timer = setTimeout(() => {
      this._handleCallbackTimeout(entry.request_id);
    }, this._timeoutSeconds * 1000);

    this._pendingMap.set(entry.request_id, { record, timer, sent_at: Date.now() });

    return record;
  }

  addReceivedCallback(callbackData) {
    // callbackData: { request_id, action, tenant_id, chat_id, ... }
    const requestId = callbackData.request_id;
    const pending = this._pendingMap.get(requestId);
    let roundTripMs = null;

    if (pending) {
      clearTimeout(pending.timer);
      roundTripMs = Date.now() - pending.sent_at;
      pending.record.callback = callbackData;
      pending.record.round_trip_ms = roundTripMs;
      pending.record.status = 'completed';
      this._pendingMap.delete(requestId);
    }

    const record = {
      id: this._entries.length + 1,
      type: 'callback_received',
      request_id: requestId,
      timestamp: new Date().toISOString(),
      data: callbackData,
      round_trip_ms: roundTripMs,
      correlated: !!pending
    };

    this._entries.push(record);
    this._trimEntries();

    return { record, roundTripMs };
  }

  _handleCallbackTimeout(requestId) {
    const pending = this._pendingMap.get(requestId);
    if (!pending) return;

    pending.record.status = 'timed_out';
    this._pendingMap.delete(requestId);

    const timeoutRecord = {
      id: this._entries.length + 1,
      type: 'callback_timeout',
      request_id: requestId,
      timestamp: new Date().toISOString(),
      data: { timeout_seconds: this._timeoutSeconds }
    };

    this._entries.push(timeoutRecord);
    this._trimEntries();

    if (this._wsManager) {
      this._wsManager.broadcast('callback_timeout', {
        request_id: requestId,
        timeout_seconds: this._timeoutSeconds
      });
    }
  }

  getTrafficLog(limit) {
    const n = limit || this._maxEntries;
    return this._entries.slice(-n).reverse();
  }

  getPendingCallbacks() {
    const pending = [];
    for (const [requestId, value] of this._pendingMap) {
      pending.push({
        request_id: requestId,
        waiting_ms: Date.now() - value.sent_at,
        data: value.record.data
      });
    }
    return pending;
  }

  clear() {
    for (const [, value] of this._pendingMap) {
      clearTimeout(value.timer);
    }
    this._pendingMap.clear();
    this._entries = [];
  }

  getStats() {
    return {
      total: this._entries.length,
      pending: this._pendingMap.size,
      webhooks_sent: this._entries.filter(e => e.type === 'webhook_sent').length,
      callbacks_received: this._entries.filter(e => e.type === 'callback_received').length,
      timeouts: this._entries.filter(e => e.type === 'callback_timeout').length
    };
  }

  _trimEntries() {
    if (this._entries.length > this._maxEntries) {
      this._entries = this._entries.slice(-this._maxEntries);
    }
  }
}

module.exports = new TrafficStore();
