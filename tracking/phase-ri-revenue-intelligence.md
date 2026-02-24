# Phase RI: Revenue Intelligence Engine

> **Durum:** IN-PROGRESS | **Baslangic:** 24 Sub 2026 | **POC DB:** WaClientvailaclinic (saglik, 5.7M msg)
> **Amac:** 55M gercek sohbet mesajindan conversion davranisi cikarmak — Revenue Intelligence urunu fizibilitesi
> **GPT-5.2-Pro Feasibility:** 7/10 | Complexity: Medium

## Ozet

91 MSSQL tenant DB'sinde ~55.7M WhatsApp/Instagram mesaji var. Mevcut WhatsAppAnalytics servisi
keyword-based outcome labeling yapiyor ama saglik sektoru icin neredeyse calismiyor (randevu pattern yok).
Bu faz, LLM-based labeling'in yeterli dogrulukta olup olmadigini 7 gunde dogruluyor.

## Q Kararlari (24 Sub 2026)

- **Monetizasyon:** Henuz belirsiz — POC sonucuna gore karar verilecek
- **KVKK / LLM:** Claude + OpenAI API'leri kullanilacak (3rd-party). PII redaction ZORUNLU.
- **Bot ayrimi:** vailaclinic'te bot mesajlari olup olmadigi bilinmiyor — RI-0.1'de kontrol edilecek

## GPT-5.2-Pro Kritik Uyarilari (plani degistiren)

| # | Uyari | Etki | Aksiyon |
|---|-------|------|---------|
| 1 | **KVKK: Saglik mesajlari ozel nitelikli veri** | CRITICAL | PII redaction modulu ZORUNLU (telefon, isim, TCKN, doktor adi). Ham metin LLM'e gitmeyecek — once summary, sonra classify |
| 2 | **Yanlis metrik: accuracy yerine macro-F1** | HIGH | Class imbalance var. Salt accuracy "hepsini no_sale de" ile %60+ tutabilir. Gate'i macro-F1 ile yeniden tanimla |
| 3 | **Taxonomy ONCE tanimlanmali** | HIGH | LLM + Q ayni dili konusmali. Etiketleme guideline'i + 5-7 label (9 fazla) |
| 4 | **Analytics tek basina satilmaz** | MEDIUM | Outcome → aksiyon baglantisi sart (FlowBuilder tetikleri) |
| 5 | **"Revenue Intelligence" ismi TR SMB'de soyut** | LOW | "Randevu/Satis Donusum Analitigi" daha anlasilir |

## Mevcut Altyapi (Sifirdan baslamiyoruz)

| Ozellik | Durum | Dosya |
|---------|-------|-------|
| MSSQL okuma (streaming) | CALISIYOR | MssqlReaderService.cs |
| Threading (conversation gruplama) | CALISIYOR | ThreaderService.cs |
| Outcome labeling (7 tip, regex) | SORUNLU | ThreaderService.cs:30-72 |
| Intent siniflandirma (12 tip) | CALISIYOR | IntentClassifierService.cs |
| Sentiment analizi | CALISIYOR | SentimentAnalyzerService.cs |
| Agent tespiti + first response time | CALISIYOR | ThreaderService.cs:199-215 |

### Mevcut Outcome Labeling Problemleri

| Problem | Etki |
|---------|------|
| `\bkargo\b` her kargo kelimesini yakaliyor | "offered" %52.8'e sisiyor |
| Saglik/dis icin SIFIR pattern | 17M+ mesaj icin outcome = hep no_sale/no_response |
| "tamam gonder" / "aliyorum" yakalanamiyor | Musteri onaylarini kaciyor |
| `appointment_booked` kategorisi YOK | Klinik/dis icin en kritik conversion |

## Task Tracking

