# Senaryo Portfoyü — Review Aksiyon Plani

> **Tarih:** 2026-02-16
> **Kaynak:** 5 bagimsiz AI review raporu konsolide edildi + 1 dogrulama review'i
> **Hedef Dosya:** `ideas/roadmap-scenarios.md` (4600+ satir, 100 senaryo)
> **Durum:** A+B+C+D TAMAMLANDI (2026-02-16) — Tüm aksiyonlar ve stratejik kararlar uygulandı
> **Revizyon:** v5 — D1-D4 stratejik kararlar Q tarafından verildi. 2 yeni sektör (Güzellik Salonu + Eğitim, 50 senaryo) + S11/S12 revenue senaryoları + internal-sales-crm.md placeholder ekleniyor

---

## Mevcut Envanter

> **v6 (2026-02-16):** S11/S12 eklendi, 2 yeni sektör (GU/EG) eklendi. Toplam 100→152.

| Bolum | Senaryo Sayisi | Notlar |
|-------|---------------|--------|
| Revenue Senaryolari (S1-S12) | 12 | v6: +S11 Abonelik, +S12 Churn |
| E-ticaret Saha (01-25) | 25 | 11 tekrar silindi (A1), referans bırakıldı |
| Dis Klinigi Saha (26-50) | 25 | |
| Estetik Klinik Saha (51-75) | 25 | |
| Otel Senaryolari (O1-O10) | 10 | |
| Mobil Senaryolari (M1-M5) | 5 | |
| Guzellik Salonu (GU-01~25) | 25 | v6: D1 karari ile eklendi |
| Egitim (EG-01~25) | 25 | v6: D1 karari ile eklendi |
| **TOPLAM** | **152** | **Silme sonrasi efektif: ~141** |

---

## A. SiL / BiRLESTiR (Tekrar Temizligi)

> **Amac:** Ayni mantikla calisan senaryolari tek noktada topla, diger yerlerde referans birak.
> **Beklenen sonuc:** B listesi 75 → ~63'e iner. Dokuman ~500 satir kisalir.

### A1. Birebir Tekrarlar (KESiN SiL)

| Ana Senaryo | Silinecek | Tekrar Icerigi | Aksiyon |
|---|---|---|---|
| **S6** (Fiyat → Randevu) | **26** | Dis kliniginde "implant kac TL?" sorusu → randevu yonlendirme. S6 zaten ayni mantigi detayli anlatiyor. | 26'nin icerigini "Bkz S6" referansiyla degistir. Dis-spesifik varyasyon notu ekle (fiyat araliklari, tedavi turleri). |
| **S7** (No-Show Onleme) | **29, 55** | Dis'te no-show (29), Estetik'te no-show (55). Uc senaryo da ayni hatirlatma zinciri: R-3gun, R-1gun, R-2saat. | 29 ve 55'i sil. S7'ye "Sektor varyasyonlari" alt bolumu ekle (dis: koltuk maliyeti 3K TL, estetik: lead degeri 15-50K TL). |
| **S8** (Post-Treatment) | **32, 45, 72** | Dis cekim sonrasi bakim (32), konsultasyon sonrasi takip (45), estetik post-op talimat (72). Hepsi ayni mekanik: T+0, T+1, T+7, T+30 mesaj zinciri. | 32/45/72'yi sil. S8'e "Tedavi Tipine Gore Talimat Sablonlari" tablosu ekle (dis cekim, implant, botox, lazer). |
| **S9** (Medikal Turizm) | **34, 61** | Dis yabanci hasta (34), estetik yabanci hasta (61). Ikisi de multi-language + paket fiyat + transfer. | 34/61'i sil. S9'a "Sektor Bazli Paket Ornekleri" ekle (dis: veneer/implant, estetik: rhinoplasty/sac ekimi). |
| **S10** (Yorum Motoru) | **50, 73** | ⚠️ **DİKKAT — Kaynak Veri Sorunu:** 50 ve 73'un basliklari "memnuniyet anketi / referral" diyor ama icerik farkli (fiyat sorulari, IG DM lead yonetimi). Once kaynak dosyada baslik/icerik uyumsuzlugu cozulmeli, sonra silinip silinmeyecegi netlesir. | 50/73'u kaynak duzeltmesinden SONRA degerlendirmeli. S10'a sektor varyasyonlari ekle. O7 (otel yorum rica) ayri kalsin cunku otel-spesifik workflow (check-out sonrasi timing, farkli tetikleyici) + Outbound Engine bagimliligi var (PMS degil — PMS bagimliligi O1/O3/O5'te). |
| **48** (Dis kapora iade) | **67** | Estetik kapora iade (67) birebir ayni. | 67'yi sil, 48'e referans. |

**Toplam silinecek:** 11 senaryo (26, 29, 32, 34, 45, 50, 55, 61, 67, 72, 73)

> **Senaryo 60 ÇIKARILDI** (onceki versiyonda S10 tekrari olarak isaretlenmisti). Senaryo 60 "birebir tekrar" DEGIL — capability set'inde C10 (Revenue Agent) + C12 (Ads Attribution) var, icerigi lead donusumu + sosyal kanit odakli, S10'un yorum recovery mekanigindan farkli. Asagidaki A2 bolumune tasindi.

### A2. Benzer Mantik (BiRLESTiRME ADAYI — Q Karari)

Bunlar birebir ayni degil ama mantik ortusmeleri var. Q karari ile birlestirilir veya oldugu gibi kalir.

