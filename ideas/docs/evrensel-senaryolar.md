# Evrensel Senaryolar — Sektörlerüstü Altyapı ve Ortak Değer

> **Kapsam:** Tüm sektörlerde geçerli temel senaryolar — sektör bağımsız, platformun omurgası
> **Hedef Kitle:** E-ticaret, sağlık, otel, güzellik, eğitim — her Invekto müşterisi
> **Anahtar Vaat:** "Hiçbir senaryo bu altyapı olmadan tam çalışmaz"
> **Kritiklik:** Bu bölümdeki senaryoların çoğu BLOCKER — diğer sektörel senaryoların ön koşulu

---

## Neden Evrensel Senaryolar?

Bir e-ticaret mağazasının kargo takip sistemi, bir diş kliniğinin randevu hatırlatma sistemi, bir otelin yorum toplama sistemi — hepsi farklı sektörler ama hepsi aynı soruyu soruyor: "Bu mesajı kim yazdı? Daha önce ne yaşadı? Nasıl cevap vermeliyim? Yasal olarak bunu yapabilir miyim?"

Bu sorulara cevap veren altyapı, sektörden bağımsızdır. Bir hastanın rıza verip vermediği de, bir müşterinin opt-in'i olup olmadığı da aynı mekanizmayla yönetilir. AI'nın yanlış bilgi verme riski de, SLA breach durumunda failover da sektörden bağımsız kurgulardır.

Evrensel senaryolar, Invekto'nun tüm sektörlere değer üretebilmesinin temel taşlarıdır. Bunlar olmadan sektörel senaryolar eksik kalır — bir bina yapıyorsanız bu senaryolar temeldir.

---

## Rıza ve Uyum Altyapısı

### CS-01 — Opt-in Toplama ve Onam Yönetimi

**Bu Neden Var?**

WhatsApp Business'ın resmi kuralı net: 24 saatlik mesaj penceresi dışında bir müşteriye proaktif mesaj göndermek istiyorsanız, o müşterinin açık rızası (opt-in) olmalı. Kampanya mesajı, hatırlatma, yorum rica — hepsi bu kurala tabi.

Ama Invekto müşterilerinin çoğunda rıza toplama sistematik değil. "Bize yazdıysa izin vermiştir" yaklaşımı WhatsApp Business Policy'ye aykırı ve Meta hesap askıya alma nedeni.

**Kime Lazım?**

Outbound mesaj gönderen her işletme. Bu, neredeyse tüm Invekto müşterisi demek.

**Çözdüğü Sorun:**

- Yasal risk: Rızasız gönderim = Meta cezası + KVKK ihlali
- Operasyonel risk: Rıza kaydı yoksa kampanya gönderemezsin
- Güven riski: Müşteri "ben izin vermedim" dediğinde kanıt sunamamak

**Bu Olmadan Çalışmayan Senaryolar:**
- Sipariş sonrası proaktif satış (S4)
- No-show hatırlatma (S7)
- Yorum rica (S10)
- Check-out sonrası anket (O7)
- Sezonluk kampanya (O10)
- Tüm follow-up zincirleri

**Nasıl Çalışıyor?**

1. Müşteri ilk kez WhatsApp'tan yazdığında, web formunda kayıt olduğunda veya randevu aldığında rıza sorusu tetiklenir
2. "Bildirimlerimizi almak ister misiniz? (Evet/Hayır)" — interaktif buton
3. Cevap veritabanına kaydedilir: kim, ne zaman, hangi kanaldan, hangi kategori için
4. "STOP", "DUR" veya "İPTAL" yazan müşteri otomatik olarak listeden çıkar
5. Utility (işlem bildirimi) ve marketing (kampanya) kategorileri ayrı ayrı yönetilir — Meta politikası gereği
6. Denetim geldiğinde her rıza kaydı kanıt olarak sunulabilir

**Psikolojik Etki:**
İşletme için huzur — "yasal olarak güvenli" bilgisi. Müşteri için güven — "izin verdim, iptal edebiliyorum" hissi.

> 💡 **Vay Be Anı:** Cumartesi sabahı. Güzellik salonu sahibi 200 kişiye kampanya mesajı gönderdi — rızası olmayan 38 kişiye de. Pazartesi sabahı Meta'dan mail geldi: "Hesabınız inceleme altına alınmıştır." 3 gün boyunca hiçbir müşteriye mesaj gönderemedi, 12 randevu kaçtı, tahminen 6.800 TL gelir buharlaştı. Invekto'nun opt-in sistemiyle bu olay yaşanmaz: sadece rızası olan 162 kişiye mesaj gider, Meta hesabınız tertemiz kalır, kampanya huzur içinde sonuçlanır. O 3 günlük kabus bir daha yaşanmaz.

---

### CS-08 — KVKK/GDPR Compliance Otomasyonu

**Bu Neden Var?**

"KVKK'ya uygunuz" demek kolay ama kanıtlamak zor. Bir sağlık kliniğinde hastanın tedavi geçmişi, bir e-ticaret mağazasında müşterinin sipariş bilgileri, bir otel odasında misafirin pasaport fotokopisi — hepsi kişisel veri ve hepsinin kuralları var.

Denetim geldiğinde "kim bu veriye ne zaman erişti?" sorusuna cevap veremezseniz, ceza kaçınılmaz.

**Kime Lazım?**

Tüm işletmeler — ama özellikle sağlık sektörü (özel nitelikli veri) ve çocuk verisi işleyen eğitim kurumları.

**Çözdüğü Sorun:**

- Yasal zorunluluk: KVKK özel nitelikli veri ihlaline ağır para cezası
- Enterprise satış engeli: Büyük müşteriler compliance kanıtı istiyor
- Operasyonel kaos: "Verimi silin" talebi geldiğinde ne yapılacağı belli değil

**Nasıl Çalışıyor?**

1. **Açık onam toplama:** Her kanalda (WhatsApp, web, form) standart rıza akışı — CS-01 ile entegre
2. **Onam logu:** Kim, ne zaman, hangi kanaldan, ne için izin verdi — değiştirilemez kayıt
3. **Template denetim izi:** Gönderilen her şablon mesajın kaydı
4. **Veri silme hakkı:** Müşteri "verimi silin" dediğinde otomatik iş akışı başlar
5. **Veri erişim hakkı:** Müşteri "verilerim neler?" dediğinde otomatik rapor üretilir
6. **Saklama süreleri:** Sağlık verisi X yıl, ticari veri Y yıl — konfigüre edilebilir
7. **Maskeleme:** TC kimlik, telefon numarası, sağlık bilgisi ekranda maskelenir
8. **Erişim logu:** Kim hangi veriye ne zaman baktı — kayıt altında

**Psikolojik Etki:**
İşletme sahibi için: "Denetim gelse hazırız" güvencesi. Müşteri için: "Verilerim güvende" hissi.

