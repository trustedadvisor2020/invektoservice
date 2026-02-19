# AI İnovasyon Ürünleri — Geleceğin Invekto Deneyimi

> **Kapsam:** Invekto'nun mevcut altyapısı üzerine inşa edilen ileri AI ürünleri
> **Durum:** Phase 3B/3C/3D — niche derinleştirme ve yeni gelir kanalları
> **Hedef:** Her bir ürün bağımsız SaaS olarak da satılabilir, Invekto ekosistemiyle birlikte ise çarpan etkisi yaratır
> **Anahtar Vaat:** "Rakiplerin 2-3 yıl sonra yapacağını bugün yap"

---

## Neden AI İnovasyon?

Invekto'nun temel değeri mesaj yönetimi ve otomasyon. Ama müşteriler sadece mesaj okuyup yazmaktan daha fazlasını istiyor. Bir estetik klinik "hastam selfie göndersin, AI analiz etsin" diyor. Bir e-ticaret mağazası "müşterim fotoğraf göndersin, ürünü bulsun" istiyor. Bir diş kliniği "gece gelen sesli mesajı dinlemeden anlamak istiyorum" diyor.

Bu istekler hayal değil — teknoloji hazır, pazar hazır, tek eksik entegrasyon. Invekto'nun WhatsApp altyapısı, CRM'i ve AI motorları bu ürünleri dakikalar içinde mevcut iş akışlarına entegre edebilir. Rakipler bu ürünlerden birini bağımsız olarak sunarken, Invekto hepsini birbirine bağlı bir ekosistem olarak sunuyor.

Bu dokümandaki 6 AI ürünü, sektörel senaryolardaki "Phase 3" etiketli maddelerin teknik karşılığıdır.

---

## 1. Sesli Mesaj AI — Dinlemeden Anlama

### Sorun

WhatsApp'ta sesli mesaj göndermek yazı yazmaktan daha kolay — özellikle acelesi olan, yazmaktan hoşlanmayan veya sorununu anlatırken detay vermek isteyen müşteriler için. Ama işletme tarafında sesli mesaj bir kabus:

- Agent 30 saniyelik sesli mesajı dinliyor — ama belki mesaj 2 dakika
- Gürültülü ortam, aksanlı konuşma — anlaşılmıyor
- Sesli mesaj aranabilir değil — ne sorulduğu kaybolup gidiyor
- Yoğun saatlerde 15 sesli mesaj birikiyor — agent hepsini dinleyecek mi?

### Kim İçin?

- E-ticaret: Müşteri beden/renk sorununu sesli anlatıyor
- Klinikler: Hasta semptomlarını sesli mesajla tarif ediyor
- Medikal turizm: Yabancı hasta uzun danışma mesajı gönderiyor (Arapça, Rusça, İngilizce, Almanca)
- Oteller: Misafir sesli mesajla taleplerini iletiyor

### Nasıl Çalışıyor?

1. Müşteri WhatsApp'tan sesli mesaj gönderir
2. Sistem sesli mesajı algılar ve transkripsiyon motoruna (OpenAI Whisper) yönlendirir
3. 30 saniyelik mesaj 2-3 saniyede yazıya çevrilir — çok dilli destek (Türkçe, İngilizce, Arapça, Rusça, Almanca)
4. Yazıya çevrilen metin mevcut Invekto AI pipeline'ına girer: niyet tespiti + duygu analizi
5. Agent ekranında sesli mesajın yanında transkript ve niyet etiketi görünür
6. AI cevap önerisi hazırlar — agent dinlemeden anlayıp cevaplar

**Bonus:** Ses tonundan duygu analizi — öfkeli müşteri tespit edilir, öncelikli kuyruğa alınır.

> 💡 **Vay Be Anı:** Pazartesi sabahı. E-ticaret mağazanıza hafta sonu 47 sesli mesaj gelmiş. Eskiden pazartesi sabahı 47 mesajı tek tek dinlerdiniz — 2 saat. Simdi hepsi transkript edilmis, niyetleri etiketlenmis, 12'sine AI otomatik cevap vermis. Siz kahvenizi icerken dashboard'da yaziyor: "47 sesli mesaj islendi. 12 otomatik cevaplandi. 8 iade talebi. 3 beden sorusu. Kalan 24 mesaj agent'a atandi." 2 saatlik isi 0 saniyede hallettiniz — hem de bunu hafta sonu, magaza kapaliyken. Rakipleriniz pazartesi sabahi hala 47 mesaji dinlemeye baslamadi bile.

> 💡 **Vay Be Anı:** Medikal turizm kliniginizdesiniz. Rusca 2 dakikalik bir sesli mesaj geldi — icinde hasta semptomlarini, gecirdigi operasyonlari, beklentilerini anlatiyor. Normalde bunu dinleyecek Rusca bilen biri lazim, o kisi izinli, mesaj 3 gun cevapsiz kalacak. Simdi? 3 saniyede Turkce transkript hazir. AI niyeti cikartti: "Burun estetigi revizyonu, onceki operasyondan memnuniyetsiz, fiyat soruyor." Agent hic Rusca bilmeden, mesaji okuyup 1 dakikada yanitladi. Hasta: "Bu klinik gece 11'de 3 saniyede cevap verdi, digerleri 3 gundur sessiz." Hasta sizin oldu — 15.000 Euro degerinde.

> 💡 **Vay Be Anı:** Gece 2'de bir ses mesaji geldi. Ama siz uyuyorsunuz, agent'lariniz uyuyor, herkes uyuyor. Fark etmez — Sesli Mesaj AI uyumuyor. Mesaji 2,7 saniyede transkript etti, niyeti cikartti ("beden degisimi istiyorum"), sinirlenmis oldugunu ses tonundan anladi, onceliklendirdi ve otomatik kurtarma mesaji gonderdi. Siz sabah uyandığınızda dashboard'da yazıyor: "Gece 02:14 — 1 sinirli musteri tespit edildi, otomatik yatistirildi, beden degisimi baslatildi." 0 insan müdahalesi. Rakiplerinizin gece nöbetçi agent maliyeti: ayda 12.000 TL. Sizin maliyetiniz: 0.

