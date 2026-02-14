# Phase 5 — Revenue Agent: Satış Yapan AI + Ölçek

> **Hafta:** 33-40
> **MRR Hedefi:** 1.2-2M TL
> **Müşteri Hedefi:** 170+
> **Bağımlılık:** Phase 4 tamamlanmış olmalı
> **Durum:** ⬜ Başlamadı
>
> **v4.2 Optimizasyon (2026-02-15):** 3 GR Phase 3'e taşındı (tedavi takip → GR-3.20,
> yorum/referans → GR-3.21, attribution → GR-3.14). Medikal turizm bölündü (TR/EN → Phase 3
> GR-3.22, AR → burada GR-5.6). Phase 5 artık 6 GR (9'dan düştü).

---

## Durum Takibi

| Alt Gereksinim | Durum | Tamamlanma Tarihi | Notlar |
|----------------|-------|-------------------|--------|
| GR-5.1 Revenue Agent — Lead Katmanı | ⬜ Başlamadı | — | — |
| GR-5.2 Revenue Agent — Satış Katmanı | ⬜ Başlamadı | — | — |
| GR-5.3 Ürün Kataloğu | ⬜ Başlamadı | — | — |
| GR-5.4 Abandoned Cart Recovery | ⬜ Başlamadı | — | — |
| GR-5.5 Sipariş Sonrası Proaktif Satış (S4) | ⬜ Başlamadı | — | — |
| ~~GR-5.6 Click-to-WhatsApp Attribution~~ | ➡️ Phase 3 | — | Phase 3 GR-3.14'e birleştirildi (v4.2) |
| ~~GR-5.7 Tedavi Sonrası Takip (S8)~~ | ➡️ Phase 3 | — | Phase 3 GR-3.20'ye taşındı (v4.2) |
| ~~GR-5.8 Google Yorum + Referans Motoru (S10)~~ | ➡️ Phase 3 | — | Phase 3 GR-3.21'e taşındı (v4.2) |
| GR-5.6 Arapça Dil + Medikal Turizm AR Genişleme | ⬜ Başlamadı | — | ← eski GR-5.9 (v4.2, TR/EN kısmı Phase 3 GR-3.22'ye taşındı) |

---

## Özet

"AI sadece soruları cevaplamıyor, satış da yapıyor." Support AI → Revenue Agent dönüşümü. Premium tier yaratılabilir.

**Satış dili:** "Lead'den ödemeye kadar AI satış yapar — ürün önerir, teklif verir, randevu alır"

| | Support AI (Phase 1-4) | Revenue Agent (Phase 5) |
|---|---|---|
| Müşteri sorar | Cevap verir | **Proaktif ürün önerir** |
| Ürün bilgisi | Yok | Katalog + stok + marj biliyor |
| Upsell | Yok | "Bu paketi alırsanız %20 tasarruf" |
| Sepet terk | Yok | Otomatik hatırlatma |
| Lead | Yok | Skorlama + randevu + ödeme |

---

## Gereksinimler

### GR-5.1: Revenue Agent — Lead Katmanı

> **Servis:** `AgentAI` genişleme
> **Sektör:** Tümü

- [ ] **5.1.1** Lead qualification script
  - Bütçe / termin / ürün ihtiyacı soruları
  - AI konuşma akışından otomatik bilgi toplama
  - Lead score (0-100 + nedenleri)
- [ ] **5.1.2** Offer / Appointment
  - Internal slot engine (basit takvim API)
  - Proposal message templates (teklif + şartlar + geçerlilik süresi)
  - Follow-up otomasyonu (T+24h teklif hatırlatma)
- [ ] **5.1.3** Payment link
  - iyzico veya PayTR link oluşturma (Q kararı)
  - Ödeme geldi → otomatik teyit mesajı
- [ ] **5.1.4** DB:
  ```sql
  lead_appointments (id, tenant_id, lead_id, slot_start, slot_end, status, created_at)
  payment_links (id, tenant_id, lead_id, provider, amount, currency, external_link_id, status, created_at)
  ```

---

### GR-5.2: Revenue Agent — Satış Katmanı

> **Servis:** `AgentAI` + `Integrations`
> **Sektör:** E-ticaret ağırlıklı

- [ ] **5.2.1** Product Recommendation Engine
  - Konuşma context'inden ürün/hizmet eşleştirme
  - Müşteri geçmişine göre kişisel öneri
  - "Bunu alanlar şunu da aldı" basit collaborative filtering
- [ ] **5.2.2** Bundle / Upsell / Cross-sell Rules
  - Tenant tanımlı kurallar (X+Y = %10 indirim)
  - Upsell tetikleyicileri (düşük paket → yüksek paket öner)
  - Cross-sell (ana ürün + aksesuar/hizmet)
- [ ] **5.2.3** Margin Awareness
  - Ürün marj bilgisi (düşük/orta/yüksek tier)
  - Düşük marjlı ürün → yüksek marjlı alternatif sun
  - İndirim limitleri (agent max %X indirim yapabilir)
- [ ] **5.2.4** Inventory-Aware Suggestions
  - Stok durumu kontrolü (Integrations'tan)
  - Stokta yoksa alternatif öner
  - Düşük stokta "son X adet" urgency mesajı

---

### GR-5.3: Ürün Kataloğu

> **Servis:** `Integrations` genişleme
> **Sektör:** E-ticaret

- [ ] **5.3.1** Product import (CSV / Trendyol sync)
- [ ] **5.3.2** Marj tier atama (düşük/orta/yüksek)
- [ ] **5.3.3** Bundle rule tanımlama UI
- [ ] **5.3.4** Stok sync (marketplace'den otomatik)
- [ ] **5.3.5** DB:
  ```sql
  products (id, tenant_id, name, sku, category, price, margin_tier, stock_qty, status, metadata_json, created_at, updated_at)
  bundle_rules (id, tenant_id, trigger_product_id, offer_product_id, rule_type, discount_pct, priority, created_at)
  recommendation_log (id, tenant_id, conversation_id, recommended_product_id, action, created_at)
  ```

---

### GR-5.4: Abandoned Cart Recovery

> **Servis:** `Integrations` + `Outbound`
> **Sektör:** E-ticaret

- [ ] **5.4.1** Sepet terk tespiti (Trendyol/HB webhook veya sync)
- [ ] **5.4.2** Trigger kuralları (T+2h ilk hatırlatma, T+24h ikinci)
- [ ] **5.4.3** Kişiselleştirilmiş mesaj (sepetteki ürüne özel)
- [ ] **5.4.4** Opt-out compliance ("STOP" → otomatik unsubscribe)
- [ ] **5.4.5** Recovery rate tracking
- [ ] **5.4.6** DB:
  ```sql
  abandoned_carts (id, tenant_id, customer_phone, cart_items_json, abandoned_at, reminder_count, recovered, created_at)
  ```

---

### GR-5.5: Sipariş Sonrası Proaktif Satış (Senaryo S4)

> **Servis:** `Outbound` + `AgentAI` + `Integrations`
> **Sektör:** E-ticaret

- [ ] **5.5.1** Teslimat tamamlandı trigger → T+3 gün "Memnun musunuz?"
- [ ] **5.5.2** Memnun → cross-sell önerisi (Ürün Kataloğu'ndan)
- [ ] **5.5.3** Memnun değil → iade çevirme akışına yönlendir
- [ ] **5.5.4** Kişiselleştirilmiş öneri (son satın alma + kategori)
- [ ] **5.5.5** Post-purchase conversion tracking
- [ ] **5.5.6** DB:
  ```sql
  post_purchase_triggers (id, tenant_id, order_id, customer_phone, trigger_type, scheduled_at, sent_at, response, conversion_product_id, created_at)
  ```

---

### ~~GR-5.6: Click-to-WhatsApp Attribution~~ → Phase 3 GR-3.14 (v4.2)

> **Taşındı:** Phase 3 GR-3.14'e birleştirildi. UTM + Meta click id tek GR'da çözülür.

---

### ~~GR-5.7: Tedavi Sonrası Takip (S8)~~ → Phase 3 GR-3.20 (v4.2)

> **Taşındı:** Teknik bağımlılık sadece Outbound (Phase 1 ✅). Enterprise beklemeye gerek yoktu.

---

### ~~GR-5.8: Google Yorum + Referans (S10)~~ → Phase 3 GR-3.21 (v4.2)

> **Taşındı:** Teknik bağımlılık sadece Outbound (Phase 1 ✅). Sağlık niche'e erken değer sağlar.

---

### GR-5.6: Arapça Dil + Medikal Turizm AR Genişleme

> **Servis:** `AgentAI` + `Outbound`
> **Sektör:** Estetik
> **Kaynak:** eski GR-5.9'un AR-özel kısmı (v4.2 — TR/EN kısmı Phase 3 GR-3.22'ye taşındı)

- [ ] **5.6.1** Arapça template desteği (TR/EN/AR — 3. dil ekleme)
- [ ] **5.6.2** Arapça medikal turizm mesaj şablonları
- [ ] **5.6.3** RTL (sağdan sola) display desteği (UI)
- [ ] **5.6.4** AR follow-up otomasyonu (Arapça konuşan hastalar için)

---

## Fiyatlandırma Tier Önerisi (Bu Phase'ten itibaren)

### E-ticaret

| Tier | İçerik | Fiyat |
|------|--------|-------|
| Starter | Auto-resolution + Agent Assist | 3.000 TL/ay |
| Growth | + Knowledge + Outbound + İade Çevirme | 7.500 TL/ay |
| Pro | + Revenue Agent + Abandoned Cart + Attribution | 12.000 TL/ay |
| Enterprise | + SSO/2FA/Audit + Dedicated support | 15.000+ TL/ay |

### Sağlık

| Tier | İçerik | Fiyat |
|------|--------|-------|
| Klinik | Fiyat→Randevu + No-Show + Agent Assist | 7.500 TL/ay |
| Klinik Pro | + Tedavi Takibi + Yorum + Referans | 15.000 TL/ay |
| Medikal Turizm | + Multi-language + Konaklama/Transfer | 25.000+ TL/ay |

### Expansion Driver'lar

| Driver | Nasıl Çalışır | Upsell Trigger |
|--------|--------------|----------------|
| Agent Seat | İlk 3 dahil, sonra +500 TL/seat | Ekip büyüyünce |
| Conversation Volume | İlk 5.000/ay, aşımda +0.50 TL | Yoğun sezon |
| AI Credits | İlk 2.000 oto-cevap/ay, sonra paket | Deflection arttıkça |
| Integration Count | İlk 1 marketplace dahil, +2.000 TL | HB, Shopify ekleme |
| Outbound Volume | İlk 1.000 proaktif/ay, sonra +0.30 TL | Kampanya dönemleri |
| Knowledge Storage | İlk 50MB, sonra +500 TL/50MB | Katalog büyüyünce |

---

## Çıkış Kriterleri (Phase 6'ya Geçiş Şartı)

- [ ] Revenue Agent aktif satış yapıyor (en az 5 tenant)
- [ ] Abandoned cart recovery çalışıyor, recovery rate ölçülüyor
- [ ] Sağlık niche'i tam: tedavi takibi + yorum + referans (Phase 3 ✅) + medikal turizm AR
- [ ] 3 dil destekleniyor (TR/EN Phase 2 ✅, AR bu phase'te eklenir)
- [ ] Premium tier en az 3 müşteriye satılmış
- [ ] MRR 1.2M+ TL

> **v4.2 Not:** Attribution tracking Phase 3'e taşındı (GR-3.14). Tedavi takip ve yorum/referans
> da Phase 3'e taşındı (GR-3.20, GR-3.21). Bu phase'te kalan sağlık işi: Arapça dil desteği.
