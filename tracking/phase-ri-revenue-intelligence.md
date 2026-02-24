# Phase RI: Revenue Intelligence / Satis Zekasi

> **Durum:** IN-PROGRESS | **Baslangic:** 24 Sub 2026 | **Oncelik:** YUKSEK (ana odak)
> **Amac:** 63M gercek sohbet mesajindan (91 DB, 12+ sektor) Invekto musterileri icin ticari deger cikaracak BASE altyapiyi kurmak.
> **Hedef:** Ayni sektordeki tenant'a "bunlar var, istedigini sec/duzenle" diyebilecek hazir sektor template'leri + 7 insight engine.
> **Monetizasyon:** Premium tier ozellik

## Vizyon

55+ aktif tenant DB'sinde ~63M WhatsApp mesaji var. Su an keyword-based etiketleme kullaniliyor (%24 dogruluk).
LLM-based classification ile %89+ dogruluga ulastik (Benchmark 11).

**Bu sadece etiketleme degil.** Her sohbetten 7 farkli insight cikarilacak:
1. Kayip gelir hesabi (ne kadar para masada kaldi?)
2. Agent performansi (kim iyi satiyor, kim kotu?)
3. Itiraz haritasi (neden almiyorlar?)
4. Cevap suresi korelasyonu (hiz = para)
5. Kurtarilabilir konusmalar (follow-up ile kapanabilecekler)
6. Konusma kalite puani (agent ne kadar iyi iletisim kuruyor?)
7. Hizmet talep haritasi (ne soruyorlar?)

**Base yaklasimi:** Invekto tum sektorleri isleyip ogreniyor → ayni sektordeki yeni tenant'a hazir template sunuyor → tenant istemedigiyle dugurler, istedigini ekler.

## Q Kararlari (24 Sub 2026)

| Karar | Secim |
|-------|-------|
| Ground Truth | **Hibrit:** Q sektor basina 50-100 etiketler, LLM calibrate, tenant agree/disagree ile flywheel |
| Sektor Stratejisi | **Top 3 ile basla:** Saglik (%36), Moda (%16), Gayrimenkul (%11) — geri kalanlar sonra |
| Isleme Sikigi | **Gunluk/haftalik batch** — musteri secer |
| LLM Maliyet Limiti | Simdilik yok, ileride belirlenecek |
| Agent Tanima | API'den cekilecek (Users tablosu) |
| Follow-up Rescue | Gece islenir, sabah raporlanir |
| Pipeline | Sektor-spesifik (her sektorde farkli label seti + farkli extraction prompt) |

## Sektor Haritasi (Gercek Veri - 24 Sub 2026)

| Sektor | Mesaj Hacmi | % | Aktif DB | Baslangic Pilotu |
|--------|------------|---|----------|------------------|
| **Saglik/Klinik** | ~23.5M | 36% | 11 | EVET (Faz 2) |
| **Moda/E-ticaret** | ~10.5M | 16% | 5 | EVET (Faz 2) |
| **Gayrimenkul** | ~7.2M | 11% | 3 | EVET (Faz 2) |
| Dijital Pazarlama | ~6.3M | 10% | 2 | Faz 4 |
| Guzellik/Estetik | ~4.2M | 7% | 2 | Faz 4 |
| Finans/Sigorta | ~2.9M | 4% | 1 | Faz 4 |
| Turizm/Seyahat | ~2.1M | 3% | 4 | Faz 4 |
| Egitim | ~1.4M | 2% | 2 | Faz 4 |
| Dis | ~1.4M | 2% | 5 | Faz 4 |
| Lojistik | ~1.2M | 2% | 1 | Faz 4 |
| Diger | ~4.5M | 7% | ~15 | Faz 5 |
| **TOPLAM** | **~63M** | | **~55** | |

**Top 5 Tenant (mesaj hacmi):** Hermest 7.4M, VailaClinic 6.7M, EbruModa 6.3M, GoldenPartner 6.1M, Paragram 5.4M

---

## FAZ PLANI

### Faz 1: Model Secimi & Kalibrasyon (DEVAM EDIYOR)

**Amac:** Hangi LLM en dogru + en ucuz? macro-F1 >= 0.80 gate.

