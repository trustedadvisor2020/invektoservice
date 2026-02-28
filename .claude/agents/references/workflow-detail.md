# Workflow v5.1 Detail (Paket Bazlı + MCP Codex)

## Full Flow

```
Q paket ister (veya sıradaki paket başlar)
    ↓
AskUserQuestion ile paket scope interview (max 4 soru/batch)
    ↓
AC Gate: min 2 AC sorusu → Q confirms → AC1, AC2...
    ↓
Risk belirlenir (LOW/MEDIUM/HIGH/CRITICAL)
    ↓
Plan JSON oluşturulur (packet_id + gr_list + acceptance_criteria)
    ↓
Q onaylar → Implement (GR'ler sıralı, her GR sonrası build check)
    ↓
Build PASS → AC verification (verified=true/false)
    ↓
/rev → MCP codex_review otomatik → verdict
    ↓
PASS → commit | FAIL → fix → /rev (max 3 iter)
```

## Acceptance Criteria Gate

Interview'da gri noktalar çözüldükten sonra, plan yazmadan önce:

1. **Min 2 AC sorusu sor** (AskUserQuestion ile):
   - "Bu feature'ı ne zaman başarılı sayarız?"
   - "Kullanıcı perspektifinden: ne olmalı ki 'tamam bu çalışıyor' desin?"

2. **Q'nun cevaplarını AC formatına çevir:**
   - Her kriter: `AC1: ...`, `AC2: ...`, `AC3: ...`
   - Her kriter test edilebilir ve somut olmalı
   - Minimum 2 kriter

3. **Plan JSON'da AC:**
```json
"acceptance_criteria": [
  {"id": "AC1", "criterion": "...", "verified": false, "verification_note": null},
  {"id": "AC2", "criterion": "...", "verified": false, "verification_note": null}
]
```

## Self-Review Protocol (CQ1-CQ8 + AQ1-AQ6)

Her dosya edit sonrası kontrol et:

| # | Kontrol | Fail Sinyali |
|---|---------|--------------|
| CQ1 | Error handling nerede? | try-catch yok, hata yutulmuş |
| CQ2 | Silent failure var mı? | Boş catch, broad try-catch |
| CQ3 | Diff minimum mu? | Plan dışı dosya/satır değişikliği |
| CQ4 | Duplicate code var mı? | Aynı pattern başka yerde mevcut |
| CQ5 | Codebase pattern'larına uyuyor mu? | Naming, error handling farkı |
| CQ6 | Performans sorunu var mı? | O(n²), N+1 query, memory leak |
| CQ7 | Yeni TODO/HACK/FIXME eklendi mi? | Yeni teknik borç |
| CQ8 | Breaking change var mı? | Silinen export, değişen interface |
| AQ1 | Scale ready mi? | Binlerce eş zamanlı kullanıcı |
| AQ2 | Error mesajı spesifik mi? | INV-xxx kodu var mı? |
| AQ3 | Mevcut bir şeyi bozar mı? | Regression riski |
| AQ4 | Mikro servis sınırlarında mı? | Başka servisi etkiliyor mu? |
| AQ5 | DB-Code senkron mu? | Tablo/kolon var mı? |
| AQ6 | arch/ dokümanlarına uyuyor mu? | Contract şeması doğru mu? |

Çıktı: `Self-Review: 14/14 PASS` veya `Self-Review: CQ2 FAIL - fixing...`

## Codex Review (MCP Automated)

Codex MCP tool üzerinden 2 blok üretir:

**BLOCK 1: CODE QUALITY GATE** (CQ1-CQ8)
**BLOCK 2: CoVe VERIFICATION** (Q1-Q3+)

**Hard gate:** Herhangi bir soru FAIL veya UNKNOWN → overall verdict = FAIL

Akış:
1. DevAgent /rev çalıştırır → MCP codex_review tool çağrılır (otomatik)
2. Codex API structured JSON döner (verdict + blocking_issues + summary)
3. DevAgent verdict'i işler, Q'ya özet gösterir

## /rev Komutu

- `/rev` → JSON güncelle, MCP codex_review çağır (otomatik)
- `/rev validate` → Sadece validation
- `/rev verdict PASS` → JSON'a PASS yaz (manual override)
- `/rev verdict FAIL "issue"` → JSON'a FAIL + blocking_issues yaz (manual override)

## Risk & Gates

| Risk | Örnek | Gate |
|------|-------|------|
| LOW | Typo fix, comment, log | Codex review |
| MEDIUM | Business logic, queries, routing | Codex review |
| HIGH | Multi-file, DB schema, service interactions | Codex review + Q approval |
| CRITICAL | Auth/security, shared contracts | Codex review + Q explicit approval |
