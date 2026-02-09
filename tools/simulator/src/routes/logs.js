'use strict';

const express = require('express');
const { proxyOpsRequest } = require('../services/ops-proxy');

const router = express.Router();

// GET /api/logs/stream - Proxy to Backend /api/ops/logs/stream
router.get('/stream', async (req, res) => {
  const result = await proxyOpsRequest('/api/ops/logs/stream', {
    level: req.query.level,
    service: req.query.service,
    search: req.query.search,
    after: req.query.after,
    limit: req.query.limit
  });

  if (result.error) {
    return res.status(result.status_code || 502).json(result);
  }
  res.json(result.data);
});

// GET /api/logs/grouped - Proxy to Backend /api/ops/logs/grouped
router.get('/grouped', async (req, res) => {
  const result = await proxyOpsRequest('/api/ops/logs/grouped', {
    level: req.query.level,
    service: req.query.service,
    search: req.query.search,
    after: req.query.after,
    limit: req.query.limit,
    category: req.query.category
  });

  if (result.error) {
    return res.status(result.status_code || 502).json(result);
  }
  res.json(result.data);
});

// GET /api/logs/errors - Proxy to Backend /api/ops/stats/errors
router.get('/errors', async (req, res) => {
  const result = await proxyOpsRequest('/api/ops/stats/errors', {
    hours: req.query.hours
  });

  if (result.error) {
    return res.status(result.status_code || 502).json(result);
  }
  res.json(result.data);
});

module.exports = router;
