# Error Codes Registry

> Standard error codes for all Invekto services.

## Format

```
SVC-CAT-NNN

SVC (Service):
├── INV - Invekto Core/Infrastructure
├── CHA - Chat Analysis
├── MSG - Messaging
├── ADM - Admin
└── GW  - Gateway (Nginx)

CAT (Category):
├── VAL - Validation
├── AUTH - Authentication/Authorization
├── NET - Network/Connection
├── DATA - Data/Database
├── BUS - Business Logic
├── SYS - System/Infrastructure
└── EXT - External Service

NNN: Sequential number (001-999)
```

---

## INV - Invekto Core

### INV-VAL - Validation
| Code | Message | HTTP | Description |
|------|---------|------|-------------|
| INV-VAL-001 | Invalid request format | 400 | Request body parse failed |
| INV-VAL-002 | Missing required field | 400 | Required field not provided |
| INV-VAL-003 | Invalid field value | 400 | Field value out of range or invalid format |
| INV-VAL-004 | Invalid idempotency key | 400 | Idempotency key format invalid |

### INV-AUTH - Authentication
| Code | Message | HTTP | Description |
|------|---------|------|-------------|
| INV-AUTH-001 | Unauthorized | 401 | Missing or invalid auth token |
| INV-AUTH-002 | Forbidden | 403 | Insufficient permissions |
| INV-AUTH-003 | Token expired | 401 | Auth token has expired |

### INV-NET - Network
| Code | Message | HTTP | Description |
|------|---------|------|-------------|
| INV-NET-001 | Connection timeout | 504 | Upstream connection timed out |
| INV-NET-002 | Service unavailable | 503 | Downstream service not responding |
| INV-NET-003 | Circuit breaker open | 503 | Circuit breaker is open |

### INV-SYS - System
| Code | Message | HTTP | Description |
|------|---------|------|-------------|
| INV-SYS-001 | Internal server error | 500 | Unexpected system error |
| INV-SYS-002 | Redis unavailable | 503 | Redis connection failed |
| INV-SYS-003 | RabbitMQ unavailable | 503 | RabbitMQ connection failed |
| INV-SYS-004 | Rate limit exceeded | 429 | Too many requests |
| INV-SYS-005 | Service degraded | 200 | Operating in degraded mode |

---

## CHA - Chat Analysis

### CHA-VAL - Validation
| Code | Message | HTTP | Description |
|------|---------|------|-------------|
| CHA-VAL-001 | Invalid chat format | 400 | Chat data format is invalid |
| CHA-VAL-002 | Chat too large | 400 | Chat exceeds max size limit |
| CHA-VAL-003 | Invalid tenant ID | 400 | Tenant ID not recognized |

### CHA-BUS - Business
| Code | Message | HTTP | Description |
|------|---------|------|-------------|
| CHA-BUS-001 | Analysis failed | 500 | Could not complete analysis |
| CHA-BUS-002 | Insufficient data | 400 | Not enough chat data to analyze |
| CHA-BUS-003 | Analysis in progress | 202 | Analysis queued, check later |

### CHA-EXT - External
| Code | Message | HTTP | Description |
|------|---------|------|-------------|
| CHA-EXT-001 | Backend unavailable | 503 | Backend API not responding |
| CHA-EXT-002 | Backend timeout | 504 | Backend API timed out |
| CHA-EXT-003 | Backend error | 502 | Backend returned error |

---

## MSG - Messaging

### MSG-NET - Network
| Code | Message | HTTP | Description |
|------|---------|------|-------------|
| MSG-NET-001 | Queue unavailable | 503 | RabbitMQ queue not accessible |
| MSG-NET-002 | Publish failed | 500 | Could not publish message |
| MSG-NET-003 | Consumer error | 500 | Consumer processing failed |

### MSG-BUS - Business
| Code | Message | HTTP | Description |
|------|---------|------|-------------|
| MSG-BUS-001 | Message rejected | 400 | Message failed validation |
| MSG-BUS-002 | Duplicate message | 409 | Message already processed (idempotency) |
| MSG-BUS-003 | Max retries exceeded | 500 | Message sent to DLQ |

---

## ADM - Admin

### ADM-AUTH - Authentication
| Code | Message | HTTP | Description |
|------|---------|------|-------------|
| ADM-AUTH-001 | Login required | 401 | Must be logged in |
| ADM-AUTH-002 | Invalid email | 403 | Email not in allowlist |
| ADM-AUTH-003 | OAuth failed | 401 | Google OAuth verification failed |

### ADM-BUS - Business
| Code | Message | HTTP | Description |
|------|---------|------|-------------|
| ADM-BUS-001 | DLQ message not found | 404 | Message ID not in DLQ |
| ADM-BUS-002 | Reprocess failed | 500 | Could not reprocess message |
| ADM-BUS-003 | Flag update failed | 500 | Could not update feature flag |

---

## GW - Gateway

### GW-NET - Network
| Code | Message | HTTP | Description |
|------|---------|------|-------------|
| GW-NET-001 | Upstream timeout | 504 | Backend service timed out |
| GW-NET-002 | No healthy upstream | 503 | All upstream servers down |
| GW-NET-003 | Request too large | 413 | Request body exceeds limit |

### GW-SYS - System
| Code | Message | HTTP | Description |
|------|---------|------|-------------|
| GW-SYS-001 | Rate limit exceeded | 429 | Tenant rate limit reached |
| GW-SYS-002 | Connection limit | 429 | Max connections reached |

---

## Usage Example

```csharp
// Throwing an error
throw new InvektoException(ErrorCodes.CHA_VAL_001, "Chat format is invalid");

// Returning error response
return Fail<T>(ErrorCodes.INV_NET_003, "Service temporarily unavailable", 503);

// Logging with error code
_logger.LogError("Error {ErrorCode}: {Message}", ErrorCodes.MSG_BUS_003, "Max retries exceeded");
```

---

**Last Updated:** 2026-01-29
