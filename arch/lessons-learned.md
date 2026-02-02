# Lessons Learned

> Q düzeltmelerinden öğrenilen dersler. `/learn` komutuyla güncellenir.

## Common Mistakes

| Date | Category | Mistake | Solution | Prevention |
|------|----------|---------|----------|------------|
| (template) | DB | Varsayılan değer unuttum | ALTER TABLE ile eklendi | CREATE TABLE'da her zaman DEFAULT belirt |

---

## Patterns That Work

| Pattern | Where Used | Why It Works |
|---------|------------|--------------|
| Service isolation | Mikro servisler | Bağımsız deploy, kolay test |
| Shared contracts | Servisler arası | Type safety, API uyumu |

---

## Anti-Patterns to Avoid

| Anti-Pattern | Problem | Better Approach |
|--------------|---------|-----------------|
| Direct DB access between services | Tight coupling | API üzerinden iletişim |
| Shared mutable state | Race conditions | Event-driven communication |

---

## Code Review Insights

| Date | Finding | Action Taken |
|------|---------|--------------|
| (template) | Error handling eksik | Tüm catch bloklarına log eklendi |

---

## How to Add

1. `/learn` komutunu çalıştır
2. Önerilen eklemeleri incele
3. "onay" de

**KURAL:** Sadece proje-spesifik öğrenimler eklenir. Genel best practice eklenmez.
