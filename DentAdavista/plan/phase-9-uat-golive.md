# Faz 9 — UAT + Go-Live

**Süre:** 1 gün | **Bağımlılık:** Faz 1-8 tamamlanmalı

## Hedef
Gerçek müşteri trafiğini açmadan önce end-to-end senaryo testleri, müşteri eğitimi, ve aşamalı canlıya alma.

## Adımlar

### 9.1 Test Senaryoları (20 adet)

**Happy Path (5):**
- [ ] S1: Landing form → WA welcome → reply "Dublin" → slot → offer → accept → Meet
- [ ] S2: Aynı, Cork ile
- [ ] S3: WA direct (reklam CTA) → welcome → happy path
- [ ] S4: X-ray upload orta akışta → Güneş acknowledge
- [ ] S5: Question asked mid-flow → FAQ answer → flow continues

**Edge Cases (10):**
- [ ] S6: Welcome no reply 1.5d → MSJ2 no reply 1d → MSJ3 no reply → warm_pool
- [ ] S7: "Not available today" → tomorrow reminder
- [ ] S8: Offer 24h no response → follow_up
- [ ] S9: Offer decline with reason → warm_pool
- [ ] S10: Duplicate lead (aynı phone 2 kez)
- [ ] S11: STOP keyword → opt_out
- [ ] S12: Turkish inbound message → TR fallback translation
- [ ] S13: FAQ `price_quote` 3 kez → fiyat listesi + call-back
- [ ] S14: FAQ `safety_concern` → social proof inject
- [ ] S15: Slot concurrent booking race

**FAQ Accuracy (5):**
- [ ] S16-20: 12 intent × 2 paraphrase, doğru cevap varyantı, rotation çalışıyor

### 9.2 Müşteri Eğitimi
- [ ] 15dk screencast: coordinator dashboard kullanımı (teklif hazırlama, offer send, manual override)
- [ ] 10dk screencast: Güneş'in AI davranışı nasıl supervise edilir (human handoff, intent log inceleme)
- [ ] FAQ cheatsheet: Güneş anlayamadığında ne yapacak (escalation)
- [ ] Canlı kickoff call: 30dk Q&A

### 9.3 Monitoring & Alerting Setup
- [ ] WAA webhook error alert (Slack/email)
- [ ] Flow stuck lead alert (1 hafta flow'da olan)
- [ ] LLM cost günlük limit alarm
- [ ] Intent confidence düşüşü (anomali detection)

### 9.4 Aşamalı Canlıya Alma
- [ ] **Stage 1 (Soft launch, 2 gün):** Sadece kliniğin test numaralarına izin, gerçek lead kabul etmez
- [ ] **Stage 2 (Limited beta, 3 gün):** İlk 20 gerçek lead kabul et, her biri manuel incele
- [ ] **Stage 3 (Full):** Landing page + reklam trafiği açık

### 9.5 Success Metrics (ilk 30 gün)
- Welcome → reply rate: hedef >40%
- Reply → slot booked: hedef >50%
- Slot → offer accept: hedef >60%
- Offer → Meet attended: hedef >80%
- Warm pool → recovery (day 3/7/14): hedef >10%
- Güneş auto-resolve rate (human handoff gerekmeden): hedef >75%

### 9.6 Post-Launch Retrospective (Go-live + 14 gün)
- [ ] Metrik review
- [ ] Lesson learned → `arch/lessons-learned.md`
- [ ] Öğrenilen pattern → skill/evolve adayları
- [ ] V2 backlog: IG + Email + Telegram + Twitter kanalları

## Deliverable
- 20 senaryo geçti ✅
- Müşteri eğitildi ✅
- Monitoring aktif ✅
- Production canlı ✅

## Çıkış Kriteri
Stage 3'te 48 saat sorunsuz trafik + ilk 5 gerçek lead başarılı onboarding.

## Riskler
- **Beklenmeyen müşteri mesajı:** AI agent anlamayacağı soru alırsa coordinator'a hızlı handoff kritik
- **Meta rate limit:** Stage 3'te aniden yüksek trafik → tier upgrade talebi Meta'ya önceden aç
- **Dentist bandwidth:** Dr. Özge günlük kaç Meet kaldırabilir? → slot availability'yi ona göre ayarla
