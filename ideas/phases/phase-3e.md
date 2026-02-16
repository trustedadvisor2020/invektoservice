# Phase 3E — Güzellik Salonu Niche

> **Hafta:** Phase 3B sonrası (paralel başlanabilir)
> **Bağımlılık:** Phase 3A (Outbound v2, Dashboard), kısmen Phase 2 (Appointments, Knowledge)
> **Durum:** ⬜ Başlamadı
>
> **v6 (2026-02-16):** D1 kararı ile eklenen yeni sektör. 25 senaryo (GU-01~GU-25).
> Çoğu capability mevcut platform üzerine config/intent/template olarak eklenir.
> Yeni mikro servis gerekmez — Appointments, Outbound, Knowledge, Automation kullanılır.

---

## Durum Takibi

| Alt Gereksinim | Durum | Tamamlanma Tarihi | Notlar |
|----------------|-------|-------------------|--------|
| GR-3E.1 Güzellik Randevu + Fiyat + Intent | ⬜ Başlamadı | — | GU-01~04: Salon randevu, fiyat sorgulama, stilist seçimi, bekleme |
| GR-3E.2 No-show + Frekans Hatırlatma | ⬜ Başlamadı | — | GU-05, 06, 15: İptal boşluk, no-show, periyodik hatırlatma |
| GR-3E.3 İşlem Sonrası Takip + Bakım Talimatları | ⬜ Başlamadı | — | GU-07, 08, 13: Bakım talimatı, şikayet, yorum isteme |
| GR-3E.4 Ürün Satışı + Online Mağaza | ⬜ Başlamadı | — | GU-09, 22: Bakım ürünleri, online sipariş |
| GR-3E.5 Özel Gün + Grup + Çoklu Hizmet | ⬜ Başlamadı | — | GU-10, 17, 23: Gelin paketi, paket fiyat, kına/doğum günü |
| GR-3E.6 Kampanya + Referral + VIP | ⬜ Başlamadı | — | GU-11, 12, 14, 18, 21: Üyelik, referral, kampanya, VIP, trend |
| GR-3E.7 Instagram Lead + Şube Yönetimi | ⬜ Başlamadı | — | GU-24, 25: IG DM lead, franchise/çoklu şube |
| GR-3E.8 KVKK Fotoğraf + Alerji Güvenliği | ⬜ Başlamadı | — | GU-19, 20: Alerji eskalasyon, before/after onam |

---

## Özet

Güzellik salonu sektörü (kuaför, berber, cilt bakım, nail art, güzellik merkezi) için 25 senaryo. Mevcut platform altyapısının üzerine sektöre özel intent setleri, template'ler ve Knowledge base içeriği eklenir. Yeni mikro servis gerekmez.

**Satış dili:** "Randevu kaçırmayı bitirin, müşteri sadakatini artırın — salon yönetimi WhatsApp'tan"

**Mevcut Altyapı Kullanımı:**
- `Invekto.Appointments` → Salon randevu slot yönetimi (GR-3E.1, 3E.2)
- `Invekto.Outbound` → Hatırlatma, kampanya, referral (GR-3E.2, 3E.3, 3E.6)
- `Invekto.Knowledge` → Fiyat, stilist, ürün katalogu (GR-3E.1, 3E.4)
- `Invekto.Automation` → Intent detection, chatbot flow (GR-3E.1, 3E.5)
- `Invekto.AgentAI` → AI öneriler, şikayet tespiti (GR-3E.3, 3E.8)

**Sektöre Özel Yenilikler:**
- Frekans bazlı hatırlatma (saç boyama 6 haftada bir → otomatik mesaj)
- İşlem sonrası bakım talimatı template sistemi
- Fotoğraf KVKK onam akışı (before/after consent)
- Alerji/reaksiyon acil eskalasyon (GU-19)

---

## Gereksinimler

### GR-3E.1: Güzellik Randevu + Fiyat + Intent

> **Kaynak senaryolar:** GU-01 (randevu), GU-02 (fiyat), GU-03 (stilist seçimi), GU-04 (bekleme)
> **Bağımlılık:** Invekto.Appointments (Phase 2 mevcut), Knowledge Service

