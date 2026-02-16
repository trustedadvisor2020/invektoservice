# Phase 3F — Eğitim Niche

> **Hafta:** Phase 3B sonrası (Phase 3E ile paralel başlanabilir)
> **Bağımlılık:** Phase 3A (Outbound v2, Dashboard), kısmen Phase 2 (Knowledge)
> **Durum:** ⬜ Başlamadı
>
> **v6 (2026-02-16):** D1 kararı ile eklenen yeni sektör. 25 senaryo (EG-01~EG-25).
> **DİKKAT:** Çocuk verisi = KVKK özel nitelikli kişisel veri. Veli iletişimi çift taraflı.
> Yeni mikro servis gerekmez — mevcut altyapı + LMS entegrasyonu (Phase 3+).

---

## Durum Takibi

| Alt Gereksinim | Durum | Tamamlanma Tarihi | Notlar |
|----------------|-------|-------------------|--------|
| GR-3F.1 Kayıt + Seviye Belirleme Flow | ⬜ Başlamadı | — | EG-01, 05: Kayıt süreci, seviye testi, sınıf ataması |
| GR-3F.2 Fiyat + Ödeme + İade | ⬜ Başlamadı | — | EG-02, 07, 08: Fiyat bilgisi, taksit, cayma hakkı |
| GR-3F.3 Ders Program + Eğitmen Bilgisi | ⬜ Başlamadı | — | EG-03, 04, 16: Program, eğitmen, personel mesai |
| GR-3F.4 Devamsızlık + Veli İletişimi | ⬜ Başlamadı | — | EG-06, 11, 19: Devamsızlık, veli rapor, churn alert |
| GR-3F.5 Materyal + Sertifika + Sınav | ⬜ Başlamadı | — | EG-09, 10, 13: Dosya paylaşım, sonuç bildirimi, sertifika |
| GR-3F.6 Kampanya + Referral + Alumni | ⬜ Başlamadı | — | EG-15, 16, 18, 21: Erken kayıt, referral, yorum, mezun |
| GR-3F.7 Özel Ders + Paket + Kariyer | ⬜ Başlamadı | — | EG-22, 24, 25, 20: Birebir ders, staj, paket, kariyer |
| GR-3F.8 Çocuk KVKK Özel Koruma | ⬜ Başlamadı | — | EG-23: 18 yaş altı veri koruma, veli onamı ZORUNLU |

---

## Özet

Eğitim sektörü (dil kursları, dershaneler, mesleki eğitim, online eğitim, özel okullar) için 25 senaryo. Mevcut platform altyapısının üzerine sektöre özel kayıt akışları, veli iletişimi ve devamsızlık takibi eklenir.

**Satış dili:** "Kayıt döneminde hiçbir adayı kaçırmayın, veli memnuniyetini artırın"

**Mevcut Altyapı Kullanımı:**
- `Invekto.Knowledge` → Kurs katalogu, fiyat, eğitmen profilleri, materyal (GR-3F.1~3F.5)
- `Invekto.Outbound` → Kampanya, referral, veli rapor, devamsızlık alert (GR-3F.4, 3F.6)
- `Invekto.Automation` → Kayıt flow, seviye testi yönlendirme (GR-3F.1)
- `Invekto.AgentAI` → AI öneriler, şikayet tespiti (GR-3F.2, 3F.7)

**Sektöre Özel Yenilikler:**
- Kayıt dönemi yoğunluk yönetimi (çok yüksek mesaj hacmi 2-3 hafta)
- Veli-öğrenci çift taraflı iletişim (aynı konu, farklı içerik)
- Devamsızlık → churn prediction pipeline
- 18 yaş altı KVKK özel koruma (zorunlu veli onamı)
- LMS entegrasyonu (Phase 3+ — sınav, materyal, devam otomatik)

---

## Gereksinimler

### GR-3F.1: Kayıt + Seviye Belirleme Flow

> **Kaynak senaryolar:** EG-01 (kayıt), EG-05 (seviye belirleme)
> **Bağımlılık:** Automation, Knowledge Service

- [ ] **3F.1.1** Eğitim intent seti: kayıt, fiyat, program, seviye, eğitmen
- [ ] **3F.1.2** Kayıt chatbot flow: bilgi toplama (ad, yaş, seviye, gün tercihi) → form linki
- [ ] **3F.1.3** Seviye testi yönlendirme: intent → online test linki → sonuç → sınıf önerisi
- [ ] **3F.1.4** Knowledge base: kurs katalogu, gerekli belgeler, dönem takvimi

### GR-3F.2: Fiyat + Ödeme + İade

