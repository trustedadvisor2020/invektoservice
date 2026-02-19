# PKT-6C2: Niche Marketing

> **Durum:** DONE | **Tarih:** 2026-02-17 | **Codex:** iter 3, PASS
> **Commit:** aa2ca9b

## GR Listesi

- **GR-3.21 Google Yorum + Referans Motoru:** Review request tracking, crypto-random referral codes
- **GR-3.22 Medikal Turizm Lead Capture:** International patient pipeline (country, lang, treatment, budget)

## GR Detail

### GR-3.21: Google Yorum + Referans Motoru
- 3.21.1 Tedavi başarılı + memnun → yorum rica mesajı
- 3.21.2 Google Maps review link gönder
- 3.21.3 Referans kodu üret → "Arkadaşınıza %10 indirim"
- 3.21.4 Referral tracking (kim kimi getirdi)
- 3.21.5 Yorum oranı dashboard (%3 → %15+ hedef)
- DB: referrals, review_requests

### GR-3.22: Medikal Turizm Lead Yönetimi
- 3.22.1 Yabancı hasta akışı: fiyat + konaklama + transfer
- 3.22.2 Döviz bazlı fiyatlandırma (EUR/USD/GBP)
- 3.22.3 Consultation booking (online muayene slot)
- 3.22.4 Multi-language follow-up (TR/EN)
- DB: medical_tourism_leads

## Deliverables

- Yeni **Invekto.Marketing** servisi (port 7112)
- MarketingRepository: 3 tablo CRUD
- 16 API endpoint: Reviews (5), Referrals (4), Tourism Leads (5), Health/Ops (2)
- Backend proxy: MarketingClient + 14 route mappings
- 13 dosya +2039
- DB: review_requests, referrals, medical_tourism_leads

## Split Notu

GR-3.24 + GR-3.25 -> PKT-6C3'e tasindi

## Plan

`arch/plans/20260217-pkt6c2-niche-marketing.json`
