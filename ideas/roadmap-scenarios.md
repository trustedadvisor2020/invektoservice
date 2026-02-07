# Invekto — Senaryo Portföyü & Outbound Engine

> Ana dosya: [roadmap.md](roadmap.md)
> Bu dosya: 10 revenue senaryosu (2 niche) + E-ticaret & Sağlık senaryoları detay + Outbound Engine kritik bulgusu ve gereksinimleri

---

## Senaryo Portföyü (10 Senaryo — 2 Niche)

> Roadmap sadece "kargom nerede" senaryosuna değil, 10 farklı revenue senaryosuna dayanıyor.
> Her senaryo test edilmiş Hormozi değer denklemine uygun.

### E-ticaret Senaryoları (Niche 1: Trendyol/HB Satıcıları)

| # | Senaryo | Tetikleyici | AI Aksiyonu | Aylık Etki (10 müşteri) | Roadmap Phase |
|---|---------|-------------|-------------|-------------------------|---------------|
| S1 | **Negatif Yorum Kurtarma** | Trendyol'da 1-2 yıldız yorum | AI tespit → özür + çözüm mesajı → yorum güncelleme talebi | ~144K TL | Phase 3 |
| S2 | **Satış Öncesi Ürün Soruları** | "Bu ürün X'e uyar mı?" | Knowledge base'den cevap + ürün linki + alternatif | ~31.5K TL | Phase 3 |
| S3 | **İade Çevirme** | "İade etmek istiyorum" | Neden sor → değişim/kupon öner → iade maliyetinden kurtar | ~18K TL | Phase 2-3 |
| S4 | **Sipariş Sonrası Proaktif Satış** | Teslimat tamamlandı | T+3 gün "memnun musunuz?" → cross-sell önerisi | ~22.5K TL | Phase 5 |
| S5 | **Toplu/B2B Lead Tespiti** | "100 adet lazım", "toptan fiyat var mı" | VIP flag → sales team alert → özel teklif akışı | ~37.5K TL | Phase 2 |

---

## E-ticaret Senaryoları — Detay

### S1: Negatif Yorum Kurtarma (Review Recovery)

**Sahne:**

Trendyol'da müşteri 1 yıldız vermiş: *"Ürün geç geldi, ilgilenilmedi."* Bu yorum ürün sayfasında duruyor. Potansiyel 100 müşteri bunu görüyor. Her 1 yıldızlık yorum → ortalama **%8 dönüşüm kaybı**. Satıcının günde 200 siparişi var → 16 satış kaybı/gün → 480 satış/ay. Ortalama sepet 300 TL → **144.000 TL/ay kayıp — TEK BİR KÖTÜ YORUMDAN.**

**Bugün ne oluyor:**
```
→ Satıcı Trendyol panelinde yorumları görüyor
→ WhatsApp'tan müşteriyi bulmaya çalışıyor (telefon eşleştirme)
→ Çoğu zaman ulaşamıyor veya çok geç kalıyor
→ Yorum düzeltilemiyor, hasar kalıcı
```

**Invekto ile:**
```
→ Trendyol API'den düşük yıldızlı yorumlar otomatik çekilir
→ Müşteri telefonu sipariş verisinden eşleştirilir
→ AI otomatik WhatsApp mesajı hazırlar:
  "Merhaba [Ad], siparişinizden memnun kalmadığınızı gördük.
   Sorununuzu çözmek istiyoruz. Size nasıl yardımcı olabiliriz?"
→ Sorun çözülürse: "Yorumunuzu günceller misiniz?" önerisi
→ Dashboard'da: kurtarılan yorum sayısı + tahmini gelir etkisi
```

**Para etkisi:**
```
Kurtarma oranı %30 olsa → ayda 43.200 TL kurtarılan gelir
Invekto fiyatı 5.000 TL → 8.6x ROI — SADECE BU SENARYODAN
```

