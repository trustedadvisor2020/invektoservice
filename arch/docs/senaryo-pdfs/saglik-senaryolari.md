# Sağlık Senaryoları — Diş Klinikleri ve Estetik Klinikler

> **Sektör:** Diş klinikleri, estetik cerrahi klinikleri, medikal estetik merkezleri
> **Hedef Kitle:** Günde 20-100 mesaj alan, 2-15 kişilik ekibiyle hasta iletişimini yöneten klinikler
> **Anahtar Vaat — Diş:** "Fiyat sorularını randevuya çevir, no-show'u %60 azalt"
> **Anahtar Vaat — Estetik:** "Lead'leri hastaya dönüştür, medikal turizmi ölçekle"

---

## Neden Sağlık Sektörü İçin Invekto?

Bir diş kliniğinin sabah mesajları arasında en az 10 tane "İmplant ne kadar?" sorusu vardır. Bu soruya verilen her geç cevap, başka bir kliniğe giden bir hasta demektir. Daha kötüsü, randevu alan hastaların %15-20'si gelmiyor — no-show. Ve tedavi sonrası takip? Çoğu klinikte hiç yapılmıyor.

Estetik kliniklerde durum daha da kritik. Instagram'dan gelen "Botox fiyatı ne?" DM'leri, klinik kapatırken resepsiyonist mesaisi bitmişken birikiyor. Yabancı hastalar İngilizce veya Arapça yazıyor — cevap verecek kimse yok. Her kaçan lead, potansiyel 5.000-50.000 TL'lik bir hasta kaybı.

Invekto bu döngüyü kırar. Fiyat sorusunu randevuya dönüştürür. Randevu hatırlatma ile no-show'u azaltır. Tedavi sonrası otomatik takip mesajları gönderir. Ve tüm bunları KVKK'ya uygun şekilde yapar.

### Sağlık Personaları

| Persona | Kim | Günlük Gerçeklik |
|---------|-----|------------------|
| **Dr. Burak (D1)** | Diş kliniği sahibi, 3 ünit, 2 asistan | Koltukta hasta varken telefonu kontrol edemiyor, akşam cevapsız mesajları görüyor |
| **Elif (D2)** | Diş kliniği ön büro sorumlusu | Telefon + WhatsApp + yüz yüze hasta aynı anda, fiyat sorusuna koşuşturma |
| **Dr. Selin (A1)** | Estetik klinik sahibi, 5 doktor, premium segment | Instagram'dan gelen leadlerin %40'ı randevuya dönmüyor, yabancı hastalar kaybolıyor |
| **Zeynep (A2)** | Estetik klinik operasyon sorumlusu | 3 kanaldan mesaj, doktor onayı gereken leadler bekliyor, kampanya takibi dağınık |

---

## Gelir Senaryoları (Sağlık)

### S6 — Fiyat Sorusunu Randevuya Çevirme (~60.000 TL/ay potansiyel etki)

**Acı:** "İmplant ne kadar?", "Botox fiyatı nedir?" — bu sorulara doğru yaklaşım randevu, yanlış yaklaşım kayıp hasta.

**Nasıl Çalışır:**
1. Hasta WhatsApp'tan veya Instagram DM'den "fiyat" sorusu sorar
2. Invekto AI soruyu anlar ve doğru stratejiyle yanıtlar:
   - Fiyat aralığı verir (kesin fiyat değil — muayene gerekli)
   - Hemen randevu teklif eder: "Kesin fiyat muayenede belirlenir. Bu hafta müsait saatlerimiz..."
3. Hasta ilgilenirse → randevu slotu sunulur
4. Randevu onaylanınca → hatırlatma zinciri başlar (R-1gün, R-2saat)

**Neden Önemli:** Fiyat sorusu = satın alma niyeti. Bu soruya 5 dakika içinde cevap veren klinik, 1 saat sonra cevap veren kliniğe göre 10 kat daha fazla randevu alır.

**Perde Arkası:** AgentAI servisi (7105) hastanın niyetini anlar, Knowledge servisi (7104) klinik fiyat aralıklarını ve kampanya bilgilerini çeker. AI, doktor onaylı cevap şablonu ile yanıt önerir.

> 💡 **Vay Be Anı:** Saat 22:30. Elif ön büroyu kapatıp eve gitmiş. Tam o sırada bir hasta Instagram'dan yazdı: "İmplant ne kadar?" Normalde bu mesajı Elif sabah 09:00'da görürdü — ama o zamana kadar hasta 4 kliniğe daha yazmıştı ve en hızlı cevap verenden randevu almıştı. Invekto ile? Mesaj geldi, 8 saniyede fiyat aralığı ve randevu teklifi gitti. Hasta gece 22:31'de randevusunu oluşturdu. Sabah Elif geldiğinde sistemde yeni randevu bildirimi vardı. "Bu hasta biz uyurken gelmiş!" dedi. O hastanın tedavi değeri: 45.000 TL. 8 saniye ile 11 saat arasındaki fark buydu.

---

### S7 — No-Show Önleme (~135.000 TL/ay potansiyel etki)

**Acı:** Randevu alan hastaların %15-20'si gelmiyor. Her boş koltuk = 1.000-5.000 TL kayıp.

**Nasıl Çalışır:**
1. Hasta randevu aldı
2. **R-1 gün:** Otomatik mesaj: "Yarın saat 14:00 randevunuz var. Onaylıyor musunuz? (Evet/Hayır)"
3. **R-2 saat:** "Randevunuz 2 saat sonra. Klinik adresi: ..."
4. Hasta "Gelemeyeceğim" derse → yeni tarih önerilir + bekleme listesindeki hastaya haber verilir
5. Hasta geldi → tedavi sonrası takip zinciri başlar

**Sonuç:** No-show oranı %60 azalır. Ortalama bir klinik için aylık 135.000 TL'lik kayıp önlenir.

> 💡 **Vay Be Anı:** Dr. Burak ayın sonunda raporlara baktı. Geçen ay 47 hasta randevusunu kaçırmıştı — bu ay sadece 19. Yani 28 hasta daha geldi. Her biri ortalama 3.500 TL'lik işlem. 28 x 3.500 = 98.000 TL. Bir tek hatırlatma mesajı yüzünden. Ama asıl "vay be" anı şuydu: Bekleme listesindeki Ayşe Hanım'a "14:00'te yer açıldı" mesajı gitmiş, Ayşe Hanım 3 dakikada onaylamış ve o gün 22.000 TL'lik implant tedavisine başlamış. Boş koltuk hem dolmuş, hem en değerli hastayla dolmuş.

---

### S8 — Tedavi Sonrası Takip (~90.000 TL/ay potansiyel etki)

