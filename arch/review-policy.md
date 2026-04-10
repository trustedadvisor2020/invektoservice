# InvektoServices Review Policy v2.0

> Bu dosya InvektoServices review kurallarinin TEK KAYNAGIDIR.
> **v2.0 (2026-04-10):** LOW=Codex zorunlu, MCP-first primary, fallback path ayrimi, cross-project hizalama.

---

## 1. Risk Seviyeleri

| Seviye | Kapsam | Ornek Degisiklikler |
|--------|--------|---------------------|
| **LOW** | UI layout/text only (no logic) | CSS changes, text updates, comments |
| **MEDIUM** | Business logic, queries, routing | API endpoints, service logic, data handling |
| **HIGH** | Auth-adjacent, service lifecycle, shared DTOs | Auth middleware, Shared DTOs, service startup |
| **CRITICAL** | Schema, auth/token, tenant isolation | Migrations, auth.cs, tenant scoping |

### Auto-Detection Patterns
```
CRITICAL: auth, migration, tenant.*scop, Shared/DTOs
HIGH:     Services/*.cs, Controllers/*.cs
MEDIUM:   Controllers/*.cs, Services/ (with logic)
LOW:      *.md, wwwroot/ (layout only)
```

### LOW Risk Sınıflandırma Guardrails

Bir değişikliğin LOW kalabilmesi için aşağıdakilerin TÜMÜ doğru olmalı:
- Kod değişikliği YOK (sadece comment, typo, log message) veya sadece CSS/layout/text
- Shared DTO değişikliği YOK
- Service startup / lifecycle değişikliği YOK
- Migration / schema değişikliği YOK
- Auth / token / tenant isolation değişikliği YOK

Kırmızı çizgilerden BİRİ bile varsa → risk OTOMATİK MEDIUM+.

> **Not:** LOW kalsın veya MEDIUM'a yükselsin, **her iki durumda da Codex review zorunlu**. Fast-track / SKIP yolu YOK. LOW sınıflandırması sadece evidence derinliğini ve iterasyon toleransını etkiler.

---

## 2. Evidence Gereksinimleri

| Risk | build | db_code_sync | high_checks | invariant_proof_pack | codex_review |
|------|-------|--------------|-------------|---------------------|---------------|
| **LOW** | Build PASS | `N/A -- LOW risk` | `N/A -- LOW risk` | `N/A -- LOW risk` | **ZORUNLU** |
| **MEDIUM** | DOLU | DOLU (>=20 char) | `N/A -- MEDIUM risk` | `N/A -- MEDIUM risk` | **ZORUNLU** |
| **HIGH** | DOLU | DOLU (>=20 char) | DOLU (>=20 char) | `N/A -- HIGH risk` | **ZORUNLU** |
| **CRITICAL** | DOLU | DOLU (>=20 char) | DOLU (>=20 char) | DOLU (>=20 char) | **ZORUNLU** |

- **DOLU** = ≥20 karakter, `N/A` ile başlamaz
- `db_code_sync`: Postgres schema / C# entity sync
- `high_checks`: Microservice isolation (Shared üzerinden iletişim), service lifecycle safety
- `invariant_proof_pack`: `INVARIANT-1: ... (dosya:satır)` formatında

---

## 3. Review Akışı (v2.0 — TÜM risk seviyeleri Codex)

### Primary Path (normal yol)

```
1. DevAgent: Kod yazar
2. Build PASS (dotnet build)
3. DevAgent `/rev` çalıştırır
4. `/rev` içinden MCP tool `mcp__codex-review__codex_review` otomatik çağrılır
5. Codex: 2 BLOK review (Code Quality CQ1-12 + CoVe)
6. Verdict plan JSON'a yazılır
7. PASS → commit → DONE | FAIL → fix → /rev tekrar (max 3 iter)
```

### Fallback Path (incident yolu)

MCP tool başarısız olursa (network, rate limit, timeout, MCP server down):

```
Fallback A: /codex {slug} manuel tetik (MCP retry)
Fallback B: Q Codex penceresine manuel copy-paste → verdict'i /rev verdict ile geri yaz
```

> **Fallback primary değildir.** Copy-paste yalnızca MCP tamamen ulaşılamazsa kullanılır.

---

## 4. Verdict Kurallari

### PASS Sartlari (TUMU gerekli)
- [ ] Architecture coverage (touched scope için)
- [ ] Policy enforced (bu dosya)
- [ ] Checklist PASS (CQ1-12)
- [ ] Evidence = Final Risk requirements
- [ ] No blocking issues
- [ ] Scope discipline (allowed_files içinde)

### FAIL Durumlari (HERHANGI BIRI)
- Tenant/auth/security regression risk
- Architecture/policy violation
- Microservice isolation violation (servisler arası doğrudan referans)
- DB injection / unsafe query
- Schema drift without migration
- snake_case violation in DB columns
- Shared DTO breaking change without cross-service update

---

## 5. Fix-Run Kuralları

| Risk | Max Iter | Escalation |
|------|----------|------------|
| LOW | 3 | Q escalate |
| MEDIUM | 3 | Q escalate |
| HIGH+ | 3 | Q onay gerekli |

Per iteration: Fix only blocking issues → build → update JSON → `/rev` again (MCP primary).

---

## 6. Referans Dosyalar

| Dosya | Rol |
|-------|-----|
| `arch/review-policy.md` | **TEK KAYNAK** (bu dosya) |
| `arch/codex-context.md` | Codex MCP server için project context |
| `INVEKTO_BASE.prompt.md` | Global agent rules |
| `INVEKTO_PLAN_AGENT.prompt.md` | Plan agent |
| `INVEKTO_DEV_AGENT.prompt.md` | Dev agent |
| `C:\Users\taner\.claude\workflow\shared-lessons.md` | Cross-project kurallar |

---

## 7. Versiyon

| Versiyon | Tarih | Değişiklik |
|----------|-------|------------|
| 2.0 | 2026-04-10 | LOW=Codex ZORUNLU, MCP-first primary, fallback path ayrımı, evidence tablosuna codex_review kolonu |
| 1.0 | 2026-02 | İlk versiyon |