> 💡 **Vay Be Anı:** Bir diş kliniğine KVKK denetimi geldi. Denetçi sordu: "Hasta Ahmet Yılmaz'ın verilerine son 6 ayda kim erişti?" Eski sistem: Excel'de kayıt yok, 3 saat boyunca e-postalarda arandı, sonunda "bilmiyoruz" denildi. Ceza: 150.000 TL. Invekto'lu klinik: Denetçi aynı soruyu sordu, 20 saniyede erişim logunu çıkardı — kim, ne zaman, hangi ekrandan baktı, hepsi kayıt altında. Denetçi "bu kadar düzenli bir sistem ilk kez görüyorum" dedi. Ceza: 0 TL. 150.000 TL'lik fark, Invekto'nun 4 yıllık abonelik bedelinden fazla.

---

## AI Güvenlik Katmanı

### CS-02 — AI → İnsan Devir Mekanizması (Handoff)

**Bu Neden Var?**

AI çok şey yapabilir ama her şeyi yapmamalı. Bir müşteri "ameliyattan sonra şişlik var, normal mi?" diye sorduğunda AI'nın kesin tıbbi yorum yapması tehlikeli. Bir misafir "faturamda hata var, avukatıma gidiyorum" dediğinde AI'nın standart şablonla cevap vermesi felakettir.

AI'nın "burada durup insana devretmem gerekiyor" diyebildiği bir sistem olmadan, tek bir hatalı cevap tüm güveni yıkar.

**Kime Lazım?**

AI destekli cevap sistemi kullanan her işletme — yani Invekto'nun tüm müşterileri.

**Çözdüğü Sorun:**

- Tıbbi risk: Yanlış sağlık bilgisi = malpractice
- Finansal risk: Yanlış fiyat = yasal sorun
- Müşteri kaybı: AI döngüsünde kalan müşteri terk eder
- Güven kaybı: Tek bir hatalı AI cevabı = "bu sistem güvenilmez" algısı

**Nasıl Çalışıyor?**

1. **Güven skoru:** AI her cevap ürettiğinde bir güven skoru hesaplar (0-100)
2. **Eşik altı devir:** Güven skoru belirlenen eşiğin altındaysa (örneğin %60) otomatik olarak insan temsilciye aktarılır
3. **Hassas konular:** Tıbbi tavsiye, hukuki konu, fiyat kesinleştirme — bu konularda AI hiçbir zaman kesin cevap vermez
4. **Müşteri talebi:** "İnsanla konuşmak istiyorum" diyen müşteri anında aktarılır
5. **Döngü tespiti:** Aynı konuda 3+ mesaj alış verişi varsa AI çözemiyordur → insan devralır
6. **Bağlam aktarımı:** Devir sırasında AI'nın topladığı tüm bilgi (konu, duygu durumu, müşteri profili, konuşma özeti) insana aktarılır
7. **Geçiş mesajı:** "Sizi konuyla ilgili uzmanımıza yönlendiriyorum. Biraz önce paylaştığınız bilgiler iletildi."
8. **Geri bildirim döngüsü:** İnsan çözdükten sonra AI bu çözümü öğrenir

**Psikolojik Etki:**
Müşteri hiçbir zaman "robotla konuşuyorum" hissetmez — ihtiyaç anında gerçek insan devreye girer. İşletme sahibi içinse "AI benim adıma hata yapmaz" güvencesi.

> 💡 **Vay Be Anı:** Estetik kliniğe bir hasta yazdı: "Rinoplasti sonrası burnumda morarma var, acil mi?" Handoff olmayan sistemde AI "morarma normaldir, endişelenmeyin" dedi — ama hastanın durumu komplikasyon başlangıcıydı. Hasta 2 gün daha bekledi, sonuç: acil revizyon ameliyatı, 35.000 TL ekstra maliyet, 1 yıldız Google yorumu. Invekto'da aynı senaryo: AI mesajı algıladı, güven skoru %42 (tıbbi acil), 4 saniyede doktora aktardı. Doktor fotoğraf istedi, hastayı sabah ilk sıraya aldı, erken müdahaleyle komplikasyon önlendi. Hasta: "Gece yazdım, 5 dakikada doktorum ilgilendi — bu klinikten asla ayrılmam."

---

### CS-03 — AI Halüsinasyon Koruması

**Bu Neden Var?**

Yapay zeka bazen çok ikna edici ama tamamen yanlış cevaplar üretebilir. Buna "halüsinasyon" deniyor. Bir AI'nın "implant 25.000 TL" demesi ama doktorun 45.000 TL fiyat vermesi güven yıkımıdır. "Hamileyken botox yapılabilir" demesi ise sağlık riskidir. "İadeniz onaylandı" demesi ama iade koşulları karşılanmamışsa operasyonel kaostur.

**Kime Lazım?**

Tüm sektörler — ama sağlık en kritik.

**Çözdüğü Sorun:**

- Sağlık riski: Yanlış tıbbi bilgi → fiziksel zarar
- Finansal risk: Yanlış fiyat veya iade onayı → kayıp
- Güven kaybı: Tutarsız bilgi → "bu sistem güvenilmez"
- Hukuki risk: Yanlış taahhüt → dava

**Nasıl Çalışıyor?**

1. **"Bilmiyorum" kapasitesi:** AI emin olmadığı konuda "Bu konuda kesin bilgi veremiyorum, sizi uzmanımıza yönlendiriyorum" der
2. **Konu bazlı koruma listesi:**
   - Tıbbi tavsiye → ASLA kesin tanı koymaz
   - Fiyat → Aralık verir, "kesin fiyat muayenede belirlenir" ekler
   - İlaç/dozaj → ASLA öneri yapmaz, doktora yönlendirir
   - Hukuki (iade hakkı, garanti) → Bilgi tabanından kaynak gösterir, yorum eklemez
3. **Düşük güven → insan:** Güven skoru düşükse CS-02 devreye girer
4. **Kayıt:** AI'nın verdiği her cevabın güven skoruyla birlikte kaydı tutulur

**Psikolojik Etki:**
Müşteri yanlış bilgiyle karşılaşmaz → AI'ya güveni artar. İşletme sahibi "AI benim adıma yanlış bir şey söylemez" diye rahat eder.

