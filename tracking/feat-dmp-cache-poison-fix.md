# P1 — FEAT-DMP Cache Cancellation Poison Fix

> **Slug:** `20260422-feat-dmp-cache-poison-fix` | **Faz:** 1 (Retro-Fix) | **Risk:** LOW
> **Roadmap:** [`pilot-launch-roadmap.md`](pilot-launch-roadmap.md) P1
> **Plan JSON:** [`arch/plans/20260422-feat-dmp-cache-poison-fix.json`](../arch/plans/20260422-feat-dmp-cache-poison-fix.json)
> **Durum:** DONE — Codex iter 0 PASS (12/12 CQ + 4/4 CoVe, 0 blocking), 7/7 test PASS, deploy pending

## Scope

`src/Invekto.Shared/Services/InmaDynamicFieldsCache.cs` cancellation-isolation retro-fix. FEAT-TFM iter 3 `DbTenantFieldMappingResolver` pattern'inin ikiz uygulamasi. Tuketici: sadece Backend (`Program.cs:450` DI, `7746/7795` endpoint'ler).

## Bug (Onceki)

```csharp
_inflight.GetOrAdd(tenantId, _ => FetchAndCacheAsync(tenantId, secretKey, ct));
```

Ilk caller'in `ct` degeri shared in-flight Task icine gomuluyor; caller A iptal ederse joined caller B de `OperationCanceledException` ile yaniyor (cancellation poison).

## Fix

- Factory `FetchAndCacheAsync(tenantId, secretKey)` → underlying `_client.GetFieldsAsync(..., CancellationToken.None)`
- Per-caller cancel `await fetch.WaitAsync(ct)` ile kendi stack'ine izole
- `Invalidate(int)` hem cache entry hem `_inflight` slot temizler (TFM parity)

## AC

| # | Kriter | Dogrulama |
|---|--------|-----------|
| AC1 | Inflight fetch ilk caller ct'sine bagli degil | Test `GetOrFetchAsync_CallerCancels_DoesNotCancelUnderlyingFetch` (`ObservedCancellationTokens == None`) |
| AC2 | Per-caller cancel WaitAsync(ct) ile izole | Test `GetOrFetchAsync_FirstCallerCancels_DoesNotAbortJoinedAwaiter` |
| AC3 | 7 unit test: isolation + single-flight + invalidate (entry & inflight) + empty-cache + exception propagation | Test suite Invekto.Backend.Tests PASS |
| AC4 | Secret-rotation davranisi korundu (first-caller secret wins) | Mevcut davranis notu XML doc'ta |
| AC5 | Build PASS + test PASS + Backend /api/v1/dynamic-fields contract korunuyor | dotnet build + test run + FEAT-DMP smoke preservation |

## Scope Discipline

**Touchable:**
- `src/Invekto.Shared/Services/InmaDynamicFieldsCache.cs`
- `tests/Invekto.Backend.Tests/UnitTests/InmaDynamicFieldsCacheTests.cs` (YENI)
- `arch/plans/20260422-feat-dmp-cache-poison-fix.json`
- `tracking/feat-dmp-cache-poison-fix.md` (YENI, bu dosya)
- `tracking/pilot-launch-roadmap.md` (P1 Status=DONE guncelleme)
- `arch/session-memory.md` (Last Update + Recently Completed)
- `arch/lessons-learned.md` (P2 paketinde yazilacak — bu paket kod-only)

**Forbidden:**
- `IInmaDynamicFieldsClient.cs`, `InmaDynamicFieldsFetchException.cs`, `Program.cs`, `DbTenantFieldMappingResolver.cs` (referans pattern, dokunma)
- Outbound/Automation (cache Backend-only registered)

## Deploy

- **Scope:** Backend-only (MCP `invekto-ops server-deploy`)
- **Config:** degismedi (sandwich preserve)
- **Health:** post-deploy `/health` teyidi + Dashboard `/api/v1/dynamic-fields` 401 probe (auth gate canli)

## Codex Verdict

- **Iter 0: PASS** (12/12 CQ + 4/4 CoVe, 0 blocking) — model gpt-5.4-2026-03-05, tokens 24091.
- Summary: "The cache fix correctly decouples shared fetch execution from individual caller cancellation, preserves public API compatibility, and adds focused regression coverage."