> **Hormozi:** *"Bu tek senaryo, kargo sorgulamadan daha değerli. Çünkü kargo sorgusu maliyet düşürür, yorum kurtarma gelir kurtarır. İnsanlar maliyet düşürmek için 1x öder, gelir kurtarmak için 3x öder."*

---

### S2: Satış Öncesi Ürün Soruları (Pre-Sale Conversion)

**Sahne:**

Instagram'dan veya WhatsApp'tan mesaj geliyor: *"Bu ceket XL bedene uyar mı?"*, *"Su geçirmez mi?"*, *"Aynı üründen mavi var mı?"*

Temsilci Trendyol paneline gidiyor → ürünü arıyor → açıklamayı okuyor → WhatsApp'a dönüp yazıyor. 5-10 dakika sürüyor. Cevap 10dk'dan fazla sürerse müşteri gidiyor. Conversion rate: **<30dk yanıt = %40 dönüşüm, >30dk yanıt = %5 dönüşüm.**

Günde 50 ürün sorusu geliyor. 30'u geç cevaplanıyor. 30 × %35 dönüşüm farkı × 300 TL sepet = 3.150 TL/gün kayıp = **94.500 TL/ay.**

**Bugün ne oluyor:**
```
→ Temsilci her soruyu manuel araştırıyor
→ Ürün bilgisi kafasında veya Trendyol sayfasında
→ Aynı soru 50 kez sorulsa 50 kez araştırıyor
→ Yoğun saatlerde cevap gecikiyor, müşteri satın almadan gidiyor
```

**Invekto ile:**
```
→ Ürün kataloğu Knowledge base'e yüklenir (PDF/CSV/Trendyol sync)
→ AI ürün sorusunu anlar → katalogdan cevap bulur → anında yanıt
→ "Bu ceket XL bedene uygundur. Göğüs: 112cm, Boy: 74cm. (kaynak: ürün kartı)"
→ Cevap 30sn'de gider, dönüşüm %40'a çıkar
→ Dashboard'da: "ürün sorusu → satın alma" dönüşüm oranı
```

**Para etkisi:**
```
30 geç cevaplanan sorunun 10'u hızlanırsa → 10 × %35 × 300 TL = 1.050 TL/gün
= 31.500 TL/ay ek gelir. Invekto 5.000 TL → 6.3x ROI
```

> **Hormozi:** *"Her cevaplanmayan ürün sorusu = kaçan satış. Bu AI'ı 'support tool' olarak değil 'sales closer' olarak konumla. Cevap hızı = para hızı."*

---

### S3: İade Çevirme (Return Deflection)

**Sahne:**

Müşteri yazıyor: *"İade etmek istiyorum."* Trendyol satıcısının iade oranı ortalama %8-15. Günde 200 sipariş × %10 iade = 20 iade/gün. Her iade: ürün maliyeti + kargo + komisyon kaybı + yorum riski ≈ 150 TL kayıp. 20 × 150 = 3.000 TL/gün = **90.000 TL/ay iade kaybı.**

**İadelerin %40'ı aslında çözülebilir:**
- "Beden küçük geldi" → değişim öner
- "Kullanmayı bilmiyorum" → kullanım videosu gönder
- "Rengi farklı çıktı" → %15 indirim kuponu teklif et
- "Geç geldi, artık lazım değil" → bir sonraki siparişe %10 indirim

**Bugün ne oluyor:**
```
→ Temsilci direkt iade prosedürünü anlatıyor
→ Neden iade istediği sorulmuyor
→ Çözülebilir iadeler de işleniyor
→ Para gidiyor
```

