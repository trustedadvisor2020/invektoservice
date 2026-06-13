# INSE Mevcut Durum — Unification Açısından

**Tarih:** 2026-04-13 (audit raporu sonrası)

## ✅ Zaten Var Olanlar (yeniden yapmayacağız)

### SSO / Auth Altyapısı (UP0.2 neredeyse hazır)
- `Invekto.Shared/Auth/InmaJwtValidator.cs` — JWT doğrulama
- `Invekto.Shared/Auth/InmaJwtSettings.cs` — config mapping
- `Invekto.Shared/Auth/InmaTokenContext.cs` — claims: `TenantId`, `UserId`, `Role`, `InseFeatures`
- `Invekto.Shared/Middleware/JwtAuthMiddleware.cs` — claim map: `CompanyCode → TenantId`, `ChatRole 1/2 → agent/admin`
- `appsettings.Production.json` → `InmaAuth` bloğu: `SecretKey`, `LoginUrl`, `RefreshUrl`, `ApiBaseUrl`, `ClockSkewSeconds`, `MockEnabled`
- Backend Program.cs:247 — validator DI registered, mock fallback mevcut

### INMA Webhook Inbound (UP0.7 hazır; C2 2026-06-13 ile DUAL-PURPOSE)
- Backend `POST /api/v1/webhook/event` (Program.cs MapPost ~1973)
- **İKİ dal:** (1) inbound mesaj/ack (legacy); (2) **INMA imzalı `customer.selection_changed` system event (C2)** — root `type` ile ayrılır, mesaj/ack kontrolünden ÖNCE.
- Model: `IncomingWebhookEvent` (mesaj) / `CustomerSelectionChangedEvent` (system event)
- Auth: JWT Bearer + IP whitelist (events kanalı ayrıca fail-closed HMAC-SHA256)
- C2 handler: dedupe(tenant_id,event_id) + durable audit `inma_webhook_events` + opaque `leads.customer_status` derivation

### INMA Client (read-only, UP0.5 temel)
- `Invekto.ChatAnalysis/Services/WapCrmClient.cs`
- Metod: `GetMessagesForPhoneAsync(phoneNumber, instanceId?)` → `POST /api/messagelistforphone`
- Auth: `X-CIB-SecretKey`
- Base URL: `https://cxapi.wapcrm.net/api`
- Timeout: 600ms (Stage-0)
- Backend `FetchWapCrmInstances` inline HttpClient ile `/api/Instances`

### INMA DTO'lar (8 adet, dağınık)
| DTO | Yer |
|-----|-----|
| `WapCrmMessage` | `Shared/DTOs/ChatAnalysis/` |
| `WapCrmApiResponse<T>` | `Shared/DTOs/ChatAnalysis/` |
| `WapCrmInstanceDto` | `Backend/Data/InstanceRepository.cs` (INLINE) |
| `WapCrmApiEnvelope` | `Backend/Data/InstanceRepository.cs` (INLINE) |
| `WapCrmRawInstance` | `Backend/Data/InstanceRepository.cs` (INLINE) |
| `WapCrmSettings` | `Backend/Data/TenantRegistryRepository.cs` (INLINE) |
| `IncomingWebhookEvent` | `Shared/DTOs/Integration/` |
| `WebhookMessage` | `Shared/DTOs/Integration/` |

### Flow Engine (FazO audit'ten)
- `FlowValidator.cs` + node tipleri: message_text, ai_intent, ai_faq, message_menu, action_api_call, action_delay, logic_condition, logic_switch, action_handoff
- **State ephemeral** (G6)

### Intent Detector
- `Invekto.Automation/Services/IntentDetector.cs` — Claude Haiku (production LLM)
- `MockIntentDetector.cs` — TR-aware keyword fallback

### Scheduler
- `ReminderSchedulerService.cs` — System.Timer, 300s interval (G7'de Hangfire'a migrate)

## ❌ Eksik Olanlar (unification için yapılacak)

### Contract Discipline (UP0.1)
- `Invekto.Shared/Contracts/Inma/` klasörü YOK → tüm INMA DTO'ları buraya consolide edilecek
- Inline DTO'lar (Backend Data katmanında) Shared'a taşınacak
- Namespace: `Invekto.Shared.Contracts.Inma` (DTOs) + `Invekto.Shared.Clients.Inma` (client interface'ler)

### INMA Send / Outbound Client (UP0.5)
- **YOK** — WapCrmClient sadece OKUR.
- Yeni `IInmaSendClient` interface + impl gerekli:
  - `SendTextAsync(companyCode, channelId, phone, text, variables)`
  - `SendMediaAsync(companyCode, channelId, phone, mediaId)`
  - `StartChatAsync(companyCode, ChatV3VM)`
  - `UploadFileAsync(companyCode, file)` (media library)
  - `GetCompanyChannelsAsync(companyCode)` → WA/IG/Telegram list
  - `BulkSendAsync(...)` (J4 INMA tarafında eklenince kullanılacak)

### INMA Tenant Lifecycle Handler
- YOK — INMA'dan `tenant.created` webhook geldiğinde auto-provision yapan handler
- Yeni: `POST /api/inbound/tenant-lifecycle` endpoint

### INMA Form/Landing Inbound Handler (J-28 için)
- INMA'da inbound webhook yok → INSE karşılayacak
- Yeni: `POST /api/inbound/form/{tenantId}` + INMA API'ye contact create push

### Feature Flag Service (UP0.6)
- Token'da `InseFeatures` claim var ama merkezi `IFeatureFlagService` YOK
- Yeni: request-scope feature check + 5dk cache

## 📉 Revize Efor (keşiften sonra)

| Paket | İlk tahmin | Gerçek |
|-------|-----------|--------|
| UP0.1 Contracts | 1g | **1g** (sadece reorg, yeni kod az) |
| UP0.2 SSO | 3-4g | **1-2g** (altyapı var, sadece end-to-end doğrulama + role map tamamlama) |
| UP0.3 Unified Tenant | 1-2g | **1-2g** (lazy provisioning tek path — 2026-04-17 Q kararı, bulk backfill atlandı) |
| UP0.4 Domain/UX | 4-5g | 4-5g |
| UP0.5 Shared Data | 3-4g | **4-5g** (yeni send client + custom field adapter) |
| UP0.6 Feature Flags | 1-2g | 1g (token claim var, service wrap yap) |
| UP0.7 Bidirectional Sync | 1g | **0.5g** (webhook zaten çalışıyor, sadece event tip genişletme) |
| UP0.8 Joint (INMA) | — | INMA ekibi |

**Yeni toplam P0:** ~12-16g (ilk tahmin 14-19g idi, keşif 2-3g kazandırdı)
