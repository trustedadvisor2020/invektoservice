# Refactor Audit — Branch Consolidation Plan (2026-06-14)

> **Status (2026-06-14):** ✅ **CONSOLIDATED** — all 18 branches merged into `work/20260614-audit-consolidation` (from master `77aa6fda`). Code conflict-free; only `arch/codex-context.md` (additive union of 8 sections, verified complete, 0 missing lines) + `arch/session-memory.md` auto-resolved. Full-solution build = **0 errors**. **Deploy gate = GREEN** (Outbound/WAA secrets set). Branch is **LOCAL only — not pushed, NOT merged to master, NOT deployed.** Next decision = Q (push / master-merge / deploy waves).
> **Source audit:** `arch/reports/20260614-audit-INDEX.md` (176 findings, 8C/34H).
> **UPDATE 2026-06-15 (prod-log pre-flight, Q-requested before any Wave-1 deploy):** Wave-1's named defects are **NOT firing** in prod (20-day window) → Wave-1 confirmed **non-urgent** (latent fixes, not firefighting). Two *different* crashes WERE firing: (1) **Outbound `template_id`-null** in the export path — **NEW, not in any audit branch** → fixed+deployed separately (master `3f540ab6`, Outbound HEALTHY, Codex PASS iter0); (2) **Knowledge `extract-from-analysis`** String-on-integer = old deployed code, **master source already clean**; its remaining latent extract crashers (C4 IndexOutOfRange + `:334` bigint cast) are **already in this Wave-1** (`knowledge-c4` + `getint32`) → deploying Wave-1 closes the Knowledge crash, no new fix needed. ⚠️ master advanced `77aa6fda`→`3f540ab6`; consolidation is off `77aa6fda` → a Wave-2 Outbound branch touching `ExportRepository.cs` may cause a **trivial** guard-line conflict on consolidation→master merge.
> **Working rule (still in force):** every batch = own `work/` branch → Codex PASS → STOP at commit. Merge+deploy = Q's explicit call.

## ⚠️ Headline correction

