# Lessons Learned

> Tekrarlanan hatalar ve çözümleri. Her session başında okunmalı!

---

## Pattern: Windows PowerShell Commands

**Problem:** Bash/Linux syntax Windows'ta çalışmaz.

**Solution:**
```
✅ DOĞRU: powershell -NoProfile -Command "cd c:\path; dotnet build"
❌ YANLIŞ: cd /path && dotnet build
```

**Rule:** Her zaman `powershell -NoProfile -Command "..."` wrapper kullan.

---

## Pattern: Idempotency Key Generation

**Problem:** Duplicate işlem riski.

**Solution:**
- Her request'te `X-Idempotency-Key` header'ı zorunlu
- Redis'te TTL ile saklama (24 saat)
- İşlem başlamadan önce kontrol et

**Rule:** Critical işlemlerde idempotency key ZORUNLU.

---

## Pattern: Circuit Breaker Configuration

**Problem:** Yanlış threshold'lar cascade failure'a neden olur.

**Solution:**
```
Sync Services:
- Failure threshold: 50%
- Sampling duration: 30s
- Break duration: 30s
- Minimum throughput: 10

Redis Operations:
- Failure threshold: 30%
- Sampling duration: 15s
- Break duration: 15s
```

**Rule:** Redis için daha agresif circuit breaker kullan.

---

## Pattern: Retry with Backoff

**Problem:** Senkron retry'da timeout aşımı.

**Solution:**
```
Sync Retry Budget:
- Total: 150ms max
- First attempt: 100ms timeout
- Retry: 50ms timeout + 25ms base delay + 0-25ms jitter

Async Retry (RabbitMQ):
- 2s → 10s → 30s → 2m → 10m → DLQ
- Toplam: ~13 dakika
```

**Rule:** Sync retry = micro-delay only. Uzun retry = async queue.

---

## Pattern: Error Response Format

**Problem:** Tutarsız error response'lar debugging'i zorlaştırır.

**Solution:**
```json
{
  "success": false,
  "error": {
    "code": "SVC-CAT-NNN",
    "message": "User-friendly message",
    "details": "Ops-only details (null in prod)",
    "traceId": "correlation-id"
  },
  "meta": {
    "requestId": "...",
    "processingTimeMs": 42
  }
}
```

**Rule:** Her hata error code + traceId içermeli.

---

## Pattern: Health Check Endpoints

**Problem:** Farklı health check ihtiyaçları.

**Solution:**
```
/health  → Self check only (liveness)
/ready   → All dependencies (readiness)
```

**Rule:** Kubernetes/load balancer için iki ayrı endpoint.

---

## Pattern: Configuration Management

**Problem:** Secrets hardcoded veya config dosyasında.

**Solution:**
```
✅ DOĞRU: Environment variables
   INVEKTO_REDIS_PASSWORD=***
   INVEKTO_RABBITMQ_PASSWORD=***

❌ YANLIŞ: appsettings.json içinde password
```

**Rule:** Secrets = env variables. Config = appsettings (non-sensitive only).

---

## Pattern: Structured Logging

**Problem:** Log'larda context eksik.

**Solution:**
```csharp
using var _ = _logger.BeginScope(new Dictionary<string, object>
{
    ["TenantId"] = context.TenantId,
    ["RequestId"] = context.RequestId,
    ["TraceId"] = context.TraceId
});

_logger.LogInformation("Processing message {MessageId}", messageId);
```

**Rule:** Her log scope'unda TenantId, RequestId, TraceId olmalı.

---

## Pattern: Dead Letter Queue Handling

**Problem:** DLQ mesajları kaybolur veya manuel müdahale gerekir.

**Solution:**
- DLQ'ya düşen her mesaj Admin UI'da görünür
- Reprocess butonu ile tekrar kuyruğa alınabilir
- Alert: DLQ depth > 10 → notification

**Rule:** DLQ = monitored, not ignored.

---

## Anti-Pattern: Silent Failures

**Problem:** try-catch içinde exception yutulur.

**Solution:**
```csharp
// ❌ YANLIŞ
catch (Exception) { }

// ✅ DOĞRU
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to process {MessageId}", messageId);
    throw;  // veya rethrow/DLQ
}
```

**Rule:** Silent failure = code review FAIL.

---

## Anti-Pattern: Unbounded Retry

**Problem:** Retry döngüsü sonsuz devam eder.

**Solution:**
```
- Sync: Max 1 retry
- Async: Max 5 retry + DLQ
- Circuit breaker: 30s break
```

**Rule:** Her retry mechanism'de limit ZORUNLU.

---

## Anti-Pattern: N+1 Queries

**Problem:** Döngü içinde database query.

**Solution:**
```csharp
// ❌ YANLIŞ
foreach (var id in ids)
{
    var item = await _db.GetByIdAsync(id);
}

// ✅ DOĞRU
var items = await _db.GetByIdsAsync(ids);
```

**Rule:** Döngü içinde I/O = code review flag.

---

## Checklist: Before Every Commit

- [ ] Build PASS?
- [ ] Error codes from arch/errors.md?
- [ ] Idempotency where needed?
- [ ] Circuit breaker configured?
- [ ] Health check updated?
- [ ] Structured logging with context?
- [ ] No silent failures?

---

**Last Updated:** 2026-01-29
**Entry Count:** 12