- [ ] **3E.1.1** Güzellik salonu intent seti: randevu, fiyat, stilist, bekleme, kapasite
- [ ] **3E.1.2** Knowledge base: fiyat listesi, hizmet katalogu, stilist profilleri
- [ ] **3E.1.3** Automation flow: randevu intent → müsait slot → onay → booking
- [ ] **3E.1.4** Fiyat aralığı cevaplama (saç uzunluğu/hizmet tipine göre)
- [ ] **3E.1.5** Stilist uzmanlık eşleştirme (Knowledge'dan)

### GR-3E.2: No-show + Frekans Hatırlatma

> **Kaynak senaryolar:** GU-05 (son dakika boşluk), GU-06 (no-show), GU-15 (frekans)
> **Bağımlılık:** Outbound Engine, Appointments

- [ ] **3E.2.1** Randevu hatırlatma: R-1gün + R-2saat (S7 mekanizması, güzellik config)
- [ ] **3E.2.2** İptal boşluk bildirimi: waitlist müşterilerine Outbound trigger
- [ ] **3E.2.3** Frekans hatırlatma: son işlem tarihi + tip → "X haftada bir" periyodik mesaj
- [ ] **3E.2.4** No-show tracking + repeat offender flag

### GR-3E.3: İşlem Sonrası Takip + Bakım Talimatları

> **Kaynak senaryolar:** GU-07 (bakım talimatı), GU-08 (şikayet), GU-13 (yorum)
> **Bağımlılık:** Outbound Engine, Knowledge Service

- [ ] **3E.3.1** İşlem tipine göre bakım talimatı template (boyama, keratin, lazer vb.)
- [ ] **3E.3.2** Outbound trigger: işlem sonrası → otomatik talimat mesajı
- [ ] **3E.3.3** T+1 gün memnuniyet follow-up + yorum isteme (S10 mekanizması)
- [ ] **3E.3.4** Şikayet intent → empati + düzeltme randevusu teklifi

### GR-3E.4: Ürün Satışı + Online Mağaza

> **Kaynak senaryolar:** GU-09 (ürün satışı), GU-22 (online mağaza)
> **Bağımlılık:** Knowledge Service, Ödeme entegrasyonu (Phase 3A)

- [ ] **3E.4.1** Ürün katalogu Knowledge base'e yükleme
- [ ] **3E.4.2** Ürün sorgusu intent + AI cevabı
- [ ] **3E.4.3** WhatsApp üzerinden sipariş + ödeme linki

### GR-3E.5: Özel Gün + Grup + Çoklu Hizmet

> **Kaynak senaryolar:** GU-10 (gelin paketi), GU-17 (çoklu hizmet), GU-23 (grup)
> **Bağımlılık:** Appointments (grup booking), Knowledge

- [ ] **3E.5.1** Gelin/özel gün paketi bilgi + fiyat (Knowledge)
- [ ] **3E.5.2** Çoklu hizmet paket fiyatlandırma önerisi
- [ ] **3E.5.3** Grup randevusu booking desteği

### GR-3E.6: Kampanya + Referral + VIP

> **Kaynak senaryolar:** GU-11 (üyelik), GU-12 (referral), GU-14 (kampanya), GU-18 (VIP), GU-21 (trend)
> **Bağımlılık:** Outbound v2, CRM

- [ ] **3E.6.1** Kampanya broadcast: segment bazlı (son X ayda Y hizmeti alan)
- [ ] **3E.6.2** Referral kodu + tracking
- [ ] **3E.6.3** VIP scoring: frekans + harcama + referans
- [ ] **3E.6.4** Üyelik/abonelik teklifi (tekrarlayan ziyaret tespiti)
- [ ] **3E.6.5** Mevsimsel trend öneri kampanyası

### GR-3E.7: Instagram Lead + Şube Yönetimi

> **Kaynak senaryolar:** GU-24 (IG DM lead), GU-25 (franchise/çoklu şube)
> **Bağımlılık:** C1 Unified Inbox, C2 Routing, C12 Attribution

- [ ] **3E.7.1** IG DM → WA geçişi (link + tracking)
- [ ] **3E.7.2** Konum bazlı şube routing
- [ ] **3E.7.3** Merkezi dashboard (çoklu şube performansı)

### GR-3E.8: KVKK Fotoğraf + Alerji Güvenliği

> **Kaynak senaryolar:** GU-19 (alerji), GU-20 (KVKK fotoğraf)
> **Bağımlılık:** CS-01 Opt-in, CS-08 Compliance, C5/C6

- [ ] **3E.8.1** Before/after fotoğraf onam mesajı (işlem öncesi otomatik)
- [ ] **3E.8.2** Onam kaydı compliance log
- [ ] **3E.8.3** Alerji/reaksiyon acil intent → PRIORITY routing → salon sahibi alert
- [ ] **3E.8.4** Alerji geçmişi kayıt (sonraki işlemde uyarı)
