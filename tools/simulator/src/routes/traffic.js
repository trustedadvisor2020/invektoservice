'use strict';

const express = require('express');
const trafficStore = require('../services/traffic-store');

const router = express.Router();

// GET /api/traffic - Get traffic log
router.get('/', (req, res) => {
  const limit = parseInt(req.query.limit, 10) || 100;
  const entries = trafficStore.getTrafficLog(limit);
  res.json({ entries, stats: trafficStore.getStats() });
});

// GET /api/traffic/pending - Get pending callbacks
router.get('/pending', (req, res) => {
  res.json({ pending: trafficStore.getPendingCallbacks() });
});

// DELETE /api/traffic - Clear traffic log
router.delete('/', (req, res) => {
  trafficStore.clear();
  res.json({ status: 'cleared' });
});

module.exports = router;
