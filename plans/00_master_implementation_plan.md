# INVEKTO MICROSERVICE SYSTEM
## Master Implementation Plan (Consolidated)

**Version:** 2.0
**Created:** 2026-01-29
**Status:** DRAFT
**Total Duration:** 20 Hafta (5 Ay)

---

# TABLE OF CONTENTS

1. [Executive Summary](#executive-summary)
2. [Phase Overview](#phase-overview)
3. [Phase 0: Foundation & Contracts](#phase-0-foundation--contracts)
4. [Phase 1: Infrastructure Setup](#phase-1-infrastructure-setup)
5. [Phase 2: Core Microservice Framework](#phase-2-core-microservice-framework)
6. [Phase 3: Messaging & Async Jobs](#phase-3-messaging--async-jobs)
7. [Phase 4: Observability Stack](#phase-4-observability-stack)
8. [Phase 5: Security & Auth](#phase-5-security--auth)
9. [Phase 6: Admin UI & Operations](#phase-6-admin-ui--operations)
10. [Phase 7: First Microservice](#phase-7-first-microservice-chat-analysis)
11. [Phase 8: Integration & Testing](#phase-8-integration--testing)
12. [Phase 9: Production Hardening](#phase-9-production-hardening)
13. [Appendix A: Timeline Summary](#appendix-a-timeline-summary)
14. [Appendix B: Team Requirements](#appendix-b-team-requirements)
15. [Appendix C: Risk Matrix](#appendix-c-risk-matrix)
16. [Appendix D: Error Categories & Retry Behaviors](#appendix-d-error-categories--retry-behaviors)
17. [Appendix E: Circuit Breaker Configurations](#appendix-e-circuit-breaker-configurations)
18. [Appendix F: Idempotency & Deduplication](#appendix-f-idempotency--deduplication)
19. [Appendix G: Polly Policy Architecture](#appendix-g-polly-policy-architecture)
20. [Appendix H: Monitoring & Alerting Details](#appendix-h-monitoring--alerting-details)

---

# EXECUTIVE SUMMARY

Bu belge, Invekto Microservice System'in sıfırdan production-ready duruma getirilmesi için gereken tüm adımları, fazları ve bağımlılıkları tanımlar.

## Hedefler
- Production-grade microservice altyapısı
- 10x scale kapasitesi
- Tam observability
- Zero-downtime deployment capability
- Self-healing (circuit breaker, retry, fallback)

## Mevcut Durum
- **InvektoServis:** Sadece planlar mevcut, kod yok
- **Altyapı:** Henüz kurulmadı (Redis, RabbitMQ, Nginx yok)

## Key Decisions

### Retry Stratejisi
Orijinal plan "Retry: 0 (STRICT)" diyordu - bu çok katı. Bu planda **kontrollü micro-retry** yaklaşımı benimsendi:
- **Senkron:** Max 1 retry, toplam 150ms overhead
- **Asenkron:** 5 retry (2s → 10s → 30s → 2m → 10m → DLQ)
- **Circuit Breaker:** %50 hata oranında trip

### Environment Strategy
```
┌─────────────────────────────────────────────────────────────┐
│                    ENVIRONMENT TOPOLOGY                      │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  DEVELOPMENT                    PRODUCTION SERVER           │
│  ═══════════                    ═════════════════           │
│  (Ayrı Makine)                  (Tek Sunucu)                │
│                                                             │
│  ┌─────────────┐               ┌─────────────────────────┐  │
│  │ Dev Machine │               │  ┌─────────────────────┐│  │
│  │             │               │  │     STAGING         ││  │
│  │ - VS/Rider  │               │  │   Port: 5xxx        ││  │
│  │ - Local     │               │  │   Redis DB: 0       ││  │
│  │   Redis     │               │  │   RabbitMQ vhost:   ││  │
│  │ - Local     │               │  │     /staging        ││  │
│  │   RabbitMQ  │               │  └─────────────────────┘│  │
│  │             │               │                         │  │
│  └─────────────┘               │  ┌─────────────────────┐│  │
│                                │  │     PRODUCTION      ││  │
│                                │  │   Port: 6xxx        ││  │
│                                │  │   Redis DB: 1       ││  │
│                                │  │   RabbitMQ vhost:   ││  │
│                                │  │     /production     ││  │
│                                │  └─────────────────────┘│  │
│                                └─────────────────────────┘  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Environment Isolation:**
- **Development:** Tamamen izole, geliştirici makinesi
- **Staging:** Production sunucusunda, farklı portlar ve vhost
- **Production:** Aynı sunucu, production portları ve vhost

### IaC Yaklaşımı (Infrastructure as Code)
**Seçim:** PowerShell Scripts + Git

**Gerekçe:**
- Windows-only ortam için doğal uyum
- Ekstra araç/bağımlılık yok
- Proje ölçeğine uygun (3 servis, 1 sunucu)
- Staging ve Prod aynı sunucuda → düşük karmaşıklık
- İleride Ansible'a geçiş kolay

**Script Organizasyonu:**
```
infra/
├── scripts/
│   ├── setup/
│   │   ├── Install-Redis.ps1
│   │   ├── Install-RabbitMQ.ps1
│   │   ├── Install-Nginx.ps1
│   │   └── Setup-All.ps1
│   ├── deploy/
│   │   ├── Deploy-Service.ps1
│   │   ├── Deploy-Staging.ps1
│   │   └── Deploy-Production.ps1
│   ├── config/
│   │   ├── staging.env
│   │   └── production.env
│   └── verify/
│       ├── Test-Health.ps1
│       └── Test-Connectivity.ps1
└── README.md
```

### Secrets Management
**Seçim:** Environment Variables

**Yaklaşım:**
- Config dosyaları **şifrelenmeyecek**
- Tüm hassas değerler **environment variables** ile
- Her ortam için ayrı `.env` dosyası (Git'e eklenmez)

**Örnek Environment Variables:**
```bash
# Database
INVEKTO_DB_CONNECTION_STRING=Server=...

# Redis
INVEKTO_REDIS_PASSWORD=***

# RabbitMQ
INVEKTO_RABBITMQ_PASSWORD=***

# Service Ports (environment-specific)
INVEKTO_CHATANALYSIS_PORT=5001
```

### Security Model
**Karar:** Microservisler arası güvenlik **DEVRE DIŞI**

**Gerekçe:**
- Tüm servisler aynı güvenli ağda (localhost/internal network)
- External erişim sadece Nginx üzerinden
- Basitlik ve performans öncelikli

**Kapsam:**
- ✅ Rate limiting (tenant bazlı, Nginx + Redis)
- ✅ Audit trail (admin işlemleri)
- ✅ Admin panel Google OAuth
- ❌ Inter-service HMAC tokens (kaldırıldı)
- ❌ Service-to-service auth (gerekli değil)

### Data Architecture
```
┌─────────────────────────────────────────────────────────────┐
│                      DATA STORES                             │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────────┐                                        │
│  │   SQL SERVER    │  Source of Truth                       │
│  │   (Backend)     │  - Jobs table                          │
│  │                 │  - Outbox table                        │
│  │                 │  - Audit logs                          │
│  └─────────────────┘                                        │
│                                                             │
│  ┌─────────────────┐                                        │
│  │     REDIS       │  Hot Path / Ephemeral                  │
│  │                 │  - Idempotency keys (24h TTL)          │
│  │                 │  - Rate limit counters (1m TTL)        │
│  │                 │  - Circuit breaker state               │
│  │                 │  - Cache (varies TTL)                  │
│  │                 │  - Job status (7d TTL)                 │
│  └─────────────────┘                                        │
│                                                             │
│  ┌─────────────────┐                                        │
│  │   RABBITMQ      │  Message Broker                        │
│  │                 │  - Job queues                          │
│  │                 │  - Retry queues (5 levels)             │
│  │                 │  - Dead Letter Queue                   │
│  │                 │  - Event fanout                        │
│  └─────────────────┘                                        │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

**Data Ownership:**
| Data Type | Primary Store | Secondary | TTL |
|-----------|--------------|-----------|-----|
| Jobs | SQL Server | Redis (status) | Permanent / 7d |
| Chat Data | Backend DB | - | N/A |
| Audit Logs | SQL Server | - | Permanent |
| Idempotency | Redis | - | 24 hours |
| Rate Limits | Redis | - | 1 minute |
| Queue Messages | RabbitMQ | - | Until processed |

---

# PHASE OVERVIEW

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         IMPLEMENTATION ROADMAP                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  PHASE 0: Foundation & Contracts          (Hafta 1-2)                       │
│  ════════════════════════════════════════════════════                       │
│  └── Project setup, shared schemas, error codes                             │
│                                                                             │
│              │                                                              │
│              ▼                                                              │
│  PHASE 1: Infrastructure Setup            (Hafta 3-4)                       │
│  ════════════════════════════════════════════════════                       │
│  └── Redis, RabbitMQ, Nginx installation & config                           │
│                                                                             │
│              │                                                              │
│              ▼                                                              │
│  PHASE 2: Core Microservice Framework     (Hafta 5-7)                       │
│  ════════════════════════════════════════════════════                       │
│  └── Base service template, Polly, health checks, RETRY POLICIES            │
│                                                                             │
│              │                                                              │
│              ▼                                                              │
│  PHASE 3: Messaging & Async Jobs          (Hafta 8-10)                      │
│  ════════════════════════════════════════════════════                       │
│  └── RabbitMQ integration, RETRY QUEUES, DLQ, outbox, IDEMPOTENCY           │
│                                                                             │
│              │                                                              │
│              ▼                                                              │
│  PHASE 4: Full Observability Stack        (Hafta 11-12)                     │
│  ════════════════════════════════════════════════════                       │
│  └── Jaeger tracing, advanced dashboards, alerting (basics in Phase 1)      │
│                                                                             │
│              │                                                              │
│              ▼                                                              │
│  PHASE 5: Security & Auth                 (Hafta 13)                        │
│  ════════════════════════════════════════════════════                       │
│  └── HMAC tokens, rate limiting, audit trail                                │
│                                                                             │
│              │                                                              │
│              ▼                                                              │
│  PHASE 6: Admin UI & Operations           (Hafta 14-15)                     │
│  ════════════════════════════════════════════════════                       │
│  └── Ops panel, DLQ viewer, dashboards, alerts                              │
│                                                                             │
│              │                                                              │
│              ▼                                                              │
│  PHASE 7: First Microservice (Chat Analysis) (Hafta 16-17)                  │
│  ════════════════════════════════════════════════════                       │
│  └── Actual business logic implementation                                   │
│                                                                             │
│              │                                                              │
│              ▼                                                              │
│  PHASE 8: Integration & Testing           (Hafta 18-19)                     │
│  ════════════════════════════════════════════════════                       │
│  └── Backend integration, contract tests, CHAOS TESTING                     │
│                                                                             │
│              │                                                              │
│              ▼                                                              │
│  PHASE 9: Production Hardening            (Hafta 20)                        │
│  ════════════════════════════════════════════════════                       │
│  └── Security audit, performance tuning, documentation                      │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

# PHASE 0: FOUNDATION & CONTRACTS
## Hafta 1-2

### Amaç
Tüm sistemin temelini oluşturan shared contracts, error codes, ve proje yapısını kurmak.

### 0.1 Repository Structure

```
InvektoServis/
├── plans/                          # Architecture documents
│   └── 00_master_implementation_plan.md
│
├── src/
│   ├── Invekto.Contracts/          # Shared library (NuGet package)
│   │   ├── DTOs/
│   │   │   ├── Requests/
│   │   │   ├── Responses/
│   │   │   └── Events/
│   │   ├── Enums/
│   │   ├── Constants/
│   │   │   ├── ErrorCodes.cs
│   │   │   ├── HeaderNames.cs
│   │   │   └── QueueNames.cs
│   │   └── Interfaces/
│   │
│   ├── Invekto.Infrastructure/     # Shared infrastructure (NuGet package)
│   │   ├── Resilience/
│   │   ├── Messaging/
│   │   ├── Redis/
│   │   └── Observability/
│   │
│   ├── Invekto.ChatAnalysis/       # First microservice
│   │   ├── src/
│   │   └── tests/
│   │
│   └── Invekto.AdminApi/           # Admin panel backend
│       ├── src/
│       └── tests/
│
├── admin-ui/                       # Admin panel frontend (Node.js)
│   ├── src/
│   └── package.json
│
├── infra/                          # Infrastructure configs
│   ├── nginx/
│   ├── rabbitmq/
│   ├── redis/
│   ├── prometheus/
│   ├── grafana/
│   └── docker-compose.dev.yml      # Local development
│
├── scripts/                        # Automation scripts
│   ├── deploy/
│   ├── setup/
│   └── health-check/
│
├── tests/
│   ├── Integration/
│   ├── Contract/
│   └── Chaos/
│
├── docs/
│   ├── runbooks/
│   ├── api/
│   └── architecture/
│
└── InvektoServis.sln               # Solution file
```

### 0.2 Error Code Standard

```
FORMAT: SVC-CAT-NNN

SVC (Service):
├── INV - Invekto Core
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

EXAMPLES:
├── CHA-VAL-001: Invalid chat format
├── CHA-BUS-001: Analysis failed - insufficient data
├── MSG-NET-001: RabbitMQ connection failed
├── INV-AUTH-001: Invalid HMAC signature
└── INV-SYS-001: Circuit breaker open
```

### 0.3 Shared Header Contract

```csharp
// HeaderNames.cs
public static class HeaderNames
{
    // Identity
    public const string TenantId = "X-Tenant-Id";
    public const string ChatId = "X-Chat-Id";
    public const string UserId = "X-User-Id";

    // Tracing
    public const string RequestId = "X-Request-Id";
    public const string TraceId = "X-Trace-Id";
    public const string SpanId = "X-Span-Id";
    public const string ParentSpanId = "X-Parent-Span-Id";

    // Idempotency
    public const string IdempotencyKey = "X-Idempotency-Key";

    // Auth
    public const string InternalToken = "X-Internal-Token";
    public const string TokenTimestamp = "X-Token-Timestamp";
    public const string TokenSignature = "X-Token-Signature";

    // Metadata
    public const string SchemaVersion = "X-Schema-Version";
    public const string ClientVersion = "X-Client-Version";
}
```

### 0.4 Base Response Contract

```csharp
// StandardResponse.cs
public class StandardResponse<T>
{
    public bool Success { get; set; }
    public T Data { get; set; }
    public ErrorInfo Error { get; set; }
    public ResponseMetadata Meta { get; set; }
}

public class ErrorInfo
{
    public string Code { get; set; }        // SVC-CAT-NNN
    public string Message { get; set; }     // User-friendly
    public string Details { get; set; }     // Ops-only (null in prod)
    public string TraceId { get; set; }
}

public class ResponseMetadata
{
    public string RequestId { get; set; }
    public long ProcessingTimeMs { get; set; }
    public string ServiceVersion { get; set; }
    public bool IsDegraded { get; set; }    // Circuit breaker fallback
    public string DegradedReason { get; set; }
}
```

### 0.5 Deliverables

| ID | Deliverable | Açıklama | Status |
|----|-------------|----------|--------|
| 0.1 | Solution structure | .sln, projects, folders | ✅ DONE |
| 0.2 | Invekto.Contracts | Shared DTOs, enums, constants | ✅ DONE |
| 0.3 | Error code registry | Complete error code list | ✅ DONE |
| 0.4 | JSON Schema files | API contracts (OpenAPI draft) | ⏭️ DEFERRED (Phase 7) |
| 0.5 | Git repository | .gitignore, README, CONTRIBUTING | ✅ DONE |
| 0.6 | CI/CD pipeline draft | GitHub Actions / Azure DevOps YAML | ✅ DONE |

### 0.6 Exit Criteria

- [x] Solution builds without errors
- [x] All contracts compile
- [x] Error codes documented (arch/errors.md + ErrorCodes.cs)
- [ ] OpenAPI draft for first service (deferred to Phase 7)
- [x] Repository initialized with proper structure

### 0.7 Completed Files (2026-01-29)

```
src/
├── InvektoServis.sln
├── Invekto.Contracts/
│   ├── Constants/ErrorCodes.cs, HeaderNames.cs, QueueNames.cs
│   ├── DTOs/Responses/StandardResponse.cs, ErrorInfo.cs, ResponseMetadata.cs
│   ├── Enums/JobStatus.cs, ErrorCategory.cs
│   └── Interfaces/IStandardResponse.cs
└── Invekto.Infrastructure/
    ├── Extensions/ServiceCollectionExtensions.cs
    └── Http/StandardResponseFactory.cs

.github/workflows/ci.yml
.gitignore
.claude/commands/push.md
```

---

# PHASE 1: INFRASTRUCTURE SETUP
## Hafta 3-4

### Amaç
Production-ready infrastructure bileşenlerini kurmak ve yapılandırmak. **Temel observability Day 1'den itibaren aktif.**

> **ÖNEMLİ:** Observability Phase 4'te "full stack" olarak tamamlanacak, ancak temel logging ve metrics Phase 1'den itibaren kurulacak. Bu sayede geliştirme sürecinde sorunları erken tespit edebiliriz.

### 1.1 Component Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         INFRASTRUCTURE COMPONENTS                            │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────┐     ┌─────────────┐     ┌─────────────┐                   │
│  │   NGINX     │     │   REDIS     │     │  RABBITMQ   │                   │
│  │  (Gateway)  │     │   (Cache)   │     │   (Queue)   │                   │
│  └─────────────┘     └─────────────┘     └─────────────┘                   │
│       │                   │                   │                             │
│       │ Port: 80/443      │ Port: 6379        │ Port: 5672/15672           │
│       │                   │                   │                             │
│  ┌────┴────────────────────┴───────────────────┴────────────────────────┐  │
│  │                        WINDOWS SERVER                                 │  │
│  │                     (Production Environment)                          │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Redis Setup

#### Installation (Windows)
```
Location: C:\Services\Redis
Version: Redis 7.x (Windows port or WSL2)
Memory: 2GB max (configurable)
Persistence: RDB snapshots every 5 min
```

#### Configuration
```conf
# redis.conf

# Network
bind 127.0.0.1
port 6379
protected-mode yes
requirepass {REDIS_PASSWORD}

# Memory
maxmemory 2gb
maxmemory-policy allkeys-lru

# Persistence
save 300 1
save 60 10000
dbfilename dump.rdb
dir C:\Services\Redis\data

# Security
rename-command FLUSHALL ""
rename-command FLUSHDB ""
rename-command DEBUG ""
rename-command CONFIG ""

# Performance
tcp-keepalive 300
timeout 0

# Logging
loglevel notice
logfile "C:\Services\Redis\logs\redis.log"
```

#### Key Namespaces
```
invekto:idem:{key}              # Idempotency keys (TTL: 24h)
invekto:lock:{resource}         # Distributed locks (TTL: 5m)
invekto:rate:{tenant}:{window}  # Rate limit counters (TTL: 1m)
invekto:cache:{type}:{id}       # General cache (TTL: varies)
invekto:job:{jobId}             # Job state (TTL: 7d)
invekto:circuit:{service}       # Circuit breaker state
invekto:config:{key}            # Dynamic configuration
```

### 1.3 RabbitMQ Setup

#### Installation (Windows)
```
Location: C:\Services\RabbitMQ
Version: RabbitMQ 3.12+ with Erlang 26+
Management UI: Port 15672
AMQP: Port 5672
```

#### Configuration
```conf
# rabbitmq.conf

# Network
listeners.tcp.default = 5672
management.tcp.port = 15672

# Security
default_user = admin
default_pass = {RABBITMQ_PASSWORD}
loopback_users.guest = false

# Resource limits
vm_memory_high_watermark.relative = 0.7
disk_free_limit.absolute = 2GB

# Queue defaults
queue_master_locator = min-masters

# Logging
log.file = C:\Services\RabbitMQ\logs\rabbit.log
log.file.level = info
```

#### Exchange & Queue Topology (Retry Architecture)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                    RABBITMQ RETRY QUEUE TOPOLOGY                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  EXCHANGES:                                                                 │
│  ├── invekto.direct      (Direct exchange - routing by key)                │
│  ├── invekto.fanout      (Fanout - broadcast)                              │
│  ├── invekto.dlx         (Dead letter exchange)                            │
│  └── invekto.retry       (Retry exchange with TTL queues)                  │
│                                                                             │
│  MAIN QUEUES:                                                               │
│  ├── invekto.jobs.chat-analysis                                            │
│  │   ├── x-dead-letter-exchange: invekto.dlx                               │
│  │   ├── x-dead-letter-routing-key: chat-analysis                          │
│  │   └── x-message-ttl: 3600000 (1h max age)                               │
│  │                                                                          │
│  RETRY QUEUES (with TTL):                                                   │
│  ├── invekto.retry.2s     (TTL: 2000ms)                                    │
│  ├── invekto.retry.10s    (TTL: 10000ms)                                   │
│  ├── invekto.retry.30s    (TTL: 30000ms)                                   │
│  ├── invekto.retry.2m     (TTL: 120000ms)                                  │
│  └── invekto.retry.10m    (TTL: 600000ms)                                  │
│                                                                             │
│  DEAD LETTER QUEUE:                                                         │
│  └── invekto.dlq          (No TTL - manual processing)                     │
│                                                                             │
│  RETRY FLOW:                                                                │
│  Main Queue → Fail → retry.2s → Fail → retry.10s → ... → DLQ              │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Retry Delay Table:**

| Deneme | Delay | Kümülatif | Açıklama |
|--------|-------|-----------|----------|
| 1 | 0 | 0 | İlk deneme, anında |
| 2 | 2s | 2s | Hızlı recovery için |
| 3 | 10s | 12s | Orta bekleme |
| 4 | 30s | 42s | Downstream recovery süresi |
| 5 | 2m | 2m 42s | Ciddi sorun, uzun bekleme |
| 6 | 10m | 12m 42s | Son şans |
| 7 | DLQ | - | Manuel müdahale |

**Toplam süre:** İlk denemeden DLQ'ya ~13 dakika

### 1.4 Nginx Setup (Windows)

#### Installation
```
Location: C:\Services\Nginx
Version: nginx/Windows 1.24+
Config: C:\Services\Nginx\conf\nginx.conf
Logs: C:\Services\Nginx\logs\
```

#### Main Configuration
```nginx
# nginx.conf

worker_processes auto;
error_log logs/error.log warn;
pid logs/nginx.pid;

events {
    worker_connections 1024;
    use select;  # Windows
}

http {
    include mime.types;
    default_type application/octet-stream;

    # Logging
    log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                    '$status $body_bytes_sent "$http_referer" '
                    '"$http_user_agent" "$http_x_forwarded_for" '
                    'rt=$request_time uct="$upstream_connect_time" '
                    'uht="$upstream_header_time" urt="$upstream_response_time" '
                    'rid=$http_x_request_id';

    access_log logs/access.log main;

    # Performance
    sendfile on;
    tcp_nopush on;
    tcp_nodelay on;
    keepalive_timeout 65;
    types_hash_max_size 2048;

    # Buffers
    client_body_buffer_size 16k;
    client_max_body_size 10m;

    # Gzip
    gzip on;
    gzip_types application/json;

    # Rate Limiting (Tenant-based)
    limit_req_zone $http_x_tenant_id zone=tenant_limit:10m rate=100r/s;
    limit_conn_zone $http_x_tenant_id zone=tenant_conn:10m;

    # Upstreams
    upstream chat_analysis {
        least_conn;
        server 127.0.0.1:5001 max_fails=3 fail_timeout=30s;
        server 127.0.0.1:5002 max_fails=3 fail_timeout=30s backup;
        keepalive 32;
    }

    upstream admin_api {
        server 127.0.0.1:5010;
        keepalive 16;
    }

    # Server
    server {
        listen 80;
        server_name invekto.local;

        # Health check endpoint (no auth)
        location /health {
            return 200 'OK';
            add_header Content-Type text/plain;
        }

        # Chat Analysis Service
        location /api/v1/chat-analysis {
            limit_req zone=tenant_limit burst=50 nodelay;
            limit_conn tenant_conn 100;

            proxy_pass http://chat_analysis;
            proxy_http_version 1.1;
            proxy_set_header Connection "";
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;

            # Timeouts (sync) - STRICT per architecture
            proxy_connect_timeout 5s;
            proxy_send_timeout 10s;
            proxy_read_timeout 600ms;

            # No retries at Nginx level (handled by Polly)
            proxy_next_upstream off;

            # Pass headers
            proxy_pass_header X-Request-Id;
            proxy_pass_header X-Trace-Id;
        }

        # Admin API
        location /admin/api {
            proxy_pass http://admin_api;
            proxy_http_version 1.1;
            proxy_set_header Connection "";
            proxy_set_header Host $host;

            proxy_connect_timeout 5s;
            proxy_send_timeout 30s;
            proxy_read_timeout 30s;
        }

        # Admin UI (static)
        location /admin {
            alias C:\Services\AdminUI\dist;
            try_files $uri $uri/ /admin/index.html;
        }
    }
}
```

### 1.5 Windows Services Setup

```batch
# Redis as Windows Service
nssm install InvektoRedis "C:\Services\Redis\redis-server.exe" "C:\Services\Redis\redis.conf"
nssm set InvektoRedis AppDirectory "C:\Services\Redis"
nssm set InvektoRedis DisplayName "Invekto Redis"
nssm set InvektoRedis Start SERVICE_AUTO_START

# RabbitMQ (uses built-in service)
rabbitmq-service install
rabbitmq-service start
rabbitmq-plugins enable rabbitmq_management

# Nginx as Windows Service
nssm install InvektoNginx "C:\Services\Nginx\nginx.exe"
nssm set InvektoNginx AppDirectory "C:\Services\Nginx"
nssm set InvektoNginx DisplayName "Invekto Nginx Gateway"
nssm set InvektoNginx Start SERVICE_AUTO_START
```

### 1.6 Health Check Scripts

```batch
# health-check-all.bat

@echo off
echo Checking Invekto Infrastructure...

echo.
echo [1/3] Redis...
redis-cli -a %REDIS_PASSWORD% ping
if %ERRORLEVEL% NEQ 0 (
    echo FAILED: Redis not responding
    exit /b 1
)
echo OK

echo.
echo [2/3] RabbitMQ...
curl -s -u admin:%RABBITMQ_PASSWORD% http://localhost:15672/api/health/checks/alarms
if %ERRORLEVEL% NEQ 0 (
    echo FAILED: RabbitMQ not responding
    exit /b 1
)
echo OK

echo.
echo [3/3] Nginx...
curl -s http://localhost/health
if %ERRORLEVEL% NEQ 0 (
    echo FAILED: Nginx not responding
    exit /b 1
)
echo OK

echo.
echo All services healthy!
```

### 1.7 Early Observability (Day 1)

> **Neden şimdi?** Geliştirme sürecinde sorunları erken tespit etmek için temel observability Phase 1'de kurulur. Full stack (Jaeger, Grafana dashboards) Phase 4'te tamamlanır.

**Phase 1'de kurulacaklar:**
```
├── Prometheus           # Metrics collection
│   └── Basic scrape configs
├── Loki                 # Log aggregation
│   └── Basic log shipping
└── Structured Logging   # Serilog + seq/console sinks
```

**Minimal Grafana Dashboard:**
- Service health status
- Error rate (5xx responses)
- Request latency (p50, p95, p99)
- Queue depths

### 1.8 Deliverables

| ID | Deliverable | Açıklama |
|----|-------------|----------|
| 1.1 | Redis installed | Windows service, configured |
| 1.2 | RabbitMQ installed | Windows service, management UI |
| 1.3 | Nginx installed | Windows service, reverse proxy |
| 1.4 | Queue topology | All exchanges and queues created (including retry queues) |
| 1.5 | Health check scripts | Automated health verification |
| 1.6 | Firewall rules | Ports configured, internal only |
| 1.7 | Backup scripts | Redis RDB, RabbitMQ definitions |
| 1.8 | Prometheus + Loki | Basic metrics/logs collection |
| 1.9 | Basic dashboard | Service health + error rates |

### 1.9 Exit Criteria

- [ ] All 3 services running as Windows Services
- [ ] Health checks passing
- [ ] RabbitMQ management UI accessible
- [ ] Redis responds to PING
- [ ] Nginx proxies test requests
- [ ] Queue topology verified (including 5 retry queues + DLQ)
- [ ] Backup scripts tested
- [ ] Prometheus scraping metrics
- [ ] Logs visible in Loki/Grafana

---

# PHASE 2: CORE MICROSERVICE FRAMEWORK
## Hafta 5-7

### Amaç
Tüm microservice'lerin kullanacağı ortak framework'ü oluşturmak. **Polly-based retry policies ve circuit breakers dahil.**

### 2.1 Project Structure

```
Invekto.Infrastructure/
├── Invekto.Infrastructure.csproj
│
├── Configuration/
│   ├── ResilienceOptions.cs
│   ├── RedisOptions.cs
│   ├── RabbitMQOptions.cs
│   └── ObservabilityOptions.cs
│
├── Resilience/
│   ├── Policies/
│   │   ├── PolicyNames.cs
│   │   ├── SyncMicroservicePolicy.cs
│   │   ├── AsyncJobPolicy.cs
│   │   ├── RedisOperationPolicy.cs
│   │   └── ExternalApiPolicy.cs
│   ├── CircuitBreaker/
│   │   ├── CircuitBreakerStateTracker.cs
│   │   └── CircuitBreakerMetrics.cs
│   ├── Fallback/
│   │   └── FallbackHandlers.cs
│   └── Extensions/
│       └── HttpClientBuilderExtensions.cs
│
├── Http/
│   ├── Middleware/
│   │   ├── RequestContextMiddleware.cs
│   │   ├── ExceptionHandlerMiddleware.cs
│   │   └── RequestTimingMiddleware.cs
│   ├── Filters/
│   │   └── IdempotencyFilter.cs
│   └── HttpContextExtensions.cs
│
├── Health/
│   ├── Checks/
│   │   ├── RedisHealthCheck.cs
│   │   ├── RabbitMQHealthCheck.cs
│   │   └── DependencyHealthCheck.cs
│   └── HealthCheckExtensions.cs
│
├── Redis/
│   ├── IRedisService.cs
│   ├── RedisService.cs
│   ├── RedisKeyBuilder.cs
│   └── RedisConnectionFactory.cs
│
├── Idempotency/
│   ├── IIdempotencyService.cs
│   ├── IdempotencyService.cs
│   └── IdempotencyResult.cs
│
├── Context/
│   ├── IRequestContext.cs
│   ├── RequestContext.cs
│   └── RequestContextAccessor.cs
│
└── Extensions/
    ├── ServiceCollectionExtensions.cs
    └── ApplicationBuilderExtensions.cs
```

### 2.2 Senkron Retry Stratejisi (CRITICAL)

```
┌────────────────────────────────────────────────────────────────┐
│                 SENKRON RETRY KURALLARI                        │
├────────────────────────────────────────────────────────────────┤
│                                                                │
│  1. TOPLAM SÜRE SINIRI: Max 150ms (retry dahil)                │
│  2. MAX DENEME: 2 (1 orijinal + 1 retry)                       │
│  3. SADECE TRANSIENT: Network/connection hataları              │
│  4. IMMEDIATE FAIL: Business logic, validation hataları        │
│  5. JITTER: Her retry'a random 0-25ms ekle                     │
│  6. TIMEOUT AZALT: Her retry'da timeout %50 azalır             │
│                                                                │
└────────────────────────────────────────────────────────────────┘
```

**Senkron Retry Flow:**

```
İstek Geldi (t=0)
    │
    ▼
┌─────────────┐
│ İlk Deneme  │ Timeout: 100ms
└─────┬───────┘
      │
      ▼
  Başarılı? ──YES──► Yanıt Dön
      │
      NO (Transient?)
      │
      ├── NO (Permanent) ──► Immediate Fail + Error Response
      │
      YES
      │
      ▼
┌─────────────┐
│ Retry #1    │ Timeout: 50ms, Delay: 25ms + jitter(0-25ms)
└─────┬───────┘
      │
      ▼
  Başarılı? ──YES──► Yanıt Dön
      │
      NO
      │
      ▼
  Partial Response veya Graceful Degradation
```

**Timeout Budget:**

```
Toplam Bütçe: 600ms (plan'dan)

Dağılım:
├── İlk istek:     100ms timeout
├── Wait:           25ms base + 0-25ms jitter
├── Retry #1:       50ms timeout
├── Reserve:       400ms (downstream + processing)
└── ────────────────────
    Toplam Max:    ~200ms retry overhead

Kalan süre downstream için: 400ms
```

### 2.3 Polly Policy Registry

```csharp
// PolicyNames.cs
public static class PolicyNames
{
    public const string SyncMicroservice = "SyncMicroservice";
    public const string AsyncJob = "AsyncJob";
    public const string RedisOperation = "RedisOperation";
    public const string ExternalApi = "ExternalApi";
    public const string DatabaseQuery = "DatabaseQuery";
}

// SyncMicroservicePolicy.cs
public static class SyncMicroservicePolicy
{
    public static IAsyncPolicy<HttpResponseMessage> Create(ResilienceOptions options)
    {
        var retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult(r => IsTransientStatusCode(r.StatusCode))
            .WaitAndRetryAsync(
                retryCount: options.Sync.MaxRetries,  // Default: 1
                sleepDurationProvider: (attempt, context) =>
                {
                    var baseDelay = TimeSpan.FromMilliseconds(options.Sync.BaseDelayMs);
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, options.Sync.MaxJitterMs));
                    return baseDelay + jitter;
                },
                onRetryAsync: async (outcome, timespan, attempt, context) =>
                {
                    var logger = context.GetLogger();
                    logger?.LogWarning(
                        "Retry {Attempt} after {Delay}ms. Error: {Error}",
                        attempt, timespan.TotalMilliseconds,
                        outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });

        var circuitBreakerPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult(r => IsTransientStatusCode(r.StatusCode))
            .AdvancedCircuitBreakerAsync(
                failureThreshold: options.Sync.CircuitBreaker.FailureThreshold,  // 0.5 = 50%
                samplingDuration: TimeSpan.FromSeconds(options.Sync.CircuitBreaker.SamplingDurationSeconds),
                minimumThroughput: options.Sync.CircuitBreaker.MinimumThroughput,
                durationOfBreak: TimeSpan.FromSeconds(options.Sync.CircuitBreaker.BreakDurationSeconds),
                onBreak: (outcome, state, breakDuration, context) =>
                {
                    var logger = context.GetLogger();
                    logger?.LogError("Circuit OPEN for {Duration}s. Reason: {Reason}",
                        breakDuration.TotalSeconds, outcome.Exception?.Message);
                },
                onReset: context =>
                {
                    var logger = context.GetLogger();
                    logger?.LogInformation("Circuit CLOSED - normal operation resumed");
                },
                onHalfOpen: () => { });

        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromMilliseconds(options.Sync.TimeoutMs),
            TimeoutStrategy.Optimistic);

        var bulkheadPolicy = Policy.BulkheadAsync<HttpResponseMessage>(
            maxParallelization: options.Sync.Bulkhead.MaxConcurrency,
            maxQueuingActions: options.Sync.Bulkhead.MaxQueueSize);

        // Combine: Timeout -> CircuitBreaker -> Retry -> Bulkhead
        return Policy.WrapAsync(timeoutPolicy, circuitBreakerPolicy, retryPolicy, bulkheadPolicy);
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.BadGateway ||           // 502
               statusCode == HttpStatusCode.ServiceUnavailable ||   // 503
               statusCode == HttpStatusCode.GatewayTimeout ||       // 504
               statusCode == HttpStatusCode.RequestTimeout;         // 408
    }
}
```

### 2.4 Request Context

```csharp
// IRequestContext.cs
public interface IRequestContext
{
    string TenantId { get; }
    string ChatId { get; }
    string RequestId { get; }
    string TraceId { get; }
    string SpanId { get; }
    string IdempotencyKey { get; }
    DateTimeOffset ReceivedAt { get; }

    void SetFromHeaders(IHeaderDictionary headers);
    IDictionary<string, string> ToHeaders();
    IDictionary<string, object> ToLogContext();
}

// RequestContext.cs
public class RequestContext : IRequestContext
{
    public string TenantId { get; private set; }
    public string ChatId { get; private set; }
    public string RequestId { get; private set; }
    public string TraceId { get; private set; }
    public string SpanId { get; private set; }
    public string IdempotencyKey { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }

    public void SetFromHeaders(IHeaderDictionary headers)
    {
        TenantId = headers[HeaderNames.TenantId].FirstOrDefault();
        ChatId = headers[HeaderNames.ChatId].FirstOrDefault();
        RequestId = headers[HeaderNames.RequestId].FirstOrDefault() ?? Ulid.NewUlid().ToString();
        TraceId = headers[HeaderNames.TraceId].FirstOrDefault() ?? Activity.Current?.TraceId.ToString();
        SpanId = Ulid.NewUlid().ToString();
        IdempotencyKey = headers[HeaderNames.IdempotencyKey].FirstOrDefault();
        ReceivedAt = DateTimeOffset.UtcNow;
    }

    public IDictionary<string, string> ToHeaders()
    {
        return new Dictionary<string, string>
        {
            [HeaderNames.TenantId] = TenantId,
            [HeaderNames.ChatId] = ChatId,
            [HeaderNames.RequestId] = RequestId,
            [HeaderNames.TraceId] = TraceId,
            [HeaderNames.SpanId] = SpanId
        };
    }

    public IDictionary<string, object> ToLogContext()
    {
        return new Dictionary<string, object>
        {
            ["TenantId"] = TenantId,
            ["ChatId"] = ChatId,
            ["RequestId"] = RequestId,
            ["TraceId"] = TraceId,
            ["SpanId"] = SpanId
        };
    }
}
```

### 2.5 Health Check System

```csharp
// HealthCheckExtensions.cs
public static class HealthCheckExtensions
{
    public static IServiceCollection AddInvektoHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck<RedisHealthCheck>("redis", tags: new[] { "infrastructure" })
            .AddCheck<RabbitMQHealthCheck>("rabbitmq", tags: new[] { "infrastructure" })
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "self" });

        return services;
    }
}

// Endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("self"),
    ResponseWriter = WriteHealthResponse
});

app.MapHealthChecks("/ready", new HealthCheckOptions
{
    Predicate = _ => true,
    ResponseWriter = WriteHealthResponse
});
```

### 2.6 Base Controller

```csharp
// InvektoControllerBase.cs
[ApiController]
public abstract class InvektoControllerBase : ControllerBase
{
    protected readonly IRequestContext RequestContext;
    protected readonly ILogger Logger;

    protected InvektoControllerBase(
        IRequestContextAccessor contextAccessor,
        ILogger logger)
    {
        RequestContext = contextAccessor.Context;
        Logger = logger;
    }

    protected ActionResult<StandardResponse<T>> Success<T>(T data)
    {
        return Ok(new StandardResponse<T>
        {
            Success = true,
            Data = data,
            Meta = CreateMeta()
        });
    }

    protected ActionResult<StandardResponse<T>> Accepted<T>(string jobId) where T : class
    {
        return StatusCode(202, new StandardResponse<T>
        {
            Success = true,
            Data = null,
            Meta = CreateMeta(jobId: jobId)
        });
    }

    protected ActionResult<StandardResponse<T>> Fail<T>(string errorCode, string message, int statusCode = 400)
    {
        return StatusCode(statusCode, new StandardResponse<T>
        {
            Success = false,
            Error = new ErrorInfo
            {
                Code = errorCode,
                Message = message,
                TraceId = RequestContext.TraceId
            },
            Meta = CreateMeta()
        });
    }

    protected ActionResult<StandardResponse<T>> Degraded<T>(T fallbackData, string reason)
    {
        return Ok(new StandardResponse<T>
        {
            Success = true,
            Data = fallbackData,
            Meta = CreateMeta(isDegraded: true, degradedReason: reason)
        });
    }

    private ResponseMetadata CreateMeta(
        string jobId = null,
        bool isDegraded = false,
        string degradedReason = null)
    {
        return new ResponseMetadata
        {
            RequestId = RequestContext.RequestId,
            ProcessingTimeMs = (DateTimeOffset.UtcNow - RequestContext.ReceivedAt).TotalMilliseconds,
            ServiceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
            IsDegraded = isDegraded,
            DegradedReason = degradedReason,
            JobId = jobId
        };
    }
}
```

### 2.7 Middleware Pipeline

```csharp
// Program.cs (typical microservice)
var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddInvektoInfrastructure(builder.Configuration);
builder.Services.AddInvektoHealthChecks(builder.Configuration);
builder.Services.AddInvektoObservability(builder.Configuration);

var app = builder.Build();

// Middleware order matters!
app.UseMiddleware<RequestTimingMiddleware>();      // 1. Start timing
app.UseMiddleware<RequestContextMiddleware>();     // 2. Extract context from headers
app.UseMiddleware<ExceptionHandlerMiddleware>();   // 3. Global error handling
// NOT: Inter-service auth middleware yok - internal network güvenli kabul edilir

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/ready");

app.Run();
```

### 2.8 Resilience Configuration

```json
// appsettings.json - Resilience section
{
  "Resilience": {
    "Policies": {
      "SyncMicroservice": {
        "Timeout": {
          "OverallMs": 600,
          "PerRetryMs": [100, 50]
        },
        "Retry": {
          "MaxAttempts": 1,
          "BaseDelayMs": 25,
          "MaxJitterMs": 25,
          "TransientHttpCodes": [408, 502, 503, 504]
        },
        "CircuitBreaker": {
          "FailureThresholdPercent": 50,
          "SamplingDurationSeconds": 30,
          "BreakDurationSeconds": 30,
          "MinimumThroughput": 10
        },
        "Bulkhead": {
          "MaxConcurrency": 50,
          "MaxQueueSize": 100
        }
      },
      "RedisOperation": {
        "Timeout": { "OverallMs": 100 },
        "Retry": { "MaxAttempts": 1, "BaseDelayMs": 10 },
        "CircuitBreaker": {
          "FailureThresholdPercent": 30,
          "SamplingDurationSeconds": 15,
          "BreakDurationSeconds": 15,
          "MinimumThroughput": 20
        }
      },
      "ExternalApi": {
        "Timeout": { "OverallMs": 30000, "PerRetryMs": [10000, 10000, 10000] },
        "Retry": {
          "MaxAttempts": 3,
          "BaseDelayMs": 1000,
          "ExponentialBackoff": true,
          "TransientHttpCodes": [429, 500, 502, 503, 504]
        },
        "CircuitBreaker": {
          "FailureThresholdPercent": 50,
          "SamplingDurationSeconds": 60,
          "BreakDurationSeconds": 60,
          "MinimumThroughput": 5
        },
        "Bulkhead": {
          "MaxConcurrency": 20,
          "MaxQueueSize": 50
        }
      }
    }
  }
}
```

### 2.9 Deliverables

| ID | Deliverable | Açıklama |
|----|-------------|----------|
| 2.1 | Invekto.Infrastructure | Shared library NuGet package |
| 2.2 | Polly policies | SyncMicroservice, RedisOperation, ExternalApi, AsyncJob |
| 2.3 | Circuit breaker | Per dependency, with state tracking |
| 2.4 | Request context | Full implementation with propagation |
| 2.5 | Health checks | Redis, RabbitMQ, self checks |
| 2.6 | Base controller | Standard response helpers |
| 2.7 | Middleware stack | Auth, timing, exception handling |
| 2.8 | Configuration system | Options pattern, hot reload |
| 2.9 | Unit tests | >80% coverage for infrastructure |

### 2.10 Exit Criteria

- [ ] Infrastructure library builds and packages
- [ ] All Polly policies unit tested
- [ ] Circuit breaker trips correctly at 50% failure rate
- [ ] Health checks work with real Redis/RabbitMQ
- [ ] Request context propagates through middleware
- [ ] Exception handler returns standardized errors
- [ ] Sample microservice starts and serves /health

---

# PHASE 3: MESSAGING & ASYNC JOBS
## Hafta 8-10

### Amaç
RabbitMQ entegrasyonu, retry mekanizması, DLQ yönetimi, outbox pattern ve **idempotency** implementasyonu.

### 3.1 Messaging Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         MESSAGING FLOW                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  Backend                                                                    │
│     │                                                                       │
│     │ 1. Write to DB + Outbox (single transaction)                         │
│     ▼                                                                       │
│  ┌─────────────────┐                                                        │
│  │  Outbox Table   │                                                        │
│  │  (SQL Server)   │                                                        │
│  └────────┬────────┘                                                        │
│           │                                                                 │
│           │ 2. Dispatcher polls outbox                                      │
│           ▼                                                                 │
│  ┌─────────────────┐      3. Publish           ┌─────────────────┐         │
│  │    Outbox       │ ─────────────────────────► │    RabbitMQ     │         │
│  │   Dispatcher    │                            │   (Exchange)    │         │
│  └─────────────────┘                            └────────┬────────┘         │
│                                                          │                  │
│                                   4. Route to queue      │                  │
│                                                          ▼                  │
│                                                 ┌─────────────────┐         │
│                                                 │   Job Queue     │         │
│                                                 └────────┬────────┘         │
│                                                          │                  │
│                                   5. Consumer picks up   │                  │
│                                                          ▼                  │
│                                                 ┌─────────────────┐         │
│                                                 │    Consumer     │         │
│                                                 │  (Idempotent)   │         │
│                                                 └────────┬────────┘         │
│                                                          │                  │
│                                          ┌───────────────┼───────────────┐  │
│                                          │               │               │  │
│                                       Success          Fail           Fail  │
│                                          │          (transient)    (perm)   │
│                                          ▼               │               │  │
│                                        ACK              │               │  │
│                                                          ▼               ▼  │
│                                                   Retry Queue          DLQ  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Message Header Contract

```
MESSAGE HEADERS (Her mesajda zorunlu):

x-message-id:        ULID (unique, idempotency için)
x-correlation-id:    İlişkili işlem ID'si
x-tenant-id:         Tenant kimliği
x-chat-id:           Chat kimliği
x-trace-id:          Distributed trace ID
x-retry-count:       Mevcut retry sayısı (0'dan başlar)
x-max-retries:       Max izin verilen retry (default: 5)
x-first-failure-at:  İlk hata zamanı (ISO 8601)
x-last-error:        Son hata mesajı (truncated, max 500 char)
x-error-category:    TRANSIENT | PERMANENT | UNKNOWN
x-original-queue:    Orijinal queue adı
x-death:             RabbitMQ native retry tracking
```

### 3.3 Outbox Pattern

```csharp
// OutboxMessage.cs (SQL Server table)
public class OutboxMessage
{
    public long Id { get; set; }
    public string MessageId { get; set; }          // ULID
    public string MessageType { get; set; }        // Full type name
    public string Exchange { get; set; }
    public string RoutingKey { get; set; }
    public string Payload { get; set; }            // JSON
    public string Headers { get; set; }            // JSON (context headers)
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string LastError { get; set; }
    public OutboxStatus Status { get; set; }       // Pending, Processing, Completed, Failed
}

// Usage in service
public async Task CreateJobAsync(CreateJobRequest request)
{
    using var transaction = await _dbContext.Database.BeginTransactionAsync();

    try
    {
        // 1. Write business data
        var job = new Job { ... };
        _dbContext.Jobs.Add(job);
        await _dbContext.SaveChangesAsync();

        // 2. Write outbox event (same transaction)
        var message = new JobCreatedEvent { JobId = job.Id, ... };
        await _outbox.EnqueueAsync(message, "invekto.direct", "job.created");

        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```

### 3.4 Consumer Base Class with Retry Logic

```csharp
// ConsumerBase.cs
public abstract class ConsumerBase<TMessage> : BackgroundService
    where TMessage : class
{
    private readonly IRabbitMQConnection _connection;
    private readonly IIdempotencyService _idempotency;
    private readonly ILogger _logger;
    private readonly ConsumerOptions _options;

    protected abstract string QueueName { get; }
    protected abstract Task ProcessAsync(TMessage message, IRequestContext context, CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await _connection.CreateChannelAsync();

        channel.BasicQos(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (model, ea) =>
        {
            var context = ExtractContext(ea.BasicProperties.Headers);
            var messageId = ea.BasicProperties.MessageId;

            using var _ = _logger.BeginScope(context.ToLogContext());

            try
            {
                // Idempotency check
                var idempResult = await _idempotency.CheckAsync(messageId);
                if (idempResult.IsProcessed)
                {
                    _logger.LogDebug("Message {MessageId} already processed, skipping", messageId);
                    channel.BasicAck(ea.DeliveryTag, multiple: false);
                    return;
                }

                // Deserialize
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                var message = JsonSerializer.Deserialize<TMessage>(body);

                // Process
                await ProcessAsync(message, context, stoppingToken);

                // Mark as processed
                await _idempotency.MarkProcessedAsync(messageId);

                // ACK
                channel.BasicAck(ea.DeliveryTag, multiple: false);

                _logger.LogDebug("Message {MessageId} processed successfully", messageId);
            }
            catch (PermanentException ex)
            {
                _logger.LogError(ex, "Permanent error processing {MessageId}, sending to DLQ", messageId);
                await SendToDlqAsync(channel, ea, ex.Message);
                channel.BasicAck(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                var retryCount = GetRetryCount(ea.BasicProperties.Headers);

                if (retryCount >= _options.MaxRetries)
                {
                    _logger.LogError(ex, "Max retries exceeded for {MessageId}, sending to DLQ", messageId);
                    await SendToDlqAsync(channel, ea, ex.Message);
                    channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
                else
                {
                    _logger.LogWarning(ex, "Transient error processing {MessageId}, retry {Retry}", messageId, retryCount + 1);
                    await SendToRetryQueueAsync(channel, ea, retryCount + 1);
                    channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
            }
        };

        channel.BasicConsume(queue: QueueName, autoAck: false, consumer: consumer);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task SendToRetryQueueAsync(IModel channel, BasicDeliverEventArgs ea, int retryCount)
    {
        var retryQueue = retryCount switch
        {
            1 => "invekto.retry.2s",
            2 => "invekto.retry.10s",
            3 => "invekto.retry.30s",
            4 => "invekto.retry.2m",
            5 => "invekto.retry.10m",
            _ => null
        };

        if (retryQueue == null)
        {
            await SendToDlqAsync(channel, ea, "Max retries exceeded");
            return;
        }

        var properties = channel.CreateBasicProperties();
        properties.Headers = new Dictionary<string, object>(ea.BasicProperties.Headers ?? new Dictionary<string, object>())
        {
            ["x-retry-count"] = retryCount,
            ["x-original-queue"] = QueueName,
            ["x-original-routing-key"] = ea.RoutingKey,
            ["x-last-error"] = "Transient error",
            ["x-error-category"] = "TRANSIENT"
        };
        properties.MessageId = ea.BasicProperties.MessageId;
        properties.Persistent = true;

        channel.BasicPublish(
            exchange: "",
            routingKey: retryQueue,
            basicProperties: properties,
            body: ea.Body);
    }

    private async Task SendToDlqAsync(IModel channel, BasicDeliverEventArgs ea, string reason)
    {
        var properties = channel.CreateBasicProperties();
        properties.Headers = new Dictionary<string, object>(ea.BasicProperties.Headers ?? new Dictionary<string, object>())
        {
            ["x-dlq-reason"] = reason,
            ["x-dlq-entered-at"] = DateTime.UtcNow.ToString("O"),
            ["x-original-queue"] = QueueName,
            ["x-original-routing-key"] = ea.RoutingKey
        };
        properties.MessageId = ea.BasicProperties.MessageId;
        properties.Persistent = true;

        channel.BasicPublish(
            exchange: "",
            routingKey: "invekto.dlq",
            basicProperties: properties,
            body: ea.Body);
    }
}
```

### 3.5 Idempotency Service

```csharp
// IdempotencyService.cs
public class IdempotencyService : IIdempotencyService
{
    private readonly IRedisService _redis;
    private readonly IdempotencyOptions _options;

    public async Task<IdempotencyResult> CheckAsync(string key)
    {
        var redisKey = $"invekto:idem:{key}";
        var value = await _redis.GetAsync(redisKey);

        if (value == null)
        {
            return new IdempotencyResult { IsProcessed = false };
        }

        var state = JsonSerializer.Deserialize<IdempotencyState>(value);
        return new IdempotencyResult
        {
            IsProcessed = state.Status == "completed",
            IsProcessing = state.Status == "processing",
            CachedResponse = state.Response
        };
    }

    public async Task MarkProcessingAsync(string key)
    {
        var redisKey = $"invekto:idem:{key}";
        var state = new IdempotencyState
        {
            Status = "processing",
            CreatedAt = DateTime.UtcNow
        };

        await _redis.SetAsync(redisKey, JsonSerializer.Serialize(state), _options.ProcessingTtl);
    }

    public async Task MarkProcessedAsync(string key, object response = null)
    {
        var redisKey = $"invekto:idem:{key}";
        var state = new IdempotencyState
        {
            Status = "completed",
            CompletedAt = DateTime.UtcNow,
            Response = response != null ? JsonSerializer.Serialize(response) : null
        };

        await _redis.SetAsync(redisKey, JsonSerializer.Serialize(state), _options.CompletedTtl);
    }
}
```

### 3.6 Dead Letter Queue Management

```
DLQ STRUCTURE:

Queue Name: invekto.dlq
├── Persistent: true
├── Max Length: 10,000 messages
├── Max Age: 30 days
└── Overflow: reject-publish

DLQ MESSAGE ENRICHMENT:
├── x-dlq-reason:        Neden DLQ'da (max_retries | permanent_error | manual)
├── x-dlq-entered-at:    DLQ'ya giriş zamanı
├── x-original-routing:  Orijinal routing key
├── x-retry-history:     JSON array of all retry attempts
└── x-last-consumer:     Son işleyen consumer ID

DLQ ACTIONS (Admin UI):
├── VIEW:      Message detaylarını gör
├── REPROCESS: Ana queue'ya geri gönder (reset retry count)
├── REDIRECT:  Farklı queue'ya gönder
├── DELETE:    Mesajı sil (audit log ile)
└── BULK:      Toplu işlem (filter ile)

DLQ ALERTS:
├── dlq.depth > 100      → WARNING
├── dlq.depth > 500      → CRITICAL
├── dlq.age.oldest > 1h  → WARNING
└── dlq.rate > 10/min    → CRITICAL (ani artış)
```

### 3.7 Deliverables

| ID | Deliverable | Açıklama |
|----|-------------|----------|
| 3.1 | RabbitMQ client wrapper | Connection pooling, channel management |
| 3.2 | Publisher service | With circuit breaker |
| 3.3 | Consumer base class | Idempotent, retry-aware |
| 3.4 | Retry queue topology | 5 delay queues + DLQ |
| 3.5 | Outbox pattern | SQL table, dispatcher service |
| 3.6 | Idempotency service | Redis-backed deduplication |
| 3.7 | DLQ service | Read, reprocess, delete operations |
| 3.8 | Integration tests | Full message flow tested |

### 3.8 Exit Criteria

- [ ] Publisher can send messages to RabbitMQ
- [ ] Consumer receives and processes messages
- [ ] Retry mechanism works (2s → 10s → 30s → 2m → 10m → DLQ)
- [ ] DLQ receives failed messages after max retries
- [ ] Idempotency prevents duplicate processing
- [ ] Outbox dispatcher reliably publishes events
- [ ] Integration tests pass with real RabbitMQ

---

# PHASE 4: FULL OBSERVABILITY STACK
## Hafta 11-12

### Amaç
Full observability: distributed tracing (Jaeger), advanced dashboards, alerting rules.

> **NOT:** Temel observability (Prometheus, Loki, basic dashboards) Phase 1'de kuruldu. Bu fazda distributed tracing ve advanced alerting tamamlanıyor.

### 4.1 Stack Overview

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         OBSERVABILITY STACK                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                        MICROSERVICES                                 │   │
│  │                                                                      │   │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐            │   │
│  │  │ Service  │  │ Service  │  │ Service  │  │  Admin   │            │   │
│  │  │    A     │  │    B     │  │    C     │  │   API    │            │   │
│  │  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘            │   │
│  │       │             │             │             │                   │   │
│  │       └─────────────┴─────────────┴─────────────┘                   │   │
│  │                           │                                         │   │
│  │              OpenTelemetry SDK (auto-instrumentation)               │   │
│  │                           │                                         │   │
│  └───────────────────────────┼─────────────────────────────────────────┘   │
│                              │                                             │
│                              ▼                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐ │
│  │                    OTEL COLLECTOR                                     │ │
│  │                    (Central hub)                                      │ │
│  └───────────┬─────────────────┬─────────────────┬───────────────────────┘ │
│              │                 │                 │                         │
│      Traces  │         Metrics │         Logs    │                         │
│              ▼                 ▼                 ▼                         │
│  ┌───────────────┐   ┌───────────────┐   ┌───────────────┐                │
│  │    JAEGER     │   │  PROMETHEUS   │   │     LOKI      │                │
│  │   (Traces)    │   │   (Metrics)   │   │    (Logs)     │                │
│  └───────┬───────┘   └───────┬───────┘   └───────┬───────┘                │
│          │                   │                   │                         │
│          └───────────────────┼───────────────────┘                         │
│                              │                                             │
│                              ▼                                             │
│                    ┌───────────────────┐                                   │
│                    │     GRAFANA       │                                   │
│                    │   (Dashboards)    │                                   │
│                    └───────────────────┘                                   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 4.2 OpenTelemetry Integration

```csharp
// ObservabilityExtensions.cs
public static class ObservabilityExtensions
{
    public static IServiceCollection AddInvektoObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("Observability").Get<ObservabilityOptions>();

        // Activity source for custom spans
        services.AddSingleton(new ActivitySource("Invekto"));

        // OpenTelemetry
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: options.ServiceName,
                    serviceVersion: options.ServiceVersion,
                    serviceInstanceId: Environment.MachineName))
            .WithTracing(tracing => tracing
                .AddSource("Invekto")
                .AddAspNetCoreInstrumentation(opts =>
                {
                    opts.RecordException = true;
                    opts.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health");
                })
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation(opts =>
                {
                    opts.SetDbStatementForText = true;
                    opts.RecordException = true;
                })
                .AddOtlpExporter(opts =>
                {
                    opts.Endpoint = new Uri(options.OtlpEndpoint);
                }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter("Invekto.Resilience")
                .AddMeter("Invekto.Messaging")
                .AddMeter("Invekto.Business")
                .AddPrometheusExporter());

        return services;
    }
}
```

### 4.3 Retry & Circuit Breaker Metrics

```csharp
// InvektoMetrics.cs
public class InvektoMetrics
{
    private readonly Meter _meter;

    // Counters
    public Counter<long> RequestsTotal { get; }
    public Counter<long> RetryAttemptsTotal { get; }
    public Counter<long> CircuitBreakerTransitions { get; }
    public Counter<long> MessagesPublished { get; }
    public Counter<long> MessagesConsumed { get; }
    public Counter<long> DlqMessagesTotal { get; }

    // Histograms
    public Histogram<double> RequestDuration { get; }
    public Histogram<double> MessageProcessingDuration { get; }
    public Histogram<double> QueueWaitTime { get; }

    // Gauges (via ObservableGauge)
    public ObservableGauge<int> ActiveConnections { get; }
    public ObservableGauge<int> QueueDepth { get; }
    public ObservableGauge<int> CircuitBreakerState { get; }

    public InvektoMetrics()
    {
        _meter = new Meter("Invekto.Resilience", "1.0.0");

        RequestsTotal = _meter.CreateCounter<long>(
            "invekto_requests_total",
            description: "Total number of requests processed");

        RetryAttemptsTotal = _meter.CreateCounter<long>(
            "invekto_retry_attempts_total",
            description: "Total number of retry attempts");

        CircuitBreakerTransitions = _meter.CreateCounter<long>(
            "invekto_circuit_breaker_transitions_total",
            description: "Circuit breaker state transitions");

        MessagesPublished = _meter.CreateCounter<long>(
            "invekto_messages_published_total",
            description: "Total messages published to RabbitMQ");

        MessagesConsumed = _meter.CreateCounter<long>(
            "invekto_messages_consumed_total",
            description: "Total messages consumed from RabbitMQ");

        DlqMessagesTotal = _meter.CreateCounter<long>(
            "invekto_dlq_messages_total",
            description: "Total messages sent to DLQ");

        RequestDuration = _meter.CreateHistogram<double>(
            "invekto_request_duration_seconds",
            unit: "s",
            description: "Request duration in seconds");

        MessageProcessingDuration = _meter.CreateHistogram<double>(
            "invekto_message_processing_duration_seconds",
            unit: "s",
            description: "Message processing duration in seconds");
    }
}
```

### 4.4 Grafana Dashboards

```
DASHBOARDS TO CREATE:

1. System Overview
   ├── Request rate (per service)
   ├── Error rate (per service)
   ├── P50/P95/P99 latency
   ├── Active connections
   └── Circuit breaker states

2. RabbitMQ Health
   ├── Queue depths (all queues)
   ├── Message rates (publish/consume)
   ├── DLQ depth
   ├── Consumer lag
   └── Oldest message age

3. Redis Health
   ├── Operations/second
   ├── Memory usage
   ├── Hit rate
   ├── Key count
   └── Latency percentiles

4. Resilience Dashboard
   ├── Retry attempts (by policy)
   ├── Circuit breaker trips
   ├── Fallback invocations
   ├── Timeout occurrences
   └── Bulkhead utilization

5. Troubleshooting Dashboard
   ├── Last 100 errors (table)
   ├── Last 100 slow requests (table)
   ├── Error rate by error code
   ├── Latency breakdown (stacked)
   └── Dependency health matrix

6. Business Metrics
   ├── Jobs created/completed
   ├── Analysis success rate
   ├── Processing time distribution
   └── Tenant usage breakdown
```

### 4.5 Alert Rules

```yaml
# prometheus/alerts/invekto-alerts.yml

groups:
  - name: invekto-infrastructure
    rules:
      - alert: HighErrorRate
        expr: |
          sum(rate(invekto_requests_total{status="error"}[5m]))
          / sum(rate(invekto_requests_total[5m])) > 0.05
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "High error rate detected"
          description: "Error rate is {{ $value | humanizePercentage }}"

      - alert: CircuitBreakerOpen
        expr: invekto_circuit_breaker_state == 1
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Circuit breaker OPEN for {{ $labels.policy }}"

      - alert: DLQGrowing
        expr: invekto_dlq_depth > 100
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "DLQ depth is {{ $value }}"

      - alert: HighLatency
        expr: |
          histogram_quantile(0.95,
            sum(rate(invekto_request_duration_seconds_bucket[5m])) by (le, service)
          ) > 0.8
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "P95 latency > 800ms for {{ $labels.service }}"

      - alert: RetriesExhausted
        expr: rate(invekto_retry_attempts_total{outcome="exhausted"}[5m]) > 0.05
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "Retries exhausted for {{ $labels.policy }}"
```

### 4.6 Deliverables

| ID | Deliverable | Açıklama |
|----|-------------|----------|
| 4.1 | OpenTelemetry integration | Traces + metrics export |
| 4.2 | Jaeger deployment | Trace storage and UI |
| 4.3 | Prometheus deployment | Metrics scraping |
| 4.4 | Loki deployment | Log aggregation |
| 4.5 | Grafana deployment | Dashboards and alerts |
| 4.6 | 6 Grafana dashboards | As specified above |
| 4.7 | Alert rules | Prometheus alerting |
| 4.8 | Serilog configuration | Structured JSON logging |

### 4.7 Exit Criteria

- [ ] Traces visible in Jaeger (end-to-end)
- [ ] Metrics scraped by Prometheus
- [ ] Logs queryable in Loki
- [ ] All 6 dashboards functional
- [ ] Alerts firing correctly (test with chaos)
- [ ] TraceId correlation works across services

---

# PHASE 5: SECURITY & AUTH
## Hafta 13

### Amaç
Rate limiting, audit trail ve Admin panel authentication implementasyonu.

> **NOT:** Inter-service authentication **KAPSAM DIŞI**. Tüm servisler güvenli internal ağda çalışacak.

### 5.1 Security Scope

```
┌─────────────────────────────────────────────────────────────┐
│                    SECURITY BOUNDARIES                       │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  EXTERNAL (Internet)                                        │
│       │                                                     │
│       │ HTTPS (TLS)                                         │
│       ▼                                                     │
│  ┌─────────────────┐                                        │
│  │     NGINX       │  ← Rate limiting (tenant-based)        │
│  │   (Gateway)     │  ← Request validation                  │
│  └────────┬────────┘                                        │
│           │                                                 │
│           │ HTTP (internal, no auth)                        │
│           ▼                                                 │
│  ┌─────────────────────────────────────────────────────┐    │
│  │              INTERNAL NETWORK (Trusted)              │    │
│  │  ┌─────────┐  ┌─────────┐  ┌─────────┐              │    │
│  │  │ Service │  │ Service │  │  Admin  │              │    │
│  │  │    A    │  │    B    │  │   API   │              │    │
│  │  └─────────┘  └─────────┘  └────┬────┘              │    │
│  │                                  │                   │    │
│  │                                  │ Google OAuth      │    │
│  │                                  ▼                   │    │
│  │                             Admin Panel              │    │
│  └─────────────────────────────────────────────────────┘    │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 5.2 Rate Limiting

```csharp
// RateLimitService.cs
public class RateLimitService : IRateLimitService
{
    private readonly IRedisService _redis;
    private readonly RateLimitOptions _options;

    public async Task<RateLimitResult> CheckAsync(string tenantId, string operation)
    {
        var windowKey = $"invekto:rate:{tenantId}:{operation}:{GetCurrentWindow()}";

        var currentCount = await _redis.IncrementAsync(windowKey);

        if (currentCount == 1)
        {
            await _redis.ExpireAsync(windowKey, TimeSpan.FromSeconds(_options.WindowSizeSeconds));
        }

        var limit = _options.GetLimit(operation);

        if (currentCount > limit.HardLimit)
        {
            return new RateLimitResult
            {
                Allowed = false,
                Reason = RateLimitReason.HardLimit,
                RetryAfterSeconds = GetSecondsUntilNextWindow()
            };
        }

        if (currentCount > limit.SoftLimit)
        {
            return new RateLimitResult
            {
                Allowed = true,
                IsWarning = true,
                Reason = RateLimitReason.SoftLimit,
                RemainingRequests = limit.HardLimit - currentCount
            };
        }

        return new RateLimitResult
        {
            Allowed = true,
            RemainingRequests = limit.SoftLimit - currentCount
        };
    }
}
```

### 5.3 Audit Trail

```csharp
// AuditLog.cs (SQL Server table)
public class AuditLog
{
    public long Id { get; set; }
    public string Email { get; set; }
    public string Action { get; set; }           // CREATE, UPDATE, DELETE, REPROCESS, etc.
    public string TargetType { get; set; }       // DLQ_MESSAGE, FLAG, CONFIG, etc.
    public string TargetId { get; set; }
    public string BeforeSnapshot { get; set; }   // JSON
    public string AfterSnapshot { get; set; }    // JSON
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public DateTime Timestamp { get; set; }
    public string RequestId { get; set; }
    public string TraceId { get; set; }
}
```

### 5.4 Deliverables

| ID | Deliverable | Açıklama |
|----|-------------|----------|
| 5.1 | Rate limit service | Redis-backed, tenant-scoped |
| 5.2 | Rate limit middleware | Nginx + application level filtering |
| 5.3 | Audit service | Log all admin actions |
| 5.4 | Audit table | SQL Server schema |
| 5.5 | Admin OAuth | Google OAuth for admin panel |
| 5.6 | Security tests | Rate limit, audit tests |

### 5.5 Exit Criteria

- [ ] Rate limits enforced (soft warning, hard reject)
- [ ] All admin actions audited
- [ ] Audit logs queryable
- [ ] Admin panel requires Google OAuth login
- [ ] Email allowlist enforced

---

# PHASE 6: ADMIN UI & OPERATIONS
## Hafta 14-15

### Amaç
Operations panel: DLQ management, health monitoring, feature flags.

### 6.1 Admin UI Architecture

```
admin-ui/
├── src/
│   ├── components/
│   │   ├── Layout/
│   │   ├── Dashboard/
│   │   ├── DLQ/
│   │   ├── Queues/
│   │   ├── Flags/
│   │   ├── Health/
│   │   └── Audit/
│   ├── pages/
│   │   ├── DashboardPage.tsx
│   │   ├── DLQPage.tsx
│   │   ├── QueuesPage.tsx
│   │   ├── FlagsPage.tsx
│   │   ├── HealthPage.tsx
│   │   ├── AuditPage.tsx
│   │   └── TracePage.tsx
│   ├── services/
│   │   └── api.ts
│   ├── hooks/
│   │   └── useAuth.ts
│   └── App.tsx
├── package.json
└── vite.config.ts

Tech Stack:
├── React 18
├── TypeScript
├── Vite
├── TailwindCSS
├── React Query
└── React Router
```

### 6.2 Admin API Endpoints

```
POST   /admin/api/auth/google          # Google OAuth login
GET    /admin/api/auth/me              # Current user info
POST   /admin/api/auth/logout          # Logout

GET    /admin/api/dlq                  # List DLQ messages (paginated)
GET    /admin/api/dlq/{id}             # Get single message
POST   /admin/api/dlq/{id}/reprocess   # Reprocess message
DELETE /admin/api/dlq/{id}             # Delete message
POST   /admin/api/dlq/bulk/reprocess   # Bulk reprocess
POST   /admin/api/dlq/bulk/delete      # Bulk delete

GET    /admin/api/queues               # Queue stats
GET    /admin/api/queues/{name}        # Single queue details

GET    /admin/api/flags                # List all flags
PUT    /admin/api/flags/{key}          # Update flag
POST   /admin/api/flags/reload         # Trigger config reload

GET    /admin/api/health               # System health overview
GET    /admin/api/health/{service}     # Single service health

GET    /admin/api/audit                # Audit log (paginated)

GET    /admin/api/traces/{traceId}     # Jump to Jaeger trace
```

### 6.3 Feature Flags System

```json
// flags.json
{
  "version": "1.0.0",
  "updatedAt": "2026-01-29T12:00:00Z",
  "updatedBy": "admin@invekto.com",

  "global": {
    "maintenance_mode": false,
    "rate_limit_enabled": true,
    "audit_enabled": true
  },

  "services": {
    "chat-analysis": {
      "enabled": true,
      "sync_enabled": true,
      "async_enabled": true,
      "circuit_breaker_enabled": true
    }
  },

  "resilience": {
    "sync_retry_enabled": true,
    "async_retry_enabled": true,
    "circuit_breaker_enabled": true,
    "fallback_enabled": true
  },

  "limits": {
    "max_concurrent_jobs": 100,
    "max_message_size_kb": 1024,
    "default_rate_limit_per_minute": 100
  }
}
```

### 6.4 Deliverables

| ID | Deliverable | Açıklama |
|----|-------------|----------|
| 6.1 | Admin API | All endpoints implemented |
| 6.2 | Google OAuth | Login with email allowlist |
| 6.3 | Admin UI | React SPA |
| 6.4 | DLQ management | Full CRUD + bulk operations |
| 6.5 | Queue monitoring | Real-time stats |
| 6.6 | Feature flags | View, edit, reload |
| 6.7 | Health dashboard | Service status matrix |
| 6.8 | Audit viewer | Searchable log viewer |
| 6.9 | Trace linking | Jump to Jaeger |

### 6.5 Exit Criteria

- [ ] Google login works with allowlist
- [ ] DLQ messages viewable and reprocessable
- [ ] Flags editable and reloadable
- [ ] Health status accurate
- [ ] Audit log shows all admin actions
- [ ] UI responsive and usable

---

# PHASE 7: FIRST MICROSERVICE (CHAT ANALYSIS)
## Hafta 16-17

### Amaç
İlk production microservice: Chat analysis service.

### 7.1 Service Scope

```
CHAT ANALYSIS SERVICE

Input:
├── Chat conversation data
├── Tenant context
└── Analysis parameters

Processing:
├── Extract entities (names, dates, numbers)
├── Normalize data (formats, encodings)
├── Score sentiment
├── Classify intent
└── Generate summary

Output:
├── Structured JSON result
├── Confidence scores
├── Extracted entities
└── Processing metadata
```

### 7.2 Service Structure

```
Invekto.ChatAnalysis/
├── src/
│   ├── Invekto.ChatAnalysis.Api/
│   │   ├── Controllers/
│   │   │   └── ChatAnalysisController.cs
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   ├── Invekto.ChatAnalysis.Core/
│   │   ├── Services/
│   │   │   ├── IAnalysisService.cs
│   │   │   ├── AnalysisService.cs
│   │   │   ├── EntityExtractor.cs
│   │   │   ├── SentimentAnalyzer.cs
│   │   │   └── IntentClassifier.cs
│   │   ├── Models/
│   │   │   ├── AnalysisResult.cs
│   │   │   ├── ExtractedEntity.cs
│   │   │   └── SentimentScore.cs
│   │   └── Validators/
│   │       └── AnalyzeRequestValidator.cs
│   │
│   └── Invekto.ChatAnalysis.Consumer/
│       ├── Consumers/
│       │   └── ChatAnalysisConsumer.cs
│       └── Program.cs
│
└── tests/
    ├── Invekto.ChatAnalysis.Unit/
    └── Invekto.ChatAnalysis.Integration/
```

### 7.3 Deliverables

| ID | Deliverable | Açıklama |
|----|-------------|----------|
| 7.1 | API project | ASP.NET Core Web API |
| 7.2 | Core library | Business logic |
| 7.3 | Consumer project | RabbitMQ consumer |
| 7.4 | Sync endpoint | /analyze (< 600ms) |
| 7.5 | Async endpoint | /analyze/async |
| 7.6 | Job status endpoint | /jobs/{id} |
| 7.7 | OpenAPI spec | Swagger documentation |
| 7.8 | Unit tests | >80% coverage |
| 7.9 | Integration tests | Full flow tested |

### 7.4 Exit Criteria

- [ ] Sync analysis completes < 600ms (P95)
- [ ] Async jobs processed correctly
- [ ] Idempotency works
- [ ] Circuit breaker trips on failures
- [ ] Metrics exported to Prometheus
- [ ] Traces visible in Jaeger
- [ ] Logs in Loki
- [ ] Health checks passing

---

# PHASE 8: INTEGRATION & TESTING
## Hafta 18-19

### Amaç
Backend entegrasyonu, contract tests, **chaos testing**.

### 8.1 Backend Integration

```
INTEGRATION POINTS:

1. Backend → Microservice (Sync)
   ├── HTTP call with HMAC token
   ├── Context headers propagated
   ├── Timeout: 600ms
   └── Fallback: partial response

2. Backend → Microservice (Async)
   ├── Write to outbox table
   ├── Dispatcher publishes to RabbitMQ
   └── Result callback via webhook

3. Microservice → Backend (Callback)
   ├── HTTP POST with result
   ├── HMAC token for auth
   └── Retry with backoff
```

### 8.2 Chaos Testing

```csharp
// Chaos test scenarios
public class ChaosTests
{
    [Fact]
    public async Task WhenRedisDown_ShouldUseFallback()
    {
        // Inject Redis failure
        _chaosPolicy.InjectFault(FaultType.RedisTimeout);

        // Execute
        var result = await _service.AnalyzeAsync(request);

        // Should degrade gracefully
        Assert.True(result.IsDegraded);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task WhenRabbitMQDown_ShouldQueueLocally()
    {
        // Inject RabbitMQ failure
        _chaosPolicy.InjectFault(FaultType.RabbitMQConnection);

        // Execute
        var result = await _service.SubmitAsyncAsync(request);

        // Should accept and queue locally
        Assert.Equal("accepted", result.Status);
    }

    [Fact]
    public async Task WhenCircuitOpen_ShouldFailFast()
    {
        // Trip circuit breaker
        for (int i = 0; i < 10; i++)
        {
            _chaosPolicy.InjectFault(FaultType.Timeout);
            await _service.AnalyzeAsync(request);
        }

        // Circuit should be open
        var sw = Stopwatch.StartNew();
        var result = await _service.AnalyzeAsync(request);
        sw.Stop();

        // Should fail fast (< 50ms)
        Assert.True(sw.ElapsedMilliseconds < 50);
        Assert.True(result.IsDegraded);
    }
}
```

### 8.3 Deliverables

| ID | Deliverable | Açıklama |
|----|-------------|----------|
| 8.1 | Backend integration | HTTP client with Polly |
| 8.2 | Callback handler | Webhook for async results |
| 8.3 | Contract tests | JSON Schema validation |
| 8.4 | Chaos tests | Fault injection scenarios |
| 8.5 | Integration test suite | End-to-end flows |
| 8.6 | Performance baseline | Latency benchmarks |
| 8.7 | Load test (basic) | Verify 10x capacity |

### 8.4 Exit Criteria

- [ ] Backend successfully calls microservice
- [ ] Async flow works end-to-end
- [ ] All contract tests pass
- [ ] Chaos tests demonstrate resilience
- [ ] System handles 10x expected load
- [ ] No data loss under failure conditions

---

# PHASE 9: PRODUCTION HARDENING
## Hafta 20

### Amaç
Security audit, performance tuning, documentation, go-live checklist.

### 9.1 Security Checklist

```
SECURITY AUDIT:

[ ] HMAC keys rotated and secured
[ ] Redis password strong and not in code
[ ] RabbitMQ credentials secured
[ ] Firewall rules verified (internal only)
[ ] No sensitive data in logs
[ ] Rate limits configured correctly
[ ] Audit trail complete
[ ] Google OAuth allowlist accurate
[ ] HTTPS certificates valid
[ ] No exposed debug endpoints
```

### 9.2 Go-Live Checklist

```
PRE-LAUNCH:

[ ] All tests passing
[ ] Security audit complete
[ ] Performance benchmarks met
[ ] Documentation complete
[ ] Monitoring dashboards ready
[ ] Alerts configured and tested
[ ] Rollback procedure tested
[ ] Team trained on operations
[ ] Stakeholders notified

LAUNCH DAY:

[ ] Health checks passing
[ ] Metrics flowing
[ ] Logs aggregating
[ ] First requests successful
[ ] Error rate acceptable
[ ] Latency within SLA
[ ] No critical alerts

POST-LAUNCH:

[ ] Monitor for 24 hours
[ ] Review error logs
[ ] Adjust alerts if needed
[ ] Document lessons learned
[ ] Plan next iteration
```

### 9.3 Deliverables

| ID | Deliverable | Açıklama |
|----|-------------|----------|
| 9.1 | Security audit report | All findings addressed |
| 9.2 | Performance report | Benchmarks documented |
| 9.3 | Operations runbook | Complete procedures |
| 9.4 | API documentation | OpenAPI + guides |
| 9.5 | Go-live checklist | Verified and signed off |
| 9.6 | Training materials | Team enablement |

### 9.4 Exit Criteria

- [ ] Security audit passed
- [ ] Performance targets met
- [ ] All documentation complete
- [ ] Operations team trained
- [ ] Go-live checklist complete
- [ ] System ready for production traffic

---

# APPENDIX A: TIMELINE SUMMARY

| Phase | Hafta | Süre | Bağımlılık |
|-------|-------|------|------------|
| 0: Foundation | 1-2 | 2 hafta | - |
| 1: Infrastructure | 3-4 | 2 hafta | Phase 0 |
| 2: Core Framework | 5-7 | 3 hafta | Phase 1 |
| 3: Messaging | 8-10 | 3 hafta | Phase 2 |
| 4: Observability | 11-12 | 2 hafta | Phase 2 |
| 5: Security | 13 | 1 hafta | Phase 2 |
| 6: Admin UI | 14-15 | 2 hafta | Phase 3, 4, 5 |
| 7: First Service | 16-17 | 2 hafta | Phase 3, 4, 5 |
| 8: Integration | 18-19 | 2 hafta | Phase 7 |
| 9: Hardening | 20 | 1 hafta | Phase 8 |

**Toplam: 20 hafta (5 ay)**

---

# APPENDIX B: TEAM REQUIREMENTS

| Rol | Sayı | Sorumluluk |
|-----|------|------------|
| Backend Developer | 2 | .NET microservices, infrastructure |
| Frontend Developer | 1 | Admin UI (React) |
| DevOps Engineer | 1 | Infrastructure, CI/CD, monitoring |
| QA Engineer | 1 | Testing, chaos engineering |
| Tech Lead | 1 | Architecture, code review, decisions |

---

# APPENDIX C: RISK MATRIX

| Risk | Olasılık | Etki | Mitigasyon |
|------|----------|------|------------|
| Redis/RabbitMQ learning curve | Orta | Orta | Training, documentation |
| Windows-specific issues | Düşük | Orta | Thorough testing |
| Integration complexity | Orta | Yüksek | Incremental integration |
| Performance targets missed | Düşük | Yüksek | Early benchmarking |
| Scope creep | Yüksek | Orta | Strict phase gates |
| Key person dependency | Orta | Yüksek | Documentation, knowledge sharing |
| Retry storm (cascade) | Orta | Yüksek | Circuit breaker + backoff + jitter |
| DLQ overflow | Düşük | Orta | Alerts + auto-archive + monitoring |
| Idempotency key collision | Çok Düşük | Orta | ULID + hash verification |

---

# APPENDIX D: ERROR CATEGORIES & RETRY BEHAVIORS

## D.1 Error Classification

```
┌─────────────────────────────────────────────────────────────────┐
│                    HATA KATEGORİLERİ                            │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  TRANSIENT (Geçici)              PERMANENT (Kalıcı)             │
│  ├── Network timeout             ├── 400 Bad Request            │
│  ├── 502 Bad Gateway             ├── 401 Unauthorized           │
│  ├── 503 Service Unavailable     ├── 403 Forbidden              │
│  ├── 504 Gateway Timeout         ├── 404 Not Found              │
│  ├── Connection refused          ├── 422 Validation Error       │
│  ├── DNS resolution fail         ├── Business logic error       │
│  ├── TCP reset                   └── Schema mismatch            │
│  ├── SQL deadlock (1205)                                        │
│  ├── SQL timeout (-2)            UNKNOWN                        │
│  └── Redis connection lost       ├── 500 Internal Server Error  │
│                                  └── Unhandled exception        │
│  CIRCUIT OPEN                                                   │
│  └── Downstream aşırı yüklü                                     │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## D.2 Retry Matrix

| Kategori | Retry? | Max Deneme | Strateji | Fallback |
|----------|--------|------------|----------|----------|
| **TRANSIENT** | ✓ EVET | Sync: 2, Async: 5 | Exponential + Jitter | Partial response |
| **PERMANENT** | ✗ HAYIR | 0 | Immediate fail | Error response |
| **UNKNOWN** | △ ŞARTLI | 1 | Single retry | Log + Alert |
| **CIRCUIT OPEN** | ✗ HAYIR | 0 | Fast fail | Cached/Partial |

## D.3 HTTP Status Code Mapping

```
RETRY EDILECEK (Transient):
├── 408 Request Timeout
├── 429 Too Many Requests (Rate limit - with backoff)
├── 500 Internal Server Error (1 kez, dikkatli)
├── 502 Bad Gateway
├── 503 Service Unavailable
└── 504 Gateway Timeout

RETRY EDİLMEYECEK (Permanent):
├── 400 Bad Request
├── 401 Unauthorized
├── 403 Forbidden
├── 404 Not Found
├── 405 Method Not Allowed
├── 409 Conflict
├── 410 Gone
├── 415 Unsupported Media Type
└── 422 Unprocessable Entity
```

---

# APPENDIX E: CIRCUIT BREAKER CONFIGURATIONS

## E.1 Circuit Breaker State Machine

```
┌─────────────────────────────────────────────────────────────────┐
│                   CIRCUIT BREAKER STATES                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│                    ┌──────────────┐                             │
│         ┌─────────►│    CLOSED    │◄─────────┐                  │
│         │          │  (Normal)    │          │                  │
│         │          └──────┬───────┘          │                  │
│         │                 │                  │                  │
│         │    Failure threshold exceeded      │                  │
│         │    (>50% error in 30s window)      │                  │
│         │                 │                  │                  │
│         │                 ▼                  │                  │
│         │          ┌──────────────┐          │                  │
│   Success         │     OPEN     │     Success                 │
│   threshold       │  (Fail Fast) │     in half-open            │
│   reached         └──────┬───────┘                              │
│         │                 │                                     │
│         │    Timeout expires (30-60s)                           │
│         │                 │                                     │
│         │                 ▼                                     │
│         │          ┌──────────────┐                             │
│         └──────────│  HALF-OPEN   │                             │
│                    │  (Testing)   │                             │
│                    └──────────────┘                             │
│                           │                                     │
│                      Failure                                    │
│                           │                                     │
│                           ▼                                     │
│                    Back to OPEN                                 │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## E.2 Per-Dependency Configuration

| Dependency | Failure Threshold | Sampling | Break Duration | Fallback |
|------------|------------------|----------|----------------|----------|
| External API | 50% | 30s | 30s | Cached response |
| Redis | 30% | 15s | 15s | Local cache |
| RabbitMQ | 40% | 30s | 60s | Local queue |
| Microservice | 50% | 30s | 30s | Partial response |
| Database | 50% | 30s | 30s | Stale data |

## E.3 Fallback Strategies

```
┌────────────────────┬────────────────────────────────────────────┐
│ Dependency         │ Fallback Behavior                          │
├────────────────────┼────────────────────────────────────────────┤
│ Redis              │ Local in-memory cache (LRU, 1000 items)    │
│                    │ Skip non-critical operations               │
│                    │ Log all skipped operations for replay      │
├────────────────────┼────────────────────────────────────────────┤
│ RabbitMQ           │ Local file queue (SQLite fallback)         │
│                    │ Background sync when circuit closes        │
│                    │ Alert: "Events queued locally"             │
├────────────────────┼────────────────────────────────────────────┤
│ External API       │ Return cached response if fresh (<5min)    │
│                    │ Return partial response with warning       │
│                    │ Return: { status: "degraded", ... }        │
├────────────────────┼────────────────────────────────────────────┤
│ Microservice       │ Return partial/cached analysis             │
│                    │ Queue for async processing                 │
│                    │ Return: { status: "pending", jobId: "..." }│
├────────────────────┼────────────────────────────────────────────┤
│ Database           │ Read: Return stale data + warning          │
│                    │ Write: Queue in outbox, alert              │
└────────────────────┴────────────────────────────────────────────┘
```

---

# APPENDIX F: IDEMPOTENCY & DEDUPLICATION

## F.1 Idempotency Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│  Request arrives with:                                          │
│  ├── X-Idempotency-Key: "tenant123_chat456_op789_1706500000"   │
│  └── X-Request-Id: "req_abc123"                                │
│                                                                 │
│                         │                                       │
│                         ▼                                       │
│              ┌─────────────────────┐                            │
│              │   Redis Lookup      │                            │
│              │   Key: idem:{key}   │                            │
│              └──────────┬──────────┘                            │
│                         │                                       │
│              ┌──────────┴──────────┐                            │
│              │                     │                            │
│           EXISTS               NOT EXISTS                       │
│              │                     │                            │
│              ▼                     ▼                            │
│     ┌─────────────────┐   ┌─────────────────┐                   │
│     │ Return cached   │   │ Process request │                   │
│     │ response        │   │ Store result    │                   │
│     │ (no processing) │   │ Set TTL: 24h    │                   │
│     └─────────────────┘   └─────────────────┘                   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## F.2 Key Format

```
KEY FORMAT:

Sync HTTP:
  idem:sync:{tenantId}:{operationType}:{resourceId}:{hash(payload)}
  TTL: 24 hours

Async Job:
  idem:job:{messageId}
  TTL: 7 days (jobs can be long-running)

Webhook:
  idem:webhook:{tenantId}:{eventType}:{eventId}
  TTL: 48 hours

EXAMPLES:
  idem:sync:tenant123:analyze:chat456:a1b2c3d4
  idem:job:01ARZ3NDEKTSV4RRFFQ69G5FAV
  idem:webhook:tenant123:message.created:evt_789
```

## F.3 Deduplication Windows

| Request Type | Window | Key Components |
|--------------|--------|----------------|
| API Mutation | 24h | tenant + op + resource |
| Async Job Submit | 1h | tenant + jobType + params |
| Webhook Delivery | 48h | tenant + event + eventId |
| Message Processing | 7d | messageId (ULID) |
| Report Generation | 6h | tenant + reportType + date |

---

# APPENDIX G: POLLY POLICY ARCHITECTURE

## G.1 Policy Stack

```
POLLY POLICY STACK:

┌─────────────────────────────────────────────────────────────────┐
│                                                                 │
│  OUTER LAYER (Applied First)                                    │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                    TIMEOUT POLICY                         │  │
│  │                    (Overall timeout)                      │  │
│  └───────────────────────────────────────────────────────────┘  │
│                              │                                  │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                  CIRCUIT BREAKER                          │  │
│  │                  (Fail fast if open)                      │  │
│  └───────────────────────────────────────────────────────────┘  │
│                              │                                  │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                    RETRY POLICY                           │  │
│  │                    (With backoff)                         │  │
│  └───────────────────────────────────────────────────────────┘  │
│                              │                                  │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                   BULKHEAD POLICY                         │  │
│  │                   (Concurrency limit)                     │  │
│  └───────────────────────────────────────────────────────────┘  │
│                              │                                  │
│  INNER LAYER (Applied Last)                                     │
│  ┌───────────────────────────────────────────────────────────┐  │
│  │                   FALLBACK POLICY                         │  │
│  │                   (Graceful degradation)                  │  │
│  └───────────────────────────────────────────────────────────┘  │
│                              │                                  │
│                              ▼                                  │
│                        HTTP REQUEST                             │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘

EXECUTION ORDER: Timeout → CircuitBreaker → Retry → Bulkhead → Fallback → Request
```

## G.2 Named Policies

| Policy Name | Use Case | Timeout | Retry | Circuit Breaker |
|-------------|----------|---------|-------|-----------------|
| SyncMicroservice | Backend → Microservice | 600ms | 1 | 50%/30s/30s |
| AsyncJob | RabbitMQ consumer | 5min | Disabled | Disabled |
| ExternalApi | 3rd party APIs | 30s | 3 | 50%/60s/60s |
| RedisOperation | Redis commands | 100ms | 1 | 30%/15s/15s |
| DatabaseQuery | SQL Server | 30s | 3 | 50%/30s/30s |

---

# APPENDIX H: MONITORING & ALERTING DETAILS

## H.1 Prometheus Metrics

```
# Retry metrics
invekto_retry_attempts_total{policy, outcome, error_type}
invekto_retry_duration_seconds{policy, attempt}

# Circuit breaker metrics
invekto_circuit_breaker_state{policy, state}
invekto_circuit_breaker_transitions_total{policy, from, to}

# Bulkhead metrics
invekto_bulkhead_concurrent_requests{policy}
invekto_bulkhead_queue_depth{policy}

# Fallback metrics
invekto_fallback_invocations_total{policy, reason}

# DLQ metrics
invekto_dlq_depth{queue}
invekto_dlq_oldest_message_age_seconds{queue}
invekto_dlq_messages_entered_total{queue, reason}
```

## H.2 Alert Rules Summary

| Alert | Condition | Severity |
|-------|-----------|----------|
| HighErrorRate | >5% errors for 5m | critical |
| CircuitBreakerOpen | state=open for 1m | critical |
| DLQGrowing | depth > 100 for 10m | warning |
| DLQCritical | depth > 500 for 5m | critical |
| HighLatency | P95 > 800ms for 5m | warning |
| RetriesExhausted | rate > 0.05 for 5m | critical |
| OldDLQMessages | age > 1h for 5m | warning |

## H.3 Grafana Dashboard Rows

```
RETRY DASHBOARD LAYOUT:

Row 1: Overview
├── Panel: Total Requests vs Retries (time series)
├── Panel: Retry Success Rate (gauge, %)
├── Panel: Circuit Breaker States (state timeline)
└── Panel: Active Alerts (alert list)

Row 2: Per-Policy Breakdown
├── Panel: Retry Attempts by Policy (stacked bar)
├── Panel: Retry Duration Distribution (heatmap)
├── Panel: Error Types (pie chart)
└── Panel: Fallback Invocations (counter)

Row 3: Queue Health
├── Panel: RabbitMQ Queue Depths (time series)
├── Panel: DLQ Depth (time series with threshold)
├── Panel: Message Processing Rate (time series)
└── Panel: Consumer Lag (time series)

Row 4: Dependency Health
├── Panel: Redis Latency (p50, p95, p99)
├── Panel: External API Latency (p50, p95, p99)
├── Panel: Microservice Latency (p50, p95, p99)
└── Panel: Database Latency (p50, p95, p99)
```

---

**Document Version:** 2.0 (Consolidated)
**Last Updated:** 2026-01-29
**Status:** DRAFT - Awaiting Review
**Next Review:** Before Phase 0 kickoff