**Invekto ile:**
```
→ AI "iade" intent'ini yakalar ama direkt iade işlemi başlatmaz
→ Önce sebebi sorar: "Üzgünüz! Neden iade düşünüyorsunuz?"
→ Sebebe göre alternatif sunar:
  - Beden → "Değişim yapalım, kargo bizden"
  - Kullanım → "Size video/rehber göndereyim"
  - Memnuniyetsizlik → "%15 indirim kuponu" (tenant tanımlı limit)
→ Müşteri kabul ederse → iade önlendi, gelir korundu
→ Dashboard'da: önlenen iade sayısı + kurtarılan gelir
```

**Para etkisi:**
```
20 iade/gün × %40 çözülebilir × %50 başarı = 4 kurtarılan iade/gün
4 × 150 TL = 600 TL/gün = 18.000 TL/ay kurtarılan gelir
```

> **Hormozi:** *"Her iade bir müşteri kaybı. Ama her iade talebi bir kurtarma fırsatı. Çoğu satıcı bunu bilmiyor çünkü araçları yok. Sen aracı ver, parayı koru."*

---

### S4: Sipariş Sonrası Proaktif Satış (Post-Purchase Outbound)

**Sahne:**

Müşteri dün telefon kılıfı aldı. 3 gün sonra kargo teslim edildi. Sessizlik.

Ama bu müşteri muhtemelen şunlara da ihtiyaç duyuyor: ekran koruyucu, şarj kablosu, powerbank. Bugün bu cross-sell yapılmıyor. Müşteri tek ürün alıp gidiyor. Repeat purchase rate: **%5** (sektör ortalaması).

**Bugün ne oluyor:**
```
→ Hiçbir şey. Sipariş teslim edildi, bitti.
→ Müşteri başka satıcıdan alıyor
→ Lifetime value düşük kalıyor
```

**Invekto ile:**
```
→ Teslimattan 2 gün sonra otomatik WhatsApp:
  "Merhaba [Ad]! Telefon kılıfınız ulaştı mı?
   Bu kılıfla en çok tercih edilen ekran koruyucumuz şu an %15 indirimli.
   İlgilenir misiniz?"
→ Bundle rules: "kılıf alan → ekran koruyucu öner" (tenant tanımlı)
→ Müşteri "evet" derse → ürün linki veya ödeme linki gönder
→ Opt-out: "Hayır teşekkürler" → bir daha mesaj gitmesin
```

**Para etkisi:**
```
Günde 200 teslimattan 50'sine mesaj (%25 hedefleme)
50'den 5'i satın alır (%10 conversion) × 150 TL = 750 TL/gün
= 22.500 TL/ay EK GELİR (sıfırdan oluşan, daha önce yoktu)
```

> **Hormozi:** *"En ucuz müşteri zaten sana para vermiş olan müşteri. Yeni müşteri bulmak 5x daha pahalı. Ama çoğu satıcı teslimattan sonra müşteriyle konuşmuyor. Bu bedava para."*

---

### S5: Toplu/B2B Lead Tespiti (High-Value Lead Detection)

**Sahne:**

WhatsApp'tan mesaj geliyor: *"Merhaba, 50 adet sipariş vermek istiyorum. Toplu fiyat var mı?"*

Bu mesaj günlük 150+ mesajın arasında kayboluyor. Temsilci normal müşteri gibi cevaplıyor. B2B fırsatı fark edilmeden geçiyor.

Veya: *"Kurumsal fatura keser misiniz?"*, *"Bayilik vermek istiyor musunuz?"*, *"Düzenli sipariş verebilir miyiz?"*

Bunlar high-value lead. Tek sipariş 50-500 adet. Ortalama B2B sipariş: 15.000-50.000 TL. Ayda 3-5 böyle lead geliyordur. **Çoğu kaçıyor.**

**Bugün ne oluyor:**
```
→ Mesaj gürültüde kaybolur
→ Temsilci fark etse bile "müdüre sorayım" der, geri dönmez
→ Müşteri bekler, rakibe gider
→ Ayda 2-3 B2B fırsat kaybolur → 30.000-150.000 TL kayıp
```

