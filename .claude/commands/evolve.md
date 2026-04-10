---
description: Yuksek confidence pattern'i skill'e donustur. /evolve {pattern-id}
---

# /evolve - Pattern to Skill Evolution

> Yeterli confidence ve evidence'a sahip pattern'leri skill dosyasina donusturur.

======================================================================

## KULLANIM

```
/evolve {pattern-id}
```

Ornek:
```
/evolve CP-001
/evolve RP-001
```

======================================================================

## EVOLUTION KRITERLERI

Bir pattern skill'e donusturulebilmesi icin:

| Kriter | Gerekli Deger |
|--------|---------------|
| Confidence | >= 8 |
| Evidence Count | >= 3 |
| Son Gorulen | Son 30 gun |
| Context Cesitliligi | 2+ farkli dosya/servis |

**Q Override:** `force evolution` ile kriterler atlanabilir.

======================================================================

## ADIMLAR

### 1. Pattern Dogrulama

```
+--------------------------------------------------------+
|  EVOLUTION CHECK: {pattern-id}                          |
|                                                         |
|  Pattern: {pattern aciklamasi}                         |
|  Confidence: {X}/10 {>= 8 ? OK : FAIL}                 |
|  Evidence: {X} {>= 3 ? OK : FAIL}                      |
|  Last Seen: {tarih} {< 30 gun ? OK : FAIL}             |
|                                                         |
|  Status: {ELIGIBLE | NOT ELIGIBLE}                      |
+--------------------------------------------------------+
```

### 2. Skill Tasarimi

Eligible ise skill tasarla:

```
+--------------------------------------------------------+
|  SKILL DESIGN: {pattern-id} -> {skill-name}.md         |
|                                                         |
|  Dosya: .claude/skills/{skill-name}.md                 |
|                                                         |
|  Icerik:                                                |
|  - Kural: {net kural ifadesi}                          |
|  - Trigger: {ne zaman uygulanir}                       |
|  - Ornek: {kod ornegi}                                 |
|  - Kaynak: instincts.md#{pattern-id}                   |
|                                                         |
|  Olusturulsun mu? (evet / hayir / duzenle)             |
+--------------------------------------------------------+
```

### 3. Skill Olusturma

Q onayi ile:

1. `.claude/skills/{skill-name}.md` dosyasi olustur
2. `arch/instincts.md`'de pattern'i "Evolved" olarak isaretle
3. `CLAUDE.md`'ye referans ekle (opsiyonel)

======================================================================

## SKILL DOSYA FORMATI

```markdown
---
description: {kisa aciklama}
trigger: {ne zaman aktif olur}
source: instincts.md#{pattern-id}
---

# {Skill Adi}

## Kural

{Net kural ifadesi - tek cumle}

## Ne Zaman Kullan

{Trigger kosullari listesi}

## Ornek

\`\`\`{dil}
// Dogru kullanim
{kod ornegi}

// Yanlis kullanim (YAPMA)
{yanlis ornek}
\`\`\`

## Kaynak

- Pattern ID: {pattern-id}
- Confidence: {X}/10
- Evidence: {X} olay
- Evolved: {tarih}
```

======================================================================

## SKILL KATEGORILERI

| Pattern Prefix | Skill Kategorisi | Ornek Skill |
|----------------|------------------|-------------|
| CP-xxx (Code) | .claude/skills/code/ | ef-concurrent-safety.md |
| RP-xxx (Risk) | .claude/skills/risk/ | microservice-isolation.md |
| PP-xxx (Process) | .claude/skills/process/ | shared-dto-checklist.md |

======================================================================

## ORNEK AKIS

```
Q: /evolve CP-001

Agent:
+--------------------------------------------------------+
|  EVOLUTION CHECK: CP-001                                |
|                                                         |
|  Pattern: IDbContextFactory + await using concurrent   |
|           EF icin                                       |
|  Confidence: 9/10 [OK]                                  |
|  Evidence: 4 [OK]                                       |
|  Last Seen: 2026-04-05 [OK]                            |
|                                                         |
|  Status: ELIGIBLE                                       |
+--------------------------------------------------------+

+--------------------------------------------------------+
|  SKILL DESIGN: CP-001 -> ef-concurrent-safety.md       |
|                                                         |
|  Dosya: .claude/skills/code/ef-concurrent-safety.md    |
|                                                         |
|  Kural: Concurrent EF operasyonlarinda                 |
|         IDbContextFactory + await using kullan         |
|                                                         |
|  Trigger:                                               |
|  - DbContext kullanilan scheduler/worker kodu          |
|  - Birden fazla thread ayni DbContext'i erisiyor       |
|  - HttpClient/Background service'de EF Core            |
|                                                         |
|  Olusturulsun mu? (evet / hayir / duzenle)             |
+--------------------------------------------------------+

Q: evet

Agent:
- .claude/skills/code/ef-concurrent-safety.md olusturuldu
- instincts.md guncellendi (CP-001 -> Evolved)
- Skill artik aktif!
```

======================================================================

## Q OVERRIDE

| Komut | Etki |
|-------|------|
| `force evolution` | Kriterleri atla, direkt olustur |
| `duzenle: {degisiklik}` | Skill icerigini degistir |
| `iptal` | Islemi durdur |
| `claude.md ekle` | CLAUDE.md'ye de referans ekle |
