# Microservice Guide

> InvektoServis mikro servis geliştirme rehberi.

## Yeni Servis Oluşturma

### 1. Dizin Yapısı

```
services/{service-name}/
├── src/
│   ├── index.ts          # Entry point
│   ├── routes/           # API routes
│   ├── services/         # Business logic
│   ├── models/           # Data models
│   ├── middleware/       # Express middleware
│   └── utils/            # Utilities
├── db/
│   ├── schema.sql        # DB schema
│   └── migrations/       # Migration files
├── tests/
│   ├── unit/
│   └── integration/
├── .env.example
├── package.json
├── tsconfig.json
└── README.md
```

### 2. package.json Şablonu

```json
{
  "name": "@invekto/{service-name}",
  "version": "1.0.0",
  "scripts": {
    "dev": "ts-node-dev src/index.ts",
    "build": "tsc",
    "start": "node dist/index.js",
    "test": "jest"
  }
}
```

### 3. Entry Point Şablonu

```typescript
// src/index.ts
import express from 'express';
import { config } from './config';
import { routes } from './routes';
import { errorHandler } from './middleware/errorHandler';
import { logger } from './utils/logger';

const app = express();

app.use(express.json());
app.use('/api', routes);
app.use(errorHandler);

app.listen(config.port, () => {
  logger.info(`${config.serviceName} started on port ${config.port}`);
});
```

---

## Servisler Arası İletişim

### HTTP API

```typescript
// Diğer servisi çağırma
const response = await fetch('http://service-b:3001/api/users/123', {
  headers: {
    'Authorization': `Bearer ${process.env.SERVICE_TOKEN}`,
    'X-Request-ID': requestId
  }
});
```

### Event-Driven (Kafka/RabbitMQ)

```typescript
// Event yayınlama
await messageQueue.publish('user.created', {
  userId: user.id,
  email: user.email,
  timestamp: new Date().toISOString()
});

// Event dinleme
messageQueue.subscribe('user.created', async (event) => {
  // Handle event
});
```

---

## API Tasarımı

### Endpoint Naming

```
GET    /api/{resource}           # List
GET    /api/{resource}/:id       # Get one
POST   /api/{resource}           # Create
PUT    /api/{resource}/:id       # Update
DELETE /api/{resource}/:id       # Delete
```

### Response Format

```json
{
  "success": true,
  "data": { ... },
  "meta": {
    "requestId": "uuid",
    "timestamp": "2026-02-01T12:00:00Z"
  }
}
```

### Error Response

```json
{
  "success": false,
  "error": {
    "code": "INV-2001",
    "message": "Resource not found",
    "details": { ... }
  },
  "meta": {
    "requestId": "uuid"
  }
}
```

---

## Health Check

Her servis `/health` endpoint'i sunmalı:

```typescript
app.get('/health', (req, res) => {
  res.json({
    status: 'healthy',
    service: config.serviceName,
    version: config.version,
    uptime: process.uptime(),
    timestamp: new Date().toISOString()
  });
});
```

---

## Deployment

### Docker

```dockerfile
FROM node:18-alpine
WORKDIR /app
COPY package*.json ./
RUN npm ci --only=production
COPY dist ./dist
EXPOSE 3000
CMD ["node", "dist/index.js"]
```

### Environment

- Development: Local `.env`
- Staging: Environment variables
- Production: Secrets manager (Vault, AWS Secrets Manager)

---

## Checklist

Yeni servis oluşturma kontrol listesi:

- [ ] Dizin yapısı oluşturuldu
- [ ] package.json ve bağımlılıklar
- [ ] TypeScript config
- [ ] Entry point ve routes
- [ ] Error handling middleware
- [ ] Logging setup
- [ ] Health check endpoint
- [ ] DB schema (gerekiyorsa)
- [ ] API documentation
- [ ] Tests (unit + integration)
- [ ] Dockerfile
- [ ] README.md
