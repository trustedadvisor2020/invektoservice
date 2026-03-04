# Sağlık Senaryoları — Diş Klinikleri ve Estetik Klinikler

> **Sektör:** Diş klinikleri, estetik cerrahi klinikleri, medikal estetik merkezleri
> **Hedef Kitle:** Günde 20-100 mesaj alan, 2-15 kişilik ekibiyle hasta iletişimini yöneten klinikler

| Vaat | Detay |
|------|-------|
| **Diş** | Fiyat sorularını randevuya çevir, no-show'u %60 azalt |
| **Estetik** | Lead'leri hastaya dönüştür, medikal turizmi ölçekle |

---

## Neden Sağlık Sektörü İçin Invekto?

Bir diş kliniğinin sabah mesajları arasında en az 10 tane *"İmplant ne kadar?"* sorusu vardır. Bu soruya verilen her geç cevap, başka bir kliniğe giden bir hasta demektir.

Daha kötüsü — randevu alan hastaların **%15-20'si gelmiyor.** No-show. Ve tedavi sonrası takip? Çoğu klinikte hiç yapılmıyor.

Estetik kliniklerde durum daha da kritik. Instagram'dan gelen *"Botox fiyatı ne?"* DM'leri, klinik kapatırken resepsiyonist mesaisi bitmişken birikiyor. Yabancı hastalar İngilizce veya Arapça yazıyor — cevap verecek kimse yok.

**Her kaçan lead = potansiyel 5.000-50.000 TL kayıp.**

Invekto bu döngüyü kırar:
- Fiyat sorusunu **randevuya** dönüştürür
- Hatırlatma ile **no-show'u** azaltır
- Tedavi sonrası **otomatik takip** gönderir
- Tüm bunları **KVKK'ya uygun** yapar

---

### Sağlık Personaları

| Persona | Kim | Günlük Gerçeklik |
|---------|-----|------------------|
| **Dr. Burak (D1)** | Diş kliniği sahibi, 3 ünit | Koltukta hasta varken telefonu kontrol edemiyor |
| **Elif (D2)** | Ön büro sorumlusu | Telefon + WhatsApp + yüz yüze aynı anda |
| **Dr. Selin (A1)** | Estetik klinik sahibi, 5 doktor | Instagram leadlerinin %40'ı dönmüyor |
| **Zeynep (A2)** | Operasyon sorumlusu | 3 kanaldan mesaj, doktor onayı bekliyor |

---

---

## Gelir Senaryoları

---

### S6 — Fiyat Sorusunu Randevuya Çevirme

| | |
|---|---|
| **Potansiyel Etki** | ~60.000 TL/ay |
| **Tetikleyen** | "İmplant ne kadar?", "Botox fiyatı nedir?" |
| **Kritik Süre** | İlk 5 dakika |

#### Acı

*"İmplant ne kadar?"* — bu soruya doğru yaklaşım randevu, yanlış yaklaşım kayıp hasta. 5 dakika içinde cevap veren klinik, 1 saat sonra cevap verene göre **10 kat** daha fazla randevu alır.

#### Nasıl Çalışır

```
Hasta sorusu (WhatsApp/IG)
  ↓
AI niyeti anlar → "fiyat + tedavi talebi"
  ↓
Fiyat ARALIĞI verir (kesin fiyat değil — muayene gerekli)
  ↓
Randevu teklifi: "Bu hafta müsait saatlerimiz..."
  ↓
Hasta onaylarsa → hatırlatma zinciri başlar (R-1gün, R-2saat)
```

**Perde Arkası:** AgentAI (7105) niyeti anlar → Knowledge (7104) fiyat aralıklarını çeker → doktor onaylı şablon ile yanıt.

---

