# Zoho P4.2 — OAUTH_SCOPE_MISMATCH Investigation

> **Status:** IMPLEMENTED + DEPLOYED 2026-04-16 (commit bf45d29) — Adavista paid-plan retest pending for flag-flip decision
> **Owner:** Q
> **Discovered:** 2026-04-17 (P4.1 deploy + smoke tenant 5050)
> **Resolved:** 2026-04-16 (slug `20260416-zoho-p42-fix`, Codex iter 0 PASS, prod 10/10 HEALTHY)
> **Parent paket:** Zoho Step 4 / P4.1 Stage Mapping editor

## Implementation Summary (2026-04-16)

Fix paketi prod'da. 3 fix uygulandı:

- **Fix 2 (chosen: variant B, not A):** `Zoho:EnableMetadataPath` flag default `false` —
  `TryGetBlueprintMetadataAsync` artık `if (_enableMetadataPath)` gate'inde, method body
  korundu (Adavista paid-plan retest sonrası flag-flip yolu açık).
- **Fix 1:** Yeni `INV-INT-137 LeadsNotInBlueprintProcess` distinct error code; aggregation
  loop'ta `recordNotInProcessCount` counter, `==sampleLeadIds.Count` durumunda INV-INT-137
  throw (vs INV-INT-121). FE `ZohoStageMappingPage` Card title koşullu + dedicated CTA
  paragraph (Manuel ID step-by-step Zoho Setup hint).
- **Fix 3:** `[ZOHO-BP-RAW]` 4 TEMP debug satır kaldırıldı, kalıcı `[ZOHO-BP]` short-form
  structured per-sample line eklendi (status + result tag).

**Codex iter 0 PASS** (12/12 CQ + 4/4 CoVe, 0 blocking). Build PASS. Deploy: Backend +
Integrations + Dashboard SPA, 10/10 HEALTHY (2026-04-16 18:17).

## Problem

`GET /crm/v6/settings/blueprint?module=Leads&layout_id=X` tenant 5050 OAuth token'ıyla
tutarlı olarak `401 OAUTH_SCOPE_MISMATCH` dönüyor. Token scope'u log'da doğrulandı:
`ZohoCRM.modules.leads.ALL ZohoCRM.modules.deals.ALL ZohoCRM.settings.ALL ZohoCRM.users.READ ZohoCRM.coql.READ`.

Bu endpoint P4.1'in "primary metadata path"idir; fail edince fallback (per-lead `/actions/blueprint`
aggregation) devreye girer. Fallback çalışması için lead'lerin Blueprint process'inde olması gerekir —
Dev Edition tenant 5050'de hiçbir lead process'te değil (aşağıdaki bulgular), fallback da fail.

## Root Cause (Evidence-Based)

