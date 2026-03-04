# E-Ticaret Senaryoları — Invekto ile Online Satışın Dönüşümü

> **Sektör:** Online perakende, marketplace satıcıları (Trendyol, Hepsiburada), D2C markalar, Shopify/WooCommerce mağazaları
> **Hedef Kitle:** Günde 50-500 mesaj alan, 1-30 kişilik ekibiyle müşteri iletişimini yöneten online satıcılar
> **Anahtar Vaat:** "Müşteri sorularının yarısını otomatik çöz, temsilci maliyetini düşür, satışı artır"

---

## Neden E-Ticaret İçin Invekto?

Türkiye'de bir Trendyol satıcısının günlük gerçeği şudur: sabah bilgisayarı açtığında WhatsApp'ta 40-50 cevapsız mesaj bekler. "Kargom nerede?", "İade kodu ne?", "Stok var mı?" — hepsi birbirine benzer ama her birinin arkasında sabırsız bir müşteri vardır. Geç kalınan her cevap, bir kötü yorum potansiyelidir. Her kötü yorum, yüzlerce potansiyel müşterinin o ürün sayfasından geri dönmesi demektir.

Invekto, bu döngüyü kırmak için tasarlandı. Tek bir ekrandan 7 kanalı yönetir. Yapay zeka temsilciye cevap önerir. Chatbot en sık sorulan soruları otomatik çözer. Toplu mesaj ile kampanya duyurur. Ve tüm bunları yaparken hiçbir mesaj kaybolmaz.

### E-Ticaret Personaları

| Persona | Kim | Günlük Gerçeklik |
|---------|-----|------------------|
| **Mehmet (E1)** | Trendyol/Hepsiburada satıcısı, 3 kişilik ekip, günde 100-200 mesaj | Paneller arası koşuyor, kargo takip linkleri kopyalıyor, aynı soruya 50. kez cevap yazıyor |
| **Ayşe (E2)** | D2C marka sahibi, Instagram + WhatsApp + Shopify, 2 kişilik ekip | DM'ler kaçıyor, sepet terk edenlere ulaşamıyor, stok sorusuna geç dönüyor |

---

## Gelir Senaryoları (E-Ticaret)

### S1 — Negatif Yorum Kurtarma (~144.000 TL/ay potansiyel etki)

**Acı:** Bir müşteri kızgınken yorum yazmadan önce onu yakalayamazsanız, o yorum sonsuza kadar orada kalır. Trendyol'da bir 1-yıldız yorum, ürünün görünürlüğünü %20-30 düşürebilir.

**Nasıl Çalışır:**
1. Müşteri WhatsApp'tan yazıyor — üslubu sert, şikayet belirtileri var
2. Invekto'nun yapay zekası mesajı analiz eder: *"Bu müşteri kızgın, yorum riski yüksek"*
3. Temsilciye özel bir uyarı gelir: 🔴 "Dikkat: yorum riski. Empati ile yaklaş"
4. AI, sakin ve empatik bir cevap önerir — temsilci onaylayıp gönderir
5. Sorun çözülünce, Invekto otomatik olarak bir "memnun kaldınız mı?" mesajı gönderir
6. Müşteri memnunsa → Google/Trendyol yorum linki gönderilir

**Perde Arkası:** Invekto ChatAnalysis servisi (7101 portu) her mesajı 15 kriterde analiz eder — duygu durumu, satın alma niyeti, şikayet seviyesi. AgentAI servisi (7105) bu verileri kullanarak temsilciye en uygun cevabı önerir.

**Sonuç:** Kötü yorum yerine olumlu yorum. Ortalama bir satıcı için aylık 144.000 TL'lik olumsuz etki tersine çevrilir.

> 💡 **Vay Be Anı:** Cuma akşamı saat 21:00. Trendyol'dan sipariş alan müşteri, kutuyu açtığında ürünün defolu olduğunu gördü ve hırsla yazmaya başladı. Siz o saatte çoktan ekranı kapatmıştınız. Ama Invekto kapamadı. AI mesajı okudu, "bu müşteri 30 saniye içinde 1 yıldız verecek" dedi ve temsilciye anında kırmızı alarm gönderdi. Temsilci 45 saniyede empatik bir cevap gönderdi. Sonuç? Müşteri 1 yıldız yerine 5 yıldız verdi ve yoruma şunu yazdı: "Sorun oldu ama anında çözdüler, güvenilir mağaza." O 5 yıldız, ürün sayfanıza gelen 200 kişiden 30'unu daha müşteriye çevirdi. Dashboard'da yazıyor: "Bu ay kurtarılan yorum sayısı: 14. Korunan gelir: 144.000 TL." Rakibiniz hala ertesi gün cevap yazıyor — siz gece 21:00'de bile kazanıyorsunuz.

---

### S2 — Satış Öncesi Ürün Soruları (~31.500 TL/ay potansiyel etki)

**Acı:** "Bu ayakkabı 42 numara normal mi yoksa dar mı kalıyor?" — bu soruya 3 dakikada cevap verirsen satarsın, 3 saatte verirsen müşteri çoktan başka yerden almıştır.

**Nasıl Çalışır:**
1. Müşteri Instagram DM'den veya WhatsApp'tan ürün sorusu sorar
2. Invekto AI, sorunun ürün bilgisi ile ilgili olduğunu anlar
3. Knowledge veritabanından (ürün katalogu, beden tablosu, malzeme bilgisi) cevap önerir
4. Temsilci tek tıkla onaylar: "42 numara normal kalıp, dar ayağa 43 öneriyoruz. Sipariş linki: ..."
5. Müşteri memnun → sipariş verir