### Maliyet ve Getiri

- **Birim maliyet:** ~0,006 $ / mesaj (30 saniye ortalama)
- **Zaman tasarrufu:** Agent günde 2,5-4 saat kazanır (sesli mesaj dinleme yerine okuma)
- **Fiyatlandırma:**
  - Başlangıç: 19 $/ay (1.000 mesaj, sadece Türkçe)
  - Büyüme: 49 $/ay (5.000 mesaj, Türkçe + İngilizce)
  - Profesyonel: 99 $/ay (15.000 mesaj, tüm diller)

### Invekto Servisleri

| Servis | Rol |
|--------|-----|
| Automation (:7108) | Yeni tetikleyici: "sesli mesaj geldi" |
| AgentAI (:7105) | Transkript → niyet tespiti + cevap önerisi |
| ChatAnalysis (:7101) | Ses tonu duygu analizi |

---

## 2. Yüz Analizi AI — Selfie'den Konsültasyona

### Sorun

Estetik kliniğe en çok gelen mesaj: "Burnuma ne yapılabilir?", "Yüzümde kırışıklıklar var, ne önerirsiniz?", "Dolgu mu yoksa botoks mu?". Doktor bu soruların her birine 5-10 dakika ayırıyor — ama çoğu randevuya dönmüyor çünkü müşteri "sadece merak ediyordum" düzeyinde.

Asıl fırsat: Gece 2'de, hafta sonu, tatil günü — kliniğin kapalı olduğu saatlerde gelen sorular. Bu sorulara cevap verilmezse müşteri sabah rakip kliniği arar.

### Kim İçin?

- **Birincil:** Estetik klinikler (burun estetiği, dolgu, botoks, cilt tedavileri)
- **İkincil:** Diş klinikleri (gülüş tasarımı, diş beyazlatma)
- Saç ekimi klinikleri, dermatologlar

### Nasıl Çalışıyor?

1. Müşteri WhatsApp'tan selfie gönderir
2. AI yüzü algılar ve 468 farklı noktayı haritalandırır (MediaPipe Face Mesh)
3. Bölge analizi yapılır: kırışıklık, hacim kaybı, asimetri, cilt kalitesi
4. Claude Vision ile doğal dilde estetik değerlendirme oluşturulur
5. Kliniğin tedavi kataloğuyla eşleştirme: "Size uygun işlemler: dudak dolgusu, göz altı dolgusu"
6. Fiyat bilgisi + randevu linki + etik uyarı ile birlikte gönderilir
7. Tüm bunlar 5-8 saniyede tamamlanır

**Etik Koruma:** Her raporda "Bu AI destekli ön değerlendirmedir. Kesin tedavi planı doktor muayenesinde belirlenir" notu bulunur. AI asla gereksiz işlem önermez.

> 💡 **Vay Be Anı:** Cumartesi gece 23:00. Estetik kliniginizin WhatsApp'ina bir selfie dustu. Normalde ne olurdu? Mesaj pazartesi sabahina kadar beklerdi. Resepsiyon bakardi, doktora iletirdi, doktor 5 dakika ayirirdi, cevap sali günü giderdi. Hasta o zamana kadar 5 klinige daha yazmis olurdu. Simdi? 8 saniyede kisiye ozel analiz raporu hazir: "Yüz analizi tamamlandi. Onerilen islemler: dudak dolgusu (asimetri duzeltme, 4.000-6.000 TL), goz alti dolgusu (hacim kaybi, 3.500-5.000 TL). Ucretsiz on gorusme: [randevu linki]." Hasta pazar sabahi klinige geldiginde: "Daha konusmadan her seyi anladilar." Rakip klinikler hala pazartesi sabahi mesaji gormedi bile.

> 💡 **Vay Be Anı:** Klinik sahibisiniz, aylik raporu aciyorsunuz. Eskiden 200 sorgu geliyordu, 30'u randevuya donuyordu. Simdi ayni 200 sorgudan 80'i randevuya dondu — cunku selfie gonderenler 8 saniyede kisisel analiz aldı, "bu klinik ciddi" dedi ve geldi. Randevuya donus %15'ten %40'a cikti. Fazladan 50 islem x 15.000 TL = ayda 750.000 TL. AI maliyeti? 10.000 TL. ROI: 45 kat. Rakip klinikler hala "doktor musait olunca bakar" diyor — siz 8 saniyede bakiyorsunuz.

> 💡 **Vay Be Anı:** Istanbul'a tatile gelmis bir turist, otel odasinda aynaya bakiyor, "su kirişikliklara bir sey yaptirmali miyim?" diye dusunuyor. Gece 01:30. Kliniginizin Instagram reklamini gormus, WhatsApp'a selfie atiyor. 8 saniye sonra Ingilizce, kulturel olarak uygun bir analiz raporu aliyor: tedavi onerileri, fiyat araligi, "yarin sabah 10:00'da ucretsiz konsultasyon" butonu. Turist heyecanla randevu aliyor. Sabah geldiginde doktor ekraninda selfie analizi hazir — konsultasyon suresi 15 dakikadan 5 dakikaya dustu. Hasta: "Turkiye'deki klinikler teknolojide 10 yil ileride." Bu hasta arkadaslarina anlatacak — ucretsiz reklam.

### Maliyet ve Getiri

