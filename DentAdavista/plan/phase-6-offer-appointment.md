# Faz 6 — Teklif & Randevu State Machine

**Süre:** 1 gün | **Bağımlılık:** Faz 5

## Hedef
Flowchart'ın **mavi kısmı** — "Teklif Süreci". Şehir seçimi yapıldıktan sonra randevu slot'u + teklif akışı + 24 saat karar süresi.

## State Machine

```
LEAD (city=Dublin|Cork)
    │
    ▼
[appointment_slot_pick]  ← lead slot seçer
    │
    ▼
[teklif_hazırlanıyor]    ← coordinator notified
    │
    ▼
[teklif_gönderildi]      ← WA üzerinden teklif mesajı + dosya
    │
    │ (24h timer başlar)
    │
    ├── accepted    → [randevu_onaylandı] → Meet link + calendar invite (Faz 7)
    ├── declined    → [iptal] → reason capture → warm_pool
    ├── on_hold     → [beklemede] → 48h sonra auto follow-up
    └── TIMEOUT 24h → [follow_up] → "Still thinking? Any questions?"
                         │
                         └── reply yoksa → warm_pool
```

## Adımlar

### 6.1 Custom Fields (tenant-specific)
`leads` tablosu tenant-scoped custom field sistemi:
- [ ] `roadshow_city` enum: `dublin` | `cork`
- [ ] `appointment_slot` datetime (30dk slot)
- [ ] `xray_uploaded` bool + `xray_file_id`
- [ ] `offer_status` enum: `none`|`preparing`|`sent`|`accepted`|`declined`|`on_hold`
- [ ] `offer_sent_at` datetime (24h timer base)
- [ ] `deposit_status` enum: `not_requested`|`requested`|`paid`
- [ ] `flight_booked` bool
- [ ] `documents_complete` bool (passport, medical history)

### 6.2 Slot Booking
- [ ] Dublin 14 Mart + Cork 15 Mart için 20-30dk slot'ları seed et (09:00-18:00, lunch gap)
- [ ] Her slot `capacity=1`, concurrent booking lock
- [ ] Lead'e slot seçim UI — **WA interactive list message** (Meta List Template)
- [ ] Slot alındı → diğer lead'ler için unavailable
- [ ] **Önerim (onaylandı — Faz 0'da):** self-serve slot picker, çift booking önler

### 6.3 Teklif Hazırlama
- [ ] Coordinator dashboard'ında yeni tab: "Teklif bekleyen leadler"
- [ ] Coordinator teklif dosyasını upload eder (PDF) veya template'ten oluşturur
- [ ] "Send" → `offer_sent_en` HSM template + attachment (PDF)
- [ ] `offer_sent_at = now()`, 24h timer kurulur

### 6.4 Cevap İşleme
- [ ] Lead yanıt verirse → intent detector: `accept` / `decline` / `question`
- [ ] Accept → randevu_onaylandı state, Faz 7 tetikle
- [ ] Decline → reason sor, warm_pool'a taşı
- [ ] Question → Faz 3 FAQ + 24h timer pause

### 6.5 24h Timer
- [ ] Scheduled job her saat başı timeout olanları tara
- [ ] Timeout → `follow_up_24h_en` template ("Any questions about the offer?")
- [ ] Reply yok → warm_pool (Faz 8)

### 6.6 X-Ray Upload Handler (BONUS — müşteri dosyasında yoktu ama Faz 0 önerisi)
- [ ] Lead WA'da görsel gönderirse → S3'e kaydet, `xray_uploaded=true`
- [ ] Güneş: "Got it, thanks! Dr. Özge will review before your appointment."
- [ ] Dentist dashboard'ında pre-appointment X-ray view

## Deliverable
- State machine SQL + flow engine node'ları
- Slot booking UI + WA interactive message
- Coordinator teklif hazırlama sayfası

## Çıkış Kriteri
Lead Dublin seçer → slot picker → slot alır → coordinator teklif hazırlar → gönderir → lead "yes" → Meet link gelir.

## Riskler
- **WA interactive message limit:** List message max 10 item. Slot sayısı >10 ise pagination gerekli.
- **Race condition:** 2 lead aynı slot'u aynı anda seçerse → DB unique constraint + retry UX