**Bulgu 1:** Zoho v8 resmi docs'ta `/settings/blueprint?module=...&layout_id=...` endpoint'i YOKTUR.
Yalnızca `/{module}/{record_id}/actions/blueprint` (per-record) dokümante edilmiştir.
[Blueprint API v8](https://www.zoho.com/crm/developer/docs/api/v8/blueprint-details.html)

**Bulgu 2:** `ZohoCRM.settings.ALL` scope'u Blueprint KAPSAMıyor. Resmi kapsamı:
territories, custom_views, related_lists, modules, variables, tags, tab_groups, fields, layouts,
macros, custom_links, custom_buttons, roles, profiles, currencies. Blueprint listede **yok**.
[Scopes v8](https://www.zoho.com/crm/developer/docs/api/v8/scopes.html)

**Bulgu 3:** Hiçbir `ZohoCRM.settings.blueprint.*` veya `ZohoCRM.blueprint.*` scope'u public
dokümantasyonda referanslanmıyor. Community topiclerinde de yok.

**Sonuç:** `/settings/blueprint` muhtemelen Zoho internal/dashboard API — public OAuth scope'uyla
erişilebilir değil. Bizim kod yazmadan önce varsayımımız yanlıştı.

## Secondary Problem — Dev Edition Blueprint Behavior

Q test: tenant 5050'de bir lead'in Blueprint widget'ından bir butona tıkladı (Contacted →
Pre-Qualified). Sonuç: state değişti ama `GET /{lead_id}/actions/blueprint` hala 400
`RECORD_NOT_IN_PROCESS` dönüyor.

**Hipotez:** Zoho Dev Edition'da "Apply Blueprint to existing records" toggle'ı yok (Q doğruladı —
UI'da seçenek görünmüyor). Blueprint buton'a tıklamak sadece field update yapıyor, lead'i Blueprint
process'ine engage etmiyor. Paid plan'larda criteria-based trigger veya "apply to existing" ile
lead'ler otomatik process'e giriyor olabilir.

## Proposed Fixes (Priority Order)

### Fix 1: UX / Banner mesajı (LOW effort, HIGH value)

Mevcut banner: *"blueprint aktif degil olabilir — Zoho Setup → Automation → Blueprint kontrolu yapin."*
Yanıltıcı. Gerçek durum iki ayrı başarısızlık:
- Metadata path fail: OAUTH_SCOPE_MISMATCH (kod tarafında fix yok, Zoho API limit)
- Fallback path fail: tüm örneklenen lead'ler `RECORD_NOT_IN_PROCESS`

Öneri: Error code `RECORD_NOT_IN_PROCESS` detect edilirse özel Turkish mesaj:
*"Zoho'daki lead'ler Blueprint process'ine dahil değil. Seçenekler: (1) Zoho'da Blueprint için
criteria-based trigger tanımla veya mevcut lead'lere manuel uygula (paid plan gerekebilir),
(2) Aşağıdaki satırlarda **Manuel ID** ile transition ID'leri elle gir — Zoho → Setup → Automation
→ Blueprint → ilgili Blueprint'i aç → her transition'a tıkla → URL'deki son segmenti kopyala."*

**Scope:** `ZohoStageMappingPage.tsx` + `ZohoBlueprintClient.cs` — RECORD_NOT_IN_PROCESS'yi
dedicated error code (örn. INV-INT-137 `LeadsNotInBlueprintProcess`) ile surface et.

### Fix 2: Metadata path'i kaldır veya deaktive et (LOW effort)

`/settings/blueprint` çağrısı her Discover'da 401 log + 1 gereksiz request. Çözüm seçenekleri:
- **A. Tamamen kaldır** — `TryGetBlueprintMetadataAsync` method'u sil, direkt fallback'e git.
- **B. Feature flag ile gate'le** — `appsettings:Zoho:EnableMetadataPath=false` default. İleride
  Zoho API değişikliği olursa aç.
- **C. Retry-suppress** — ilk OAUTH_SCOPE_MISMATCH sonrası 24h kara listeye al (IMemoryCache), tekrar
  deneme.

Öneri: **A** — undocumented endpoint'e güvenmek risk, kod sadeleşir.

### Fix 3: Zoho Support ticket (BLOCKED by Zoho)

"`/settings/blueprint` endpoint'i için resmi scope nedir? Public mi internal mi?" sorusuyla ticket aç.
Cevaba göre Fix 2'yi yeniden değerlendir.

### Fix 4 (out of scope): Lead'leri toplu Blueprint'e engage etmek

Dev Edition'da "apply to existing records" yok. Paid plan upgrade gerekebilir. Veya her lead create
sırasında otomatik trigger (criteria-based — örn. Lead_Status='New'). Bu production müşteri seviyesi
karar, tech backlog değil.

## Current Workaround

P4.1 **Manuel ID toggle** tam bu senaryo için tasarlandı. Q tenant 5050'de smoke'u bu yolla
bitirebilir:
1. Dashboard → Asama Eslesmeleri → her satırda "Manuel ID" toggle'a tıkla
2. Zoho Setup → Automation → Blueprint → Leads Blueprint → edit → transition → URL'den 19-digit id kopyala
3. UI'da input'a yapıştır → Kaydet
4. Manuel input dry-run bypass eder (state-with-no-lead whitelist); 19-digit format validation aktif

## References

- [Get Blueprint Details API | v8](https://www.zoho.com/crm/developer/docs/api/v8/blueprint-details.html)
- [Scopes | v8](https://www.zoho.com/crm/developer/docs/api/v8/scopes.html)
- [Update Blueprint Details API | v8](https://www.zoho.com/crm/developer/docs/api/v8/update-blueprint.html)
- [OAuth 2.0 Authentication | v8](https://www.zoho.com/crm/developer/docs/api/v8/oauth-overview.html)
- Production log evidence: `C:\Invekto\Integrations\logs\service-stdout.log` — `[ZOHO-META] settings/blueprint status=401` (OAUTH_SCOPE_MISMATCH) + `[ZOHO-BP-RAW] lead=... status=400 RECORD_NOT_IN_PROCESS`

## Pending Tests (Paid Plan Validation)

Q direktifi: Dev Edition'da gözlemlenen davranış paid plan'da farklı olabilir.

- [ ] **Adavista tenant'ıyla retest:** Dent Adavista paid Zoho plan. Zoho → Setup → Automation →
      Blueprint → Leads Blueprint → "Apply to existing records" toggle'ı VAR mı? Varsa enable et,
      tenant'daki lead'lerin `/actions/blueprint` 200 + transition array döndürüp döndürmediğini
      test et.
- [ ] **Metadata path paid plan'da:** Aynı tenant'ta `/settings/blueprint?module=Leads&layout_id=X`
      yine 401 OAUTH_SCOPE_MISMATCH mı döner? Eğer 200 dönerse "Dev Edition limit" hipotezi doğrulanır
      ve Fix 2 (metadata removal) gereksiz olabilir.
- [ ] **Criteria-based trigger:** Paid plan'da Lead_Status='New' gibi criteria ile trigger
      tanımlanabiliyor mu? Bu production müşteri onboarding akışı için kritik.

Adavista test sonuçları P4.2 kod impl kararını belirler.

## Current Production State (2026-04-16 deploy sonrası)

- **Commit 6085755** master'da, P4.1 deployed.
- **`[ZOHO-BP-RAW]` debug patch HALA PRODUCTION'DA** (uncommitted working tree değişikliği).
  Integrations `ZohoBlueprintClient.cs` line 148-170 arası: per-response stdout log, her Discover'da
  1-2 satır log üretiyor. P4.2 kod paketinde rollback edilecek. Production log buffer'ı dolmuyor
  çünkü çağrı frekansı düşük.
- Tenant 5050 smoke açık: Q Manuel ID ile tamamlayacak (P4.1 tasarımı bu senaryo için).

## Next Action

1. **Önce:** Q Adavista'da retest → paid plan davranışını doğrula
2. **Sonra:** Bulgulara göre Fix 1 + Fix 2A + Fix 3 (debug patch rollback) bir paket olarak
   impl + Codex review + deploy. ~1-2 saat. Ayrı session.