- **Birim maliyet:** ~0,07-0,20 $ / analiz
- **Dönüşüm etkisi:** Mesajdan randevuya dönüşüm %15'ten %40+'ya çıkar
- **Gelir etkisi örneği:**
  - Önce: 200 sorgu/ay → %15 randevu → 30 randevu → 20 işlem × 15.000 ₺ = 300.000 ₺
  - Sonra: 200 sorgu/ay → %40 randevu → 80 randevu → 50 işlem × 15.000 ₺ = 750.000 ₺
  - **ROI: 45 kat** (AI maliyeti ~10.000 ₺/ay)
- **Fiyatlandırma:**
  - Başlangıç: 79 $/ay (200 analiz)
  - Büyüme: 199 $/ay (1.000 analiz)
  - Profesyonel: 399 $/ay (5.000 analiz)

### Genişleme Potansiyeli

- Gülüş analizi (diş klinikleri)
- Saç analizi (saç ekimi klinikleri)
- Vücut analizi (kozmetik cerrahi)
- Cilt hastalığı analizi (dermatologlar)

### Invekto Servisleri

| Servis | Rol |
|--------|-----|
| FaceAnalysis (:7110) | Yüz algılama + bölge analizi + değerlendirme |
| Automation (:7108) | Chatbot akışı: "Selfie gönderin" → analiz tetikleme |
| AgentAI (:7105) | Tedavi eşleştirme + randevu önerisi |
| Knowledge (:7104) | Klinik tedavi kataloğu + fiyat listesi |

---

## 3. Görsel Ürün Arama (Visual Product Search) — Fotoğraftan Satışa

### Sorun

Müşterinin Instagram'da gördüğü bir elbiseyi satmak istiyorsunuz. Ama müşteri "şu elbise var mı?" diye fotoğraf gönderdiğinde agent 3-5 dakika katalogda arar, bulamaz veya yanlış ürün önerir. Modada her dakika önemli — müşteri "bulamıyorlar" diye başka mağazaya gider.

### Kim İçin?

- E-ticaret (moda, giyim, aksesuar — görsel ağırlıklı kategoriler)
- Instagram'dan lead alan mağazalar
- Trendyol/Hepsiburada satıcıları

### Nasıl Çalışıyor?

1. Müşteri WhatsApp veya Instagram DM'den ürün fotoğrafı / ekran görüntüsü gönderir
2. Sistem görüntüyü alır, Instagram UI elemanlarını temizler
3. CLIP modeli ile görsel embedding oluşturulur
4. Mağazanın ürün kataloğunda benzerlik araması yapılır (Qdrant vektör veritabanı)
5. **Birebir eşleşme:** "Bu ürün: Kırmızı Midi Elbise #12345. Stok: S ✅ M ✅ L ❌ XL ✅. Fiyat: 899 TL"
6. **Benzer ürünler:** En yakın 5 alternatif ürün kartı olarak sunulur
7. Stok, beden, renk, fiyat bilgisi canlı olarak çekilir

> 💡 **Vay Be Anı:** Musteri Instagram'da bir influencer'in giydigi elbiseyi gordu. Story'nin ekran goruntusunu aldi, WhatsApp'a gonderdi. 4 saniye sonra cevap geldi: "Bu urun stogumuzda! Kirmizi Midi Elbise, 899 TL. S ve M beden mevcut. Siparis linki: ..." Musteri saskin: "Instagram'da 10 dakika aradim, marka etiketini bile bulamadim. Siz 4 saniyede buldunuz!" Normalde agent bu urunu katalogda 5 dakika arardi — belki de bulamazdi. Simdi AI 4 saniyede 50.000 urunluk katalogu taradi, buldu, stok ve fiyat bilgisiyle sundu. Rakipleriniz hala "bir bakalim" yazarken, siz siparis linkini gondermistiniz bile.

> 💡 **Vay Be Anı:** Cuma aksami saat 22:00. Magaza kapali, agent'lar eve gitmis. Ama WhatsApp'a foto geliyor — bir musteri Pinterest'te gordugu cizme fotoğrafını gonderdi. 4 saniyede AI cevap verdi: "Bu urun veya cok benzeri stogumuzda! 2 secenek bulundu." Urun kartlari, fiyatlar, stok durumlari — hepsi hazir. Musteri gece 22:03'te siparis verdi. Siz sabah geldiginizde kasada 1.200 TL var. Bu satis, Gorsel Urun Arama olmasa kaybolacakti — cunku musteri sabaha kadar unutacakti veya baska mağazadan alacakti. Rakiplerinizin gece satisi: 0 TL. Sizin gece satısınız: 7/24 acik.

> 💡 **Vay Be Anı:** Aylik raporunuzu inceliyorsunuz. Gorsel Urun Arama ile gelen sorgularin %72'si satisa dondu — cunku musteri zaten o urunu istiyor, sadece sizde olup olmadığını soruyordu. Metin tabanli aramalarda bu oran %25. Neden? Cunku musteri "kirmizi uzun elbise" yazinca 40 sonuc cikiyor, kayboluyordu. Foto atinca TAMAMI o urunu istiyor, AI bire bir eslestirdi. Ayda 800 gorsel sorgu x %72 donus x 650 TL ortalama sepet = 374.400 TL ek gelir. AI maliyeti: 2.000 TL. Syte veya ViSenze gibi rakip cozumler ayda 5.000$+ istiyor — siz 29$/ay oduyorsunuz.

### Maliyet ve Getiri

- **Birim maliyet:** ~0,05-0,15 $ / arama
- **Rakip fiyatlandırma:** Syte, ViSenze gibi çözümler 5.000+ $/ay — Invekto 29 $/ay'dan başlıyor
- **Fiyatlandırma:**
  - Başlangıç: 29 $/ay (500 arama)
  - Büyüme: 79 $/ay (2.000 arama)
  - Profesyonel: 199 $/ay (10.000 arama)

### Size/Fit AI Sinerjisi

Ürün bulundu → "Boyunuz ve kilonuz nedir?" → Akıllı Beden Önerisi → "L beden, bu bedeni alanların %95'i memnun" → Sipariş linki ile birlikte. Arama + beden = tam otomatik satın alma yolu.