**Acı:** Tedavi yapıldı, hasta gitti. Bir daha haber yok. Komplikasyon olursa hasta panikle arıyor. İyi giderse sessiz — ama tekrar randevuya da gelmiyor.

**Nasıl Çalışır:**
1. Tedavi yapıldı (diş çekimi, implant, botox, dolgu...)
2. **T+1 gün:** "Merhaba! Tedaviniz nasıl gidiyor? Şişlik veya ağrı varsa bilgi verebiliriz"
3. **T+3 gün:** "Herhangi bir şikayetiniz var mı?"
4. **T+7 gün:** "Kontrolünüz yaklaşıyor. Randevu oluşturalım mı?"
5. **T+30 gün:** "Memnun kaldınız mı? Bizi Google'da değerlendirir misiniz?"

**Sonuç:** Hasta sadakati artar. Komplikasyonlar erken tespit edilir. Google yorumları çoğalır.

> 💡 **Vay Be Anı:** Mehmet Bey implant yaptırdı, ertesi gün T+1 mesajı geldi: "Tedaviniz nasıl gidiyor?" Mehmet Bey "biraz ağrı var" yazdı. AI hemen bilgi verdi: "İlk 48 saatte hafif ağrı normal, soğuk kompres uygulayabilirsiniz. Ağrı artarsa hemen bizi bilgilendirin." Mehmet Bey rahatladı. T+7'de kontrol randevusu önerildi, hemen aldı. T+30'da Google yorumu istendi — 5 yıldız verdi ve şunu yazdı: "Tedaviden sonra bile benimle ilgilendiler, hiçbir klinikte böyle bir şey yaşamadım." O yorum kliniğe 3 yeni hasta getirdi. Mehmet Bey'in eşi de 2 ay sonra tedaviye başladı. Tek bir takip mesajı zinciri, toplam değer: 120.000 TL.

---

### S9 — Medikal Turizm Lead Yönetimi (~300.000+ TL/ay potansiyel etki)

**Acı:** Türkiye dünyada medikal turizmin merkezi — 3.48 milyar dolarlık pazar. Ama gece 3'te Arapça veya Rusça mesaj geldiğinde kimse cevap veremiyor.

**Nasıl Çalışır:**
1. Yabancı hasta Instagram veya WhatsApp'tan yazıyor (İngilizce, Arapça, Rusça, Almanca)
2. Invekto AI dili otomatik algılar
3. Hastanın dilinde cevap verir: tedavi bilgisi, fiyat aralığı, otel/transfer seçenekleri
4. Lead olgunlaşınca → paket teklif: "Tedavi + 3 gece otel + havaalanı transfer: €X.XXX"
5. Saat dilimi fark etmez — 7/24 cevap verilir

**Perde Arkası:** Multilingual AI, 5 dilde kültürel uyumlu cevap üretir. Arap hastaya "İnşallah" ile başlayan cevap, Alman hastaya teknik detay içeren cevap. Her dil kendi kültürel kodlarıyla.

> 💡 **Vay Be Anı:** Gece 03:15, Riyad. Ahmed WhatsApp'tan Arapça yazdı: "أريد زراعة أسنان — كم التكلفة؟" Zeynep uyuyordu, Dr. Selin uyuyordu, tüm Türkiye uyuyordu. Ama Invekto uyanıktı. 5 saniyede Arapça cevap gitti: tedavi bilgisi, fiyat aralığı, otel + transfer paketi. Ahmed 03:20'de selfie gönderdi, AI ön değerlendirme yaptı. 03:22'de randevu oluştu. Sabah Dr. Selin masasına oturduğunda: "Dün gece Suudi Arabistan'dan 1 randevu, toplam paket değeri: 8.500 €. Ajans komisyonu: 0 €." Normal şartlarda bu hasta bir aracıya giderdi, aracı %30-35 komisyon keserdi. 8.500 €'nun 2.975 €'su aracıda kalırdı. Şimdi hepsi kliniğin.

---

### S10 — Google Yorum + Referans Motoru (~105.000 TL/ay potansiyel etki)

**Acı:** Memnun hastalar sessiz. Mutsuz hastalar hemen yorum yazıyor. Sonuç: Google puanı düşük.

**Nasıl Çalışır:**
1. Tedavi sonrası memnuniyet anketi: "1-5 arası puan verir misiniz?"
2. 4-5 puan → Google yorum linki gönderilir: "Deneyiminizi paylaşır mısınız?"
3. 1-3 puan → dahili eskalasyon: "Üzgünüz, ne olduğunu anlatır mısınız?" + yöneticiye bildirim
4. Kötü deneyim düzeltildikten sonra → tekrar puan sorulur

**Sonuç:** Google puanı yükselir. Her 0.1 puan artış = %5-9 daha fazla randevu.

> 💡 **Vay Be Anı:** Dr. Burak'ın kliniği Google'da 3.8 puandı. Her hafta 1-2 kötü yorum geliyordu ama 20 memnun hasta sessiz kalıyordu. Invekto devreye girdi. İlk ay: 47 hastaya memnuniyet anketi gitti. 38'i 4-5 puan verdi ve bunların 29'u Google'a yorum yazdı. 3 hasta 2 puan verdi — onlara özel ilgi gösterildi, sorunları çözüldü, biri puanını 5'e güncelledi. 3 ayda Google puanı 3.8'den 4.6'ya çıktı. Dr. Burak hesapladı: 4.6 puan ile aylık %35 daha fazla randevu talebi geliyor. "Bir yorum sistemi için aylık 35 yeni hasta mı? Bunu neden daha önce yapmamışız?" dedi. Cevap basitti — daha önce 47 hastaya tek tek mesaj atmak imkansızdı.

---

## Diş Kliniği Saha Senaryoları (26-50)

### Senaryo 27 — "Instagram DM: Foto atsam fiyat verir misiniz?"

Hasta Instagram'dan diş fotoğrafı gönderiyor, "fiyat ne?" diyor. Bu, sağlık sektörünün en değerli lead'idir — hasta tedaviye hazır, sadece fiyat onayı bekliyor.

**Invekto ile:**
- IG DM Invekto panelinden tek ekrandan yönetilir
- AI fotoğrafı inceler değil ama niyeti anlar: "fiyat + tedavi talebi"
- Cevap önerisi: "Fotoğrafınız için teşekkürler! Kesin tedavi planı ve fiyat muayenede belirlenir. Bu hafta [gün] saat [saat] müsaitiz. Randevu oluşturalım mı?"
- **Dikkat:** AI kesin fiyat vermez — bu guardrail'dir (sağlık riskli alan)

