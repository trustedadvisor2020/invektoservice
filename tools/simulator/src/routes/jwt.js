'use strict';

const express = require('express');
const { generateToken, decodeToken } = require('../services/jwt-service');

const router = express.Router();

// POST /api/jwt/generate
router.post('/generate', (req, res) => {
  const { tenant_id, user_id, role, expires_in_minutes } = req.body;

  if (!tenant_id || !user_id) {
    return res.status(400).json({
      error: 'tenant_id and user_id are required (both must be integers)'
    });
  }

  if (!Number.isInteger(Number(tenant_id)) || !Number.isInteger(Number(user_id))) {
    return res.status(400).json({
      error: 'tenant_id and user_id must be integers'
    });
  }

  try {
    const result = generateToken({ tenant_id, user_id, role, expires_in_minutes });
    res.json(result);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
});

// POST /api/jwt/decode
router.post('/decode', (req, res) => {
  const { token } = req.body;
  if (!token) {
    return res.status(400).json({ error: 'token is required' });
  }
  const result = decodeToken(token);
  res.json(result);
});

module.exports = router;
