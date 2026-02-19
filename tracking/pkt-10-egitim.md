# PKT-10: Egitim

> **Durum:** PLANNED | **Phase:** 3F

## Ozet

Eğitim sektörü (dil kursları, dershaneler, mesleki eğitim, online eğitim) için 25 senaryo. Yeni mikro servis gerekmez — mevcut altyapı (Knowledge, Outbound, Automation, AgentAI) kullanılır.

**DİKKAT:** Çocuk verisi = KVKK özel nitelikli kişisel veri. Veli iletişimi çift taraflı.
**Bagimlilik:** PKT-5 (Outbound v2) + PKT-6A (Intent) + PKT-6B (Lead Mgmt)

## GR Listesi

- **GR-3F.1** Kayıt + Seviye Belirleme Flow: kayıt chatbot, seviye testi yönlendirme
- **GR-3F.2** Fiyat + Ödeme + İade: kurs fiyat, taksit, cayma hakkı iade çevirme
- **GR-3F.3** Ders Program + Eğitmen Bilgisi: program, kontenjan, eğitmen profil
- **GR-3F.4** Devamsızlık + Veli İletişimi: devamsızlık alert, veli rapor, churn prediction
- **GR-3F.5** Materyal + Sertifika + Sınav: dosya paylaşım, sonuç bildirimi, sertifika
- **GR-3F.6** Kampanya + Referral + Alumni: erken kayıt, referral, yorum, mezun
- **GR-3F.7** Özel Ders + Paket + Kariyer: birebir ders, staj, paket, kariyer
- **GR-3F.8** Çocuk KVKK Özel Koruma: 18 yaş altı veri koruma, veli onamı ZORUNLU

## GR Detail

### GR-3F.1: Kayıt + Seviye Belirleme Flow
- 3F.1.1 Eğitim intent seti: kayıt, fiyat, program, seviye, eğitmen
- 3F.1.2 Kayıt chatbot flow: bilgi toplama → form linki
- 3F.1.3 Seviye testi yönlendirme: intent → test linki → sonuç → sınıf önerisi
- 3F.1.4 Knowledge base: kurs katalogu, belgeler, dönem takvimi

### GR-3F.2: Fiyat + Ödeme + İade
- 3F.2.1 Kurs + seviye bazlı fiyat bilgisi (Knowledge)
- 3F.2.2 Taksit/kampanya seçenekleri
- 3F.2.3 İade/cayma intent → politika + iade çevirme (grup/gün değişikliği)
- 3F.2.4 Online ödeme linki + taksit takibi + gecikme hatırlatma

### GR-3F.3: Ders Program + Eğitmen
- 3F.3.1 Ders programı + kontenjan (Knowledge)
- 3F.3.2 Eğitmen profil (özgeçmiş, uzmanlık, değerlendirme)
- 3F.3.3 Müsaitlik sorgulama (online vs yüz yüze)

### GR-3F.4: Devamsızlık + Veli İletişimi
- 3F.4.1 Devamsızlık bildirim intent → kayıt + telafi
- 3F.4.2 Otomatik alert: 2 ders → öğrenci, 3 ders → veli + danışman
- 3F.4.3 Veli periyodik rapor: aylık devam + performans (Outbound)
- 3F.4.4 Churn prediction: devamsızlık + memnuniyetsizlik → kayıp riski

### GR-3F.5: Materyal + Sertifika + Sınav
- 3F.5.1 Materyal isteme → Knowledge'dan dosya/link
- 3F.5.2 Sınav sonucu bildirimi: Outbound → öğrenci + veli
- 3F.5.3 Sertifika sorgulama: durum + hazır olunca bildirim
- 3F.5.4 LMS entegrasyonu (opsiyonel)

### GR-3F.6: Kampanya + Referral + Alumni
- 3F.6.1 Erken kayıt kampanya broadcast
- 3F.6.2 Referral kodu + tracking
- 3F.6.3 Dönem sonu yorum isteme (NPS + Google)
- 3F.6.4 Alumni: yeni kurs teklifi, etkinlik daveti

### GR-3F.7: Özel Ders + Paket + Kariyer
- 3F.7.1 Özel ders intent → müsait eğitmen + saat + fiyat
- 3F.7.2 Çoklu kurs paket öneri + indirim
- 3F.7.3 Staj/kariyer bilgisi + partner şirketler
- 3F.7.4 Eğitmen müsaitlik takvimi

### GR-3F.8: Çocuk KVKK Özel Koruma
- 3F.8.1 Kayıt sürecinde veli KVKK onam mesajı (otomatik)
- 3F.8.2 Çocuk verisi özel maskeleme (TC, fotoğraf)
- 3F.8.3 Veri erişim/silme talepleri iş akışı
- 3F.8.4 Saklama süresi politikası

## Notlar

- Config + content katmani (yeni servis yok, PKT-6 altyapisini tuketir)
- 8 GR, ~32 alt madde
- Sektöre özel: kayıt dönemi yoğunluk, veli çift taraflı iletişim, devamsızlık→churn
