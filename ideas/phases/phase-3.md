# Phase 3 — Niche Genişleme + Outbound v2

> **Hafta:** 17-24
> **MRR Hedefi:** 500-800K TL
> **Müşteri Hedefi:** 100+
> **Bağımlılık:** Phase 2 tamamlanmış olmalı
> **Durum:** ⬜ Başlamadı
>
> **v4.3 Bölünme (2026-02-14):** Phase 3 22 GR'ye ulaştığı için iki alt-phase'e bölündü.
> Detaylar artık ayrı dosyalarda:

---

## Alt-Phase'ler

| Alt-Phase | Dosya | GR Sayısı | Hafta | Odak |
|-----------|-------|-----------|-------|------|
| **Phase 3A** | [phase-3a.md](phase-3a.md) | 6 GR | 17-20 | Platform Enablers — Integrations servisi, Outbound v2, Randevu Advanced, Dashboard, Ads Attribution |
| **Phase 3B** | [phase-3b.md](phase-3b.md) | 16 GR | 21-24 | Niche Derinleştirme — E-ticaret/Diş/Estetik intents, lead pipeline, outbound senaryoları, sağlık genişleme |

---

## Bağımlılık Akışı

```
Phase 2 (AI + Kritik Niche)
    │
    ▼
Phase 3A (Platform Enablers)
    │  Integrations :7106 doğar
    │  Outbound v2, Dashboard genişleme
    │  Randevu Advanced, Ads Attribution
    │
    ▼
Phase 3B (Niche Derinleştirme)
    │  E-ticaret: intent + B2B + iade v1/v2
    │  Diş: intent + onboarding + outbound
    │  Estetik: intent + lead management
    │  Sağlık: tedavi takip + yorum + medikal turizm
    │
    ▼
Phase 4 (Enterprise)
```

---

## GR Dağılımı

### Phase 3A — Platform Enablers (6 GR)
| GR | Ad |
|----|----|
| GR-3.4 | Hepsiburada API + Integrations servisi |
| GR-3.6 | Kargo Entegrasyonu |
| GR-3.14 | Ads Attribution (Basit + Full) |
| GR-3.15 | Outbound Engine v2 |
| GR-3.18 | Dashboard Genişletme |
| GR-3.19 | Randevu Motoru v2 (Advanced) |

### Phase 3B — Niche Derinleştirme (16 GR)
| GR | Ad | Sektör |
|----|----|----|
| GR-3.1 | Intent Genişletme + Oto. Etiketleme | E-ticaret |
| GR-3.2 | B2B / VIP Lead Tespiti | E-ticaret |
| GR-3.3 | Agent Assist Genişleme | E-ticaret |
| GR-3.5 | Onboarding Otomasyonu | E-ticaret |
| GR-3.7 | Outbound E-ticaret Senaryoları | E-ticaret |
| GR-3.8 | İade Çevirme v1 | E-ticaret |
| GR-3.9 | Diş Intent + Fiyat Pipeline | Diş |
| GR-3.10 | Diş Onboarding Otomasyonu | Diş |
| GR-3.11 | Klinik Outbound v1 | Diş |
| GR-3.12 | Estetik Intent + Lead Pipeline | Estetik |
| GR-3.13 | Lead Management v2 | Estetik |
| GR-3.16 | Negatif Yorum Kurtarma | Platform |
| GR-3.17 | İade Çevirme v2 | Platform |
| GR-3.20 | Tedavi Sonrası Takip | Sağlık |
| GR-3.21 | Google Yorum + Referans Motoru | Sağlık |
| GR-3.22 | Medikal Turizm Lead (AR hariç) | Sağlık |

---

## Notlar

- **v4.1:** Eski Phase 2 niche GR'ları + eski Phase 3 kalan GR'ları birleştirildi
- **v4.2:** Phase 5'ten 3 sağlık GR taşındı, ads attribution birleştirildi → 22 GR
- **v4.3:** 22 GR → 3A (6 platform) + 3B (16 niche) bölünmesi
- GR numaraları değişmedi — sadece dosya organizasyonu değişti