> 💡 **Vay Be Anı:** E-ticaret mağazası: Müşteri "bu ürün su geçirmez mi?" diye sordu. AI, bilgi tabanında kesin veri bulamadı — koruma sistemi olmayan rakip platformda AI "evet, su geçirmez" dedi (halüsinasyon). Müşteri ürünü suya soktu, bozuldu, 2.200 TL'lik iade + 1 yıldız yorum + 3 potansiyel müşteri o yorumu görüp vazgeçti. Invekto'da aynı mesaj geldiğinde AI güven skoru %38 verdi, cevap: "Bu konuda kesin bilgi veremiyorum, sizi ürün uzmanımıza yönlendiriyorum." Agent 2 dakikada teknik sayfayı kontrol etti, doğru cevabı verdi. Satış kapandı, müşteri memnun. Tek bir halüsinasyonun önlenmesi = 2.200 TL iade + 3 kaçan müşteri (tahmini 4.500 TL) = 6.700 TL kurtarıldı.

---

## Operasyonel Güvenilirlik

### CS-04 — SLA Watchdog ve Failover

**Bu Neden Var?**

Mesajlara geç cevap vermek, hiç cevap vermemekle aynı şeydir. Araştırmalar gösteriyor: 5 dakikadan fazla bekleyen müşterinin dönüşüm oranı %80 düşüyor. Ama bir agent hastalandığında, AI servisi çöktüğünde veya yoğun saatlerde mesajlar biriktiğinde ne olacak? Hiçbir şey — ta ki watchdog mekanizması devreye girene kadar.

**Kime Lazım?**

Mesaj hacmi günde 30+'yı geçen her işletme.

**Çözdüğü Sorun:**

- Cevapsız mesaj: Agent offline, mesajlar birikiyor, kimse farkında değil
- AI çökmesi: Servis down, müşteriye hiçbir cevap gitmiyor
- VIP kaybı: Önemli lead 1 saat bekledi, rakibe gitti
- SLA ihlali: Taahhüt edilen cevap süresi aşıldı, müşteri memnuniyetsiz

**Nasıl Çalışıyor?**

1. **SLA kuralları** (işletme bazında ayarlanabilir):
   - Genel mesaj: 5 dakika içinde ilk yanıt
   - VIP müşteri: 2 dakika içinde
   - Acil (sağlık): 1 dakika içinde
2. **Erken uyarı:** SLA süresine 1 dakika kala agent'e bildirim
3. **Süre aşımı:** SLA doldu → yöneticiye otomatik bildirim
4. **Kritik aşım:** SLA 2 katı aşıldı → müsait herhangi bir agent'e veya AI yedek mesajına yönlendirme
5. **AI yedek:** AI servisi çöktüyse → "Şu an yoğunuz, en kısa sürede döneceğiz" otomatik mesajı
6. **Tekrar sorunu:** AI üst üste 3 kez düşük güvenle cevap verdiyse → direkt insana yönlendirme
7. **Dashboard:** SLA ihlal sayısı, ortalama bekleme, toparlanma süreleri

**Maliyet Etkisi:**
Her SLA breach = potansiyel müşteri kaybı. E-ticarette kaçan müşteri = ortalama 500 TL, klinikte kaçan hasta = 5.000-50.000 TL, otelde kaçan rezervasyon = 1.500+ TL/gece.

> 💡 **Vay Be Anı:** Perşembe 14:23. VIP lead yazdı: "50 kişilik kurumsal sipariş için fiyat?" Agent öğle molasında. Eskiden bu mesaj 2 saat cevapsız kalırdı — müşteri çoktan rakibi aramıştı. Şimdi 4. dakikada SLA alarmı çaldı, supervisor mesajı gördü, 47 saniyede cevap verdi. Sonuç: 175.000 TL'lik kurumsal sipariş. Ay sonunda dashboard'a bakıldı: "Bu ay 0 SLA breach. Invekto öncesi: ayda 47 breach." İşletme sahibi eski hayatı hatırladı — cevapsız mesajlar, kaçan müşteriler — ve "bir daha o günlere dönmem" dedi.

---

### CS-05 — Kayıp Müşteri Sinyali Tespiti (Churn Prevention)

**Bu Neden Var?**

Müşteriler genellikle "ben gidiyorum" demez. Sessizce uzaklaşır. Ama sessizliğin öncesinde sinyaller vardır: "neyse boş ver", "başka yere de bakıyorum", "düşüneyim" deyip bir daha yazmamak, ya da düzenli gelen müşterinin aniden gelmemesi.

Bu sinyalleri yakalayıp proaktif müdahale etmek, kayıp müşteriyi kurtarmanın en etkili yoludur. Çünkü yeni müşteri kazanmak, mevcut müşteriyi tutmaktan 5-7 kat daha pahalıdır.

**Kime Lazım?**

Tekrar eden müşteri ilişkisi olan her işletme — klinikler, kurslar, salonlar, e-ticaret.

**Nasıl Çalışıyor?**

1. **Sinyal tespiti:** AI mesajlardaki kayıp sinyallerini algılar:
   - Pasif agresif: "neyse", "boş ver", "bir daha uğraşmam"
   - Karşılaştırma: "rakip X daha ucuz", "başka yere bakıyorum"
   - Soğuma: Aktif konuşmada 3+ gün sessizlik
   - Frekans düşüşü: Düzenli müşteri uzun süredir sipariş/randevu almıyor
2. **Risk skoru:** Düşük / Orta / Yüksek / Kritik
3. **Otomatik aksiyon:**
   - Orta risk: Agent'e "dikkat: kayıp riski" uyarısı
   - Yüksek risk: Yöneticiye bildirim + önerilen kurtarma aksiyonu
   - Kritik: Outbound kurtarma mesajı (özel teklif, VIP ilgi)
4. **Dashboard:** Churn risk pipeline — kurtarılan vs kaybedilen

**Psikolojik Etki:**
Müşteri "beni önemsiyorlar" hisseder — proaktif ilgi, bağlılığı artırır. İşletme sahibi kayıpları görmezden gelmez, verilere dayalı müdahale eder.

> 💡 **Vay Be Anı:** Güzellik salonu sahibi Ayşe Hanım'ın en sadık müşterisi Zeynep, 4 yıldır her ay geliyordu — ama son 45 gündür sessiz. Eski sistemde 3 ay sonra fark edilirdi, o zamana kadar Zeynep çoktan rakip salona geçmişti. Invekto'da 30. günde churn radarı alarm verdi: "Zeynep B. — Kritik risk. 4 yıllık müşteri, son 30 gün sessiz. Yıllık değeri: 14.400 TL." Ayşe Hanım hemen kişisel bir mesaj gönderdi: "Sizi özledik Zeynep Hanım, size özel %20 indirimli bakım paketi hazırladık." Zeynep döndü. Invekto olmasa o 14.400 TL sessizce kaybolacaktı — ve Ayşe Hanım bunun farkına bile varmayacaktı.

---

### CS-06 — Birleşik Müşteri Zaman Çizelgesi

**Bu Neden Var?**

Aynı müşteri WhatsApp'tan yazdı, Instagram'dan DM attı, telefon etti, web'den sipariş verdi — ama bunlar hep ayrı kayıtlar. Agent müşteriyi aradığında "daha önce de yazmıştım" der, agent "ne için yazmıştınız?" diye sorar. Müşteri haklı olarak "her defasında baştan mı anlatacağım?" diye düşünür.

