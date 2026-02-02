---
description: Oturum duzeltmelerini lessons-learned.md'ye kaydet. Hatalardan ogrenmek icin kullan.
---

# /learn - Session Learning (v1.0)

> **Persist After Compact:** Oturum sifirlaninca bile calisir.

Oturumdaki Q duzeltmelerini analiz edip `arch/lessons-learned.md`'ye kaydeder.

======================================================================

## KULLANIM

### 1. `/learn` - Oturum Analizi

Tum oturumu analiz et, ogrenilecek seyleri listele, Q onayiyla kaydet.

### 2. `/learn "konu"` - Spesifik Konu

Belirtilen konuyu lessons-learned.md'ye ekle.

======================================================================

## STEP 1: SINYAL TESPITI

Oturumda sunlari tara:

### Q Duzeltmeleri
- "hayir", "yanlis", "aslinda", "degil", "oyle degil"
- Q'nun kod degistirme talepleri
- Rejected approach'lar

### Tekrarlayan Pattern'ler
- 2+ kez ayni hata/duzeltme
- Ayni dosyada birden fazla fix
- Benzer soru tekrari

### Onaylanan Yaklasimlar
- "evet", "dogru", "aynen", "tamam", "guzel"
- Build PASS sonrasi Q memnuniyeti
- Codex PASS alan pattern'ler

### Hata -> Cozum Zincirleri
- Build FAIL -> fix -> PASS
- Codex FAIL -> fix -> PASS
- Q duzeltme -> uygulama -> onay

======================================================================

## STEP 2: FILTRELEME

Her sinyal icin su sorulari sor:

```
+--------------------------------------------------------+
|  [?] Proje-spesifik mi? (Bu projeye ozgu)              |
|  [?] Tekrarlanabilir mi? (Gelecekte karsilasilir)      |
|  [?] Yeni bilgi mi? (lessons-learned.md'de yok)        |
|  [?] Actionable mi? (Somut onlem alinabilir)           |
+--------------------------------------------------------+
```

**4'unden 3'u EVET ise -> KABUL**
**Aksi halde -> REDDET**

======================================================================

## STEP 3: KATEGORIZASYON

| Sinyal Tipi | Hedef Tablo |
|-------------|-------------|
| Hata yaptim, Q duzeltti | Common Mistakes |
| Bu yaklasim ise yaradi | Patterns That Work |
| Bunu yapma | Anti-Patterns to Avoid |
| Review'da fark edildi | Code Review Insights |

======================================================================

## STEP 4: ONIZLEME

Q'ya goster:

```md
## /learn Bulgulari

### Eklenecek: Common Mistakes
| Date | Category | Mistake | Solution | Prevention |
|------|----------|---------|----------|------------|
| {tarih} | {kategori} | {hata} | {cozum} | {onlem} |

### Eklenecek: Patterns That Work
| Pattern | Where Used | Why It Works |
|---------|------------|--------------|
| {pattern} | {nerede} | {neden} |

### Reddedilen (gerekceli):
- "{sinyal}" -> Genel best practice, proje-spesifik degil

**Onayliyor musun?** (evet / hayir / duzenle)
```

======================================================================

## STEP 5: KAYIT

Q onayi alindiktan sonra:

1. `arch/lessons-learned.md` dosyasini oku
2. Ilgili tabloya yeni satir ekle
3. Tarih formati: `YYYY-MM-DD`
4. Duplicate check: Ayni mistake zaten varsa ekleme

======================================================================

## FORMAT KURALLARI

### Common Mistakes Satiri
```
| {YYYY-MM-DD} | {Category} | {Kisa hata aciklamasi} | {Cozum} | {Gelecekte onlem} |
```

**Category secenekleri:** DB, SQL, API, UI, Auth, Config, Codex, Risk

### Patterns That Work Satiri
```
| {Pattern adi} | {Kullanilan yer} | {Neden ise yariyor} |
```

### Anti-Patterns Satiri
```
| {Anti-pattern adi} | {Problem} | {Daha iyi yaklasim} |
```

======================================================================

## ENTEGRASYON

### Auto Workflow ile
- `/learn` auto workflow'u KESMEZ
- **Her DONE sonrasi:** Agent oturumda ogrenilecek sey varsa hatirlatir
- **Her FAIL sonrasi:** Agent bu hatayi kaydetmek isteyip istemedigini sorar

### /rev ile
- `/rev verdict FAIL` sonrasi `/learn` onerilebilir
- 3 iteration'da ayni hata -> otomatik oneri

======================================================================

## KISITLAMALAR

```
+--------------------------------------------------------+
|  X Otomatik ekleme yapmaz (her zaman Q onayi)          |
|  X Genel best practice eklemez                         |
|  X Framework/tool dokumantasyonu eklemez               |
+--------------------------------------------------------+
```

======================================================================

## Q OVERRIDE

| Q Komutu | Etki |
|----------|------|
| `STOP` | Islemi durdur |
| `duzenle: {degisiklik}` | Oneriyi degistir |
| `sadece mistakes` | Sadece Common Mistakes ekle |
| `skip` | Bu oturumda /learn atla |
