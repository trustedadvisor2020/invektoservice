# FEAT-VFB F0 PoC — Browser Smoke Test Senaryoları

> **Slug:** `20260523-feat-vfb-f0-poc` | **Tarih:** 2026-05-24 | **Test ortami:** Q laptop, Chrome 130+, `http://localhost:7115/voice-poc.html` | **Mod:** dev=1 bypass (localhost) | **Ses:** Türkçe TR-TR

Bu dosya F0 PoC live smoke test senaryolarını sıralar. Her senaryo 3 kez tekrarlanır (round 1/2/3); ortalama + p95 first-byte ve barge-in tepkisi `arch/reports/feat-vfb-f0-latency-report.md` icinde toplanir.

---

## Test Akışı — Her Senaryo İçin

1. Tarayıcıyı yenile (state temiz başlasın)
2. **"Mikrofonu Başlat"** → "Bağlı, konuşabilirsin"
3. Senaryodaki cümleyi sesli oku (Q kendi sesiyle)
4. Bot cevabı bekle → HUD'da `first_byte: <ms>` notu al
5. (Barge-in senaryolarında) bot konuşurken sözünü kes → `barge_in: <ms>` notu al
6. `response_done` görünene kadar bekle, sonra "Durdur"
7. Tarayıcıyı yenile, 2. round'a geç (3 round)

Her round için kaydet: **first_byte_ms, bot_voice_quality (TR doğallık 1-5), intent_correctness (cevap konuya uygun mu 1-5), barge_in_ms (varsa)**.

---

## Senaryolar 1-10 (mevcut, baseline)

| # | Kategori | Cümle / Davranış | Beklenen | Riski |
|---|----------|------------------|----------|-------|
| **1** | Sade soru | "Saç ekimi fiyatları nedir?" | Bot doğrudan fiyat aralığı verir; `first_byte<1000ms` p50 | Düşük (kontrollü baseline) |
| **2** | Uzun soru | "Saç ekimi yaptırmak istiyorum, kaç seans, ne kadar sürer, fiyat ne olur?" | Bot 3 alt-soruyu sıralı yanıtlar veya özetler | Orta (multi-question) |
| **3** | Barge-in | Bot fiyat söylerken "Dur, indirim var mı?" diye kes | TTS <500ms susar (`barge_in.elapsed_ms`); yeni soruya cevap | Yüksek (concurrent send + cancel race) |
| **4** | Duraksama | "Yani saç ekimi... [1.5sn bekle] ...ne kadar oluyor?" | Bot yarı yolda cevap üretmeye başlamaz; tam cümleyi bekler | Yüksek (OpenAI semantic_vad turn detection accuracy) |
| **5** | TR özel karakter | "Şu işlem için bilgi alabilir miyim?" | Türkçe karakterler (Ş, İ, ı, ç, ğ) doğru transcript edilir | Düşük (Whisper TR kalitesi) |
| **6** | Kısa cümle | "Fiyat nedir?" | Bot kısa cümleye context'siz makul cevap verir | Orta (intent boş bağlam) |
| **7** | Soru tonu | "Saç ekimi mi yaptırıyorsunuz?" (yükselen tonlama) | VAD tonlama farkını yakalar, bot anlam karışıklığını çözer | Düşük |
| **8** | Conjunction sonu | "Saç ekimi ve fiyatları ve... seans sayısı?" | Bot 've' bağlacında durmaz, tam soruyu bekler | Yüksek (semantic EOT) |
| **9** | Sessizlik öncesi | 5sn bekle, sonra "Merhaba?" | Bot 5sn boyunca sessizlik fail-open yapmaz; konuşma başlar başlamaz tepki verir | Düşük |
| **10** | Fragmented | "Saç ekimi... yani... biraz bilgi" | Bot kelime kümelerini birleştirip anlamlı yanıt verir | Yüksek (EOT model) |

---

## Senaryolar 11-15 (yeni — Q talebi 2026-05-24)

