---
description: Manuel Codex review trigger (fallback veya Q override icin)
---

# /codex - Manuel Codex Trigger (v2.0 - MCP)

Bu komut Q'nun manuel olarak Codex review baslatmasi icin kullanilir.

**Normal akista gerek yok** - `/rev` otomatik olarak MCP uzerinden review yapar.

## Kullanim

```
/codex {slug}
```

Ornek:
```
/codex fix-webchat-bug
/codex 20260312-outbound-feature
```

## Ne Yapar (v2.0 - MCP)

1. Plan JSON dosyasini okur: `arch/plans/{slug}.json`
2. Staged diff'i alir
3. MCP tool call yapar: `mcp__codex-review__codex_review`
4. Structured JSON verdict dondurur
5. Plan JSON verdict bolumunu gunceller
6. Inline status gosterir

## MCP Tool Call

```
Tool: mcp__codex-review__codex_review

Input:
  slug:                   plan.slug
  risk_level:             plan.risk
  iteration:              plan.verdict.iteration (veya 0)
  summary:                plan.plan.summary
  files_changed:          plan.files_changed
  git_diff:               staged diff content
  verification_questions: plan.verification_questions
  build_status:           "PASS"

Output:
  verdict:            "PASS" | "FAIL" | "UNKNOWN"
  code_quality_gate:  { CQ1..CQ12: { result, evidence }, overall }
  cove_verification:  { Q1..Qn: { result, reasoning } }
  blocking_issues:    string[]
  summary:            string
  model_used:         string
  token_usage:        { prompt_tokens, completion_tokens, total_tokens }
```

## Ne Zaman Kullanilir

| Durum | Kullanim |
|-------|----------|
| `/rev` MCP call basarisiz | `/codex {slug}` (fallback) |
| Q ara review istiyor | `/codex {slug}` |
| Q override istiyor | `/codex {slug}` |

## CQ1-12 Tanimlari

| CQ | Konu |
|----|------|
| CQ1 | Hata yakalama ve kullaniciya geri bildirim |
| CQ2 | Silent failure riski |
| CQ3 | Diff minimum mi (scope creep) |
| CQ4 | Duplicate kod kontrolu |
| CQ5 | Codebase pattern uyumu |
| CQ6 | Performans (O(n^2), N+1, memory leak) |
| CQ7 | Yeni TODO/HACK/FIXME |
| CQ8 | Breaking change (API, export, type) |
| CQ9 | Is mantigi tutarliligi (izolasyon, contract) |
| CQ10 | UX consistency (style guide, responsive) |
| CQ11 | DB-code sync (schema drift) |
| CQ12 | Error handling quality (error codes, messages) |

## Hata Durumlari

| Error | Aksiyon |
|-------|---------|
| AUTH_ERROR | Q'ya bildir: "OPENAI_API_KEY ayarlanmali" |
| RATE_LIMIT | 30 saniye bekle, tekrar dene |
| TIMEOUT | Q'ya bildir: "API timeout - diff cok buyuk olabilir" |
| MODEL_ERROR | Q'ya bildir: "Model bulunamadi - CODEX_MODEL env kontrol et" |