| Grup | Senaryolar | Ortak Mantik | Olasi Aksiyon |
|---|---|---|---|
| **Kargo/Lojistik** | 01, 02, 06, 07, 18, 19, 24 | Hepsi "kargo sorunu" varyasyonu. Farkli alt konular: tracking, hasar, kayip, gel-al noktasi. | Tek "Kargo Lojistik Senaryolari" baslik altinda grupla. Her biri alt senaryo olarak kalsin ama ortak capability mapping'i birlestir. |
| **Odeme/Fatura** | 08, 13, 21, 22 | Platform bazli odeme sorunlari. Trendyol, Shopify, WooCommerce, genel. | "Odeme ve Fatura Senaryolari" baslik altinda grupla. Platform farki = config. |
| **Kriz De-eskalasyon** | 03, 15, 35, 57 | Hepsi kizgin musteri + empati + cozum. Iade reddi (03), gorsel uyumsuz (15), dolgu dustu (35), post-op panik (57). | Ortak "Kriz De-eskalasyon Template" olustur. Her senaryo bu template'i referans alsin. |
| **KVKK/Veri Guvenligi** | 33, 44, 62, 75 | Hasta verisi, foto/rapor, saklama/silme. | Tek "KVKK Compliance Senaryolari" baslik altinda birlestirilir. |
| **Lead Donusum + Sosyal Kanit** | 60, (73?) | IG DM lead yonetimi, speed-to-lead, kapora, before/after kanit, C10+C12 capability. 60 A1'den tasindi (S10 tekrari DEGIL). 73 icerik olarak 60'in kopyasi ama basliginda "referral" var. | Q karari: 60 oldugu gibi kalsin mi, yoksa yeni bir ana senaryoya (ornek: "IG Lead Capture") birlestirilsin mi? 73 icerik duzeltmesinden sonra degerlendirmeli. |

---

## B. EKLE — KRiTiK EKSiKLER

> **Amac:** Tum raporlarin tespit ettigi bosluklar. Oncelik sirasinda.
> **Her eksik icin:** Ne, neden, hangi raporlar tespit etti, onerilen phase, etki seviyesi.

### B1. Cross-Sektor Eksikler (EN YUKSEK ONCELIK)

Bu eksikler TUM sektorleri etkiler. Bunlar olmadan sistemin yarisinin calismayacagi konusunda 5 raporun 4'u hem fikir.

---

#### B1.1 — Opt-in Toplama Senaryosu

**Ne:** WhatsApp Business Policy geregi, 24 saat penceresi disinda template mesaj gondermek icin musteriden ACIK ONAM (opt-in) alinmasi gerekiyor. Bu onam nasil toplanacak, nerede saklanacak, nasil yonetilecek — bagimsiz, uctan uca bir senaryo olarak tanimlanmamis.

> **Mevcut referanslar:** Opt-in kavrami kaynak dokumanda mevcut — senaryo basliklarinda (50, 73 "(opt-in)"), Outbound Engine gereksinimlerinde "KVKK/GDPR uyumlu consent tracking" (satir 705), ve opt-out yonetimi (satir 702). Ancak bunlar parcali referanslar. Eksik olan: opt-in'in nasil TOPLANDIGI (hangi kanaldan, hangi formla, hangi mesajla), nerede SAKLANDI (DB schema), ve nasil YONETILDIGI (kategori bazli onam, gecmis verisi) konusunda dedicated bir senaryo/workflow.

**Neden kritik:** Bu olmadan su senaryolar CALISMIYOR:
- S4 (Siparis sonrasi proaktif satis) — outbound
- S7 (No-show hatirlatma) — outbound
- S10 (Yorum rica) — outbound
- O7 (Check-out sonrasi yorum) — outbound
- O10 (Sezonluk kampanya) — outbound
- Tum follow-up zincirleri

**Onerilen icerik:**
- Opt-in toplama kanallari: ilk WA mesajinda, web formunda, siparis onayinda, randevu formunda
- Opt-in saklama: musteri profilinde `wa_opt_in: true/false, date, source`
- Opt-out yonetimi: "STOP" mesaji → otomatik unsubscribe
- Kategori bazli onam: utility vs marketing template ayirimi
- Compliance log: kim, ne zaman, hangi kanaldan opt-in verdi

**Tespit eden:** Rapor 4 (ana bulgu), Rapor 1, 5
**Onerilen phase:** Phase 1 (Outbound Engine ile birlikte — zorunlu prerequisite)
**Etki:** BLOCKER — bu olmadan outbound senaryolarinin hicbiri yasal olarak calismaz

---

#### B1.2 — AI → Insan Handoff (Eskalasyon Kurallari)

**Ne:** AI'nin cozemedigi, emin olmadigi veya hassas konularda insana devretme mekanizmasi. Su an hicbir senaryoda bagimsiz olarak tanimlanmamis. Parcali referanslar var ama tutarli bir framework yok.

**Neden kritik:**
- AI yanlis tibbi bilgi verirse → malpractice riski
- AI yanlis fiyat verirse → yasal risk
- AI krizde yanlis yaklarsa → musteri kaybeder
- AI kapasitesini asarsa → sessizlik → musteri bekler → churn

**Onerilen icerik:**
- Handoff tetikleyicileri:
  - AI confidence < threshold (ornek: %60)
  - Belirli intent'ler (tibbi tavsiye, hukuki, fiyat kesinlestirme)
  - Musteri acikca "insanla konusmak istiyorum" dedigi zaman
  - Sentiment skoru kritik esigi astiginda
  - Ayni konuda 3+ mesaj dongusu (AI cozemiyor)
- Context aktarimi: AI'nin topladigi bilgi (intent, sentiment, musteri profili, konusma ozeti) insana transfer
- Handoff UX: musteriye "sizi uzman arkadasimiza yonlendiriyorum" mesaji
- Geri donus: insan cozdukten sonra AI ozete kayit yazar (knowledge loop)
- SLA: handoff sonrasi insan max X dakikada cevap vermeli

**Tespit eden:** Rapor 1, 2, 3, 4, 5 (tum raporlar)
**Onerilen phase:** Phase 1 (AI Assist ile birlikte — zorunlu)
**Etki:** BLOCKER — bu olmadan AI guvenilir degildir

---

#### B1.3 — AI Hallucination Guardrail

**Ne:** AI'nin tibbi, finansal veya hukuki konularda yanlislikla kesin/yaniltici bilgi uretmesini engelleyen mekanizma.

