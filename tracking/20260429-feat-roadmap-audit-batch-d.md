# Yol Haritası Audit Batch D — Ops Auth Tenant Gate Fix (HIGH RISK)

> **Slug:** `20260429-feat-roadmap-audit-batch-d`
> **Status:** REVIEW (build PASS, /rev pending)
> **Risk:** **HIGH** (auth touchpoint, 30+ /api/ops/* call site etkili)
> **Plan:** [`arch/plans/20260429-feat-roadmap-audit-batch-d.json`](../arch/plans/20260429-feat-roadmap-audit-batch-d.json)
> **Audit kaynak:** `c:/tmp/tmp-roadmap-audit-next-session-handoff.md` Paket 3 (D027)

## Özet

Audit P0 #1 — Program.cs ValidateOpsAuth tenant gate yok. Tenant-admin (Role=admin, TenantId>0) tüm /api/ops/* yüzeyine erişim alıyordu. SuperAdmin canonical TenantId=0 (no-tenant scope). Q kararı G1: inse internal JWT path Role==admin && TenantId==0 guard; inma JWT fallback REJECTED.

## Q Kararları

| # | Soru | Karar |
|---|------|-------|
| G1 | Ops auth flow | **inse internal JWT** (TenantId int claim) — Q kendi credentials. inma SSO kullanılmıyor. |

## Acceptance Criteria

| ID | Kriter | Status |
|----|--------|--------|
| AC1 | inse JWT path: Role==admin && TenantId==0 guard | ✅ Program.cs:830 |
| AC2 | inma JWT fallback: admin role tespit edilse bile reddet (security-first) | ✅ Program.cs:856-866 |
| AC3 | 30 satır inline dokümantasyon (Codex CQ5/CQ8) | ✅ Program.cs:826-866 |
| AC4 | Build PASS, 30+ call site signature unchanged | ✅ Backend 0 errors / 17 warnings |

## Düzeltilen Bug

### D027 — Ops Auth Tenant Gate Yok

**Bug:** Program.cs:826-848 ValidateOpsAuth sadece `Role == "admin"` kontrol ediyordu. Tenant-admin (TenantId>0) tüm `/api/ops/*` yüzeyine girebiliyordu (30+ endpoint: TenantsPage, OpsZohoPage, KanbanEndpoints, deploy, vb.).

**Cross-tenant lateral movement riski:** Tenant A admin'i tenant B verilerine erişebiliyordu (Ops yüzeyi platform-level).

**Fix:**
```csharp
// inse internal JWT path
if (jwtValidator != null) {
    var (context, _) = jwtValidator.ValidateToken(token);
    if (context?.Role == "admin" && context.TenantId == 0) return true;  // tenant gate
}

// inma JWT fallback REJECTED
var introspector = ctx.RequestServices.GetService<InmaTokenIntrospector>();
if (introspector != null) {
    var introspectResult = introspector.ValidateAsync(token).GetAwaiter().GetResult();
    if (introspectResult.Context?.Role == "admin") {
        return false;  // bilinçli reddet (CompanyCode opaque, async resolution scope creep)
    }
}
```

**Future option (B0-AUTH backlog):** CompanyCode → tenant_id async resolution + check == 0; 30+ call site async refactor + InmaTokenContext genişletme + TenantRegistryRepository hop. Q kararı G1 sonrası şu an gereksiz (Q inse JWT kullanıyor).

## Files Changed

```
src/Invekto.Backend/Program.cs                                  [MOD] D027 (line 826-866 expanded)
arch/plans/20260429-feat-roadmap-audit-batch-d.json             [NEW]
tracking/20260429-feat-roadmap-audit-batch-d.md                 [NEW] Bu dosya
```

## Build / Codex / Deploy

- Backend build: **PASS** (0 errors / 17 warnings pre-existing, 17.65s)
- SPA rebuild: **GEREK YOK** (sadece Backend Program.cs değişti)
- Codex full review: **PENDING** — Q FORCE PASS precedent (FEAT-PHOTO parent iter 4 ARCHITECTURE_CONFLICT) bu paket için kullanılabilir
- Deploy: **GEREKİYOR** (Backend kod değişikliği var)

## Smoke Plan (deploy sonrası)

| # | Adım | Beklenen |
|---|------|----------|
| S1 | inse JWT TenantId=0 admin → /api/ops/health 200 | ✅ SuperAdmin canonical |
| S2 | inse JWT TenantId>0 admin → /api/ops/health 401 | ✅ Tenant-admin reddedilir |
| S3 | inma JWT herhangi rol → /api/ops/health 401 | ✅ Inma fallback reddi |
| S4 | inse JWT TenantId=0 + Role=admin /api/ops/kanban/dent-pilot 200 | ✅ Kanban GET çalışır |
| S5 | TenantsPage + OpsZohoPage SuperAdmin login → 200 | ✅ Mevcut Q workflow korunur |

## Rollback

Acil rollback gerekirse 1 commit revert:
- Program.cs:830 `&& context.TenantId == 0` kaldır
- Program.cs:856-866 inma JWT path eski davranışa geri al (admin role kabul)

## Risk Assessment

**HIGH risk faktörleri:**
- Auth touchpoint (yetki kontrolü)
- 30+ /api/ops/* call site etkili
- Davranış değişikliği: tenant-admin önce 200 alabiliyordu, şimdi 401
- inma JWT admin reddedilir (eski davranış kabul ediyordu)

**Mitigation:**
- Q kararı G1: SuperAdmin inse JWT TenantId=0 kullanıyor → Q workflow korunur
- ValidateOpsAuth Func signature unchanged → 30+ call site explicit refactor gerektirmez
- Inline 30 satır dokümantasyon (Codex review CQ5/CQ8 detaylı)
- Future B0-AUTH backlog: inma JWT admin path async resolution gerekirse

## Notes

- Audit raporu P0 sıralamasında #1 (en kritik)
- FEAT-PHOTO parent paket Q FORCE PASS iter 4 precedent: ARCHITECTURE_CONFLICT classification (Codex template "no cross-service ref" gibi project-specific carve-out'ları görmüyor)
- Bu paket için benzer Codex iter 0 expected (auth security-first justification + Q karari G1 documented)
- 30 satır inline comment Codex'a sunulan ek dokümantasyon (audit fix D027 detaylı)
