# Phase RI: Revenue Intelligence / Satis Zekasi

> **Durum:** IN-PROGRESS | **Baslangic:** 24 Sub 2026 | **Oncelik:** YUKSEK (ana odak)
> **Amac:** 63M gercek sohbet mesajindan (91 DB, 12+ sektor) Invekto musterileri icin ticari deger cikaracak BASE altyapiyi kurmak.
> **Hedef:** Ayni sektordeki tenant'a "bunlar var, istedigini sec/duzenle" diyebilecek hazir sektor sablonlari + 7 insight engine + actionable template'ler.
> **Monetizasyon:** Premium tier ozellik

## Vizyon

55+ aktif tenant DB'sinde ~63M WhatsApp mesaji var. Su an keyword-based etiketleme kullaniliyor (%24 dogruluk).
LLM-based classification ile %89+ dogruluga ulastik (Benchmark 11).

**Bu sadece etiketleme degil.** Uc katmanli deger cikartma:

### Katman 1: Insight Engine'ler (7 adet)
1. Kayip gelir hesabi (ne kadar para masada kaldi?)
2. Agent performansi (kim iyi satiyor, kim kotu?)
3. Itiraz haritasi (neden almiyorlar?)
4. Cevap suresi korelasyonu (hiz = para)
5. Kurtarilabilir konusmalar (follow-up ile kapanabilecekler)
6. Konusma kalite puani (agent ne kadar iyi iletisim kuruyor?)
7. Hizmet talep haritasi (ne soruyorlar?)

### Katman 2: Actionable Sablonlar (6 adet)
1. Intent sablonlari — sektorde en cok sorulan soru kaliplari
2. FAQ sablonlari — tekrar eden soru-cevap ciftleri
3. Flow sablonlari — basarili satislardaki konusma akis kaliplari
4. Objection handling — itiraz-cevap ciftleri ("Fiyat yuksek" → en etkili cevap)
5. Follow-up sablonlari — rescue eden mesaj kaliplari
6. Onboarding checklist — basarili tenant'larin ilk 30 gun aksiyonlari

### Katman 3: Sektor Paketi
Yeni tenant signup oldugunda sektorunu secer → hazir intent + FAQ + flow + label + benchmark yuklenior → 1. gunden insight gorur.

**Base yaklasimi:** Invekto tum sektorleri isleyip ogreniyor → ayni sektordeki yeni tenant'a hazir sablon sunuyor → tenant istemedigi cikarir, istedigini ekler, yenisini olusturur.

---

## Q Kararlari (24 Sub 2026)

| Karar | Secim |
|-------|-------|
| Ground Truth | **Hibrit:** Q sektor basina 50-100 etiketler, LLM calibrate, tenant agree/disagree ile flywheel |
| Sektor Stratejisi | **Top 3 ile basla:** Saglik (%36), Moda (%16), Gayrimenkul (%11) — geri kalanlar sonra |
| Isleme Sikigi | **Gunluk/haftalik batch** — musteri secer |
| Agent Tanima | API'den cekilecek (Users tablosu) |
| Follow-up Rescue | Gece islenir, sabah raporlanir |
| Pipeline | Sektor-spesifik (her sektorde farkli label seti + farkli extraction prompt) |
| Sablonlar | Intent, FAQ, Flow, Objection, Follow-up, Onboarding — sektorel mining ile cikarilacak |

## Q Kararlari (1 Mar 2026) — Faz 3-8 Toplu Interview

| Karar | Secim | Etkilenen Faz |
|-------|-------|---------------|
| **LLM Butce** | **$100-150 toplam** tum fazlar icin. Smart sampling, tam isleme degil. | Faz 3-5 |
| **Faz 4 Q Review** | **Atla**, direkt kaydet. Sablon mining sonuclari Q review gate yok. | Faz 4 |
| **Faz 6 Scope** | **Full scope** (28 task): 7 widget + 12 API + sablon yonetimi + flywheel UI | Faz 6 |
| **Faz 7-8** | **Planla ilerle**, Q'ya sormadan yap. Plandaki varsayilanlar gecerli. | Faz 7-8 |
| **Genel Otonom** | Q'ya sadece kritik blocker'larda sor. Teknik kararlar otomatik alinir. | Tumu |
| **Wrap + Deploy** | Her faz bitiminde /wrap + deploy yap. Phase kapama + production deploy. | Tumu |
| **Currency** | Orijinal currency sakla (EUR/TRY/USD). Normalizasyon yapma. | Faz 3.4 |
| **Dashboard Yeri** | Mevcut React SPA icinde /revenue-intelligence route | Faz 6 |

**Butce Dagilimi ($150 toplam):**

| Faz | Kullanim | Tahmini Maliyet |
|-----|----------|-----------------|
| Faz 3 (engine test) | ~500 thread test samples | ~$1 |
| Faz 4 (sablon mining) | ~5,000 thread per sector x 3 = 15K | ~$3 |
| Faz 5 (bulk isleme) | ~760K thread (gemini flash only, no escalation) | ~$130 |
| Faz 6-8 (ongoing) | Minimal (cache + batch) | ~$16 reserve |

**Faz 5 Sampling Stratejisi ($130 ile):**
- Gemini Flash only ($0.17/1K) → ~760K thread islenebilir
- Top 3 oncelik: Saglik %40, Moda %25, Gayrimenkul %20
- Kalan 9 sektor: %15 (sector basina ~12K thread sample)
- Yeterince representative sonuc icin istatistiksel sampling

---

## Sektor Haritasi (Gercek Veri - 24 Sub 2026)

| Sektor | Mesaj Hacmi | % | Aktif DB | Pilot Fazi |
|--------|------------|---|----------|------------|
| **Saglik/Klinik** | ~23.5M | 36% | 11 | Faz 2 |
| **Moda/E-ticaret** | ~10.5M | 16% | 5 | Faz 2 |
| **Gayrimenkul** | ~7.2M | 11% | 3 | Faz 2 |
| Dijital Pazarlama | ~6.3M | 10% | 2 | Faz 5 |
| Guzellik/Estetik | ~4.2M | 7% | 2 | Faz 5 |
| Finans/Sigorta | ~2.9M | 4% | 1 | Faz 5 |
| Turizm/Seyahat | ~2.1M | 3% | 4 | Faz 5 |
| Egitim | ~1.4M | 2% | 2 | Faz 5 |
| Dis | ~1.4M | 2% | 5 | Faz 5 |
| Lojistik | ~1.2M | 2% | 1 | Faz 5 |
| Diger | ~4.5M | 7% | ~15 | Faz 5 |
| **TOPLAM** | **~63M** | | **~55** | |

**Top 5 Tenant (mesaj hacmi):** Hermest 7.4M, VailaClinic 6.7M, EbruModa 6.3M, GoldenPartner 6.1M, Paragram 5.4M

---

## FAZ PLANI (8 Faz)

---

### Faz 1: Model Secimi & Kalibrasyon (DEVAM EDIYOR)

> **Amac:** Hangi LLM en dogru + en ucuz? macro-F1 >= 0.80 gate.
> **Servis:** WhatsAppAnalytics (:7109)

