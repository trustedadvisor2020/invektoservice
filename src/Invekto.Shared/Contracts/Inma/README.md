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

**Sonraki iterasyon (UP0.1b):**
- [ ] `Invekto.Shared.DTOs.ChatAnalysis.WapCrmMessage` + `WapCrmApiResponse<T>` → buraya taşı
- [ ] `Invekto.Shared.DTOs.Integration.IncomingWebhookEvent` + `WebhookMessage` → `Webhooks/` altına

Namespace rename breaking change — tüm caller'ların `using` satırları güncellenecek. Ayrı paket.

## Kural

- INMA contract'ları **sadece burada** tanımlansın (inline DTO yasak)
- INMA endpoint'leri `Invekto.Shared.Clients.Inma` altındaki interface'lerle çağrılsın (UP0.5)
- Contract değişikliği breaking ise INMA ekibiyle koordinasyon gerekli

Referans: `arch/platform/inma-inse-unification/`
