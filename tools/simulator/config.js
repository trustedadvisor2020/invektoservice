'use strict';

// Q: InvektoServis Simulator Configuration
// Environment variable overrides for all settings

module.exports = {
  port: parseInt(process.env.SIMULATOR_PORT || '4500', 10),

  services: {
    backend: {
      url: process.env.BACKEND_URL || 'http://localhost:5000',
      name: 'Invekto.Backend'
    },
    chatAnalysis: {
      url: process.env.CHAT_ANALYSIS_URL || 'http://localhost:7101',
      name: 'Invekto.ChatAnalysis'
    },
    automation: {
      url: process.env.AUTOMATION_URL || 'http://localhost:7108',
      name: 'Invekto.Automation'
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
