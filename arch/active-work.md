# Active Work Tracker

> Devam eden işler. Session başında kontrol et.

## In Progress

| Slug | Status | Started | Description |
|------|--------|---------|-------------|
| 20260216-pkt3-ops-dashboard | ✅ DONE | 2026-02-16 | PKT-3: Ops Dashboard (GR-2.5 + WA-4). Commit: 63543d4. Codex CQ 8/8 PASS, Q FORCE PASS. |

---

## Execution Queue — 12 Paket Stratejisi (v5.2)

> **Karar (2026-02-15):** Tekli GR döngüsü yerine paket halinde yürütme.
> Her paket: 1 interview + 1 plan + sıralı dev + 1 build + 1 Codex review.
> **Neden:** Overhead %60 azalır, saf kod süresi aynı kalır.
> **Kural:** WA = WhatsApp Analytics, RP = Roadmap Phase, PKT = Execution Packet.
> **v5.1 (2026-02-15):** PKT-6 (19 GR, ~80 item) 3 alt pakete bölündü → PKT-6A/6B/6C. Toplam: 8 → 10 paket.
> **v5.2 (2026-02-16):** PKT-9 (Phase 3E Güzellik) + PKT-10 (Phase 3F Eğitim) eklendi → Toplam: 12 paket.

### Tamamlanan (Paket Öncesi — Tekli Döngü)

| Kod | İş | Durum | Notlar |
|-----|----|-------|--------|
| WA-3 + GR-2.1 Phase A | Knowledge Service Core (RAG + import) | ✅ DONE | Commit: 385d3e0 |
| GR-2.1 Phase B | Knowledge: PDF upload + chunking + UI | ✅ DONE | Commit: 89bbe72 |
| WA-5/6 Phase A | C# Microservice (Stages 1-3, Port 7109) | ✅ DONE | Commit: 18f387f |

### Aktif Paket Sırası