> #### 💡 Vay Be Anı
>
> **Saat 22:30.** Elif ön büroyu kapatıp eve gitmiş.
>
> Tam o sırada bir hasta Instagram'dan yazdı: *"İmplant ne kadar?"*
>
> Normalde bu mesajı Elif sabah 09:00'da görürdü — ama o zamana kadar hasta 4 kliniğe daha yazmıştı ve en hızlı cevap verenden randevu almıştı.
>
> **Invekto ile?** Mesaj geldi, **8 saniyede** fiyat aralığı ve randevu teklifi gitti. Hasta gece 22:31'de randevusunu oluşturdu.
>
> Sabah Elif geldiğinde sistemde yeni randevu bildirimi vardı:
>
> *"Bu hasta biz uyurken gelmiş!"*
>
> O hastanın tedavi değeri: **45.000 TL.**
> 8 saniye ile 11 saat arasındaki fark buydu.

---

---

### S7 — No-Show Önleme

| | |
|---|---|
| **Potansiyel Etki** | ~135.000 TL/ay |
| **Problem** | Randevu alan hastaların %15-20'si gelmiyor |
| **Her boş koltuk** | 1.000 - 5.000 TL kayıp |

#### Nasıl Çalışır

| Zamanlama | Mesaj |
|-----------|-------|
| **R - 1 gün** | "Yarın saat 14:00 randevunuz var. Onaylıyor musunuz?" |
| **R - 2 saat** | "Randevunuz 2 saat sonra. Klinik adresi: ..." |
| **İptal gelirse** | Yeni tarih önerilir + bekleme listesine haber verilir |
| **Hasta geldi** | Tedavi sonrası takip zinciri başlar |

**Sonuç:** No-show oranı **%60 azalır.** Ortalama bir klinik için aylık **135.000 TL** kayıp önlenir.

---

> #### 💡 Vay Be Anı
>
> Dr. Burak ayın sonunda raporlara baktı:
>
> | | Geçen Ay | Bu Ay |
> |---|---|---|
> | Kaçan randevu | 47 hasta | 19 hasta |
> | **Fark** | | **+28 hasta geldi** |
>
> 28 hasta x ort. 3.500 TL = **98.000 TL** — bir tek hatırlatma mesajı yüzünden.
>
> Ama asıl "vay be" anı şuydu:
>
> Bekleme listesindeki Ayşe Hanım'a *"14:00'te yer açıldı"* mesajı gitmiş. Ayşe Hanım **3 dakikada** onaylamış. O gün **22.000 TL'lik** implant tedavisine başlamış.
>
> Boş koltuk hem dolmuş — hem de **en değerli hastayla** dolmuş.

---

---

### Senaryo 27 — "Instagram DM: Foto atsam fiyat verir misiniz?"

| | |
|---|---|
| **Kanal** | Instagram DM |
| **Lead Tipi** | Fotoğraf + fiyat sorusu = en değerli lead |
| **Guardrail** | AI kesin fiyat vermez (sağlık riskli alan) |

#### Acı

Hasta Instagram'dan diş fotoğrafı gönderiyor, *"fiyat ne?"* diyor. Bu sağlık sektörünün **en değerli lead'idir** — hasta tedaviye hazır, sadece fiyat onayı bekliyor.

#### Invekto ile

- IG DM tek ekrandan yönetilir
- AI fotoğrafı inceler değil — ama **niyeti anlar:** "fiyat + tedavi talebi"
- Cevap önerisi: *"Fotoğrafınız için teşekkürler! Kesin tedavi planı ve fiyat muayenede belirlenir. Bu hafta [gün] saat [saat] müsaitiz. Randevu oluşturalım mı?"*

---

> #### 💡 Vay Be Anı
>
> **Pazar akşamı, saat 21:00.** Elif evde, tatilini yaşıyor.
>
> Bir hasta Instagram DM'den diş fotoğrafı attı:
> *"Bu diş kurtarılabilir mi? Fiyat ne olur?"*
>
> Invekto **12 saniyede** cevap verdi:
> *"Fotoğrafınız için teşekkürler! Kesin tedavi planı muayenede belirlenir. Yarın Pazartesi 10:00 veya 14:30 müsaitiz. Hangisi size uyar?"*
>
> Hasta 10:00'ı seçti. Pazartesi geldi. **35.000 TL'lik** implant tedavisine başladı.
>
> Hasta sonra şunu söyledi:
>
> > *"5 kliniğe yazdım, sadece siz cevap verdiniz. Diğerleri Pazartesi'ye kadar dönmedi bile."*
>
> Bir Pazar gecesi. 12 saniye. **35.000 TL.**