The session-continuation prompt (and session-memory's running count) said **6** unmerged branches. The actual count is **18**. `git branch --no-merged master` confirms: Batches 1–5, C4, the 4 AgentAI branches, the Knowledge sweep, and both WAA branches were **also never merged** — the "5–6" count only tracked the latest wave. Only **WebChat §7** (`7d895d47`) and **Backend auth/login** (`30fd9361`) ever merged+deployed.

## Conflict reality (merge-tree verified, non-destructive)

Pairwise `git merge-tree` on every colliding file pair:

| Surface | Result |
|---|---|
| **All `.cs` / `.ts` source files** | **Auto-merge CLEAN** — incl. Backend `Program.cs` (settings∩payment), AgentAI `Program.cs` (dbpath∩genpath), `AutomationRepository.cs` (getint32∩automation), `OutboundRepository.cs` (getint32∩outbound), WAA `Program.cs` (failclosed∩waa-sweep) |
| **`arch/codex-context.md`** | **CONFLICT** — 8 branches append a sanctioned-broad-catch `###` section at the same anchor (before `## FAIL CONDITIONS`). Additive only → resolve = union of all sections. (hot-lessons L21 predicted this.) |
| **`arch/session-memory.md`, `arch/hot-lessons.md`** | 2 §7 branches carry stale snapshots. Resolve = **keep current/HEAD**, discard branch versions (regenerated at /wrap). |
| **`arch/errors.md`, `ErrorCodes.cs`** | Single branch (errorcode-collision) only — no collision. |
| **`arch/plans/*.json`** | Distinct file per branch — no collision, ride along. |

**Net: the only manual merge work is ~7–8 trivial union-resolutions of one markdown file.** Code is conflict-free.

## The 18 branches — two natural waves

### Wave 1 — Critical correctness (Batches 1–5 + C4): real prod bugs + security, NOT YET IN PROD

| Branch | Commit | What it fixes | Deploy target(s) | Prereq / risk |
|---|---|---|---|---|
| getint32-bigint-cast | `ad7efb63` | `GetInt32`-on-bigint `InvalidCastException` crash | **6 svc**: Appointments, Automation, Backend, Integrations, Knowledge, Outbound | none |
| knowledge-c4-grouptag-select | `2e7c8325` | `IndexOutOfRangeException` on **every** published faq/intent template | Knowledge (7104) | none |
| voiceai-path-crash-traversal | `c23c9831` | `:`-path crash **+ CWE-22 traversal (SECURITY)** | VoiceAI (separate NSSM — not in server-deploy enum, L-2026-05-31) | none |
| outbound-internal-jwt | `fff607fd` | opt-out + outbox-drain **401-broken since FEAT-J2** | Outbound (7107) | **PREREQ:** Backend+Outbound prod `InternalServices:SharedSecret` set **and equal** |
| failclosed-auth | `82424043` | ChatAnalysis+WAA startup fail-closed if internal keys missing | ChatAnalysis (7101) + WhatsAppAnalytics (7109) | **HARD PREREQ:** prod `Benchmark:OpsKey` + `Microservice:InternalApiKey` set **first** — else both services throw on startup = **DOWN** |
| errorcode-collision | `0c3c3a4a` | 6 INV-BE wire-value collisions → wrong-code mapping | **Shared.dll** (values, names unchanged) → Backend primarily; Shared refresh on its consumers | low — error-response strings only |

### Wave 2 — Quality sweeps (broad-catch→typed, behavior-preserving hardening)

| Branch | Commit | Deploy target(s) | Note |
|---|---|---|---|
| agentai-dbpath-typed-catch | `e30b4605` | AgentAI (7105) | 4 AgentAI branches merge clean together |
| agentai-genpath-typed-catch | `1465dad1` | AgentAI | |
| agentai-header-tryaddwithoutvalidation | `dd310f0e` | AgentAI | auth-header touch (Q already approved) |
| knowledgeclient-parse-singlepass | `f27b0bf8` | AgentAI | perf-dup fix |
| knowledge-errorhandling-sweep | `a87b0a15` | Knowledge (7104) | |
| automation-section7-safe-sweep | `f6021a41` | Automation (7108) | this session's work |
| integrations-section7-quickwins | `100d7a56` | Integrations (7106) | Int-3 GRANT already correct in prod = **no migration** |
| backend-settings-instances-typed-catch | `ef88ec8f` | Backend (5000) | |
| backend-payment-typed-catch | `69115b9f` | Backend (5000) | |
| waa-errorhandling-sweep | `ab69be82` | WhatsAppAnalytics (7109) | |
| waa-quality-avg-fix | `6a7007ee` | WhatsAppAnalytics (7109) | **Note: this is a REAL bug** (AvgOverallScore SUM→AVG, 10x inflation, dominant path). Quality-insight-scoped → kept in W2, but is correctness not hardening. |
| outbound-section7-quickwins | `0b8f7c95` | Outbound (7107) + **Shared.dll** (ExportDtos, additive) + **Backend SPA rebuild** (Dashboard `api.ts`) | |

> Nearly all 11 services are touched. This is effectively a **platform-wide deploy** → must be sequenced and smoke-verified per service, not done in one shot.

## Merge plan (one integration branch, code is conflict-free)

1. `git checkout master && git pull` (sync), create `work/20260614-audit-consolidation` from master.
2. Merge the 18 branches into it (Wave 1 first, then Wave 2, so a partial-stop still lands the critical fixes). Order within a wave doesn't matter for code.
3. On each `arch/codex-context.md` conflict → resolve = **keep both sections** (delete markers, union). ~7–8 trivial resolutions.
4. On `arch/session-memory.md` / `arch/hot-lessons.md` conflicts → `git checkout --ours` (keep integration/HEAD).
5. **Full-solution build** (Shared changed → `dotnet build InvektoServis.sln`). Must pass.
6. Single consolidated `/rev` (or trust the per-branch PASSes + a focused merge-correctness review of codex-context.md union + build).

## Deploy plan (risk-ordered, per Q go-ahead)

**Gate before ANY deploy — verified live 2026-06-14 (read-only). Per-service requirement corrected (each service gates ONE key, not both):**

| Prereq | Required by | Prod state (verified) | Verdict |
|---|---|---|---|
| Backend & Outbound `InternalServices:SharedSecret` **equal** | outbound-jwt | Backend=64-char (Production.json, sha 04A96F3D); Outbound=36-char default (base appsettings.json, sha 28561D33) — **MISMATCH**; Outbound never got prod override | ❌ BLOCKED — set Outbound Production.json `InternalServices:SharedSecret` = Backend's value |
| ChatAnalysis `Microservice:InternalApiKey` | failclosed-auth (CA gate = InternalApiKey only) | present, 40-char, Production.json | ✅ OK |
| WhatsAppAnalytics `Benchmark:OpsKey` | failclosed-auth (WAA gate = OpsKey only) | **MISSING** | ❌ BLOCKED — set WAA Production.json `Benchmark:OpsKey` before deploy (else startup throws = WAA DOWN) |

→ **RESOLVED 2026-06-14 (Q-approved writes):** Outbound `InternalServices:SharedSecret` set = Backend's value (sha8 04A96F3D, MATCH confirmed); WAA `Benchmark:OpsKey` set = fresh 64-hex random (sha8 EEB34235). Both added as new top-level blocks via targeted insert (no reserialize), `.bak-20260614-auditgate` backups written. **On-disk only — effective at next restart/deploy.** Note: post-deploy WAA `/api/ops/*` will require the new OpsKey (held server-side; any benchmark caller needs same value). **Gate = GREEN; all 3 critical-wave deploys now unblocked.**

**Deploy waves (each: publish → stop NSSM → upload → start → `/health` + live smoke + keep `.bak` rollback):**
- **D1 (lowest risk, highest value):** VoiceAI (security/crash, isolated), Knowledge (c4 + getint32 + sweep), Appointments (getint32).
- **D2 (Shared.dll):** errorcode-collision + outbound-section7 ExportDtos → Shared.dll refresh paired with Backend, Outbound, and any other consumer; Backend SPA rebuild for `api.ts`.
- **D3 (prereq-gated):** Outbound (after SharedSecret verified), then ChatAnalysis + WAA (after OpsKey/InternalApiKey verified — these can take services DOWN if skipped).
- **D4 (quality):** AgentAI, Automation, Integrations, Backend settings/payment, WAA sweep.

## Recommendation

**Do Wave 1 (merge + deploy) first as priority; Wave 2 can follow same-day or next session.**

Rationale, grounded:
- Wave 1 isn't "debt piling up" — it's **shipped fixes for live defects sitting out of prod**: a crash hit on every published faq/intent template (C4), a security path-traversal (VoiceAI), a feature 401-broken since FEAT-J2 (outbound opt-out/outbox-drain), and bigint-cast crashes across 6 services. Severity per audit; *live impact not independently re-verified against prod logs* (medium confidence on "currently firing").
- Merge cost is near-zero (code conflict-free; one markdown union). The thing that's been blocking is the deploy decision + prereq config, not merge difficulty.
- **`failclosed-auth` is the one trap:** deploying it without the two prod keys set takes ChatAnalysis + WAA DOWN. Verify config first; it's the only hard gate.

**Open question for Q:** deploy all 18 in waves now, or merge-all-to-master-now + deploy in scheduled waves, or Wave-1-only first? (This is the irreversible/multi-tenant part — your call.)