| # | Kategori | Cümle / Davranış | Beklenen | Riski |
|---|----------|------------------|----------|-------|
| **11** | **İnsan transfer talebi** | "Bir doktora bağlanabilir miyim?" veya "Sizi anlamadım, gerçek bir kişiyle konuşmak istiyorum" | Bot transfer intent'i tanır; "Sizi temsilciye yönlendiriyorum" anonsu verir (F2'de gerçek `voice_transfer` node tetikler — F0'da sadece intent log ile doğrulanır, transcript'te "yönlendiriyorum" görünmeli) | Orta (paralel GPT-4o-mini intent classifier, F0 stateless ama Realtime kendi tool calling ile yakalamali) |
| **12** | **Çoklu sıralı intent (compound query)** | "Önce fiyatı, sonra randevu, en sonunda da iptal şartlarını söyler misiniz?" | Bot 3 alt-intent'i sıralı işler: fiyat → randevu → iptal. Atlama veya sıra karışıklığı olmamalı. `transcript_bot` üç ayrı bölüm olarak gelir | Yüksek (multi-step reasoning + sıra preservation; OpenAI Realtime context window dışına çıkma riski) |
| **13** | **Empati / şikayet** | "Geçen sefer kötü bir deneyim yaşadım, sizinle çalışmak istemiyorum aslında" (üzgün/sinirli tonla) | Bot agresif/savunmacı tepki vermez; "Üzgünüm, anlıyorum" gibi empati cümlesi ile başlar; sonra çözüm önerisi sunar. **Türkçe doğal ses tonu kritik** (Realtime audio-out duygu nüansını yakalamalı) | Yüksek (LLM bias riski + TR duygu doğallığı + müşteri retention senaryosu) |
| **14** | **Sayısal entity extraction** | "3 Haziran Cuma günü saat 14:30'da, 3000 graft için randevu alabilir miyim?" | Bot tarih (3 Haziran Cuma), saat (14:30), miktar (3000 graft) entity'lerini doğru transcript eder ve cevabında geri yansıtır. **Saat formatı kritik** (14:30 vs 2:30 PM kafa karışıklığı yok) | Yüksek (Whisper TR sayı/tarih accuracy; sayıların yazılış formatı transcript'te "üç bin" vs "3000" tutarsızlığı) |
| **15** | **Yarım kalmış cümle (gerçek interruption)** | "Saç ekimi yaptırmak istiyorum ama..." [konuşmayı kes, 3sn bekle, sonra] "...aslında daha sonra ararım" | İlk yarıda bot cevap üretmeye başlamamalı (3sn susma boş). İkinci yarı eklendikten sonra bot bağlamı toparlayıp uygun cevap verir ("Tabii, sizi sonra bekleriz") | Yüksek (extended pause + context retention + intent reversal — "ama" sonrası negatif intent flip) |

---

## Latency Hedefler (F0 Hedef AC4)

| Metrik | Hedef | Kabul edilebilir | FAIL |
|--------|-------|------------------|------|
| `first_byte_ms` p50 | <800ms | <1000ms | ≥1500ms |
| `first_byte_ms` p95 | <1000ms | <1500ms | ≥2000ms |
| `barge_in_ms` (senaryo 3) | <250ms | <500ms | ≥800ms |
| `intent_correctness` ortalama | ≥4.5/5 | ≥4.0/5 | <3.5/5 |
| `bot_voice_quality` ortalama (TR doğallık) | ≥4.0/5 | ≥3.5/5 | <3.0/5 |

> **Sales-ready threshold:** `first_byte_ms` p95 < 1000ms ve `intent_correctness` ≥4.0 — bu eşik tutturulursa müşteri prospect demo'da kullanılır. Aşılırsa F2 öncesi root-cause analiz.

---

## Test Aracı

`/metrics/latency` endpoint'inden ham ölçüm al (token = Q superadmin JWT):

```powershell
Invoke-RestMethod "http://localhost:7115/metrics/latency?token=<SUPERADMIN_JWT>" | ConvertTo-Json -Depth 5
```

Her round sonu HUD'daki değerleri ve `/metrics/latency` snapshot'ını rapora yapıştır.

---

## Sıralı Test Akışı (Önerim)

1. **Önce 1-2-5-6** (baseline + kontrollü): sistemin temel çalışması doğrulanır
2. **Sonra 4-8-10** (semantic EOT zorluk): duraksama/conjunction/fragmented — Realtime semantic_vad kalitesi netleşir
3. **Sonra 3** (barge-in): concurrent send + cancel race testi (en yüksek riskli teknik path)
4. **Sonra 11-12** (intent + multi-step): GPT-4o reasoning quality testi
5. **Sonra 13** (empati): duygu/ton kalitesi — sales-demo'da en çok izlenecek senaryo
6. **Sonra 14** (entity extraction): sayı/tarih accuracy
7. **En son 15** (yarım cümle + reversal): edge case + context retention

Her round 1-2 dakika alır → **15 senaryo × 3 round ≈ 60-90 dakika** toplam smoke. Q tek oturumda yapabilir veya 3 oturuma bölebilir (her seferinde yenileme + tarayıcı temizliği).

---

## Smoke FAIL Eylemleri

- Eğer `first_byte_ms` p95 > 1500ms ise → **AD-8 fallback "pre-greeting padding"** (bot ilk 300ms doğal duraksama sesi) F2'de aktive et, F0'da sadece raporla
- Eğer `intent_correctness` < 3.5 ise → OpenAI Realtime `instructions` prompt'unu güçlendir (RealtimeSession.cs satır 35-40)
- Eğer `barge_in_ms` > 800ms ise → SemaphoreSlim send lock race condition analizi (VoicePocOrchestrator)
- Eğer senaryo 13 empati toned değilse → `voice` parametresi (alloy → shimmer/coral test et)

---

**Önceki firma referansı:** Q müşteri prospect'i "önceki firma 2.5sn gecikme" diyor. Bu raporun değeri: F0 sayısal kanıt + 15 senaryo kapsamı → sales-demo'da "biz 800ms, onlar 2500ms — 3x hızlı" iddiası belgelenir.