| # | Task | Durum | Notlar |
|---|------|-------|--------|
| RI-0.1 | Schema Uyumluluk Tarami | **DONE** | 5/5 DB %100 uyumlu |
| RI-0.2 | Veri Kalitesi Degerlendirmesi | **DONE** | Ort %68 usable |
| RI-0.3 | Sektor Haritalama | **DONE** | 88 tenant DB, 14 sektor |
| RI-0.4 | Outcome Taxonomy v0.2 | **DONE** | 7 label, sektor-spesifik notlar |
| RI-0.5 | PII Redaction kurallari | **DONE** | 8 PII tipi tanimli |
| RI-1A | LLM Benchmark Altyapisi | **DONE** | DB, DTOs, clients, PiiMasker |
| RI-1B | Benchmark Orchestration + Endpoints | **DONE** | 5 API endpoint, 6 model |
| RI-1.1 | 200 Thread Benchmark (4 model) | **IN-PROGRESS** | Benchmark #12 calisiyor |
| RI-1.2 | Q Manuel Etiketleme (Ground Truth) | PLANNED | Benchmark sonuclari gelince 200 thread Q etiketler |
| RI-1.3 | Dogruluk Olcumu | PLANNED | macro-F1 + confusion matrix |
| RI-1.4 | Maliyet Modeli | PLANNED | Model basina cost/thread |
| RI-1.5 | Pipeline Karsilastirmasi | PLANNED | 4 model karsilastirma raporu |
| **GATE** | **Decision Gate** | PLANNED | macro-F1 >= 0.80 → Faz 2'ye gec |

**Beklenen cikti:** Kazanan model + maliyet tahmini + accuracy raporu

---

### Faz 2: Sektor Pipeline Gelistirme (Top 3)

**Amac:** Saglik, Moda, Gayrimenkul icin sektor-spesifik extraction pipeline'lari kurmak.

| # | Task | Durum | Notlar |
|---|------|-------|--------|
| RI-2.1 | Sektor-spesifik taxonomy tanimlama | PLANNED | Her sektor icin label seti + karar agaci |
| RI-2.2 | Sektor-spesifik LLM prompt'lari | PLANNED | 3 sektor x optimized prompt |
| RI-2.3 | Pilot: Saglik (vailaclinic + 2 DB daha) | PLANNED | ~500 thread, Q etiketleme + LLM |
| RI-2.4 | Pilot: Moda (EbruModa + 2 DB daha) | PLANNED | ~500 thread |
| RI-2.5 | Pilot: Gayrimenkul (GoldenPartner + 1 DB) | PLANNED | ~500 thread |
| RI-2.6 | Cross-sector accuracy raporu | PLANNED | macro-F1 per sector |
| RI-2.7 | Sektor template sistemi tasarimi | PLANNED | Template = label set + prompt + extraction config |

**Beklenen cikti:** 3 sektor icin calibrated pipeline + template yapisi

---

### Faz 3: 7 Insight Engine

**Amac:** Etiketleme ustune 7 extraction engine kurmak. Her biri bagimsiz calisir.

| # | Engine | Aciklama | Karmasiklik | LLM? |
|---|--------|----------|-------------|------|
| RI-3.1 | **Response Time Correlation** | Ilk mesaj → ilk cevap suresi vs conversion | **Low** | Hayir (pure timestamp) |
| RI-3.2 | **Service Demand Heatmap** | Hangi hizmet/urun ne kadar soruluyor | Low-Med | Evet (extraction) |
| RI-3.3 | **Agent Leaderboard** | Agent bazli conversion rate, response time, ghost rate | Low-Med | Hayir (aggregation) |
| RI-3.4 | **Lost Revenue Calculator** | Offered konusmalardan tutar extraction + toplam kayip | Medium | Evet (price extraction) |
| RI-3.5 | **Objection Map** | Neden almiyorlar? Sebep dagilimi | Medium | Evet (reason extraction) |
| RI-3.6 | **Follow-up Rescue Alerts** | Offered + 48 saat cevapsiz → rescue listesi | Medium | Hayir (timestamp + label) |
| RI-3.7 | **Conversation Quality Score** | Agent iletisim kalitesi 1-10 puan | Medium | Evet (scoring) |

