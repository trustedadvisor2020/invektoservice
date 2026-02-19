# PKT-9: Guzellik Salonu

> **Durum:** PLANNED | **Phase:** 3E

## Ozet

Güzellik salonu sektörü (kuaför, berber, cilt bakım, nail art) için 25 senaryo. Yeni mikro servis gerekmez — mevcut altyapı (Appointments, Outbound, Knowledge, Automation) kullanılır.

**Bagimlilik:** PKT-5 (Outbound v2) + PKT-6A (Intent) + PKT-6B (Lead Mgmt)

## GR Listesi

- **GR-3E.1** Güzellik Randevu + Fiyat + Intent: salon randevu, fiyat, stilist seçimi, bekleme
- **GR-3E.2** No-show + Frekans Hatırlatma: iptal boşluk, no-show, periyodik hatırlatma
- **GR-3E.3** İşlem Sonrası Takip + Bakım Talimatları: bakım, şikayet, yorum
- **GR-3E.4** Ürün Satışı + Online Mağaza: bakım ürünleri, online sipariş
- **GR-3E.5** Özel Gün + Grup + Çoklu Hizmet: gelin paketi, paket fiyat, kına/doğum günü
- **GR-3E.6** Kampanya + Referral + VIP: üyelik, referral, kampanya, VIP, trend
- **GR-3E.7** Instagram Lead + Şube Yönetimi: IG DM lead, franchise/çoklu şube
- **GR-3E.8** KVKK Fotoğraf + Alerji Güvenliği: alerji eskalasyon, before/after onam

## GR Detail

### GR-3E.1: Güzellik Randevu + Fiyat + Intent
- 3E.1.1 Intent seti: randevu, fiyat, stilist, bekleme, kapasite
- 3E.1.2 Knowledge base: fiyat listesi, hizmet katalogu, stilist profilleri
- 3E.1.3 Automation flow: randevu intent → müsait slot → onay → booking
- 3E.1.4 Fiyat aralığı cevaplama (saç uzunluğu/hizmet tipine göre)
- 3E.1.5 Stilist uzmanlık eşleştirme (Knowledge)

### GR-3E.2: No-show + Frekans Hatırlatma
- 3E.2.1 Randevu hatırlatma: R-1gün + R-2saat
- 3E.2.2 İptal boşluk: waitlist müşterilerine trigger
- 3E.2.3 Frekans hatırlatma: son işlem + tip → periyodik mesaj
- 3E.2.4 No-show tracking + repeat offender flag

### GR-3E.3: İşlem Sonrası Takip
- 3E.3.1 İşlem tipine göre bakım talimatı template
- 3E.3.2 Outbound trigger: işlem sonrası → talimat mesajı
- 3E.3.3 T+1 gün memnuniyet follow-up + yorum isteme
- 3E.3.4 Şikayet intent → empati + düzeltme randevusu

### GR-3E.4: Ürün Satışı
- 3E.4.1 Ürün katalogu Knowledge'a yükleme
- 3E.4.2 Ürün sorgusu intent + AI cevabı
- 3E.4.3 WA üzerinden sipariş + ödeme linki

### GR-3E.5: Özel Gün + Grup
- 3E.5.1 Gelin/özel gün paketi bilgi + fiyat
- 3E.5.2 Çoklu hizmet paket fiyatlandırma
- 3E.5.3 Grup randevusu booking

### GR-3E.6: Kampanya + Referral + VIP
- 3E.6.1 Kampanya broadcast: segment bazlı
- 3E.6.2 Referral kodu + tracking
- 3E.6.3 VIP scoring: frekans + harcama + referans
- 3E.6.4 Üyelik/abonelik teklifi
- 3E.6.5 Mevsimsel trend öneri

### GR-3E.7: Instagram Lead + Şube
- 3E.7.1 IG DM → WA geçişi (link + tracking)
- 3E.7.2 Konum bazlı şube routing
- 3E.7.3 Merkezi dashboard (çoklu şube)

### GR-3E.8: KVKK Fotoğraf + Alerji
- 3E.8.1 Before/after fotoğraf onam mesajı
- 3E.8.2 Onam kaydı compliance log
- 3E.8.3 Alerji/reaksiyon acil → PRIORITY routing → salon sahibi alert
- 3E.8.4 Alerji geçmişi kayıt

## Notlar

- Config + content katmani (yeni servis yok, PKT-6 altyapisini tuketir)
- 8 GR, ~32 alt madde
- Sektöre özel: frekans bazlı hatırlatma, bakım talimatı template, alerji eskalasyon
