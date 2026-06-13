# FEAT-INMA-PIPELINE-V2 Chunk 3a — customer_status_changed flow trigger (backend)

> **Slug:** `20260613-feat-inma-pipeline-v2-c3a-flow-trigger` | **Risk:** MEDIUM | **Status:** ✅ DONE+DEPLOYED — Codex PASS iter 0
> **Commits:** `d9a2ecc6` (feat) · `665a50e7` (spa rebuild) | **Deploy:** 2026-06-13, Backend+Automation, 10/10 HEALTHY, config-restore'lu, SPA `index-DB6P83VO.js`
> **Plan:** `arch/plans/20260613-feat-inma-pipeline-v2-c3a-flow-trigger.json` | **Diff:** `arch/plans/diffs/20260613-feat-inma-pipeline-v2-c3a-flow-trigger.diff` (gitignored)
> **Parent:** [FEAT-INMA-PIPELINE-V2](README.md#feat-inma-pipeline-v2) (5 chunk) | **Contract:** `arch/contracts/inma-customer-status-webhook.json` v2.0 (c3_trigger)
> **Önceki:** [C2 inbound consumer](feat-inma-pipeline-v2-c2.md) DONE+VERIFY-LIVE

## Karar Özeti

C2 bir lead'in `customer_status`'ünü **uyguladığında** (`StoredApplied`), Backend her etkilenen lead'in **eski→yeni** durumunu atomik yakalar, **fail-closed loop guard** + **change-gate** uygular ve **gerçekten değişen** her lead için Automation'a (Hangfire, G7) **lead başına bir iş** kuyruğa alır. İş, tenant'ın `customer_status_changed` flow'larını çözer (**most-specific-wins** featureGroupId eşleşmesi) ve FlowEngineV2'yi değişiklik bağlamıyla çalıştırır.

**C3a = SADECE backend tetikleme borusu.** C3b (Flow Builder UI node + cxapi feature-group katalog dropdown) ve C4 (Set Customer Status INMA write-back) ERTELENDİ.

## Interview + Codex Kararları

| Konu | Karar |
|------|-------|
| Kapsam | **C3a backend-only** (UI node + katalog = C3b) |
| Lead bağlamı | C2 repo'yu **genişlet** (`RETURNING id + old status`) → **lead başına 1 flow** |
| Eşleşme | **featureGroupId** (gruptaki herhangi bir değişikliğe ateşle; from/to filtre yok) — Codex: **most-specific-wins** |
| Suppression | **Hardcode** — Codex: **FAIL CLOSED** (yalnız `actor.type=='user'` ateşler) |
| Retry | **AutomaticRetry=0, dedupe-table YOK** (Q kararı — welcome/cron precedent'i; replay C2'de dedupe'lü) |

**Plan codex_consult(critique) ile implementasyon ÖNCESİ strestlendi** → 3 gerçek bulgu benimsendi: (1) CTE `FOR UPDATE` concurrency (monotonic guard'ı korur), (2) change-gate `old IS DISTINCT FROM new` (sahte tetik önler), (3) fail-closed loop guard (echo eksikse bile döngü imkansız).

## Acceptance Criteria (Codex PASS iter 0: 12/12 CQ + 4/4 CoVe)

| ID | Kriter | Sonuç |
|----|--------|-------|
| AC1 | `FlowGraphV2.TriggerTypes += customer_status_changed`; `FlowValidator.RequiredFields += [label]` | ✅ |
| AC2 | `CustomerStatusChangedTriggerHandler` (pure INodeHandler) — 4 contract var'ı surface eder, DI'a kayıtlı | ✅ |
| AC3 | C2 repo CTE `MATERIALIZED+FOR UPDATE+ORDER BY id` → per-lead old/new RETURNING; `AppliedLeads`; MatchedLeadCount==rows | ✅ |
| AC4 | StoredApplied'da suppression + change-gate + per-lead enqueue; typed-catch INV-INM-008, HTTP 200 korunur | ✅ |
| AC5 | `TriggerCustomerStatusFlowJob` [automation][retry=0]; tenant-scoped flow resolve; most-specific-wins; synthetic session -4M; INV-AT-088 | ✅ |
| AC6 | Loop guard döngüyü ispatlı kırar (`invekto-` prefix + fire-only-on-user); prefix tek shared const (C4 ile paylaşımlı) | ✅ |
| AC7 | Migration YOK; UI/TS YOK (C3b); full-solution build PASS (0 error) | ✅ |
| AC8 | Change-gate: aynı statüye re-selection → occurred_at ilerler ama tetik YOK | ✅ |
| AC9 | Yapısal loglar (fire/suppress(reason)/changed/enqueued + per-flow) | ✅ |

## Files Changed (12 dosya, +607 / -16)

- `src/Invekto.Shared/Contracts/Inma/Webhooks/CustomerSelectionChangedEvent.cs` — `CustomerStatusFlowSuppression.Evaluate` + `OriginRequestIdPrefix='invekto-'` (C4 paylaşımlı) + `FlowSuppressionDecision`
- `src/Invekto.Shared/Constants/ErrorCodes.cs` — INV-INM-008 + INV-AT-088
- `src/Invekto.Backend/Data/InmaWebhookEventRepository.cs` — CTE FOR UPDATE + `AppliedLead` + result struct
- `src/Invekto.Backend/Program.cs` — `DispatchCustomerStatusFlowTriggers` (StoredApplied)
- `src/Invekto.Automation/Services/FlowGraphV2.cs` — TriggerTypes +=
- `src/Invekto.Automation/Services/FlowValidator.cs` — RequiredFields +=
- `src/Invekto.Automation/Services/NodeHandlers/CustomerStatusChangedTriggerHandler.cs` (yeni)
- `src/Invekto.Automation/Services/Jobs/TriggerCustomerStatusFlowJob.cs` (yeni)
- `src/Invekto.Automation/Data/AutomationRepository.cs` — `GetActiveCustomerStatusFlowsAsync` + `CustomerStatusFlowInfo`
- `src/Invekto.Automation/Program.cs` — handler DI
- `arch/errors.md` — INV-INM-008 + INV-AT-088
- `tests/InvektoServis.Tests/Shared/Inma/CustomerStatusFlowSuppressionTests.cs` (yeni, 18/18 PASS)

## Error Codes

INV-INM-008 (Backend enqueue fail, WARN/200) · INV-AT-088 (Automation job exec fail). `arch/errors.md` + `ErrorCodes.cs`.

## Flow context değişkenleri (downstream node'lar için)

`{{customer_status_group}}` · `{{old_customer_status}}` · `{{new_customer_status}}` · `{{customer_status_changed_by}}`

## Deploy ✅ DONE (2026-06-13)

**Backend + Automation deploy edildi** (C3a kodu yalnız bu iki serviste; additive Shared değişikliği diğer 9 servis için inert + canlı Outbound'u gereksiz restart riski yok). Migration YOK. `server-deploy` (stop→zip→upload→extract→start→health, appsettings.Production.json korunur) → **10/10 HEALTHY**, SPA `index-DB6P83VO.js`. Webhook AllowedIps (78.135.105.25 events kanalı) + cxapi gate'leri config-restore ile korundu. **CANLI ama inert** — henüz `customer_status_changed` trigger node'lu flow yok (C3b UI gelmeden SQL ile hand-seed edilebilir; smoke ertelendi). SPA rebuild bundle'ları git rename (97-99% identical) = source değişmedi, sadece hash churn.

## Bilinen Açık Uçlar / Backlog (C3a DIŞI)

- **Reliability hardening (go-live öncesi):** bounded-retry + idempotency dedupe-table + transactional outbox. Şu an AutomaticRetry=0 → transient blip'te tek otomasyon kaybı mümkün (welcome/cron precedent'i; replay C2'de dedupe'lü). One-shot event olduğu için welcome'dan daha lossy (re-engage yok).
- **Per-lead fan-out:** bir telefon → N lead → N otomasyon (kanal-seviye dedup yok); Q kararı per-lead.
- **Synthetic-session mesaj teslimi:** welcome/cron ile aynı yolu miras alır — C3a yeni send-path eklemez (deploy sonrası doğrula).
- **Medipol (100000001):** `webhook_secret` set DEĞİL → C2 event'i 401; C3 prereq.
- **text-mode event (featureGroupId null):** yalnız catch-all (boş feature_group_id) flow eşleşir.

## C3b — Flow Builder UI node + cxapi katalog dropdown ✅ DONE (Codex PASS iter 0)

> **Slug:** `20260613-feat-inma-pipeline-v2-c3b-flow-node-catalog` | **Risk:** MEDIUM | **Codex:** PASS iter 0 (12/12 CQ + 4/4 CoVe, 0 blocker)
> **Plan:** `arch/plans/20260613-feat-inma-pipeline-v2-c3b-flow-node-catalog.json` | **Build:** .NET exit 0 + SPA tsc/vite exit 0 (bundle `index-E1nc-dJ3.js`)

C3a backend trigger borusunu **görünür + kurulabilir** yaptı. SPA `customer_status_changed` trigger node (webhook_trigger aynası) + Backend-direct, tenant-scoped, **24h cache**'li cxapi katalog proxy (`GET /api/v1/customer-feature-groups`) → feature_group_id picker.

**Kararlar (interview + Codex critique):**
- **node.data.feature_group_id** = numeric STRING (catch-all = `''`, backend C3a empty=catch-all; non-numeric sessizce skip → picker yalnız `''` veya sayısal yazar). defaultData yalnız `{label}` → fresh node = catch-all.
- **Single-trigger slot fix** (Codex zorunlu): her flow undeletable `trigger_start` seed eder → `flow-store.addNode` trigger-kategori node bırakılınca mevcut trigger'ı DEĞİŞTİRİR (outgoing edge + pozisyon korunur, undoable). Yoksa customer_status_changed kullanılamaz olurdu.
- **Metin-modu (selectionMode=3) gruplar** dropdown'da disabled + not + onChange hard-guard (featureGroupId=null gelir → özel eşleşme asla tetiklemez).
- **WapCRM'siz tenant** (katalog 422 INV-BE-132) → bilgi notu + node catch-all çalışır.
- **features[] read-only** gösterilir ("gruptaki HERHANGİ bir değişiklikte tetikler, tek durum seçilemez").
- **TTL 24h** (Q notu; isimler nadiren değişir, ids stabil, change-event YOK) + manuel "Kataloğu yenile" butonu + cache-invalidate endpoint. (Codex 1h önerdi — Q kararı bekliyor.)

**Mimari:** Backend BFF zaten cxapi'yi direkt proxy'liyor (wa-templates prod'da canlı → egress IP whitelisted) — izolasyon ihlali yok. Yeni Shared `WapCrmFeatureGroupCatalogClient` (WapCrmTemplateClient aynası: per-request X-CIB-SecretKey, SSRF-fixed base, AllowAutoRedirect=false, throw-on-failure) + `WapCrmFeatureGroupCatalogCache` (InmaDynamicFieldsCache aynası: single-flight + Invalidate, failures NOT cached). INV-BE-132 (not-configured 422) + INV-BE-133 (upstream-fail 503).

**Files (16):** Shared DTOs/client/cache + ErrorCodes; Backend Program.cs (DI + jwt prefix + 2 endpoint); SPA flow.ts/graph-validator/node-metadata/NodePalette/nodes-index/CustomerStatusTriggerNode.tsx/flow-store/api.ts/NodePropertyPanel; arch errors.md + contract. **No migration.** Additive Shared → diğer 10 servis inert.

**Deploy:** PENDING (Q-gated; Backend yeniden deploy + SPA bundle). **Smoke (AC10):** 5050 hand-seed customer_status_changed flow (SQL) + uçtan uca tetik doğrula; Medipol secret'a dokunma (Q kararı).

## Sıradaki

**C4** `Set Customer Status` action (INMA `customer-feature-groups/update` endpoint + `invekto-{flowRunUuid}` ClientRequestID loop-guard, `CustomerStatusFlowSuppression.OriginRequestIdPrefix` const'ını reuse eder).