### Invekto Servisleri

| Servis | Rol |
|--------|-----|
| VisualSearch (:7111) | Görsel embedding + vektör arama + eşleştirme |
| Automation (:7108) | Chatbot akışı: görüntü algılama → VPS çağrısı |
| Integrations (:7106) | Ürün kataloğu senkronizasyonu (API, XML feed, CSV) |
| AgentAI (:7105) | Sonuç zenginleştirme + cross-sell önerisi |

---

## 4. Akıllı Beden Önerisi (Size/Fit AI) — İadeden Kurtulma

### Sorun

E-ticaretin en büyük maliyet kalemlerinden biri iade. Türkiye'de ortalama iade oranı %30-40, bunun %60-70'i beden uyumsuzluğu. Her iade = kargo maliyeti + işçilik + müşteri memnuniyetsizliği + stok karışıklığı.

Müşteri "M mi L mi alsam?" diye sorar. Agent "beden tablosuna bakın" der. Müşteri beden tablosunu yanlış okur, M alır, küçük gelir, iade ister. Bu döngü her gün yüzlerce kez tekrarlanır.

### Kim İçin?

- E-ticaret (giyim, ayakkabı, aksesuar)
- WhatsApp üzerinden beden sorusu soran müşteriler

### Nasıl Çalışıyor?

1. Müşteri: "170 cm, 65 kg, normalde M giyiyorum. Bu elbise için ne almalıyım?"
2. AI boy + kilo + cinsiyet/yaş bilgisinden tahmini vücut ölçülerini hesaplar
3. Ürünün beden tablosu ile karşılaştırma yapılır
4. Geçmiş iade verileri devreye girer: "Bu üründe M alanların %38'i iade etti — beden küçük çıkıyor"
5. Sonuç: "Size L beden öneriyoruz. Bu bedeni alan müşterilerin %95'i memnun kalmıştır."
6. Sosyal kanıt + veri destekli öneri = müşteri güvenle alır

> 💡 **Vay Be Anı:** Magaza sahibisiniz. Her ay iade raporunu actiginizda mideniz bulaniyordu: 1.137 iade, 136.440 TL kayıp, kargocu arayip durma, depoda biriken iade kutulari... Akilli Beden Onerisi'ni actiniz. Ilk ayin sonunda rapora bakiyorsunuz: iade sayisi 1.137'den 487'ye dustu. Kayip 136.440 TL'den 58.500 TL'ye indi. Ayda 77.940 TL cebinizde kaldi. 3 ayda 233.820 TL. Bir yilda 935.280 TL. AI maliyeti? Ayda 5.000 TL. Iadelerin yarisi ortadan kayboldu — ve musteriler "bu magaza bedeni tam biliyor" diye tekrar tekrar geliyor. Rakipleriniz hala "beden tablosuna bakin" yaziyor.

> 💡 **Vay Be Anı:** Gece 23:45. Musteri WhatsApp'a yazdi: "170cm, 65kg, normalde M giyiyorum. Bu elbise icin ne almaliyim?" 3 saniye sonra cevap geldi: "Size L beden oneriyoruz. Bu bedeni alan musterilerin %95'i memnun kalmistir. M beden alanların %38'i iade etmis — beden kucuk cikiyor." Musteri: "Bu kadar detayli bilgi veren baska magaza gormedim. Hem de gece 12'ye yakin!" L beden siparis etti, iade etmedi, 2 hafta sonra ayni magazadan 3 urun daha aldi. Neden? Cunku guven kuruldu — "bu magaza beni yaniltmaz" hissi. Rakip magazada ayni soruya cevap: "Beden tablomuza bakabilirsiniz: [link]." Musteri linke tiklar, tablo karisik, M alır, kucuk gelir, iade eder, bir daha o magazadan alismaz.

> 💡 **Vay Be Anı:** "En Cok İade Edilen Urunler" dashboard'unu aciyorsunuz. AI size gosteriyor: "Marka X, Model Y elbisede M beden alanların %52'si iade etmis. Sebep: gogus olcusu dar." Bu veriyi tedarikciye gonderdiniz, beden standartlarini guncellediler. Sonraki siparis partisinde o urunde iade %52'den %11'e dustu. Sadece bir urunun beden duzeltmesiyle yilda 45.000 TL tasarruf. Bu bilgi Akilli Beden Onerisi olmasaydi asla ortaya cikmayacakti — agent'lar "M beden iade" diye not alip geciyordu, kimse pattern'i goremiyordu. Simdi AI goru.

### Maliyet ve Getiri

- **Birim maliyet:** ~0,004 $ / öneri (API çağrısı yok, hesaplama tabanlı)
- **İade azaltma etkisi:**
  - Önce: 5.000 sipariş/ay × %35 iade × %65 beden kaynaklı = 1.137 iade × 120 ₺/iade = 136.440 ₺/ay kayıp
  - Sonra: Aynı hacim × %15 iade × %65 = 487 iade = 58.500 ₺/ay
  - **Tasarruf: 77.940 ₺/ay, AI maliyeti: 5.000 ₺ = ROI: 15,6 kat**
- **Fiyatlandırma:**
  - Başlangıç: 29 $/ay (1.000 öneri)
  - Büyüme: 79 $/ay (5.000 öneri + iade veri entegrasyonu)
  - Profesyonel: 199 $/ay (20.000 öneri + fotoğraf analizi)

### "En Çok İade Edilen Ürünler" Dashboard

Bonus özellik: Hangi ürünlerin en çok iade edildiği, hangi bedenlerin sorunlu olduğu raporlanır. Bu veri satın alma kararlarını etkiler — tedarikçiyle beden standardizasyonu sağlanır.

### Invekto Servisleri

