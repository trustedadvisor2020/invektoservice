# PKT-6C3: Marketing v2

> **Durum:** DONE | **Tarih:** 2026-02-18 | **Codex:** iter 2, FORCE PASS

## GR Listesi

- **GR-3.24 Proactive Review Rescue:** review_risks + rescue_templates, risk score 0-100, 5 rescue strategy
- **GR-3.25 Multilingual Medical Tourism:** treatment_catalog + tourism_conversations, Claude Haiku multilingual

## GR Detail

### GR-3.24: Proactive Review Rescue
- 3.24.1 Sentiment bazlı risk skoru (LOW/MEDIUM/HIGH/CRITICAL)
- 3.24.2 Keyword algılama: "iade", "şikayet", "yorum yazacağım"
- 3.24.3 Risk seviyesine göre otomatik aksiyon (uyarı → özür → supervisor alert)
- 3.24.4 Kurtarma stratejileri (özür, indirim, ücretsiz kargo, değişim, tam iade)
- 3.24.5 Follow-up: T+24h memnuniyet → T+48h değerlendirme ricası
- 3.24.6 Kurtarma dashboard
- DB: review_risks, rescue_templates

### GR-3.25: Multilingual Medical Tourism
- 3.25.1 Language Router: dil algılama → pipeline yönlendirme
- 3.25.2 Kültürel uyum (Arapça: resmi, İngilizce: rahat, Rusça: detaylı, Almanca: formal)
- 3.25.3 Medical Tourism Engine: intent + entity çıkarma, katalog, döviz
- 3.25.4 Klinik personel görünümü: orijinal + Türkçe çeviri + AI cevabı
- 3.25.5 7/24 otomatik yanıt (gece/tatil/mesai dışı)
- 3.25.6 Diller: EN + AR (MVP), RU + DE (sonra)
- DB: treatment_catalog, tourism_conversations

## Deliverables

- Review Rescue: Risk CRUD + stats, Template CRUD + deactivate (8 endpoint)
- Tourism: Catalog CRUD, Conversation CRUD, Respond (Claude), Stats (8 endpoint)
- TourismResponseGenerator: Claude Haiku multilingual response
- Backend proxy: 16 yeni route + MarketingProxyDelete helper
- 9 dosya +2012
- DB: review_risks, rescue_templates, treatment_catalog, tourism_conversations
- Error codes: INV-MK-011~023

## Codex Notlari

- iter 0: CQ5 FAIL - generic `catch(Exception ex)` in TourismResponseGenerator
- iter 1: CQ5 FAIL - ParseResponse kalan generic catch
- iter 2: ALL CQ1-CQ8 PASS. Fix: typed catches (HttpRequestException + JsonException)

## Plan

`arch/plans/20260218-pkt6c3-marketing-v2.json`
