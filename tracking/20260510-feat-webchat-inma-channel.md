# FEAT-WC-INMA — WebChat as INMA Channel

**Date:** 2026-05-10 (spec accepted) / TBD (implementation)
**Slug:** `20260510-feat-webchat-inma-channel`
**Spec:** [arch/specs/webchat-inma-channel.md](../arch/specs/webchat-inma-channel.md) (ACCEPTED v10)
**Contract:** [arch/contracts/webchat-inma-channel.json](../arch/contracts/webchat-inma-channel.json)
**Callback Extension:** [arch/contracts/integration-callback.json](../arch/contracts/integration-callback.json) (channel + visitor_key + sender_type + sender_name OPTIONAL fields)
**Migration:** [arch/db/migrations/047-webchat-inma-channel-cutover.sql](../arch/db/migrations/047-webchat-inma-channel-cutover.sql)
**Risk:** HIGH
**Status:** SPEC-ACCEPTED — Phase 1 implementation pending INMA endpoint readiness

---

## Ozet

WebChat'i bagimsiz Invekto urunu olmaktan cikarip INMA'nin (WapCRM) yeni bir kanal tipi haline getiriyoruz — WABA ve IG gibi.

**Mimari:**
- Invekto.WebChat → Invekto.WebChat.Gateway (rename) — sadece browser-INMA koprusu
- INMA = single source of truth for messages
- Outbox Pattern + 7-day dual-write window cutover
- Cookie-based visitor identity (full UUID, snake_case `wc-{widget}-{visitor}`)
- SignalR realtime + INMA HTTP relay

## Decision Log

10 iterasyon Codex review sonrasi spec ACCEPTED:
- Mimari kararlar v3'te kapandi (Q soru-cevap turlari ile)
- Format/tutarlilik v4-v10'da islendi
- v10'da kalan 3 issue: 2 false positive + 1 minor type fix (dev sirasinda)
- Q karari: implementation paketinde devam, kod review CODEX UTANSIN ile PASS hedefi

**Q kararlari (ana noktalar):**
- Cutover: 7 gun dual-write window + Outbox Pattern
- History: Eski WebChat dashboard tamamen sil (T+8), backfill yok, history kaybi kabul
- Cookie clear: recovery yok, yeni visitor (WABA'da telefon degistirmek gibi)
- Bridge bug 906/907: ONKOSUL paket fix sonrasi webchat cutover (DB CHECK constraint enforce)

## Phase Plan

### Phase 0 — INMA Spec Delivery (PENDING)
- INMA ekibine teslim: spec + contract dosyalari
- INMA tarafinda gelistirilecek: 3 endpoint (inbound, history, widget_sync) + WEBCHAT enum + callback bridge channel field handling
- Open questions: WEBCHAT enum, service auth, idempotency window onay, operator queue, attachment storage (Phase 2), widget metadata sync

### Phase 1 — Invekto Implementation (PENDING — INMA blocker)
**Onkosul:** PKT-BRIDGE-906-907-FIX paketi tamamlanmali (callback bridge parse bug fix)

- Invekto.WebChat → Invekto.WebChat.Gateway rename + scope refactor
- Inbound forwarder, OutboundReceiver, WidgetSyncClient, OutboxWriter, INMAResponseNormalizer
- 4 Hangfire job (idempotency cleanup, outbox reconciliation 5dk, widget sync retry, outbox archive)
- Widget management API (CRUD, public contract)
- Widget management UI (Invekto Dashboard — Phase 1, Phase 2'de INMA'ya devir)
- Cookie SameSite=None+Secure (HTTPS) + dev/staging Lax fallback
- Eski WebChat tablolari + dashboard cutover scheduling

### Phase 2 — INMA UI Migration (POST-PHASE-1)
- INMA UI widget management sayfasini kullanir (ayni public API)
- Invekto Dashboard widget sayfasi kaldirilir
- Automation flow yonetimi INMA'ya tasinir

## Acceptance Criteria

20 AC tanimli — bkz. [spec §2](../arch/specs/webchat-inma-channel.md#2-acceptance-criteria).

Anahtar AC'ler:
- AC-7: 7 gun dual-write window + Outbox Pattern (`retry_count <= 5`, max 6 attempt)
- AC-15: Bridge fix gate DB CHECK constraint enforce
- AC-16: Idempotency Hangfire saatlik cleanup, unbounded growth yok
- AC-17: Outbox reconciliation 5dk cron, exponential backoff (1,2,5,15,60 dk = 83 dk fail-safe)

## Risk & Mitigation

bkz. [spec §8](../arch/specs/webchat-inma-channel.md#8-risk--mitigation) — 13 risk + mitigation.

## Next Steps

1. **INMA ekibine spec teslimi** — bu dokuman + 2 contract dosyasi
2. **PKT-BRIDGE-906-907-FIX paketi** — onkosul, ayri tracking dosyasi olusturulmali
3. **Phase 1 plan JSON** — INMA endpoint'leri ready oldugunda /auto workflow ile baslar
