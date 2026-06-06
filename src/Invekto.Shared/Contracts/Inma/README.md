# Invekto.Shared.Contracts.Inma

INMA (wapcrm) ile paylaşılan tüm contract'ların (DTO, webhook payload, client arayüzleri) canonical namespace'i.

## Yapı

```
Contracts/Inma/
├── Dtos/        — REST API request/response DTO'ları
├── Webhooks/    — INMA → INSE inbound webhook payload'ları (gelecek)
├── *Client.cs   — typed cxapi HTTP client'ları (root)
└── README.md
```

## cxapi Client'ları (root)

- `HttpInmaContactOptOutClient` — INMA `/api/optout`+`/optin` push (J2 opt-out sync). DI-time tek (global) secret → DefaultRequestHeaders.
- `WapCrmSendClient` (FEAT-PROJELER cxapi PR-2, 2026-06-06) — cxapi `POST /api/chatoperation` düz-metin gönderim; envelope → typed `WapCrmSendResult` (Submitted/ProviderFailed/RateLimited/Ambiguous/TransportError). **Per-request** secret (`HttpRequestMessage.TryAddWithoutValidation("X-CIB-SecretKey")`, ASLA DefaultRequestHeaders — multi-tenant pooled client). 301/302 (HTTP veya envelope, status!=true) → bounded retry+backoff/jitter → RateLimited; status==true otoriter Submitted. DI: `AllowAutoRedirect=false`+`UseCookies=false`. **PR-2'de prod routing YOK** (PR-3 cutover). DTO'lar `Dtos/WapCrmSendDtos.cs`.

## Consolidation Durumu (UP0.1 MVP)

**Bu iterasyonda:**
- ✅ `WapCrmInstanceDto`, `WapCrmApiEnvelope`, `WapCrmRawInstance` (önceden Backend/Data inline idi)
- ✅ `WapCrmSettings` (önceden Backend/Data inline idi)

**UP0.1b (2026-04-15, tamamlandı):**
- ✅ `WapCrmMessage` + `WapCrmApiResponse<T>` → `Dtos/WapCrmMessage.cs` (namespace `Invekto.Shared.Contracts.Inma.Dtos`)
- ✅ `IncomingWebhookEvent` + `WebhookMessage` → `Webhooks/IncomingWebhookEvent.cs` (namespace `Invekto.Shared.Contracts.Inma.Webhooks`)

Hard cut — eski namespace'ler kaldırıldı, 5 caller dosyası `using` güncellendi (Backend Program/AttributionService, Automation Program/Orchestrator, ChatAnalysis WapCrmClient).

## Kural

- INMA contract'ları **sadece burada** tanımlansın (inline DTO yasak)
- INMA endpoint'leri `Invekto.Shared.Clients.Inma` altındaki interface'lerle çağrılsın (UP0.5)
- Contract değişikliği breaking ise INMA ekibiyle koordinasyon gerekli

Referans: `arch/platform/inma-inse-unification/`