| # | Paket | Kod | İçerik | GR Sayısı | Durum |
|---|-------|-----|--------|-----------|-------|
| 1 | **AI Upgrade** | PKT-1 | GR-2.2 Agent Assist v2 + GR-2.3 Multi-lang | 2 GR, 13 alt madde | ✅ PASS (iter 3, FORCE PASS) |
| 2 | **Sağlık Core** | PKT-2 | GR-2.4 Randevu Motoru + GR-2.6 KVKK | 2 GR, 11 alt madde | ✅ PASS (iter 1, FORCE PASS) |
| 3 | **Ops Dashboard** | PKT-3 | GR-2.5 Otomasyon Dashboard + WA-4 BI Dashboard | 2 GR, 12 alt madde | ✅ PASS (iter 1, FORCE PASS) |
| 4 | **WA Analytics** | PKT-4 | WA-6 NLP Stages 4-7 + Backend proxy | 1 WA faz, ~8 alt madde | ✅ PASS (iter 7) |
| 5 | **Platform** | PKT-5 | Phase 3A: Integrations (:7106) + Outbound v2 + Randevu Advanced + Dashboard + Ads | 6 GR, 30 alt madde | ⬜ Bekliyor |
| 6A | **Niche Foundation** | PKT-6A | Phase 3B: Intent + Onboarding + Voice AI (bagimsiz, PKT-5 sonrasi hemen baslanabilir) | 7 GR, ~28 alt madde | ⬜ Bekliyor |
| 6B | **Niche Business Logic** | PKT-6B | Phase 3B: Outbound + Iade + Lead + Yorum Kurtarma (PKT-5 Integrations'a bagli) | 7 GR, ~30 alt madde | ⬜ Bekliyor |
| 6C | **Niche Health Expansion** | PKT-6C | Phase 3B: Saglik genisleme + Review Rescue + Multilingual (PKT-6B'ye bagli) | 5 GR, ~22 alt madde | ⬜ Bekliyor |
| 7 | **Visual AI** | PKT-7 | Phase 3C: Visual Product Search + Size/Fit AI (:7111) | 8 GR, ~30 alt madde | ⬜ Bekliyor |
| 8 | **Face AI** | PKT-8 | Phase 3D: Face Analysis AI (:7110) | 5 GR, ~20 alt madde | ⬜ Bekliyor |
| 9 | **Güzellik Salonu** | PKT-9 | Phase 3E: Güzellik Niche (config+content katmanı) | 8 GR, ~32 alt madde | ⬜ Bekliyor |
| 10 | **Eğitim** | PKT-10 | Phase 3F: Eğitim Niche (config+content katmanı) | 8 GR, ~32 alt madde | ⬜ Bekliyor |

> **PKT-1~4 = Phase 2 tamamlama** (kesin ihtiyaç, hemen deploy edilecek)
> **PKT-5~8 = Phase 3A-D tamamlama** (müşteri feedback'ine göre revize edilebilir)
> **PKT-9~10 = Phase 3E-F niche genişleme** (PKT-6 altyapısını tüketir, yeni servis yok)
> **Bağımlılık zinciri:** PKT-5 → PKT-6A (bağımsız) | PKT-5 → PKT-6B (Integrations) | PKT-6B → PKT-6C (3.24 için 3.8+3.16) | PKT-6 → PKT-9 (3E) | PKT-6 → PKT-10 (3F)

### Paket Detayları

**PKT-1: AI Upgrade** — AgentAI + ChatAnalysis + Knowledge genişletme
- GR-2.2: Reply generation Knowledge'dan beslenecek, kaynak referansı, tone presets, multi-turn
- GR-2.3: Language detection, multi-lang response, Knowledge multi-lang, Outbound dil seçimi

**PKT-2: Sağlık Core** — Backend + Outbound genişletme
- GR-2.4: Haftalık slot, randevu al/iptal, hatırlatma (T-48h/T-2h), Dashboard slot yönetimi
- GR-2.6: AI disclaimer, açık rıza, veri minimizasyonu, fotoğraf politikası

**PKT-3: Ops Dashboard** — Dashboard React + Backend genişletme
- GR-2.5: Deflection/handoff rate, trend grafikleri, intent performance, FRT, conversation metadata
- WA-4: Agent performans, conversion, trend raporları (Python NLP çıktıları üzerinden)

**PKT-4: WA Analytics** — WhatsAppAnalytics servis genişletme
- WA-6: NLP stages 4-7 (intent, FAQ, sentiment, product) C# portu + query layer + Backend proxy

**PKT-5: Platform** — Yeni servis (Integrations :7106) + genişletmeler
- GR-3.4 HB API, GR-3.6 Kargo, GR-3.14 Ads Attribution, GR-3.15 Outbound v2, GR-3.18 Dashboard, GR-3.19 Randevu Advanced

**PKT-6A: Niche Foundation** — Intent + Onboarding + Voice AI (7 GR, ~28 item)
- GR-3.1 Intent Genişletme + Oto. Etiketleme (e-ticaret)
- GR-3.2 B2B / VIP Lead Tespiti (e-ticaret)
- GR-3.5 Onboarding Otomasyonu (e-ticaret)
- GR-3.9 Diş Intent + Fiyat Pipeline (diş)
- GR-3.10 Diş Onboarding Otomasyonu (diş)
- GR-3.12 Estetik Intent + Lead Pipeline (estetik)
- GR-3.23 Voice Message AI (evrensel)
- **Bağımlılık:** Yok (PKT-5 sonrası hemen başlanabilir)

**PKT-6B: Niche Business Logic** — Outbound + İade + Lead + Yorum Kurtarma (7 GR, ~30 item)
- GR-3.7 Outbound E-ticaret Senaryoları
- GR-3.8 İade Çevirme v1
- GR-3.11 Klinik Outbound v1
- GR-3.13 Lead Management v2
- GR-3.3 Agent Assist Genişleme E-ticaret (← PKT-5 Integrations)
- GR-3.16 Negatif Yorum Kurtarma (← PKT-5 Integrations)
- GR-3.17 İade Çevirme v2 (← GR-3.8 aynı paket)
- **Bağımlılık:** PKT-5 Integrations (GR-3.3, 3.16, 3.17)

**PKT-6C: Niche Health Expansion** — Sağlık genişleme + Review Rescue + Multilingual (5 GR, ~22 item)
- GR-3.20 Tedavi Sonrası Takip
- GR-3.21 Google Yorum + Referans Motoru
- GR-3.22 Medikal Turizm Lead (AR hariç)
- GR-3.24 Proactive Review Rescue (← GR-3.8 PKT-6B + GR-3.16 PKT-6B)
- GR-3.25 Multilingual Medical Tourism (← GR-3.22 aynı paket)
- **Bağımlılık:** PKT-6B (GR-3.24 için GR-3.8+3.16 gerekli)

**PKT-7: Visual AI** — Yeni servis (VisualSearch :7111)
- GR-3C.1~3C.8: CLIP engine, katalog, web widget, tenant mgmt, WA/IG entegrasyon, analytics, Size/Fit AI

**PKT-8: Face AI** — Yeni servis (FaceAnalysis :7110)
- GR-3D.1~3D.5: MediaPipe + Claude Vision, tedavi eşleştirme, multi-lang, WA/IG, analytics + ethics

**PKT-9: Güzellik Salonu** — Config + content katmanı (yeni servis yok, PKT-6 altyapısını tüketir)
- GR-3E.1 Güzellik Intent Tanıma (saç boyama, keratin, manikür, cilt bakımı)
- GR-3E.2 Randevu Optimizasyonu (kuaför slot, işlem süresi tahmini)
- GR-3E.3 Ürün Satış Entegrasyonu (saç bakım, kozmetik cross-sell)
- GR-3E.4 Sadakat Programı (puan, kampanya, doğum günü)
- GR-3E.5 Müşteri Portföy Yönetimi (tercihler, alerji, geçmiş)
- GR-3E.6 Personel Performans (kuaför bazlı metrikler)
- GR-3E.7 Sosyal Medya Entegrasyonu (before/after, referans)
- GR-3E.8 Kampanya Motoru (sezonluk, paket, hediye çeki)
- **Bağımlılık:** PKT-5 (Outbound v2) + PKT-6A (Intent) + PKT-6B (Lead Mgmt)

**PKT-10: Eğitim** — Config + content katmanı (yeni servis yok, PKT-6 altyapısını tüketir)
- GR-3F.1 Eğitim Intent Tanıma (kayıt, ders programı, sınav, veli)
- GR-3F.2 Kayıt & Ödeme Otomasyonu (taksit, erken kayıt, burs)
- GR-3F.3 Veli İletişim Motoru (devamsızlık, not, etkinlik)
- GR-3F.4 Ders Programı Entegrasyonu (telafi, değişiklik bildirimi)
- GR-3F.5 Öğrenci Takip Sistemi (ilerleme, risk tespiti)
- GR-3F.6 Kampanya & Dönem Yönetimi (erken kayıt, yaz okulu)
- GR-3F.7 Mezun İlişki Yönetimi (kariyer, networking, referans)
- GR-3F.8 Anket & Geri Bildirim (NPS, ders değerlendirme)
- **Bağımlılık:** PKT-5 (Outbound v2) + PKT-6A (Intent) + PKT-6B (Lead Mgmt)

### WA (WhatsApp Analytics) Fazları

| Faz | İsim | Durum | Paket | Açıklama |
|-----|------|-------|-------|----------|
| WA-1 | Temizlik + Threading | ✅ 2026-02-14 | — | 01_cleaner, 02_threader, 03_stats |
| WA-2 | NLP Pipeline | ✅ 2026-02-14 | — | 04_intent, 05_faq, 06_sentiment, 07_product + shared claude_client |
| WA-3 | Training Data Export | ✅ 2026-02-14 | — | FAQ clusters + intent patterns → Knowledge DB (Phase A ile beraber) |
| WA-4 | BI Dashboard | ⬜ Sırada | **PKT-3** | Agent performans, conversion, trend raporları |
| WA-5 | C# Microservice Phase A | ✅ 2026-02-15 | — | Pipeline stages 1-3 (cleaner, threader, stats). Port 7109. Commit: 18f387f |
| WA-6 | NLP Stages 4-7 + Proxy | ✅ 2026-02-17 | **PKT-4** | NLP stages 4-7 C# portu, query layer, Backend proxy. Codex iter 7 PASS. |

---

## Recently Completed

| Slug | Completed | Description |
|------|-----------|-------------|
| PKT-2-saglik-core | 2026-02-16 | Paket 2: Saglik Core (GR-2.4 Randevu Motoru + GR-2.6 KVKK Minimum). Yeni Invekto.Appointments servis (port 7102), slot CRUD, booking, IHostedService reminder (T-48h/T-2h), KVKK 5 servis (disclaimer, photo block, medical tag, AgentAI warning). 31 dosya +9006/-15. Codex iter 1 FORCE PASS. Commit: e994e29. Plan: `arch/plans/20260215-pkt2-saglik-core.json` |
| PKT-1-ai-upgrade | 2026-02-15 | Paket 1: AI Upgrade (GR-2.2 Agent Assist v2 + GR-2.3 Multi-lang). Codex iter 3 FORCE PASS. Commit: 97d9888. Plan: `arch/plans/20260215-pkt1-ai-upgrade.json` |
| 20260214-wa-analytics-phaseA | 2026-02-15 | WA-5/6 Phase A: Invekto.WhatsAppAnalytics (Port 7109). Full C# port of pipeline stages 1-3. Streaming CSV, Turkish text normalization, SHA256 dedup, 25 outcome patterns, IAsyncEnumerable streaming, restart recovery (stale timeout + SKIP LOCKED). 10-table PostgreSQL schema, 15 error codes. 20 dosya +5417. Codex 4 iter PASS. Commit: 18f387f. Plan: `arch/plans/20260214-wa-analytics-phaseA.json` |
| 20260214-knowledge-service-phaseB | 2026-02-15 | GR-2.1 Phase B: PDF upload + PdfPig chunking, combined FAQ+chunk search, Backend proxy (JWT bridge), Dashboard Knowledge UI. 27 dosya +13413/-449. Codex 3 iter PASS. Commit: 89bbe72. Plan: `arch/plans/20260214-knowledge-service.json` |
| 20260214-knowledge-service-phaseA | 2026-02-14 | GR-2.1 Phase A: Knowledge Service core (RAG, pgvector, WA-3 import, FAQ CRUD, embeddings). 22 dosya +2615. Codex 5 iter PASS. Plan: `arch/plans/20260214-knowledge-service.json` |
| 20260214-whatsapp-analytics | 2026-02-14 | WA-2: NLP Pipeline (intent classifier, FAQ extractor, sentiment analyzer, product analyzer + shared claude_client). 8 dosya +1919. Codex 3 iter PASS. Plan: `arch/plans/20260214-whatsapp-analytics.json` |
| 20260214-idea-phase-integration | 2026-02-14 | 5 idea dokümanı roadmap phase'lerine entegre edildi (v4.5): Voice AI→3B GR-3.23, Face Analysis→3D GR-3D.1-5, Size/Fit→3C GR-3C.8, Review Rescue→3B GR-3.24, Multilingual→3B GR-3.25. Yeni phase-3d.md oluşturuldu. |
| 20260214-flow-builder-phase5 | 2026-02-14 | Flow Builder Phase 5: Production Integration. Deploy script'e FlowBuilder SPA build adimi eklendi. 1 dosya +26 -5. Codex 3 iter Q FORCE PASS. |
| 20260214-flow-builder-phase4b | 2026-02-14 | Flow Builder Phase 4b: AI/API Nodes (ai_intent, ai_faq, action_api_call). 3 handler + IntentDetector refactor + SSRF validation + 3 SPA node + 3 property editor + graph-validator + flow-summarizer. 27 dosya +1516 -104. Codex 4 iter PASS. |
| 20260214-flow-builder-phase4a | 2026-02-14 | Flow Builder Phase 4a: Pure Logic Nodes (logic_condition, logic_switch, action_delay, utility_set_variable). 4 handler + 4 SPA node + property editors + validation. 25 dosya +964 -42. Codex 3 iter Q FORCE PASS. |
| 20260213-flow-builder-phase3c | 2026-02-14 | Flow Builder Phase 3c: Validation UI + Variable Inspector + AHA #3 Ghost Path + AHA #5 Health Score. 20 dosya +746 -195. Codex 3 iter PASS. |
| 20260213-flow-builder-phase3b | 2026-02-13 | Flow Builder Phase 3b: SimulationEngine + SPA Chat Panel + AHA #4 Tek Tikla Test. 25 dosya +1461 -199. Codex 3 iter PASS. |
| 20260213-flow-builder-phase3a | 2026-02-13 | Flow Builder Phase 3a: FlowEngine v2 + Validator + Migrator + 5 NodeHandlers. 16 dosya +1942 -27. Codex 3 iter Q FORCE PASS. |
| 20260213-flow-builder-phase25 | 2026-02-13 | Flow Builder Phase 2.5 SPA Quick Wins - AHA #6 Kopya, #2 Kirmizi Kenar, #1 Canli Onizleme. Codex 2 iter PASS. |
| 20260212-flow-builder | 2026-02-13 | Flow Builder Phase 2 (API + Backend + Multi-flow + Auth) - Codex 3 iter, Q FORCE PASS. Committed + deployed. |
| 20260212-outbound-service | 2026-02-12 | GR-1.3: Invekto.Outbound broadcast & trigger engine (Port 7107) - Codex 3 iter PASS. Deployed (NSSM). |
| 20260211-testing-tooling | 2026-02-11 | Backend proxy architecture for simulator + E2E scenarios - Codex 5 iter PASS |
| 20260211-agentai-service | 2026-02-11 | GR-1.11: Invekto.AgentAI AI agent assist (Port 7105) - Codex 2 iter, Q FORCE PASS |
| 20260209-simulator | 2026-02-09 | Test & Simulation tool (Node.js, Port 4500) - Codex 3 iter PASS |
| 20260209-automation-service | 2026-02-09 | GR-1.1: Invekto.Automation chatbot/flow builder servisi (Codex 3 iter, Q FORCE PASS) |
| 20260208-integration-bridge | 2026-02-08 | GR-1.9: Invekto <-> InvektoServis API koprusu (JWT, webhook, callback, PostgreSQL) |
| 20260202-chatanalysis-integration | 2026-02-02 | WapCRM + Claude Haiku integration |
| 20260202-stage0-review-fixes | 2026-02-02 | Ops auth + log reader fixes |
| 20260202-stage0-scaffold | 2026-02-02 | Stage-0 scaffold: Backend + ChatAnalysis + Shared |
| 20260201-initial-setup | 2026-02-01 | Proje workflow yapısı kuruldu |

---

## Blocked

| Slug | Blocked Since | Reason | Waiting For |
|------|---------------|--------|-------------|
| (none) | - | - | - |

---

## Stage-0 Checklist

| Item | Status |
|------|--------|
| Solution yapısı | ✅ |
| Invekto.Shared | ✅ |
| Invekto.ChatAnalysis | ✅ |
| Invekto.Backend | ✅ |
| /health endpoint | ✅ |
| /ops endpoint | ✅ |
| JSON Lines logger | ✅ |
| 600ms timeout | ✅ |
| 0 retry | ✅ |
| Windows Service ready | ✅ |
| Build PASS | ✅ |
| WapCRM integration | ✅ |
| Claude Haiku analysis | ✅ |
| Sentiment/Category API | ✅ |

---

## Usage

### Yeni İş Başlatma
```markdown
| {slug} | IN_PROGRESS | {tarih} | {açıklama} |
```

### İş Tamamlama
1. In Progress'ten kaldır
2. Recently Completed'a ekle

### İş Engellenirse
1. In Progress'ten Blocked'a taşı
2. Waiting For alanını doldur