> 💡 **Vay Be Anı:** Pazar akşamı saat 21:00. Bir hasta Instagram DM'den diş fotoğrafı attı ve "Bu diş kurtarılabilir mi? Fiyat ne olur?" yazdı. Elif evdeydi, tatilini yaşıyordu. Ama Invekto 12 saniyede cevap verdi: "Fotoğrafınız için teşekkürler! Kesin tedavi planı muayenede belirlenir. Yarın Pazartesi 10:00 veya 14:30 müsaitiz. Hangisi size uyar?" Hasta 10:00'ı seçti. Pazartesi geldi, 35.000 TL'lik implant tedavisine başladı. Hasta sonra şunu söyledi: "5 kliniğe yazdım, sadece siz cevap verdiniz. Diğerleri Pazartesi'ye kadar dönmedi bile." Bir Pazar gecesi, 12 saniye, 35.000 TL.

---

### Senaryo 28 — Randevu Alma: Uygun Saat ve Doktor Seçimi

Her gün 20-40 kez yapılan işlem. Resepsiyonist ajandaya bakıyor, doktor müsaitliğini kontrol ediyor, hastaya dönüyor — 5-10 dakika.

**Invekto ile:**
- AI: "Dr. Mehmet bu hafta Salı 10:00 ve Perşembe 14:00'te müsait. Hangisini tercih edersiniz?"
- Hasta seçer → randevu otomatik kaydedilir
- Hatırlatma zinciri başlar

> 💡 **Vay Be Anı:** Elif bir gün saydı: sabah 09:00-12:00 arası tam 23 kez randevu telefonu geldi. Her biri 5-7 dakika sürüyordu — ajandayı aç, doktora sor, hastaya dön, onay al, kaydet. Toplam 2 saat sadece randevu. Invekto devreye girince hastalar WhatsApp'tan "randevu" yazdı, AI müsait saatleri gösterdi, hasta tek dokunuşla seçti. Elif'in 2 saatlik işi 15 dakikaya düştü. "Artık hastayla göz göze konuşabiliyorum, telefonu kulağıma yapışık değil" dedi. Ama asıl güzel olan: hastalar da memnun. "3 dakikada randevumu aldım, kimseyi beklemedim" mesajları gelmeye başladı.

---

### Senaryo 30 — Acil Ağrı: Gece Mesajı, Triage Gerekiyor

Gece 2'de hasta yazıyor: "Dişim çok ağrıyor, dayanamıyorum!" Bu mesajı sabaha erteleyemezsiniz.

**Invekto ile:**
- AI acil intent tespit eder → yüksek öncelik etiketi
- Otomatik cevap: "Acil durumunuzu anladık. Ağrı kesici olarak [genel bilgi] alabilirsiniz. Sabah ilk randevu için sizi kayıt ediyoruz. Acil bir durum ise en yakın hastane acil servisine başvurunuz"
- Doktora push bildirim (mobil uygulamada)
- **Guardrail:** AI ilaç dozajı veya tedavi önerisi YAPMAZ — sadece genel bilgi + yönlendirme

> 💡 **Vay Be Anı:** Gece 02:15. Ayşe Hanım uyandı, sol çenesinde dayanılmaz ağrı. Panikle Google'a yazdı: "diş ağrısı ölümcül olabilir mi?" — korkunç sonuçlar çıktı, uykusu kaçtı. Sonra aklına geldi, kliniğin WhatsApp'ını denedi. 5 saniyede cevap geldi: "Acil ağrınızı anlıyoruz. Geçici rahatlama için soğuk kompres uygulayabilirsiniz. Sabah 09:00'da size ilk randevuyu ayırdık. Ama: ateş, yüzde şişlik veya nefes darlığı varsa hemen 112'yi arayın." Ayşe Hanım derin bir nefes aldı. Biri vardı, biri ilgileniyordu. Sabah geldi, 45 dakikada tedavisi bitti. Çıkışta resepsiyona döndü: "Gece 2'de bile cevap vermeniz... ben başka kliniğe gitmem artık." 6 ay sonra tüm ailesini getirdi — eşi, annesi, oğlu. Toplam değer: 78.000 TL. Hepsi gece 2'deki o 5 saniyelik mesajla başladı.

---

### Senaryo 31 — Tedavi Planı: İki Seans, Süreç Anlatımı

Hasta implant yapacak ama süreci bilmiyor. "Kaç seans? Ne kadar sürer? Acıyor mu?"

**Invekto ile:**
- AI Knowledge veritabanından tedavi süreci bilgisi çeker
- "İmplant süreci: 1) Muayene ve planlama 2) İmplant yerleştirme (45dk) 3) 3 ay iyileşme 4) Protez takma. Detaylı bilgi için muayene randevusu oluşturalım mı?"
- Hasta sorularına interaktif cevap verilir

> 💡 **Vay Be Anı:** Hasan Bey implant düşünüyordu ama korkuyordu. "Kaç seans? Acıyor mu? Ne kadar sürer?" yazdı. Invekto adım adım anlattı: "4 aşama, toplam 3-4 ay, implant yerleştirme sadece 45 dakika, lokal anesteziyle ağrısız." Hasan Bey "ama internette 6 ay diyor?" diye sordu. AI hemen açıkladı: "Sizin durumunuz muayenede netleşir, bazı vakalarda 3 ay yeterli." Hasan Bey rahatladı, randevu aldı. Muayenede Dr. Burak: "Sistem hastayı zaten bilgilendirmiş, bana sadece kişiselleştirilmiş plan kaldı." 15 dakikalık muayene yerine 7 dakikada bitti — çünkü temel sorular zaten cevaplanmıştı. Hasan Bey tedaviye "hazır" geldi, tereddütsüz onay verdi. 42.000 TL'lik plan, bir bilgilendirme mesajıyla satıldı.

---

### Senaryo 33 — Hasta Kimlik/Rapor Gönderdi: KVKK Riski

Hasta TC kimlik, kan testi sonucu veya röntgen gönderdi. Bu özel nitelikli kişisel veri — KVKK'da en ağır korunan kategori.

**Invekto ile:**
- Sistem hassas veri gönderimini algılar
- Otomatik uyarı: veri şifreli kanalda saklanır
- Erişim kontrolü: sadece yetkili kişiler görür
- Saklama süresi politikası uygulanır

> 💡 **Vay Be Anı:** Bir hasta WhatsApp'tan kan tahlili sonucunu ve TC kimlik fotoğrafını gönderdi. Normal bir klinikte bu veriler WhatsApp'ın yedeklemesiyle her telefondan erişilebilir hale gelir — stajyer dahil. Invekto'da ise sistem anında hassas veriyi algıladı, şifreli kanalda sakladı, sadece yetkili doktor ve koordinatöre erişim verdi. Hasta 6 ay sonra KVKK kapsamında "verilerimi silin" dedi. Tek tıkla silindi, rapor oluşturuldu. Dr. Burak: "KVKK denetimi gelse bile hazırız. Eskiden hastaların raporları 3 ayrı telefonda, kim bilir kaç yedeklemede duruyordu. Şimdi her şey kayıt altında." Bu güvenlik hissi hastalara da yansıyor — "verilerim güvende" bilen hasta, daha rahat bilgi paylaşıyor.