**Perde Arkası:** Knowledge servisi (7104) satıcının ürün bilgilerini, beden tablolarını ve sıkça sorulan soruları bir bilgi bankasında tutar. AI her soru geldiğinde bu bankadan en doğru cevabı çeker.

**Sonuç:** Cevap süresi 2 saatten 30 saniyeye düşer. Dönüşüm oranı artar.

> 💡 **Vay Be Anı:** Saat 14:00, Instagram'dan 3 kişi aynı anda beden soruyor. Eskiden hepsine teker teker beden tablosu açıp yazıyordunuz — ilk müşteriye döndüğünüzde 12 dakika geçmişti, o çoktan rakipten sipariş vermişti. Şimdi? AI 3 müşteriye de 15 saniyede cevap önerdi. Siz sadece "gönder" dediniz. 3'ü de satın aldı. Ekrandaki sayaç diyor ki: "Bu mesajı ortalama 15 saniyede cevapladınız. Sektör ortalaması: 3 dakika." O 15 saniye farkı, ayda 31.500 TL demek. Rakipleriniz hala beden tablosu açarken siz parayı kasaya koyuyorsunuz.

---

### S3 — İade Çevirme (~18.000 TL/ay potansiyel etki)

**Acı:** Türkiye'de e-ticaret iade oranı %35-40. Bunların çoğu "beden tutmadı" veya "beklediğim gibi değil" — yani çözülebilir sorunlar.

**Nasıl Çalışır:**
1. Müşteri "İade etmek istiyorum" diye yazar
2. Invekto AI, iade nedenini sorar: "Neden iade etmek istiyorsunuz?"
3. Neden "beden küçük" ise → "Bir beden büyük göndermemizi ister misiniz? Ücretsiz değişim yapıyoruz"
4. Neden "beğenmedim" ise → "Alternatif ürünlerimize bakmak ister misiniz? Size özel %10 indirim kodu: KALSIN10"
5. Müşteri kabul ederse → iade yerine değişim veya yeni satış

**Sonuç:** İade oranı %15-20 azalır. Kaybedilecek satışlar kurtarılır.

> 💡 **Vay Be Anı:** Sabah mağazayı açıyorsunuz. Dün gece 23:00'te gelen 3 iade talebinin 2'si otomatik olarak değişime çevrilmiş. Bir müşteriye "bir beden büyüğünü ücretsiz gönderelim mi?" denmiş, kabul etmiş. Diğerine "bu ürün yerine en çok beğenilen alternatifimizi %10 indirimle deneyin" denmiş, o da kabul etmiş. Siz uyurken 1.500 TL kurtarılmış. Dashboard'da yazıyor: "Bu ay kurtarılan gelir: 18.200 TL. Dönüştürülen iade sayısı: 23." Artık Invekto'nun aylık ücretini sorgulayan siz değilsiniz — sorgulayan rakibiniz, çünkü o hala her iade talebinde para kaybediyor.

---

### S4 — Sipariş Sonrası Proaktif Satış (~22.500 TL/ay potansiyel etki)

**Acı:** Müşteri bir kez aldı ve gitti. Tekrar satış fırsatı kaçırılıyor.

**Nasıl Çalışır:**
1. Müşteri sipariş verdi ve teslim aldı
2. Invekto Outbound servisi T+3 gün sonra otomatik mesaj gönderir: "Ürünü beğendiniz mi?"
3. Beğendiyse → ilgili ürün önerisi: "Bu ürünü alanlar şunu da aldı..."
4. T+30 gün → yeniden satın alma hatırlatması (tükenebilir ürünlerde)

**Perde Arkası:** Outbound servisi (7107), müşteri onayı (opt-in) olan kişilere zamanlı mesaj zincirleri gönderir. Her mesaj WhatsApp template kurallarına uygun, spam olmayan, değer katan içeriklerdir.

**Sonuç:** Tekrar satın alma oranı %15-25 artar.

> 💡 **Vay Be Anı:** Geçen ay 320 sipariş gönderdiniz. Eskiden bunların %95'i bir defa aldı ve bir daha geri gelmedi. Şimdi Invekto her müşteriye teslimatın 3. gününde "Memnun kaldınız mı?" diye soruyor. Memnun olanların %22'sine "Bu ürünü alanlar şunu da sevdi" önerisi gidiyor. Sonuç? Bu ay 47 tekrar sipariş geldi — hiçbirini siz takip etmediniz, hepsini sistem yaptı. Dashboard'da: "Proaktif satış geliri: 22.500 TL. Müşteri başı ek sipariş oranı: %14.7." Siz yeni müşteri peşinde koşarken, Invekto mevcut müşterilerinizden sessizce para kazanıyor.

---

### S5 — B2B Lead Tespiti (~37.500 TL/ay potansiyel etki)

**Acı:** Toplu sipariş veren veya kurumsal alım yapan müşteriler normal kuyruğa düşüyor. VIP muamele görmüyor.

**Nasıl Çalışır:**
1. Müşteri "500 adet fiyat alabilir miyim?" yazar
2. Invekto AI "B2B lead" olarak işaretler — özel etiket + VIP öncelik
3. Mesaj direkt satış ekibine yönlendirilir (normal sıraya girmez)
4. Satış ekibi özel teklif hazırlar, Invekto üzerinden gönderir
5. Deal takibi CRM'de devam eder

**Sonuç:** B2B fırsatlar kaçırılmaz. Yüksek değerli siparişler hızla kapanır.

