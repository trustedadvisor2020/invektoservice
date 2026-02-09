'use strict';

const express = require('express');
const trafficStore = require('../services/traffic-store');
const wsManager = require('../ws/ws-manager');

const router = express.Router();

// POST /api/callback - Receive callbacks from Automation service
// This acts as the "Main App" callback endpoint that Automation calls after processing
router.post('/', (req, res) => {
  const callbackData = req.body;

  if (!callbackData || !callbackData.request_id) {
    return res.status(400).json({ error: 'Invalid callback: request_id is required' });
  }

  const { record, roundTripMs } = trafficStore.addReceivedCallback(callbackData);

  // Broadcast to all WebSocket clients
  wsManager.broadcast('callback_received', {
    request_id: callbackData.request_id,
    action: callbackData.action,
    tenant_id: callbackData.tenant_id,
    chat_id: callbackData.chat_id,
    confidence: callbackData.data?.confidence,
    processing_time_ms: callbackData.processing_time_ms,
    round_trip_ms: roundTripMs,
    full_body: callbackData
  });

  // Return 200 OK (Automation expects 2xx for success)
  res.json({
    status: 'ok',
    received_at: new Date().toISOString(),
    request_id: callbackData.request_id
  });
});

module.exports = router;
