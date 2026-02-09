# Invekto Phase Dosyaları

> Son güncelleme: 2026-02-08
> Ana kaynak: [../roadmap.md](../roadmap.md)

---

## Genel Durum

| Phase | Dosya | Hafta | MRR Hedefi | Müşteri | Durum |
|-------|-------|-------|------------|---------|-------|
| **0** | [phase-0.md](phase-0.md) | 1-2 | 50-200K (koruma) | 50+ | ⬜ Başlamadı |
| **1** | [phase-1.md](phase-1.md) | 3-8 (gerçekçi: 10-15) | 200-300K | 60+ | ⬜ Başlamadı |
| **2** | [phase-2.md](phase-2.md) | 9-16 | 300-500K | 75+ | ⬜ Başlamadı |
| **3** | [phase-3.md](phase-3.md) | 17-24 | 500-800K | 100+ | ⬜ Başlamadı |
| **4** | [phase-4.md](phase-4.md) | 25-32 | 800K-1.2M | 130+ | ⬜ Başlamadı |
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
| `Invekto.Integrations` | 7106 | Phase 2 |
| `Invekto.Knowledge` | 7104 | Phase 3 |
| `Invekto.Audit` | 7103 | Phase 4 |

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