---

### Senaryo 35 — "Dolgu Düştü" — Tekrar Randevu

Hasta şikayet ediyor. Hızlı müdahale gerekiyor, yoksa kötü yorum gelir.

**Invekto ile:**
- AI empati cevabı önerir: "Çok üzgünüz! Bu hemen düzeltilecek bir durum. Yarın saat kaçta gelebilirsiniz?"
- Acil randevu slotu sunulur
- Doktora bildirim gönderilir

> 💡 **Vay Be Anı:** Cumartesi akşamı, düğüne 3 saat kala. Zehra Hanım WhatsApp'a yazdı: "Ön dişimdeki dolgu düştü, yarın düğünüm var, ne yapacağım?!" Invekto anında cevap verdi: "Çok üzgünüz Zehra Hanım! Bu acil durumunuzu anlıyoruz. Bugün saat 18:30'da acil slotumuz müsait. Gelebilir misiniz?" Zehra Hanım koştu geldi, 30 dakikada dolgusu yenilendi. Düğüne gülerek gitti. Ertesi hafta Google'a 5 yıldızlı yorum yazdı: "Düğünümden 3 saat önce dolgum düştü. Cumartesi akşamı bile acil randevu verdiler. Hayat kurtarıcı klinik!" O yorum 4.200 kişi tarafından görüldü. 3 saatlik acil müdahale, binlerce kişiye ulaşan bir güven reklamına dönüştü.

---

### Senaryo 36 — Ödeme/Kapora: Rezervasyon İçin Ödeme Linki

Hasta randevu aldı ama kapora isteniyor (özellikle estetik klinikte).

**Invekto ile:**
- AI ödeme bilgisi gönderir: "Randevunuzu kesinleştirmek için 1.000 TL kapora gerekiyor. Ödeme linki: ..."
- Phase 2'de iyzico/PayTR entegrasyonu ile tek tıkla ödeme
- Ödeme gelince → randevu kesinleşir, onay mesajı gönderilir

> 💡 **Vay Be Anı:** Zeynep eskiden kapora takibini Excel'de yapıyordu. "Ahmet Bey'den kapora geldi mi?" diye her gün kontrol ediyordu. Bazı hastalar "yatırdım" diyordu ama yatırmamıştı — randevu günü gelmeyince anlaşılıyordu. Invekto ile ödeme linki WhatsApp'tan gidiyor, hasta tek tıkla ödüyor, sistem otomatik onay gönderiyor. Geçen ay 34 hastaya kapora linki gitti, 31'i aynı gün içinde ödedi. Kalan 3'e otomatik hatırlatma gitti, 2'si ödedi. Toplam no-show: 1 kişi (eskiden 8-10 kişiydi). Zeynep: "Excel'i kapattım, artık kapora takibim yok çünkü sistem kendisi yapıyor." Aylık kurtarılan gelir: 45.000 TL.

---

### Senaryo 38 — Çocuk Hasta: Korku Yönetimi + Randevu

Veli yazıyor: "6 yaşında kızım var, diş çürüğü var ama çok korkuyor."

**Invekto ile:**
- AI empati ve güven mesajı: "Anlıyoruz! Dr. Ayşe çocuk diş hekimimiz, küçük hastalarımıza çok iyi yaklaşıyor. İlk seferde sadece tanışma yapıyoruz"
- Çocuk dostu template mesajlar
- KVKK: çocuk verisi = özel koruma, veli onayı zorunlu

> 💡 **Vay Be Anı:** Fatma Hanım yazdı: "6 yaşında kızım var, dişçiden çok korkuyor, ağlayarak kaçıyor." Invekto cevap verdi: "Anlıyoruz Fatma Hanım! Dr. Ayşe çocuk diş hekimimiz, küçük hastalarımıza özel yaklaşımı var. İlk seansta sadece tanışma yapıyoruz — koltuğa oturmak bile zorunlu değil." Fatma Hanım randevu aldı. İlk seansta Dr. Ayşe, küçük Elif'e aynayı verdi, birlikte dişlere baktılar, oyun oynadılar. Elif ağlamadı. Tedavi 2. seansta başladı, sorunsuz bitti. Fatma Hanım sonra yazdı: "Kızım artık 'dişçiye gidelim mi?' diyor. 2 yıldır ağlaya ağlaya götürdüğüm çocuk, kendi istiyor." Bu mesajı gören Dr. Burak onu klinik Instagram'ında paylaştı (izinle). 340 beğeni, 28 yorum, 7 yeni veli randevusu.

---

### Senaryo 40 — Randevu İptal/Erteleme Yoğunluğu

Klinik telefonu çalıyor: "İptal etmek istiyorum." Bu, gün içinde 5-10 kez tekrarlanan bir süreç.

**Invekto ile:**
- AI: "Randevunuzu iptal etmek yerine başka bir güne erteleyebilir miyiz?"
- İptal edilirse → bekleme listesindeki hastaya haber verilir
- No-show tracking: sık iptal eden hastalar için depozit politikası uygulanır

> 💡 **Vay Be Anı:** Elif bir Pazartesi sabahı 7 iptal telefonu aldı — 40 dakika sadece iptal işlemi. Invekto sonrası hastalar WhatsApp'tan "iptal" yazdığında AI önce sordu: "Başka bir güne erteleyebilir miyiz? Bu hafta Çarşamba 11:00 ve Cuma 15:00 müsait." Sonuç: 10 iptal talebinin 6'sı ertelemeye dönüştü. Kalan 4 iptal anında bekleme listesine düştü, 3'ü 15 dakika içinde doldu. Elif'in iptal süresı 40 dakikadan 5 dakikaya indi. Ama asıl rakam şu: ayda 24 "iptal → erteleme" dönüşümü x ortalama 2.800 TL tedavi değeri = 67.200 TL kurtarılan gelir. Sırf "İptal etmek yerine erteleyelim mi?" sorusuyla.

---

### Senaryo 42 — Doktor Meşgul: Ön Büro Cevap Veremiyor

Doktor koltukta hasta ile ilgileniyor. Ön büro fiyat sorusuna cevap veremiyor çünkü "fiyat doktorun vermesi gereken bilgi."

**Invekto ile:**
- AI doktor onaylı fiyat aralıklarını Knowledge veritabanından çeker
- Ön büro doktoru beklemeden cevap verebilir
- Kesin fiyat için "muayene gerekli" kuralı korunur

