# SPEC: [Feature Adi]

> **Spec ID:** SPEC-XXX | **Paket:** PKT-XX | **Risk:** LOW/MEDIUM/HIGH
> **Yazar:** Q | **Son Guncelleme:** YYYY-MM-DD | **Durum:** DRAFT/APPROVED/IMPLEMENTED

## 1. Intent (Ne & Neden)

[Q'nun kendi sozleriyle: bu feature ne yapiyor ve neden gerekli?]

## 2. Acceptance Criteria

| # | Kriter | Dogrulama Yontemi |
|---|--------|-------------------|
| AC-1 | ... | Manual test / Codex CQ / DB query |
| AC-2 | ... | ... |

## 3. Architectural Decisions

[Bilincli kararlar — Codex'in false positive vermemesi icin]

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| ... | ... | EXPECTED: CQ5 ... skip |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| API Request/Response | `arch/contracts/xxx.json` |
| DB Schema | `arch/db/xxx.sql` |
| Error Codes | `arch/errors.md` INV-XX-xxx |

## 5. Scope Boundaries

### In Scope
- ...

### Out of Scope (Explicit)
- ...

### Degismeyen Alanlar (Pre-existing)
- [Codex'in pre-existing pattern false positive vermemesi icin]

## 6. Service Boundaries

| Servis | Rol | Degisiklik Tipi |
|--------|-----|-----------------|
| Backend | Proxy | Yeni endpoint |
| ServisX | Core logic | Yeni servis / Major change |

## 7. Risk & Mitigation

| Risk | Olasilik | Mitigation |
|------|----------|------------|
| ... | LOW/MED/HIGH | ... |
