# InvektoServices Review Policy v1.0

> Bu dosya tum review kurallarinin TEK KAYNAGIDIR.

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

---

## 2. Evidence Gereksinimleri

| Risk | build | db_code_sync | high_checks | invariant_proof_pack |
|------|-------|--------------|-------------|---------------------|
| **LOW** | Build PASS | `N/A -- LOW risk` | `N/A -- LOW risk` | `N/A -- LOW risk` |
| **MEDIUM** | DOLU | DOLU (>=20 char) | `N/A -- MEDIUM risk` | `N/A -- MEDIUM risk` |
| **HIGH** | DOLU | DOLU (>=20 char) | DOLU (>=20 char) | `N/A -- HIGH risk` |
| **CRITICAL** | DOLU | DOLU (>=20 char) | DOLU (>=20 char) | DOLU (>=20 char) |

---

## 3. Verdict Kurallari

### PASS Sartlari (TUMU gerekli)
- [ ] Architecture coverage
- [ ] Policy enforced
- [ ] Checklist PASS (CQ1-12)
- [ ] Evidence = Final Risk requirements
- [ ] No blocking issues
- [ ] Scope discipline

### FAIL Durumlari (HERHANGI BIRI)
- Tenant/auth/security regression risk
- Architecture/policy violation
- Microservice isolation violation
- DB injection / unsafe query
- Schema drift without migration
- snake_case violation in DB columns
