---
name: sync-check
description: Checks arch/ docs against actual codebase for staleness. Use when Q says "sync check", "docs stale?", "verify arch/", or at session start.
disable-model-invocation: true
---

# Arch Sync Checker v1.0

## Gorev

arch/ dokumanlarinin kodla tutarliligini kontrol et. $ARGUMENTS varsa sadece belirtilen alani kontrol et.

## Kontrol Listesi

### 1. DB Schema Sync
- `arch/db/*.sql` dosyalarindaki tablolari oku
- Kodda (*.cs) kullanilan tablo/kolon adlarini tara (Explore subagent kullan)
- Kodda olup schema'da olmayan tablo/kolon = **STALE UYARI**

### 2. Error Code Sync
- `arch/errors.md` dosyasindaki INV-xxx kodlarini listele
- Kodda (*.cs) kullanilan error kodlarini tara
- Kodda olup errors.md'de olmayan kod = **STALE UYARI**
- errors.md'de olup kodda kullanilmayan kod = **ORPHAN UYARI**

### 3. Endpoint Sync
- `arch/endpoints.md` dosyasindaki endpoint listesini oku
- Kodda tanimli controller endpoint'lerini tara
- Uyumsuzluk = **STALE UYARI**

### 4. Session Memory Freshness
- `arch/session-memory.md` icindeki "Son guncelleme" tarihini kontrol et
- 7 gunden eskiyse = **STALE UYARI**

### 5. Quality Grades Check
- `arch/quality-grades.md` icindeki "Son guncelleme" tarihini kontrol et
- Son paket sonrasi guncellenmemisse = **UPDATE NEEDED**

## Output Format

```
SYNC CHECK REPORT
=================
DB Schema:      [OK | X stale items]
Error Codes:    [OK | X missing, Y orphan]
Endpoints:      [OK | X stale items]
Session Memory: [OK | STALE (X days old)]
Quality Grades: [OK | UPDATE NEEDED]

DETAILS:
- [item details if any issues found]

ACTIONS NEEDED:
- [specific fix instructions]
```

## Kurallar
- Sadece READ islemleri yap, hicbir dosya DEGISTIRME
- Sorunlar bulunursa Q'ya rapor et, fix icin onay bekle
- Explore subagent kullan (context koruma icin)
- Cross-service tarama gerekiyorsa paralel Explore agent'lar calistir