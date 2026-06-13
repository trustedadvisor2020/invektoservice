# FEAT-INMA-PIPELINE-V2 Chunk 2 — INSE inbound consumer (customer.selection_changed)

> **Slug:** `20260613-feat-inma-pipeline-v2-c2-inbound-consumer` | **Risk:** MEDIUM | **Status:** DONE+DEPLOYED+VERIFY-LIVE 2026-06-13
> **Commits:** `ab2e644e` (feat, Codex PASS iter2) · `3667f0ca` (HMAC recipe confirmed live) · `385c3ae5`+`ce10c626` (docs)
> **Parent:** [FEAT-INMA-PIPELINE-V2](README.md#feat-inma-pipeline-v2) (5 chunk) | **Plan:** `arch/plans/20260613-feat-inma-pipeline-v2-c2-inbound-consumer.json`
> **Contract:** `arch/contracts/inma-customer-status-webhook.json` (v2.0 FINAL) | **Decision:** [project_inma_pipeline_v2_decision](../../../Users/taner/.claude/projects/c--CRMs-InvektoServices/memory/project_inma_pipeline_v2_decision.md)

## Karar Özeti

INMA, bir agent müşterinin feature-group seçimini değiştirince **imzalı** `customer.selection_changed` system event'ini **MEVCUT** `POST /api/v1/webhook/event?companyId=X` webhook'una gönderir. C2 bunu tüketir. (Dedike `/api/v1/inbound/inma/customer-status-change` endpoint fikri **SUPERSEDED** — Q kararı 2026-06-10, cxapi ack-ingress reuse pattern'i; abonelik zaten bu URL'e canlı kuruldu.)

**Akış:** type-branch (mesaj/ack kontrolünden ÖNCE) → fail-closed HMAC-SHA256 verify → dedupe(tenant_id,event_id) + durable raw audit → opaque `leads.customer_status` derive → 2xx. Tenant SADECE authenticated TenantContext'ten (`?companyId`), body companyId DEĞİL (audit-only).

## Interview Kararları (Q, AskUserQuestion 4/4 — hepsi öneri)

| Konu | Karar |
|------|-------|
| Kapsam | **MVP**: ingress + HMAC + dedupe + raw audit + opaque store. Flow-trigger forward = C3, cxapi fetch-verify = ertelendi |
| Storage | Yeni `leads.customer_status TEXT` kolonu (mevcut `pipeline_status` DOKUNULMADI) |
| HMAC secret | `settings_json->'inma'->>'webhook_secret'` (MSSQL `CompanyWebhookEndpoints.Secret` base64-32-byte) |
| Telefon eşleşmezse | drop-with-audit + WARN (INV-INM-005); lead-create YOK, cxapi YOK; raw event korunur (replay) |
| HMAC encoding | fail-closed + verify-live (PDF yoktu) |

## Acceptance Criteria (Codex PASS iter2: 12/12 CQ + 5/5 CoVe)

| ID | Kriter | Sonuç |
|----|--------|-------|
| AC1 | Migration 065: `leads.customer_status` + `customer_status_occurred_at` + functional phone idx + `inma_webhook_events` (UNIQUE tenant_id,event_id) + GRANT | ✅ prod'da çalıştı |
| AC2 | `type`==customer.selection_changed branch, msg/ack'ten ÖNCE; raw body 1x okunur (mevcut path'ler byte-identical) | ✅ |
| AC3 | Fail-closed HMAC (eksik sig/secret/malformed/mismatch → 401, yazma YOK), constant-time | ✅ |
| AC4 | Idempotency: ON CONFLICT DO NOTHING (1 tx); raw_body_sha256 ile divergent-body tespit (INV-INM-006) | ✅ |
| AC5 | Derivation (single→names[0]; []→NULL; multi→alfabetik CSV; text→textValue); phone-miss INV-INM-005; persist-fail 5xx | ✅ |
| AC6 | occurredAt monotonic guard (eski event yeni state'i ezmez/anchor'ı NULL'a resetlemez — COALESCE + only-fill-if-empty) | ✅ |

## Files Changed

- `arch/db/migrations/065-inma-customer-status-inbound.sql` (yeni)
- `src/Invekto.Shared/Constants/ErrorCodes.cs` — INV-INM-001..007
- `arch/errors.md` — INV-INM inbound pipeline bölümü
- `src/Invekto.Shared/Contracts/Inma/Webhooks/CustomerSelectionChangedEvent.cs` (yeni) — DTO + `CustomerStatusMapping`
- `src/Invekto.Backend/Services/Inma/InmaWebhookSignatureValidator.cs` (yeni) — fail-closed HMAC
- `src/Invekto.Backend/Services/Inma/InmaWebhookOptions.cs` (yeni)
- `src/Invekto.Backend/Data/InmaWebhookEventRepository.cs` (yeni) — dedupe+apply 1 tx
- `src/Invekto.Backend/Data/TenantRegistryRepository.cs` — `GetInmaWebhookSecretAsync`
- `src/Invekto.Backend/Program.cs` — handler type-branch + `HandleCustomerSelectionChangedAsync` + DI + raw-body refactor
- `src/Invekto.Backend/appsettings.json` — `InmaWebhook` config

## Deploy + VERIFY-LIVE (2026-06-13, gerçek 5050 event)

1. Migration 065 prod'a uygulandı (DO $verify$ INV-SEED-065 PASS).
2. Backend deploy HEALTHY.
3. 5050 `webhook_secret` settings_json'a yazıldı (MSSQL `CompanyWebhookEndpoints.Secret`, base64→32 byte).
4. **BLOKER 1 (çözüldü):** events webhook AYRI IP'den (`78.135.105.25`) geliyor → IP-whitelist'te yoktu → HMAC'ten ÖNCE `401 INV-AUTH-003`. Fix: prod `Webhook:AllowedIps += 78.135.105.25` + restart. (Bkz. deploy-info.md "Webhook AllowedIps".)
5. **BLOKER 2 (çözüldü):** HMAC reçetesi bilinmiyordu (PDF yok). Geçici diag ile gerçek imza yakalandı + brute-force.
6. **VERIFY-LIVE PASS:** gerçek event → `C2 customer_status applied ... leads=1 sigEnc=hex`, `leads.customer_status='Uçak Bileti'`, occurredAt guard OK, audit row tam (actor_type=api, originRequestId echo).
7. Test artifact temizliği: INMA selection restore + test lead + livetest event satırları silindi.

### HMAC reçetesi (CONFIRMED — InmaWebhookOptions'a locked)

- **key** = base64 secret'in **STRING'i (UTF-8 byte, base64-decode YOK)** → `KeyEncoding=utf8`
- **message** = `{X-Invekto-Timestamp (unix saniye)}.{ham gövde byte}`
- **digest** = hex, `sha256=` prefix → `SignatureDigestEncoding=hex` + `SignaturePrefix=sha256=` + `AcceptEitherEncoding=false`

## Error Codes

INV-INM-001 (sig invalid 401) · 002 (secret yok 401) · 003 (malformed 400) · 004 (persist fail 500) · 005 (phone-unmatched WARN 2xx) · 006 (divergent-body WARN 2xx) · 007 (body too large 413). `arch/errors.md` + `ErrorCodes.cs`.

## Bilinen Açık Uçlar / Backlog (hardening — C2 kapsamı DIŞI)

- **EnforceTimestampFreshness=false** (format=unixSeconds doğrulandı). Dedupe replay'i kapatıyor; freshness opsiyonel sertleştirme (24h window, retention job sonrası anlamlı).
- **`inma_webhook_events` 30-gün retention job YOK** (deferred). Volume küçük; `idx_inma_webhook_events_received_at` hazır. Hangfire LogCleanupService pattern'i ile eklenebilir.
- **Medipol (100000001)** C2 kod kapsamında ama `webhook_secret` set DEĞİL + events aboneliği yok → event gelirse 401 INV-INM-002. C3/C4 scope genişlemesi öncesi prereq.
- **Multi-IP riski:** INMA gelecekte ek events outbox IP kullanabilir → sessiz 401. İzleme: `/webhook/event` 401 INV-AUTH-003 spike'ı.
- Orphan audit rows (matched lead silinince) — benign, retention job temizler.

## Sıradaki

**C3** Flow Builder `customer_status_changed` trigger kanalı (C2 zaten `actor_type` + `origin_request_id` audit'liyor → suppress için hazır) → **C4** `Set Customer Status` action (INMA update endpoint + ClientRequestID loop-guard).
