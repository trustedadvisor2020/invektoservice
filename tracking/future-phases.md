# Gelecek Fazlar (Phase 4 ~ Phase 7)

> Uzak gelecek fazları. Detaylar ihtiyaç olduğunda güncellenecek.
> Kaynak: ideas/phases/phase-4.md ~ phase-7.md (artık silinmiş)

---

## Phase 4: Enterprise Altyapı (Hafta 25-32)

> **MRR:** 800K-1.2M TL | **Müşteri:** 130+ | **9 GR**
> **Yeni Servis:** Invekto.Audit (port 7103)

### GR Listesi

| GR | Ad | Açıklama |
|----|-----|----------|
| GR-4.1 | SSO / 2FA Genişletme | Google/Microsoft OIDC, TOTP 2FA, session mgmt, IP/country allowlist |
| GR-4.2 | Audit Service | Append-only event store (port 7103), 7 kritik event tipi, search API |
| GR-4.3 | PII Koruma | TC/telefon/IBAN/email detector, maskeleme, export redaction |
| GR-4.4 | Guardrails | Banned phrases, PII prevention, AI audit log, escalation notes |
| GR-4.5 | Admin Panel | Tenant yönetimi, security policy, audit viewer, PII ayarları |
| GR-4.6 | QA & Mining Hazırlık | Conversation metadata genişletme, script compliance, haftalık rapor |
| GR-4.7 | Sağlık Enterprise | SLA tracking, hasta verisi audit, advanced analytics, multi-şube |
| GR-4.8 | Sigorta Provizyon | Sigorta sorusu intent, poliçe bilgi toplama, kapsam bilgisi (v6: SB-02) |
| GR-4.9 | Compliance Tam | Veri erişim hakkı raporu, cascade silme, GDPR DPA, denetim modu (v6: CS-08) |

### DB Tablolar (Planlanan)
- sessions, login_attempts, tenant_policies (SSO/2FA)
- audit_events, retention_policies (Audit)

### Çıkış Kriterleri
- 1+ kurumsal müşteri SSO ile bağlı
- Audit log çalışıyor, PII maskeleme aktif
- Diş: 10+ klinik, no-show <%10, fiyat→randevu %35+
- Estetik: 10+ klinik, lead→randevu %35+

---

## Phase 5: Revenue Agent (Hafta 33-40)

> **MRR:** 1.2-2M TL | **Müşteri:** 170+ | **8 GR**

### GR Listesi

| GR | Ad | Açıklama |
|----|-----|----------|
| GR-5.1 | Revenue Agent — Lead Katmanı | Lead qualification, offer/appointment, payment link (iyzico/PayTR) |
| GR-5.2 | Revenue Agent — Satış Katmanı | Product recommendation, bundle/upsell/cross-sell, margin awareness |
| GR-5.3 | Ürün Kataloğu | Product import, marj tier, bundle rules, stok sync |
| GR-5.4 | Abandoned Cart Recovery | Sepet terk tespiti, T+2h/T+24h trigger, kişiselleştirilmiş mesaj |
| GR-5.5 | Sipariş Sonrası Proaktif Satış | T+3gün memnuniyet → cross-sell / iade çevirme |
| GR-5.6 | Arapça Dil + Medikal Turizm AR | 3. dil (AR), RTL display, Arapça medikal turizm template |
| GR-5.7 | Abonelik / Üyelik Modeli | Frekans analizi → abonelik teklifi, 6 sektör genelinde (v6: S11) |
| GR-5.8 | Churn Prevention / Win-back | Sektöre özel churn sinyal, T+30/60/90 win-back kampanya (v6: S12) |

### Fiyatlandırma Tier (Bu Phase'ten)
- **E-ticaret:** Starter 3K → Growth 7.5K → Pro 12K → Enterprise 15K+ TL/ay
- **Sağlık:** Klinik 7.5K → Klinik Pro 15K → Medikal Turizm 25K+ TL/ay

### DB Tablolar (Planlanan)
- lead_appointments, payment_links (Revenue Agent)
- products, bundle_rules, recommendation_log (Katalog)
- abandoned_carts (Cart Recovery)
- post_purchase_triggers (Proaktif Satış)
- subscriptions, subscription_plans (Abonelik)
- churn_risks, winback_campaigns (Churn)

### Çıkış Kriterleri
- Revenue Agent 5+ tenant'ta aktif satış yapıyor
- Abandoned cart recovery çalışıyor
- 3 dil destekleniyor (TR/EN/AR)
- Premium tier 3+ müşteriye satılmış
- MRR 1.2M+ TL

---

## Phase 6: QA + Conversation Mining (Hafta 41-48)

> **MRR:** 2M+ TL | **Müşteri:** 200+ | **5 GR**

### GR Listesi

| GR | Ad | Açıklama |
|----|-----|----------|
| GR-6.1 | SLA Tracker (tam) | Tam SLA engine, tenant bazlı hedefler, breach alerts, niche-özel SLA |
| GR-6.2 | QA Scoring | AI temsilci değerlendirme, 5 skor kriteri, coaching insights |
| GR-6.3 | Conversation Mining | Win/loss phrase, complaint drivers, conversion patterns, trend analiz |
| GR-6.4 | Knowledge Gap Report | Top 50 unanswered, "doc ekle" 1-tık, AI accuracy trend |
| GR-6.5 | Revenue Attribution Dashboard | Kanal ROI, agent performans, AI vs Human, niche-özel dashboard |

### DB Tablolar (Planlanan)
- sla_configs, sla_breaches (SLA)
- qa_scores, qa_coaching_insights (QA)
- mining_insights, mining_digests (Mining)
- knowledge_gaps (Knowledge Gap)

### Çıkış Kriterleri
- SLA compliance %90+
- QA skor ortalaması %75+
- Knowledge gap close rate %60+
- 5+ actionable insight/ay
- MRR 2M+ TL

---

## Phase 7: Genişleme (Hafta 49+)

> **MRR:** 2M++ TL | **Müşteri:** 200++ | **7 GR**

### GR Listesi

| GR | Ad | Açıklama |
|----|-----|----------|
| GR-7.1 | Mobil Uygulama | iOS + Android (React Native/Flutter), push notification, agent assist |
| GR-7.2 | Yeni Kanal Entegrasyonları | Shopify, Amazon TR, Google Business Messages, Apple Business Chat |
| GR-7.3 | Voice & Video | Voice transcription (Whisper), video call (medikal konsültasyon) |
| GR-7.4 | Predictive Analytics | Churn prediction, lead scoring, best send-time, demand forecasting |
| GR-7.5 | Global Pazar | Multi-currency, timezone-aware, yeni diller (RU, DE), GDPR scanner |
| GR-7.6 | QR Kod Hızlı Erişim | QR jeneratör, pre-filled WA, konum routing, analytics (v6: M6) |
| GR-7.7 | Çevrimdışı Mod | Offline mesaj kuyruğu, cache, taslak, sync (v6: M7) |

### Çıkış Kriterleri
- 200++ aktif müşteri
- MRR 2M++ TL
- Mobil uygulama yayında
- Yeni kanal(lar) aktif
- Predictive analytics pilot
- Global altyapı hazır
