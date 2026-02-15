# Invekto Phase Dosyaları

> Son güncelleme: 2026-02-15 (v5.0 — 8 Paket Execution Stratejisi)
> Ana kaynak: [../roadmap.md](../roadmap.md)
> Execution detay: `arch/active-work.md`

---

## Yürütme Stratejisi (v5.0 — 2026-02-15)

> **Karar:** Tekli GR döngüsü yerine **8 paket** halinde yürütme.
> Her paket: 1 interview + 1 plan + sıralı dev + 1 build + 1 Codex review.
> Overhead %60 azalır (24 döngü → 8 döngü), toplam süre ~%50 kısalır.
> Detay: `arch/active-work.md` → "Execution Queue — 8 Paket Stratejisi"

| PKT | Paket | Phase | İçerik |
|-----|-------|-------|--------|
| 1 | AI Upgrade | 2 | GR-2.2 + GR-2.3 |
| 2 | Sağlık Core | 2 | GR-2.4 + GR-2.6 |
| 3 | Ops Dashboard | 2 | GR-2.5 + WA-4 |
| 4 | WA Analytics | 2 | WA-6 |
| 5 | Platform | 3A | GR-3.4, 3.6, 3.14, 3.15, 3.18, 3.19 |
| 6 | Niche | 3B | 19 GR (e-ticaret/diş/estetik/sağlık/voice) |
| 7 | Visual AI | 3C | GR-3C.1~3C.8 |
| 8 | Face AI | 3D | GR-3D.1~3D.5 |

---

## Genel Durum

| Phase | Dosya | Hafta | MRR Hedefi | Müşteri | Durum |
|-------|-------|-------|------------|---------|-------|
| **0** | [phase-0.md](phase-0.md) | 1-2 | 50-200K (koruma) | 50+ | ✅ Tamamlandı (2026-02-15) |
| **1** | [phase-1.md](phase-1.md) | 3-8 (gerçekçi: 10-15) | 200-300K | 60+ | ✅ Core Tamamlandı (polish ertelendi, 1 tenant production pending) |
| **2** | [phase-2.md](phase-2.md) | 9-16 | 300-500K | 75+ | 🔄 Kısmen Tamamlandı — GR-2.1 ✅, kalan: PKT-1~4 |
| **3** | [phase-3.md](phase-3.md) | 17-24 | 500-800K | 100+ | ⬜ Başlamadı — PKT-5~8 ile tamamlanacak |
| ↳ 3A | [phase-3a.md](phase-3a.md) | 17-20 | 500-650K | 85+ | ⬜ Başlamadı — **PKT-5** Platform Enablers (6 GR) |
| ↳ 3B | [phase-3b.md](phase-3b.md) | 21-24 | 650-800K | 100+ | ⬜ Başlamadı — **PKT-6** Niche Derinleştirme (19 GR) |
| ↳ 3C | [phase-3c.md](phase-3c.md) | 25-28 | 800K-1M | 110+ | ⬜ Başlamadı — **PKT-7** Visual AI (8 GR, port 7111) |
| ↳ 3D | [phase-3d.md](phase-3d.md) | 29-32 | 1M-1.2M | 120+ | ⬜ Başlamadı — **PKT-8** Face AI (5 GR, port 7110) |
| **4** | [phase-4.md](phase-4.md) | 33-40 | 1.2-1.5M | 140+ | ⬜ Başlamadı |
| **5** | [phase-5.md](phase-5.md) | 33-40 | 1.2-2M | 170+ | ⬜ Başlamadı |
| **6** | [phase-6.md](phase-6.md) | 41-48 | 2M+ | 200+ | ⬜ Başlamadı |
| **7** | [phase-7.md](phase-7.md) | 49+ | 2M++ | 200++ | ⬜ Başlamadı |

---

## Mikro Servis Doğuş Haritası

| Servis | Port | Doğduğu Phase |
|--------|------|---------------|
| `Invekto.Backend` | 5000 | Mevcut |
| `Invekto.ChatAnalysis` | 7101 | Mevcut |
| `Invekto.Automation` | 7108 | Phase 1 |
| `Invekto.AgentAI` | 7105 | Phase 1 |
| `Invekto.Outbound` | 7107 | Phase 1 |
| `Invekto.Knowledge` | 7104 | Phase 2 |
| `Invekto.Integrations` | 7106 | Phase 3A |
| `Invekto.VisualSearch` | 7111 | Phase 3C (PKT-7) — ~~7109~~ → 7111 (WA Analytics çakışma fix) |
| `Invekto.FaceAnalysis` | 7110 | Phase 3D (PKT-8) |
| `Invekto.Audit` | 7103 | Phase 4 |

---

## WhatsApp Analytics Fazlari (WA-*)

> **KURAL:** WA = WhatsApp Analytics, RP = Roadmap Phase. Ayri takip edilir!
> **Kaynak:** `tools/whatsapp-analyzer/` (Python), ebrumoda.com 2.1M mesaj
> **Execution Queue:** `arch/active-work.md`

| Faz | Isim | Durum | Cikti |
|-----|------|-------|-------|
| WA-1 | Temizlik + Threading | ✅ 2026-02-14 | cleaned_messages.csv, conversations.csv, metadata.json |
| WA-2 | NLP Pipeline | ✅ 2026-02-14 | intent_classifications.csv, faq_pairs.csv, faq_clusters.json, sentiment.csv, product_analysis.csv |
| WA-3 | Training Data Export | ✅ 2026-02-14 | FAQ → Knowledge DB (GR-2.1 Phase A ile beraber). Commit: 385d3e0 |
| WA-4 | BI Dashboard | ⬜ Sırada | Agent performans, conversion, trend raporları. **PKT-3** |
| WA-5 | C# Microservice Phase A | ✅ 2026-02-15 | Pipeline stages 1-3 (Port 7109). Commit: 18f387f |
| WA-6 | NLP Stages 4-7 + Proxy | ⬜ Bekliyor | NLP stages 4-7 C# portu, query layer, Backend proxy. **PKT-4** |

**Tamamlanan cross-dependency:** WA-3 + RP-2 GR-2.1 BERABER yapıldı ✅.

---

## Kullanım

Her phase dosyasında:

1. **Durum Takibi tablosu** — alt gereksinimlerin anlık durumunu gösterir
2. **Gereksinimler** — `[ ]` checkbox'ları ile adım adım takip
3. **Çıkış Kriterleri** — bir sonraki phase'e geçiş şartları

### Durum Güncelleme Kuralları

| Sembol | Anlam |
|--------|-------|
| ⬜ Başlamadı | Henüz başlanmadı |
| 🔄 Devam Ediyor | Üzerinde çalışılıyor |
| ✅ Tamamlandı | Tamamlandı + tarih yazıldı |
| 🚫 Bloke | Engel var — notlarda açıklama |

### Bir Phase Tamamlandığında

1. Phase dosyasındaki tüm `[ ]` → `[x]` olmalı
2. Durum Takibi tablosundaki tüm satırlar `✅ Tamamlandı` olmalı
3. Çıkış Kriterleri'ndeki tüm `[ ]` → `[x]` olmalı
4. Bu README'deki Genel Durum tablosunda phase durumu `✅ Tamamlandı` olarak güncellenmeli
5. Sonraki phase başlatılabilir