**Neden kritik:**
- Dis klinigi: "Implant 25.000 TL" dedi AI ama doktor 45.000 TL yaziyor → guven kaybi
- Estetik: "Hamileyken botox yapilabilir" derse → saglik riski → dava
- E-ticaret: "Iadeniz onaylandi" dedi ama iade kosullarini karsilamiyor → operasyonel kaos
- Otel: "Oda musait" dedi ama dolu → musteri geldi, oda yok

**Onerilen icerik:**
- "Bilmiyorum" capacity: AI emin olmadigi konuda acikca "Bu konuda kesin bilgi veremiyorum, sizi uzmanımıza yonlendiriyorum" demeli
- Konu bazli guardrail listesi:
  - Tibbi tavsiye → ASLA kesin diagnosis verme
  - Fiyat → "aralik" ver, "kesin fiyat muayenede/gorusmede belirlenir" ekle
  - Ilac/dozaj → ASLA oneri yapma, doktora yonlendir
  - Hukuki (iade hakki, garanti) → knowledge base'den kaynak goster, yorum ekleme
- Confidence-based routing: dusuk confidence → human handoff (B1.2 ile entegre)
- Audit log: AI'nin verdigi her cevabin kaydi + confidence skoru

**Tespit eden:** Rapor 2 (ana bulgu), Rapor 3, 5
**Onerilen phase:** Phase 1 (Agent Assist ile birlikte)
**Etki:** YUKSEK — yasal risk azaltma, guven insa

---

#### B1.4 — SLA Watchdog / Failover

**Ne:** Mesaj bekleme suresi asildigi, agent offline oldugu veya AI cevap uretemediginde otomatik mudahale mekanizmasi.

