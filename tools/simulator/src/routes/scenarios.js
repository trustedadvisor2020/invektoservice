'use strict';

const express = require('express');
const { getScenarioList, getScenario } = require('../scenarios/presets');
const { sendWebhook } = require('../services/webhook-sender');
const wsManager = require('../ws/ws-manager');

const router = express.Router();

let _runningScenario = null;
let _abortController = null;

// GET /api/scenarios - List all preset scenarios
router.get('/', (req, res) => {
  res.json({
    scenarios: getScenarioList(),
    running: _runningScenario ? { name: _runningScenario.name, step: _runningScenario.currentStep } : null
  });
});

// POST /api/scenarios/run - Execute a preset scenario
router.post('/run', async (req, res) => {
  const { name, jwt_token, tenant_id } = req.body;

  if (!name) {
    return res.status(400).json({ error: 'name is required (scenario key)' });
  }

  if (!jwt_token) {
    return res.status(400).json({ error: 'jwt_token is required' });
  }

  const scenario = getScenario(name);
  if (!scenario) {
    return res.status(404).json({ error: `Scenario not found: ${name}` });
  }

  if (_runningScenario) {
    return res.status(409).json({
      error: 'A scenario is already running',
      running: _runningScenario.name
    });
  }

  // Start scenario execution in background
  _abortController = new AbortController();
  _runningScenario = { name: scenario.name, key: name, currentStep: 0, totalSteps: scenario.steps.length };

  res.json({
    status: 'started',
    scenario: scenario.name,
    steps: scenario.steps.length
  });

  // Execute steps sequentially
  try {
    for (let i = 0; i < scenario.steps.length; i++) {
      if (_abortController.signal.aborted) break;

      _runningScenario.currentStep = i + 1;
      const step = scenario.steps[i];

      wsManager.broadcast('scenario_step', {
        scenario_name: scenario.name,
        step_index: i + 1,
        step_total: scenario.steps.length,
        status: 'sending',
        message: `Sending webhook: ${step.webhook.data?.message_text || step.webhook.event_type}`,
        expected: step.expected
      });

      // Wait for delay before this step
      if (step.delay_ms > 0) {
        await sleep(step.delay_ms, _abortController.signal);
      }

      if (_abortController.signal.aborted) break;

      // Override tenant_id in JWT if provided
      const webhookPayload = { ...step.webhook };
      if (tenant_id) {
        webhookPayload.tenant_id = tenant_id;
      }

      const result = await sendWebhook(webhookPayload, jwt_token);

      const stepStatus = result.error ? 'error' : (result.status_code === 202 || result.status_code === 200) ? 'sent' : 'failed';

      wsManager.broadcast('scenario_step', {
        scenario_name: scenario.name,
        step_index: i + 1,
        step_total: scenario.steps.length,
        status: stepStatus,
        message: result.error || `HTTP ${result.status_code}`,
        request_id: result.request_id,
        expected: step.expected,
        result: {
          status_code: result.status_code,
          duration_ms: result.duration_ms,
          response_body: result.response_body
        }
      });

      // Wait a bit for callback to arrive before next step
      if (i < scenario.steps.length - 1) {
        await sleep(1000, _abortController.signal);
      }
    }

    wsManager.broadcast('scenario_step', {
      scenario_name: scenario.name,
      step_index: scenario.steps.length,
      step_total: scenario.steps.length,
      status: _abortController.signal.aborted ? 'stopped' : 'completed',
      message: _abortController.signal.aborted ? 'Scenario stopped by user' : 'Scenario completed'
    });
  } catch (err) {
    wsManager.broadcast('scenario_step', {
      scenario_name: scenario.name,
      step_index: _runningScenario?.currentStep || 0,
      step_total: scenario.steps.length,
      status: 'error',
      message: err.message
    });
  } finally {
    _runningScenario = null;
    _abortController = null;
  }
});

// POST /api/scenarios/stop - Stop running scenario
router.post('/stop', (req, res) => {
  if (!_runningScenario) {
    return res.json({ status: 'no_scenario_running' });
  }

  _abortController.abort();
  res.json({ status: 'stopping', scenario: _runningScenario.name });
});

function sleep(ms, signal) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(resolve, ms);
    if (signal) {
      signal.addEventListener('abort', () => {
        clearTimeout(timer);
        resolve(); // Resolve instead of reject to allow graceful stop
      }, { once: true });
    }
  });
}

module.exports = router;