> 💡 **Vay Be Anı:** Pazartesi sabahı WhatsApp'ınıza 87 mesaj gelmiş. 85'i "kargom nerede?" tarzı normal sorular. Ama 2 tanesi farklı: biri "200 adet sipariş için fiyat alabilir miyim?", diğeri "kurumsal fatura ile toplu alım yapmak istiyoruz." Eskiden bu mesajlar 85 mesajın arasında kaybolurdu — temsilci sırayla cevap verirdi ve kurumsal müşteri 4 saat beklerdi. 4 saatte o müşteri 3 rakibe daha yazmıştı. Şimdi? Invekto o 2 mesajı anında "B2B Lead" olarak etiketledi, normal kuyruğun önüne çıkardı, doğrudan satış ekibine yönlendirdi. Satış ekibi 8 dakikada teklif gönderdi. O 2 sipariş toplam 37.500 TL. Eğer Invekto yokken kaçırıyor olsaydınız, yılda 450.000 TL görünmez gelir kaybı demekti.

---

## Saha Senaryoları (Günlük Operasyon)

### Kargo ve Lojistik Grubu

#### Senaryo 01 — "Kargom nerede? Takip linki atar mısın?"

Her e-ticaret satıcısının günde en az 30-200 kez duyduğu soru. Bugün temsilci Trendyol paneline giriyor, sipariş numarasını buluyor, takip linkini kopyalayıp yapıştırıyor — mesaj başına 2-4 dakika.

**Invekto ile:**
- **Bugün (mevcut):** 7 kanal tek ekranda, hazır cevap şablonları ile hız artışı
- **Phase 1 sonrası:** AI mesajı okur, sipariş bilgisini çeker, "Kargonuz yolda, tahmini teslimat: yarın. Takip linki: ..." cevabını önerir. Temsilci tek tıkla gönderir
- **Phase 2 sonrası:** Trendyol/Hepsiburada API entegrasyonu ile sipariş bilgisi otomatik çekilir — temsilcinin panel açmasına gerek kalmaz

**Kazanım:** Mesaj başına 2-4 dakika → 10 saniye. Günde 100 mesaj × 3 dakika = 5 saat tasarruf.

> 💡 **Vay Be Anı:** Dün gece bir müşteriniz "Kargom nerede?" yazmak için WhatsApp'ı açtı. Ama mesaj yazmaya fırsat kalmadan bildirim geldi: "Kargonuz yarın 14:00-16:00 arasında teslim edilecek." Müşteri mesajı yazmadı. Sormadı. Çünkü cevap zaten oradaydı. Bu sabah dashboard'a baktınız: "Dün gelen 120 mesajın 45'i kargo sorusuydu. 38'i müşteri sormadan ÖNCE otomatik bildirimle çözüldü." 38 mesaj x 3 dakika = 114 dakika. Neredeyse 2 saat. Temsilciniz o 2 saati gerçek satış yaparak geçirdi. Rakibiniz hala Trendyol panelinden takip kodu kopyalıyor.

---

#### Senaryo 02 — "İade kodu aldım, hangi kargoya vereceğim?"

İade süreci Türkiye'de en stresli müşteri deneyimlerinden biri. Müşteri iade kodunu aldı ama ne yapacağını bilmiyor — yardım bekliyor.

**Invekto ile:**
- AI iade sürecini adım adım anlatır: "İade kodunuz: XYZ123. En yakın Yurtiçi Kargo şubesine gidin, kodu söyleyin. 3 iş günü içinde iade işleminiz tamamlanır"
- Knowledge veritabanından kargo şirketi + süreç bilgisi otomatik çekilir

> 💡 **Vay Be Anı:** Müşteriniz iade kodunu aldı ama kafası karışık. Eskiden sizi arar, siz paneli açar, kargo firmasını bulur, kodu tekrar söyler, şubeyi arardınız — 8 dakika. Şimdi müşteri "İade kodumu aldım, ne yapacağım?" yazdı, Invekto 10 saniyede cevap verdi: kargo firması, en yakın şube adresi, adım adım süreç. Müşteri "bu kadar basit miymiş?" dedi. Ama asıl vay be anı şu: geçen hafta bu mesajdan 47 tane geldi. 47 x 8 dakika = 6 saat 16 dakika. O süreyi artık satışa harcıyorsunuz.

---

#### Senaryo 06 — "Teslim edildi görünüyor ama gelmedi"

Müşterinin en panik anlarından biri. Kargoda "teslim edildi" yazıyor ama paket yok.

**Invekto ile:**
- AI kriz tespiti yapar — bu mesaj yüksek öncelikli
- Empati cevabı önerir: "Anlıyoruz, çok sinir bozucu bir durum. Hemen araştırıyoruz..."
- Kargo şirketiyle iletişim süreci başlatılır
- Temsilci durumu güncelledikçe müşteriye otomatik bilgi gider

> 💡 **Vay Be Anı:** Saat 19:00, müşteri kargoda "teslim edildi" görüyor ama kapısında paket yok. Panik. Eskiden bu müşteri size yazar, siz yarın sabah görür, yarın öğlen araştırır, akşam dönerdiniz — o zamana kadar müşteri Şikayetvar'a yazmıştı bile. Şimdi? Invekto mesajı okuduğu an "kriz" olarak etiketledi. En deneyimli temsilcinize yönlendirdi. 90 saniyede empati dolu bir cevap gitti: "Hemen araştırıyoruz, sizi bilgilendireceğiz." Müşteri kalbinde taşıdığı o "kimse ilgilenmiyor" hissini bıraktı. Kargo firması ile iletişim başladı, süreç boyunca müşteriye otomatik güncelleme gitti. Sonuç: Şikayetvar yerine 5 yıldız. Bir kriz anını sadakat anına çevirdiniz.