> **Kaynak senaryolar:** EG-02 (fiyat), EG-07 (ödeme), EG-08 (iade)
> **Bağımlılık:** Knowledge Service, Ödeme entegrasyonu

- [ ] **3F.2.1** Kurs + seviye bazlı fiyat bilgisi (Knowledge)
- [ ] **3F.2.2** Taksit/kampanya seçenekleri cevaplama
- [ ] **3F.2.3** İade/cayma intent → politika bilgisi + iade çevirme (grup/gün değişikliği öner)
- [ ] **3F.2.4** Online ödeme linki + taksit takibi + gecikme hatırlatma

### GR-3F.3: Ders Program + Eğitmen Bilgisi

> **Kaynak senaryolar:** EG-03 (program), EG-04 (eğitmen), EG-16 (personel mesai)
> **Bağımlılık:** Knowledge Service

- [ ] **3F.3.1** Ders programı + kontenjan bilgisi (Knowledge)
- [ ] **3F.3.2** Eğitmen profil bilgisi (özgeçmiş, uzmanlık, değerlendirme)
- [ ] **3F.3.3** Müsaitlik sorgulama (online vs yüz yüze, hafta içi/sonu)

### GR-3F.4: Devamsızlık + Veli İletişimi

> **Kaynak senaryolar:** EG-06 (devamsızlık), EG-11 (veli), EG-19 (devam takibi)
> **Bağımlılık:** Outbound Engine, Devamsızlık tracking sistemi

- [ ] **3F.4.1** Devamsızlık bildirim intent → kayıt + telafi teklifi
- [ ] **3F.4.2** Otomatik devamsızlık alert: 2 ders → öğrenciye, 3 ders → veliye + danışman
- [ ] **3F.4.3** Veli periyodik rapor: aylık devam + performans özeti (Outbound)
- [ ] **3F.4.4** Churn prediction: devamsızlık + memnuniyetsizlik → kayıp riski (S12 entegre)

### GR-3F.5: Materyal + Sertifika + Sınav

> **Kaynak senaryolar:** EG-09 (materyal), EG-10 (sınav), EG-13 (sertifika)
> **Bağımlılık:** Knowledge Service, Outbound Engine, LMS entegrasyonu (Phase 3+)

- [ ] **3F.5.1** Materyal isteme intent → Knowledge'dan dosya/link gönderimi
- [ ] **3F.5.2** Sınav sonucu bildirimi: Outbound trigger → öğrenci + veli
- [ ] **3F.5.3** Sertifika sorgulama: durum bilgisi + hazır olunca bildirim
- [ ] **3F.5.4** LMS entegrasyonu (opsiyonel): otomatik sonuç + materyal çekme

### GR-3F.6: Kampanya + Referral + Alumni

> **Kaynak senaryolar:** EG-15 (erken kayıt), EG-16 (referral), EG-18 (yorum), EG-21 (mezun)
> **Bağımlılık:** Outbound v2

- [ ] **3F.6.1** Erken kayıt kampanya broadcast (geçmiş öğrenci + tamamlanmamış kayıt)
- [ ] **3F.6.2** Referral kodu + tracking
- [ ] **3F.6.3** Dönem sonu yorum isteme (NPS + Google yorum)
- [ ] **3F.6.4** Alumni iletişimi: yeni kurs teklifi, etkinlik daveti

### GR-3F.7: Özel Ders + Paket + Kariyer

> **Kaynak senaryolar:** EG-22 (özel ders), EG-24 (staj), EG-25 (paket), EG-20 (kariyer)
> **Bağımlılık:** Knowledge Service, Appointments (özel ders booking)

- [ ] **3F.7.1** Özel ders intent → müsait eğitmen + saat + fiyat
- [ ] **3F.7.2** Çoklu kurs paket öneri + indirim hesaplama
- [ ] **3F.7.3** Staj/kariyer bilgisi (Knowledge) + partner şirket listesi
- [ ] **3F.7.4** Eğitmen müsaitlik takvimi entegrasyonu

### GR-3F.8: Çocuk KVKK Özel Koruma

> **Kaynak senaryolar:** EG-23
> **Bağımlılık:** CS-01 Opt-in, CS-08 Compliance, C5/C6
> **KVKK:** YÜKSEK — 18 yaş altı = özel nitelikli kişisel veri

- [ ] **3F.8.1** Kayıt sürecinde veli KVKK onam mesajı (otomatik)
- [ ] **3F.8.2** Çocuk verisi özel maskeleme (TC kimlik, fotoğraf)
- [ ] **3F.8.3** Veri erişim/silme talepleri iş akışı
- [ ] **3F.8.4** Saklama süresi politikası (çocuk verisi X yıl)