**Invekto ile:**
```
→ AI mesajda B2B sinyallerini tespit eder:
  - "toplu", "adet", "kurumsal", "bayilik", "düzenli sipariş"
→ Otomatik olarak:
  - Konuşmayı VIP olarak etiketler
  - Satıcıya/müdüre acil bildirim gönderir
  - Müşteriye hızlı yanıt: "Toplu siparişler için özel fiyatlarımız var!
    Hemen ilgili arkadaşımız sizinle iletişime geçecek."
→ Dashboard'da: tespit edilen B2B lead'ler + durumları + kapanış oranı
```

**Para etkisi:**
```
Ayda 3 B2B lead × %50 tespit (daha önce kaçıyordu)
1.5 yeni B2B müşteri/ay × 25.000 TL ortalama sipariş = 37.500 TL/ay
```

> **Hormozi:** *"Bir B2B müşteri 100 normal müşteriye bedel. Ama çoğu satıcı B2B lead'in geldiğini bile bilmiyor. AI'ın bir tek şeyi daha iyi yapması lazım: altın nugget'ı çöpten ayır."*

---

### Sağlık Senaryoları (Niche 2: Klinikler — Phase 3-4+)

| # | Senaryo | Tetikleyici | AI Aksiyonu | Aylık Etki (5 klinik) | Roadmap Phase |
|---|---------|-------------|-------------|----------------------|---------------|
| S6 | **Fiyat → Randevu Dönüşümü** | "İmplant ne kadar?" | Fiyat aralığı + ücretsiz muayene teklifi + slot öner | ~60K TL | Phase 3-4 |
| S7 | **No-Show Önleme** | Randevu T-48h, T-2h | Otomatik hatırlatma + onay iste + iptal slot'u doldur | ~135K TL | Phase 3-4 |
| S8 | **Tedavi Sonrası Takip** | Tedavi tamamlandı | T+1, T+7, T+30 kontrol soruları → şikayet varsa doktora alert | ~90K TL | Phase 4-5 |
| S9 | **Medikal Turizm Lead Yönetimi** | İngilizce/Arapça mesaj | Multi-language AI → fiyat + konaklama + transfer teklifi | ~300K+ TL | Phase 4-5 |
| S10 | **Google Yorum + Referans Motoru** | Tedavi başarılı + hasta memnun | Yorum rica → link gönder → referans kodu → arkadaş getir kampanyası | ~105K TL | Phase 4-5 |

---

## Sağlık Niche — Hedef Avatar

```
İsim: Dr. Ayşe
İş: İstanbul'da 3 koltuklu özel diş kliniği
Ekip: 1 resepsiyonist, 2 asistan
WhatsApp durumu:
  → Günde 40-60 mesaj geliyor (Instagram reklamları + Google + tavsiye)
  → %70'i fiyat sorusu: "İmplant kaç para?", "Zirkonyum fiyat?"
  → Resepsiyonist mesajlara yetişemiyor, hastayla da ilgileniyor
  → Akşam/hafta sonu mesajlar cevapsız kalıyor → hasta rakibe gidiyor
  → Randevuya gelen hasta, gelmeyenden 10x değerli ama dönüşüm %15
  → No-show oranı %25 → günde 2 boş koltuk = 6.000 TL/gün kayıp
Ciro: ~300.000 TL/ay
Ağrı: Hastaları kaybediyor çünkü hızlı cevap veremiyor
```

---

## Sağlık Senaryoları — Detay

### S6: Fiyat Sorusu → Randevu Dönüşümü (Price-to-Appointment Conversion)

**Sahne:**

Instagram reklamından WhatsApp'a mesaj düşüyor: *"Merhaba, diş implant kaç lira?"*

Bu sağlık sektörünün "kargom nerede"si. Hacmi en yüksek mesaj. Günde 30+ fiyat sorusu geliyor.

