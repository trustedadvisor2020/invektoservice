# InvektoServices Platform Raporu

> **Tarih:** 2026-02-18 | **Hazirlayan:** Claude (Q icin)

---

## Ozet

InvektoServices, **WhatsApp tabanli musteri iletisim ve CRM platformu**dur. 10 mikroservis + 1 gorsel flow builder'dan olusur. Her servis bagimsiz deploy edilir, JWT ile guvenli iletisim kurar ve multi-tenant (coklu isletme) destekler.

**Hedef sektorler:** Saglik (dis klinikleri, estetik), E-ticaret, Medikal turizm

---

## Servis Haritasi (Tek Bakista)

```
                          ┌─────────────────────┐
                          │    Main App (CRM)    │
                          │  WhatsApp Frontend   │
                          └─────────┬───────────┘
                                    │
                          ┌─────────▼───────────┐
                          │   Backend (:5000)    │
                          │   API Gateway +      │
                          │   Ops Dashboard +    │
                          │   Flow Builder UI    │
                          └─────────┬───────────┘
                                    │
            ┌───────────────────────┼───────────────────────┐
            │                       │                       │
   ┌────────▼────────┐   ┌────────▼────────┐   ┌─────────▼────────┐
   │  AI Katmani     │   │  Is Surecleri   │   │  Analiz & Veri   │
   │                 │   │                 │   │                  │
   │ • ChatAnalysis  │   │ • Automation    │   │ • WA Analytics   │
   │ • AgentAI       │   │ • Outbound      │   │ • Knowledge      │
   │ • Marketing     │   │ • Appointments  │   │ • Integrations   │
   │   (AI Response) │   │ • Marketing     │   │                  │
   └─────────────────┘   └─────────────────┘   └──────────────────┘
```

---

## 1. Backend (API Gateway)

**Ne yapar:** Tum servislerin tek giris noktasi. Main App (WhatsApp CRM arayuzu) sadece Backend'e konusur, Backend istekleri ilgili servise yonlendirir.