**Siralama:** LLM gerektirmeyenler once (3.1, 3.3, 3.6), sonra LLM gerektirenler (3.2, 3.4, 3.5, 3.7)

**Onemli:** RI-3.2 + RI-3.4 + RI-3.5 ayni LLM call'da cikarilabilir (tek prompt, multi-extraction). Maliyet optimizasyonu.

**Beklenen cikti:** 7 engine calisiyor, her biri sektor-agnostik ama sektor template'ine gore configure edilebilir

---

### Faz 4: Bulk Sektor Isleme + Kalan Sektorler

**Amac:** Top 3 sektor icin tum verileri isle + kalan sektorleri ekle.

| # | Task | Durum | Notlar |
|---|------|-------|--------|
| RI-4.1 | Saglik tum DB'leri isleme (~23.5M msg) | PLANNED | 11 DB, batch pipeline |
| RI-4.2 | Moda tum DB'leri isleme (~10.5M msg) | PLANNED | 5 DB |
| RI-4.3 | Gayrimenkul tum DB'leri isleme (~7.2M msg) | PLANNED | 3 DB |
| RI-4.4 | Kalan sektorler taxonomy + prompt | PLANNED | Guzellik, Turizm, Egitim, Dis, vd. |
| RI-4.5 | Kalan sektorler pilot + isleme | PLANNED | Oncelik: hacim sirasina gore |
| RI-4.6 | Sektor profil raporlari | PLANNED | Her sektor icin "bu sektorde su oluyor" ozeti |

**Beklenen cikti:** 63M mesaj islenmis, sektor bazli profiller hazir

---

### Faz 5: Tenant Self-Service + Dashboard

**Amac:** Tenant kendi verilerini gorsun, template'i ozellestirsin, agree/disagree ile ground truth versin.

| # | Task | Durum | Notlar |
|---|------|-------|--------|
| RI-5.1 | Dashboard widget'lari tasarimi | PLANNED | 7 engine icin 7+ widget |
| RI-5.2 | Lost Revenue widget | PLANNED | Buyuk kirmizi sayi: "Bu ay €X kapanmadi" |
| RI-5.3 | Agent Leaderboard widget | PLANNED | Ranking tablosu + trend |
| RI-5.4 | Objection Map widget | PLANNED | Pie chart: kayip sebepleri |
| RI-5.5 | Response Time widget | PLANNED | Korelasyon grafigi + SLA |
| RI-5.6 | Rescue Alerts widget | PLANNED | Sabah raporu: "X konusma rescue bekliyor" |
| RI-5.7 | Quality Score widget | PLANNED | Agent bazli kalite puani |
| RI-5.8 | Service Demand widget | PLANNED | Heatmap: hizmet talep dagilimi |
| RI-5.9 | Tenant template yonetimi UI | PLANNED | Label ekleme/cikarma, prompt duzenleme |
| RI-5.10 | Agree/disagree UI (ground truth flywheel) | PLANNED | "Bu etiket dogru mu?" butonu |
| RI-5.11 | API endpoints (tenant-facing) | PLANNED | Tum widget verileri + export |
| RI-5.12 | Gunluk/haftalik batch pipeline | PLANNED | Cron job, tenant bazi, configurable |

**Beklenen cikti:** Premium tier olarak canli urun

---

### Faz 6: Optimizasyon & Olcekleme

**Amac:** Maliyet dusurme, accuracy artirma, yeni sektorler.

| # | Task | Durum | Notlar |
|---|------|-------|--------|
| RI-6.1 | Flywheel feedback → model iyilestirme | PLANNED | Tenant agree/disagree → prompt tuning |
| RI-6.2 | Maliyet optimizasyonu | PLANNED | Hybrid: keyword pre-filter + LLM (sadece belirsizler) |
| RI-6.3 | Yeni sektor onboarding sureci | PLANNED | Self-serve sektor ekleme |
| RI-6.4 | FlowBuilder entegrasyonu | PLANNED | Insight → otomatik aksiyon tetikleme |

---

## Mevcut Altyapi (Sifirdan baslamiyoruz)

