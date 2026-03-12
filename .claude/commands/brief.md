# /brief - Session Brief (v1.0)

> Session basinda otomatik ozet.

======================================================================

## KULLANIM

```
/brief          -> Bugunun ozeti
/brief 7        -> Son 7 gunun ozeti
/brief backend  -> Backend context'ine ozel ozet
```

======================================================================

## STEP 1: VERI TOPLAMA

Su dosyalari oku:
1. `arch/lessons-learned.md` -> Son N gunun hatalari
2. `arch/session-memory.md` -> Son session durumu

======================================================================

## STEP 2: FILTRELEME

### Zaman Filtresi
- Default: Son 7 gun
- Parametre ile degistirilebilir: `/brief 14`

### Context Filtresi (opsiyonel)
- `/brief backend` -> Sadece Backend/API pattern'leri
- `/brief webchat` -> Sadece WebChat pattern'leri
- `/brief db` -> Sadece PostgreSQL/DB pattern'leri

======================================================================

## STEP 3: OZET OLUSTUR

```markdown
## SESSION BRIEF ({tarih})

### Son {N} Gunun Kritik Hatalari
| Tarih | Kategori | Hata | Cozum |
|-------|----------|------|-------|
| ... son 5 hata ... |

### Bugun Hatirla
1. {En kritik kural 1}
2. {En kritik kural 2}
3. {En kritik kural 3}
```

======================================================================

## STEP 4: CONTEXT DETECTION

Eger context belirtilmemisse, su sinyallerden tahmin et:

| Sinyal | Context |
|--------|---------|
| Son commit Backend dosyalarinda | backend |
| Son commit WebChat dosyalarinda | webchat |
| Son commit SQL dosyalarinda | db |
| Belirsiz | all |