**TUZAK:** Fiyat verirsen hasta gidiyor. Çünkü:
- Fiyatı başka klinikle kıyaslıyor (apple-to-orange)
- Muayene olmadan doğru fiyat verilemez
- Fiyat yüksek gelirse bir daha yazmıyor

> Sektörün bildiği gerçek: *"Kliniğe gelen hastanın %70'i tedavi olur. Gelmeyenin %5'i tedavi olur."*
> Yani ASIL HEDEF fiyat vermek değil, **RANDEVU almak**.

**Bugün ne oluyor:**
```
→ Resepsiyonist "İmplant 25.000-45.000 TL arası" yazıyor
→ Hasta "teşekkürler" yazıp gidiyor
→ Dönüşüm: %10-15
→ Günde 30 fiyat sorusu × %12 dönüşüm = 3.6 randevu
→ Kaybedilen: 26 potansiyel hasta/gün
```

**Invekto ile:**
```
→ AI fiyat sorusunu yakalar ama DİREKT FİYAT VERMEZ
→ Bunun yerine:
  "Teşekkürler! İmplant tedavisi hastaya göre farklılık gösterir.
   Size doğru bilgi verebilmemiz için ücretsiz muayene randevusu
   öneriyoruz. Bu hafta Çarşamba 14:00 veya Cuma 10:00 uygun mu?"
→ Hasta "Çarşamba olur" derse → randevu otomatik kayıt
→ Hasta ısrar ederse → fiyat aralığı ver + "kesin fiyat muayenede"
→ 48 saat cevap yoksa → follow-up: "Randevunuzu ayırmamızı ister misiniz?"
```

**Para etkisi:**
```
Dönüşüm %12 → %25'e çıkarsa (sektör benchmark'ı: iyi klinikler %30)
30 soru × %13 artış = 3.9 ek randevu/gün
3.9 × %70 tedavi × 25.000 TL ort. tedavi = 68.250 TL/gün EK GELİR
Aylık: ~1.400.000 TL ek gelir potansiyeli

(Gerçekçi: her gün bu kadar olmaz, ama ayda %20 artış bile =
 60.000 TL/ay ek gelir. Invekto fiyatı 10.000 TL → 6x ROI)
```

> **Hormozi:** *"Sağlık sektöründe fiyat vermek = müşteri kaybetmek. İnsanlar fiyat sorar ama aslında güven ister. AI'ın işi fiyat vermek değil, kapıdan içeri sokmak. Kapıdan giren hastanın %70'i para bırakır."*

---

### S7: No-Show Katili (Appointment No-Show Prevention)

**Sahne:**

Dr. Ayşe'nin günde 12 randevusu var. No-show oranı %25 → günde 3 boş koltuk. Koltuk başına ortalama gelir: 3.000 TL. 3 × 3.000 = 9.000 TL/gün kayıp = **270.000 TL/ay**.

**No-show nedenleri:**
- Unuttum (%40)
- Vazgeçtim ama söylemedim (%30)
- Başka klinikle randevu aldım (%20)
- Gerçek engel (%10)

**Bugün ne oluyor:**
```
→ Resepsiyonist randevudan 1 gün önce arar (40 dakika telefon)
→ %30'una ulaşamıyor
→ İptal eden olursa boş koltuk doldurulamıyor
→ Hasta "geleceğim" diyor ama gelmiyor
```

**Invekto ile:**
```
→ Otomatik hatırlatma zinciri:
  - R-3 gün: "Randevunuz Çarşamba 14:00. Takvime ekleyin [link]"
  - R-1 gün: "Yarın 14:00 randevunuz var. Onaylıyor musunuz? ✓/✗"
  - R-2 saat: "2 saat sonra görüşmek üzere! Adresimiz: [harita link]"
→ Hasta "gelemiyorum" derse:
  - Hemen alternatif tarih öner
  - Boşalan slota bekleme listesinden hasta çağır
→ Cevap vermezse → "iptal riski" olarak işaretle, resepsiyonist arasın
```