---

#### Senaryo 07 — "Kargo paketi hasarlı, tutanak istiyor"

Paket hasarlı geldi, müşteri fotoğraf çekmiş. Tutanak gerekiyor.

**Invekto ile:**
- Müşteriden fotoğraf alınır (dosya paylaşım mevcut)
- AI süreç adımlarını anlatır: "Fotoğrafı aldık. Kargo şirketinden tutanak talep ediyoruz, 1 iş günü içinde dönüş yapacağız"
- Dosyalar konuşma geçmişinde saklanır — hiçbir kanıt kaybolmaz

> 💡 **Vay Be Anı:** Müşteri hasarlı paketi fotoğrafladı ve WhatsApp'tan gönderdi. Eskiden ne olurdu? Fotoğraf bir temsilcinin telefonunda kalır, e-posta ile muhasebeciye iletilir, muhasebeci kargo şirketini arar, dosya kaybolur, müşteri 3 gün sonra "ne oldu?" diye sorar. Şimdi? Fotoğraf konuşma geçmişinde kayıtlı — kaybolamaz. AI anında süreci başlattı, müşteriye adım adım bilgi verdi. Tutanak süreci 3 gün yerine 1 günde tamamlandı. Müşteri "ilk kez hasarlı kargo bu kadar kolay çözüldü" dedi. Bu cümle, bir sonraki siparişin garantisidir.

---

### İade ve Ürün Sorunları Grubu

#### Senaryo 03 — "İadem reddedildi, ürünü hiç açmadım!"

**Kriz senaryosu.** Müşteri sinirli, "Şikayetvar'a yazacağım" diyor. Bu an kritiktir.

**Invekto ile:**
- AI mesajın kriz içerdiğini tespit eder → yüksek öncelik etiketi
- Empati cevabı önerir: "Durumunuzu anlıyoruz ve sizin için çözmek istiyoruz"
- Mesaj deneyimli temsilciye yönlendirilir (yeni başlayan agent bu mesajı almaz)
- Platform süreçleri açık bir dille anlatılır
- **İnsan devir zorunlu** — bu senaryoda tam otomasyon riskli

> 💡 **Vay Be Anı:** Müşteri büyük harflerle yazdı: "İADEMİ REDDETTİNİZ, ŞİKAYETVAR'A YAZIYORUM!" Eskiden bu mesaj yeni başlayan temsilcinin ekranına düşer, temsilci panikler, yanlış cevap verir, müşteri daha da kızar. Şimdi? Invekto "kriz" alarmı çaldı. Mesaj doğrudan en deneyimli temsilcinize gitti. AI, empatik bir cevap taslağı sundu: "Durumunuzu anlıyoruz, 10 dakika içinde kişisel olarak ilgileniyoruz." Temsilci 3 dakikada çözüm sundu. Müşteri Şikayetvar'a yazmadı. Bir Şikayetvar yazısının size maliyeti biliyor musunuz? Ortalama 1 kötü Şikayetvar yazısı = 15-20 potansiyel müşteriyi kaybetmek. Bu tek kurtarma bile aylık Invekto ücretini karşıladı.

---

#### Senaryo 04 — "Kusurlu ürün geldi, değişim mi iade mi?"

**Invekto ile:**
- AI müşteriye seçenek sunar: "Değişim veya iade yapabiliriz. Hangisini tercih edersiniz?"
- Değişim seçilirse → yeni gönderim süreci başlatılır
- İade seçilirse → iade kodu + kargo bilgisi otomatik gönderilir

> 💡 **Vay Be Anı:** Müşteri "Ürün kusurlu geldi" yazdı. Eskiden temsilci "Fotoğraf atar mısınız?", müşteri atar, temsilci "İade mi değişim mi?", müşteri düşünür, temsilci bekler — 15 dakikalık ping pong. Şimdi AI tek mesajda tüm seçenekleri sundu: "Üzgünüz! Fotoğrafı aldık. Değişim: yeni ürün 24 saatte kapınızda, ücretsiz. İade: kodu ve kargo bilgisini hemen gönderiyoruz. Hangisini tercih edersiniz?" Müşteri "değişim" dedi, 30 saniyede süreç başladı. Sonuç? Kusurlu ürünlerde değişim tercih oranı %65'e çıktı — eskiden %30'du. Her değişim, kurtarılmış bir satıştır.

---

#### Senaryo 05 — "Yanlış ürün geldi / eksik parça"

**Invekto ile:**
- Fotoğraf istenir (kanıt toplama)
- Sipariş detayı ile karşılaştırılır (API entegrasyonu ile)
- Doğru ürün gönderim veya iade süreci başlatılır

> 💡 **Vay Be Anı:** Müşteri fotoğraf attı: sipariş ettiği mavi kazak yerine kırmızı gönderilmiş. Eskiden bu fotoğraf bir temsilcinin telefonunda kalır, depoculara iletilir, 2 gün sonra "bakıyoruz" denir, müşteri o sürede iade yapar ve bir daha alışveriş yapmaz. Şimdi? Invekto fotoğrafı aldığı an sipariş detayıyla karşılaştırdı, hatayı doğruladı, müşteriye 60 saniyede "Doğru ürünü yarın kargoya veriyoruz, yanlış ürünü kuryemiz alacak" dedi. Müşteri şok oldu — "Bu kadar hızlı çözüldü mü?" Evet, çözüldü. Ve o müşteri 2 hafta sonra tekrar sipariş verdi. Hız, güven inşa eder. Güven, tekrar satış getirir.

