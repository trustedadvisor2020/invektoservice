# Faz 0 — Invekto Capability Audit (Dent Adavista Pilot)

**Tarih:** 2026-04-13 | **Mod:** READ-ONLY

## Capability Matrisi

| # | Capability | Durum | Kanıt |
|---|-----------|-------|-------|
| 1 | INMA integration | ✅ YES | `src/Invekto.ChatAnalysis/Services/WapCrmClient.cs` — `X-CIB-SecretKey` auth, `messagelistforphone` endpoint. Commit `a21a928` IP whitelist fix. Şu an READONLY pattern — **yazma (send) endpoint'i için Q onayı gerek** |
| 2 | WAA servisi | ⚠️ PARTIAL | `src/Invekto.WhatsAppAnalytics/` — INMA bağlı, Meta direct YOK. Commit `424013c` DI fresh |
| 3 | Multi-channel adapter | ❌ NO | `IMessageChannel` abstraction yok, single-path INMA |
| 4 | Intent Detector | ✅ YES (LLM) | `IntentDetector.cs` Claude Haiku real. `MockIntentDetector.cs` TR-aware. EN intent array hazır. **Variant rotation YOK** |
| 5 | Flow Builder + validation | ✅ YES | `FlowValidator.cs`, commit `878c358`. Node tipleri: `message_text`, `ai_intent`, `ai_faq`, `message_menu`, `action_api_call`, `action_delay`, `logic_condition/switch`, `action_handoff`. **`action_delay` state ephemeral (restart = kayıp)** |
| 6 | Scheduled jobs | ⚠️ PARTIAL | `ReminderSchedulerService.cs` — custom `System.Timer`, 300s interval. Hangfire/Quartz YOK. **Process crash = job kaybı** |
| 7 | Tenant custom fields | ❌ NO | `leads` hardcoded kolonlar. JSON/schemaless mekanizma yok |
| 8 | File upload (media) | ⚠️ PARTIAL | Abstraction yok, X-ray için S3/Blob gerekli |
| 9 | Template system | ⚠️ PARTIAL | `TemplateSubstitution.cs` `{{var}}` OK, EN lang prop OK. **A/B rotation seed logic YOK** |

**Özet:** 4 FULL · 4 PARTIAL · 2 NO (toplam 10 madde — 8 orijinal + 2 alt gap)

## Gap List (Pilot İçin Yapılması Gerekenler)

| # | Gap | Efor | Pilot Bloklayıcı? | Fazda Nerede |
|---|-----|------|-------------------|--------------|
| G1 | INMA **yazma** endpoint aç (send message) | S (1g) | 🔴 EVET | Faz 2 |
| G2 | Multi-channel adapter pattern (`IMessageChannel`) | M (3-5g) | 🟡 v2'ye ertele — pilot INMA-only | Faz 2 (minimal) |
| G3 | Template A/B rotation seed | S (1-2g) | 🔴 EVET (46 varyant kritik) | Faz 3 |
| G4 | Tenant custom fields (`leads.custom_jsonb`) | L (5-7g) | 🟡 ŞART DEĞİL — hardcoded field'lar eklenir | Faz 6 |
| G5 | Media upload persistent (S3/Blob) | M (3-4g) | 🟡 v2'ye ertele — X-ray Faz 6 bonus idi | Faz 6 |
| G6 | Flow execution state persistence | M (2-3g) | 🔴 EVET (1.5g wait kritik) | Faz 5 |
| G7 | Hangfire/Quartz migration | L (5-7g) | 🟡 Pilot süre kısaysa Timer yeterli + cron redundancy | Faz 5 |
| G8 | EN template locale test | S (<1g) | 🔴 EVET | Faz 3 |

**Pilot kritik path (🔴):** G1 + G3 + G6 + G8 = ~5-7 iş günü ek iş.

## Kararlar (Q'ya sorular)

### Karar 1 — Custom Fields (G4)
Tenant-bazlı dinamik field mi, yoksa Dent Adavista için `leads` tablosuna doğrudan kolon mu ekleyelim?

- **A)** Jenerik `custom_jsonb` column (5-7 gün, gelecek tüm tenantlar için)
- **B)** Dent'e özel kolonlar: `roadshow_city`, `appointment_slot`, `offer_status`, `deposit_status`, `flight_booked` (1 gün, teknik borç olarak kalır)
- **Önerim:** **B** — pilot için hız, sonra G4 ayrı paket

### Karar 2 — Flow State Persistence (G6)
1.5 gün wait kritik. Mevcut FlowEngineV2 memory-based.

- **A)** Hemen persistent state ekle (`flow_execution_state` tablosu) — 2-3 gün
- **B)** Wait node yerine **dış scheduler job** kullan (job `flow_resume` callback'i tetikler) — 1 gün, teknik temiz
- **Önerim:** **B** — Faz 6'da Hangfire'a geçilince zaten birleşir

### Karar 3 — Scheduler (G7)
- **A)** Hangfire migration şimdi (5-7 gün) — production-grade
- **B)** Timer + PostgreSQL advisory lock + cron fallback — 1 gün, pilot için yeterli
- **Önerim:** **B**, Hangfire v2

### Karar 4 — Multi-channel abstraction (G2)
- **A)** `IMessageChannel` interface + Inma impl şimdi (3-5 gün)
- **B)** Direct INMA call, refactor v2'de
- **Önerim:** **A minimal versiyonu (1 gün interface + 1 impl)** — gelecekte kolay

### Karar 5 — INMA yazma endpoint'i (G1)
Memory kuralı: "INMA READONLY" — lisans için. Mesajlaşma için INMA'nın **outbound send API**'si var mı? Swagger'da doğrulanacak.
- **A)** INMA outbound send destekliyor → kural genişlet (mesajlaşma = yazma OK, lisans hâlâ readonly)
- **B)** INMA send etmiyor → Meta direct adapter lazım (Faz 2 büyür)

**Swagger'ı senin için kontrol edeyim mi, yoksa INMA tarafından sen bilgi mi vereceksin?**

## Revize Efor

| | İlk tahmin | Revize |
|---|-----------|--------|
| Faz 0-9 | 9 gün | **12-14 gün** (G1+G3+G6+G8 eklendi) |

## Sıradaki

Yukarıdaki **5 karar** cevaplandıktan sonra:
- Faz 0 ✅ kapat
- Faz 1 (tenant provisioning) başla
- Paralel: INMA Swagger doğrulama (Karar 5 cevabına göre)
