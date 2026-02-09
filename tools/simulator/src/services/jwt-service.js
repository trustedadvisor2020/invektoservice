'use strict';

const jwt = require('jsonwebtoken');
const config = require('../../config');

/**
 * HMAC-SHA256 JWT token generator.
 * Mirrors JwtValidator.cs claim structure exactly:
 *   tenant_id (int), user_id (int), role (string), exp (unix)
 */

function generateToken({ tenant_id, user_id, role, expires_in_minutes }) {
  const secret = config.jwt.secretKey;
  if (!secret || Buffer.byteLength(secret, 'utf8') < 32) {
    throw new Error('JWT SecretKey must be at least 32 bytes. Configure SIMULATOR_JWT_SECRET env var.');
  }

  const payload = {
    tenant_id: parseInt(tenant_id, 10),
    user_id: parseInt(user_id, 10),
    role: role || 'agent'
  };

  const options = {
    algorithm: config.jwt.algorithm,
    expiresIn: `${expires_in_minutes || config.jwt.defaultExpiresMinutes}m`
  };

  if (config.jwt.issuer) options.issuer = config.jwt.issuer;
  if (config.jwt.audience) options.audience = config.jwt.audience;

  const token = jwt.sign(payload, secret, options);
  const decoded = jwt.decode(token);

  return {
    token,
    decoded,
    expires_at: new Date(decoded.exp * 1000).toISOString()
  };
}

function decodeToken(token) {
  try {
    return { decoded: jwt.decode(token), error: null };
  } catch (err) {
    return { decoded: null, error: err.message };
  }
}

module.exports = { generateToken, decodeToken };