| Servis | Rol |
|--------|-----|
| Automation (:7108) | Chatbot: beden sorusu niyeti → Size AI tetikleme |
| Integrations (:7106) | Ürün beden tablosu + iade veri entegrasyonu |
| Outbound (:7107) | Teslimat sonrası "Bedeniniz uydu mu?" takip mesajı |

---

## 5. Olumsuz Yorum Önleme AI (Review Rescue) — Hasar Oluşmadan Müdahale

### Sorun

Memnuniyetsiz müşteri genellikle iki şey yapar: Ya WhatsApp'tan şikayet eder, ya da direkt Google/Trendyol'a kötü yorum yazar. İkinci seçenekte artık çok geçtir — kötü yorum orada kalır ve potansiyel müşterileri kaçırır.

Ama şikayet WhatsApp'tan geldiğinde, hasar henüz oluşmamıştır. Eğer müşteriyi bu aşamada yakalayıp memnun edebilirseniz, kötü yorum hiç yazılmaz. Hatta bazen "sorunum çözüldü, teşekkürler" diye olumlu yorum bile yazılır.

### Kim İçin?

- E-ticaret (marketplace satıcıları için hayati — puan = görünürlük)
- Klinikler (Google puanı = hasta akışı)
- Oteller (Google/Booking puanı = doluluk)

### Nasıl Çalışıyor?

1. **Algılama:** Mevcut ChatAnalysis servisi mesajdaki duygu durumunu ve anahtar kelimeleri analiz eder: "iade", "şikayet", "yorum yazacağım", "berbat"
2. **Risk skoru hesaplama:** Duygu × 30 + anahtar kelime × 25 + zamanlama × 20 + cevap gecikmesi × 15 + geçmiş × 10 = 0-100
3. **Risk seviyeleri:**
   - 🟢 Düşük (0-30): Normal akış
   - 🟡 Orta (30-60): Agent'e öncelik uyarısı
   - 🟠 Yüksek (60-80): Otomatik kurtarma mesajı + yönetici uyarısı
   - 🔴 Kritik (80-100): Tam iade + indirim + müdür devreye girer
4. **Kurtarma stratejileri:** Özür + çözüm seçenekleri (indirim kodu, ücretsiz iade kargоsu, ekspres değişim, tam iade + gelecek alışveriş indirimi)
5. **Takip:** 24 saat sonra "Memnun kaldınız mı?", 48 saat sonra "Değerlendirmenizi paylaşır mısınız?"

> 💡 **Vay Be Anı:** Cuma aksami 21:30. Müsteri ofkeyle yazdi: "Urun berbat geldi, yarin sabah ilk is Trendyol'a 1 yildiz yazacagim!" Normalde ne olurdu? Agent eve gitmis, mesaj pazartesi sabahina kadar beklerdi. Pazartesi sabahi musteri coktan 1 yildiz yazmis, 3 arkadasina "su magazadan almayin" demis. Simdi? AI 30 saniyede risk skorunu hesapladi: 87 — KRITIK. Otomatik kurtarma devreye girdi: "Cok uzgunuz! Sorununuzu hemen cozmek istiyoruz. Size ozel secenekler: (1) Ucretsiz iade + tam geri odeme, (2) Ekspres degisim — yarin kapinizda, (3) %25 indirim kodu + ucretsiz iade." Musteri 22:00'de "ekspres degisim istiyorum" dedi. Pazar günü yeni urun elinde. Trendyol'a 1 yildiz yerine 5 yildiz yazdi: "Sorunum aninda cozuldu, harikulade hizmet!" Bu gorünmez kalkan, hic kimsenin fark etmedigi ama isletmenizi koruyan bir kaledir.

> 💡 **Vay Be Anı:** Ay sonu raporunu aciyorsunuz. Dashboard'da yaziyor: "Bu ay 23 olumsuz yorum YAZILMADAN onlendi. Tahmini kurtarilan satis etkisi: 165.000 TL." Detaya tikladiginizda goruyorsunuz: 23 musteri ofkeyle yazmis, 16'si otomatik kurtarmayla cozulmus, 7'si agent'a yonlendirilmis, hepsi memnun edilmis. 0 olumsuz yorum. Google puaniniz 4.2'den 4.6'ya cikmis — cunku onceden kimsenin yakalayamadigi o sinirli musteriler simdi AI tarafindan daha yazmadan tespit ediliyor. Rakiplerinizin Google puani? 3.8 — her ay 5-10 olumsuz yorum yiyor ve hic haberleri yok.

> 💡 **Vay Be Anı:** Dis kliniginizde bir hasta "implant hareket ediyor, cok korkuyorum" yazdi. Duygu analizi: panik + korku, risk skoru: 75. Normal sartlarda hasta cevap beklerken Google'a "dis implant basarisizlik" arar, korkar, "bu klinige gitmeyin" yorumu yazar. Simdi? 45 saniyede AI devreye girdi: "Anladim, endiselenmenizi cok iyi anliyoruz. Dr. Mehmet size 30 dakika icinde donecek. Bu arada: hafif hareket ilk haftalarda normal olabilir, ama kesinlikle kontrol edilmeli." Hasta rahat nefes aldi. Dr. Mehmet aradi, kontrol randevusu verildi. Hasta geldi, sorun kucuktu, cozuldu. 1 ay sonra Google'a 5 yildiz yazdi: "Gece bile ilgileniyorlar." Onlenmeyen 1 yildizin maliyeti? Yillik 200.000-500.000 TL hasta kaybi. AI maliyeti? Neredeyse sifir.

### Maliyet ve Getiri

- **Birim maliyet:** ~0,05-0,15 $ / kurtarma girişimi
- **Etki hesabı:**
  - Ayda 50 riskli müşteri, %70 kurtarma oranı = 35 önlenen olumsuz yorum
  - Her önlenen yorum = 50.000-200.000 ₺ satış etkisi (görünürlük kaybının önlenmesi)
  - Kurtarma maliyeti: 50 × 150 ₺ ortalama (indirim/iade) = 7.500 ₺
  - **Net fayda: 27.500-132.500 ₺/ay**