**Para etkisi:**
```
No-show %25 → %12'ye düşerse (sektör en iyi: %10)
Günde 1.5 ek dolu koltuk × 3.000 TL = 4.500 TL/gün
= 135.000 TL/ay kurtarılan gelir
```

> **Hormozi:** *"No-show = her gün kasanın önüne para koyup yakmak. 3 mesajla %50 azaltabilirsin. Bu dünyanın en kolay ROI'si. Her klinik sahibi bunu duyunca 'dün neden yoktun?' der."*

---

### S8: Tedavi Sonrası Takip (Post-Treatment Care)

**Sahne:**

Hasta dün implant ameliyatı oldu. Gece WhatsApp'a yazıyor: *"Şişlik normal mi?"*, *"Ağrım var ne yapmalıyım?"*

Dr. Ayşe gece 23:00'te bu mesajı görüyor. Ertesi gün 15 hasta daha tedavi sonrası soru soruyor. Her soru aynı: "şişlik normal mi", "ne yiyebilirim", "ilacı ne zaman alayım". Bu soruların **%90'ı standart**. Ama her biri doktoru/resepsiyonisti meşgul ediyor. Ve cevaplanmayan sorular → hasta paniği → kötü Google yorumu → güven kaybı.

**Bugün ne oluyor:**
```
→ Doktor akşam/gece WhatsApp'tan cevap veriyor (burnout)
→ Resepsiyonist aynı bilgiyi 50 kez yazıyor
→ Bazı hastalar cevap alamıyor → endişe → kötü yorum
→ Doktor zamanı tedavide değil mesajlaşmada gidiyor
```

**Invekto ile:**
```
→ Tedavi tipi bazlı otomatik takip zinciri:
  - T+0 (ameliyat günü akşam):
    "Ameliyatınız başarılı geçti! İlk 24 saat rehberiniz:
     - Hafif şişlik normaldir
     - Soğuk kompres uygulayın
     - [İlaç adı] 8 saatte bir alın
     - Acil durumda [telefon]"
  - T+1 gün: "Bugün kendinizi nasıl hissediyorsunuz?"
    Hasta "şişlik var" → "48 saate kadar normal. Kompres devam."
    Hasta "çok ağrı" → "Hemen doktorumuzu arayın: [telefon]" + doktora alert
  - T+7 gün: "Kontrol randevunuz yaklaşıyor. [tarih] uygun mu?"
  - T+30 gün: "İyileşme nasıl? Memnuniyetinizi öğrenmek isteriz."
→ Doktor sadece GERÇEK acil durumlarda ulaşılıyor
→ Standart sorular AI tarafından %90 oranında çözülüyor
```

**Para etkisi:**
```
Doktor zamanı: günde 1 saat mesaj yazma → 15dk'ya düşer = 45dk kazanç
45dk × 1 ek tedavi yapabilir × 3.000 TL = 3.000 TL/gün
= 90.000 TL/ay (doktorun zamanını tedaviye çevirmek)
+ Hasta memnuniyeti → Google yorum skoru artışı → daha fazla yeni hasta
+ Malpractice riski azalır (takip kayıt altında)
```

> **Hormozi:** *"Doktor mesaj yazarak para kazanmıyor. Doktor tedavi yaparak para kazanıyor. Her dakika mesajlaşmada = koltuktaki para kaybı. AI doktoru mesajdan kurtar, koltuğa oturt."*

---

### S9: Medikal Turizm Lead Yönetimi (Medical Tourism Pipeline)

**Sahne:**

İstanbul saç ekimi / diş / estetik kliniği. Instagram'dan Arapça, İngilizce, Rusça mesajlar geliyor. Her lead değeri: 2.000-10.000 USD (50.000-250.000 TL). Günde 20-30 uluslararası lead geliyor. Karar süreci uzun: 2-8 hafta. Zaman farkı var: hasta gece yazıyor, klinik sabah cevaplıyor → 8 saat gecikme. Hasta 3-4 klinikten aynı anda teklif alıyor.