**Neden kritik:**
- Mesaj 5dk+ cevapsiz → musteri gider (e-ticaret donusum %5'e duser)
- Agent hastalandı, hepsi offline → mesajlar biriyor, kimse farketmiyor
- AI servisi down → sessizlik → musteri "bozuk mu bu?" der
- VIP lead 1 saat bekliyor → rakibe gitmis

**Onerilen icerik:**
- SLA kuralları (tenant bazli konfigüre edilebilir):
  - Genel: 5dk icinde ilk yanit
  - VIP: 2dk icinde ilk yanit
  - Acil (saglik): 1dk icinde
- Watchdog mekanizmasi:
  - SLA suresi dolmadan 1dk → uyari (agent'e push)
  - SLA suresi doldu → otomatik eskalasyon (supervisor'a)
  - SLA 2x asildi → emergency routing (musait herhangi agent veya AI fallback mesaji)
- AI failover:
  - AI servisi down → "Su an yogunuz, en kisa surede donecegiz" template mesaji
  - AI 3 kez ust uste dusuk confidence → human routing
- Dashboard metrikleri: SLA breach sayisi, ortalama bekleme, breach recovery suresi

**Tespit eden:** Rapor 3 (ana bulgu), Rapor 1
**Onerilen phase:** Phase 1-2
**Etki:** YUKSEK — operasyonel guvenilirlik

---

#### B1.5 — Churn Sinyali Tespiti

**Ne:** Musterinin acikca sikayet etmeden terk etme sinyalleri veren cumlelerin AI ile tespit edilip proaktif mudahale yapilmasi.

**Neden kritik:**
- "Bir daha almayacagim" → sikayet degil ama kayip sinyali
- "Fiyat cok artmis" → fiyat hassasiyeti, rakibe bakiyor olabilir
- "Eskiden daha iyiydi" → memnuniyet dususu
- "Dusuneyim" (follow-up sonrasi) → soguma sinyali
- Bu sinyaller yakalanmazsa → sessiz churn → en pahali kayip turu

**Onerilen icerik:**
- Churn sinyal kelimeleri/pattern'leri:
  - Pasif agresif: "neyse", "bos ver", "bir daha ugrasmam"
  - Karsilastirma: "rakip X daha ucuz", "baska yere bakiyorum"
  - Soguma: 3+ gun cevap yok (aktif konusmada), "dusuneyim" + sessizlik
  - Frekans dususu: duzenli musteri → uzun sure siparis/randevu yok
- Risk skoru: Low / Medium / High / Critical
- Otomatik aksiyon:
  - Medium: Agent'e "dikkat: kayip riski" badge
  - High: Supervisor'a alert + onerilen kurtarma aksiyonu
  - Critical: Outbound kurtarma mesaji (ozel teklif, VIP ilgi)
- Dashboard: churn risk pipeline, kurtarilan vs kaybedilen

**Tespit eden:** Rapor 2, 3
**Onerilen phase:** Phase 3 (sentiment analysis altyapisi gerekir)
**Etki:** ORTA-YUKSEK — retention artisi, LTV artisi

---

#### B1.6 — Unified Customer Timeline

**Ne:** Tek bir musteri/hasta icin TUM kanallardaki (WA, IG DM, telefon, email, siparis, randevu, yorum) etkilesim gecmisinin tek bir zaman cizelgesinde gorulmesi.

**Neden kritik:**
- Su an: WA'dan yazdi → ayri, IG'den yazdi → ayri, telefon aradı → ayri
- Agent musterinin gecmisini goremiyor → "daha once yazmistim" → "ne icin yazmistiniz?"
- Intent AI tek mesaja bakiyor, gecmis context yok → yuzeysel analiz
- VIP flag anlamsizlasiyor cunku toplu etkilesim gorulmuyor
- Follow-up kor oluyor: S9'da hasta 3 hafta once IG'den yazdi, simdi WA'dan yazıyor, baglanti kurulamiyor

**Onerilen icerik:**
- Musteri profili: telefon + email + IG handle + WA numara ile eslestirme
- Timeline gorunumu: kronolojik, kanal ikonu ile
- Her entry'de: kanal, tarih, konu/intent, cozum durumu, agent
- AI icin context window: son 10 etkilesim ozeti → cevap onerisi icin
- CRM entegrasyonu: siparis gecmisi, randevu gecmisi, yorum gecmisi

**Tespit eden:** Rapor 3 (ana bulgu)
**Onerilen phase:** Phase 2-3 (CRM derinlestirme ile birlikte)
**Etki:** YUKSEK — tum AI ve routing kalitesini arttirir

---

#### B1.7 — Revenue Attribution

**Ne:** Her satisin/randevunun hangi kanaldan, hangi mesajdan, AI mi insan mi tarafindan kapatildiginin takibi.

**Neden kritik:**
- "300K TL kazaniyoruz" diyorsun ama ispat yok
- Hangi senaryo gercekten para kazandiriyor belli degil
- Enterprise musteri soruyor: "AI ROI'niz nedir?" → cevap yok
- Kampanya optimizasyonu yapilamiyor: hangi outbound template daha iyi donuyor?

**Onerilen icerik:**
- Conversion source tracking: ilk temas kanali (WA organic, IG ad, Google, referral)
- AI vs Human flag: cevabi AI mi onerdi, insan mi yazdi, ikisi birlikte mi
- Deal value: randevu → tedavi tutari, siparis → sepet tutari
- Funnel: lead → first response → qualified → appointment/purchase → closed
- Dashboard: kanal bazli ROI, agent bazli kapanis orani, AI assist orani

**Tespit eden:** Rapor 3 (ana bulgu), Rapor 1
**Onerilen phase:** Phase 2-3
**Etki:** YUKSEK — enterprise satis icin sart, kendi ROI'mizi kanitlamamiz lazim

---

#### B1.8 — Compliance Otomasyonu (KVKK/GDPR Framework)

**Ne:** Sadece "KVKK'ya dikkat" demek yetmez. Sistematik compliance altyapisi.

**Neden kritik:**
- Saglik verisi = KVKK ozel nitelikli veri → ihmal = agir ceza
- AB musterisi varsa GDPR de gecerli
- Opt-in kayitlari, veri silme talepleri, erisim haklari → hepsi otomatik olmali
- Denetim geldiginde kanit sunabilmek lazim

**Onerilen icerik:**
- Explicit consent flow: her kanalda acik onam toplama + kayit
- Opt-in log: kim, ne zaman, hangi kanaldan, ne icin onam verdi
- Template audit trail: gonderilen her template mesajin kaydi
- Veri silme hakki: musteri "verimi silin" dedi → otomatik is akisi
- Veri erisim hakki: musteri "verilerim neler?" dedi → otomatik rapor
- Saklama suresi: saglik verisi X yil, ticari veri Y yil
- Maskeleme: TC kimlik, telefon, saglik bilgisi goruntulemede maskelenmeli
- Audit log: kim hangi veriye ne zaman erisit → kayit

**Tespit eden:** Rapor 1, 3, 5
**Onerilen phase:** Phase 1 (temel) → Phase 4 (enterprise tam)
**Etki:** YUKSEK — yasal zorunluluk, enterprise satis engeli

---

### B2. E-ticaret Eksikleri

| # | Senaryo | Detay | Phase | Etki |
|---|---|---|---|---|
| **B2.1** | **Stok Bildirim (Back-in-Stock)** | Musteri "gelince haber ver" dedi. Stok girisi olunca otomatik WA mesaji. Outbound Engine + stok entegrasyonu gerektirir. Opt-in zorunlu. Template kategorisi: utility. | Phase 2-3 | ORTA — musteri memnuniyeti + donusum |
| **B2.2** | **Influencer/Affiliate Attribution** | "Kod neydi?", "Link acilmiyor" mesajlari. Influencer kodu/UTM ile kampanya bazli etiketleme. Hangi influencer ne kadar satis getirdi? Pazarlama butcesi optimizasyonu icin sart. | Phase 3 | ORTA — pazarlama ROI |
| **B2.3** | **Proaktif Siparis Durum Guncelleme** | Kargo gecikmesi, stok sorunu olunca MUSTERIDEN ONCE bilgilendir. "Siparisinizdeki X urunu stok sorunu nedeniyle 2 gun gecikmeli gonderilecek." Kriz oncesi mudahale. S4 ile iliskili ama farkli: S4 satis, bu bilgilendirme. | Phase 2 | YUKSEK — sikayet onleme |
| **B2.4** | **Cross-Platform Siparis Eslestirme** | Musteri Trendyol'dan aldi, WA'dan yaziyor, HB siparisi de var. Hangi siparis? Telefon numarasi ile cross-platform eslestirme. C11 entegrasyon gerektirir. | Phase 2 | ORTA — operasyonel verimlilik |
| **B2.5** | **Sikayetvar / BTK Eskalasyon** | Senaryo 03'te bahsedilmis ama bagimsiz senaryo olmali. Sikayetvar'a dusen case'in WA uzerinden proaktif cozumu. "Sikayetvar'da yazisinizi gorduk, sorunu hemen cozmek istiyoruz." Risk: cok gec kalirsa etki sifir. | Phase 3 | ORTA — itibar koruma |
| **B2.6** | **Garanti ve Teknik Servis** | "Urun bozuldu, garanti kapsaminda mi?", "Teknik servise nasil gonderecegim?" Garanti suresi kontrol + teknik servis yonlendirme. Knowledge base + entegrasyon. | Phase 3 | DUSUK-ORTA |
| **B2.7** | **Fraud / Dolandiricilik Suphesi** | "Bu siparisi ben vermedim", "Hesabim calindi". Yuksek oncelikli, hassas. AI panik butonu → hesap dondurma + uzman agent'e acil yonlendirme. Normal kuyruk bypass. | Phase 2 | YUKSEK — guvenlik |

---

### B3. Saglik Eksikleri

| # | Senaryo | Detay | Phase | Etki |
|---|---|---|---|---|
| **B3.1** | **Tedavi Plani Onay Akisi** | Doktor plan gonderdi (PDF/mesaj), hasta onay vermedi. Follow-up zinciri: T+1gun "Tedavi planinizi incelediniz mi?", T+3gun "Sorulariniz varsa yardimci olabiliriz", T+7gun son hatirlatma. Onay gelmezse supervisor'a alert. Ciddi gelir kaybi — plan gonderilip takip edilmeyen her hasta = kaybedilen 15-50K TL tedavi. | Phase 2 | YUKSEK |
| **B3.2** | **Sigorta Provizyon On Kontrol** | 37'de sigorta sorusu var ama yuzeysel. Gercek ihtiyac: poliçe no + TC kimlik ile provizyon sorgusu, kapsam kontrolu, katkı payi hesabi. Saglikta buyuk operasyonel yuk. Tam otomasyon zor (sigorta API'leri karmasik), ama en azindan bilgi toplama + manuel onaya sunma sureci. | Phase 3-4 | ORTA |
| **B3.3** | **Coklu Klinik/Sube Yonetimi** | Hasta hangi subeye gidecek? Doktor hangi subede, hangi gun? Konum bazli yonlendirme. "Kadikoy subemizde Dr. Mehmet Pazartesi-Carsamba, Besiktas subemizde Persembe-Cumartesi." Zincir klinikler icin sart. | Phase 2 | ORTA |
| **B3.4** | **Tedavi Oncesi Hazirlik Talimatlari** | Ameliyat oncesi: 8 saat aclik, X ilaci kesin, Y ilaci devam edin, randevuya refakatci ile gelin. S8'in tersi — S8 post-op, bu pre-op. Ayni mekanik (T-3gun, T-1gun, T-sabah mesaj zinciri) ama icerik farkli. Hasta hazirligi eksikse ameliyat iptal → koltuk bos → gelir kaybi. | Phase 2 | YUKSEK |
| **B3.5** | **Recete/Ilac Sorgulari** | "Recetemi yazdiniz mi?", "Ilaci nereden alacagim?", "Dozaji ne kadardi?" Tekrarli sorular. Knowledge base'den otomatik cevaplanabilir. Dikkat: dozaj onerisi YAPMA, sadece doktorun verdigi bilgiyi tekrarla. Hallucination guardrail (B1.3) ile entegre. | Phase 3 | DUSUK-ORTA |

---

### B4. Otel Eksikleri (EN BUYUK BOSLUK)

> 5 raporun 3'u oteli "en zayif sektor" olarak isaretledi.
> Mevcut 10 senaryo temel sorulari kapsiyor ama IN-STAY (misafir oteldeyken) ve OPERASYONEL senaryolar tamamen eksik.

| # | Senaryo | Detay | Phase | Etki |
|---|---|---|---|---|
| **B4.1** | **O11 — Oda Servisi Siparisi** | "Odaya kahvalti gonderin", "Gece oda servisi menunuz var mi?" Misafir oteldeyken WA uzerinden siparis. Menu gonderme + siparis onay + tahmini sure. PMS/POS entegrasyonu ideal ama Phase 1'de template ile baslanabilir. | Phase 1 (template) / Phase 2 (POS) | YUKSEK |
| **B4.2** | **O12 — Housekeeping Talebi** | "Odaya havlu lazim", "Temizlik yapilmamis", "Yastik degisimi". En sik in-stay talebi. AI → housekeeping departmanina otomatik is emri. Cozum sonrasi "Talebiniz karsilandi mi?" follow-up. | Phase 1 (routing) | YUKSEK |
| **B4.3** | **O13 — Spa/Restoran Rezervasyonu** | Tesis ici hizmet rezervasyonlari. "Aksam 8'de 2 kisilik masa ayirtabilir miyim?", "Yarin 15:00'te masaj randevusu." Slot yonetimi. Dis/estetik randevu mantigi ile ortak altyapi. | Phase 2 | ORTA |
| **B4.4** | **O14 — Late Check-out / Early Check-in** | "Saat 10'da geliyorum, oda hazir mi?", "Ucusumuz aksam, gec cikis yapabilir miyiz?" Cok sik. AI musaitlik kontrolu + ucretli/ucretsiz secenek sunma. Saf kar marji (oda zaten bos ise). Upsell firsati. | Phase 1 (template) / Phase 2 (PMS) | YUKSEK |
| **B4.5** | **O15 — Fatura/Odeme Sorunlari** | "Faturada hata var", "Ekstra ucret ne icin?", "Kurumsal fatura keser misiniz?" Hassas konu — para. AI bilgilendirme + eskalasyon. | Phase 1 | ORTA |
| **B4.6** | **O16 — OTA Mesaj Entegrasyonu** | Oteller sadece WA'dan degil, Booking.com ve Expedia mesaj kanallarindan da mesaj aliyor. Bu kanal TAMAMEN EKSIK. En azindan senaryo olarak tanimlanmali, entegrasyon Phase 2-3'te. | Phase 2-3 | YUKSEK |
| **B4.7** | **O17 — Ozel Gun/Kutlama** | Dogum gunu, balayi, evlilik teklifi organizasyonu. "Esimin dogum gunu, surprise yapar misiniz?" Yuksek deger, dusuk hacim. VIP deneyim. Upsell: pasta, cicek, oda dekorasyonu. | Phase 3 | DUSUK-ORTA |

---

### B5. Mobil Eksikleri

| # | Senaryo | Detay | Phase | Etki |
|---|---|---|---|---|
| **B5.1** | **M6 — QR Kod ile Hizli Erisim** | Otel odasinda, restoran masasinda, klinik bekleme salonunda QR kod → direkt WA konusmasi baslatma. Fiziksel → dijital kopru. Opt-in toplama firsati da (QR tararken onam). | Phase 7 (mobil) | ORTA |
| **B5.2** | **M7 — Cevrimdisi Mod** | Internet olmadan onceki mesajlari gorme, taslak kaydetme. Saha calisanlari, depo ziyaretleri, ucus sirasinda. Sync olunca gonder. | Phase 7 (mobil) | DUSUK-ORTA |

---

## C. YAPISAL DUZELTMELER

> Senaryo icerigini degil, dokumanin YAPISINI iyilestirme.

### C1. Revenue (A) → Saha (B) Mapping Tablosu

**Sorun:** Revenue senaryolari ile saha senaryolari arasindaki iliski parcali. Parantez ici referanslar var ama tutarsiz.

**Aksiyon:** Her saha senaryosuna "Besler → S#" satiri ekle. Ornek:
```
SENARYO 01 — Kargom nerede?
Besler → Genel operasyonel verimlilik (dogrudan revenue senaryosuna bagli degil)

SENARYO 26 — Fiyat sorusu (SILINDI — bkz S6)
Besler → S6 (Fiyat → Randevu Donusumu)
```

Ayrica ters mapping tablosu ekle (S# → hangi saha senaryolari besliyior):
```
S1 Review Recovery ← 03, 15 (kriz senaryolari sentiment verisi saglar)
S6 Fiyat → Randevu ← 26 (silindi), 41, 51
S7 No-Show ← 29 (silindi), 40, 55 (silindi)
...
```

---

### C2. Phase Bagimlilik Tablosu

**Sorun:** Bazi senaryolarin phase atamalari belirsiz ("Phase 2-3" gibi araliklar) ve bagimlilik zincirleri acik degil.

**Aksiyon:** Net bagimlilik tablosu olustur:
```
| Senaryo | Phase | Bagimliliklar (once bunlar hazir olmali) |
|---------|-------|------------------------------------------|
| S1      | 3     | C11 (Trendyol API), Outbound Engine, Sentiment |
| S4      | 5     | Outbound Engine GELISMIS (Phase 2: follow-up zincirleri, cross-sell kurallari), C11 entegrasyon |
| S7      | 1     | Outbound Engine TEMEL (Phase 1: broadcast + trigger) |
| S9      | 3     | Multi-language AI (Phase 3), Outbound |
| ...     |       |                                          |
```

> **S4 hakkinda NOT:** S4 Phase 5'te — bu tutarsizlik DEGIL. Outbound Engine'in iki seviyesi var (satir 682-683):
> - **Phase 1 = temel:** broadcast + trigger (S7 icin yeterli)
> - **Phase 2 = gelismis:** follow-up zincirleri + cross-sell kurallari (S4 icin gerekli)
>
> S4'un Phase 5'te olmasi, Outbound v2'nin otesinde ek bagimliliklari olduğunu gosterir (cross-sell oneri motoru, musteri segmentasyonu, C11 marketplace entegrasyonu). Phase indirgeme onerisi KALDIRILDI.

---

### C3. Saha Senaryolarina Etki Seviyesi Ekle

**Sorun:** Revenue senaryolarinda (A) detayli TL hesaplari var. Saha senaryolarinda (B) hic yok. ROI gosteren A'yi satista kullanabiliyorsun, B'yi kullanamazsin.

**Aksiyon:** Her saha senaryosuna en azindan etki etiketi ekle:
```
| Etki | Anlam | Ornek |
|------|-------|-------|
| 🔴 YUKSEK | >50K TL/ay potansiyel veya kritik risk | 01 kargo, 03 iade krizi |
| 🟡 ORTA | 10-50K TL/ay potansiyel | 08 fatura, 11 beden sorusu |
| 🟢 DUSUK | <10K TL/ay veya operasyonel iyilestirme | 25 template, 39 kayit |
```

---

### C4. Entegrasyon Gereksinimleri Tablosu

**Sorun:** Senaryolarda "entegrasyon gerekir" deniyor ama hangi API/arac belirtilmiyor.

**Aksiyon:** Dokuman sonuna entegrasyon matrisi ekle:
```
| Entegrasyon | Senaryolar | API/Arac | Phase |
|-------------|-----------|----------|-------|
| Trendyol API | S1, 01, 03, 19, 20 | Trendyol Seller API v2 | Phase 2 |
| Hepsiburada API | 19 | HB Open API | Phase 2 |
| Shopify | 21 | Shopify Admin API | Phase 2 |
| WooCommerce | 22 | WooCommerce REST API | Phase 2 |
| PMS (Otel) | O1, O3, O5 | OPERA, Protel vb. | Phase 2 |
| Booking.com | O16 | Booking Connectivity API | Phase 2-3 |
| Google Business | S10, O7 | Google Business Profile API | Phase 3 |
| Odeme | 36, 68, O15 | iyzico/Param/PayTR | Phase 2 |
```

---

### C5. KVKK Risk Skoru Ekle

**Sorun:** Saglik senaryolarinda KVKK vurgusu var ama sistematik degil. E-ticarette hic yok.

**Aksiyon:** Her senaryoya KVKK risk etiketi ekle:
```
| Risk | Kriter | Ornekler |
|------|--------|----------|
| 🔴 YUKSEK | Saglik verisi, ozel nitelikli kisisel veri | 33, 44, 62, 75, 30, 47 |
| 🟡 ORTA | Kisisel veri (ad, telefon, adres, siparis) | 08, 09, 13, 36, 68 |
| 🟢 DUSUK | Anonim/genel bilgi | 01, 02, 12, O2, O8 |
```

---

### C6. Dis Sunum vs Internal Playbook Ayirimi

**Sorun:** 75 saha senaryosu teknik doküman gibi. Musteriye veya yatirimciya agir. Ama internal olarak cok degerli.

**Aksiyon (oneri — Q karari):**
- `roadmap-scenarios.md` → internal playbook olarak kalsin
- Yeni dosya: `ideas/core-use-cases.md` → dis sunum icin 8-10 core use case
  - Lead Capture + First Response AI
  - Intent + VIP Detection
  - Follow-up / No-Show Engine
  - Review & Referral Engine
  - Human + AI Routing
  - Knowledge Base (sektorel FAQ)
  - Outbound Campaigns
  - Analytics & Attribution
- Her use case: 1 paragraf aciklama + 3 sektor ornegi + ROI ozeti

---

## D. STRATEJiK KARARLAR ✅ KARARA BAGLANDI (2026-02-16)

> **Durum:** Tum D kararlari Q tarafindan 2026-02-16 tarihinde verildi.

### D1. Yeni sektor eklenmeli mi? → ✅ KARAR: B — 2 sektor ekle (Guzellik Salonu + Egitim)

| Secenek | Avantaj | Dezavantaj |
|---------|---------|------------|
| A: Ekleme, 4 sektore odaklan | Focus, hizli delivery, mevcut musterilere deger | Pazar sinirli kalabilir |
| **→ B: 2 sektor ekle (Guzellik Salonu + Egitim)** | **Pazar genisler, 6 sektore cikar** | **Kaynak dagitir, 50 yeni senaryo** |

**Q Karari:** Guzellik Salonu (kuafor, berber, cilt bakim, nail art) + Egitim (kurs, dershane, online egitim) ekleniyor. Her biri 25 senaryo ile. Phase 3+ isi ama senaryo tanimlari simdi yapiliyor.

### D2. B serisini kaca indirelim? → ✅ KARAR: A — Tekrarlar silindi, yeter

| Secenek | Sonuc |
|---------|-------|
| **→ A: Sadece tekrarlari sil (~63)** | **Minimal mudahale, bilgi kaybi yok** |
| B: Benzerleri de birlestir (~45-50) | Daha temiz ama bilgi kaybi riski |
| C: 8-10 core use case + internal playbook | Dis sunum temiz, internal zengin kalir |

**Q Karari:** 11 tekrar zaten silindi (A1). Geri kalanlar grup etiketleriyle (A2) duzenlendi. Ek birlestirme yapilmiyor. Internal playbook olarak tam liste korunuyor.

### D3. Revenue senaryolari kac tane olmali? → ✅ KARAR: C — 12'ye cikar (+Abonelik, +Churn)

| Secenek | Icerik |
|---------|--------|
| A: 10 koru (mevcut) | Genis yelpaze, her sektore bir seyler |
| B: 5'e indir (Rapor 3 onerisi) | S1, S3, S6, S7, S9 tut. Digerleri internal. |
| **→ C: 12'ye cikar (+S11 Abonelik, +S12 Churn)** | **Yeni revenue kanallari ekle** |

**Q Karari:** S11 (Abonelik/Uyelik Modeli) ve S12 (Churn Prevention/Win-back) ekleniyor. Musteri senaryosu olarak: e-ticaret abonelik kutusu, klinik uyelik, otel sadakat + churn sinyali tespiti.

### D4. Internal Sales CRM senaryosu eklenmeli mi? → ✅ KARAR: A — Ayri dokuman

| Secenek | Anlam |
|---------|-------|
| **→ A: Ayri dokuman** | **`ideas/internal-sales-crm.md` olarak ayir** |
| B: roadmap-scenarios'a ekle | Yeni bolum: "E) Invekto Internal Sales" |
| C: Simdilik atlat | Phase 3+ isi |

**Q Karari:** roadmap-scenarios.md = musteri senaryolari icin. Invekto'nun kendi satis sureci ayri dokumanda (`ideas/internal-sales-crm.md`). Placeholder olusturulacak.

---

## UYGULAMA ONCELIK SIRASI

> **v2 DUZELTME:** Stratejik kararlar (D) onceki versiyonda 6. siradaydi ama 1-5. adimlar
> bazi D kararlarina bagimli (ornek: A2 birlestirme = D2 kararina bagli, C2 phase tablosu = D3'e bagli).
> Engeller onceye alindi.

| Sira | Is | Tahmini Etki | Zorluk | Bagimlilik |
|------|---|---|---|---|
| **0** | ~~ENGELLEYICI KARARLAR~~ ✅ D2=A (tekrarlar silindi), D3=C (12'ye cikar) | Tamamlandi | — | — |
| **1** | Kaynak veri duzeltmeleri (50, 73 baslik/icerik uyumsuzlugu) | Veri butunlugu | Kolay — kaynak dosyada baslik guncelle | Adim 0 |
| **2** | Tekrarlari temizle (A bolumu) | Dokuman kalitesi | Kolay — sil + referans ekle | Adim 0 (D2: B serisi boyutu) + Adim 1 |
| **3** | Cross-sektor kritikleri ekle (B1.1-B1.4) | Sistem guvenilirligi | Orta — yeni senaryo yaz | — |
| **4** | Otel boslugunu kapat (B4) | Sektor kapsami | Kolay — yeni senaryo yaz | — |
| **5** | Yapisal duzeltmeler (C1-C5) | Dokuman kullanilabilirligi | Orta — tablo ve mapping | Adim 0 (D3: phase netlik) + Adim 2 |
| **6** | E-ticaret + saglik eksikleri (B2, B3) | Senaryo derinligi | Kolay — yeni senaryo yaz | — |
| **7** | ~~ERTELENMIS KARARLAR~~ ✅ D1-D4 KARARA BAGLANDI | Tamamlandi | — | — |
| **8** | Mobil eksikleri (B5) | Phase 7 isi | Dusuk oncelik | — |
| **9** | Dis sunum formati (C6) | Satis etkisi | Q karari gerekli | Adim 2 tamamlandiktan sonra |

---

## KAYNAK: REVIEW RAPORLARI OZETI

| Rapor | Odak | Ana Bulgular |
|-------|------|-------------|
| **Rapor 1** | Eksik/fazla/eklenecek genel | Phase belirsizligi, ROI eksik, entegrasyon detaysiz, KVKK yetersiz, mobil link eksik. +15-20 tekrar, +25-30 ekleme. |
| **Rapor 2** | Operasyonel guvenlik + buyume | Influencer attribution, sigorta on kontrol, AI hallucination, churn tespiti, late checkout upsell. |
| **Rapor 3** | Stratejik yapi | "Cok senaryo, az sistem". Unified timeline, revenue attribution, SLA watchdog, compliance, internal CRM eksik. 5 revenue engine onerisi. |
| **Rapor 4** | Teknik detay + eksik senaryolar | Opt-in (en kritik), handoff, cross-platform, sikayetvar, tedavi plani onay, OTA entegrasyon. ~8 sil, ~12 ekle. |
| **Rapor 5** | Genel kalite + derinlik | Multi-language yonetimi, sesli mesaj, fraud, abonelik, garanti, ikinci gorus. Phase standardizasyonu, KVKK, metrik tutarliligi onerisi. |

> **5 raporun ortaklastigi TOP 3 bulgu:**
> 1. Opt-in + Handoff + Guardrail olmadan sistem calismiyor
> 2. ~11 senaryo tekrar, silinmeli (v1'de 12'ydi, 60 cikarildi)
> 3. Otel sektoru en zayif, 7+ senaryo eklenmeli

---

## ERRATA — v2 Duzeltmeleri (Dogrulama Review Sonrasi)

> **Tarih:** 2026-02-16 | **Tetikleyen:** 5 maddelik dogrulama review'i (2 BLOCKER, 2 HIGH, 1 MEDIUM)

### E1. [BLOCKER] Senaryo 60 — S10 Tekrari DEGIL

**v1 hatasi:** Senaryo 60 "birebir tekrar" olarak S10 sil listesinde isaretlenmisti.
**Gercek:** Senaryo 60'in capability set'i C1,C2,C3,C4,C8,**C10**,**C12** — Revenue Agent + Ads Attribution iceriyor. Icerigi IG DM lead yonetimi, speed-to-lead, kapora/randevu odakli. S10 (yorum motoru) ile ayni mekanik DEGIL.
**Duzeltme:** 60, A1 sil listesinden cikarildi → A2 birlestirme adaylarina tasindi. Toplam silinecek: 12 → 11.
**Kaynak:** `roadmap-scenarios.md` satir 3588-3624

### E2. [BLOCKER] O7 Ayri Kalma Gerekcesi — PMS Degil, Outbound Engine

**v1 hatasi:** "O7 ayri kalsin cunku otel-spesifik PMS entegrasyonu var" denmisti.
**Gercek:** O7'nin bagimliligi "Outbound Engine" (satir 4543). PMS bagimliligi O1/O3/O5'e ait (satir 4537-4541).
**Duzeltme:** O7'nin ayri kalma gerekcesi duzeltildi: sektor-spesifik workflow (check-out sonrasi timing + farkli tetikleyici) + Outbound Engine bagimliligi.
**Kaynak:** `roadmap-scenarios.md` satir 4537-4546

### E3. [HIGH] Opt-in "Hicbir Senaryoda Yok" — Kesin Ifade Yanlis

**v1 hatasi:** "Bu onam nasil toplanacak... hicbir senaryoda yok" denmisti.
**Gercek:** Opt-in kavrami kaynak dokumanda MEVCUT:
- Senaryo 50 basligi: "Hasta memnuniyet anketi **(opt-in)**"
- Senaryo 73 basligi: "Memnuniyet anketi + referral isteme **(opt-in)**"
- Outbound Engine gereksinimleri satir 702: opt-out yonetimi, satir 705: KVKK/GDPR consent tracking
**Duzeltme:** Ifade yumusatildi: "hicbir senaryoda yok" → "bagimsiz, uctan uca bir senaryo olarak tanimlanmamis". Mevcut referanslar acikca belirtildi. Eksik olan parcali referanslar degil, dedicated toplama workflow'u.
**Kaynak:** `roadmap-scenarios.md` satir 702, 705, 3083, 4245

### E4. [HIGH] Uygulama Oncelik Sirasi — Bagimlilik Celiskisi

**v1 hatasi:** Stratejik kararlar (D) 6. sirada, ama 1-5. adimlar bazi D kararlarina bagimli.
- A2 birlestirme → D2'ye bagli ("B serisini kaca indirelim?")
- C2 phase tablosu → D3'e bagli ("Revenue senaryolari kac tane olmali?")
**Duzeltme:** D ikiye ayrildi:
- **Adim 0: Engelleyici kararlar** (D2, D3) — once alinmali, diger adimlar buna bagimli
- **Adim 7: Ertelenmis kararlar** (D1, D4) — Phase 3+ gundemine birakilabilir, acil engellemez

### E5. [MEDIUM] S4 Phase Indirgeme Onerisi — Outbound Basic/Advanced Ayrimi Gozardi Edilmis

**v1 hatasi:** "S4'un Phase'i 5'ten 2'ye dusurulmeli — Outbound Engine Phase 1'de hazir olacaksa" denmisti.
**Gercek:** Outbound Engine'in iki seviyesi var (satir 682-683):
- Phase 1 = **temel:** broadcast + trigger (S7 tipi senaryolar icin yeterli)
- Phase 2 = **gelismis:** follow-up zincirleri + cross-sell kurallari (S4 icin gerekli)
S4 Phase 5'te cunku Outbound v2'nun OTESINDE ek bagimliliklar var (cross-sell oneri motoru, musteri segmentasyonu, C11 marketplace entegrasyonu).
**Duzeltme:** Phase indirgeme onerisi kaldirildi. C2 bolumunde Outbound temel/gelismis ayrimi acikca belirtildi.
**Kaynak:** `roadmap-scenarios.md` satir 163, 682-683

### EK BULGU: Senaryo 50 ve 73 — Baslik/Icerik Uyumsuzlugu

**Tespit:** Kaynak dokumandaki veri kalitesi sorunu:
- **Senaryo 50** basligi: "Hasta memnuniyet anketi (opt-in)" → icerigi: fiyat sorulari, lead kaybi, tutarsiz fiyat vaat riski (satir 3083-3124)
- **Senaryo 73** basligi: "Memnuniyet anketi + referral isteme (opt-in)" → icerigi: Senaryo 60 ile neredeyse birebir ayni (IG DM lead yonetimi, speed-to-lead) (satir 4245-4282)
**Etki:** Bu uyumsuzluk A1 tablosundaki S10 silinecek karari etkiliyor — basliga gore tekrar gibi gorunuyorlar ama icerik farkli.
**Oneri:** Kaynak dosyada once baslik/icerik uyumu saglannmali, SONRA sil/birlestir karari verilebilir.
