'use strict';

const express = require('express');
const { getScenarioList, getScenario } = require('../scenarios/presets');
const { sendWebhook, sendRequest } = require('../services/webhook-sender');
const wsManager = require('../ws/ws-manager');
const config = require('../../config');

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

  // Execute steps sequentially; stepResults stores each step's response for chaining
  const stepResults = [];
  try {
    for (let i = 0; i < scenario.steps.length; i++) {
      if (_abortController.signal.aborted) break;

      _runningScenario.currentStep = i + 1;
      const step = JSON.parse(JSON.stringify(scenario.steps[i]));

      // Resolve {{step_N.field}} placeholders from previous step results
      if (step.body) {
        resolveStepRefs(step.body, stepResults);
      }

      // Build status message based on step type
      let sendingMessage;
      if (step.type === 'api_call') {
        sendingMessage = `${step.method || 'POST'} ${step.endpoint}`;
      } else if (step.webhook) {
        sendingMessage = `Webhook: ${step.webhook.data?.message_text || step.webhook.event_type || 'event'}`;
      } else {
        sendingMessage = `Step ${i + 1}`;
      }

      wsManager.broadcast('scenario_step', {
        scenario_name: scenario.name,
        step_index: i + 1,
        step_total: scenario.steps.length,
        status: 'sending',
        message: sendingMessage,
        expected: step.expected
      });

      // Wait for delay before this step
      if (step.delay_ms > 0) {
        await sleep(step.delay_ms, _abortController.signal);
      }

      if (_abortController.signal.aborted) break;

      let result;
      if (step.type === 'api_call') {
        // API call through Backend proxy (all traffic routes via Backend:5000)
        const serviceUrl = step.service_url || config.services[step.service]?.url || config.services.backend.url;
        const url = `${serviceUrl}${step.endpoint}`;
        result = await sendRequest({
          method: step.method || 'POST',
          url,
          body: step.body,
          jwtToken: jwt_token,
          headers: step.headers
        });
      } else if (step.webhook) {
        // Legacy webhook send (requires step.webhook object)
        const webhookPayload = { ...step.webhook };
        if (tenant_id) {
          webhookPayload.tenant_id = tenant_id;
        }
        result = await sendWebhook(webhookPayload, jwt_token);
      } else {
        // Unknown step type - skip with error
        result = { error: `Unknown step type: ${step.type || 'undefined'}`, status_code: 0, duration_ms: 0, request_id: 'N/A' };
      }

      // Store step result for chaining (e.g. suggestion_id from suggest → feedback)
      stepResults.push(result.response_body || {});

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

/**
 * Resolve {{step_N.field}} placeholders in step body from previous step results.
 * Example: "{{step_1.suggestion_id}}" in feedback body gets replaced with
 * the actual suggestion_id from step 1's response.
 * Logs warnings for unresolved placeholders via WebSocket.
 */
function resolveStepRefs(obj, stepResults) {
  const unresolved = [];
  _resolveStepRefsInner(obj, stepResults, unresolved);
  if (unresolved.length > 0) {
    wsManager.broadcast('scenario_step', {
      status: 'warning',
      message: `Unresolved placeholders: ${unresolved.join(', ')}. Previous step may have failed or returned different fields.`
    });
  }
}

function _resolveStepRefsInner(obj, stepResults, unresolved) {
  for (const key of Object.keys(obj)) {
    if (typeof obj[key] === 'string') {
      obj[key] = obj[key].replace(/\{\{step_(\d+)\.([a-zA-Z0-9_.]+)\}\}/g, (match, idx, field) => {
        const stepIdx = parseInt(idx, 10) - 1; // step_1 → index 0
        if (stepIdx >= 0 && stepIdx < stepResults.length) {
          const val = field.split('.').reduce((o, k) => o?.[k], stepResults[stepIdx]);
          if (val !== undefined && val !== null) {
            return String(val);
          }
          unresolved.push(match);
          return match;
        }
        unresolved.push(match);
        return match;
      });
    } else if (obj[key] && typeof obj[key] === 'object') {
      _resolveStepRefsInner(obj[key], stepResults, unresolved);
    }
  }
}

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