---

#### Senaryo 15 — "Ürün açıklamasıyla gelen farklı"

Görsel uyumsuzluk — en sık şikayet nedenlerinden biri. Instagram'daki fotoğraf ile gelen ürün farklı.

**Invekto ile:**
- AI empati + çözüm önerisi: değişim, iade veya indirim kuponu
- Kriz scorelama — yüksek risk ise deneyimli temsilciye yönlendirme

> 💡 **Vay Be Anı:** "Instagram'daki renkle aynı değil!" — bu mesaj haftada ortalama 12 kez geliyor. Her biri potansiyel 1 yıldız yorum. Eskiden temsilci ne diyeceğini bilemezdi: kabul mü etsin, reddetsin mi? Şimdi AI, mesajın kriz skorunu hesaplıyor. Yüksek riskli olanları deneyimli temsilciye yönlendiriyor ve 3 seçenekli çözüm sunuyor: "Değişim yapalım, iade edelim veya bir sonraki siparişinizde %15 indirim kodu kullanalım." Müşterilerin %40'ı indirim kodunu seçiyor — yani iade yerine yeni satış oluyor. Bu ay 12 görsel uyumsuzluk şikayetinin 5'i yeni satışa döndü. Şikayet, gelire dönüştü.

---

### Ödeme ve Fatura Grubu

#### Senaryo 08 — "Fatura / kurumsal fatura talebi"

**Invekto ile:**
- AI fatura sürecini anlatır: "Kurumsal fatura için TC/VKN bilgilerinizi iletir misiniz?"
- Bilgi toplandıktan sonra muhasebe departmanına yönlendirilir

> 💡 **Vay Be Anı:** "Kurumsal fatura keser misiniz?" Eskiden bu mesaj geldiğinde temsilci TC/VKN ister, müşteri gönderir, temsilci muhasebeciye e-posta atar, muhasebeci 2 gün sonra bakar, müşteri "faturam nerede?" diye tekrar yazar. 3 mesaj, 2 gün, 1 mutsuz müşteri. Şimdi? Invekto tek mesajda tüm bilgileri topluyor, muhasebe departmanına otomatik iletiyor, müşteriye "Faturanız 24 saat içinde e-posta adresinize gönderilecek" diyor. 3 mesajlık süreç 1 mesaja düştü. Temsilci zaman kazandı, müşteri hızlı cevap aldı, muhasebeci düzenli bilgi aldı. Herkes kazandı.

---

#### Senaryo 13 — "Kapıda ödeme var mı? Havale/IBAN at"

**Invekto ile:**
- AI ödeme yöntemlerini listeler: "Kapıda ödeme: Evet. Havale: IBAN ve banka bilgimiz..."
- Knowledge veritabanından güncel ödeme bilgileri çekilir

> 💡 **Vay Be Anı:** Bu soru günde en az 10-15 kez geliyor. Her seferinde temsilci IBAN'ı bir yerden kopyalıyor, ödeme seçeneklerini yazıyor — mesaj başına 2 dakika. Ayda 400 kez aynı şey. Şimdi? Invekto bu soruyu tanıyor ve 5 saniyede güncel IBAN, kapıda ödeme bilgisi ve taksit seçeneklerini sunuyor. Temsilci dokunmuyor bile. 400 mesaj x 2 dakika = 13 saat/ay. O 13 saati artık satış yaparak geçiriyorsunuz. Üstelik IBAN yanlış kopyalama hatası da sıfırlandı — geçen ay 3 müşteri yanlış hesaba havale yapmıştı, artık yapmıyor.

---

### Satış ve Dönüşüm Grubu

#### Senaryo 11 — "Ürün bedeni / uyumu — Instagram DM'den soru"

Instagram'dan gelen lead — hızlı cevap = satış, geç cevap = kayıp.

**Invekto ile:**
- IG DM tek ekrandan yönetilir (ayrı uygulama açmaya gerek yok)
- AI beden önerisi yapar: "Boy: 175cm, kilo: 70kg → M beden öneriyoruz"
- Phase 3C'de Size/Fit AI ile kişiselleştirilmiş beden tahmini

> 💡 **Vay Be Anı:** Bir müşteri Instagram'dan ürün fotoğrafı attı ve "Bu bende nasıl durur?" diye sordu. Eskiden siz Instagram uygulamasını açar, beden tablosunu bulur, elle yazardınız — en az 5 dakika. O 5 dakikada müşteri 3 rakip mağazaya daha yazmıştı, ilk cevap veren kazandı ve o siz değildiniz. Şimdi? Invekto Instagram DM'yi tek ekrandan gösteriyor, AI fotoğrafı analiz edip "170cm, 65kg → M beden, bu bedeni alan müşterilerin %95'i memnun" dedi. Temsilci tek tıkla gönderdi. 30 saniye. Müşteri: "Normalde 10 dakikada cevap gelmez, siz 30 saniyede beden bile önerdiniz!" Sipariş verildi. İlk cevap veren mağaza olmak, her seferinde satışı kazanmak demek.

---

#### Senaryo 12 — "Stok var mı? Ne zaman gelir?"

**Invekto ile:**
- Phase 1: Stok bilgisi Knowledge veritabanından çekilir
- Phase 2+: Stok gelince otomatik bildirim (back-in-stock alert)