| # | Task | Durum | Tarih | Notlar |
|---|------|-------|-------|--------|
| RI-0.1 | Schema Uyumluluk Tarami (5 DB) + Bot tespiti | **DONE** | 24 Sub | 5/5 DB %100 uyumlu. Bot: IsBotUser hep false, ApiMessage+IsTemplateMessage filtre icin mevcut |
| RI-0.2 | Veri Kalitesi Degerlendirmesi (5 DB) | **DONE** | 24 Sub | Ort %68 usable. vailaclinic %79, EbruModa %53, DentAdavista %72, elcitur %73, SisliMYO %62 |
| RI-0.3 | Sektor Haritalama (88 DB) | **DONE** | 24 Sub | 88 tenant DB (5 sistem/test haric). 14 sektor tespit edildi |
| RI-0.4 | Outcome Taxonomy v0.2 (gercek veriye dayali) | **DONE** | 24 Sub | 7 label, sektor-spesifik notlar, etiketleme guideline, sinir durumlari |
| RI-0.5 | PII Redaction kurallari | **DONE** | 24 Sub | 8 PII tipi, regex taslagi, sakla/sil stratejisi |
| RI-1A | LLM Benchmark Altyapisi (DB, DTOs, ILlmClient, GeminiClient, PiiMasker, Repository) | **DONE** | 24 Sub | ~500 satir. arch/plans/20260224-ri-benchmark-tiered.json |
| RI-1B | Benchmark Orchestration + Endpoints (Orchestrator, ProcessingService, OutcomeClassifier, MetricsCalculator, TieredClassifier, 5 API endpoint) | **DONE** | 24 Sub | ~700 satir. 6 model: Haiku, Sonnet, Flash, Pro, 3Flash, Tiered |
| RI-1.1 | Thread Ornekleme (1000 thread) | PLANNED | - | vailaclinic, stratified sample |
| RI-1.2 | Aday Cikarma (manual label seti) | PLANNED | - | active learning: belirsiz thread oncelikli |
| RI-1.3 | Manuel Etiketleme (Q) | PLANNED | - | 50-100 thread, ~2-3 saat Q |
| RI-1.4 | LLM Etiketleme (1000 thread) | PLANNED | - | Benchmark endpoint ile: POST /api/ops/benchmark/start |
| RI-1.5 | Dogruluk Olcumu | PLANNED | - | macro-F1 + confusion matrix via /api/ops/benchmark/{id}/metrics |
| RI-1.6 | Maliyet Modeli | PLANNED | - | Scale: 1K → 5M conv |
| RI-1.7 | Pipeline Karsilastirmasi | PLANNED | - | Keyword vs LLM vs Hybrid vs Tiered |
| GATE | Decision Gate | PLANNED | - | macro-F1 bazli |

## Decision Gate Kriterleri (GUNCELLENDI: macro-F1)

| LLM macro-F1 | Yol | Aksiyon |
|-------------|-----|---------|
| >= 0.80 | A: Revenue Intelligence Urunu | Production pipeline + Revenue UI + monetizasyon |
| 0.60-0.79 | B: Operational Analytics | Keyword regex iyilestir + yeni kategoriler |
| < 0.60 | C: Rethink | Prompt iyilestir / basit metriklere odaklan |

## Outcome Taxonomy v0.2 (Gercek Veriye Dayali, 7 label)

> **GUNCELLENDI 24 Sub:** vailaclinic'ten 60+ gercek mesaj incelenerek dogrulandi.
> Saglik/medical tourism + retail/e-ticaret icin gecerli. Sektor-agnostik tanimlar.

| Label | Tanim | Gercek Sinyal Ornekleri | Mevcut Regex? |
|-------|-------|------------------------|---------------|
| **sale** | Odeme/depozito alindi VEYA siparis kesin onaylandi | "Thank you for the payment", "deposit received", "surgery date is booked", "siparisiniz olusturuldu", "kargo takip no" | EVET (retail regex, saglik EKSIK) |
| **appointment_booked** | Randevu/ameliyat tarihi kesinlesti (odeme bekleniyor olabilir) | "I have reserved your surgery date for April 16th", "randevunuz olusturuldu", "ameliyat tarihiniz 18 Mart" | **HAYIR — KRITIK EKSIK** |
| **offered** | Fiyat/bilgi verildi, musteri henuz karar vermedi | "The total price is €6,500", "BBL procedure is €8,000", "indirim verebilirim", depozito istendi ama gelmedi | EVET (ama `\bkargo\b` sisis) |
| **no_sale** | Musteri acikca vazgecti veya uygun degil | "not interested", "too expensive", "decided not to go ahead", "I am not impressed", doktor reddi | EVET (ama saglik red pattern eksik) |
| **no_response** | Son mesaj ajandan, musteri yanit vermedi | Agent follow-up gonderdi, musteri sessiz. "I haven't heard back from you" | EVET |
| **abandoned** | 1-2 mesaj, gercek etkilesim yok | Tek "merhaba" veya sadece otomatik karsilama | EVET |
| **return_or_complaint** | Iade/sikayet/memnuniyetsizlik (post-hizmet) | "It's my money and the consultation got cancelled!", "bozuk geldi", "iade taleb" | EVET (retail, saglik icin az) |

