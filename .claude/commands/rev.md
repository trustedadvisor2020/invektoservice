---
description: Codex review hazırlığı - JSON güncelleme + verdict işleme
---

# /rev - Codex Review Hazırlığı (v3.0)

> **🔄 PERSIST AFTER COMPACT:** Session sıfırlansa bile staged değişiklikler Codex review gerektirir.

Codex is a **reviewer only**.
Codex never implements code.
Codex **DOSYA DEĞİŞTİRMEZ** - sadece metin output verir.

======================================================================

## KULLANIM MODLARI

### 1. `/rev` - Build PASS Sonrası (Review Hazırlığı)

Build PASS olduktan sonra çalıştırılır. JSON plan dosyasını günceller.

**Preconditions (HARD FAIL):**

```
1. git diff --cached --name-status boş mu?
   → Boşsa: "Staged changes yok. Önce git add yapın."

2. Plan JSON dosyası var mı? (arch/plans/{slug}.json)
   → Yoksa: "Plan dosyası bulunamadı."

3. Build PASS kanıtı var mı? (build.timestamp)
   → Yoksa: "Build kanıtı yok. Önce build çalıştırın."

4. allowed_files scope kontrolü
   → Diff'te olup allowed_files'ta olmayan dosya varsa WARN
```

**JSON Güncellemeleri:**

```json
{
  "status": "REVIEW",
  "git_diff": {
    "patch_truncated": "İlk 51200 byte",
    "sha256": "Tam diff'in hash'i",
    "full_path": "arch/plans/diffs/{slug}.diff",
    "stats": {
      "insertions": 42,
      "deletions": 10,
      "files_count": 3
    }
  },
  "files_changed": [
    { "path": "file.ts", "is_new": false }
  ],
  "updated_at": "2026-02-01T12:00:00Z"
}
```

**Diff Dosyası:**

```
arch/plans/diffs/{slug}.diff → Truncate edilmemiş tam diff
```

**Q'ya Minimal Prompt:**

```
{slug-name} ---
# CODEX REVIEW REQUEST
Plan: arch/plans/{slug}.json
{RISK} :{iteration}
{plan.summary}
```

======================================================================

### 2. `/rev validate` - Sadece Validation (Opsiyonel)

Sadece validation yapar, JSON güncellemez.

```
/rev validate

→ Schema validation
→ Coverage check
→ Preconditions check
→ PASS/FAIL raporu
```

======================================================================

### 3. `/rev verdict <PASS|FAIL|UNKNOWN> [issue]` - Verdict İşleme

Q, Codex output'unu DevAgent'a ilettiğinde kullanılır.

**Kullanım:**

```bash
/rev verdict PASS
/rev verdict FAIL "CQ2 failed: silent failure in catch block"
/rev verdict UNKNOWN
```

**KURAL: FAIL durumunda issue ZORUNLU!**

```
❌ /rev verdict FAIL              → ERROR: blocking_issues boş olamaz
✅ /rev verdict FAIL "CQ2 failed: silent failure in catch block"
```

**Iteration Artışı:**

```
iteration SADECE /rev verdict çağrısında artar:

/rev (review hazırlığı)     → iteration'a DOKUNMAZ
/rev verdict PASS           → iteration++
/rev verdict FAIL           → iteration++
```

**Escalation Trigger:**

```
iteration >= 3 → JSON'a otomatik:
  "escalation_required": true
  "escalation_reason": "Max iteration (3) reached"

→ DevAgent fix YAPMAZ
→ Q kararı zorunlu
```

======================================================================

## AKIŞ ÖZETİ

```
DevAgent /rev → JSON güncellenir, diff yazılır
    ↓
Q'ya: "Codex review: arch/plans/{slug}.json"
    ↓
Q prompt'u Codex'e yapıştırır
    ↓
Codex 2 BLOK output verir (DOSYA DEĞİŞTİRMEZ!)
    ↓
Q Codex output'unu DevAgent'a iletir
    ↓
DevAgent /rev verdict PASS|FAIL|UNKNOWN
    ↓
JSON verdict güncellenir
    ↓
PASS → commit → DONE
FAIL → fix → /rev (max 3 iter)
UNKNOWN → Q escalate
```