**Kime Lazım?**

Birden fazla kanaldan müşteri iletişimi alan her işletme — yani Invekto'nun tüm müşterileri.

**Çözdüğü Sorun:**

- Kopuk deneyim: Müşteri her kanalda baştan başlıyor
- Yüzeysel AI: AI sadece son mesaja bakıyor, geçmiş bağlamı yok
- VIP körlüğü: Müşterinin toplam etkileşimi görülmüyor, VIP tespiti yapılamıyor
- Takip körlüğü: Hasta 3 hafta önce IG'den yazdı, şimdi WA'dan yazıyor, bağlantı kurulamıyor

**Nasıl Çalışıyor?**

1. **Kimlik eşleştirme:** Telefon + e-posta + Instagram handle + WhatsApp numarası ile aynı kişinin tüm kanalları birleştirilir
2. **Kronolojik görünüm:** Tüm etkileşimler tarih sırasında, kanal ikonu ile
3. **Her kayıt:** Kanal, tarih, konu, çözüm durumu, ilgilenen agent
4. **AI bağlamı:** Son 10 etkileşimin özeti AI'ya verilir → daha akıllı cevap önerisi
5. **CRM entegrasyonu:** Sipariş geçmişi, randevu geçmişi, yorum geçmişi — hepsi tek ekranda

**Psikolojik Etki:**
Müşteri "beni tanıyorlar" hisseder. Agent "müşteriyi anlıyorum" güvenini taşır. Yönetici müşterinin gerçek değerini görür.

> 💡 **Vay Be Anı:** Diş kliniği: Hasta Mehmet Bey WhatsApp'tan yazdı: "Geçen hafta Instagram'dan sormuştum, fiyat bilgisi almıştım." Eski sistemde agent "hangi tedavi için sormuştunuz?" diye sordu. Mehmet Bey sinirli: "Her seferinde baştan mı anlatacağım? Boşverin, başka kliniğe gidiyorum." Kaybedilen tedavi: 28.000 TL'lik implant paketi. Invekto'da aynı senaryo: Mehmet Bey yazdığı an, agent ekranında zaman çizelgesi açıldı — 5 gün önce Instagram DM'den implant fiyatı sormuş, 3 gün önce web sitesinden randevu sayfasını ziyaret etmiş. Agent: "Mehmet Bey, Instagram'dan sorduğunuz implant tedavisi için randevu ayarlayalım mı?" Mehmet Bey: "Vay, hatırlıyorsunuz! Evet, uygun bir güne yazın." 28.000 TL kurtarıldı — tek bir soru sormadan.

---

### CS-07 — Gelir İlişkilendirme (Revenue Attribution)

**Bu Neden Var?**

"Invekto sayesinde ne kadar para kazandık?" sorusuna cevap veremiyorsanız, Invekto'nun değerini kanıtlayamazsınız. Hangi kanal en çok satış getirdi? AI mı yoksa insan mı kapattı bu satışı? Hangi kampanya şablonu daha iyi dönüş sağladı? Bu soruların cevabı, hem işletme sahibinin Invekto'ya devam kararı hem de Invekto'nun enterprise satışı için kritik.

**Kime Lazım?**

ROI hesabı yapmak isteyen her işletme. Enterprise satışta ise zorunlu.

**Çözdüğü Sorun:**

- ROI kanıtı yok: "300K TL kazandık" ama ispat yok
- Kanal körlüğü: Hangi kanaldan ne kadar gelir geldiği belirsiz
- Kampanya optimizasyonu: Hangi outbound template daha iyi dönüyor bilinmiyor
- Enterprise engel: Büyük müşteri "AI ROI'niz nedir?" diye soruyor, cevap yok

**Nasıl Çalışıyor?**

1. **Kaynak takibi:** Her satışın ilk temas kanalı kaydedilir (WhatsApp organik, Instagram reklam, Google, referans)
2. **AI vs İnsan:** Cevabı AI mı önerdi, insan mı yazdı, birlikte mi çalıştılar — kayıt altında
3. **Değer eşleştirme:** Randevu → tedavi tutarı, sipariş → sepet tutarı
4. **Satış hunisi:** Lead → İlk yanıt → Nitelikli → Randevu/Satın alma → Kapandı
5. **Dashboard:** Kanal bazlı ROI, agent bazlı kapanış oranı, AI katkı oranı

**Maliyet Etkisi:**
Enterprise müşteriye "AI ROI: %340" diyebildiğinizde, fiyat tartışması biter. Kendi ekibiniz için ise hangi senaryoya yatırım yapacağınıza veri ile karar verirsiniz.

> 💡 **Vay Be Anı:** Eğitim kurumu sahibi muhasebecisiyle oturdu. "Invekto'ya ayda 4.500 TL ödüyoruz, buna değiyor mu?" Eski sistemde cevap: "Herhalde ediyor, mesajlar hızlı gidiyor." Ikna edici değil. Invekto'da revenue dashboard'u açıldı: "Bu ay Invekto kanalından gelen kayıtlar: 23 yeni öğrenci. Toplam gelir: 69.000 TL. AI'nın otomatik cevapladığı mesajlardan kapanan satış: 14 (42.000 TL). Kurtarılan kayıp müşteri: 4 (12.000 TL). Net ROI: %1.433." Muhasebeci: "Bunu Excel'e aktarabilir miyiz?" Artık Invekto masrafı tartışılmıyor — yatırım olarak görülüyor.

---

## Mobil Erişim Senaryoları

> **Not:** Bu senaryolar Phase 7'de planlanan mobil uygulama içindir. Sektör bağımsız — tüm Invekto müşterilerine fayda sağlar.

### M1 — Sahada Mesaj Yönetimi

**Bu Neden Var?**

E-ticaret satıcısı depoda, diş doktoru öğle yemeğinde, estetik koordinatör sahada, otel müdürü toplantıda — hepsi masabaşından uzakta ama VIP lead veya acil mesaj beklemiyor ki gelmeyecek. Mobil tarayıcıda web uygulaması kullanışsız. Mesajlar birikir, fırsatlar kaçar.

**Nasıl Çalışıyor?**

Push notification gelir → konuşma listesini açarsın → AI cevap önerisi hazırdır → 1 dokunuşla gönderirsin. Masabaşına dönmeye gerek yok.

