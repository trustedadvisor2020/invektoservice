# Faz 8 — Post-Roadshow Nurture (Öneri #5)

**Süre:** 0.5 gün | **Bağımlılık:** Faz 5

## Hedef
Roadshow sonrası "henüz karar vermedim" lead'leri için 3/7/14 gün drip sekansı. Müşteri flowchart'ında YOK — Invekto'nun eklediği strategic layer. Lead-to-patient conversion ~3x.

## Tetikleyiciler (warm_pool'a giriş)
1. Welcome/MSJ2/MSJ3 hiçbiri yanıt almadı (Faz 5'ten)
2. Offer `declined` + reason "düşünüyorum" (Faz 6'dan)
3. Appointment tamamlandı ama treatment rezervasyonu yok (Faz 7'den)
4. Offer `on_hold` state 7+ gün

## Drip Sekansı

```
WARM_POOL ENTRY
    │
    ▼
[WAIT 3 days] → [post_event_day3_en]
    │           "Hi {{name}}, just checking — any questions after the Roadshow?"
    │           + social proof (son patient testimonial)
    │
    ▼
[WAIT 4 days (day 7)] → [post_event_day7_en]
    │                    "We still have spots next month — interested?"
    │                    + before/after Instagram link
    │
    ▼
[WAIT 7 days (day 14)] → [post_event_day14_en]
    │                     "Here's our full treatment guide 📎"
    │                     + PDF guide attach
    │
    ▼
[ARCHIVE] → long_term_list (monthly newsletter opt-in)
```

## Adımlar

### 8.1 Content Hazırlama (müşteri input)
- [ ] Day 3 mesaj: sıcak check-in + 1 kısa testimonial
- [ ] Day 7 mesaj: alternatif tarih/sonraki roadshow bilgisi (varsa)
- [ ] Day 14 mesaj: "Treatment Guide" PDF (müşteri hazırlayacak, yoksa biz template)

### 8.2 HSM Template Onayı
- [ ] 3 template Meta'ya submit (Faz 2 ile koordine)
- [ ] Utility category tercih et (marketing reject riskini düşürür)

### 8.3 Exit Conditions
Herhangi bir drip aşamasında:
- [ ] Lead yanıtlarsa → nurture pause, normal flow'a al
- [ ] STOP/UNSUBSCRIBE → opt_out, nurture kapat
- [ ] Lead booking yaparsa → nurture kapat (success)

### 8.4 A/B Test Setup (BONUS)
- [ ] Warm pool'un %50'sine drip, %50'sine gönderme (control)
- [ ] 30 gün sonra conversion farkı ölç → önerinin ROI'si

### 8.5 Analytics
- [ ] Her drip mesajı için open/reply rate
- [ ] Warm → booked conversion funnel
- [ ] Day 3/7/14 hangi en yüksek converting

## Deliverable
- 3 HSM template onaylı
- Drip flow JSON: `DentAdavista/plan/flow-post-nurture.json`
- A/B control grup setup

## Çıkış Kriteri
Test lead warm_pool'a al → 3 gün hızlandırılmış sim → day 3 mesajı gönderilsin → reply → nurture kapansın.

## Riskler
- **Meta 24h window:** Post-nurture 24h session dışı → HSM zorunlu. Template reject olursa fallback: free-text yerine emoji + kısa prompt
- **Marketing fatigue:** 3 mesaj çok mu? A/B test ile doğrula. Cap: max 3 mesaj, opt-out her mesajda