> 💡 **Vay Be Anı:** Dr. Burak saat 11:00'de implant cerrahisi yapıyordu. Tam o sırada 3 fiyat sorusu geldi WhatsApp'tan. Eskiden Elif "doktor çıkınca soracağım" diyordu — doktor saat 13:00'te çıkıyordu, 2 saat geçmişti, hastalar çoktan başka kliniğe yazmıştı. Invekto ile Elif tek tıkla AI'nın hazırladığı fiyat aralığı cevabını gönderdi: "İmplant tedavimiz 15.000-45.000 TL aralığında, vakanıza özel fiyat muayenede belirlenir." 3 hastanın 2'si hemen randevu aldı. Dr. Burak koltuktan kalkmadı, Elif doktoru beklemedi, hastalar 2 saat kaybetmedi. Herkes kazandı. Elif: "Eskiden 'doktora soracağım' deyip hastayı kaybediyordum. Şimdi doktor onaylı bilgiyi anında veriyorum."

---

## Estetik Klinik Saha Senaryoları (51-75)

### Senaryo 51 — "Instagram DM: Fiyat nedir? Botox/dolgu"

Estetik kliniğin Instagram'ı vitrindir. Her DM bir potansiyel hasta — ama cevap gecikmesi %50 kayıp demek.

**Invekto ile:**
- IG DM tek ekrandan yönetim
- AI: "Botox fiyatlarımız bölgeye göre 3.000-8.000 TL arasında değişiyor. Yüz yüze değerlendirme ile size özel plan oluşturuyoruz. Bu hafta randevu oluşturmamı ister misiniz?"
- Lead takibi: DM → WhatsApp geçişi → randevu → tedavi

> 💡 **Vay Be Anı:** Dr. Selin Instagram analitiğine baktı: geçen ay 127 DM gelmiş, ekip sadece 68'ine cevap vermiş. 59 cevapsız DM. Her DM ortalama 6.000 TL potansiyel. 59 x 6.000 = 354.000 TL havaya uçmuş. Invekto sonrası: 127 DM'nin 127'sine 15 saniye içinde cevap verildi. 127'nin 43'ü randevu aldı, 31'i tedavi oldu. 31 x 6.000 = 186.000 TL. Eskiden 68 cevapta 18 tedavi, şimdi 127 cevapta 31 tedavi. Dr. Selin: "Bu rakamları görene kadar kaçan hastaları bilmiyordum. Bilmediğin şeyi ölçemezsin, ölçemediğin şeyi düzeltemezsin."

---

### Senaryo 52 — Before/After Fotoğraf İsteği + Güven Sorusu

"Öncesi sonrası fotoğraf gösterir misiniz?" — bu, hastanın güven inşa ettiği andır.

**Invekto ile:**
- AI Knowledge'dan uygun before/after galeri linki gönderir
- "Benzer vakaları görmek ister misiniz? [link]"
- KVKK uyumlu: fotoğraflar hasta onayı ile paylaşılır
- Güven mesajı: "Dr. Selin 5.000+ başarılı işlem gerçekleştirmiştir"

> 💡 **Vay Be Anı:** Deniz Hanım dudak dolgusu düşünüyordu ama korkuyordu: "Ya doğal durmaysa? Ya abartılı olursa?" WhatsApp'tan sordu: "Öncesi sonrası fotoğraf var mı?" Invekto 10 saniyede, KVKK onaylı 5 benzer vaka fotoğrafını carousel formatında gönderdi. Her fotoğrafın altında: "Doğal dolgu, 1ml, Dr. Selin uygulaması." Deniz Hanım: "Tam istediğim gibi doğal görünüyor!" Hemen randevu aldı. İşlem sonrası kendi before/after fotoğrafını çektirdi ve paylaşım izni verdi. O fotoğraf sonraki 3 ayda 14 hastaya gösterildi ve 9'u randevu aldı. Bir memnun hasta, 9 yeni hasta getirdi — en güçlü pazarlama.

---

### Senaryo 53 — DM'den WhatsApp'a Geçiş

Instagram DM'de konuşma başlıyor ama detaylı bilgi WhatsApp'ta devam etmeli. "Numaranızı atın" derken hasta kayboluyor.

**Invekto ile:**
- AI doğal geçiş sağlar: "Detaylı bilgi ve randevu için WhatsApp'tan devam edebilir miyiz? [link]"
- Geçiş yapılınca → konuşma geçmişi Unified Timeline'da birleşik görünür
- Lead kaybolmaz

> 💡 **Vay Be Anı:** Zeynep eskiden Instagram DM'de hasta numarasını istiyordu: "WhatsApp numaranızı atar mısınız?" Bu soruda 10 hastadan 4'ü kayboluyordu — numarasını vermek istemiyordu, ya da mesajı görüp unutuyordu. Invekto ile AI doğal bir geçiş linki sunuyor: "Detaylı bilgi ve randevu için WhatsApp'tan devam edebilir miyiz? [tek tıkla link]" Hasta tıklıyor, WhatsApp açılıyor, konuşma kaldığı yerden devam ediyor. Kayıp oranı %40'tan %8'e düştü. Zeynep: "Eskiden DM'de 'numaranızı atın' diyordum, hasta bir daha yazmıyordu. Şimdi tek tıkla geçiyor ve hastanın ne sorduğunu WhatsApp'ta da görüyorum — sıfırdan başlamıyoruz."

---

### Senaryo 56 — Uygunluk / Kontrendikasyon Soruları

"Hamileyken botox yaptırabilir miyim?", "Diyabetim var, implant olabilir mi?"

**Invekto ile:**
- **KRİTİK GUARDRAIL:** AI kesinlikle tıbbi tavsiye vermez
- Cevap: "Bu soruyu doktorumuzla değerlendirmemiz gerekiyor. Muayene randevusu oluşturalım mı?"
- Genel güvenlik bilgisi verilebilir ama kesin yorum yapılmaz
- Handoff: AI → doktora yönlendirme (CS-02)

> 💡 **Vay Be Anı:** Bir hasta yazdı: "Kan sulandırıcı kullanıyorum, botox yaptırabilir miyim?" Tehlikeli bir soru — yanlış cevap ciddi sağlık riski. Invekto'nun guardrail'i devreye girdi: "Bu önemli bir sağlık sorusu ve kesinlikle doktorumuzun değerlendirmesi gerekiyor. Size özel bir ön görüşme randevusu oluşturalım mı? Doktorumuz ilaç geçmişinizi değerlendirip size en güvenli planı sunacak." Hasta randevu aldı. Dr. Selin muayenede: "İyi ki doğrudan botox randevusu vermemişler. Bu hastada önce kardiyologla konsültasyon gerekiyordu." Hasta tedavi sonrası: "Başka klinikte direkt yaptıracaklardı, burada önce sağlığımı düşündüler." Bu güven, parayla satın alınamaz. O hasta 2 yılda 85.000 TL'lik işlem yaptırdı — çünkü "burada beni koruyorlar" hissetti.