> 💡 **Vay Be Anı:** Müşteri "Bu ürün M bedende var mı?" diye sordu. Stokta yoktu. Eskiden "Şu an yok, gelince haber veririz" der ve unuturdunuz. Müşteri de unuturdu — veya rakipten alırdı. Şimdi Invekto "Şu an M beden tükenmiş, stoğa girdiğinde sizi ilk bilgilendireceğiz" dedi. 10 gün sonra stok girdi, otomatik mesaj gitti: "Beklediğiniz ürün tekrar stoklarımızda! Son 3 adet kaldı." Müşteri 4 dakikada sipariş verdi. Dashboard'da bu ay: "Stok bildirimi gönderilen: 28 müşteri. Satışa dönen: 11 (%39). Kurtarılan gelir: 8.700 TL." Bu müşterileri eskiden %100 kaybediyordunuz. Artık %39'unu kurtarıyorsunuz.

---

#### Senaryo 14 — "Sepeti terk etti — takip mesajı"

Müşteri WhatsApp'tan ürün sordu, fiyatı öğrendi ama almadı. Sepette ürün bıraktı.

**Invekto ile:**
- T+1 saat: "İlgilendiğiniz ürün hala müsait. Sorularınız varsa yardımcı olabiliriz"
- T+24 saat: "Ürünü sepetinize eklemiştiniz. Size özel %5 indirim kodu: HOSGELDIN5"
- Opt-in (müşteri onayı) olmadan bu mesajlar gönderilmez

> 💡 **Vay Be Anı:** Geçen hafta 180 kişi ürün sordu ama satın almadı. Eskiden bu 180 kişi sessizce kaybolurdu — hiçbiriyle iletişime geçemezdiniz. Şimdi? Invekto 1 saat sonra nazik bir hatırlatma gönderdi, 24 saat sonra %5 indirim kodu sundu. 180 kişiden 23'ü geri döndü ve satın aldı. 23 x ortalama 450 TL = 10.350 TL. Bu para havadan gelmedi — zaten ilgilenen ama bir dürtmeye ihtiyacı olan müşterilerden geldi. Eskiden bu 10.350 TL her ay çöpe gidiyordu. Artık gitmiyor.

---

### Operasyonel Verimlilik Grubu

#### Senaryo 23 — "Çoklu müşteri aynı hattan yazıyor — ekip çakışması"

3 temsilci aynı müşteriye farklı cevap veriyor. Müşteri kafası karışık.

**Invekto ile:**
- Gelişmiş routing: her mesaj tek bir sorumluya atanır
- Mevcut müşteri → eski temsilcisine yönlendirilir
- Temsilci offline ise → aynı gruptaki başka temsilciye atanır

> 💡 **Vay Be Anı:** Geçen ay bir müşteri şikayet etti: "Sabah biri 'iade yapabiliriz' dedi, öğlen başka biri 'yapamayız' dedi. Siz kendi aranızda konuşmuyor musunuz?" Bu 1 müşteri, ama bu sorun haftada en az 5-6 kez yaşanıyordu. Her biri potansiyel bir kötü yorum, bir kayıp müşteri. Şimdi Invekto'da her müşteri tek bir temsilciye atanıyor. Konuşma geçmişi herkesin önünde. Temsilci offline'sa mesaj aynı gruptaki başka temsilciye gidiyor — ama önceki konuşmayı görerek. Bu ay: "Çelişkili cevap şikayeti: 0." Sıfır. Müşterileriniz artık tek bir sesle konuşan profesyonel bir ekip görüyor, kaotik bir WhatsApp grubu değil.

---

#### Senaryo 25 — "Mesaj penceresi kapandı — template ile bilgi verme"

WhatsApp'ta 24 saat cevap vermezseniz, mesaj penceresi kapanır. Artık sadece onaylı şablon mesaj gönderebilirsiniz.

**Invekto ile:**
- Sistem 24 saat dolmadan uyarı verir
- Pencere kapandıysa → otomatik template mesaj gönderilir
- Template yönetimi Invekto panelinden yapılır (Meta paneline girmenize gerek kalmaz)

> 💡 **Vay Be Anı:** Geçen ay 12 müşterinin mesaj penceresi kapandı. Neden? Temsilci cevap vermeyi unuttu veya yoğunluktan kaçırdı. 24 saat geçti, pencere kapandı, müşteriye ulaşamaz hale geldiniz. 12 müşteri x ortalama 500 TL sipariş = 6.000 TL kayıp. Şimdi Invekto, 24 saat dolmadan 2 saat önce kırmızı uyarı veriyor: "Bu müşteriye cevap verilmedi, 2 saat sonra pencere kapanacak!" Temsilci kaçırsa bile, pencere kapanmadan AI otomatik template mesaj gönderiyor. Bu ay kapanan pencere sayısı: 1 (ve o da müşterinin numarasını değiştirdiği için). 12'den 1'e. 11 müşteri kurtarıldı. 5.500 TL korundu. Üstelik Meta paneline girmeden template yönetimi yapıyorsunuz — o panel labirentinde kaybolmak artık tarih.

---

## E-Ticaret Ek Senaryolar (Derinleştirilmiş)

### EB-01 — Stok Bildirim (Back-in-Stock)

Müşteri "Bu ürün gelince haber verin" dedi. Bugün hiçbir şey yapılmıyor — müşteri unutuyor veya rakipten alıyor.

**Invekto ile:**
- Stok girişi olunca otomatik WhatsApp mesajı: "İstediğiniz ürün tekrar stoklarımızda! Hemen sipariş verin"
- Trendyol/Shopify stok webhook'u ile tetiklenir
- Müşteri onayı (opt-in) zorunlu

