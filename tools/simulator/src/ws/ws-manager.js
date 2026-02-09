'use strict';

const { WebSocketServer } = require('ws');

/**
 * WebSocket broadcast manager.
 * Attached to the HTTP server, handles client tracking, heartbeat, broadcast.
 */

class WsManager {
  constructor() {
    this._wss = null;
    this._clients = new Set();
    this._heartbeatInterval = null;
  }

  attach(httpServer) {
    this._wss = new WebSocketServer({ server: httpServer, path: '/ws' });

    this._wss.on('connection', (ws) => {
      this._clients.add(ws);
      ws.isAlive = true;

      ws.on('pong', () => { ws.isAlive = true; });
      ws.on('close', () => { this._clients.delete(ws); });
      ws.on('error', () => { this._clients.delete(ws); });

      // Send welcome event to this client only
      this._sendTo(ws, 'connected', {
        message: 'InvektoServis Simulator connected',
        clients: this._clients.size
      });
    });

    // Heartbeat: ping every 30s, close dead connections
    this._heartbeatInterval = setInterval(() => {
      for (const ws of this._clients) {
        if (!ws.isAlive) {
          ws.terminate();
          this._clients.delete(ws);
          continue;
        }
        ws.isAlive = false;
        ws.ping();
      }
    }, 30000);
  }

  broadcast(type, data) {
    const message = JSON.stringify({ type, data, timestamp: new Date().toISOString() });
    for (const ws of this._clients) {
      if (ws.readyState === 1) { // OPEN
        ws.send(message);
      }
    }
  }

  _sendTo(ws, type, data) {
    if (ws.readyState === 1) {
      ws.send(JSON.stringify({ type, data, timestamp: new Date().toISOString() }));
    }
  }

  getClientCount() {
    return this._clients.size;
  }

  close() {
    if (this._heartbeatInterval) clearInterval(this._heartbeatInterval);
    if (this._wss) this._wss.close();
  }
}

module.exports = new WsManager();
