---
name: reviewing-code
description: Runs automated Codex review via MCP tool, updates plan JSON, processes verdict. Use after build passes or to submit a review verdict.
---

# /rev - Codex Review via MCP (v5.1)

> **PERSIST AFTER COMPACT:** Staged changes require Codex review even after session reset.

Codex is a **reviewer only**. It never implements code. It **DOES NOT modify files** - it only produces structured JSON output via MCP tool.

**v5.1 Upgrade:** Copy-paste workflow replaced with `mcp__codex-review__codex_review` tool. Q no longer needs to relay prompts manually.

---

## Usage Modes

### 1. `/rev` - After Build PASS (Automated Review)

Runs after build PASS. Updates plan JSON + calls Codex via MCP.

**Preconditions (Hard Fail):**

1. `git diff --cached --name-status` empty? -> "No staged changes. Run git add first."
2. Plan JSON exists? (`arch/plans/{slug}.json`) -> "Plan file not found."
3. Build PASS evidence? (`build.timestamp`) -> "No build evidence. Run build first."
4. `allowed_files` scope check -> WARN if diff contains files outside allowed_files

**JSON Updates (same as v5.0):**

```json
{
  "status": "REVIEW",
  "git_diff": {
    "sha256": "Hash of full diff",
    "full_path": "arch/plans/diffs/{slug}.diff",
    "total_bytes": 125000,
    "chunks": 3,
    "stats": { "insertions": 42, "deletions": 10, "files_count": 3 }
  },
  "files_changed": [{ "path": "src/Invekto.AgentAI/Services/ReplyGenerator.cs", "is_new": false }],
  "updated_at": "2026-02-15T12:00:00Z"
}
```

**Diff stats:** Exclude self-referencing diff file:
```bash
git diff --cached --stat -- ':!arch/plans/diffs/*'
```

**Diff file:** `arch/plans/diffs/{slug}.diff` -> Full untruncated diff

**Secret scan (BLOCKING):** Before writing diff file:
```bash
grep -iE 'sk-|apikey.*[a-zA-Z0-9]{20}|password\s*[:=]' diff_file
```
If match found -> WARN Q, do NOT proceed until resolved.

**MCP Tool Call (v2.1 - diff_file_path fallback):**

After preconditions pass, diff written, and secret scan clean:

```
Call: mcp__codex-review__codex_review
Args:
  slug: {plan slug from JSON}
  risk_level: {plan risk level}
  iteration: {current iteration, 0-based}
  summary: {plan summary}
  files_changed: {from plan JSON files_changed array}
  git_diff: {full staged diff text from `git diff --cached`}
  diff_file_path: "arch/plans/diffs/{slug}.diff"
  verification_questions: {from plan JSON verification_questions}
  build_status: "PASS"
```

**CRITICAL - Diff ASLA Truncate Edilmez:**

Diff truncate edilirse Codex eksik kod üzerinde review yapar → false PASS riski.
Bunun yerine büyük diff'ler parçalanır ve her parça ayrı review alır.

**Diff Chunking Stratejisi:**

1. **Boyut kontrolü:** `git diff --cached` çıktısının byte boyutunu ölç
2. **Eşik:** 40KB (40960 byte) altı = tek review, üstü = chunk'la
3. **Chunking yöntemi (dosya bazlı):**
   - Her dosyanın diff'ini ayrı al: `git diff --cached -- {file_path}`
   - Her dosya diff'i bir chunk olur
   - Tek dosya > 40KB ise hunk bazlı böl (her `@@` bloğu ayrı chunk)
4. **Her chunk için ayrı MCP çağrısı:**
   ```
   Chunk 1/N: slug = "{slug}--chunk-1of3"
   Chunk 2/N: slug = "{slug}--chunk-2of3"
   ...
   ```
5. **Verdict birleştirme:**
   - TÜM chunk'lar PASS = genel PASS
   - HERHANGİ bir chunk FAIL = genel FAIL (blocking issues birleştirilir)
   - HERHANGİ bir chunk UNKNOWN = Q escalation
6. **Summary:** Tüm chunk verdict'leri Q'ya tek özet olarak gösterilir

**Diff Resolution (3-tier fallback):**
1. `git_diff` inline string (en az 50 char) -> kullan
2. `git_diff` boş/kısa -> `diff_file_path` dosyasını diskten oku
3. `diff_file_path` de yoksa -> `arch/plans/diffs/{slug}.diff` otomatik dene
4. Hiçbiri yoksa -> HATA (API çağrısı yapılmaz, token israfı önlenir)

**DevAgent MUST:**
- `git diff --cached` çıktısının boyutunu kontrol et
- 40KB üstü ise chunking stratejisini uygula, **ASLA truncate etme**
- Her chunk için `git_diff` parametresine o chunk'ın tam diff'ini gönder
- AYRICA `diff_file_path` parametresini her zaman ekle (güvenlik ağı)
- Diff boşsa MCP tool HATA döner (UNKNOWN yerine açık hata mesajı)
- Chunk'lı review'da tüm verdict'leri birleştirip Q'ya tek sonuç sun

