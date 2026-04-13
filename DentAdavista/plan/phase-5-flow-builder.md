# Faz 5 — Flow Builder (Ana Nurture Sekansı)

**Süre:** 1.5 gün | **Bağımlılık:** Faz 3, Faz 4

## Hedef
Müşteri flowchart'ının **kırmızı/siyah kısmını** Invekto flow engine'de kur: İlk mesaj → MSJ2 → MSJ3 → bağlantı kurulamazsa warm-lead havuzu.

## Akış Diyagramı (text)

```
LEAD CREATED
    │
    ▼
[NODE: Welcome Message] (Faz 3 varyant rotation)
    │
    ├── INBOUND REPLY within 1.5 days?
    │       │
    │       ├── YES → [NODE: City Detection]
    │       │           ├── Dublin/Cork seçildi → OFFER FLOW (Faz 6)
    │       │           └── Belirsiz → [NODE: Ask Again]
    │       │
    │       └── NO → [WAIT 1.5 days]
    │                   │
    │                   ▼
    │               [NODE: MSJ2 Reminder]
    │                   │
    │                   ├── REPLY? → City Detection
    │                   └── NO REPLY → [WAIT 1 day]
    │                                       │
    │                                       ▼
    │                                   [NODE: MSJ3 Final]
    │                                       │
    │                                       ├── REPLY? → City Detection
    │                                       └── NO REPLY → WARM_POOL (Faz 8 post-nurture)
    │
    └── "Not available now" keyword detected
            │
            ▼
        [NODE: Schedule Tomorrow Reminder] (24h wait)
```

## Adımlar

### 5.1 Flow Nodes Tanımla
- [ ] `welcome_send` — Faz 3 template rotation
- [ ] `wait_1_5_days` — scheduled pause, reply ile erken exit
- [ ] `msj2_reminder` — `reminder_day1_en` template
- [ ] `wait_1_day`
- [ ] `msj3_final` — `reminder_day3_en` template
- [ ] `city_detection` — intent: `location_choice` (Dublin/Cork/unclear)
- [ ] `ask_again` — "Which city suits you — Dublin or Cork?" (max 2 retry)
- [ ] `warm_pool_assign` — tag lead `nurture_cold`, Faz 8'e devret
- [ ] `availability_tomorrow` — "not today" intent → 24h sonra retry

### 5.2 Flow Validation Gate (recent commit)
- [ ] Flow build edildikten sonra validation: tüm path'ler terminal node'a ulaşıyor mu
- [ ] Dead-end yok, infinite loop yok
- [ ] Her node için error handler var

### 5.3 Reply Handler (inbound interrupt)
Herhangi bir wait node'unda lead yanıt verirse:
- [ ] Wait iptal, flow ilgili branch'e atla
- [ ] FAQ intent tetiklenirse → Faz 3 FAQ cevabı + flow'a geri dön (context preserve)

### 5.4 Timezone Awareness
- [ ] Lead ülke bazlı timezone tahmini (phone +353 → Europe/Dublin)
- [ ] Mesaj gönderim penceresi: 08:00 – 21:00 local time (gece göndermeyi engelle)
- [ ] Wait bitişi gece denk gelirse → sabah 08:00'a kay

### 5.5 Monitoring
- [ ] Her node execution log'a yazılsın (`flow_execution_log` tablosu)
- [ ] Dashboard widget: aktif flow'lar, wait'te olanlar, stuck lead'ler

## Deliverable
- Flow JSON spec `DentAdavista/plan/flow-main-nurture.json`
- Flow validation test PASS
- 5 senaryo simülasyonu (reply/no-reply/not-available/wrong-city/happy-path)

## Çıkış Kriteri
Test lead → welcome → no reply 1.5 day (hızlandırılmış sim) → msj2 → reply "Dublin" → offer flow'a geçiş.

## Riskler
- **Scheduled job drift:** Background scheduler'ın 1.5 gün sonra ateşlemesi için job queue güvenilir olmalı (Hangfire/Quartz hangisi kullanılıyorsa sağlamlaştır)
- **Reply race condition:** Lead wait'teyken yanıt verir + scheduled msj2 aynı anda atar → duplicate mesaj. Lock gerekli.