> **İLK CEVAPLAYAN KAZANIYOR.**

**Bugün ne oluyor:**
```
→ Resepsiyonist İngilizce'de zorlanıyor
→ Arapça mesajlar Google Translate ile çevriliyor
→ Gece gelen mesajlar sabaha cevap buluyor → hasta rakibe gitmiş
→ Takip yapılmıyor → "fiyat aldım ama bir daha yazmadılar"
→ 20 lead'den 2-3'ü randevuya dönüyor (%12)
```

**Invekto ile:**
```
→ 7/24 otomatik cevap (İngilizce/Arapça/Rusça):
  "Thank you for your interest! Here's what we need to give you
   an accurate quote:
   1. A photo of your current situation
   2. Your general health condition
   3. When are you planning to visit Istanbul?"
→ AI hasta cevaplarından ön değerlendirme yapıyor
→ Lead scoring: budget, timeline, readiness
→ Otomatik follow-up zinciri:
  - T+0: İlk cevap (30sn)
  - T+1 gün: "Did you have a chance to send the photos?"
  - T+3 gün: "We prepared a special package for you"
  - T+7 gün: "Last week, 3 patients from [ülke] visited us [before/after]"
→ Sıcak lead → doktora/koordinatöre bildirim
```

**Para etkisi:**
```
Dönüşüm %12 → %20'ye çıkarsa
20 lead × %8 artış = 1.6 ek hasta/gün
1.6 × 80.000 TL ort. tedavi = 128.000 TL/gün
Aylık çalışma günü 25 → 3.200.000 TL/ay ek gelir potansiyeli

(Gerçekçi: her lead her gün gelmez, ama ayda %30 artış bile =
 300.000+ TL/ay ek gelir. Invekto fiyatı 25.000 TL → 12x ROI)
```

> **Hormozi:** *"Medical tourism en yüksek değerli niche. Bir saç ekimi hastası 5.000 USD bırakıyor. İlk cevap veren kliniğe gidiyor. Gece 3'te Suudi'den mesaj geldiğinde 30sn'de AI cevap veriyorsan, sen kazanırsın. 8 saat bekletirsen, rakip kazanır."*

---

### S10: Google Yorum Toplama + Referans Motoru (Review Engine)

**Sahne:**

Dr. Ayşe'nin Google'da 4.2 yıldız puanı var. Rakip klinik 4.8. Bu 0.6 fark → Google aramalarda **%40 daha az tıklama**. Hasta klinik seçerken ilk baktığı yer: Google yorumlar.

Her ay 150+ hasta tedavi oluyor. Yorum bırakan: 5-8 hasta (%5). Ve genellikle kötü deneyim yaşayanlar yazıyor → puan düşüyor. Memnun hastalar sessiz kalıyor.

**Bugün ne oluyor:**
```
→ Resepsiyonist bazen "bizi değerlendirir misiniz?" diyor
→ Çoğu hasta "tamam" deyip unutuyor
→ Kötü deneyim yaşayan hemen yazıyor → puan düşüyor
→ Yorum toplama sistematik değil
```

**Invekto ile:**
```
→ Tedavi sonrası otomatik memnuniyet anketi (WhatsApp'tan):
  T+3 gün: "Tedavinizden memnun kaldınız mı? (1-5 puan)"
→ Puan 4-5 ise:
  "Çok mutlu olduk! Deneyiminizi Google'da paylaşır mısınız? [link]"
→ Puan 1-3 ise:
  "Üzgünüz! Sorununuzu çözmek istiyoruz. Ne oldu?"
  → İç eskalasyon → sorun çözülsün, kötü yorum önlensin
→ Referans motoru:
  T+30 gün: "Bir yakınınız tedaviye ihtiyaç duyarsa,
   sizi yönlendirdiğinizde %10 indirim hediyemiz olsun."
→ Dashboard: yorum oranı, ortalama puan trendi, referans sayısı
```