**Musteri faydasi:**
- Tek endpoint, tek baglanti — frontend karmasikligi sifir
- Ops Dashboard ile tum servislerin sagligi tek ekrandan izlenir
- Flow Builder UI'i host eder (gorsel chatbot tasarimi)
- Metrik toplama (5 dk'da bir) ile performans takibi

**Entegrasyonlar:** 9 servise proxy, hepsinin health check'ini toplar

---

## 2. ChatAnalysis (Sohbet Analizi)

**Ne yapar:** Musterinin WhatsApp sohbet gecmisini alir, Claude AI ile **15 farkli kriteri paralel analiz** eder, sonucu callback ile geri gonderir.

**15 Analiz Kriteri:**
| # | Kriter | Aciklama |
|---|--------|----------|
| 1 | Icerik | Mesajin konusu ve temasi |
| 2 | Tutum | Musterinin genel tavri (olumlu/olumsuz/notr) |
| 3 | Yaklasim Onerisi | Temsilciye nasil yaklasmasi gerektigi |
| 4 | Satin Alma Olasiligi | 0-100 arasi skor |
| 5 | Ihtiyaclar | Musterinin acik/gizli ihtiyaclari |
| 6 | Karar Sureci | Musteri nasil karar veriyor |
| 7 | Satis Engelleri | Satisi engelleyen faktorler |
| 8 | Iletisim Tarz | Resmi/samimi/teknik vs. |
| 9 | Musteri Profili | Demografik/davranissal profil |
| 10 | Memnuniyet | Mevcut memnuniyet seviyesi |
| 11 | Teklif & Donusum | Teklif etkinligi |
| 12 | Destek Stratejisi | Onerilen destek yaklasimi |
| 13 | Rakip Analizi | Rakip urunlere atiflar |
| 14 | Davranis Kaliplari | Tekrarlayan davranis oruntuleri |
| 15 | Temsilci Yanit Onerisi | Hazir yanit taslagi |

**Musteri faydasi:**
- Her sohbet icin AI destekli satis & hizmet icgorusu
- Temsilciye "su müsteriye boyle yaklas" yonlendirmesi
- Satin alma olasiligini otomatik skorlama
- Rakip ataklarini erken tespit

---

## 3. Appointments (Randevu Yonetimi)

**Ne yapar:** Klinikler icin komple randevu sistemi. Slot yonetimi, rezervasyon, bekleme listesi, tedavi yasam dongusu takibi, no-show istatistikleri.

**Temel Ozellikler:**
- **Slot Yonetimi:** Gun/doktor bazli kapasite tanimla
- **Akilli Bekleme Listesi:** Iptal oldugunda siradaki hastaya otomatik bildirim
- **Tedavi Yasam Dongusu:** Ameliyat oncesi, sonrasi, plan onay takibi
- **No-Show Takibi:** Gelmeme gecmisi olan hastalara ozel yaklasim
- **Otomatik Hatirlatma:** Outbound servis uzerinden WhatsApp hatirlatma
- **Fiyatlandirma:** Hizmet bazli fiyat tanimlama

**Musteri faydasi:**
- Randevu kacirma oranlari duser (otomatik hatirlatma)
- Bos slotlar otomatik dolar (bekleme listesi mekanizmasi)
- Tedavi sureci uctan uca takip edilir
- Hasta sadakati artar (takip mesajlari)

**Entegrasyon:** Outbound'a hatirlatma mesaji gonderir

---

## 4. Knowledge (Bilgi Bankasi & RAG)

**Ne yapar:** Isletmenin bilgi bankasi. SSS'leri, PDF dokumanlari ve intent pattern'lari yonetir. **Vektorel arama** (pgvector) ile en alakali bilgiyi bulur.

**Temel Ozellikler:**
- **SSS Yonetimi:** Soru-cevap cifleri, otomatik embedding olusturma
- **PDF Yukleme:** PDF'leri parcalara ayirir (chunking), aranabilir hale getirir
- **Semantik Arama:** "Bu konuyla en alakali bilgi ne?" sorusuna AI cevabi
- **Sektor Sablonlari:** Dis klinigi, estetik, e-ticaret icin hazir intent seed'leri
- **KVKK Uyumu:** Saglik verilerine ozel etiketleme

**Musteri faydasi:**
- Chatbot dogru cevap verir (bilgi bankasi destekli)
- Temsilciler dogru bilgiye hizla erisir
- PDF'ler otomatik aranabilir hale gelir
- Yeni isletme hizla baslar (sektor sablonlari)

**Entegrasyon:** AgentAI ve Automation tarafindan sorgulanir

---

## 5. AgentAI (Temsilci Asistani)

**Ne yapar:** Canli temsilciye AI destekli **yanit onerisi** uretir. Knowledge'dan bilgi ceker, sohbet gecmisini ozetler, temsilci profilini hesaba katar.

**Temel Ozellikler:**
- **Akilli Yanit Onerisi:** Sohbet baglamina uygun hazir cevap
- **RAG Destekli:** Knowledge'dan ilgili SSS/dokumani bulur, cevaba dahil eder
- **Temsilci Profili:** Geri bildirim gecmisinden temsilcinin tarzini ogrenir
- **Geri Bildirim Dongusu:** Temsilci oneriyi kabul/duzenle/ret etti — AI ogrenir
- **E-ticaret Asistani:** Siparis kartini ceker, eskalasyon notu olusturur
- **KVKK Uyarisi:** Saglik kiracilari icin otomatik uyari

**Musteri faydasi:**
- Temsilci cevap suresi %50+ duser (hazir oneri)
- Tutarli musteri deneyimi (AI ton kontrolu)
- Yeni temsilciler hemen verimli olur
- E-ticaret siparisleri tek tikla goruntulenir

**Entegrasyon:** Knowledge'dan bilgi ceker, Integrations'tan siparis bilgisi alir

---

## 6. Integrations (Dis Entegrasyonlar)

**Ne yapar:** E-ticaret pazaryerleri (Hepsiburada, Trendyol) ve kargo firmalari (Aras, Yurtici) ile entegrasyon. Siparis senkronizasyonu, kargo takibi, yorum uyari sistemi.

**Temel Ozellikler:**
- **Siparis Senkronizasyonu:** 5 dk'da bir pazaryerlerinden siparis ceker
- **Kargo Takibi:** Takip kodundan guncel durum sorgulama
- **Yorum Uyari Sistemi:** Olumsuz yorumlari yakalayip kurtarma sureci baslatir
- **Kurtarma Takibi:** Her olumsuz yorum icin (bekliyor/iletisime gecildi/cozuldu/cozulemedi)
- **Baglanti Testi:** Entegrasyon hesabinin calistigini dogrula

**Musteri faydasi:**
- Siparisler WhatsApp CRM icerisinden goruntulenir
- Kargo durumu sorgulanabilir (temsilci musteri icin kontrol eder)
- Olumsuz yorumlar aninda yakalanir, musteri kaybi onlenir
- Coklu pazaryeri tek noktadan yonetilir

---

## 7. Outbound (Mesaj Gonderim Motoru)

**Ne yapar:** WhatsApp uzerinden toplu ve tetiklemeli mesaj gonderimi. Kampanya yonetimi, ROI takibi, riza yonetimi, KVKK uyumu.

**Temel Ozellikler:**
- **Toplu Gonderim (Broadcast):** Musterilere toplu WhatsApp mesaji (oran sinirli: 30 msg/dk)
- **Tetiklemeli Mesajlar:** Yeni lead, odeme alindi, randevu hatirlatma, iade sureci vb.
- **Kampanya Motoru:** Kampanya olustur → aktiflesir → duraklat, ROI/donusum takibi
- **Riza Yonetimi:** Pazarlama/bildirim/tumu icin musteri rizasi kaydi
- **Opt-Out:** STOP yazan musteri otomatik listeden cikarilir
- **KVKK Silme:** Tek musteri icin tum outbound verisini sil
- **Sablon Yonetimi:** Coklu dil destekli mesaj sablonlari

**Musteri faydasi:**
- Kampanyalarin gercek ROI'si olculur
- KVKK'ya tam uyum (riza, opt-out, silme)
- Otomatik tetiklemeler ile dogru zamanda dogru mesaj
- Iade/degisim sureclerinde proaktif iletisim

**Entegrasyon:** Appointments'tan hatirlatma alir, Main App'e teslimat durumu bildirir

---

## 8. Automation (Chatbot Motoru + Flow Builder)

**Ne yapar:** WhatsApp chatbot'unun beyni. Gelen mesaji alir, gorsel flow tanimi uzerinden isler, AI intent tespiti yapar, SSS eslestirmesi yapar, yaniti gonderir.

**Flow Builder Node Turleri:**
| Node | Aciklama |
|------|----------|
| TriggerStart | Akisin basladigi nokta |
| MessageText | Kullaniciya metin mesaji gonder |
| MessageMenu | Secenekli menu goster |
| LogicCondition | Kosullu dallanma (if/else) |
| LogicSwitch | Coklu kosul dallanmasi (switch/case) |
| ActionDelay | Belirli sure bekle |
| SetVariable | Degisken ata |
| ActionHandoff | Canli temsilciye aktar |
| UtilityNote | Ic not (gorunmez) |
| AiIntent | Claude AI ile niyet tespit et |
| AiFaq | SSS'den otomatik cevap bul |
| ApiCall | Dis API'ye istek gonder |

**Diger Ozellikler:**
- **Simulasyon Motoru:** DB'ye yazmadan akisi test et
- **Coklu Akis:** Kiracı basina birden fazla akis, biri aktif
- **v1→v2 Migrasyon:** Eski config formatini yeni grafa donustur
- **Iade Deflection:** Iade sureci chatbot ile yonetilir, istatistikler tutulur
- **VIP Tespiti:** Yuksek degerli musterileri otomatik isaretle

**Musteri faydasi:**
- Kod yazmadan chatbot tasarla (drag & drop)
- %80+ tekrar soruyu chatbot cozar (temsilci yuku duser)
- AI destekli niyet tespiti (musteri ne istiyor?)
- Iade sureclerini chatbot ile yonet, operasyon maliyeti duser
- Canli test (simulasyon) ile hatasiz yayinla

**Entegrasyon:** Knowledge'dan intent yukler, Main App'e yanit gonderir

---

## 9. WhatsAppAnalytics (Sohbet Analitiği)

**Ne yapar:** Gecmis WhatsApp sohbet verilerini (CSV) yukleyip 7 asamali NLP boru hatti ile analiz eder. Toplu veri icgorusu cikarir.

**7 Asamali Pipeline:**
1. **CSV Okuma** — Dosyayi parse et
2. **Metin Normalizasyonu** — Temizle, standartlastir
3. **Temizleme** — Gereksiz mesajlari filtrele
4. **Thread Gruplama** — Mesajlari konusmalara ayir
5. **Intent Siniflandirma** (Claude AI) — Her mesajin niyetini belirle
6. **SSS Cikarimi** — Tekrar eden sorulari tespit et
7. **Duygu Analizi** (Claude AI) + **Urun Analizi** — Memnuniyet & urun ilgisi

**Sorgulanabilir Ciktilar:**
- Intent dagilimi (en cok sorulan konular)
- Duygu ozeti (olumlu/olumsuz/notr oranlar)
- En cok konusulan urunler
- Fiyat ataklari (rakip fiyat karsilastirmalari)
- SSS kumeleri (bilgi bankasina aktarilabilir)

**Musteri faydasi:**
- "Musterilerimiz en cok ne soruyor?" sorusuna veri bazli cevap
- Hangi urunler en cok konusuluyor, rakip karsilastirmasi
- Olumsuz duygu trendlerini erken yakala
- Cikarilan SSS'leri Knowledge'a aktararak chatbot'u gelistir

---

## 10. Marketing (Pazarlama Motoru)

**Ne yapar:** Saglik & medikal turizm odakli niche pazarlama motoru. Google yorum toplama, hasta yonlendirme programi, medikal turizm lead takibi, olumsuz yorum kurtarma.

**Temel Ozellikler:**
- **Google Yorum Toplama:** Yorum talep olustur → link gonder → yorum geldi (puanla birlikte)
- **Hasta Yonlendirme Programi:** Benzersiz indirim kodu uret, takip et, kullan
- **Medikal Turizm CRM:** Uluslararasi hasta sorgudan tedaviye kadar takip
- **Yorum Kurtarma (Review Rescue):** Risk skorlama + strateji (ozur/indirim/ucretsiz iade/tam iade)
- **Tedavi Katalogu:** TR/EN isimler, EUR fiyatlar, sure/iyilesme bilgisi
- **Coklu Dil AI Yanit:** Uluslararasi hastaya kendi dilinde yanit uret + TR tercumesi

**Musteri faydasi:**
- Google yorum sayisi sistematik olarak artar
- Hasta yonlendirme ile organik buyume (indirim kodlu)
- Uluslararasi hasta potansiyeli (coklu dil destegi)
- Olumsuz yorumlar kurtarilir, itibar korunur
- Tedavi katalogu ile profesyonel fiyatlandirma

---

## Flow Builder (Gorsel Chatbot Tasarimcisi)

**Ne yapar:** Backend icerisinde host edilen React uygulamasi. Surukle-birak ile chatbot akisi tasarlama araci.

**Ozellikler:**
- Drag & drop node ekleme
- 12 farkli node turu (mesaj, menu, kosul, AI, API cagirisi vb.)
- Gercek zamanli validasyon (akis sagligi skoru)
- Simulasyon ile canli test
- v1 → v2 migrasyon destegi

**Musteri faydasi:**
- Teknik bilgi gerektirmez
- Dakikalar icerisinde chatbot olustur
- Test et, hatasiz yayinla
- AI node'lari ile akilli chatbot (sadece menu degil, gercek anlama)

---

## Servisler Arasi Entegrasyon Matrisi

```
                 Backend  ChatAn  Appoint  Knowledge  AgentAI  Integr  Outbound  Autom  WAAnal  Market
Backend            -       →        →         →         →        →        →        →       →       →
ChatAnalysis       ←       -        .         .         .        .        .        .       .       .
Appointments       ←       .        -         .         .        .        →        .       .       .
Knowledge          ←       .        .         -         .        .        .        .       .       .
AgentAI            ←       .        .         →         -        →        .        .       .       .
Integrations       ←       .        .         .         .        -        .        .       .       .
Outbound           ←       .        .         .         .        .        -        .       .       .
Automation         ←       .        .         →         .        .        .        -       .       .
WA Analytics       ←       .        .         .         .        .        .        .       -       .
Marketing          ←       .        .         .         .        .        .        .       .       -

→ = cagirir  |  ← = cagirilir  |  . = dogrudan iletisim yok
```

**Kilit Baglantilar:**
- **Backend → Herkes:** Tek giris noktasi (API Gateway)
- **AgentAI → Knowledge:** Temsilci onerisi icin bilgi bankasi sorgusu
- **AgentAI → Integrations:** Siparis karti cekmek icin
- **Automation → Knowledge:** Chatbot icin intent ve SSS yukleme
- **Appointments → Outbound:** Randevu hatirlatma mesajlari
- **WA Analytics → Knowledge:** Cikarilan SSS'ler import edilebilir

---

## Musteri Yolculugu: Uctan Uca Senaryo

### Senaryo 1: Dis Klinigi

```
1. Hasta WhatsApp'tan mesaj yazar
   → Automation chatbot devreye girer (Flow Builder akisi)
   → AiIntent node ile niyet tespit: "randevu almak istiyor"
   → AiFaq node ile SSS kontrolu (Knowledge'dan)
   → Randevu slotlarini gosterir

2. Hasta randevu alir
   → Appointments servisi slot kapasitesini kontrol eder
   → Onay mesaji gonderilir (Outbound)
   → 24 saat once hatirlatma (Appointments → Outbound)

3. Tedavi sonrasi
   → Treatment Lifecycle takibi baslar
   → Memnuniyet mesaji (Outbound tetiklemesi)
   → Google yorum talep edilir (Marketing)
   → Hasta memnunsa → yorum linki gonderilir
   → Yonlendirme kodu uretilir (Marketing referral)

4. Olumsuz durum varsa
   → Review Rescue devreye girer (Marketing)
   → Risk skoru hesaplanir
   → Uygun strateji secilir (ozur/indirim/ucretsiz tedavi)
```

### Senaryo 2: E-ticaret

```
1. Musteri WhatsApp'tan "siparis nerede?" yazar
   → Automation chatbot devreye girer
   → AiIntent: "kargo takibi"
   → Integrations'tan siparis + kargo bilgisi cekilir
   → Chatbot kargo durumunu iletir

2. Musteri karmasik sorun yasarsa
   → Chatbot canli temsilciye aktarir (ActionHandoff)
   → AgentAI temsilciye yanit onerisi uretir
   → Knowledge'dan urun/politika bilgisi eklenir
   → Temsilci hizla cevap verir

3. Kampanya zamani
   → Outbound ile toplu WhatsApp kampanyasi
   → ROI takibi ile donusum olcumu
   → ChatAnalysis ile sohbet kalitesi analizi

4. Donem sonu analiz
   → WA Analytics ile 3 aylik sohbet verisi yukle
   → En cok sorulan konular, duygu trendi, urun ilgisi
   → Cikarilan SSS'leri Knowledge'a aktar
   → Chatbot akisini optimize et
```

### Senaryo 3: Medikal Turizm

```
1. Yabanci hasta Ingilizce mesaj yazar
   → Automation chatbot AiIntent ile tespit eder
   → Marketing servisi coklu dil AI yaniti uretir
   → Hastanin dilinde yanit + TR tercumesi

2. Lead olarak kaydedilir
   → Marketing tourism CRM'e lead eklenir
   → Durum: inquiry → consultation → booked → treated

3. Tedavi katalogu sunulur
   → TR/EN isimler, EUR fiyatlar
   → Paket detaylari (sure, iyilesme, dahil olan)

4. Tedavi sonrasi
   → Google yorum talep (Marketing)
   → Referral kodu ile yeni hasta kazanimi
```

---

## Teknik Ozet

| Ozellik | Deger |
|---------|-------|
| Toplam Servis | 10 + Flow Builder UI |
| Toplam Endpoint | ~150+ |
| AI Kullanan Servisler | ChatAnalysis, AgentAI, Automation, WA Analytics, Marketing (5/10) |
| AI Modeli | Claude Haiku 4.5 |
| Veritabani | PostgreSQL 16 + pgvector |
| Auth | JWT (paylasimli secret) |
| Multi-tenant | Evet (tum servisler) |
| KVKK Uyumu | Riza yonetimi, opt-out, veri silme |
| Sektor Destegi | Saglik, E-ticaret, Medikal Turizm |

---

## Deger Onerileri (Musteriye Satis Pitchi)

1. **"Temsilcinizin %50 yukunu AI alsin"** — AgentAI + Automation ile tekrar sorulari chatbot cozar, karmasik sorularda temsilciye hazir yanit onerir

2. **"Randevulari kaybetmeyin"** — Appointments + Outbound ile otomatik hatirlatma, bekleme listesi, no-show takibi

3. **"Musterinizi taniyin"** — ChatAnalysis ile her sohbetten 15 farkli icgoru, WA Analytics ile toplu analiz

4. **"Google yorumlarinizi artirin"** — Marketing ile sistematik yorum toplama, olumsuz yorum kurtarma

5. **"Uluslararasi hasta kazanin"** — Marketing medikal turizm modulu ile coklu dil destegi, lead takibi

6. **"Kampanyalarinizin ROI'sini olcun"** — Outbound ile gercek donusum takibi, kampanya bazli analiz

7. **"Kod yazmadan chatbot olusturun"** — Flow Builder ile gorsel tasarim, AI destekli akilli chatbot

8. **"E-ticaretinizi entegre edin"** — Integrations ile Hepsiburada/Trendyol siparis senkronizasyonu, kargo takibi
