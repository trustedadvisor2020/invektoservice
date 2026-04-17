# SPEC: Natural Send Pipeline (HFM-1)

> **Spec ID:** HFM-1 | **Paket:** 20260417-human-feel-multilang-pilot | **Risk:** MEDIUM
> **Yazar:** Q | **Son Güncelleme:** 2026-04-17 | **Durum:** IMPLEMENTED

## 1. Intent (Ne & Neden)

Mevcut `message_text` node tek balon gönderiyor. 200 karakterlik bir welcome mesajı, anında, tek parça → WA ekranında "robot yazdı" hissi. Adavista pilotu "insan hissi" omurgasıyla çalışacak; chunk + jittered delay + think time primitifleri pilot öncesi hazır olmalı.

Mevcut güçlü parçalar: `HashBasedTemplateRotationService` (FNV-1a deterministik A/B), `MessageSenderService` (rate limiter + retry), flow engine `List<string> Messages` pipeline'ı. Eksik: chunk planlayıcı + orchestrator dispatch delay.

Research arka planı: `arch/platform/inma-inse-unification/human-feel-multilang-research.md` §3.2 — ECIS 2018 "Faster is not always better", 2025 Utrecht "uncanny valley", WA best practice (~134 char/balon).

## 2. Acceptance Criteria

| # | Kriter | Doğrulama |
|---|--------|-----------|
| AC-1 | `message_text` node'una `text_chunks: string[]` JSON array field | Flow config save/load roundtrip |
| AC-2 | `text_chunks` yoksa ve `text` içinde `\n\n` varsa auto-split (soft opt-in) | Unit test `MessageTextHandler.ResolveChunks` |
| AC-3 | `MessageChunkPlanner.Plan` formula + ±15% jitter + 8s total cap | Unit test (formula boundaries + proportional scale) |
| AC-4 | Orchestrator chunked payload'ı sentinel ile tespit + Task.Delay/callback | `DispatchMessageOrChunksAsync` integration |
| AC-5 | Simulation mode delay skip (SimulationEngine path'i değişmez) | `ctx.IsSimulation=true` dalı |
| AC-6 | Geriye uyumluluk: eski flow'lar (text-only, chunking yok) değişmez | Regression — mevcut AC testleri |
| AC-7 | Rate limiter + retry her chunk için korunur (her chunk ayrı callback) | Mevcut MessageSenderService pipeline |
| AC-8 | Invalid text_chunks JSON → INV-AT-062 warn + legacy text fallback | `MessageTextHandler.ResolveChunks` catch |

## 3. Architectural Decisions

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| Chunk payload encoding: `"\u001EHFM1_CHUNKS\u001E{json}"` sentinel prefix | FlowEngineV2 pure kalır (no new API). `List<string> Messages` stream değişmez. Sentinel ASCII Record Separator — doğal metinde bulunmaz | EXPECTED: sentinel pattern kasıtlı, CQ4 "string manipulation" flag = intentional |
| Jitter source: `Random.Shared` (non-deterministic by default) | Unit test için `Func<double>` inject edilebilir. Trace-only değer | EXPECTED: CQ7 non-determinism = intentional, test-injectable |
| Chunk rate limiter: her chunk ayrı queued message | Aggregation yok; tenant QPS low ise chunk'lar doğal throttle olur | EXPECTED: plan Q1 — tenant quota "chunk = 1 slot" design |
| Soft opt-in `\n\n` auto-split | Operator yeni field öğrenmeden chunking kazanır | AC-2 explicit |
| Total cap 8s | WA typing_on 25s timeout + UX ceiling | Research §3.1 |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| Node data schema | mevcut (additive `text_chunks` JSON string field) |
| Error Codes | `arch/errors.md` INV-AT-062 (ChunkScheduleInvalid) |
| Shared Service | `Invekto.Shared/Services/IMessageChunkPlanner.cs` |

## 5. Scope Boundaries

### In Scope
- `MessageChunkPlanner` + `IMessageChunkPlanner` (Shared)
- `MessageTextHandler` chunk resolution + sentinel emit
- `AutomationOrchestrator.DispatchMessageOrChunksAsync` chunk-aware dispatch
- `ActionDelayHandler` dokunulmaz (existing delay primitif korunur)

### Out of Scope (Explicit)
- WA `typing_on` API çağrısı (Q kararı: jitter yeterli, J-TYPE INMA'dan istenmiyor)
- Per-character typing simulation
- Tone matrix (HFM-3 post-pilot)
- Flow builder UI chunk editor (opsiyonel sonraki paket)

### Değişmeyen Alanlar
- HashBasedTemplateRotationService (PROD-READY)
- MessageSenderService callback pipeline
- FlowEngineV2 List<string> Messages API
- Rate limiter + retry

## 6. Service Boundaries

| Servis | Rol | Değişiklik |
|--------|-----|-----------|
| Shared | MessageChunkPlanner | NEW (additive) |
| Automation | MessageTextHandler + Orchestrator dispatch | Modified (additive) |
| Outbound | MessageSenderService | No change |

## 7. Risk & Mitigation

| Risk | Olasılık | Mitigation |
|------|----------|------------|
| Chunk arası cancel race | LOW | `OperationCanceledException` propagate, sonraki chunk dispatch olmaz |
| Sentinel collision (operator metni sentinel'a benzer) | VERY LOW | Record Separator (`\u001E`) ASCII control char, doğal metinde yok |
| Rate limiter quota burst (3 chunk = 3 slot) | LOW | Tenant QPS planlama pilot öncesi yapılacak (~10 msg/sec Dent pilot için yeterli) |
| Total cap 8s operator metni kesiyor | LOW | Planner proportional scale — tüm chunk'lar gönderilir, delay kısaltılır |