> 💡 **Vay Be Anı:** Klinik koordinatörü Selin, diş fuarında dolaşıyor. Cebine bildirim düştü: "Yeni lead — 6'lı veneer fiyat sorgusu." Eskiden bu mesaj 3 saat bekliyordu, Selin masasına dönene kadar lead çoktan 4 klinikten teklif almıştı. Şimdi Selin, fuarda yürürken telefonunu çıkardı, AI'nın hazırladığı cevabı gördü — fiyat aralığı, tedavi süresi, randevu linki hazır. Tek dokunuşla gönderdi. 22 saniye. Lead randevu aldı. Tedavi değeri: 54.000 TL. Selin, espresso alırken 54.000 TL'lik satış kapattı.

---

### M2 — Acil Mesaj Bildirimi

SLA süresi dolmak üzere veya VIP müşteri yazmış — ama sen bilgisayar başında değilsin. Mobil push notification ile anında haberdar olursun, müdahale edersin.

> 💡 **Vay Be Anı:** Cumartesi öğlen, e-ticaret mağaza sahibi Burak parkta oğluyla oynuyor. Cebine push bildirimi geldi: "SLA uyarısı — VIP müşteri 3 dakikadır bekliyor. Konu: 85.000 TL'lik toplu sipariş." Eskiden bu mesajı Pazartesi görecekti. Müşteri çoktan rakipten almış olacaktı. Burak telefonu açtı, AI'nın hazırladığı teklifi inceledi, "gönder" dedi. 45 saniye. Pazartesi ofise geldiğinde sipariş onaylanmıştı. "Oğlumla oynuyordum, aynı anda 85.000 TL'lik sipariş aldım" — bu cümle Invekto'nun pazarlama bültenine haber oldu.

---

### M3 — Sahada Ekip İzleme

Yönetici dışarıda ama ekibinin ne durumda olduğunu görmek istiyor. Bekleyen mesaj sayısı, ortalama yanıt süresi, aktif agent sayısı — basit bir mobil dashboard ile hepsi avucunun içinde.

> 💡 **Vay Be Anı:** Otel müdürü Cenk, Antalya'daki turizm fuarında. Aklı otelde: "Acaba bu hafta sonu yoğunluğunda ekip mesajları yetiştirebiliyor mu?" Eskiden ya sürekli arayıp soruyordu ya da otele dönene kadar bilemiyordu. Şimdi telefonunu çıkardı: 3 aktif agent, bekleyen mesaj 2, ortalama yanıt süresi 42 saniye. Yeşil gösterge. Rahatlayıp fuara döndü. Bir baktığında: bekleyen mesaj 14'e çıkmış, 1 agent offline. Hemen reserve agent'i aktif etti — 3 dokunuş. Cenk otele dönmeden krizi çözdü. "Artık otelden uzaklaşmak demek, kontrolü kaybetmek demek değil."

---

### M4 — Mesai Dışı Acil Triage (Sağlık)

Hasta gece "şişlik var, normal mi?" yazdı. Doktor evde. AI ön cevap hazırlar, sadece "acil" etiketli mesajlar doktora push notification gönderir. Doktor mobil uygulamadan resmi Invekto kanalıyla cevap verir — kayıt altında, KVKK uyumlu.

> 💡 **Vay Be Anı:** Gece 01:45. Doktor Elif evde uyuyor. Telefonuna Invekto bildirimi geldi: "Acil etiketli mesaj — Ameliyat sonrası hasta." Hasta diyor ki: "Yüzümde şişlik arttı, ateşim de yükseldi." AI ön cevabı hazır: "Rahatlama mesajı + soğuk kompres talimatı + sabah erken randevu önerisi." Ama ateş detayı yüzünden acil flag kalkmış. Doktor Elif, yatağından kalkıp bilgisayar açmadı — telefonundan hastanın mesaj geçmişini gördü, fotoğraf istedi, 3 dakikada durumu değerlendirdi: acil değil ama sabah kontrol şart. Cevabını gönderdi, tekrar yattı. Hasta rahatladı: "Gece 2'de doktorum cevap verdi — hayatımda böyle bir klinik görmemiştim." Bu hissin parasal karşılığı hesaplanamaz, ama o hasta ömür boyu o kliniğin.

---

### M5 — Hareket Halinde Sipariş Yönetimi (E-ticaret)

Satıcı depoda, fuarda veya toplantıda. "Kargom nerede?" mesajları birikiyor ama dizüstü bilgisayar açamıyor. Mobil uygulama ile konuşma listesi, AI cevap önerisi, sipariş kartı — hepsi cepte.

> 💡 **Vay Be Anı:** E-ticaret satıcısı Deniz, depoda stok sayımı yapıyor. Son 1 saatte 7 "kargom nerede?" mesajı birikmiş. Eskiden bunları masasına dönünce tek tek cevaplıyordu — ortalama 2 saat gecikme, her gecikmede müşteri memnuniyetsizliği. Şimdi Deniz depoda yürürken telefonundan 7 mesajı gördü. AI her birine kargo takip bilgisiyle birlikte cevap hazırlamış. 7 mesaj, 7 dokunuş, 90 saniye. Tek bir müşteri bile beklemedi. "Depodan çıkmadan bütün destek mesajlarını bitirdim — bu eskiden imkansızdı."

---

### M6 — QR Kod ile WhatsApp Başlatma

Otel odasında, restoran masasında, klinik bekleme salonunda, mağazada — QR kod taranıyor, direkt WhatsApp konuşması başlıyor. Fiziksel dünyadan dijital iletişime köprü. QR tarama anında opt-in toplama fırsatı da yaratılır.

**Kullanım Alanları:**
- Otel: Oda kapısı QR → housekeeping/oda servisi talebi
- Klinik: Bekleme salonu QR → randevu bilgi/form doldurma
- E-ticaret: Ürün kutusu QR → destek/iade başlatma
- Salon: Ayna QR → randevu/ürün sipariş

> 💡 **Vay Be Anı:** Butik otel: Misafir sabah 07:15'te uyandı, havlu lazım ama resepsiyonu aramak istemiyor — daha kimseyle konuşacak halde değil. Komodinin üstündeki QR kodu telefon kamerasıyla taradı, WhatsApp açıldı, "Ekstra havlu" butonuna bastı. 8 dakikada kapısında havlu vardı. Misafir check-out anketinde yazdı: "Kimseyle konuşmadan, 3 dokunuşla havlu geldi. Bu oteli herkese tavsiye edeceğim." Otelin Google yorumlarında bu özellik 14 kez bahsedildi. 14 organik yorum = binlerce TL'lik reklam değeri — sadece bir QR kodla.

---

### M7 — Çevrimdışı Mod

Saha çalışanı depoda, doktor ameliyatta, satıcı uçuşta — internet yok. Önceki mesajları çevrimdışı okuyabilir, taslak hazırlayabilir, internet geldiğinde otomatik gönderim yapılır.