---

### Senaryo 57 — İşlem Sonrası Şikayet: Şişlik/Morarma Panik

"Botox sonrası yüzüm şişti, normal mi?!" — hasta panik halinde.

**Invekto ile:**
- AI yüksek öncelikli: sağlık riski tespit
- Sakinleştirme mesajı: "İlk 24-48 saatte hafif şişlik normaldir. Ama belirtileriniz şiddetliyse hemen kliniğimize ulaşın"
- Doktora acil bildirim
- **Guardrail:** "Normal" deme — "genel bilgi" ver, kesin tanı koymaktan kaçın

> 💡 **Vay Be Anı:** Gece 23:45. Seda Hanım dudak dolgusu yaptırmıştı, aynaya baktı, dudakları şişmişti. Panikle Google'a yazdı: "dudak dolgusu sonrası şişlik tehlikeli mi?" Korkunç haberler, alerjik reaksiyon fotoğrafları... Kalbi hızlandı. WhatsApp'tan kliniğe yazdı: "Dudaklarım çok şişti, korkuyorum!" 5 saniyede cevap geldi: "Seda Hanım, ilk 24-48 saatte şişlik beklenen bir durumdur. Soğuk kompres uygulayabilirsiniz. Ama: nefes darlığı, şiddetli ağrı veya ateş varsa hemen acile gidin. Sabah doktorumuz sizi arayacak." Seda Hanım rahatladı, uyudu. Sabah Dr. Selin aradı, her şey normaldi. Seda Hanım: "O gece o mesaj olmasaydı acile koşacaktım — hem gereksiz yere hem de panikle. Biri vardı, biri baktı. Bu kliniği bırakmam." 6 arkadaşına önerdi. 6 arkadaş x ortalama 8.000 TL = 48.000 TL. Bir gece mesajının getirisi.

---

### Senaryo 58 — Paket Satış: Lazer 6 Seans

"Lazer epilasyon 6 seans paketi var mı?" — yüksek değerli satış fırsatı.

**Invekto ile:**
- AI paket fiyatını ve avantajını sunar: "6 seans lazer paketi: 12.000 TL (tek seans 2.500 TL, paket ile %20 tasarruf)"
- Randevu oluşturma + ödeme linki gönderme
- Paket takibi: hangi seans yapıldı, kaç kaldı

> 💡 **Vay Be Anı:** Melis "lazer fiyat?" yazdı. Invekto hemen cevap verdi: "Tek seans 2.500 TL, 6 seans paketi 12.000 TL (%20 tasarruf — 3.000 TL indirim!)" Melis paketi aldı. 3. seanstan sonra motivasyonu düştü, "erteliyorum" dedi. Invekto T+30 gün hatırlatma gönderdi: "Melis Hanım, lazer paketinizde 3 seans kaldı. 4. seans için bu hafta uygun musunuz? Düzenli aralıklarla en iyi sonuç alırsınız." Melis geldi, 6 seansı tamamladı. Sonra ne oldu? "Koltuk altı da yaptırmak istiyorum" — 2. paket satıldı. İlk paketin takibi, ikinci paketi getirdi. Toplam değer: 24.000 TL. Takip yapılmasaydı, Melis 3. seansta bırakırdı — 6.000 TL'si klinğin cebinden giderdi.

---

### Senaryo 59 — Fiyat Pazarlığı + Kampanya

"Çok pahalı, indirim yapar mısınız?" — Türkiye'de çok sık. AI'nın doğru yaklaşımı kritik.

**Invekto ile:**
- AI kampanya bilgisini sunar: "Şu an dolgu işlemlerinde %15 kampanyamız var"
- Pazarlık isteği → doktora/yöneticiye yönlendirme
- **Guardrail:** AI kendi başına indirim sözü vermez

> 💡 **Vay Be Anı:** "12.000 TL çok pahalı, indirim yapar mısınız?" Eskiden Zeynep bu mesajı doktora iletirdi, doktor koltukta olurdu, 3 saat sonra dönerdi, hasta çoktan başka klinikten fiyat almıştı. Invekto ile AI hemen aktif kampanyayı sundu: "Şu an dolgu işlemlerinde %15 kampanyamız var — 12.000 TL yerine 10.200 TL! Bu kampanya bu hafta sonu bitiyor." Hasta: "Tamam randevu alayım." 30 saniyede çözüldü. Ama asıl değerli olan guardrail: AI kendi başına indirim sözü vermedi. Kampanyada olmayan bir tedavide "maalesef şu an kampanyamız yok ama muayenede size özel plan oluşturabiliriz" dedi ve doktora yönlendirdi. Doktor uygun gördüğü hastaya özel teklif verdi. Zeynep: "AI pazarlığa girmedi, ama mevcut kampanyayı anında sunarak hastayı kaybetmemi engelledi."

---

### Senaryo 60 — Yorum/Şikayet Yönetimi (Sosyal Kanıt)

Google/Instagram'da yorum geldi — iyi veya kötü. İki durumda da hız kritik.

**Invekto ile:**
- Kötü yorum → proaktif WhatsApp iletişimi: "Geri bildiriminizi gördük, çözmek istiyoruz"
- İyi yorum → teşekkür + referans teklifi
- S10 yorum motoru ile entegre

> 💡 **Vay Be Anı:** Cuma akşamı Google'a 1 yıldızlı yorum geldi: "Randevum vardı, 45 dakika bekledim. Saygısızlık." Dr. Selin Pazartesi görecekti. Invekto Cuma akşamı algıladı ve hastaya WhatsApp mesajı gönderdi: "Geri bildiriminizi gördük, bekleme süreniz için çok özür dileriz. Durumu araştırıp size özel çözüm sunmak istiyoruz." Hasta şaşırdı — bu kadar hızlı dönüş beklemiyordu. Pazartesi Dr. Selin aradı, ücretsiz seans teklif etti. Hasta yorumunu güncelledi: "Sorunu çok hızlı çözdüler, 5 yıldıza yükseltiyorum." Google'da potansiyel hastaların %89'u olumsuz yoruma verilen yanıtı okuyor. "Çözdüler" yanıtlı kötü yorum, hiç yorum olmayan klinikten daha güvenilir görünüyor.

---

### Senaryo 63 — Click-to-WhatsApp Reklam Lead'i

Instagram reklamında "Şimdi Yaz" butonu → WhatsApp'a düşüyor. Bu lead'in değeri yüksek çünkü reklama para harcanmış.

