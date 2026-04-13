# Kararlar (2026-04-13, Q onaylı)

| # | Konu | Karar | Sonuç |
|---|------|-------|-------|
| 1 | Custom Fields | **REVİZE: INMA'nın mevcut 10 tenant custom field'ını kullan** | G4 iptal, 4-5g kazanç |
| 2 | Flow Wait State | **A — `flow_execution_state` tablosu (persistent)** | FlowEngineV2 refactor, M efor (2-3g), Faz 5'te |
| 3 | Scheduler | **A — Hangfire migration** | `ReminderSchedulerService` kaldırılır, L efor (5-7g), Faz 5 öncesi |
| 4 | Multi-channel | **INMA üzerinden WA + IG + Telegram (Email YOK)** | `IMessageChannel` + `InmaAdapter` (single adapter, 3 kanal), M efor (3g) |
| 5 | INMA Send | **Swagger doğrulandı** — endpoint'ler hazır, 5050 tenant'ta canlı |

## INMA Endpoint Haritası (Faz 2 için)

| İhtiyaç | INMA Endpoint |
|---------|--------------|
| Outbound mesaj başlat | `POST /api/chatsv3/start-chat` (ChatV3VM) |
| Outbound v3 alternatif | `POST /api/chatsv3/start-chat-v3` (ChartStartControlVM) |
| Kanal listesi (WA/IG/Telegram) | `GET /api/chatsv3/getcompanychannels` |
| Telefondan chat bul | `GET /api/chatsv3/find-chat-by-phonenumber/{id}` |
| Media upload | `POST /api/chats/upload-file` |
| Image retrieve | `GET /api/chats/getimage` |
| Block/unblock | `POST /api/chatsv3/block-phone-number` |
| Performance raporu | `GET /api/chats/performance-report` |

**Auth:** `X-CIB-SecretKey` (mevcut `WapCrmClient.cs` pattern).

**Açık soru:** Inbound webhook? Swagger görünen listede webhook endpoint yok — INMA **polling** mi kullanıyor, yoksa ayrı dokümantasyonda mı? `WapCrmClient` nasıl inbound alıyor → Faz 2'de verify.

## Revize Efor Tahmini

| Paket | Süre | Not |
|-------|------|-----|
| G3 (Template rotation) | 1-2g | Faz 3 öncesi |
| G6 (Flow state) | 2-3g | Faz 5 öncesi |
| G7 (Hangfire) | 5-7g | Faz 5 öncesi, tüm cron'lar migrate |
| G2 (Multi-channel) | 3g | Faz 2 içinde |
| ~~G4~~ | **İPTAL** | INMA'nın 10 field'ı kullanılacak |
| G8 (EN locale test) | <1g | Faz 3 içinde |
| **Toplam gap fix** | **~15-20g** | |
| Faz 0-9 orijinal | 9g | |
| **GRAND TOTAL** | **~22-28 iş günü** | |

**Önemli:** Hangfire + Custom fields + Multi-channel = **platform yatırımı**, sadece Dent'e ait değil. Pilot sonrası diğer tüm tenantlar faydalanır.

## Unified Platform Kararı (2026-04-13 — paradigma şifti)

INMA + INSE **tek native platform**. Dent pilot bu çerçevede. Detay: [unified-platform-architecture.md](unified-platform-architecture.md).

**P0 (Dent için şart, ~12-15g):**
- SSO (INMA token → INSE)
- Unified tenant (`CompanyCode` = `tenant_id`)
- Bidirectional sync (webhook var, shared bus v2)
- Shared data layer (INMA contacts + INSE extensions)
- Feature flag / license merkez INMA'da
- Contract discipline (Invekto.Shared her iki tarafta)

**P1 (UX, v1.1):** Embedded UI · feature surfacing · unified admin · shared domain
**P2 (v2):** Audit/notification · unified WebSocket

## Paralel Yapılabilir
- G3, G7, G4 bağımsız — paralel dev mümkün
- G2 Faz 2 içinde, Faz 1 ile paralel değil (tenant lazım)
- G6 Faz 5'e girmeden önce tamamlanmalı
