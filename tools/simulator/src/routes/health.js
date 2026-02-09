'use strict';

const express = require('express');
const healthChecker = require('../services/health-checker');

const router = express.Router();

// GET /api/health/check - On-demand health check of all services
router.get('/check', async (req, res) => {
  try {
    const result = await healthChecker.checkAllServices();
    res.json(result);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// GET /api/health/status - Last cached health state
router.get('/status', (req, res) => {
  const last = healthChecker.getLastResult();
  if (!last) {
    return res.json({ services: [], timestamp: null, message: 'No health check performed yet' });
  }
  res.json(last);
});

module.exports = router;