### Oyun Teorisi Koruması

"Kızarsam indirim alırım" taktiğine karşı: Müşteri geçmişi kontrol edilir, tekrarlayan agresif davranış tespit edilir, kurtarma bütçesi müşteri bazında sınırlandırılır.

### Invekto Servisleri

| Servis | Rol |
|--------|-----|
| ChatAnalysis (:7101) | Duygu analizi + risk skorlama |
| AgentAI (:7105) | Yeni niyetler: "iade_tehditi", "yorum_tehditi" |
| Outbound (:7107) | Kurtarma mesaj şablonları + takip otomasyonu |
| Backend (:5000) | "Yüksek Risk" rozeti + yönetici uyarıları |

---

## 6. Çok Dilli Medikal Turizm Asistanı — 7/24 Dünyaya Açık Klinik

### Sorun

Türkiye'nin medikal turizm geliri 3,48 milyar doları aşıyor. İstanbul, Antalya ve İzmir'deki estetik ve diş klinikleri, Körfez ülkeleri, Avrupa ve Rusya'dan yoğun talep alıyor. Ama bu talebin büyük kısmı ajanslar üzerinden geliyor — %30-40 komisyon.

Neden? Çünkü Dubai'den gece 2'de Arapça mesaj gelen klinikte kimse cevap veremiyor. Londra'dan "how much for rhinoplasty?" yazan hastaya resepsiyon Google Translate ile cevap veriyor — yanlış çeviri, yanlış bilgi, güven kaybı.

"İlk cevap veren kliniği %70 oranında tercih eder" araştırması var. Gece 2'de 8 saniyede Arapça cevap veren klinik, sabah 9'da Türkçe cevap veren klinikten çok önde.

### Kim İçin?

- **Birincil:** Estetik klinikler (burun estetiği, dolgu, veneer — yüksek değerli işlemler)
- **İkincil:** Diş klinikleri (veneer, implant, gülüş tasarımı)
- Saç ekimi klinikleri, göz cerrahisi

### Nasıl Çalışıyor?

1. Hasta herhangi bir dilde mesaj gönderir (İngilizce, Arapça, Rusça, Almanca, Fransızca)
2. Sistem dili otomatik algılar ve medikal turizm pipeline'ına yönlendirir
3. Hastanın niyeti çıkarılır: tedavi sorgusu, fiyat sorusu, paket sorusu, müsaitlik kontrolü, fotoğraf konsültasyonu
4. Kliniğin tedavi veritabanından bilgi çekilir (RAG — Knowledge servisi)
5. **Kültürel uyumlama:** Her dil için farklı ton
   - Arapça: Resmi ve saygılı, "hayırlı günler" ile başlangıç
   - İngilizce: Profesyonel ve rahat
   - Rusça: Detaylı ve teknik
   - Almanca: Formal ve kesin
6. **Para birimi:** Hastanın ülkesine göre EUR/USD/GBP gösterimi
7. **Paket sunumu:** Tedavi + otel + transfer = tek fiyat paketi
8. **Personel görünümü:** Orijinal (yabancı dil) + Türkçe çeviri + AI cevap önerisi + lead sıcaklığı

> 💡 **Vay Be Anı:** Gece 02:00, Istanbul. Kliniginiz kapali, herkes uyuyor. Dubai'den bir hasta Arapca sesli mesaj gonderiyor: "Burun esteti̇gi̇ fi̇yati ne kadar? Otel dahil mi?" 3 saniyede Sesli Mesaj AI transkript ediyor. 5 saniye sonra Cok Dilli Asistan Arapca, kulturel olarak uygun bir cevap gonderiyor: fiyat (USD cinsinden), otel + transfer paketi, randevu linki. Hasta 02:03'te randevu aliyor. Toplam sure: 3 dakika. Insan mudahalesi: 0. Ajans komisyonu: 0 Euro. Normalde bu hasta icin ajansa %35 komisyon oderdiniz — 5.000 Euro'luk islemde 1.750 Euro. Simdi o 1.750 Euro cebinizde. Sabah kliniğe geldiğinizde dashboard'da yazıyor: "Dun gece 3 yeni uluslararasi randevu alindi. Toplam deger: 45.000 Euro. Ajans komisyonu: 0 Euro." Bu ani yaşayan klinik, Invekto'dan asla ayrilmaz.

> 💡 **Vay Be Anı:** Londra'dan bir hasta Instagram reklamınızı gordu, gece 23:30'da Ingilizce yazdi: "How much for a full set of veneers? Can I get it done in a week?" Rakip kliniklerde ne olurdu? Mesaj sabaha kadar beklerdi. Resepsiyon Google Translate ile ceviri yapardi — "20 teeth porcelain layer" gibi garip cumleler cikardi. Hasta guvenini kaybederdi. Simdi? 8 saniyede profesyonel Ingilizce cevap: "A full set of porcelain veneers (20 teeth) starts from EUR 4,800. Treatment time: 5-7 days. Your package includes: 5-star hotel (6 nights), airport transfers, dedicated patient coordinator. Would you like to schedule a free video consultation?" Hasta: "Finally, a clinic that speaks my language — literally." Ertesi gun video konsultasyon, 2 hafta sonra Istanbul'da. Deger: 8.000 Euro. Ajans maliyeti: 0.

