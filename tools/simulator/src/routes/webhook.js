'use strict';

const express = require('express');
const { sendWebhook, sendRequest } = require('../services/webhook-sender');
const config = require('../../config');

const router = express.Router();

// POST /api/webhook/send - Send webhook to Automation (or any service)
router.post('/send', async (req, res) => {
  const { payload, jwt_token, target_url } = req.body;

  if (!payload) {
    return res.status(400).json({ error: 'payload is required (webhook event body)' });
  }

  if (!jwt_token) {
    return res.status(400).json({ error: 'jwt_token is required. Generate one first via /api/jwt/generate' });
  }

  try {
    const result = await sendWebhook(payload, jwt_token, target_url);
    res.json(result);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// POST /api/webhook/request - Send generic request to any endpoint
router.post('/request', async (req, res) => {
  const { method, url, headers, body, jwt_token } = req.body;

  if (!url) {
    return res.status(400).json({ error: 'url is required' });
  }

  try {
    const result = await sendRequest({ method, url, headers, body, jwtToken: jwt_token });
    res.json(result);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// GET /api/webhook/services - List available service URLs
router.get('/services', (req, res) => {
  const services = {};
  for (const [key, svc] of Object.entries(config.services)) {
    services[key] = { name: svc.name, url: svc.url };
  }
  res.json({ services });
});

module.exports = router;