======================================================================

## KRİTİK KURALLAR

```
┌─────────────────────────────────────────────────────┐
│  1. Codex DOSYA DEĞİŞTİRMEZ!                        │
│     → JSON okur, 2 blok TEXT output verir           │
│                                                      │
│  2. verdict.* alanlarını KİM doldurur?              │
│     → DevAgent (Q'dan aldığı bilgiyle)              │
│                                                      │
│  3. FAIL + boş blocking_issues = ERROR!             │
│     → DevAgent /rev verdict FAIL yaparken issue zorunlu│
│                                                      │
│  4. iteration 3'e ulaşınca → Q escalate             │
│     → Yeni iter'a Q izni olmadan geçilemez          │
│                                                      │
│  5. Scope violation = HARD FAIL                     │
│     → allowed_files dışı değişiklik kabul edilmez   │
└─────────────────────────────────────────────────────┘
```

======================================================================

## CODEX OUTPUT FORMAT (Beklenen)

Codex 2 BLOK üretir:

```
=== CODE QUALITY GATE ===

CQ1: "Hata yakalama ve kullanıcıya geri bildirim nerede?"
Result: PASS | FAIL | UNKNOWN
Evidence: {dosya:satır + mesaj formatı}

CQ2: "Silent failure üretebilir mi?"
Result: PASS | FAIL | UNKNOWN
Evidence: {catch blokları + broad try-catch + early-return}

CQ3: "Diff minimum mu? Scope dışı refactor var mı?"
Result: PASS | FAIL | UNKNOWN
Evidence: {değişen dosya/satır sayısı}

CQ4: "Bu kod codebase'de zaten var mı? (duplicate)"
Result: PASS | FAIL | UNKNOWN
Evidence: {grep/search sonucu}

CQ5: "Codebase pattern'larına uyuyor mu?"
Result: PASS | FAIL | UNKNOWN
Evidence: {naming, error handling, dosya yapısı}

CQ6: "Performans sorunu var mı? (O(n²), N+1 query, memory leak)"
Result: PASS | FAIL | UNKNOWN
Evidence: {nested loops, döngü içi query, kapatılmayan resource}

CQ7: "Yeni TODO/HACK/FIXME eklendi mi?"
Result: PASS | FAIL | UNKNOWN
Evidence: {yeni eklenen tech debt marker'ları}

CQ8: "Breaking change var mı? (API contract, export, shared type)"
Result: PASS | FAIL | UNKNOWN
Evidence: {kaldırılan export, değişen interface}

CODE QUALITY VERDICT: PASS | FAIL

=== COVE VERIFICATION ===

Q1: {verification sorusu}
Result: PASS | FAIL | UNKNOWN
Reasoning: {kısa, somut açıklama}

Q2: {verification sorusu}
Result: PASS | FAIL | UNKNOWN
Reasoning: {kısa, somut açıklama}

Q3: {verification sorusu}
Result: PASS | FAIL | UNKNOWN
Reasoning: {kısa, somut açıklama}

CoVe VERDICT: PASS | FAIL

=== VERDICT ===

OVERALL: PASS | FAIL | UNKNOWN
BLOCKING ISSUES: [liste veya "None"]
```

======================================================================

## Q OVERRIDE

| Q Komutu | Etki |
|----------|------|
| `STOP` | Tüm işlemi durdur |
| `SKIP CODEX` | /rev'i atla, direkt commit (sadece Q izniyle) |
| `FORCE PASS` | Verdict override (sadece Q izniyle) |

======================================================================

## CANONICAL RULE

```
Codex enforces correctness.
DevAgent implements + /rev çalıştırır.
Q owns decisions + copy-paste köprüsü.
```

This rule overrides convenience and speed.
