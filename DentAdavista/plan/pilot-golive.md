# Dent Adavista — UAT & Go-Live

> **Pilot-specific test senaryolari + launch plan.** Generic feature'lar icin test kriterleri feature spec'lerinde AC-* satirlarinda.

## UAT Test Senaryolari (20)

### Happy Path (5)
- [ ] S1: Landing form -> WA welcome -> reply "Dublin" -> slot -> offer -> accept -> Meet
- [ ] S2: Ayni, Cork ile
- [ ] S3: WA direct (reklam CTA) -> welcome -> happy path
- [ ] S4: X-ray upload orta akista -> Gunes acknowledge
- [ ] S5: FAQ question mid-flow -> cevap -> flow continues

### Edge Cases (10)
- [ ] S6: Welcome no reply 1.5d -> MSJ2 no reply 1d -> MSJ3 no reply -> warm_pool
- [ ] S7: "Not available today" -> tomorrow reminder
- [ ] S8: Offer 24h no response -> follow_up
- [ ] S9: Offer decline with reason -> warm_pool
- [ ] S10: Duplicate lead (ayni phone 2 kez, 30g window)
- [ ] S11: STOP keyword -> opt_out
- [ ] S12: Turkish inbound message -> TR fallback / translation
- [ ] S13: FAQ `price_quote` 3 kez -> fiyat listesi + call-back
- [ ] S14: FAQ `safety_concern` -> social proof inject
- [ ] S15: Slot concurrent booking race (2 lead ayni slot)

### FAQ Accuracy (5)
- [ ] S16-20: 12 intent x 2 paraphrase -> dogru cevap varyanti, rotation calisiyor

## Asamali Canlıya Alma

- [ ] **Stage 1 (Soft launch, 2 gun):** Sadece klinigin test numaralari kabul; gercek lead reddedilir
- [ ] **Stage 2 (Limited beta, 3 gun):** Ilk 20 gercek lead kabul; her biri manuel inceleme
- [ ] **Stage 3 (Full):** Landing page + reklam trafigi acik

## Success Metrics (ilk 30 gun)

| Metrik | Hedef |
|--------|-------|
| Welcome -> reply rate | >40% |
| Reply -> slot booked | >50% |
| Slot -> offer accept | >60% |
| Offer -> Meet attended | >80% |
| Warm pool -> recovery (day 3/7/14) | >10% |
| Agent auto-resolve (no handoff) | >75% |

## Monitoring & Alerting

- [ ] WAA webhook error alert (Slack/email)
- [ ] Flow stuck lead alert (1 hafta flow'da)
- [ ] LLM cost gunluk limit alarmi
- [ ] Intent confidence dusus (anomaly detection)

## Musteri Egitimi Deliverables

- [ ] 15dk screencast: coordinator dashboard (teklif hazirlama, offer send, manual override)
- [ ] 10dk screencast: AI agent supervise (handoff, intent log)
- [ ] FAQ cheatsheet: Gunes anlayamadiginda ne yapacak (escalation karti)
- [ ] 30dk kickoff call Q&A

## Post-Launch Retrospective (Go-live + 14 gun)

- [ ] Metrik review vs hedef
- [ ] Lesson learned -> `arch/lessons-learned.md`
- [ ] Ogrenilen pattern -> skill/evolve adaylari
- [ ] V2 backlog: IG + Telegram kanal aktif, yeni campaign slug'lari

## Riskler

- **Beklenmeyen musteri mesaji:** AI anlamayacagi soru alirsa coordinator'a hizli handoff kritik
- **Meta rate limit:** Stage 3 ani trafik -> tier upgrade talebi Meta'ya onceden
- **Dentist bandwidth:** Dr. Ozge gunluk Meet kapasitesi -> slot availability ayari
- **INMA uptime:** SLA bilinmiyor -> monitoring + retry kuyrugu