### Sektor-Spesifik Notlar

**Medical Tourism (vailaclinic tipi):**
- `sale` = depozito odendi (€500 tipik) + ameliyat tarihi kesinlesti. Tam odeme ameliyat gununde.
- `appointment_booked` = ameliyat tarihi verildi ama depozito HENUZ odenmedi. (sale'den farkli!)
- Funnel: Inquiry → Medical form → Dr. approval → Pricing → Deposit → Surgery date
- Drop reasons: too expensive, medical rejection (Dr. not approving), timing, competitor clinic
- Coklu dil: EN/TR/NL/DE/FR karisik — LLM multilingual olmali

**Retail/E-ticaret (EbruModa tipi):**
- `sale` = siparis onaylandi, kargo hazirlik
- `appointment_booked` = KULLANILMAZ (retail'de randevu yok)
- Funnel: Inquiry → Product info → Size/stock → Order → Shipment
- Drop reasons: stok yok, fiyat, beden uyumsuzlugu

**Dis/Dental (DentAdavista tipi):**
- `sale` = tedavi basladi / odeme alindi
- `appointment_booked` = muayene/tedavi randevusu kesinlesti
- Funnel: Inquiry → Treatment info → Pricing → Appointment

### Etiketleme Guideline (Q ve LLM icin)

**Karar agaci (oncelik sirasi):**
1. Odeme/depozito alindiysa → `sale`
2. Tarih/saat kesinlestiyse (odeme olmasa bile) → `appointment_booked`
3. Fiyat/teklif verildiyse ama karar yok → `offered`
4. Musteri acikca "hayir" dediyse → `no_sale`
5. Son mesaj ajandan, musteri sessiz → `no_response`
6. 1-2 mesaj, gercek etkilesim yok → `abandoned`
7. Iade/sikayet varsa → `return_or_complaint`

**Sinir durumlari:**
- Musteri "dusunecegim" dedi → `offered` (henuz red degil)
- Doktor reddetti (saglik nedeni) → `no_sale` (musteri istese de olamaz)
- Depozito istendi ama gelmedi → `offered` (henuz sale degil)
- Ameliyat tarihi "tentative" verildi ama depozito bekleniyor → `appointment_booked` (tarih kesin)
- Musteri baska klinik buldu → `no_sale`

---

## PII Redaction Kurallari (RI-0.5)

> **KVKK CRITICAL:** Saglik mesajlari "ozel nitelikli kisisel veri". LLM'e gitmeden ONCE PII temizlenmeli.

### Tespit Edilen PII Tipleri (gercek vailaclinic verisinden)

| PII Tipi | Ornek | Redaction | Oncelik |
|----------|-------|-----------|---------|
| **Telefon numarasi** | 447729758860, 905015527793 | `[PHONE]` | CRITICAL |
| **Hasta adi** | "Meryem Hanim", "Gulnaz Hanim", "Kay", "Michelle" | `[NAME]` | CRITICAL |
| **Doktor adi** | "Dr. Furkan", "Dr. Furkan Certel" | `[DOCTOR]` | HIGH |
| **TCKN** | 11 haneli sayi | `[TCKN]` | CRITICAL |
| **Odeme linki** | "https://iyzi.link/AJDFSA" | `[PAYMENT_LINK]` | MEDIUM |
| **Banka bilgisi** | IBAN, hesap numarasi, Western Union detaylari | `[BANK_INFO]` | HIGH |
| **Adres** | Otel adresi, klinik adresi | `[ADDRESS]` | MEDIUM |
| **Tibbi bilgi** | "short bowel syndrome", "TPN", "demir ve B12 takviyesi" | **SAKLA** (outcome icin gerekli) | N/A |
| **Fiyat bilgisi** | "€6,500", "£8,700" | **SAKLA** (conversion analizi icin gerekli) | N/A |

### Redaction Stratejisi

**SAKLANACAKLAR (outcome icin gerekli):**
- Fiyatlar, indirimler, odeme tutarlari
- Tibbi bilgiler (tedavi turu, doktor degerlendirmesi)
- Tarihler (randevu, ameliyat)
- Genel lokasyonlar (ulke/sehir — "UK", "Istanbul")

**SILINECEKLER:**
- Telefon numaralari → `[PHONE]`
- Kisi isimleri → `[NAME]`
- Doktor tam isimleri → `[DOCTOR]`
- TCKN/pasaport → `[ID]`
- Banka/odeme detaylari → `[BANK_INFO]`
- Odeme linkleri → `[PAYMENT_LINK]`
- Tam adresler → `[ADDRESS]`
- Email adresleri → `[EMAIL]`

### Regex Taslagi (C# icin)

```
Telefon:  \+?\d{10,15} veya \b\d{3}[\s-]?\d{3}[\s-]?\d{4}\b
TCKN:     \b[1-9]\d{10}\b
Email:    [\w.+-]+@[\w-]+\.[\w.]+
IBAN:     \b[A-Z]{2}\d{2}[\s]?[\dA-Z]{4}[\s]?[\dA-Z]{4}[\s]?[\dA-Z]{4}[\s]?[\dA-Z]{4}[\s]?[\dA-Z]{0,4}\b
URL:      https?://[^\s]+
```

Isim redaction icin NER (Named Entity Recognition) veya basit heuristik:
- "Hanim/Bey" oncesindeki kelime → isim
- "Dr./Doktor" sonrasindaki kelime(ler) → doktor adi
- "Dear/Hi/Hello" sonrasindaki kelime → isim (EN)

> NOT: Production'da NER modeli (spaCy tr/en) daha dogru olur. POC icin regex + heuristik yeterli.

---

## LLM Pipeline (Two-Pass)

```
Pass 1: Thread → PII Redaction → Summary (kisa ozet)
Pass 2: Summary → Outcome Classification (label + confidence + evidence)
```

Bu yaklasim:
- KVKK riskini azaltir (ham metin LLM'e gitmez, sadece ozet)
- Token maliyetini dusurur (~%40)
- Tekrar calistirilabilir (summary cache'lenir)

## Maliyet Tahminleri (Claude Haiku 4.5)

| Olcek | Konusma | Tahmini Maliyet |
|-------|---------|----------------|
| POC | 1,000 | ~$1.50 |
| Tek Tier-1 DB | 170,000 | ~$200 |
| Tum Tier-1 (16 DB) | ~2M | ~$2,500 |
| Tum 91 DB (~5M conv) | ~5M | ~$6,000 |

## Zamanlama

```
GUN 1: RI-0.1 + RI-0.2 (Schema tarami + veri kalitesi)
GUN 2: RI-0.3 + RI-0.4 + RI-0.5 (Sektor harita + taxonomy + PII kurallari)
GUN 3: RI-1.1 + RI-1.2 (Ornekleme + aday cikarma)
GUN 4: RI-1.3 (Manuel etiketleme — Q, ~2-3 saat)
GUN 5: RI-1.4 (LLM etiketleme — two-pass)
GUN 6: RI-1.5 + RI-1.6 + RI-1.7 (Analiz)
GUN 7: DECISION GATE
```

## Risk Kaydi (GUNCELLENDI)

| Risk | Olasilik | Etki | Onlem |
|------|----------|------|-------|
| **KVKK: saglik verisi 3rd-party LLM'e** | YUKSEK | CRITICAL | PII redaction + summary-only pipeline + kisa retention |
| Class imbalance yanlis gate karari | ORTA | YUKSEK | macro-F1 kullan, per-class precision/recall raporla |
| Schema uyumsuzlugu | DUSUK | ORTA | 4 DB zaten dogrulandi (ayni INMA CRM) |
| Label accuracy <0.60 | ORTA | YUKSEK | Prompt iyilestirme, few-shot, Sonnet dene |
| LLM maliyeti yuksek | DUSUK | ORTA | Hybrid (keyword pre-filter + LLM) %30 azaltir |
| vailaclinic sorgu agirligi (5.7M) | ORTA | ORTA | TOP + DATEADD filtresi, NOLOCK |
| Bot/human karisimligi | BILINMIYOR | ORTA | RI-0.1'de kontrol edilecek |

## Mevcut Phase'lerle Iliski

| Phase | Iliski |
|-------|--------|
| Phase 5 (Revenue Agent) | RI veri temeli saglar, Phase 5 "agent" katmani olur |
| Phase 6 (QA + Conv Mining) | GR-6.3 Conv Mining ~ RI-2/3, GR-6.5 Revenue Dashboard ~ RI-5 |
| PKT-7/8/9/10 | Bagimsiz |

## Phase RI-2+ Taslak (Gate A sonrasi)

- RI-2: Journey stage extraction (inquiry → info → price → trust → booking)
- RI-3: Decision point analizi (sektor bazli drop tetikleyicileri)
- RI-4: Agent performansi (lead kalitesi kontrollu — ham conversion rate DEGIL)
- RI-5: Revenue UI (1 sayfa donusum ozeti + 3 insight + FlowBuilder CTA)
- RI-6: Monetizasyon (POC sonucuna gore belirlenecek)

## Detayli Plan

Tam plan: `C:\Users\taner\.claude\plans\linked-jingling-summit.md`

## Bulgular (POC sirasinda guncellenecek)

### RI-0.1 Bulgulari — Schema Uyumluluk (DONE)

**Sonuc: 5/5 DB %100 uyumlu.** Tum kritik kolonlar mevcut:

| Kolon | vailaclinic | EbruModa | DentAdavista | elcitur | SisliMYO |
|-------|:-----------:|:--------:|:------------:|:-------:|:--------:|
| ChatMessages.Body | OK | OK | OK | OK | OK |
| ChatMessages.FromMe | OK | OK | OK | OK | OK |
| ChatMessages.ChatID | OK | OK | OK | OK | OK |
| ChatMessages.MessageType | OK | OK | OK | OK | OK |
| ChatMessages.ApiMessage | OK | OK | OK | OK | OK |
| ChatMessages.IsTemplateMessage | OK | OK | OK | OK | OK |
| ChatMessages.SystemMessageType | OK | OK | OK | OK | OK |
| Chats.CustomerPhoneNumber | OK | OK | OK | OK | OK |
| Chats.InstanceType | OK | OK | OK | OK | OK |
| Chats.IsGroup | OK | OK | OK | OK | OK |
| Users.Name | OK | OK | OK | OK | OK |
| Users.IsBotUser | OK | OK | OK | OK | OK |

**Bot tespiti (vailaclinic):** 55 user, hepsi IsBotUser=false. Ancak "Welcome" user otomasyon/karsilama hesabi olabilir. ApiMessage + IsTemplateMessage kolonlari bot mesaj filtrelemesi icin kullanilabilir.

**ConversationResults:** vailaclinic'te 955 kayit var ama TAMAMI PBX telefon arama loglari (%94 "Ulasilamadi"). WhatsApp outcome ground-truth olarak KULLANILAMAZ.

**Risk guncelleme:** Schema uyumsuzlugu riski DUSUK → COZULDU.

---

### RI-0.2 Bulgulari — Veri Kalitesi (DONE)

| DB | Sektor | Toplam Msg | Usable Text | % Usable | WA Musteri | User Sayisi |
|----|--------|-----------|-------------|----------|------------|-------------|
| vailaclinic | Saglik (Med. Tourism) | 6,750K | 5,360K | **79.4%** | ~92K chat | 55 |
| EbruModa | Moda/Retail | 6,317K | 3,344K | **52.9%** | 173,855 | 10 |
| DentAdavista | Dis | 357K | 256K | **71.7%** | 7,794 | 4 |
| elcitur | Turizm | 594K | 433K | **72.9%** | 38,313 | 6 |
| SisliMYO | Egitim | 598K | 372K | **62.2%** | 31,930 | 19 |

**Ortalama usable: %67.8** (gate: >=%60 — **GECTI**)

**EbruModa dusuk neden?** 2.4M system mesaj (toplamin %38'i) + 2.7M empty body. E-ticaret otomasyon mesajlari yuksek.

**Konusma Uzunlugu Dagilimi:**

| Bucket | EbruModa | DentAdavista | elcitur | SisliMYO |
|--------|----------|-------------|---------|----------|
| 1 msg | 5,664 (3%) | 1,666 (22%) | 2,813 (7%) | 900 (3%) |
| 2-5 msgs | 90,058 (52%) | 2,260 (30%) | 15,382 (41%) | 13,239 (45%) |
| 6-20 msgs | 50,923 (29%) | 1,839 (24%) | 16,283 (43%) | 11,419 (39%) |
| 21-50 msgs | 18,373 (11%) | 1,113 (15%) | 2,246 (6%) | 2,867 (10%) |
| 50+ msgs | 8,152 (5%) | 775 (10%) | 912 (2%) | 895 (3%) |
| **Toplam** | **173,170** | **7,653** | **37,636** | **29,320** |
| **Anlamli (6+ msg)** | **77,448 (45%)** | **3,727 (49%)** | **19,441 (52%)** | **15,181 (52%)** |

**Kritik bulgu:** DentAdavista'da %22 tek mesajli konusma (yuksek abandon). Saglik sektorunde konusmalar daha uzun (vailaclinic'te 50+ msg konusmalar yasik — medical tourism long-cycle).

**vailaclinic ozel bulgular:**
- 35 WhatsApp instance, Instagram YOK
- Uluslararasi hastalar: UK, Hollanda, Almanya, Fransa, Gana, Nijerya
- Medical tourism klinigi (Voila Health Tourism)
- Ingilizce + Turkce karisik konusmalar
- 3 ornek thread incelendi: BBL/meme/karin germe operasyonlari — uzun satis dongusu

---

### RI-0.3 Bulgulari — Sektor Haritalama (DONE)

**Toplam:** 93 DB listelendi. 5 sistem/test DB cikarildi → **88 tenant DB**

Sistem/Test (haric): WaClient.Client, WaClient.Management, WaClientLog, WaClientofficetest, WaClientTxcteste

| Sektor | DB Sayisi | Ornek DB'ler |
|--------|----------|-------------|
| **Saglik/Medikal** | ~11 | vailaclinic, Hermest, erdemhospital, Estethica, trustmed, AlfaTip, Sanovita, lindenclinics, medipol, Menplusclinic, auraliss |
| **Dis** | ~7 | DentAdavista, Dentares, Dentmaks, SMILEPOD, CeyhunAydoganClinic, Yucelerdis, Vivaladent |
| **Sac/Guzellik** | ~4 | ClinicHair, Hairtime, dogalfilem, Beautywithdany |
| **Turizm/Seyahat** | ~7 | elcitur, Capellatour, FlyTo, EthnoHotels, Justtravel, Mysltravel, B2BSeas |
| **Moda/Retail** | ~8 | EbruModa, nevinkayamoda1, SizeOzel, QUstyle, Elodi, Guney, Moreandmore, Altinbas(io) |
| **Gida/F&B** | ~3 | Mutbex, Cafemarkt, Makropoli |
| **Egitim** | ~3 | SisliMYO, sislimyoMali, CinKulturMerkezi |
| **Teknoloji/Dijital** | ~5 | CloudyFlex, TeberDijital, Paragram, Nternetspace, tunusbt |
| **Sigorta** | ~1 | Erhanyazicisigorta |
| **Mimari/Insaat** | ~1 | arkhemimarlik |
| **Etkinlik/Organizasyon** | ~2 | Drum-Party, Fuarara |
| **Lojistik/Kargo** | ~2 | Techcargohub, Logix |
| **Tarim/Hayvancilik** | ~2 | Krishiyug, Dhatifeeds |
| **Diger/Belirsiz** | ~32 | 3x0sx2-zn, Aonedgtalndapvtltd, BKA, Brightnexmarketing, Dive, EmreIlhan, Eppa, glass, GoldenPartner, Inbox, idc, K-Plumbing, Ligarba, Mafabioscience, Maitre, Marinehardwaresyndicate, MLPCM, Mokn, Mytaxicrm, OzakGlobal, Plus963, pst, Rgx, chatin6, SuleymanTas, tcs, Thegolfbuggyguy, Unknwndesignz, vimfay, Wapcrm, Xenom, ymmetal |

**Sektor dagilimi ozet:** Saglik+Dis+Sac = **22 DB (~%25)** → en buyuk ve en cok conversion-relevant segment. Moda/Retail = 8 DB ama yuksek mesaj hacmi. Turizm = 7 DB. "Belirsiz" 32 DB icin sample mesaj okuma gerekli (ayri task).

**RI icin onemli:** POC'u vailaclinic (saglik/med tourism) uzerinde yapiyoruz. Sektor dagilimi, saglik+dis segmentinin buyuk oldugunu dogruluyor — appointment_booked label'i en az 22 DB'de islevsel olacak.

---

### RI-1.5 Accuracy Sonuclari
_Henuz baslamadi_

### Decision Gate Karari
_Henuz belirlenmedi_