---

---

### Senaryo 30 — Acil Ağrı: Gece Mesajı

| | |
|---|---|
| **Zaman** | Gece 02:00+ |
| **Aciliyet** | Yüksek — triage gerekiyor |
| **Guardrail** | AI ilaç dozajı veya tedavi önerisi YAPMAZ |

#### Acı

Gece 2'de hasta yazıyor: *"Dişim çok ağrıyor, dayanamıyorum!"*

Bu mesajı sabaha erteleyemezsiniz.

#### Invekto ile

- AI **acil intent** tespit eder → yüksek öncelik etiketi
- Otomatik cevap: genel bilgi + yönlendirme
- Doktora **push bildirim** (mobil uygulama)
- Guardrail: sadece genel bilgi, kesin yorum yok

---

> #### 💡 Vay Be Anı
>
> **Gece 02:15.** Ayşe Hanım uyandı — sol çenesinde dayanılmaz ağrı.
>
> Panikle Google'a yazdı: *"diş ağrısı ölümcül olabilir mi?"* Korkunç sonuçlar çıktı, uykusu kaçtı.
>
> Sonra aklına geldi — kliniğin WhatsApp'ını denedi.
>
> **5 saniyede** cevap geldi:
>
> > *"Acil ağrınızı anlıyoruz. Geçici rahatlama için soğuk kompres uygulayabilirsiniz. Sabah 09:00'da size ilk randevuyu ayırdık.*
> >
> > *Ama: ateş, yüzde şişlik veya nefes darlığı varsa hemen 112'yi arayın."*
>
> Ayşe Hanım derin bir nefes aldı. **Biri vardı, biri ilgileniyordu.**
>
> Sabah geldi, 45 dakikada tedavisi bitti. Çıkışta resepsiyona döndü:
>
> > *"Gece 2'de bile cevap vermeniz... ben başka kliniğe gitmem artık."*
>
> **6 ay sonra** tüm ailesini getirdi — eşi, annesi, oğlu.
>
> Toplam değer: **78.000 TL.** Hepsi gece 2'deki o 5 saniyelik mesajla başladı.

---

---

### Senaryo 51 — Instagram DM: "Botox fiyatı nedir?"

| | |
|---|---|
| **Kanal** | Instagram DM |
| **Sektör** | Estetik klinik |
| **Kritik Metrik** | Her cevapsız DM = ort. 6.000 TL kayıp |

#### Acı

Estetik kliniğin Instagram'ı **vitrindir.** Her DM bir potansiyel hasta — ama cevap gecikmesi **%50 kayıp** demek.

#### Invekto ile

- IG DM tek ekrandan yönetim
- AI: *"Botox fiyatlarımız bölgeye göre 3.000-8.000 TL arasında değişiyor. Yüz yüze değerlendirme ile size özel plan oluşturuyoruz. Bu hafta randevu oluşturmamı ister misiniz?"*
- Lead takibi: DM → WhatsApp geçişi → randevu → tedavi

---

> #### 💡 Vay Be Anı
>
> Dr. Selin Instagram analitiğine baktı:
>
> | | Geçen Ay |
> |---|---|
> | Gelen DM | 127 |
> | Cevaplanan | 68 |
> | **Cevapsız** | **59** |
>
> Her DM = ortalama 6.000 TL potansiyel.
> **59 x 6.000 = 354.000 TL** havaya uçmuş.
>
> **Invekto sonrası:**
>
> | | Öncesi | Sonrası |
> |---|---|---|
> | Cevaplanan | 68 / 127 | **127 / 127** |
> | Cevap süresi | 2-6 saat | **15 saniye** |
> | Randevu | 18 | **31** |
> | Tedavi | 18 x 6K = 108K | **31 x 6K = 186K TL** |
>
> Dr. Selin:
> > *"Bu rakamları görene kadar kaçan hastaları bilmiyordum. Bilmediğin şeyi ölçemezsin, ölçemediğin şeyi düzeltemezsin."*

