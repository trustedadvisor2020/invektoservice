# FEAT-PROJELER + cxapi Gönderim Motoru — Roadmap

> **Durum:** IN PROGRESS (PR-1/PR-2/PR-3a DONE; PR-3b BLOCKED-G12; PR-4 TODO; **PKT-14 Projeler S1+S2+S3+S4 ✅ DONE — paket TAMAM**) · **Oluşturma:** 2026-06-06 · **Sahip:** Q
> **Projeler paketi = PKT-14** (fresh global packet no, Q kararı 2026-06-07; README'deki "PKT-2" = ilgisiz DONE "Sağlık Core"). Roadmap-içi etiket olarak "PKT-2" kalabilir ama plan packet_id = `PKT-14`. 4 slice: S1 schema ✅ · S2 CRUD ✅ · S3 cxapi template-list ✅ · S4 React UI ✅. **Şablon picker + gönderim PR-4'te** (S2 CRUD'da cxapi config kolonları INERT).
> ⛔ **GATE P0-3:** Hiçbir tenant CxapiSend allowlist'ine EKLENMEZ — PR-3b inene VEYA cxapi delivery-callback'lerinin `/api/v1/webhook/delivery-status`'a routelanMADIĞI kanıtlanana kadar. Kod default-OFF/inert.
> **Master tablo linki:** `tracking/README.md`
> **Codex consult:** 2026-06-06 (critique) — 4-PR split + P0 sertleştirme önerisi benimsendi.

## Amaç

INSE bulk WhatsApp gönderimini INMA Main App köprüsünden **doğrudan WapCRM External API**'ye (`cxapi.wapcrm.net/api/chatoperation`) taşımak + **WhatsApp onaylı şablon (HSM)** gönderimini açmak. Üstüne TONIVA mode4 benzeri **"Projeler"** yönetim katmanı (data list hedefle → "arama yerine template at").

## Mimari katmanlar

```
Projeler sayfası (PKT-2)
   └─ Proje (projects tablosu, project_id FK)
        └─ Run = bulk_send_job
             └─ Bulk motoru (preview→confirm→broadcast)  ← değişmez
                  └─ Gönderim: cxapi /chatoperation (PKT-1)  ← text + approved-template
```

## Kilitlenen kararlar (interview 2026-06-06)

- **Yol:** doğrudan cxapi `/chatoperation` (köprü değil). Sebep: approved-template köprüden gidemiyor + gerçek statusCode görünürlüğü + optout zaten direkt.
- **Kapsam:** hem INSE-render düz metin hem cxapi approved-template.
- **Dinamik içerik SADECE approved-template `parameters[]`** ile; düz metin statik. Mevcut DMP/dynamicMessage cxapi yolunda **devre dışı (HARD FAIL, sessiz fallback yok)**.
- **Faz 1:** sadece bulk (`broadcast_id != null`) cxapi'ye; trigger/transactional köprüde kalır.
- **instance_id + template** bilgisi bulk-send request'inden (PKT-2'de proje seviyesinden).
- **Auth:** IP whitelist OK (Outbound IP), `secret_key` tenant settings'ten, `userID=WapCrmSettings.UserId`. Opt-out: cxapi server-side INMA + INSE OptOutManager (double-guard).
- **Feature-flag (CxapiSend) + tenant allowlist** ile kademeli cutover.
- **Proje↔Run modeli:** yeni `projects` parent + run = `bulk_send_job` (project_id FK). Bulk motoru değişmez.
- **🔒 Secret repo'ya YAZILMAZ** — sadece `tenant_registry.settings_json->'wapcrm'.secret_key`.

## Codex P0 sertleştirmeleri (tüm PR'lara işlenecek)

1. **Per-request secret:** `HttpRequestMessage` + `TryAddWithoutValidation("X-CIB-SecretKey")` — ASLA `DefaultRequestHeaders` (cross-tenant leak). Tenant'ın `instance_id` yetkisi doğrulanır. Credentials batch'te distinct tenant için toplu yüklenir (N+1 yok). Secret loglanmaz.
2. **Idempotency state machine:** `queued → leased → posting → submitted → provider_failed / ambiguous`. **`posting` crash'i otomatik requeue EDİLMEZ → `ambiguous` + manuel/ops.** Timeout = ambiguous (retry değil). Sadece 301/302'de güvenli delayed retry.
3. **DMP HARD FAIL:** `CxapiSend + çözülmemiş {{}}` → reject; `useDynamic + param_mapping yok` → reject; eksik required param → reject. **Allowlist öncesi geçmiş DMP kullanımı taranır.**
4. **Status split + structured provider alanları:** HTTP 200 + `status=false` = `provider_failed`. Alanlar: `provider_status_code/status/request_id/error_message, last_attempt_at, attempt_count`.
5. **Immutable route:** `outbound_messages.send_route (mainapp_bridge|wapcrm_cxapi)` + `message_kind (plain_text|wapcrm_template)` — mesaj oluşturulurken karar; broadcast homojen (tek template/lang/instance).
6. **RateLimiter key = `tenant_id + instance_id`**; 301/302 → cooldown + Retry-After + backoff/jitter, final-fail değil.
7. **ext_id:** unique `(tenant_id, ext_id)` (global değil); validasyon preview/confirm'de. **→ PR-3'e taşındı (Codex CQ9/Q2 + Q kararı 2026-06-06):** tenant-scoped uniqueness'i tenant-blind lookup ile birlikte eklemek cross-tenant ambiguity'yi resmîleştirir; unique index + tenant-scoped `FindMessageByExternalIdAsync` PR-3'te G12 onayından sonra atomik gider. PR-1 ext_id'ye dokunmaz.
8. **Reserved nullable kolonlar:** `template_header_media JSONB`, `template_language` — şimdi aç, implement etme.

## Paket sırası (Q kararı: PKT-1 motor önce, 4 alt-PR)

| Paket | Slug | Kapsam | Risk | Durum |
|---|---|---|---|---|
| **PR-1** | `20260606-cxapi-pr1-schema` | Migration 055: **outbound_messages** (send_route, message_kind, instance_id, template_*, provider_*, attempt_count) + **outbound_broadcasts** (send_route + template_*) nullable/defaulted kolonlar + 3 CHECK + DTO opsiyonel alanlar + OutboundRepository read projection + canonical outbound.sql sync + INV-SEED-055. **Davranış DEĞİŞMEZ (no-op).** ~~ext_id unique~~→PR-3 (CQ9/Q2+G12); ~~bulk_send_jobs cxapi kolonları~~→PR-4 (CQ11: canonical .sql'i yok). | MEDIUM | ✅ **DONE** · build PASS · **Codex PASS (iter3, 12/12 CQ)** |
| **PR-2** | `20260606-cxapi-pr2-sendclient` | `WapCrmSendClient` (IHttpClientFactory, per-request secret, envelope parse→typed result, 301/302 backoff+jitter→RateLimited) + 13 fake integration test. **Prod routing YOK (no-op).** Client+DTO'lar **Shared/Contracts/Inma** (opt-out client ile aynı yer); RateLimiter'a DOKUNULMADI (PR-3); 301/302 dual-detection (HTTP veya envelope, status==true otoriter), AllowAutoRedirect=false + UseCookies=false. | MEDIUM | ✅ **DONE** · build PASS · 13/13 test PASS · **Codex PASS (iter0, 12/12 CQ + 4/4 CoVe)** |
| **PR-3a** | `20260606-cxapi-pr3-plaintext-cutover` | Düz-metin bulk cxapi (flag+allowlist arkası): sender route dalı + immutable route write-at-create + tenant-default instance (settings_json→'wapcrm'.instance_id) + state machine ('posting'+'ambiguous' yeni; submitted→'sent'/provider_failed→'failed' reuse) + provider_* persistence + DMP hard-reject + RateLimiter (tenant,instance) key+cooldown + batch creds (N+1 yok) + metrics/log. Migration **056** = chk_message_status genişlet (+'posting','ambiguous') + INV-SEED-056 + canonical sync. Yeni kodlar INV-OB-062..065. | HIGH | ✅ **DONE** (2026-06-07, commit `0415d68c` · 14 dosya, +939/-46) · build PASS (full solution) · 10/10 test PASS · isolation PASS · **Codex Q FORCE PASS** (codex_review gpt-5.5 iter1/2/3 FAIL; her iter gerçek bulgu → folded: INV-OB-064/065 terminal-path emission, instance-consistency guard, misconfig/sweep/parse INV tags; tek kalan DECISION_CONFLICT = CQ9/CQ11/Q2 Outbound paylaşılan tenant_registry okuması + tenant-blind boot sweep'leri, İKİSİ DE pre-existing shipped pattern [GetTenantHealthInfoAsync:1414 + ResetSendingMessagesAsync:686] + arch/tenant-isolation.md:27 sanctioned). ⛔ **LIVE-ENABLEMENT GATE (P0-3): hiçbir tenant allowlist'e PR-3b inene VEYA delivery-callback'lerin /api/v1/webhook/delivery-status'a routelanMADIĞI kanıtlanana kadar — kod default-OFF/inert.**) |
| **PR-3b** | `20260606-cxapi-pr3b-extid` | **ext_id composite UNIQUE (tenant_id, external_message_id) + tenant-scoped FindMessageByExternalIdAsync + delivery-status webhook'a tenant_id wiring (atomik)** — **G12 onayı GEREKLİ** (PR-3a'da G12 hâlâ açık olduğu için ayrıldı; tenant-blind lookup dururken ext_id eklemek cross-tenant ambiguity yaratır, Codex CQ9/Q2). | MEDIUM | BLOCKED (G12) — PR-3a (`0415d68c`) ext_id'siz shipped (planlı); G12 onaylanınca açılır + live-enablement gate kalkar |
| **PR-4** | `20260606-cxapi-pr4-approved-template` | **bulk_send_jobs cxapi kolonları (instance_id/template_kind/wa_template_id/param_mapping/template_language) migration + BulkSendRepository writer + canonical bulk_send_jobs .sql sync (PR-1'den taşındı, CQ11)**, `wa_template_id` + `param_mapping` + per-recipient parameters[] + preview/confirm validasyon (required param, language, ownership). | HIGH | TODO |
| **PKT-14** (roadmap-local "PKT-2") | `20260607-projeler-s1-schema` (S1) | **Projeler yönetim katmanı — 4 slice (Q kararı 2026-06-07: slice + full parent model + fresh PKT no).** **S1 ✅ DONE** = migration 057 (`projects` full parent model: lifecycle status + project-level cxapi config + denormalized roll-up counters + soft-delete-as-archive; `project_targets` junction 1→N data_lists; `bulk_send_jobs.project_id` nullable composite FK ON DELETE RESTRICT) + canonical `outbound.sql` sync + INV-SEED-057. INERT (schema-only, gate-bağımsız). **S2 ✅ DONE** = projects CRUD (Outbound ProjectsRepository/ProjectsService + 5 endpoint `/api/v1/projects` + ProjectDtos[Shared] + ProjectsOptions gate[default-OFF, ContactList aynası] + INV-OB-066..071; embedded set-based targets + **FOR SHARE** target lock [soft-delete race kapalı] + in-tx detail [post-commit reload yok] + soft-delete-as-archive; cxapi config/counter INERT→PR-4) · **S3** = cxapi template-list endpoint (Backend proxy + WapCrmTemplateClient) · **S4** = Projeler React page+wizard (DataImportPage wizard pattern, X-only modal) + Backend proxy routes. TONIVA mode4 = yapı ref (telephony YOK, kod karıştırma YOK). **S3 ✅ DONE** = `GET /api/v1/settings/wa-templates` (Backend-direct, read-only, gate-dışı) + `WapCrmTemplateClient` (Shared/Contracts/Inma; per-request X-CIB-SecretKey, fixed base URL [SSRF], AllowAutoRedirect=false, typed outcome) + INV-BE-127..130; sibling of settings/instances; Codex PASS iter1 (12/12+5/5), commit `bd91ebd4`. **S4 ✅ DONE** = Projeler React sayfası (list + tek X-close create/edit modal: ad+açıklama+çoklu data-list hedef, sendable_count; Arşivle) + 5 Backend proxy route `/api/v1/outbound/projects*` (data-lists GR4 mirror) + api.ts client/types + /projects route + dual-source nav (Layout.tsx + /api/v1/inma/nav); metadata-only (şablon/gönderim PR-4); Codex PASS iter0 (12/12+4/4), commit `aa00a34a`. | HIGH | **PAKET TAMAM** — S1 (iter0) + S2 (iter4, `2bdb9da9`) + S3 (iter1, `bd91ebd4`) + S4 (iter0, `aa00a34a`). Deploy: migration 057 + Outbound + Backend + SPA bundle sonraki `/deploy` ile. |

## Açık sorular (INMA ekibine)

- **G12:** cxapi `/chatoperation` response `requestID`, delivery-status webhook'taki id ile **birebir aynı mı**? (ext_id eşleşmesi buna bağlı — teyit alınmadan delivery-status migration yapılmaz.)
- **G13:** cxapi approved-template: `templateId` formatı, language kaynağı, parametre positional mı named mi, required param count nereden bilinir, header media zorunlu şablonda davranış?
- (G9 ✓ INMA server-side opt-out yapacak · G11 ✓ Outbound IP whitelist OK)

## Referanslar

- Plan JSON'lar: `arch/plans/20260606-cxapi-pr*.json`
- Entegrasyon kılavuzu: `temp/wapcrm-api-integration-guide-for-agents.md` (INMA ekibinden)
- Mevcut bulk: `src/Invekto.Outbound/Services/{BulkSend,Broadcast}Orchestrator.cs`, `MessageSenderService.cs`
- cxapi okuma örneği: `src/Invekto.ChatAnalysis/Services/WapCrmClient.cs`
- TONIVA referans: `c:\CRMs\TONIVA` mode4 `ivr_campaigns` (sadece yapı referansı — kod karıştırma yok)