**Invekto ile:**
- UTM tracking ile reklam kaynağı kaydedilir (hangi kampanya, hangi reklam)
- AI hızlı karşılama + niyete uygun cevap
- Dashboard'da reklam ROI'si görünür: "Bu kampanyadan 45 lead, 12 randevu, 8 tedavi"

> 💡 **Vay Be Anı:** Dr. Selin ayda 25.000 TL Instagram reklamı veriyordu. "Kaç hasta geldi?" diye sorduğunda kimse net cevap veremiyordu. Invekto ile her Click-to-WhatsApp lead'i UTM ile etiketlendi. Ay sonunda rapor: "Kampanya A: 45 lead → 12 randevu → 8 tedavi → 64.000 TL gelir. Kampanya B: 62 lead → 7 randevu → 3 tedavi → 18.000 TL gelir." Dr. Selin hemen gördü: Kampanya A'da lead başına maliyet düşük, dönüşüm yüksek. Kampanya B pahalı lead getiriyor ama dönmüyor. Bütçeyi yeniden dağıttı — Kampanya A'ya ağırlık verdi. Sonraki ay: aynı 25.000 TL reklam bütçesiyle %40 daha fazla hasta. "Artık karanlıkta reklam vermiyorum, her kuruşun nereye gittiğini biliyorum" dedi.

---

### Senaryo 66 — Doktor Onayı Gereken Lead'ler

Bazı tedaviler doktor onayı gerektirir. Ön büro karar veremez.

**Invekto ile:**
- AI lead'i filtreler: "Bu hastanın sorusu doktor değerlendirmesi gerektiriyor"
- Doktora bildirim: fotoğraf + hasta bilgisi + soru
- Doktor onayladıktan sonra → hastaya cevap gönderilir
- Doktor koltuktan kalkmadan onay verir (mobil uygulama ile)

> 💡 **Vay Be Anı:** Bir hasta burun estetiği sordu ve fotoğraf gönderdi. Zeynep biliyordu: bu soruya kendi başına cevap veremez, doktor değerlendirmesi şart. Eskiden: doktora haber ver, doktor seanslar arasında 5 dakika bulsun, fotoğrafı görsün, Zeynep'e söylesin, Zeynep hastaya yazsın — ortalama 4-6 saat. Invekto ile: AI lead'i "doktor onayı gerekli" olarak etiketledi, fotoğraf + hasta bilgisiyle Dr. Selin'in telefonuna push bildirim gönderdi. Dr. Selin iki hasta arasındaki 2 dakikalık molada telefona baktı, "uygun vaka, ön görüşme randevusu verin" diye onayladı. Hastaya 18 dakikada dönüldü. Hasta: "Bu kadar hızlı cevap beklemeiyordum, hemen randevu alıyorum." O hasta 55.000 TL'lik burun estetiği yaptırdı. 4-6 saat beklese, çoktan başka kliniğe gitmişti.

---

## Sağlık Ek Senaryoları

### SB-01 — Tedavi Planı Onay Akışı

Doktor tedavi planı gönderdi — 30.000 TL'lik implant planı. Hasta "düşüneyim" dedi ve kayboldu.

**Invekto ile:**
- T+1 gün: "Tedavi planınızı incelediniz mi? Sorularınız varsa yardımcı olabiliriz"
- T+3 gün: "Planınız hâlâ aktif. Randevu oluşturmak ister misiniz?"
- T+7 gün: Son hatırlatma
- Onay gelmezse → yöneticiye alert: "30.000 TL'lik plan onaysız, hasta kaybolma riski"

> 💡 **Vay Be Anı:** Dr. Burak geçen ay 12 hastaya tedavi planı gönderdi. 7'si "düşüneyim" dedi ve kayboldu. 7 x ortalama 25.000 TL = 175.000 TL havada. Invekto devreye girdi. Bu ay yine 12 plan gönderildi. T+1'de nazik hatırlatma, T+3'te "sorularınız varsa buradayım" mesajı, T+7'de son hatırlatma gitti. Sonuç: 12 planın 9'u onaylandı. 3 onaysız plan için yöneticiye alert gitti, biri telefonla arandı ve ikna edildi. 10 onay x 25.000 TL = 250.000 TL. Fark: 175.000 TL'den 250.000 TL'ye — sadece zamanında hatırlatma ile. Dr. Burak: "Bu hastaları kaybettiğimizi biliyordum ama peşlerinden koşacak zamanımız yoktu. Şimdi sistem koşuyor."

---

### SB-03 — Çoklu Klinik/Şube Yönetimi

Zincir klinik, 3 şube. Hasta yanlış şubeye mesaj atıyor.

**Invekto ile:**
- Konum bazlı yönlendirme: "Kadıköy şubemizde Dr. Mehmet Pazartesi-Çarşamba, Beşiktaş'ta Perşembe-Cumartesi"
- Merkezi dashboard: tüm şubelerin performansı tek ekranda

> 💡 **Vay Be Anı:** 3 şubeli zincir klinik. Hasta Kadıköy şubesine yazdı: "Randevu almak istiyorum ama Beşiktaş'a daha yakınım." Eskiden: "Beşiktaş numarasını veriyim, onlara yazın" — hasta yarısı yazmıyordu, kayboluyordu. Invekto ile: "Beşiktaş şubemizde Dr. Mehmet bu hafta Perşembe 10:00 ve Cuma 14:00'te müsait. Hangisi size uyar?" Hasta hemen Perşembe'yi seçti. Merkezi dashboard'da 3 şubenin performansı yan yana: Kadıköy %85 doluluk, Beşiktaş %62, Bakırköy %71. Klinik sahibi gördü: "Beşiktaş'ta kapasite var, Kadıköy'den yönlendirme mantıklı." 1 ayda Beşiktaş doluluk oranı %62'den %78'e çıktı. 3 şubeyi tek ekrandan yönetmek — her hasta doğru yere, her doktor dolu koltuğa.

---

### SB-04 — Tedavi Öncesi Hazırlık Talimatları (Pre-Op)

İmplant yapılacak: "12 saat açlık, aspirin kesin, refakatçi ile gelin."

**Invekto ile:**
- R-3 gün: Genel hazırlık talimatları
- R-1 gün: Hatırlatma + soru varsa "şimdi sorun"
- R-sabah: Son kontrol + klinik adresi
- Tedavi tipine göre özel talimat (implant vs çekim vs estetik farklı)