**Para etkisi:**
```
Yorum oranı %5 → %20'ye çıkarsa (memnun hastalar da yazarsa)
Google puanı 4.2 → 4.6 çıkar → tıklama oranı %30 artar
Günde 5 ek lead × %20 dönüşüm × 3.000 TL = 3.000 TL/gün
= 90.000 TL/ay ek gelir (Google görünürlük artışından)

+ Referans programı: ayda 5 referans hasta × 3.000 TL = 15.000 TL/ay
Toplam: 105.000 TL/ay. Invekto 10.000 TL → 10.5x ROI
```

> **Hormozi:** *"Her işletmenin en ucuz müşteri edinme kanalı referans. Ama kimse sistematik olarak istemiyor. 'Bizi değerlendirir misiniz' demek vs. '3 gün sonra WhatsApp'tan memnuniyet anketi → yorum linki → referans teklifi' çok farklı şeyler. Biri umut, diğeri sistem."*

---

## Kritik Bulgu: OUTBOUND ENGINE Eksik

```
MEVCUT ROADMAP = %100 INBOUND
  Müşteri yazar → AI cevap verir

EKSİK OLAN = OUTBOUND
  AI proaktif olarak müşteriye ulaşır

10 senaryodan 7'si OUTBOUND gerektiriyor:
  S1  Yorum kurtarma        → Outbound (AI müşteriye ulaşır)
  S3  İade çevirme           → Inbound (müşteri yazar) + Outbound (follow-up)
  S4  Sipariş sonrası satış  → Outbound (AI ilk mesajı atar)
  S7  No-show önleme         → Outbound (hatırlatma)
  S8  Tedavi sonrası takip   → Outbound (kontrol soruları)
  S9  Medikal turizm         → Inbound + Outbound (follow-up)
  S10 Yorum toplama          → Outbound (rica mesajı)

OUTBOUND ENGINE olmazsa:
  → Gelir potansiyelinin %70'i kilitli kalır
  → Sadece "soruya cevap veren AI" olursun (commodity)
  → Hormozi'nin "proaktif değer yarat" ilkesi ihlal edilir

OUTBOUND ENGINE = Phase 2-3'te kritik bileşen
```

---

## Outbound Engine Gereksinimleri

```
Outbound Engine (Phase 2'de temel, Phase 3'te tam):

1. Trigger Engine
   ├── Event-based: sipariş teslim edildi, yorum geldi, randevu yaklaşıyor
   ├── Time-based: T+Xh/Xd delay kuralları
   ├── Condition-based: "sadece 1-2 yıldız yorum ise"
   └── Tenant-bazlı ON/OFF ve kural özelleştirme

2. Template Engine
   ├── Değişkenli mesaj şablonları ({müşteri_adı}, {ürün}, {tarih})
   ├── Tenant-bazlı şablon yönetimi
   ├── AI-generated personalization (opsiyonel — Phase 3+)
   └── Multi-language template desteği (Phase 3-4: TR/EN/AR)

3. Delivery & Compliance
   ├── WhatsApp Business API rate limiting (24h window kuralı)
   ├── Opt-out yönetimi ("STOP" → unsubscribe)
   ├── Gönderim kuyruğu + retry logic
   ├── Delivery status tracking (sent/delivered/read/failed)
   └── KVKK/GDPR uyumlu consent tracking

4. Analytics
   ├── Gönderim başarı oranı
   ├── Okunma/cevaplanma oranı
   ├── Conversion tracking (mesaj → aksiyon)
   └── ROI per campaign/trigger
```

---

> Phase detaylarında bu senaryoların hangi adımlarda implemente edildiği: [roadmap-phases.md](roadmap-phases.md)