**Result Processing:**

The MCP tool returns structured JSON with:
- `verdict`: "PASS" | "FAIL" | "UNKNOWN"
- `code_quality_gate`: CQ1-CQ8 results with evidence
- `cove_verification`: Q1-Qn results with reasoning
- `blocking_issues`: array of issue descriptions
- `summary`: 1-2 sentence review summary
- `raw_response`: full Codex output text
- `model_used`: actual model that ran
- `token_usage`: prompt/completion/total tokens

**After MCP response:**
1. Show Q a concise summary: verdict, blocking issues, summary
2. Auto-process verdict (see section 3 below)
3. If FAIL -> show blocking issues, fix, re-run `/rev`
4. If PASS -> proceed to commit

---

### 2. `/rev validate` - Validation Only (Optional)

Runs validation without calling Codex.

```
/rev validate -> Schema validation -> Coverage check -> Preconditions -> PASS/FAIL report
```

---

### 3. `/rev verdict <PASS|FAIL|UNKNOWN> [issue]` - Manual Verdict Override

Used when Q wants to manually override the MCP result.

```bash
/rev verdict PASS
/rev verdict FAIL "CQ2 failed: silent failure in catch block"
/rev verdict UNKNOWN
```

**Rule: FAIL requires an issue description.**
- `/rev verdict FAIL` -> ERROR: blocking_issues cannot be empty
- `/rev verdict FAIL "CQ2 failed: ..."` -> OK

**Iteration increment:** Only incremented on verdict processing (auto or manual).

**Escalation trigger:** When iteration >= 3:
- `"escalation_required": true` added to JSON
- DevAgent stops fixing, Q decision required

---

## Devil's Advocate (PP-006)

> **Canonical source:** `INVEKTO_BASE.prompt.md` SEYTANIN AVUKATLIGI section.

During review, challenge the code - don't rubber-stamp:
- Question if the diff is truly minimal
- Flag silent failures and missing error handling
- Ask "what breaks if this input is unexpected?"
- Challenge scope creep ("is this change in the plan?")
- Don't accept PASS just because the build passed

---

## Flow Summary (v5.1 - MCP Automated)

```
DevAgent /rev -> JSON updated, diff written, secret scan
    |
Diff boyut kontrolü: <= 40KB?
    |
  EVET -> Tek MCP çağrısı (git_diff inline)
  HAYIR -> Dosya bazlı chunk'la -> Her chunk için ayrı MCP çağrısı
    |
Codex API returns structured JSON (CQ1-8 + CoVe + verdict) [per chunk]
    |
DevAgent verdict'leri birleştirir, Q'ya tek özet sunar
    |
ALL PASS -> commit -> DONE
ANY FAIL -> show blocking issues -> fix -> /rev (max 3 iterations)
ANY UNKNOWN -> Q escalation
```

**No more copy-paste!** The entire review flow is automated via MCP.

---

## Critical Rules

1. **Codex DOES NOT modify files** - MCP tool is read-only, returns review JSON
2. **Who processes verdict?** - DevAgent (auto from MCP result, or manual via `/rev verdict`)
3. **FAIL + empty blocking_issues = ERROR** - issue description is mandatory
4. **Iteration reaches 3 -> Q escalation** - no new iteration without Q permission
5. **Scope violation = HARD FAIL** - changes outside allowed_files are rejected
6. **Secret in diff = BLOCKING** - scan diff for secrets before MCP call
7. **MCP error handling:** If MCP tool returns `error: true`, categorize by `error_type`:
   - `AUTH_ERROR` -> Check OPENAI_API_KEY in .mcp.json
   - `RATE_LIMIT` -> Wait and retry
   - `TIMEOUT` -> Diff chunking stratejisini uygula (dosya bazlı böl, ayrı review al)
   - `MODEL_ERROR` -> Check CODEX_MODEL env var

---

## Codex Output Format

For the expected Codex output format (Code Quality Gate + CoVe Verification blocks), see [references/codex-output-format.md](rev/references/codex-output-format.md).

---

## Q Override

| Command | Effect |
|---------|--------|
| `STOP` | Halt all operations |
| `SKIP CODEX` | Skip /rev, commit directly (Q permission only) |
| `FORCE PASS` | Override verdict (Q permission only) |

---

## MCP Server Info

| Property | Value |
|----------|-------|
| Server | `mcp-servers/codex-review/` |
| Version | `v2.1.0` (diff_file_path fallback + auto-discover) |
| Tool name | `codex_review` |
| MCP config | `.mcp.json` (gitignored, contains API key) |
| Model | `gpt-5.2-codex` (configurable via CODEX_MODEL env) |
| Permission | `mcp__codex-review__*` in `.claude/settings.local.json` |
| Min diff | 50 chars (shorter = error, prevents empty diff UNKNOWN results) |

---

## Canonical Rule

Codex enforces correctness. DevAgent implements + runs /rev. Q owns decisions.

MCP automates the bridge - no manual copy-paste required.

This rule overrides convenience and speed.