---

---

### Senaryo 56 — Uygunluk / Kontrendikasyon Soruları

| | |
|---|---|
| **Tipik Soru** | "Hamileyken botox yaptırabilir miyim?" |
| **Risk** | Yanlış cevap = ciddi sağlık riski |
| **Guardrail** | AI kesinlikle tıbbi tavsiye vermez |

#### Acı

*"Kan sulandırıcı kullanıyorum, botox yaptırabilir miyim?"* — bu soruya yanlış cevap hayati tehlike yaratabilir.

#### Invekto ile

- **KRİTİK GUARDRAIL:** AI kesinlikle tıbbi tavsiye vermez
- Cevap: *"Bu soruyu doktorumuzla değerlendirmemiz gerekiyor. Muayene randevusu oluşturalım mı?"*
- Genel güvenlik bilgisi verilebilir ama kesin yorum yapılmaz
- Handoff: AI → doktora yönlendirme

---

> #### 💡 Vay Be Anı
>
> Bir hasta yazdı: *"Kan sulandırıcı kullanıyorum, botox yaptırabilir miyim?"*
>
> Tehlikeli soru. Yanlış cevap = ciddi sağlık riski.
>
> Invekto'nun guardrail'i devreye girdi:
>
> > *"Bu önemli bir sağlık sorusu ve kesinlikle doktorumuzun değerlendirmesi gerekiyor. Size özel bir ön görüşme randevusu oluşturalım mı?"*
>
> Hasta randevu aldı. Dr. Selin muayenede:
>
> > *"İyi ki doğrudan botox randevusu vermemişler. Bu hastada önce kardiyologla konsültasyon gerekiyordu."*
>
> Hasta tedavi sonrası:
>
> > *"Başka klinikte direkt yaptıracaklardı, burada önce sağlığımı düşündüler."*
>
> Bu güven, parayla satın alınamaz.
>
> O hasta **2 yılda 85.000 TL'lik** işlem yaptırdı — çünkü *"burada beni koruyorlar"* hissetti.

---

---

### Senaryo 63 — Click-to-WhatsApp Reklam Lead'i

| | |
|---|---|
| **Kanal** | Instagram Reklam → WhatsApp |
| **Önem** | Reklama para harcanmış — her lead değerli |
| **Fark** | ROI ölçülebilir hale gelir |

#### Acı

Instagram reklamında "Şimdi Yaz" butonu → WhatsApp'a düşüyor. Bu lead'in değeri yüksek çünkü **reklama para harcanmış.** Ama "kaç hasta geldi?" sorusuna kimse net cevap veremiyor.

#### Invekto ile

- **UTM tracking** ile reklam kaynağı kaydedilir
- AI hızlı karşılama + niyete uygun cevap
- Dashboard: *"Bu kampanyadan 45 lead, 12 randevu, 8 tedavi"*

---

> #### 💡 Vay Be Anı
>
> Dr. Selin ayda **25.000 TL** Instagram reklamı veriyordu.
>
> *"Kaç hasta geldi?"* diye sorduğunda — kimse net cevap veremiyordu.
>
> **Invekto ile** her Click-to-WhatsApp lead'i UTM ile etiketlendi. Ay sonu raporu:
>
> | Kampanya | Lead | Randevu | Tedavi | Gelir |
> |----------|------|---------|--------|-------|
> | **A** | 45 | 12 | 8 | **64.000 TL** |
> | **B** | 62 | 7 | 3 | **18.000 TL** |
>
> Hemen görünüyor: Kampanya A düşük maliyetli ve yüksek dönüşümlü. Kampanya B pahalı lead getiriyor ama dönmüyor.
>
> Dr. Selin bütçeyi yeniden dağıttı — Kampanya A'ya ağırlık verdi.
>
> Sonraki ay: aynı 25.000 TL bütçeyle **%40 daha fazla hasta.**
>
> > *"Artık karanlıkta reklam vermiyorum, her kuruşun nereye gittiğini biliyorum."*

---