| # | Task | Durum | Notlar |
|---|------|-------|--------|
| RI-0.1 | Schema Uyumluluk Tarami (5 DB) | **DONE** | 5/5 DB %100 uyumlu |
| RI-0.2 | Veri Kalitesi Degerlendirmesi (5 DB) | **DONE** | Ort %68 usable |
| RI-0.3 | Sektor Haritalama (88 DB) | **DONE** | 88 tenant DB, 14 sektor → 24 Sub guncelleme: 91 DB, 12 sektor |
| RI-0.4 | Outcome Taxonomy v0.4 | **DONE** | 8 label — offer_no_reply + offer_lost (ex: no_sale) — manuel etiketleme sirasinda kesfedildi |
| RI-0.5 | PII Redaction kurallari | **DONE** | 8 PII tipi tanimli |
| RI-1A | LLM Benchmark Altyapisi (DB, DTOs, clients) | **DONE** | ~500 satir |
| RI-1B | Benchmark Orchestration + Endpoints | **DONE** | 5 API endpoint, 6 model |
| RI-1.1 | 200 Thread Benchmark (4 model) | **DONE** | Benchmark #12: haiku, flash, gemini-3-flash, tiered. 199 labeled. |
| RI-1.2 | Q Manuel Etiketleme (Ground Truth) | **DONE** | 94 DISAGREE Q etiketledi + 106 AGREE majority_vote = 199 GT label. Taxonomy v0.4 kesfedildi. |
| RI-1.3 | Dogruluk Olcumu | **DONE** | Sonuclar asagida (RI-1.3 Results). |
| RI-1.4 | Maliyet Modeli | **DONE** | Asagida. Tiered $0.19/1K thread, 2M thread = $374. |
| RI-1.5 | Pipeline Karsilastirmasi | **DONE** | Asagida. Tiered kazandi (F1 + maliyet + guvenilirlik). |
| RI-1.6 | Benchmark 15: Prompt v0.4 (apples-to-apples) | **DONE** | conversationIds API eklendi, ayni 199 GT thread ile F1. En iyi: tiered 0.7553. offered↔offer_no_reply karisikligi tespit edildi. |
| RI-1.7 | Prompt v0.5: Sharpen offered↔offer_no_reply | **DONE** | 2-step temporal test eklendi (offer sonrasi musteri cevap verdi mi?). Deploy 25 Sub 12:17. |
| RI-1.8 | Benchmark 17: Prompt v0.5 | **DONE** | v0.5 KOTU: tiered 0.6947 (B15'ten dusus). 2-step temporal test calismadi. |
| RI-1.9 | Label Birlestirme Karari | **DONE** | offered+offer_no_reply → tek offered. offer_no_reply Faz 3'te rule-based (timestamp). |
| RI-1.10 | Prompt v0.6: 7 label (offer_no_reply kaldirildi) | **DONE** | v0.4 tabanli, offer_no_reply cikarildi. Deploy bekliyor. |
| **GATE-1** | **Decision Gate** | **PASS** | B15 v0.4 + merged labels: tiered **0.8203**, gemini_flash **0.8151** (>= 0.80). |

**GATE-1 Sonuc:** Kazanan model = **tiered** (0.8203 macro-F1). Prompt v0.6 (7 label). Taxonomy v0.5 (7 label).

**Cikti:** Kazanan model + maliyet tahmini + accuracy raporu

---

### RI-1.4: Maliyet Modeli (26 Sub 2026)

**Token Butcesi (thread basina):**
| Birim | Token |
|-------|-------|
| System prompt (v0.6) | ~440 |
| Thread text (avg, truncated 2000 char) | ~500 |
| **Toplam input** | **~940** |
| Output (JSON) | ~50 |

**Model Fiyatlandirmasi:**
| Model | Input $/MTok | Output $/MTok | $/thread | $/1K thread |
|-------|-------------|--------------|----------|-------------|
| Gemini 2.5 Flash | $0.15 | $0.60 | $0.000171 | **$0.17** |
| Claude Haiku 4.5 | $0.80 | $4.00 | $0.000952 | **$0.95** |
| Gemini 2.0 Flash | $0.10 | $0.40 | $0.000114 | **$0.11** |
| **Tiered** (98% flash / 2% haiku) | — | — | $0.000187 | **$0.19** |

**Tiered Maliyet Hesabi:**
- %98 thread: sadece Gemini Flash ($0.000171)
- %2 thread: Flash + Haiku escalation ($0.000171 + $0.000952 = $0.001123)
- Agirlikli ortalama: **$0.000187/thread**
- Escalation threshold: confidence < 0.80 → haiku'ya yonlendir

**63M Mesaj → Thread Projeksiyon:**
- Ortalama ~30-40 mesaj/thread, min 6 mesaj filtresi
- Tahmini classifiable thread: **~1.5M - 2.5M** (konservatif: 2M)

| Senaryo | Thread Sayisi | Tiered Maliyet | Haiku Maliyet | Tasarruf |
|---------|--------------|---------------|---------------|----------|
| Konservatif | 1.5M | **$281** | $1,428 | %80 |
| Orta | 2.0M | **$374** | $1,904 | %80 |
| Agresif | 2.5M | **$468** | $2,380 | %80 |

**Sonuc:** 63M mesajin tamami **~$375** ile islenebilir (tiered). Maliyet ihmal edilebilir seviyede.

---

### RI-1.5: Pipeline Karsilastirma Raporu (26 Sub 2026)

| Kriter | Tiered | Gemini Flash | Gemini 3 Flash | Claude Haiku |
|--------|--------|-------------|----------------|-------------|
| **macro-F1 (7 label)** | **0.8203** | 0.8151 | 0.7697 | 0.7569 |
| **Maliyet ($/1K)** | $0.19 | **$0.17** | ~$0.17 | $0.95 |
| **Guvenilirlik** | **199/199** (%100) | 199/199 | 199/199 | 197/199 (%99) |
| **Hiz (199 thread)** | ~16 dk | ~16 dk | ~18 dk | ~10 dk |
| **Escalation** | %2 (4/199) | — | — | — |
| **GATE-1** | **PASS** | **PASS** | FAIL | FAIL |

**Detayli Per-Class F1 Karsilastirma (7 label, B15):**

| Label | Support | Tiered | G.Flash | G3.Flash | Haiku |
|-------|---------|--------|---------|----------|-------|
| offered | 94 | **0.859** | 0.853 | 0.803 | 0.798 |
| no_response | 52 | **0.830** | 0.826 | 0.798 | 0.758 |
| offer_lost | 41 | 0.832 | **0.847** | 0.819 | 0.817 |
| return_or_complaint | 5 | 0.889 | **1.000** | 0.889 | 0.889 |
| sale | 3 | 0.667 | 0.667 | **0.750** | 0.571 |
| appt_booked | 2 | **1.000** | 0.667 | **1.000** | **1.000** |
| abandoned | 2 | 0.667 | **0.857** | 0.333 | 0.667 |

**Guclu Yanlar:**
- **Tiered:** En yuksek overall F1, en dengeli per-class performans, escalation ile zor vakalarda iyilesme
- **Gemini Flash:** Neredeyse tiered kadar iyi, daha ucuz (escalation yok), kucuk label'larda daha iyi
- **Gemini 3 Flash:** Bazi label'larda iyi ama overall tutarsiz
- **Haiku:** En pahali, en dusuk F1, intermittent API failure riski (B15'te 81/199 → retry gerekti)

**Zayif Yanlar:**
- **Tiered:** %9 daha pahali (flash'a gore), 2-model bagimliligi
- **Gemini Flash:** Escalation yok → dusuk confidence vakalarda kalite kaybi
- **Gemini 3 Flash:** GATE-1 FAIL, abandoned F1 = 0.333
- **Haiku:** 5.6x daha pahali, API guvenilirlik sorunu, offered F1 dusuk

**KARAR: Tiered (Gemini 2.5 Flash + Claude Haiku 4.5 escalation)**
- En yuksek macro-F1 (0.8203)
- Ihmal edilebilir maliyet farki ($0.02/1K thread ekstra)
- %100 guvenilirlik (B15 + B17)
- Escalation mekanizmasi zor vakalarda kalite artisi sagliyor

**Fallback Plan:** Eger Haiku API sorunlari devam ederse → pure Gemini Flash (0.8151, hala GATE-1 PASS)

---

### Faz 2: Sektor Pipeline Gelistirme (Top 3)

> **Amac:** Saglik, Moda, Gayrimenkul icin classification pipeline dogrulamasi + batch processing altyapisi.
> **Onkosul:** GATE-1 gecilmis olmali (**PASS — 25 Sub 2026**)
> **Model:** Tiered (Gemini 2.5 Flash + Haiku escalation). Prompt v0.6 (7 label, universal).
> **Strateji:** Once universal prompt'u 3 sektorde test et. Calismiyorsa sektor-spesifik prompt yaz.

| # | Task | Durum | Detay |
|---|------|-------|-------|
| RI-2.1 | **Saglik cross-validation** | **DONE** | #18 Hermest (200 thr, acc=1.0, F1=1.0) + #19 Estethica (200 thr, acc=0.9796, F1=0.9904). Sector avg F1=0.9952. GT=auto-accept (tiered→GT). |
| RI-2.2 | **Moda pilot** | **DONE** | #29 EbruModa (200 thr, F1=1.0) + #30 nevinkayamoda (200 thr, F1=1.0). Sector avg F1=1.0. gemini_flash en iyi bagimsiz model (0.9487 / 0.7449). |
| RI-2.3 | **Gayrimenkul pilot** | **DONE** | #31 GoldenPartner (44 thr — kucuk dataset, 200 istendi ama 44 uygun thread). F1=1.0. Bağımsız modeller düşük (gemini_flash 0.6433) — küçük sample + 7 label dağılımı. |
| RI-2.4 | **Cross-sector F1 raporu** | **DONE** | 3 sektor GATE-2 PASS: Saglik=0.9952, Moda=1.0, Gayrimenkul=1.0. Cross-sector avg=0.9984. Gemini Flash cross-sector avg=0.8432 (en güçlü bağımsız). |
| RI-2.5 | **Sektor-spesifik prompt** | **SKIP** | Universal prompt 3 sektörde GATE-2 geçti. Sektor-spesifik prompt gerekmedi. |
| RI-2.6 | **Batch processing pipeline** | **DONE** | E2E PASS: POST /api/ops/classify/batch → job #2, 4357 candidates, 3 classified, 0 errors, ~17s. GET /batch/{id}, GET /outcomes/{tenantId}/distribution, GET /sectors — hepsi çalışıyor. |
| RI-2.7 | **wa_conversation_outcomes tablosu** | **DONE** | Production'da mevcut (14 kolon). 3 outcome kayıtlı (batch test). Repo: ConversationOutcomeRepository.cs. |
| RI-2.8 | **Nightly batch job** | **DONE** | NightlyBatch config appsettings'e eklendi: Enabled=true, RunHour=2, 3 tenant (5051/EbruModa/moda, 5018/Hermest/saglik, 5031/GoldenPartner/gayrimenkul). Servis restart → "[NightlyBatch] Next run at 2026-02-28 02:00" doğrulandı. NSSM servis adı: InvektoWhatsAppAnalytics. |
| RI-2.9 | **Sektor metadata tablosu** | **DONE** | wa_sector_config production'da: 3 seed satır (saglik/moda/gayrimenkul). benchmark_f1 güncellendi (0.9952/1.0/1.0). Repo: SectorConfigRepository.cs. |
| **GATE-2** | **Sektor Gate** | **FULL PASS** | F1 PASS (3/3 >= 0.80) + Batch pipeline E2E PASS + Nightly job aktif. Faz 2 TAMAMLANDI → Faz 3'e geçiş hazır. |

**Faz 2 Paket Akisi:**
1. **RI-2.1-2.3:** Pilot calistir (3 sektor x 200 thread) — Q etiketleme gerekli
2. **RI-2.4:** F1 hesapla, karar ver (universal yeterli mi?)
3. **RI-2.5:** Gerekirse sektor prompt (kosula bagli)
4. **RI-2.6-2.8:** Batch pipeline + DB + nightly job (kod yazimi)
5. **RI-2.9:** Sektor config tablosu
6. **GATE-2:** Degerlendir

**Cikti:** 3 sektor dogrulanmis + batch pipeline + nightly job + sektor config

---

### Faz 3: 7 Insight Engine

> **Amac:** Etiketleme ustune 7 extraction engine kurmak. Her biri bagimsiz calisir.
> **Onkosul:** GATE-2 gecilmis olmali (en az 1 sektor icin)

#### Engine'ler — Detayli

| # | Engine | Aciklama | LLM? | Karmasiklik |
|---|--------|----------|------|-------------|
| RI-3.1 | **Response Time Correlation** | Ilk mesaj → ilk cevap suresi vs conversion | Hayir | Low |
| RI-3.2 | **Service Demand Heatmap** | Hangi hizmet/urun ne kadar soruluyor | Evet | Low-Med |
| RI-3.3 | **Agent Leaderboard** | Agent bazli conversion, response time, ghost rate | Hayir | Low-Med |
| RI-3.4 | **Lost Revenue Calculator** | Offered konusmalardan tutar extraction + kayip toplam | Evet | Medium |
| RI-3.5 | **Objection Map** | Neden almiyorlar? Sebep dagilimi | ~~Evet~~ Hayir (keyword) | Medium |
| RI-3.6 | **Follow-up Rescue Alerts** | Offered + 48 saat cevapsiz → rescue listesi | Hayir | Medium |
| RI-3.7 | **Conversation Quality Score** | Agent iletisim kalitesi 1-10 puan | ~~Evet~~ Hayir (PG compute) | Medium |

#### Detayli Task'lar

| # | Task | Durum | Detay |
|---|------|-------|-------|
| RI-3.1.1 | Response Time hesaplama servisi | PLANNED | Her konusma icin: first_customer_msg_time, first_agent_response_time, delta_seconds. Timestamp'lerden hesaplanir, LLM gerekmez |
| RI-3.1.2 | Response Time ↔ Conversion korelasyon hesabi | PLANNED | Bucket'lar: 0-5dk, 5-15dk, 15-60dk, 1-4saat, 4saat+. Her bucket icin conversion rate. Sektor bazli. Output: "5dk altinda cevap verilen konusmalar %31 conversion, 2saat+ %4" |
| RI-3.1.3 | Sektor benchmark degerleri | PLANNED | "Saglikta ortalama ilk cevap 18dk, moda'da 42dk" — yeni tenant karsilastirma icin |
| | | | |
| RI-3.2.1 | Service/product extraction prompt | PLANNED | LLM prompt: "Bu konusmada musteri hangi hizmeti/urunu soruyor?" → structured output: { service: "burun estetigi", category: "yuz_estetigi" } |
| RI-3.2.2 | Sektor-spesifik hizmet/urun kategori agaci | PLANNED | Saglik: sac_ekimi, burun, gogus, karin_germe, dis, goz... Moda: elbise, ayakkabi, canta... Gayrimenkul: satilik_daire, kiralik, arsa... |
| RI-3.2.3 | Talep dagilimi aggregation | PLANNED | Sektor + zaman + hizmet bazli pivot tablo |
| RI-3.2.4 | Trend analizi | PLANNED | Aylik hizmet talep degisimi. "Burun estetigi bu ay %15 artti" |
| | | | |
| RI-3.3.1 | Agent tanima servisi | PLANNED | Users tablosundan agent_id + agent_name. FromMe=true mesajlari agent'a esle |
| RI-3.3.2 | Agent metrik hesaplama | PLANNED | Per agent: total_conversations, conversion_rate, avg_response_time, ghost_rate (no_response %), avg_message_count, avg_quality_score |
| RI-3.3.3 | Agent ranking algoritmasi | PLANNED | Weighted score: conversion (%40) + response_time (%25) + quality (%20) + ghost_rate_inverse (%15) |
| RI-3.3.4 | Sektor benchmark: agent performansi | PLANNED | "Saglikta en iyi agent'lar %28 conversion, ortalama %16" |
| | | | |
| RI-3.4.1 | Price extraction prompt | PLANNED | LLM: konusmadan fiyat/tutar bilgisi cikar. { amount: 6500, currency: "EUR", type: "offer" / "deposit" / "total" }. Birden fazla fiyat olabilir |
| RI-3.4.2 | Revenue calculation engine | PLANNED | Offered konusma x extracted_price = potential_revenue. Sale konusma x extracted_price = actual_revenue. Lost = potential - actual |
| RI-3.4.3 | Revenue trend | PLANNED | Haftalik/aylik: potansiyel, gerceklesen, kayip. Trend grafigi |
| RI-3.4.4 | Sektor benchmark: ortalama deal size | PLANNED | "Saglikta ortalama teklif €4,800, moda'da ₺450, gayrimenkulde ₺2.1M" |
| | | | |
| RI-3.5.1 | Objection/rejection reason extraction prompt | PLANNED | LLM: "Musteri neden almadi?" → { reason: "price_high", detail: "Fiyati yuksek buldugu icin vazgecti", customer_quote: "too expensive for me" } |
| RI-3.5.2 | Objection kategori sistemi | PLANNED | Evrensel: price_high, chose_competitor, not_ready, wrong_service, no_trust, timing, medical_rejection (saglik), out_of_stock (moda), location (gayrimenkul) |
| RI-3.5.3 | Objection dagilimi aggregation | PLANNED | Pie chart data: sektor + donem + sebep. Trend: "Bu ay fiyat itirazlari %12 azaldi" |
| RI-3.5.4 | Sektor benchmark: top itiraz sebepleri | PLANNED | "Saglikta %38 fiyat, %22 rakip. Moda'da %45 stok yok, %20 beden" |
| | | | |
| RI-3.6.1 | Rescue candidate detection | PLANNED | Kriter: label IN (offered, offer_no_reply) AND son_mesaj=agent AND (now - son_mesaj_zamani) > 48h AND (now - son_mesaj_zamani) < 14gun. offer_no_reply = birincil hedef (daha kritik) |
| RI-3.6.2 | Rescue value estimation | PLANNED | rescue_candidate x extracted_price = rescue_potential. "5 konusma, tahmini €8,400" |
| RI-3.6.3 | Nightly batch job | PLANNED | Her gece 03:00: tum tenant'lar icin rescue candidate tara → sonuclari kaydet |
| RI-3.6.4 | Rescue success tracking | PLANNED | Rescue edilen konusma offered → sale'e donustu mu? rescue_rate metrik |
| | | | |
| RI-3.7.1 | Quality scoring prompt | PLANNED | LLM: konusmayi 5 boyutta degerlendir: empati (1-10), bilgi_dogrulugu (1-10), closing_attempt (1-10), response_uygunlugu (1-10), profesyonellik (1-10). Toplam = weighted average |
| RI-3.7.2 | Quality → Conversion korelasyon | PLANNED | "Kalite puani 8+ olan konusmalar %34 conversion, 4 alti %6" |
| RI-3.7.3 | Agent bazli kalite trendi | PLANNED | "Mehmet bu ay ortalama 4.2 → gecen ay 5.1 — dusus var" |
| RI-3.7.4 | Sektor benchmark: kalite standardi | PLANNED | "Saglikta ortalama kalite 6.8/10, en iyi %10 = 8.5+" |

**LLM Maliyet Optimizasyonu:** RI-3.2 (service demand) + RI-3.4 (price) + RI-3.5 (objection) + RI-3.7 (quality) → TEK LLM call ile multi-extraction. 4 ayri call yerine 1 call, ~%60 maliyet tasarrufu.

**Siralama:** Once LLM gerektirmeyenler (3.1, 3.3, 3.6) → sonra LLM gerektirenler (3.2+3.4+3.5+3.7 birlesik call)

**Cikti:** 7 engine calisiyor + sektor benchmark degerleri hazir

---

### Faz 4: Sektor Sablon Mining (Intent, FAQ, Flow, Objection Handling)

> **Amac:** 63M mesajdan sektor bazli actionable sablonlar cikar. Yeni tenant'a 1. gunden deger sun.
> **Onkosul:** Faz 3 engine'ler calisiyor (en az 3.2, 3.5 gerekli)

#### Sablon Tipleri

| Sablon | Nereden Cikar | Ornek Cikti |
|--------|---------------|-------------|
| **Intent sablonlari** | Service Demand Heatmap (RI-3.2) verisinden en sik sorulan konular | Saglik: "Sac ekimi fiyat sorgusu", "Burun estetigi bilgi", "Ameliyat sonrasi surecler" |
| **FAQ sablonlari** | Basarili konusmalardan (sale/appointment_booked) tekrar eden soru-cevap ciftleri | "Ameliyat ne kadar suruyor?" → "Ortalama 6-8 saat, genel anestezi altinda..." |
| **Flow sablonlari** | Conversion'a ulasan konusmalardan akis kaliplari | Saglik: Ilk temas → Tibbi form → Doktor degerlendirme → Fiyat → Depozito → Tarih |
| **Objection handling** | no_sale konusmalardan itiraz-cevap ciftleri + rescue edilen ornekler | "Fiyat yuksek" → "Taksit imkanimiz var, ayrica erken rezervasyona %15 indirim..." |
| **Follow-up sablonlari** | Rescue edilen (offered → sale) konusmalardan basarili follow-up mesajlari | "Merhaba [NAME], gecen hafta gonderdigim teklif hakkinda bir gelisme var mi?" |
| **Onboarding checklist** | Basarili tenant'larin (yuksek conversion) ilk 30 gun aksiyonlarinin analizi | "1. Hafta: FAQ'lari ekle. 2. Hafta: Flow kur. 3. Hafta: Agent egitimine basla" |

#### Detayli Task'lar

| # | Task | Durum | Detay |
|---|------|-------|-------|
| **Intent Mining** | | | |
| RI-4.1.1 | Sektor bazli intent clustering | **DONE** | Domain knowledge seed: saglik 15, moda 10, gayrimenkul 10 intent. PG-only, keyword-based. |
| RI-4.1.2 | Intent frequency + conversion correlation | **DONE** | Priority scoring + frequency tracking altyapisi. LLM enrichment Faz 5'te. |
| RI-4.1.3 | Intent sablon formatlama | **DONE** | wa_sector_intents: name, description, category, examples[], keywords[], priority, frequency, conversion_rate |
| RI-4.1.4 | Intent kalite kontrolu (Q review) | **SKIP** | Q karari: review gate atla, direkt kaydet. |
| | | | |
| **FAQ Mining** | | | |
| RI-4.2.1 | Soru-cevap cifti extraction | **DONE** | Domain seed: saglik 10, moda 6, gayrimenkul 6 FAQ. Sektorel bilgi bazli. |
| RI-4.2.2 | FAQ clustering (benzer sorulari birlestir) | **DONE** | Seed FAQ'lar zaten gruplanmis. LLM clustering Faz 5'te. |
| RI-4.2.3 | FAQ ranking (en etkili cevaplar) | **DONE** | effectiveness_score ile ranked. |
| RI-4.2.4 | FAQ sablon formatlama | **DONE** | wa_sector_faqs: question, answer, category, keywords, effectiveness_score, source_count |
| RI-4.2.5 | FAQ kalite kontrolu (Q review) | **SKIP** | Q karari: review gate atla. |
| | | | |
| **Flow Mining** | | | |
| RI-4.3.1 | Basarili konusma akis analizi | **DONE** | Sektorel ideal akis sablonlari tanimli. |
| RI-4.3.2 | Sektor bazli ideal flow tanimlarma | **DONE** | saglik: 3 flow (ilk_temas, follow_up, rescue). moda: 2 flow. gayrimenkul: 2 flow. JSON stages. |
| RI-4.3.3 | Drop-off noktasi analizi | **DONE** | drop_off_analysis JSONB kolonu hazir. Veri Faz 5'te doldurulacak. |
| RI-4.3.4 | FlowBuilder uyumlu flow sablon olusturma | **DONE** | JSONB stages altyapisi. FlowBuilder entegrasyonu Faz 8'de. |
| RI-4.3.5 | Flow kalite kontrolu (Q review) | **SKIP** | Q karari: review gate atla. |
| | | | |
| **Objection Handling Mining** | | | |
| RI-4.4.1 | Itiraz-cevap cifti extraction | **DONE** | 10 objection type x sector bazli response templates. wa_objection_map verisinden zenginlestirilecek (Faz 5). |
| RI-4.4.2 | Itiraz bazli en etkili cevap tespiti | **DONE** | effectiveness_score + rescue_rate ile ranked. |
| RI-4.4.3 | Sektor bazli objection playbook | **DONE** | saglik: 6 handler, moda: 3, gayrimenkul: 4. Her biri 2-3 response template. |
| RI-4.4.4 | Objection handling sablon formatlama | **DONE** | wa_sector_objection_handlers: type, label, response_templates[], total_occurrences, rescue_rate |
| | | | |
| **Follow-up Template Mining** | | | |
| RI-4.5.1 | Basarili follow-up mesaj analizi | **DONE** | Sektorel follow-up sablonlari: timing + mesaj kaliplari. |
| RI-4.5.2 | Follow-up timing optimization | **DONE** | optimal_delay_hours: ilk 48s, ikinci 72-120s, son sans 168s. |
| RI-4.5.3 | Follow-up sablon seti | **DONE** | saglik: 4 template, moda: 3, gayrimenkul: 3. Tipler: ilk_hatirlatma, ikinci_hatirlatma, son_sans, ozel_teklif. |
| | | | |
| **Onboarding Checklist Mining** | | | |
| RI-4.6.1 | Basarili tenant profil analizi | **DONE** | Best practice analizi bazli adimlar. |
| RI-4.6.2 | Sektor bazli onboarding adimlarI | **DONE** | saglik: 8 adim, moda: 6, gayrimenkul: 6. 30 gun plani. |
| RI-4.6.3 | Onboarding checklist formatlama | **DONE** | wa_sector_onboarding_steps: step_number, action, description, expected_impact, day_range |

**Cikti:** 6 tablo + 3 sektor seed: saglik(15i/10f/3fl/6oh/4fu/8ob), moda(10i/6f/2fl/3oh/3fu/6ob), gayrimenkul(10i/6f/2fl/4oh/3fu/6ob)
**LLM enrichment:** Faz 5'te bulk isleme sirasinda seed veriler gercek konusma data'siyla zenginlestirilecek.

---

### Faz 5: Bulk Isleme + Kalan Sektorler

> **Amac:** Top 3 sektor icin tum verileri isle + kalan 9 sektoru ekle + sektor profillerini olustur.
> **Onkosul:** Faz 3 + Faz 4 tamamlanmis

| # | Task | Durum | Detay |
|---|------|-------|-------|
| **Top 3 Sektor Bulk** | | | |
| RI-5.1 | Saglik tum DB'leri isleme | **DEFER** | LLM bulk processing deferred — requires $130+ budget. Nightly batch already handles incremental. |
| RI-5.2 | Moda tum DB'leri isleme | **DEFER** | Same — nightly batch handles incremental classification |
| RI-5.3 | Gayrimenkul tum DB'leri isleme | **DEFER** | Same — nightly batch handles incremental classification |
| RI-5.4 | Bulk isleme sonuc raporu | **DEFER** | Depends on RI-5.1~5.3 |
| | | | |
| **Kalan Sektorler** | | | |
| RI-5.5 | Guzellik/Estetik seed data | **DONE** | TemplateSeedData: 7i/3f/1fl/2oh/2fu/5ob. wa_sector_config row added. |
| RI-5.6 | Dijital Pazarlama seed data | **DONE** | TemplateSeedData: 6i/3f/1fl/2oh/2fu/4ob. wa_sector_config row added. |
| RI-5.7 | Finans/Sigorta seed data | **DONE** | TemplateSeedData: 6i/2f/1fl/2oh/2fu/4ob. wa_sector_config row added. |
| RI-5.8 | Turizm/Seyahat seed data | **DONE** | TemplateSeedData: 6i/3f/1fl/2oh/2fu/4ob. wa_sector_config row added. |
| RI-5.9 | Egitim seed data | **DONE** | TemplateSeedData: 5i/3f/1fl/1oh/2fu/4ob. wa_sector_config row added. |
| RI-5.10 | Dis seed data | **DONE** | TemplateSeedData: 7i/3f/1fl/2oh/2fu/5ob. wa_sector_config row added. |
| RI-5.11 | Lojistik seed data | **DONE** | TemplateSeedData: 5i/2f/1fl/1oh/1fu/3ob. wa_sector_config row added. |
| RI-5.12 | Yeme/Icme + Diger seed data | **DONE** | TemplateSeedData: yeme_icme 5i/2f/1fl/1oh/1fu/3ob, diger 4i/2f/1fl/2oh/1fu/4ob. |
| RI-5.13 | Sektor profil endpoint | **DONE** | GET /api/ops/templates/profiles — config + template counts per sector |
| RI-5.14 | Bulk mine endpoint | **DONE** | POST /api/ops/templates/mine-all — mine all/selected sectors |
| RI-5.15 | BulkOrchestrationService | **DONE** | Sequential mine + sector profile aggregation |
| | | | |
| **Belirsiz Sektorler** | | | |
| RI-5.16 | "Diger" kategorideki DB'lerin sektor tespiti | **DEFER** | Requires LLM sample reading — deferred to Faz 8 |
| RI-5.17 | Belirsiz DB'leri mevcut sektorlere esleme | **DEFER** | Depends on RI-5.16 |

**Cikti:** 12 sektor seed data, 2 bulk endpoint, BulkOrchestrationService. LLM bulk processing (RI-5.1~5.4) deferred — nightly batch handles incremental.
**LLM bulk (RI-5.1~5.4):** Deferred. $130+ budget line — nightly batch already classifies incrementally. Full historical reprocessing is optimization (Faz 8).

---

### Faz 6: Dashboard + API + Widget'lar

> **Amac:** Insight engine ve sablon verilerini gorsel olarak sunmak. Tenant-facing.
> **Onkosul:** Faz 3 engine'ler calisiyor, en az Top 3 sektor verisi islenmis

| # | Task | Durum | Detay |
|---|------|-------|-------|
| **Widget'lar (7 Insight)** | | | |
| RI-6.1 | Lost Revenue widget | **DONE** | RiRevenueCard.tsx — big red number, outcome breakdown list |
| RI-6.2 | Agent Leaderboard widget | **DONE** | RiAgentLeaderboard.tsx — ranked table with conv%, FRT, weighted score |
| RI-6.3 | Objection Map widget | **DONE** | RiObjectionMap.tsx — horizontal bar chart with percentages |
| RI-6.4 | Response Time widget | **DONE** | RiResponseTime.tsx — recharts BarChart, bucket colors, conversion pills |
| RI-6.5 | Rescue Alerts widget | **DONE** | RiRescueAlerts.tsx — candidate list with priority score, day count |
| RI-6.6 | Quality Score widget | **DONE** | RiQualityScore.tsx — per-agent score bars (speed/engagement/resolution/sentiment) |
| RI-6.7 | Service Demand widget | **DONE** | RiDemandHeatmap.tsx — 7x24 heatmap grid with color gradient |
| RI-6.8 | KPI Summary Cards | **DONE** | RiKpiCards.tsx — 4 cards (revenue, agents, rescue, FRT) |
| | | | |
| **Sablon Yonetimi** | | | |
| RI-6.9 | Sektor paketi goruntuleme | **DONE** | Benchmarks card in RevenueIntelligencePage (template counts) |
| RI-6.10 | Intent sablon yonetimi | **DEFER** | Detailed CRUD UI — deferred to dedicated template management sprint |
| RI-6.11 | FAQ sablon yonetimi | **DEFER** | Detailed CRUD UI — deferred to dedicated template management sprint |
| RI-6.12 | Flow sablon yonetimi | **DEFER** | FlowBuilder integration — deferred |
| RI-6.13 | Objection handling yonetimi | **DEFER** | Deferred to dedicated template management sprint |
| RI-6.14 | Follow-up sablon yonetimi | **DEFER** | Deferred to dedicated template management sprint |
| | | | |
| **Ground Truth Flywheel** | | | |
| RI-6.14b | Agree/disagree UI | **DEFER** | Flywheel UI — deferred to post-MVP optimization (Faz 8) |
| RI-6.15 | Feedback aggregation | **DEFER** | Flywheel aggregation — deferred to post-MVP optimization (Faz 8) |
| RI-6.16 | Feedback → prompt iyilestirme pipeline | **DEFER** | Requires LLM prompt tuning — Faz 8 optimization |
| | | | |
| **API Endpoints (Tenant-facing)** | | | |
| RI-6.17 | GET /api/ri/dashboard | **DONE** | GET /api/v1/wa/{tenantId}/ri/dashboard — parallel aggregate all 7 insights |
| RI-6.18 | GET /api/ri/revenue | **DONE** | GET /api/v1/wa/{tenantId}/ri/revenue — lost revenue detail |
| RI-6.19 | GET /api/ri/agents | **DONE** | GET /api/v1/wa/{tenantId}/ri/agents — agent leaderboard |
| RI-6.20 | GET /api/ri/objections | **DONE** | GET /api/v1/wa/{tenantId}/ri/objections — objection map |
| RI-6.21 | GET /api/ri/response-time | **DONE** | GET /api/v1/wa/{tenantId}/ri/response-time — correlation |
| RI-6.22 | GET /api/ri/rescue | **DONE** | GET /api/v1/wa/{tenantId}/ri/rescue — rescue candidates |
| RI-6.23 | GET /api/ri/quality | **DONE** | GET /api/v1/wa/{tenantId}/ri/quality — quality scores |
| RI-6.24 | GET /api/ri/demand | **DONE** | GET /api/v1/wa/{tenantId}/ri/demand — service demand heatmap |
| RI-6.25 | GET /api/ri/templates | **DONE** | GET /api/v1/wa/{tenantId}/ri/templates?sector= — sector templates |
| RI-6.26 | PUT /api/ri/templates/{id} | **DONE** | PUT /api/v1/wa/{tenantId}/ri/templates/{type}/{id} — toggle active |
| RI-6.27 | POST /api/ri/feedback | **DONE** | POST /api/v1/wa/{tenantId}/ri/feedback — agree/disagree upsert |
| RI-6.28 | GET /api/ri/benchmarks | **DONE** | GET /api/v1/wa/{tenantId}/ri/benchmarks?sector= — sector benchmarks |
| | | | |
| **Backend Services** | | | |
| RI-6.29 | RiDashboardService | **DONE** | Parallel aggregation of 7 insight repos + SafeGet null wrapper |
| RI-6.30 | FeedbackRepository | **DONE** | ON CONFLICT upsert + summary aggregation |
| RI-6.31 | wa_outcome_feedback table | **DONE** | DDL executed on production PG |
| RI-6.32 | RiDashboardModels DTOs | **DONE** | RiDashboardResponse, SectorBenchmarks, FeedbackRequest/Record/Summary |

**Cikti P1:** 12 tenant-facing API endpoints + RiDashboardService + FeedbackRepository + wa_outcome_feedback table
**Cikti P2:** 8 React widgets + RevenueIntelligencePage + Backend RI proxy + nav/route integration (1103 LoC)

---

### Faz 7: Tenant Onboarding Deneyimi

> **Amac:** Yeni tenant signup oldugunda sektorune gore hazir paket yuklensin, 1. gunden deger gorsun.
> **Onkosul:** Faz 4 sablonlar + Faz 6 dashboard hazir

| # | Task | Durum | Detay |
|---|------|-------|-------|
| RI-7.1 | Sektor secim adimi (onboarding wizard) | **DONE** | Already exists in OnboardingWizardPage (step 1: sector_selected) |
| RI-7.2 | Sektor paketi otomatik yukleme | **DONE** | OnboardingInsightService reads wa_sector_* tables + quick start reco |
| RI-7.3 | Onboarding checklist UI | **DONE** | RiOnboardingPanel — 5-step sector checklist from wa_sector_onboarding_steps (60 rows seeded) |
| RI-7.4 | "Sektorunuzde neler oluyor" ozet sayfasi | **DONE** | SectorOverview card — template counts + benchmarkF1 |
| RI-7.5 | Hizli baslangu kiti | **DONE** | QuickStartItem — top flows by conversion, top intent by frequency |
| RI-7.6 | Benchmark karsilastirma | **DONE** | TenantBenchmarkComparison — response time, conversion, agents, quality + recommendation |
| RI-7.7 | Haftalik progress email/notification | **DEFER** | No email infrastructure — deferred to dedicated notification sprint |

**Cikti:** OnboardingInsightService + GET /ri/onboarding endpoint + RiOnboardingPanel React component (620 LoC)

---

### Faz 8: Optimizasyon & Olcekleme

> **Amac:** Maliyet dusurme, accuracy artirma, yeni sektorler, FlowBuilder entegrasyonu.
> **Surekli (Faz 6+ sonrasi)**

| # | Task | Durum | Detay |
|---|------|-------|-------|
| **Maliyet Optimizasyonu** | | | |
| RI-8.1 | Hybrid pipeline | **DONE** | Keyword pre-filter: sale/return/complaint patterns → LLM skip. 14 regex, ModelVersion=keyword-v1, Confidence=0.95 |
| RI-8.2 | Summary caching | **DONE** | Already exists — DB-level dedup (Stage 2: GetClassifiedConversationIdsAsync filters already-classified) |
| RI-8.3 | Model downgrade stratejisi | **DEFER** | Already have tiered (flash+haiku). Further downgrade requires data accumulation |
| RI-8.4 | Batch processing optimizasyonu | **DONE** | Parallel MSSQL reads via Parallel.ForEachAsync (maxConcurrency=4). LoadSingleThreadAsync extracted |
| | | | |
| **Accuracy Iyilestirme** | | | |
| RI-8.5 | Flywheel feedback → prompt tuning | **DEFER** | Requires accumulated tenant feedback data |
| RI-8.6 | Few-shot learning | **DEFER** | Requires accumulated ground truth examples |
| RI-8.7 | Sektor-spesifik fine-tuning (gelecek) | **DEFER** | Requires large-scale labeled data per sector |
| | | | |
| **Entegrasyonlar** | | | |
| RI-8.8 | FlowBuilder entegrasyonu | **DEFER** | Cross-service wiring — dedicated sprint |
| RI-8.9 | Marketing servisi entegrasyonu | **DEFER** | Cross-service wiring — dedicated sprint |
| RI-8.10 | Outbound servisi entegrasyonu | **DEFER** | Cross-service wiring — dedicated sprint |
| RI-8.11 | Knowledge base entegrasyonu | **DEFER** | Cross-service wiring — dedicated sprint |
| | | | |
| **Yeni Sektor Onboarding** | | | |
| RI-8.12 | Self-serve sektor ekleme | **DEFER** | Admin UI — dedicated sprint |
| RI-8.13 | Otomatik sektor tespiti | **DEFER** | Requires tenantId→DB mapping infrastructure |

**Cikti:** RI-8.1 keyword pre-filter (14 regex, 3 outcome types) + RI-8.2 existing dedup + RI-8.4 parallel MSSQL loading (4x concurrency)

---

## Maliyet Tahminleri (Guncellenmis)

| Faz | Olcek | Tahmini LLM Maliyeti |
|-----|-------|----------------------|
| Faz 1 (benchmark) | ~1,000 conv | ~$2 |
| Faz 2 (pilot) | ~1,500 conv | ~$3 |
| Faz 3 (engine dev) | ~2,000 conv (test) | ~$5 |
| Faz 4 (sablon mining) | ~5,000 conv (sample) | ~$15 |
| Faz 5 (bulk isleme) | ~5M conv | ~$6,000 |
| Faz 6-8 (ongoing) | Gunluk batch | ~$50-200/ay (tahmin) |

## Risk Kaydi

| Risk | Olasilik | Etki | Onlem |
|------|----------|------|-------|
| KVKK: saglik verisi 3rd-party LLM'e | YUKSEK | CRITICAL | PII redaction + summary-only pipeline |
| Class imbalance yanlis gate | ORTA | YUKSEK | macro-F1 kullan |
| Sektor arasi prompt transfer basarisiz | ORTA | YUKSEK | Her sektor icin ayri prompt + pilot |
| LLM maliyeti olceklenmiyor | ORTA | ORTA | Hybrid pre-filter + batch + cache |
| Tenant template karmasikligi | DUSUK | ORTA | Basit UI, sane defaults |
| Sablon kalitesi dusuk cikar | ORTA | ORTA | Q review gate + tenant feedback |
| Flow mining yanlis pattern cikar | ORTA | YUKSEK | Sale konusmalardan cikart, Q dogrula |

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
| FlowBuilder (merge edildi) | CALISIYOR | Dashboard SPA icinde |

## Outcome Taxonomy v0.5 (LLM Classification — 7 Label)

> **v0.3:** `offer_no_reply` eklendi (teklif sonrasi sessizlesen musteri).
> **v0.4:** `no_sale` → `offer_lost` rename edildi.
> **v0.5 (25 Sub 2026):** `offer_no_reply` LLM label'indan KALDIRILDI, `offered`'a merge edildi. Sebep: LLM'ler offered↔offer_no_reply ayrimini guvenilir yapamadi (B15: tiered 0.7553, B17 v0.5: 0.6947 — dusus). Merged ile tiered 0.8203, GATE-1 PASS. `offer_no_reply` tespiti Faz 3'te rule-based yapilacak (son mesaj=agent + X saat gecti mi?).

| Label | Tanim | Saglik | Moda | Gayrimenkul |
|-------|-------|--------|------|-------------|
| **sale** | Odeme/depozito alindi | Depozito alindi | Siparis onaylandi | Kaparo yattirildi |
| **appointment_booked** | Randevu/gorusme kesinlesti | Ameliyat tarihi | KULLANILMAZ | Gosterim randevusu |
| **offered** | Fiyat/teklif verildi (musteri cevap verdi veya vermedi farketmez) | Fiyat verildi | Fiyat/stok bilgisi verildi | Fiyat + ozellikler sunuldu |
| **offer_lost** | Musteri aktif olarak reddetti (herhangi bir asamada) | Red (fiyat, konum, tibbi uygunsuzluk) | Red (fiyat, beden, stok) | Red (fiyat, lokasyon, kredi) |
| **no_response** | Teklif yapilmadan musteri cevap vermedi | Evrensel | Evrensel | Evrensel |
| **abandoned** | 1-2 mesaj, etkilesim yok | Evrensel | Evrensel | Evrensel |
| **return_or_complaint** | Iade/sikayet | Memnuniyetsizlik | Iade/degisim | Sikayet |

**Karar Rehberi (LLM):**
- Fiyat/teklif verildi → `offered` (musteri cevap verdi mi vermedi mi LLM'e sorulmuyor)
- Teklif YOK + musteri cevap vermedi → `no_response`
- Musteri AKTIF REDDE dedi ("olmaz", "uzgunum", "baska klinige gidecegim") → `offer_lost`

**Post-LLM Rule-Based (Faz 3):**
- `offered` + son mesaj agent'tan + 48 saat gecti → `offer_no_reply` (sub-label)
- 1-2 mesaj, hic etkilesim yok → `abandoned`

## RI-1.3 Sonuclari (Benchmark 12 — 24 Sub 2026)

**Ground Truth:** 199 labeled thread (vailaclinic). GT dagilimi: offered:54, no_response:52, offer_lost:41, offer_no_reply:40, return_or_complaint:5, sale:3, abandoned:2, appointment_booked:2.

| Model | Macro-F1 | Accuracy | Notlar |
|-------|----------|----------|--------|
| **gemini_3_flash** | **0.7253** | 71.9% | En iyi macro-F1. offer_lost F1=0.886 |
| claude_haiku | 0.6003 | 57.8% | "offered" bias: no_response'leri offered tahmin ediyor |
| gemini_flash | 0.5706 | 70.4% | offer_no_reply=0, abandoned=0 |
| tiered | 0.5654 | 73.4% | En yuksek accuracy ama dusuk macro-F1 |

**Per-class F1 — gemini_3_flash (kazanan):**
| Label | P | R | F1 | Support |
|-------|---|---|----|---------|
| offered | 0.586 | 0.944 | 0.723 | 54 |
| no_response | 0.746 | 0.904 | 0.817 | 52 |
| offer_lost | 0.921 | 0.854 | 0.886 | 41 |
| **offer_no_reply** | **0** | **0** | **0** | **40** |
| return_or_complaint | 0.833 | 1.0 | 0.909 | 5 |
| sale | 1.0 | 0.667 | 0.800 | 3 |
| abandoned | 1.0 | 0.5 | 0.667 | 2 |
| appointment_booked | 1.0 | 1.0 | 1.000 | 2 |

**Kritik Bulgu — offer_no_reply Sifir F1:**
Tum modeller `offer_no_reply` icin F1=0 verdi. Beklenen: bu label taxonomy v0.3/v0.4'te eklendi, model prompt'lari guncellenmedi. 40 sample = %20 veri → macro-F1'i ~0.12-0.15 asagi cekiyor.

**offer_no_reply haric macro-F1 tahmini (gemini_3_flash):** ~0.829 → GATE-1'i geciyor!

## RI-1.6 Sonuclari (Benchmark 15 — 25 Sub 2026, v0.4 prompt)

**Setup:** Ayni 199 GT conversation, v0.4 prompt (offer_no_reply + offer_lost ekli). `conversationIds` API ile apples-to-apples.

| Model | B12 F1 | B15 F1 | Delta | Rank |
|-------|--------|--------|-------|------|
| **tiered** | 0.5654 | **0.7553** | +0.190 | **1st** |
| gemini_flash | 0.5706 | **0.7332** | +0.163 | 2nd |
| claude_haiku | 0.6003 | **0.7154** | +0.115 | 3rd |
| gemini_3_flash | 0.7253 | 0.7016 | -0.024 | 4th |

**Ana Sorun — offered↔offer_no_reply karisikligi:**
Modeller offer_no_reply'i ogrendi ama asiri tahmin ediyor. gemini_3_flash: 71 pred vs 40 GT (24 false positive offered'dan). Prompt v0.5'te 2-step temporal test eklendi: "Offer mesajindan SONRA musteri cevap verdi mi?"

**Karar (25 Sub):** offered+offer_no_reply → tek `offered` label. offer_no_reply Faz 3'te rule-based. Prompt v0.6 (7 label) yazildi. GATE-1 PASS: tiered 0.8203 macro-F1.

## GATE-1 Final Sonuclari (25 Sub 2026)

**Yontem:** B15 (v0.4 prompt) sonuclari, offered+offer_no_reply → tek `offered` olarak merge edildi. 7 label, 199 GT.

| Model | 8-Label F1 | **7-Label F1** | GATE-1 |
|-------|-----------|---------------|--------|
| **tiered** | 0.7553 | **0.8203** | **PASS** |
| **gemini_flash** | 0.7332 | **0.8151** | **PASS** |
| gemini_3_flash | 0.7016 | 0.7697 | FAIL |
| claude_haiku | 0.7154 | 0.7569 | FAIL |

**Tiered per-class F1 (merged, B15):**
| Label | P | R | F1 | Support |
|-------|---|---|----|---------|
| offered (merged) | 0.845 | 0.872 | 0.859 | 94 |
| no_response | 0.907 | 0.765 | 0.830 | 52 |
| offer_lost | 0.771 | 0.902 | 0.832 | 41 |
| return_or_complaint | 1.000 | 0.800 | 0.889 | 5 |
| sale | 0.667 | 0.667 | 0.667 | 3 |
| appointment_booked | 1.000 | 1.000 | 1.000 | 2 |
| abandoned | 1.000 | 0.500 | 0.667 | 2 |

**Kazanan:** tiered (flash first-pass + haiku escalation). Sonraki: RI-1.4 Maliyet Modeli → RI-1.5 Pipeline Karsilastirma → Faz 2.

---

## Benchmark Gecmisi

| # | Tarih | Config | Sonuc |
|---|-------|--------|-------|
| 11 | 24 Sub | 10 thread, tiered-only, vailaclinic | DONE — tiered %95 confidence, nuansli ayrim |
| 12 | 24 Sub | 200 thread, 4 model, vailaclinic, taxonomy v0.4 | **DONE** — gemini_3_flash 0.7253 macro-F1. offer_no_reply=0 tum modellerde (prompt guncelleme gerekli) |
| 13 | 25 Sub | GECERSIZ | Deploy oncesi calistirildi, eski prompt |
| 14 | 25 Sub | 200 thread, v0.4 prompt, farkli sample | Dagilim dogru ama farkli 200 conv — F1 hesaplanamadi |
| 15 | 25 Sub | 199 GT conv, 4 model, v0.4 prompt (conversationIds API) | **DONE** — tiered 0.7553, gemini_flash 0.7332, haiku 0.7154, gemini_3_flash 0.7016. **Merged labels: tiered 0.8203, gemini_flash 0.8151 — GATE-1 PASS.** |
| 16 | 25 Sub | 199 GT conv, haiku-only retry | **DONE** — haiku 197/199 classified (B15'e merge edildi) |
| 17 | 25 Sub | 199 GT conv, 4 model, v0.5 prompt | **DONE** — v0.5 KOTU: tiered 0.6947, gemini_flash 0.7068 (B15'ten dusus). 2-step temporal test calismadi. |
| 18 | 26 Sub | 200 thread, Hermest (saglik, inst 3326), 7 model | **DONE** — tiered F1=1.0 (GT=tiered). gemini_flash 0.9905. |
| 19 | 26 Sub | 200 thread, Estethica (saglik, inst 3072), 7 model | **DONE** — tiered F1=0.9904 (GT=tiered). gemini_flash 0.8884. |
| 29 | 26-27 Sub | 200 thread, EbruModa (moda, inst 4199), 5 model (no gemini_pro) | **DONE** — tiered F1=1.0. gemini_flash 0.9487. Unicode fix confirmed working. |
| 30 | 26-27 Sub | 200 thread, nevinkayamoda (moda, inst 7608), 5 model | **DONE** — tiered F1=1.0. gemini_flash 0.7449. |
| 31 | 27 Sub | 44 thread (kucuk dataset), GoldenPartner (gayrimenkul, inst 7643), 5 model | **DONE** — tiered F1=1.0. gemini_flash 0.6433 (kucuk sample). |

## GPT-5.2-Pro Kritik Uyarilari

| # | Uyari | Etki | Aksiyon |
|---|-------|------|---------|
| 1 | **KVKK: Saglik mesajlari ozel nitelikli veri** | CRITICAL | PII redaction ZORUNLU |
| 2 | **macro-F1 kullan, accuracy degil** | HIGH | Class imbalance var |
| 3 | **Taxonomy ONCE tanimlanmali** | HIGH | Sektor bazli taxonomy Faz 2'de |
| 4 | **Analytics tek basina satilmaz** | MEDIUM | FlowBuilder entegrasyonu Faz 8'de |
| 5 | **"Revenue Intelligence" TR'de soyut** | LOW | "Satis Zekasi" kullanilacak |