> 💡 **Vay Be Anı:** 6 aylik raporunuzu inceliyorsunuz. Onceki 6 ay: ajanslara toplam 127.500 Euro komisyon odediniz (50 hasta x 5.000 Euro x %35 x 6 ay... hayır, aylik 50 hasta icin). Simdi son 6 ayda: ajans komisyonu 12.000 Euro'ya dustu (sadece birkac ajans hasta), direkt hasta sayisi 50'den 85'e cikti cunku 7/24 aninda cevap veriyorsunuz. Net fark: 6 ayda 424.500 Euro ek gelir. Yillik 849.000 Euro. AI maliyeti? Ayda 500 Euro. Rakip klinikler hala ajansa %35 oduyor, gece gelen mesajlara sabah donuyor, ve "neden hasta sayimiz artmiyor?" diye toplanti yapiyor. Siz gece uyurken hasta kazaniyorsunuz.

### Maliyet ve Getiri

- **Birim maliyet:** ~0,07-0,18 $ / konuşma
- **Ajans komisyon tasarrufu:**
  - Önce (ajansla): 100 hasta × 5.000 € = 500.000 €, eksi %35 komisyon = 325.000 € net
  - Sonra (AI ile direkt): 100 mevcut + 50 yeni (7/24 hız avantajı) = 150 × 5.000 € = 750.000 €, AI maliyeti 500 €
  - **Fark: +424.500 €/ay**
- **Fiyatlandırma:**
  - Başlangıç: 99 $/ay (500 konuşma, İngilizce + 1 dil)
  - Büyüme: 249 $/ay (2.000 konuşma, İngilizce + Arapça + 1 dil)
  - Profesyonel: 499 $/ay (10.000 konuşma, 5 dil + sesli mesaj)

### Desteklenen Diller ve Pazarlar

| Dil | Hedef Pazarlar | Ortalama Hasta Değeri |
|-----|---------------|----------------------|
| İngilizce | İngiltere, ABD, Avustralya | 3.000-15.000 € |
| Arapça | BAE, Suudi Arabistan, Kuveyt | 5.000-20.000 € |
| Rusça | Rusya, Kazakistan | 2.000-10.000 € |
| Almanca | Almanya, Avusturya, İsviçre | 3.000-12.000 € |
| Fransızca | Fransa, Belçika (opsiyonel) | 2.000-8.000 € |

### Invekto Servisleri

| Servis | Rol |
|--------|-----|
| Backend (:5000) | Dil yönlendirici: TR → standart, diğer → medikal turizm pipeline |
| AgentAI (:7105) | Çok dilli niyet çıkarma + cevap üretimi |
| Knowledge (:7104) | Klinik tedavi veritabanı + fiyat + paket bilgileri (çok dilli) |
| Outbound (:7107) | Takip mesajları (hastanın dilinde) |

---

## Sinerji Haritası — Ürünler Birlikte Nasıl Çalışır?

### Senaryo A: Medikal Turizm Tam Yolculuk

```
Dubai'den hasta (gece 02:00):
  ↓ Arapça sesli mesaj gönderir
  ↓ Sesli Mesaj AI → transkript + niyet
  ↓ Çok Dilli Asistan → Arapça, kültürel cevap
  ↓ Hasta selfie gönderir
  ↓ Yüz Analizi AI → kişisel tedavi raporu (Arapça)
  ↓ Fiyat + otel paketi + randevu linki
  ↓ Hasta randevu alır
  ↓ SONUÇ: Sıfır insan müdahalesi, 02:00'de tam konsültasyon
```

> 💡 **Vay Be Anı:** Sabah 08:00, klinik aciliyor. Doktor masasina oturuyor, dashboard'u aciyor. Yaziyor: "Dun gece 02:00-04:00 arasi: 3 uluslararasi hasta tam konsultasyon tamamladi. 2 randevu alindi (Dubai + Riyad). Toplam deger: 35.000 Euro. Insan mudahalesi: 0." Doktor gozlerine inanamıyor — gece uyurken 3 hasta tum yolculugu tamamlamis: sesli mesaj transkript, yuz analizi, tedavi plani, fiyat, otel paketi, randevu. Normalde bu 3 hasta icin: 1 Arapca bilen koordinator (maas: 25.000 TL/ay), 1 doktor konsultasyonu (45 dk x 3 = 2.25 saat), ajans komisyonu (35.000 x %35 = 12.250 Euro). Simdi: 0 personel, 0 doktor zamani, 0 komisyon. 3 AI urunu birlikte calisarak gece 2'de insansiz klinik isletti.

> 💡 **Vay Be Anı:** Riyad'dan bir hasta gece 03:15'te Arapca 2 dakikalik sesli mesaj gonderdi — semptomlarini, beklentilerini, butcesini anlatiyor. Sesli Mesaj AI 3 saniyede transkript etti, Cok Dilli Asistan niyeti cikartti: "Burun esteti̇gi̇ revizyonu, önceki operasyondan memnuniyetsiz, 10.000 Euro butce." Hasta selfie gonderdi. Yuz Analizi AI 8 saniyede rapor hazirladi — Arapca, kulturel tonla. 03:18'de hasta randevu aldi. Sabah 9'da rakip klinikler mesajı gormeye baslarken, siz hastayı coktan kazanmistiniz. Bu hasta 3 arkadasina daha anlatti — hepsi geldi. 1 gece, 4 hasta, 40.000 Euro. Ajansa 0 Euro.

### Senaryo B: E-ticaret Tam Satın Alma Yolu

```
Müşteri Instagram'da elbise görür:
  ↓ Ekran görüntüsünü WhatsApp'tan gönderir
  ↓ Görsel Ürün Arama → "Kırmızı Midi Elbise #12345, 899 TL"
  ↓ Müşteri: "Hangi beden almalıyım? 170cm, 65kg"
  ↓ Akıllı Beden Önerisi → "L beden, %95 memnuniyet"
  ↓ Sipariş linki (beden önceden seçili)
  ↓ SONUÇ: Fotoğraftan satışa 60 saniye
```

