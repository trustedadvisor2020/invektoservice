# Logging Standards

> Tüm servisler için loglama standartları.
> **IMPLEMENTATION:** `src/Invekto.Shared/Logging/JsonLinesLogger.cs`

## Stage-0 Log Format (JSON Lines)

Her satır bir JSON objesi:

```json
{"timestamp":"2026-02-02T12:00:00.000Z","service":"Invekto.Backend","level":"INFO","requestId":"abc123","tenantId":"tenant1","chatId":"chat1","route":"/api/v1/chat/analyze","durationMs":45,"status":"ok","message":"Request completed"}
```

## Zorunlu Alanlar

| Field | Type | Description |
|-------|------|-------------|
| `timestamp` | ISO 8601 | UTC zaman damgası |
| `service` | string | Servis adı (Invekto.Backend, Invekto.ChatAnalysis) |
| `level` | string | INFO, WARN, ERROR |
| `requestId` | string | Unique request ID (korelasyon için) |
| `tenantId` | string | Tenant ID |
| `chatId` | string | Chat ID |
| `route` | string | Endpoint path |
| `durationMs` | number | İşlem süresi (ms) |
| `status` | string | ok, partial, fail |
| `errorCode` | string | Hata kodu (INV-xxx) - sadece hatalarda |
| `message` | string | Kısa açıklama |

## Log Levels

| Level | Kullanım | Status |
|-------|----------|--------|
| `INFO` | Normal işlemler | ok |
| `WARN` | Partial response, degraded | partial |
| `ERROR` | Hatalar | fail |

## Log Dosya Yapısı

```
logs/
├── 2026-02-01.jsonl
├── 2026-02-02.jsonl
└── ...
```

- Günlük rotasyon: `YYYY-MM-DD.jsonl`
- Retention: 30 gün
- Format: JSON Lines (her satır valid JSON)

## Kullanım

### C# Kodu

```csharp
var logger = new JsonLinesLogger("Invekto.Backend", "logs");

// Info log
logger.Info("Request completed", context, "/api/v1/chat/analyze", durationMs: 45);

// Warning log
logger.Warn("Microservice timeout", context, "/api/v1/chat/analyze", ErrorCodes.BackendMicroserviceTimeout);

// Error log
logger.Error("Processing failed", context, "/api/v1/chat/analyze", ErrorCodes.ChatAnalysisProcessingFailed);
```

### RequestContext

```csharp
var context = RequestContext.Create(tenantId: "tenant1", chatId: "chat1");
// Otomatik olarak RequestId üretir
```

## Payload Logging YASAK

Stage-0 kuralı: Payload içeriği ASLA loglanmaz.

```csharp
// ❌ YANLIŞ
logger.Info($"Received payload: {payload}");

// ✅ DOĞRU
logger.Info("Payload received", context, route, payloadSize: payload.Length);
```

## Log Okuma (Ops)

```powershell
# Son 10 satır
Get-Content logs\2026-02-02.jsonl -Tail 10

# Hataları filtrele
Get-Content logs\2026-02-02.jsonl | Where-Object { $_ -match '"level":"ERROR"' }

# RequestId ile ara
Get-Content logs\2026-02-02.jsonl | Where-Object { $_ -match '"requestId":"abc123"' }
```

## Stage-0 Limitleri

- Trace ID yok (sadece requestId ile korelasyon)
- Centralized logging yok (dosya bazlı)
- Log aggregation yok (manuel okuma)

Stage-1'de: OpenTelemetry, Loki, Grafana eklenecek.