> 💡 **Vay Be Anı:** Ali Bey implant cerrahisine geldi. Sabah kahvaltı yapmıştı — 12 saat açlık kuralını unutmuştu. Ameliyat iptal, koltuk boş kaldı, yeni randevu 2 hafta sonraya atıldı. Bu hem Ali Bey'i hem kliniği mağdur etti. Invekto sonrası: R-3 gün mesajı gitti: "Ameliyatınız 3 gün sonra! Hazırlık: 12 saat açlık, aspirin/kan sulandırıcı 1 hafta önceden kesilmeli, refakatçi ile gelin." R-1 gün: "Yarın sabah ameliyatınız var. Hatırlatma: bu gece 22:00'den sonra yeme-içme yok. Sorularınız var mı?" R-sabah: "Bugün günü geldi! Klinik adresimiz: ... Refakatçinizle birlikte 08:30'da bekliyoruz." Ali Bey her adımda bilgilendirildi, hazır geldi, ameliyat zamanında başladı. Son 6 ayda hazırlık eksikliğinden iptal edilen ameliyat: 0. Eskiden ayda 2-3 kez yaşanıyordu. Her iptal = 15.000-40.000 TL ertelenmiş gelir + hasta memnuniyetsizliği.

---

## Sağlık Grand Slam Offers

### Offer: Invekto for Dental
> **Sonuç vaadi:** "Fiyat sorularını randevuya çevir, no-show'u %60 azalt"
> **Fiyat:** 7.500 TL/ay
> **Garanti:** 30 günde no-show düşmezse 2. ay ücretsiz
> **Risk:** "Mevcut sisteminiz aynen çalışır, AI katmanı üstüne biner"

### Offer: Invekto for Clinics (Estetik)
> **Sonuç vaadi:** "Lead'leri hastaya dönüştür, medikal turizmi ölçekle"
> **Fiyat:** 15.000-25.000 TL/ay
> **Garanti:** 30 günde randevu dönüşümü artmazsa 2. ay ücretsiz
> **Risk:** "Mevcut iletişiminiz kesintisiz devam eder"

---

## Invekto Servisleri — Sağlık İçin Ne Yapıyor?

| Servis | Port | Sağlık'ta Görevi |
|--------|------|------------------|
| **Backend** | 5000 | Tüm mesajları toplar, routing yapar, klinik dashboard'u gösterir |
| **ChatAnalysis** | 7101 | Hasta mesajını analiz eder: acil mi, şikayet mi, fiyat sorusu mu, randevu talebi mi |
| **Automation** | 7108 | Chatbot: fiyat sorusu → randevu akışı, randevu hatırlatma, tedavi sonrası takip zincirleri |
| **AgentAI** | 7105 | Ön büroya cevap önerisi, hasta niyetini anlama, doktor onayı gerektiren leadleri filtreleme |
| **Outbound** | 7107 | Randevu hatırlatma, tedavi sonrası takip, kampanya duyurusu, Google yorum rica |
| **Knowledge** | 7104 | Tedavi bilgileri, fiyat aralıkları, doktor profilleri, bakım talimatları, sigorta bilgileri |
| **FaceAnalysis** | 7110 | Selfie'den yüz analizi → kişiselleştirilmiş tedavi önerisi (estetik klinikte) |

---

## Özet Tablo — Tüm Sağlık Senaryoları

### Diş Kliniği (26-50)

| # | Senaryo | Etki | Phase |
|---|---------|------|-------|
| 27 | IG DM: foto ile fiyat sorusu | YÜKSEK | 1 |
| 28 | Randevu alma | YÜKSEK | 1-2 |
| 30 | Acil ağrı (gece triage) | YÜKSEK | 1 |
| 31 | Tedavi planı anlatımı | ORTA | 1 |
| 33 | KVKK: hasta kimlik/rapor | ORTA | 1-2 |
| 35 | Şikayet: dolgu düştü | YÜKSEK | 1 |
| 36 | Kapora/ödeme linki | ORTA | 1-2 |
| 37 | Sigorta sorusu | ORTA | 2-3 |
| 38 | Çocuk hasta korku yönetimi | ORTA | 1 |
| 39 | Mesaj kayıt/raporlama | DÜŞÜK | 1 |
| 40 | Randevu iptal yoğunluğu | YÜKSEK | 1 |
| 41 | Beyazlatma kampanyası IG lead | ORTA | 1-2 |
| 42 | Doktor meşgul | ORTA | 1 |
| 43 | Fiyat/plan tutarsızlığı | ORTA | 1 |
| 44 | Fotoğraf data loss | DÜŞÜK | 2 |
| 46 | Gece/hafta sonu oto-cevap | DÜŞÜK | 1 |
| 47 | İkinci görüş dosyaları | DÜŞÜK | 2 |
| 48 | Kapora iade | DÜŞÜK | 2 |
| 49 | Hekim notları + etiketli rapor | DÜŞÜK | 2 |

### Estetik Klinik (51-75)

| # | Senaryo | Etki | Phase |
|---|---------|------|-------|
| 51 | IG DM: botox/dolgu fiyat | YÜKSEK | 1 |
| 52 | Before/after fotoğraf + güven | YÜKSEK | 1-2 |
| 53 | DM → WhatsApp geçişi | ORTA | 1 |
| 54 | Randevu + kapora | YÜKSEK | 1-2 |
| 56 | Kontrendikasyon soruları | ORTA | 1 |
| 57 | İşlem sonrası şikayet (panik) | YÜKSEK | 1 |
| 58 | Paket satış (lazer) | ORTA | 1-2 |
| 59 | Fiyat pazarlığı | ORTA | 1 |
| 60 | Yorum/şikayet yönetimi | YÜKSEK | 1-2 |
| 62 | KVKK: foto/video sağlık verisi | ORTA | 1-2 |
| 63 | Click-to-WA reklam lead | ORTA | 2 |
| 64 | Mesaj penceresi kapandı | ORTA | 1 |
| 65 | Çakışan cevaplar | ORTA | Mevcut |
| 66 | Doktor onayı gereken leadler | ORTA | 1-2 |
| 68 | Ödeme linki + taksit | ORTA | 2 |
| 69 | İşlem takvimi slot yönetimi | DÜŞÜK | 2 |
| 70 | "Doktorla konuşmak istiyorum" | DÜŞÜK | 1 |
| 71 | Ön değerlendirme formu | DÜŞÜK | 2 |
| 74 | Spam/yanlış tetik IG limitleri | DÜŞÜK | 1 |
| 75 | Hasta verisi saklama/erişim/silme | DÜŞÜK | 2-4 |

### Sağlık Ek (SB-01 to SB-05)

| # | Senaryo | Etki | Phase |
|---|---------|------|-------|
| SB-01 | Tedavi planı onay takibi | YÜKSEK | 2 |
| SB-02 | Sigorta provizyon | ORTA | 3-4 |
| SB-03 | Çoklu şube yönetimi | ORTA | 2 |
| SB-04 | Pre-op hazırlık talimatları | YÜKSEK | 2 |
| SB-05 | Reçete/ilaç sorguları | DÜŞÜK | 3 |
