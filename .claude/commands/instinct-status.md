---
description: Mevcut instinct durumunu goster. Ogrenilen pattern'lerin ozeti.
---

# /instinct-status - Instinct Status Report

> Mevcut ogrenilmis pattern'lerin ozetini gosterir.

======================================================================

## KULLANIM

```
/instinct-status
```

======================================================================

## CIKTI FORMATI

`arch/instincts.md` dosyasini oku ve su formatta ozetle:

```
+============================================================+
|               INVEKTOSERVICES INSTINCT STATUS              |
+============================================================+

## Active Instincts (Confidence 7+)

| Kategori | Sayi | Ort. Confidence | En Guclu |
|----------|------|-----------------|----------|
| Code Patterns (CP) | X | X.X | CP-001 |
| Risk Patterns (RP) | X | X.X | RP-001 |
| Process Patterns (PP) | X | X.X | PP-001 |

## Evolution Adaylari (Conf >= 8, Evidence >= 3)

| ID | Pattern | Conf | Evidence | Skill Onerisi |
|----|---------|------|----------|---------------|
| CP-001 | IDbContextFactory | 9 | 4 | ef-concurrent-safety.md |
| ... | ... | ... | ... | ... |

## Evolving Instincts (Confidence 4-6)

| ID | Pattern | Conf | Gerekli Evidence |
|----|---------|------|------------------|
| EV-001 | ... | 6 | +2 |

## Weak Signals (Confidence 1-3)

| ID | Signal | Conf | First Seen |
|----|--------|------|------------|
| WS-001 | ... | 3 | YYYY-MM-DD |

## Ozet

- Toplam Aktif: X
- Toplam Evolving: X
- Toplam Weak: X
- Son Guncelleme: YYYY-MM-DD

+============================================================+
```

======================================================================

## AKSIYONLAR

Rapor sonunda Q'ya sun:

```
Yapmak istediginiz bir sey var mi?
1. `/evolve {id}` - Pattern'i skill'e donustur
2. `/learn` - Yeni pattern ekle
3. Cikis
```

======================================================================

## ORNEK

```
Q: /instinct-status

Agent:
+============================================================+
|               INVEKTOSERVICES INSTINCT STATUS              |
+============================================================+

## Active Instincts (Confidence 7+)

| Kategori | Sayi | Ort. Confidence | En Guclu |
|----------|------|-----------------|----------|
| Code Patterns (CP) | 0 | - | - |
| Risk Patterns (RP) | 0 | - | - |
| Process Patterns (PP) | 0 | - | - |

## Ozet

- Toplam Aktif: 0
- Toplam Evolving: 0
- Toplam Weak: 0
- Son Guncelleme: (yeni kurulum)

Not: Henuz ogrenilmis pattern yok. `/learn` ile eklemeye basla
     veya session sonunda /wrap otomatik lesson kaydeder.

+============================================================+

Yapmak istediginiz bir sey var mi?
1. `/learn` - Yeni pattern ekle
2. Cikis
```