> 💡 **Vay Be Anı:** Estetik klinik koordinatörü Gamze, Istanbul-Izmir uçuşunda. 1 saatlik uçuş boyunca internet yok. Ama Gamze telefonundan dünkü mesaj geçmişini okudu, 5 hastanın tedavi planı notlarını inceledi, 3 tanesine cevap taslağı hazırladı. Uçak indi, internet bağlandı, 3 mesaj otomatik gönderildi. Gamze daha havalimanından çıkmadan 3 hasta cevabını almıştı. "Uçakta bile çalışabildim ama zorla değil — 10 dakikada işimi hallettim, geri kalan uçuşta kitabımı okudum." İşte olmasan da işin başındasın — ama 7/24 çalışmak zorunda değilsin.

---

## Gelir Senaryoları (S1-S12) — Tüm Sektörlerde Ortak Gelir Mekanizmaları

> **Not:** Her gelir senaryosu birden fazla sektörde uygulanır. Sektörel varyasyonlar ilgili sektör dokümanlarında detaylandırılmıştır. Burada evrensel mekanizmalar özetlenmiştir.

### S1 — AI Cevap Önerisi ile Hız ve Kalite

**Mekanik:** AI gelen mesajı analiz eder → agent'e hazır cevap önerisi sunar → agent onaylar veya düzenler → gönderir.

**Neden Değerli?** Cevap süresi 3 dakikadan 30 saniyeye düşer. E-ticarette ilk 5 dakikada cevap veren satıcının dönüşüm oranı %391 daha yüksek.

**Sektör Uygulamaları:**
- E-ticaret: Ürün sorusu, stok bilgisi, kargo takibi
- Diş: Tedavi bilgisi, fiyat aralığı, randevu önerisi
- Estetik: Prosedür bilgisi, iyileşme süreci
- Otel: Fiyat, müsaitlik, check-in bilgisi

**Tahmini Etki:** ~144.000 TL/ay (50 müşteri bazında)

> 💡 **Vay Be Anı:** Diş kliniği: Akşam 18:47, mesai bitmek üzere. Son anda bir lead yazdı: "Zirkonyum kaplama fiyatları nedir?" Agent Melis masasını toplamak üzere. Eskiden bu mesaj sabaha kalacaktı — hasta gece boyunca 5 kliniğe daha yazacak, sabah ilk cevap verene gidecekti. Şimdi AI 3 saniyede cevap önerisi hazırladı: fiyat aralığı, tedavi süresi, randevu linki. Melis bir göz attı, "gönder" dedi. 12 saniye. Hasta 19:05'te randevu aldı. Tedavi değeri: 32.000 TL. Melis'in o gün kapattığı son satış, ayın en büyük satışı oldu. 12 saniye = 32.000 TL.

---

### S2 — Akıllı Yönlendirme (Routing)

**Mekanik:** Gelen mesajın konusu, dili, aciliyeti ve müşteri segmenti analiz edilir → doğru departmana veya agent'e otomatik yönlendirilir.

**Neden Değerli?** Yanlış kişiye giden mesaj = zaman kaybı + müşteri memnuniyetsizliği. Doğru yönlendirme, ilk temasta çözüm oranını artırır.

**Tahmini Etki:** ~31.500 TL/ay

> 💡 **Vay Be Anı:** Estetik klinik: Arapça mesaj geldi — "أريد عملية تجميل الأنف" (rinoplasti istiyorum). Eski sistemde mesaj genel havuza düştü, 3 agent baktı "ben Arapça bilmiyorum" dedi, mesaj 45 dakika bekledi. Lead çoktan Dubai'deki başka bir kliniğe yazmıştı. Invekto'da mesaj geldiği an: dil algılandı (Arapça), konu algılandı (rinoplasti), Arapça bilen koordinatör Fatma'ya otomatik yönlendirildi. Fatma 90 saniyede cevap verdi. Tedavi paketi: 8.500 Euro (otel + transfer dahil). O 45 dakikalık gecikme ile 90 saniyelik cevap arasındaki fark = 8.500 Euro. Bu ay böyle 4 lead daha geldi. Routing tek başına bu klinik için aylık 34.000 Euro değer üretti.

---

### S3 — İade Dönüştürme

**Mekanik:** Müşteri iade istediğinde AI alternatif sunar: değişim, indirim kuponu, mağaza kredisi. Amacı iadeyi önlemek değil, müşteriye daha iyi bir seçenek sunarak satışı kurtarmak.

**Sektör Uygulamaları:**
- E-ticaret: "Beden küçük geldi" → beden değişimi + ücretsiz kargo
- Eğitim: "Kursu bırakmak istiyorum" → grup/gün değişikliği teklifi
- Güzellik: "Saç rengim istediğim gibi olmadı" → düzeltme randevusu

**Tahmini Etki:** ~18.000 TL/ay

> 💡 **Vay Be Anı:** E-ticaret mağazası: Müşteri yazdı "beden küçük geldi, iade istiyorum." Eski sistemde agent standart iade prosedürüne girer, 750 TL'lik satış geri döner, kargo masrafı mağazaya kalır. Invekto'da AI müşterinin mesajını algıladı, agent'e 3 alternatif sundu. Agent: "Bir üst beden göndermemizi ister misiniz? Kargo bizden, 24 saatte kapınızda. Ya da 825 TL mağaza kredisi (750 TL + %10 bonus) ile dilediğiniz ürünü seçebilirsiniz." Müşteri mağaza kredisini seçti — 825 TL ile 950 TL'lik bir ürün aldı, aradaki farkı ödedi. Sonuç: iade yerine 200 TL ek satış. Bu ay 23 iade talebinin 9'u dönüştürüldü = 6.750 TL kurtarılan gelir + 1.800 TL ek satış.

---

### S4 — Proaktif Satış

**Mekanik:** Satın alma veya hizmet sonrası, müşteriye ilgili ürün/hizmet önerisi gönderilir.

**Örnekler:**
- Diş: Beyazlatma yapıldı → 6 ay sonra kontrol hatırlatma
- E-ticaret: Ayakkabı aldı → bakım spreyi önerisi
- Otel: Check-in sonrası → spa paketi teklifi

**Tahmini Etki:** ~22.500 TL/ay

> 💡 **Vay Be Anı:** Güzellik salonu: Müşteri keratin bakımı yaptırdı (850 TL). Eski sistemde: "güle güle, yine bekleriz" ve 3 ay sessizlik. Invekto'da: T+3 gün: "Keratininiz nasıl? Sülfatsız şampuan önerimiz" (cross-sell: 180 TL). T+30 gün: "Keratin bakımınızın etkisi azalmaya başlayabilir. Yenileme randevusu?" T+45 gün müşteri geldi, yenileme yaptırdı (850 TL), şampuan da aldı (180 TL). Tek bir proaktif satış dizisi: 850 + 180 = 1.030 TL ek gelir. 30 müşteride: ayda 30.900 TL. Salon sahibi: "Mesaj göndermesek bu müşteriler 5 ay sonra gelirdi — ya da hiç gelmezdi."

