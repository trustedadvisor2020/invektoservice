'use strict';

const http = require('http');
const express = require('express');
const path = require('path');
const config = require('./config');
const wsManager = require('./src/ws/ws-manager');
const trafficStore = require('./src/services/traffic-store');
const healthChecker = require('./src/services/health-checker');

// --- Startup validation ---
const jwtKeyLength = Buffer.byteLength(config.jwt.secretKey, 'utf8');
if (!config.jwt.secretKey || jwtKeyLength < 32) {
  console.error(`
╔══════════════════════════════════════════════════════════════╗
║  FATAL: JWT SecretKey is not configured or too short.       ║
║                                                              ║
║  Set SIMULATOR_JWT_SECRET environment variable:              ║
║    $env:SIMULATOR_JWT_SECRET = "your-32-byte-minimum-key"   ║
║                                                              ║
║  The key must match Jwt:SecretKey in services' appsettings.  ║
║  Minimum 32 bytes (256 bits) for HMAC-SHA256.                ║
║  Current: ${jwtKeyLength} bytes                                         ║
╚══════════════════════════════════════════════════════════════╝
`);
  process.exit(1);
}

// --- Express app setup ---
const app = express();
app.use(express.json({ limit: '1mb' }));

// CORS (dev-only tool)
app.use((req, res, next) => {
  res.header('Access-Control-Allow-Origin', '*');
  res.header('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
  res.header('Access-Control-Allow-Headers', 'Content-Type, Authorization, X-Request-Id');
  if (req.method === 'OPTIONS') return res.sendStatus(204);
  next();
});

// Request logging
app.use((req, res, next) => {
  if (req.path.startsWith('/api/')) {
    const start = Date.now();
    res.on('finish', () => {
      const duration = Date.now() - start;
      console.log(`[${new Date().toISOString()}] ${req.method} ${req.path} ${res.statusCode} ${duration}ms`);
    });
  }
  next();
});

// Static files
app.use(express.static(path.join(__dirname, 'public')));

// --- Routes ---
app.use('/api/jwt', require('./src/routes/jwt'));
app.use('/api/webhook', require('./src/routes/webhook'));
app.use('/api/callback', require('./src/routes/callback'));
app.use('/api/health', require('./src/routes/health'));
app.use('/api/logs', require('./src/routes/logs'));
app.use('/api/scenarios', require('./src/routes/scenarios'));
app.use('/api/traffic', require('./src/routes/traffic'));

// SPA fallback - serve index.html for unmatched routes
app.get('*', (req, res) => {
  res.sendFile(path.join(__dirname, 'public', 'index.html'));
});

// --- HTTP + WebSocket server ---
const server = http.createServer(app);
wsManager.attach(server);
trafficStore.setWsManager(wsManager);

// --- Start ---
server.listen(config.port, () => {
  const svcList = Object.values(config.services)
    .map(s => `    ${s.name.padEnd(25)} ${s.url}`)
    .join('\n');

  console.log(`
============================================
  InvektoServis Test & Simulation Tool
  Port: ${config.port}
  UI: http://localhost:${config.port}
  WebSocket: ws://localhost:${config.port}/ws
  Callback URL: http://localhost:${config.port}/api/callback
============================================
  Services:
${svcList}
============================================
  Health check: every ${config.health.intervalSeconds}s
  JWT key: configured (${jwtKeyLength} bytes)
============================================
`);

  // Start periodic health checks
  healthChecker.startPeriodicCheck();
});

// Graceful shutdown
process.on('SIGINT', () => {
  console.log('\nShutting down...');
  healthChecker.stopPeriodicCheck();
  wsManager.close();
  server.close(() => process.exit(0));
});

process.on('SIGTERM', () => {
  healthChecker.stopPeriodicCheck();
  wsManager.close();
  server.close(() => process.exit(0));
});
