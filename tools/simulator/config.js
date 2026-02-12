'use strict';

// Q: InvektoServis Simulator Configuration
// Environment variable overrides for all settings

module.exports = {
  port: parseInt(process.env.SIMULATOR_PORT || '4500', 10),
  callbackHost: process.env.SIMULATOR_CALLBACK_HOST || 'localhost',

  services: {
    backend: {
      url: process.env.BACKEND_URL || 'http://services.invekto.com:5000',
      name: 'Invekto.Backend'
    },
    chatAnalysis: {
      url: process.env.CHAT_ANALYSIS_URL || 'http://services.invekto.com:7101',
      name: 'Invekto.ChatAnalysis'
    },
    automation: {
      url: process.env.AUTOMATION_URL || 'http://services.invekto.com:7108',
      name: 'Invekto.Automation'
    },
    agentAI: {
      url: process.env.AGENTAI_URL || 'http://services.invekto.com:7105',
      name: 'Invekto.AgentAI'
    }
  },

  jwt: {
    secretKey: process.env.SIMULATOR_JWT_SECRET || '',
    algorithm: 'HS256',
    issuer: process.env.SIMULATOR_JWT_ISSUER || null,
    audience: process.env.SIMULATOR_JWT_AUDIENCE || null,
    defaultExpiresMinutes: 60
  },

  ops: {
    username: process.env.OPS_USERNAME || 'admin',
    password: process.env.OPS_PASSWORD || 'admin123'
  },

  callback: {
    timeoutSeconds: 30,
    maxTrafficEntries: 500
  },

  health: {
    intervalSeconds: 15,
    timeoutMs: 5000
  }
};