---

### S5 — B2B Lead Tespiti

**Mekanik:** AI mesajda kurumsal sinyalleri algılar: "50 kişilik sipariş", "şirketimiz için", "toplu alım". Bu mesajlar VIP olarak etiketlenir ve satış ekibine yönlendirilir.

**Tahmini Etki:** ~37.500 TL/ay

> 💡 **Vay Be Anı:** E-ticaret mağazası: Normal bir müşteri sorusu gibi görünen mesaj geldi: "Bu ürünü 35 adet alabilir miyiz? Kurumsal fatura kesilir mi?" Eski sistemde agent standart cevap verdi, müşteri "tamam düşüneyim" dedi, mesaj kayboldu. Invekto'da AI "kurumsal", "35 adet", "fatura" kelimelerini algıladı, mesajı anında VIP etiketiyle satış müdürüne yönlendirdi. Satış müdürü 4 dakikada aradı: "Size özel toplu alım fiyatı ve kurumsal fatura çıkartabiliriz." Sonuç: 35 adet x 1.200 TL = 42.000 TL'lik sipariş. Agent fark etmese o mesaj "düşüneyim"de kalacaktı. AI fark etti, 42.000 TL kazanıldı.

---

### S6 — Randevu Optimizasyonu

**Mekanik:** Boş slotların otomatik tespiti, bekleyen hastalara/müşterilere bildirim, iptal durumunda waitlist aktivasyonu.

**Sektör Uygulamaları:**
- Diş/Estetik: Boş koltuk = doğrudan gelir kaybı
- Güzellik: İptal oldu → bekleyen müşteriye anında haber

**Tahmini Etki:** ~60.000 TL/ay

> 💡 **Vay Be Anı:** Diş kliniği: Salı günü 14:00 randevusu iptal oldu. Doktorun 1 saati boş — maliyet: klinik kirası, personel maaşı, elektrik, o koltuğun 0 TL üretmesi. Eskiden kimseye haber verilmezdi. Invekto'da iptal anında otomatik tetik: Son 30 günde randevu bekleyen 8 hastaya bildirim: "Bugün 14:00'te yer açıldı! Ilk yanıtlayan alır." 3 dakika sonra Ayşe Hanım: "Ben gelirim!" Randevu oluşturuldu. Tedavi: 4.500 TL'lik dolgu. O boş koltuk 4.500 TL üretti. Ayda ortalama 12 iptal oluyordu — Invekto ile 9'u dolduruldu. Aylık kurtarılan gelir: 9 x 4.500 = 40.500 TL. Boş koltuk artık boş kalmıyor.

---

### S7 — No-Show Önleme

**Mekanik:** Randevudan 1 gün önce hatırlatma → 2 saat önce son hatırlatma → onay butonu. Gelmezse no-show kaydı, tekrarlayan no-show'lara depozit politikası.

**Etki:** No-show oranı %15-20'den %5-8'e düşer.

**Tahmini Etki:** ~135.000 TL/ay

> 💡 **Vay Be Anı:** Estetik klinik: Ayda 40 randevunun 8'i no-show oluyordu (%20). Her no-show = ortalama 3.500 TL kayıp. Aylık kayıp: 8 x 3.500 = 28.000 TL. Invekto devreye girdi: T-1 gün hatırlatma, T-2 saat son hatırlatma, onay butonu. İlk ay: no-show 8'den 3'e düştü. 5 kurtarılan randevu x 3.500 TL = 17.500 TL/ay. Ama asıl vay be anı şu: Tekrarlayan no-show yapan 2 hastaya "gelecek randevunuz için 500 TL depozit istiyoruz" mesajı gitti. İkisi de geldi. "Param gidecek diye geliyorum ama aslında iyi ki geldim, erteler dururdun" dedi biri. No-show artık %5 — klinik bu parayla yeni bir cihaz aldı.

---

### S8 — Tedavi Planı Takibi

**Mekanik:** Çok seanslı tedavilerde (diş implant, estetik, fizik tedavi) her seans sonrası otomatik hatırlatma, bakım talimatı, sonraki randevu koordinasyonu.

**Tahmini Etki:** ~90.000 TL/ay

> 💡 **Vay Be Anı:** Diş kliniği: 6 seanslı implant tedavisi (toplam 45.000 TL). Hastalar genellikle 3. seansta motivasyonunu kaybediyor — "acısı geçti, devamını erteliyeyim" diyor. Eski sistemde 4 hastadan 1'i tedaviyi yarıda bırakıyordu. 45.000 TL'nin yarısı havada kalıyordu. Invekto'da her seans sonrası otomatik ilerleme mesajı: "3. seansınız tamamlandı! %50 ilerleme. Yeni gülüşünüze 3 seans kaldı. Sonraki randevu: 15 Mart." Hasta somut ilerlemeyi gördü, "yarısını geçtim, bırakmam mantıksız" dedi. Tedavi terk oranı %25'ten %6'ya düştü. 10 hastalık farkla: 10 x 22.500 TL (kalan seanslar) = 225.000 TL/çeyrek kurtarılan gelir.

---

### S9 — Proaktif Follow-up Zincirleri

**Mekanik:** Hizmet veya satış sonrası otomatik takip dizisi: memnuniyet kontrolü → bakım hatırlatma → yeni hizmet önerisi → yorum rica.

**Tahmini Etki:** ~300.000+ TL/ay (en yüksek potansiyel)

> 💡 **Vay Be Anı:** Otel: Misafir check-out yaptı. Memnundu ama kimse sormadı. Eskiden 6 ay sonra aynı şehre geldiğinde başka otel seçerdi. Invekto'da: T+4 saat: "Konaklamanız nasıldı?" (5 yıldız). T+4 saat: "Google'da paylaşır mısınız? Gelecek konaklamada %10 indirim kodu: WELCOME10." T+3 ay (yaz sezonu yaklaşırken): "Sizi özledik! Yaz erken kayıt %20 indirimli. Geçen yılki odanız müsait." Sonuç: Misafir Google'a 5 yıldız yazdı (organik reklam değeri), 3 ay sonra tekrar rezervasyon yaptı (1 gece 1.800 TL x 3 gece = 5.400 TL). Tek bir follow-up zinciri: 5 yıldız yorum + 5.400 TL tekrar satış. 50 misafirde aylık: 270.000 TL potansiyel.

---

### S10 — Google Yorum + Referans Yönetimi

**Mekanik:** Hizmet sonrası memnuniyet anketi → yüksek puan verenler Google yorum sayfasına yönlendirilir, düşük puan verenler iç eskalasyona.

**Etki:** Google puanı her 0.1 artış = %5-9 müşteri artışı.

**Tahmini Etki:** ~105.000 TL/ay

