# FEAT-INMA-PIPELINE-V2 — C3 HARDENING (flow-trigger durable outbox + exactly-once + C4 retry)

> **Slug:** `20260615-feat-inma-pipeline-v2-c3-hardening` | **Risk:** MEDIUM | **Plan:** [`arch/plans/20260615-feat-inma-pipeline-v2-c3-hardening.json`](../arch/plans/20260615-feat-inma-pipeline-v2-c3-hardening.json)
> **Status:** CODE DONE — build + /rev pending | **Migration:** 066

## Problem (confirmed in code)

C2 (`InmaWebhookEventRepository.PersistAndApplyAsync`) committed `leads.customer_status` in ONE tx, then C3a (`Program.cs DispatchCustomerStatusFlowTriggers`, AFTER commit) enqueued `TriggerCustomerStatusFlowJob` per changed lead. A crash / Hangfire-enqueue failure **between commit and enqueue lost the trigger permanently** — INMA already got 200, and C2's `ON CONFLICT DO NOTHING` means a re-delivery never re-dispatches. Job was `[AutomaticRetry(Attempts=0)]` (lossy).

## Decision (Q interview + pre-impl codex_consult)

Q scope: **C2→C3a outbox + C4 retry**; idempotency folded into outbox UNIQUE; outbox-only drain.
- Refinement (Q-accepted): UNIQUE key needs `lead_id` (event fans out to N leads via phones[]) → **`UNIQUE(tenant_id, event_id, lead_id)`** (still event_id-anchored).
- codex_consult(critique) surfaced 2 real blind spots → folded in:
  1. naive outbox→Hangfire is **at-least-once** (FlowEngine sends WhatsApp mid-flow, not idempotent) → added **job-side atomic claim** (`processing`→`done` before any side effect) = exactly-once-at-start.
  2. preflight `NpgsqlException` swallow = silent loss → preflight now **THROWS** + `AutomaticRetry=2`; execution phase still swallows (no mid-flow double-send).
- Codex cross-event-ordering concern **already closed** by C2 `occurredAt` monotonic guard + `StoredStaleSkipped` (InmaWebhookEventRepository.cs:93-127) — stale event writes no outbox row.

## Design

| Layer | Behavior |
|-------|----------|
| **Outbox INSERT** | Inside the C2 tx, on `StoredApplied` + changed lead + `dispatch.Allowed` (suppression==Fire). `ON CONFLICT(tenant_id,event_id,lead_id) DO NOTHING`. |
| **Drain** (Backend timer `CustomerStatusFlowOutboxDrainJob`, 10s) | Recover stale `processing`→`pending` at startup; claim `pending`→`processing` (`FOR UPDATE SKIP LOCKED`); enqueue `TriggerCustomerStatusFlowJob`; on enqueue fail bounce → `pending` (INV-INM-008) / `failed` after MaxAttempts (INV-INM-009). attempts++ ONLY on enqueue fail. |
| **Job claim** | `TriggerCustomerStatusFlowJob` after retryable preflight lookup → atomic `processing`→`done` claim; 0 rows ⇒ skip (no double-fire). |
| **C4 retry** | `BackendCustomerStatusClient.UpdateAsync` bounded transient retry (timeout/transport/5xx; 2 retries, 500/1500ms); business/auth (4xx, vendor 903/920-923) no retry. Stable ClientRequestID = value-idempotent. |

State machine: `pending` → `processing` (drain) → `done` (job claim) | `failed` (drain exhausted). Stale recovery: `processing`→`pending`.

## Files (14)

- **NEW** `arch/db/migrations/066-customer-status-flow-outbox.sql` (migration-only, mirrors 065 — no canonical `arch/db/*.sql` mirror exists for C2 tables)
- **NEW** `src/Invekto.Backend/Data/CustomerStatusFlowOutboxRepository.cs`, `src/Invekto.Backend/Services/CustomerStatusFlowOutboxDrainJob.cs`
- `src/Invekto.Backend/Data/InmaWebhookEventRepository.cs` (outbox INSERT in tx + `CustomerStatusFlowDispatch`)
- `src/Invekto.Backend/Program.cs` (suppression-before-persist + remove inline dispatch + DI register)
- `src/Invekto.Automation/Services/Jobs/TriggerCustomerStatusFlowJob.cs` (eventId param + preflight throw + AutomaticRetry=2 + claim)
- `src/Invekto.Automation/Data/AutomationRepository.cs` (`ClaimCustomerStatusOutboxAsync`)
- `src/Invekto.Automation/Services/BackendCustomerStatusClient.cs` (C4 bounded transient retry)
- `src/Invekto.Shared/Constants/ErrorCodes.cs` + `arch/errors.md` (INV-INM-008 repurpose + INV-INM-009)
- `arch/codex-context.md` (sanctioned notes) + this tracking + plan JSON

## Error codes

- **INV-INM-008** repurposed: was inline C3a enqueue fail → now drain transient enqueue fail (bounce to pending).
- **INV-INM-009** new: drain enqueue retries exhausted → `failed` (ops repair signal).
- C4 retry reuses existing `CustomerStatusUpdateUpstreamFailed` (INV-BE-141).

## Deploy scope (when approved)

Backend + Automation (Shared changed → full-solution build first). Migration 066 before deploy. Pipeline INERT (no flow uses these nodes) → low live-data risk. **Not in scope:** live e2e smoke (Q/INMA + Medipol webhook_secret).