> 💡 **Vay Be Anı:** Musteri Instagram'da bir elbise gordu, ekran goruntusunu WhatsApp'a atti. 4 saniyede Gorsel Urun Arama urunu buldu. Musteri "hangi beden?" dedi, Akilli Beden Onerisi 3 saniyede "L beden, %95 memnuniyet" dedi. Siparis linki beden secili geldi. Musteri tek tikla siparis verdi. Instagram story'den siparise toplam 60 saniye. Normalde ne olurdu? Agent 5 dakika katalogda arar, belki bulamazdi. Beden sorusuna "beden tablosuna bakin" derdi. Musteri tabloyu yanlış okur, M alir, kucuk gelir, iade ederdi. 60 saniye vs 3 gun iade sureci — musteri hangisini tercih eder? Ayda 200 gorsel sorgu x %72 donus x 650 TL sepet = 93.600 TL ek gelir. Iade azalmasi ile birlikte aylik toplam etki: 170.000+ TL.

> 💡 **Vay Be Anı:** Magaza sahibisiniz, haftalik raporu inceliyorsunuz. "Bu hafta Gorsel Urun Arama ile 47 satis yapildi. Akilli Beden Onerisi ile bu 47 satisin iade orani: %8. Normal satis kanaliyla iade orani: %35." Yani iki AI birlikte calisinca satisi 3 kat artirdi VE iadeyi 4 kat azaltti. Rakiplerinizin bu haftaki gorsel sorgu cevap suresi: ortalama 4 dakika (ve %30'unu bulamadilar). Sizinki: 4 saniye, %98 bulma orani.

### Senaryo C: Olumsuz Yorum Önleme + Kurtarma

```
Müşteri öfkeyle yazar: "Ürün berbat, yorum yazacağım!"
  ↓ Olumsuz Yorum Önleme → Risk skoru: 85 (KRİTİK)
  ↓ Otomatik kurtarma: Özür + tam iade + %20 indirim kodu
  ↓ Müşteri kabul eder
  ↓ 48 saat sonra: "Deneyiminizi paylaşır mısınız?"
  ↓ Müşteri olumlu yorum yazar
  ↓ SONUÇ: Kötü yorum → olumlu yoruma dönüştü
```

> 💡 **Vay Be Anı:** Trendyol saticiniz. Magaza puani 4.5 — her puan gosterimdeki yerinizi belirliyor. Bir musteri "1 yildiz yazacagim" diye yaziyor. Eskiden ne olurdu? Agent mesaji gorene kadar musteri coktan 1 yildiz yazmisti. Puaniniz 4.5'ten 4.4'e duserdi — gosterim %15 azalir, aylik 45.000 TL kayıp. Simdi? Olumsuz Yorum Onleme 30 saniyede devreye girdi, kurtarma mesaji gonderdi, musteri memnun edildi. 1 yildiz yerine 5 yildiz geldi. Puaniniz 4.5'te kaldi — hatta yuklendi. Aylik kurtarilan satis etkisi: 165.000 TL. Bu gorünmez kalkan olmasa, her ay 5-10 olumsuz yorum Google/Trendyol puanınızı kemirirdi ve siz nedenini bile bilemezdiniz.

> 💡 **Vay Be Anı:** Otel isletiyorsunuz. Misafir check-out'ta "oda servisi cok yavas geldi" diye sinirli ayrildi. Google'a kotu yorum yazmak uzere. Ama check-out sirasinda WhatsApp'a gonderdigi "memnun degilim" mesaji AI tarafindan yakalandi. Risk skoru: 72. Otomatik tetiklendi: mudur arayacak + bir sonraki konaklama %20 indirimli + odaya meyve tabagi notu. Mudur aradi, ozur diledi. Misafir: "Beni arayacaklarini hic beklemiyordum. Oteli begeniyorum aslinda, sadece o gun kotuydu." Google'a 4 yildiz yazdi, "sorunlari hemen cozuyorlar" diye not dustu. Booking puaniniz 8.9'da kaldi. Rakip otelde bu misafir 2 yildiz yazardi — cunku kimse aramazdi.

---

## Özet Tablo — AI İnovasyon Ürünleri

| # | Ürün | Phase | Birincil Sektör | ROI | Birim Maliyet |
|---|------|-------|----------------|-----|---------------|
| 1 | Sesli Mesaj AI | 3B | Tüm sektörler | Agent zaman tasarrufu 2,5-4 saat/gün | ~0,006 $/mesaj |
| 2 | Yüz Analizi AI | 3D | Estetik + Diş | 45x | ~0,07-0,20 $/analiz |
| 3 | Görsel Ürün Arama | 3C | E-ticaret | Satış artışı + hız | ~0,05-0,15 $/arama |
| 4 | Akıllı Beden Önerisi | 3C | E-ticaret | 15,6x (iade azaltma) | ~0,004 $/öneri |
| 5 | Olumsuz Yorum Önleme | 3B | E-ticaret + Tüm | 27.500-132.500 ₺/ay net | ~0,05-0,15 $/girişim |
| 6 | Çok Dilli Medikal Turizm | 3B | Estetik + Diş | +424.500 €/ay fark | ~0,07-0,18 $/konuşma |

---

## Ortak Teknik Özellikler

Altı ürünün tamamı bu ortak mimari ilkeleri paylaşır:

- **Çok kiracılı SaaS tasarımı:** Her müşteri izole namespace'de
- **WhatsApp Business API entegrasyonu:** Birincil kanal, mevcut Invekto altyapısı
- **Mevcut servis kullanımı:** ChatAnalysis, Outbound, Automation — sıfırdan inşa yok
- **LLM destekli:** Claude Vision/API ile kaliteli, doğal dilde çıktı
- **Vektör arama:** CLIP, pgvector, Qdrant — benzerlik ve eşleştirme
- **Kiracı bazlı özelleştirme:** Katalog, fiyat, marka — her işletme kendi verisiyle
- **7/24 otomasyon:** Minimum insan müdahalesi, gece/hafta sonu tam çalışır
