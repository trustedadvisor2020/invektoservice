# Faz 4 — Lead Intake (Müşteri Landing + WA)

**Süre:** 1 gün | **Bağımlılık:** Faz 2

## Hedef
İki lead kaynağı entegre edilecek:
1. **Müşterinin mevcut landing page formu** → Invekto webhook
2. **WhatsApp'a doğrudan mesaj** (reklam CTA'sı → wa.me linki) → inbound trigger

## Adımlar

### 4.1 Landing Page Webhook (müşterinin sayfası)
- [ ] Müşteriden landing page form field listesi al (Faz 0 çıktısı)
- [ ] Invekto side: `POST /api/tenants/dentadavista/leads/webhook` endpoint aç
  - Auth: pre-shared API key
  - Body: `{ name, phone, email, city_preference, country, source }`
- [ ] Müşterinin form backend'ine webhook çağrısı eklettir (müşteri dev'i veya bizim küçük JS snippet)
- [ ] Yanıt: `{ lead_id, status:"created" }`
- [ ] Landing → webhook başarı sonrası kullanıcıya "We'll message you on WhatsApp shortly 👋" ekranı

### 4.2 Lead Normalizasyon
- [ ] Phone: E.164 format'a zorla (`+353...` IE, `+44...` UK vb.)
- [ ] Duplicate check: aynı phone son 30 gün içinde varsa → mevcut lead'e merge, yeni flow start ETME
- [ ] Source tag: `landing_page_main`, `landing_page_ads`, `whatsapp_direct`, `instagram_ads_cta`

### 4.3 WhatsApp Direct Entry
- [ ] `wa.me/{dentadavista_number}?text=Hi+I'm+interested+in+the+Roadshow` — reklamlarda kullanılacak
- [ ] Inbound mesaj → eğer lead yoksa otomatik create (phone only, name WA profile'dan)
- [ ] Welcome flow otomatik tetikle (Faz 5)

### 4.4 Meta Lead Ads Opsiyonu (v2 — şimdilik SKIP)
- Not: Müşterinin landing page'i varken Meta Lead Ads direkt entegrasyon opsiyonel. Faz 2 backlog.

### 4.5 GDPR / Consent
- [ ] Lead create sırasında `consent_marketing` flag (landing form'da opt-in checkbox olmalı)
- [ ] Opt-out her WA mesajın altında footer: "Reply STOP to opt out"
- [ ] Opt-out → nurture flow durur, sadece transactional (offer/appointment) devam

## Deliverable
- Webhook endpoint canlı, Postman test PASS
- Müşteri landing formundan test lead → Invekto'da görünüyor
- WA direct message → lead otomatik create

## Çıkış Kriteri
End-to-end: landing form doldur → 60sn içinde WA'da welcome mesajı gelsin.

## Riskler
- **Müşteri landing page backend erişimi:** Müşteri kendi dev'i ile webhook eklemezse JS snippet ile client-side alternatif (ama IP bazlı bot riski)
- **Duplicate key:** Aynı kişi hem landing hem WA'dan geldiğinde merge logic kritik