> 💡 **Vay Be Anı:** 45 müşteri "gelince haber verin" dedi. Eskiden bir Excel'e yazdınız, stok geldiğinde 45 kişiye tek tek mesaj attınız — ki attıysanız. Çoğu zaman unutuldu. Şimdi? Stok girdiği an Invekto 45 kişiye aynı anda bildirim gönderiyor: "Beklediğiniz ürün stoklarda! Son 8 adet." "Son 8 adet" detayı fark ettiniz mi? Bu FOMO tetikleyicisi sayesinde müşterilerin %42'si ilk 2 saat içinde satın alıyor. 45 kişiden 19'u satışa döndü. Hiçbirini siz takip etmediniz, hiçbirine tek mesaj yazmadınız. Stok girdi, para geldi.

---

### EB-03 — Proaktif Sipariş Durum Güncelleme

Kargo gecikecek. Müşteriden ÖNCE bilgilendir.

**Invekto ile:**
- Sipariş durumu değiştiğinde otomatik mesaj: "Siparişiniz hakkında bilgilendirme: X ürünü stok sorunu nedeniyle 2 gün gecikmeli gönderilecek. Özür dileriz"
- Proaktif bilgilendirme şikayet oranını %40-60 azaltır

> 💡 **Vay Be Anı:** Kargo gecikti. Eskiden ne olurdu? Müşteri 2 gün bekler, sinirlenir, "nerede kargom?" yazar, siz araştırır, "gecikme var" dersiniz, müşteri zaten öfkeli. Şimdi? Invekto gecikmeyi tespit ettiği an, müşteri sormadan ÖNCE mesaj gönderiyor: "Siparişiniz stok sorunu nedeniyle 1 gün gecikmeli teslim edilecek. Özür dileriz, size özel %5 indirim kodunuz: OZURDILERIZ5." Müşteri şikayet etmek yerine "vay, sorun olmadan haber verdiler" diyor. Bu ay 34 gecikme oldu. Eskiden 34 kızgın mesaj + 8 Şikayetvar yazısı demekti. Şimdi? 0 Şikayetvar, 0 kızgın mesaj, 6 müşteri indirim kodunu kullanarak yeni sipariş verdi. Gecikme bir krize değil, yeni bir satışa döndü.

---

### EB-04 — Cross-Platform Sipariş Eşleştirme

Müşteri Trendyol'dan aldı, WhatsApp'tan yazıyor, Hepsiburada'da da siparişi var. "Hangi sipariş?" sorusu kabusu.

**Invekto ile:**
- Telefon numarası ile tüm platformlardaki siparişler eşleştirilir
- AI: "Son 3 siparişiniz: 1) Trendyol - ayakkabı, 2) HB - çanta. Hangisi hakkında yardım istersiniz?"

> 💡 **Vay Be Anı:** Müşteri WhatsApp'tan yazdı: "Siparişim nerede?" Ama 3 farklı platformda 4 siparişi var. Eskiden temsilci "Hangi sipariş? Trendyol mu, Hepsiburada mı? Sipariş numaranız ne?" diye sorar, müşteri aramaya başlar, 5 dakika geçer, ikisi de sinirli. Şimdi? Invekto telefon numarasından tüm platformlardaki siparişleri eşleştirdi: "Son siparişleriniz: 1) Trendyol — Siyah Ayakkabı (kargoda), 2) HB — Deri Çanta (hazırlanıyor), 3) Shopify — Bileklik (teslim edildi). Hangisi hakkında yardım istersiniz?" Müşteri "1" dedi, 10 saniyede cevap aldı. Tek soru, tek cevap, sıfır sürtünme. Temsilciniz 4 farklı panel açmak yerine tek ekrandan her şeyi gördü.

---

### EB-05 — Şikayetvar Eskalasyon Yönetimi

Müşteri Şikayetvar'a yazdı. Her geçen saat etki düşüyor.

**Invekto ile:**
- Proaktif WhatsApp mesajı: "Şikayetvar'daki yazınızı gördük, sorunu hemen çözmek istiyoruz"
- Hız kritik — ne kadar erken ulaşılırsa o kadar etkili
- Çözüm sonrası müşteriden "çözüldü" güncellemesi rica edilir

> 💡 **Vay Be Anı:** Şikayetvar'a bir yorum düştü. Eskiden ne olurdu? 3 gün sonra fark ederdiniz, müşteri çoktan herkese anlatmıştı, yorum Google'da indexlenmişti. Şimdi? Invekto Şikayetvar bildirimini anında yakaladı ve 15 dakika içinde müşteriye WhatsApp'tan ulaştı: "Şikayetvar'daki yazınızı gördük, sorununuzu hemen çözmek istiyoruz." Müşteri şok oldu — "Bu kadar hızlı mı?" Sorun 2 saat içinde çözüldü, müşteriden "çözüldü" güncellemesi alındı. Şikayetvar puanınız 3.2'den 4.1'e çıktı. Bu puan, yeni müşterilerin sizi Google'da aradığında ilk gördüğü şey. 3.2 gören gitmez, 4.1 gören güvenir. Bir puan farkı, yüzlerce müşteri demek.

---

### EB-07 — Dolandırıcılık / Fraud Şüphesi

"Bu siparişi ben vermedim!" — müşteri panik halinde.

**Invekto ile:**
- AI panik mesajı tespit eder → normal kuyruk bypass → acil temsilciye yönlendirilir
- Hemen sakinleştirme mesajı: "Hesabınız güvende, durumu inceliyoruz"
- Eskalasyon: yöneticiye push bildirim

