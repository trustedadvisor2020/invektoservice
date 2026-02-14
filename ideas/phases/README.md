# Invekto Phase Dosyaları

> Son güncelleme: 2026-02-14 (v4.5 — 5 yeni idea entegre: Voice AI, Face Analysis, Size/Fit, Review Rescue, Multilingual)
> Ana kaynak: [../roadmap.md](../roadmap.md)

---

## Genel Durum

| Phase | Dosya | Hafta | MRR Hedefi | Müşteri | Durum |
|-------|-------|-------|------------|---------|-------|
| **0** | [phase-0.md](phase-0.md) | 1-2 | 50-200K (koruma) | 50+ | ✅ Tamamlandı (2026-02-15) |
| **1** | [phase-1.md](phase-1.md) | 3-8 (gerçekçi: 10-15) | 200-300K | 60+ | ✅ Core Tamamlandı (polish ertelendi, 1 tenant production pending) |
| **2** | [phase-2.md](phase-2.md) | 9-16 | 300-500K | 75+ | ⬜ Başlamadı — **AI Derinleştirme + Kritik Niche (Hibrit v4.1)** |
| **3** | [phase-3.md](phase-3.md) | 17-24 | 500-800K | 100+ | ⬜ Başlamadı — **Niche Genişleme + Outbound v2 (v4.3: 3A/3B bölünmüş)** |
| ↳ 3A | [phase-3a.md](phase-3a.md) | 17-20 | 500-650K | 85+ | ⬜ Başlamadı — Platform Enablers (6 GR) |
| ↳ 3B | [phase-3b.md](phase-3b.md) | 21-24 | 650-800K | 100+ | ⬜ Başlamadı — Niche Derinleştirme + Voice/Review/Multilingual (19 GR) |
| ↳ 3C | [phase-3c.md](phase-3c.md) | 25-28 | 800K-1M | 110+ | ⬜ Başlamadı — **Visual Product Search + Size/Fit AI** (8 GR) |
| ↳ 3D | [phase-3d.md](phase-3d.md) | 29-32 | 1M-1.2M | 120+ | ⬜ Başlamadı — **Face Analysis AI** (5 GR) |
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
| `Invekto.VisualSearch` | 7109 | Phase 3C |
| `Invekto.FaceAnalysis` | 7110 | Phase 3D |
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
| WA-3 | Training Data Export | ⬜ Sirada | FAQ → Knowledge DB, intent → Flow Builder template |
| WA-4 | BI Dashboard | ⬜ Sirada | Agent performans, conversion, trend raporlari |
| WA-5 | C# Microservice | ⬜ Bekliyor | Pipeline'i InvektoServices mikro servis olarak sarmala |
| WA-6 | SQL Server Entegrasyon | ⬜ Bekliyor | CSV → DB bulk insert, live query |

**Cross-dependency:** WA-3 + RP-2 GR-2.1 (Knowledge Service) BERABER yapilacak.

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