> 💡 **Vay Be Anı:** Diş kliniği: Google puanı 4.1'di. "İyi ama harika değil." Hastaların %80'i memnun ayrılıyordu ama kimse yorum yazmıyordu — sadece kızgın olanlar yazıyordu. Invekto devreye girdi: Tedavi sonrası otomatik anket → 5 puan veren 47 hastaya Google yorum linki gönderildi → 19'u yorum yazdı. 2 puan veren 3 hastaya iç eskalasyon → klinik müdürü arayıp sorunu çözdü, 2'si puanını 4'e yükseltti. 2 ayda Google puanı 4.1'den 4.6'ya çıktı. Araştırmalara göre 0.5 puanlık artış = %25-45 yeni hasta artışı. Klinik bu çeyrekte aylık 12 yeni hasta daha aldı. 12 x ortalama tedavi 8.000 TL = aylık 96.000 TL ek gelir. Sadece yorum yönetimiyle.

---

### S11 — Abonelik/Üyelik Dönüşümü

**Mekanik:** Düzenli gelen müşteri tespiti → üyelik/paket teklifi. "Her ay geliyorsunuz, aylık paket %30 tasarruf sağlar!"

**Tahmini Etki:** ~75.000 TL/ay

> 💡 **Vay Be Anı:** Güzellik salonu: Müşteri Elif 8 aydır her ay geliyor — boyama (600 TL) + bakım (350 TL) = ayda 950 TL. Ama her ay ayrı ayrı randevu alıyor, bazen 6 hafta bekliyor, bazen erteliyor. Invekto frekans analizi yaptı: "Elif H. — 8 ay üst üste, toplam 7.600 TL harcama. Üyelik teklifi öner." Agent mesaj gönderdi: "Elif Hanım, her ay geliyorsunuz — size özel aylık paket: boyama + bakım 750 TL (ayda 200 TL tasarruf!). Randevunuz otomatik, siz sadece gelin." Elif kabul etti. Salon için: 12 aylık garanti gelir = 9.000 TL. Elif için: yıllık 2.400 TL tasarruf. Herkes kazandı — ve Elif artık asla başka salona gitmez çünkü "paketim var."

---

### S12 — Kayıp Müşteri Kurtarma (Churn Prevention)

**Mekanik:** Uzun süredir gelmeyen müşteri tespiti → kademeli kurtarma: nazik hatırlatma → özel teklif → son şans kampanyası.

**Eğitim varyasyonu:** 3+ ders devamsızlık → öğrenciye/veliye kademeli iletişim.

**Tahmini Etki:** ~120.000 TL/ay

> 💡 **Vay Be Anı:** Eğitim kurumu: 180 aktif öğrenci. Dönem başında kayıt yenileme zamanı geldi. Eski sistemde toplu mesaj gönderilirdi, %60 yenilerdi, %40 sessizce giderdi. 72 öğrenci x 3.000 TL = 216.000 TL kayıp. Invekto'da churn sinyalleri 2 ay önceden algılandı: devamsızlık artan 15 öğrenci, "düşüneyim" diyen 8 veli, fiyat karşılaştıran 5 veli. Kademeli kurtarma başladı: 15 devamsıza "seni özledik + ücretsiz telafi" → 11'i döndü. 8 kararsıza "erken kayıt %15 indirim" → 6'sı yeniledi. 5 fiyat karşılaştırana "sadakat bonusu: 1 ay ücretsiz" → 3'ü kaldı. Toplam kurtarılan: 20 öğrenci x 3.000 TL = 60.000 TL. Bu dönemlik. Her dönem tekrarlıyor. Yıllık: 240.000 TL kurtarılan gelir — sadece doğru zamanda doğru mesaj göndererek.

---

## Özet Tablo — Evrensel Senaryolar

### Altyapı Senaryoları (CS)

| # | Senaryo | Grup | Etki | Phase | Kritiklik |
|---|---------|------|------|-------|-----------|
| CS-01 | Opt-in toplama ve onam yönetimi | Uyum | YÜKSEK | 1 | BLOCKER |
| CS-02 | AI → İnsan handoff | AI Güvenlik | YÜKSEK | 1 | BLOCKER |
| CS-03 | AI halüsinasyon koruması | AI Güvenlik | YÜKSEK | 1 | YÜKSEK |
| CS-04 | SLA watchdog / failover | Operasyon | YÜKSEK | 1-2 | YÜKSEK |
| CS-05 | Churn sinyali tespiti | Retention | ORTA-YÜKSEK | 3 | ORTA |
| CS-06 | Birleşik müşteri zaman çizelgesi | CRM | YÜKSEK | 2-3 | YÜKSEK |
| CS-07 | Revenue attribution | Analitik | YÜKSEK | 2-3 | YÜKSEK |
| CS-08 | KVKK/GDPR compliance | Uyum | YÜKSEK | 1-4 | BLOCKER |

### Mobil Senaryolar (M)

| # | Senaryo | Sektör | Etki | Phase |
|---|---------|--------|------|-------|
| M1 | Sahada mesaj yönetimi | Tümü | YÜKSEK | 7 |
| M2 | Acil mesaj push notification | Tümü | YÜKSEK | 7 |
| M3 | Sahada ekip izleme | Tümü | ORTA | 7 |
| M4 | Mesai dışı acil triage | Sağlık | YÜKSEK | 7 |
| M5 | Hareket halinde sipariş | E-ticaret | ORTA | 7 |
| M6 | QR kod ile WA başlatma | Tümü | ORTA | 7 |
| M7 | Çevrimdışı mod | Tümü | ORTA | 7 |

### Gelir Senaryoları (S)

| # | Senaryo | Tahmini Etki (TL/ay) | Sektörler |
|---|---------|---------------------|-----------|
| S1 | AI cevap önerisi | ~144.000 | Tümü |
| S2 | Akıllı yönlendirme | ~31.500 | Tümü |
| S3 | İade dönüştürme | ~18.000 | E-ticaret, Eğitim, Güzellik |
| S4 | Proaktif satış | ~22.500 | Tümü |
| S5 | B2B lead tespiti | ~37.500 | Tümü |
| S6 | Randevu optimizasyonu | ~60.000 | Sağlık, Güzellik |
| S7 | No-show önleme | ~135.000 | Sağlık, Güzellik, Eğitim |
| S8 | Tedavi planı takibi | ~90.000 | Sağlık |
| S9 | Proaktif follow-up | ~300.000+ | Tümü |
| S10 | Google yorum + referans | ~105.000 | Tümü |
| S11 | Abonelik dönüşümü | ~75.000 | Güzellik, Eğitim, Sağlık |
| S12 | Kayıp müşteri kurtarma | ~120.000 | Tümü |
| | **TOPLAM** | **~1.138.000+** | |
