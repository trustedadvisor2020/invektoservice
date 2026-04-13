# Invekto.Shared.Contracts.Inma

INMA (wapcrm) ile paylaşılan tüm contract'ların (DTO, webhook payload, client arayüzleri) canonical namespace'i.

## Yapı

```
Contracts/Inma/
├── Dtos/        — REST API request/response DTO'ları
├── Webhooks/    — INMA → INSE inbound webhook payload'ları (gelecek)
└── README.md
```

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