| Ozellik | Durum | Dosya |
|---------|-------|-------|
| MSSQL okuma (streaming) | CALISIYOR | MssqlReaderService.cs |
| Threading (conversation gruplama) | CALISIYOR | ThreaderService.cs |
| Outcome labeling (7 tip, regex) | SORUNLU (%24) | ThreaderService.cs:30-72 |
| Intent siniflandirma (12 tip) | CALISIYOR | IntentClassifierService.cs |
| Sentiment analizi | CALISIYOR | SentimentAnalyzerService.cs |
| Agent tespiti + first response time | CALISIYOR | ThreaderService.cs:199-215 |
| LLM Benchmark sistemi | CALISIYOR | Services/Benchmark/*.cs |
| PII Masker | CALISIYOR | Services/Benchmark/PiiMasker.cs |
| Gemini + Claude client | CALISIYOR | Services/Benchmark/GeminiLlmClient.cs, AnthropicLlmClient.cs |

## GPT-5.2-Pro Kritik Uyarilari

| # | Uyari | Etki | Aksiyon |
|---|-------|------|---------|
| 1 | **KVKK: Saglik mesajlari ozel nitelikli veri** | CRITICAL | PII redaction ZORUNLU |
| 2 | **macro-F1 kullan, accuracy degil** | HIGH | Class imbalance var |
| 3 | **Taxonomy ONCE tanimlanmali** | HIGH | Sektor bazli taxonomy Faz 2'de |
| 4 | **Analytics tek basina satilmaz** | MEDIUM | FlowBuilder entegrasyonu Faz 6'da |
| 5 | **"Revenue Intelligence" TR'de soyut** | LOW | "Satis Zekasi" kullanilacak |

## Maliyet Tahminleri

| Olcek | Konusma | Tahmini Maliyet (Haiku/Flash) |
|-------|---------|-------------------------------|
| POC (Faz 1) | 1,000 | ~$1.50 |
| Pilot (Faz 2) | ~1,500 | ~$3 |
| Tek buyuk DB | ~170,000 | ~$200 |
| Top 3 sektor | ~3M | ~$3,500 |
| Tum 55 aktif DB | ~5M+ conv | ~$6,000 |

## Risk Kaydi

| Risk | Olasilik | Etki | Onlem |
|------|----------|------|-------|
| KVKK: saglik verisi 3rd-party LLM'e | YUKSEK | CRITICAL | PII redaction + summary-only pipeline |
| Class imbalance yanlis gate | ORTA | YUKSEK | macro-F1 kullan |
| Sektor arasi prompt transfer basarisiz | ORTA | YUKSEK | Her sektor icin ayri prompt + pilot |
| LLM maliyeti olceklenmiyor | ORTA | ORTA | Hybrid pre-filter + batch |
| Tenant template karmasikligi | DUSUK | ORTA | Basit UI, sane defaults |

## Outcome Taxonomy v0.2 (Saglik sektoru icin dogrulandi)

| Label | Tanim | Sektor Notu |
|-------|-------|-------------|
| **sale** | Odeme/depozito alindi veya siparis onaylandi | Saglik: depozito. Moda: siparis. Gayrimenkul: kaparo |
| **appointment_booked** | Randevu/gorusme tarihi kesinlesti | Saglik: ameliyat. Moda: YOK. Gayrimenkul: gosterim |
| **offered** | Fiyat/teklif verildi, karar yok | Evrensel |
| **no_sale** | Musteri acikca vazgecti | Evrensel |
| **no_response** | Musteri cevap vermedi | Evrensel |
| **abandoned** | 1-2 mesaj, etkilesim yok | Evrensel |
| **return_or_complaint** | Iade/sikayet | Saglik: memnuniyetsizlik. Moda: iade |

## PII Redaction (RI-0.5 — DONE)

8 PII tipi tanimli, regex + heuristik. Fiyat ve tibbi bilgi SAKLANIR (analiz icin gerekli).
Detay: Yukaridaki PII section'da.

## Benchmark Gecmisi

| # | Tarih | Config | Sonuc |
|---|-------|--------|-------|
| 11 | 24 Sub | 10 thread, tiered-only, vailaclinic | DONE — tiered %95 confidence, nuansli ayrim |
| 12 | 24 Sub | 200 thread, 4 model, vailaclinic | IN-PROGRESS |

## Detayli Plan

Tam plan: `arch/plans/` altinda olusturulacak (her faz icin ayri JSON)