> 💡 **Vay Be Anı:** Gece 23:30, müşteri panik içinde yazdı: "Hesabımdan bilmediğim bir sipariş verilmiş!" Bu mesaj sıraya girip sabahı beklerse, müşteri gece boyunca uyuyamaz, bankalara koşar, sosyal medyada paylaşır. Şimdi? Invekto "fraud" alarmını anında tetikledi. Sıradan mesajların önüne geçti. Otomatik sakinleştirme mesajı 10 saniyede gitti: "Hesabınız güvende, durumu inceliyoruz. Lütfen merak etmeyin." Yöneticiye push bildirim gitti. Sabah ilk iş hesap incelendi, müşteriye bilgi verildi. Müşteri sosyal medyaya "Gece yarısı bile ilgilendiler, güvenilir mağaza" yazdı. O paylaşımı 2.000 kişi gördü. Bir kriz anını bedava reklama çevirdiniz.

---

## E-Ticaret Grand Slam Offer

> "Kargo ve iade sorularının %50'sini otomatik çöz"
>
> **Fiyat:** 3.000-5.000 TL/ay
> **Garanti:** 30 günde %50 otomatik çözüm yoksa 2. ay ücretsiz
> **Risk:** "Biz kuruyoruz, siz izliyorsunuz"
> **Kıtlık:** İlk 10 Trendyol satıcısına özel lansman fiyatı

---

## Invekto Servisleri — E-Ticaret İçin Ne Yapıyor?

| Servis | Port | E-Ticaret'te Görevi |
|--------|------|---------------------|
| **Backend** | 5000 | Tüm mesajları toplar, routing yapar, dashboard gösterir |
| **ChatAnalysis** | 7101 | Her mesajı duygu durumu, şikayet riski, satın alma niyeti açısından analiz eder |
| **Automation** | 7108 | Chatbot akışları: "Kargom nerede?" → otomatik takip linki, İade süreci → adım adım yönlendirme |
| **AgentAI** | 7105 | Temsilciye cevap önerisi, müşteri niyetini anlama, B2B lead tespiti |
| **Outbound** | 7107 | Toplu mesaj, kampanya, sipariş takip, stok bildirimi, sepet hatırlatma |
| **Knowledge** | 7104 | Ürün bilgisi, beden tablosu, iade politikası, sık sorulan sorular veritabanı |
| **Integrations** | 7106 | Trendyol, Hepsiburada, Shopify, WooCommerce API bağlantıları |

---

## Özet Tablo — Tüm E-Ticaret Senaryoları

| # | Senaryo | Grup | Etki | Phase |
|---|---------|------|------|-------|
| 01 | Kargom nerede? | Kargo/Lojistik | YÜKSEK | 1-2 |
| 02 | İade kodu, hangi kargoya? | Kargo/Lojistik | ORTA | 1-2 |
| 03 | İade reddedildi (kriz) | Kriz | YÜKSEK | 1-2 |
| 04 | Kusurlu ürün | İade | ORTA | 1-2 |
| 05 | Yanlış/eksik ürün | İade | ORTA | 1-2 |
| 06 | Teslim edildi ama gelmedi | Kargo/Lojistik | ORTA | 1-2 |
| 07 | Hasarlı paket | Kargo/Lojistik | ORTA | 1-2 |
| 08 | Fatura talebi | Ödeme/Fatura | ORTA | 1-2 |
| 09 | Adres/teslimat değişikliği | Operasyonel | ORTA | 1-2 |
| 10 | Kampanya/kupon sorunu | Ödeme/Fatura | DÜŞÜK | 1-2 |
| 11 | Ürün bedeni (IG DM) | Satış | ORTA | 1-3C |
| 12 | Stok sorgusu (D2C) | Satış | DÜŞÜK | 1-2 |
| 13 | Kapıda ödeme/IBAN | Ödeme/Fatura | DÜŞÜK | 1 |
| 14 | Sepet terk takip | Satış | YÜKSEK | 1-2 |
| 15 | Görsel uyumsuzluk (kriz) | Kriz | ORTA | 1-2 |
| 16 | İade süresi kaç gün? | İade | DÜŞÜK | 1 |
| 17 | İade süreci kaç günde biter? | İade | DÜŞÜK | 1 |
| 18 | Gel Al noktası kapalı | Kargo/Lojistik | DÜŞÜK | 1-2 |
| 19 | HB kargo gecikmesi | Kargo/Lojistik | ORTA | 1-2 |
| 20 | Trendyol Express vs WA | Operasyonel | DÜŞÜK | 1 |
| 21 | Shopify ödeme başarısız | Ödeme/Fatura | DÜŞÜK | 1-2 |
| 22 | WooCommerce kargo iade | Ödeme/Fatura | DÜŞÜK | 1-2 |
| 23 | Ekip çakışması | Operasyonel | DÜŞÜK | Mevcut |
| 24 | İade paketi kayboldu | Kargo/Lojistik | DÜŞÜK | 1-2 |
| 25 | Mesaj penceresi kapandı | Operasyonel | DÜŞÜK | 1 |
| EB-01 | Stok bildirim | Satış | ORTA | 2-3 |
| EB-02 | Influencer attribution | Pazarlama | ORTA | 3 |
| EB-03 | Proaktif sipariş güncelleme | Kargo/Lojistik | YÜKSEK | 2 |
| EB-04 | Cross-platform eşleştirme | Operasyonel | ORTA | 2 |
| EB-05 | Şikayetvar eskalasyon | İtibar | ORTA | 3 |
| EB-06 | Garanti/teknik servis | Satış Sonrası | DÜŞÜK | 3 |
| EB-07 | Fraud yönetimi | Güvenlik | YÜKSEK | 2 |
