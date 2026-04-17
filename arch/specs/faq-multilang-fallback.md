# SPEC: FAQ Multi-Language Fallback (HFM-2)

> **Spec ID:** HFM-2 | **Paket:** 20260417-human-feel-multilang-pilot | **Risk:** MEDIUM
> **Yazar:** Q | **Son Güncelleme:** 2026-04-17 | **Durum:** IMPLEMENTED

## 1. Intent (Ne & Neden)

Mevcut `TranslationService` prod-ready: 18 dil, Gemma+Claude dual, DB cache, batch lookup. **Runtime FAQ pipeline'da çağrılmıyor.** AiFaqHandler Knowledge'daki tenant-authored answer'ı ham yolluyor. Lead `preferred_locale` kolonu yok. AiIntentHandler 15+ hardcoded TR cümlesi pilot EN-odaklı için blocker.

Adavista pilot Adavista Dental Dublin/Cork benzeri EN + DE/FR/AR/RU leadleri alacak. Dil-aware yanıt olmazsa "speak my language" hissi gider.

Research arka planı: `arch/platform/inma-inse-unification/human-feel-multilang-research.md` §3.3 — Claude TR medical > Grok, dental cevapları LLM'e bırakma (tenant-authored + translate), silent switch > explicit notice (UserExperience.org 2025).

## 2. Acceptance Criteria

| # | Kriter | Doğrulama |
|---|--------|-----------|
| AC-1 | Migration 018: `leads.preferred_locale VARCHAR(5)` + partial index + CHECK regex | `arch/db/migrations/018-leads-preferred-locale.sql` |
| AC-2 | İlk mesajda `LanguageDetector.Detect` + `UpsertLeadPreferredLocaleAsync` sticky | `ResolveLeadPreferredLocaleAsync` orchestrator |
| AC-3 | `ON CONFLICT COALESCE` existing locale PRESERVED | Repository upsert pattern (sticky) |
| AC-4 | AiFaqHandler matched answer → TranslationHopClient (target != answer.lang) | `MaybeTranslateAsync` post-match |
| AC-5 | Graceful degrade: translation fail → original answer | `TranslationHopClient` null return → caller keeps raw |
| AC-6 | AiIntentHandler 8 core prompt i18n (10 dil embedded resource) | `IntentPromptLoader.Get(locale, key, subs)` |
| AC-7 | Fallback chain: lead.preferred_locale → 'en' default → raw | AiFaqHandler `ResolveTargetLocale` + null path |
| AC-8 | Silent switch: user'a "I'll respond in English" YOK | AC-2 sticky persist, AC-4 silent translate |
| AC-9 | Ops warmup endpoint: POST /ops/translation/warmup (X-Ops-Key auth) | Backend Program.cs |
| AC-10 | AiFaqHandler simulation mode → translate skip (no HTTP) | `ctx.IsSimulation` dalı |

## 3. Architectural Decisions

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| Post-match translate (tenant text değişmez) | Hallucination riski (klinik guideline mismatch) — LLM cevabını yazmasın, sadece çevirsin | EXPECTED: CQ1 tenant content integrity |
| Sticky preferred_locale via COALESCE | İlk detect > sonraki revise. Kullanıcı bir gün EN bir gün TR yazarsa race olmaz | EXPECTED: plan Q3 race determinizmi |
| Embedded resource (not DB) | Runtime zero-latency, tenant-global | EXPECTED: CQ3 scope — tenant-specific override HFM-2 dışı |
| Ops warmup by design cross-tenant | X-Ops-Key admin endpoint, audit log mevcut ValidateOpsAuth | EXPECTED: CQ5 "missing tenant_id filter" → plan Q5 intentional |
| `en` default fallback (no tenant.locale_default) | Pilot scope minimal; tenant-level default ileride eklenebilir | Plan scope |
| AiIntent 8 prompt i18n (full handler i18n değil) | Clarify/confirm/greeting günlük path; Claude system prompt TR kalır | Out of scope → plan non_goals |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| DB Schema | `arch/db/migrations/018-leads-preferred-locale.sql` |
| Translation DTO | `Invekto.Shared/DTOs/Translation/TranslationDtos.cs` (mevcut, değişmedi) |
| Error Codes | `arch/errors.md` INV-AT-063..065 |

## 5. Scope Boundaries

### In Scope
- `leads.preferred_locale` column + upsert path
- AiFaqHandler post-match translate hook (graceful degrade)
- AiIntentHandler 8 core prompt resource lookup (10 locale)
- Ops warmup endpoint (pilot pre-populate)
- 4 yeni error code

### Out of Scope (Explicit)
- `tenant_registry.locale_default` kolonu (HFM-2'de YOK)
- AiIntent Claude system prompt i18n (tenant-level opsiyonel)
- Custom tenant prompt override (tenant field mapping pattern ile ileride)
- Bulk historical translate backfill (leads NULL → lazy populate)
- Emoji policy (HFM-3)
- Tone matrix (HFM-3)

### Değişmeyen Alanlar
- TranslationService (mevcut 18 dil, Gemma primary + Claude fallback)
- LanguageDetector (Shared, 13 dil script-based)
- Knowledge service (FAQ CRUD + pgvector search)
- IntentDetector (Claude prompt TR kalır)
- MockFaqMatcher / MockIntentDetector (simulation)

## 6. Service Boundaries

| Servis | Rol | Değişiklik |
|--------|-----|-----------|
| Backend | TranslationService + warmup endpoint | Additive endpoint |
| Automation | AiFaqHandler + AiIntentHandler + Orchestrator + Repository | Modified |
| Shared | LanguageDetector | No change |

## 7. Risk & Mitigation

| Risk | Olasılık | Mitigation |
|------|----------|------------|
| Translation latency hot-path (50-150ms/miss) | MEDIUM | Warmup endpoint pre-pilot populate; 80%+ cache hit target |
| preferred_locale race (paralel upsert) | LOW | COALESCE last-write-wins doc (plan Q3); sticky persist sonrakileri korur |
| Lang detect short message yanlış | MEDIUM | `LanguageDetector` 2 char threshold; AI detect Backend'de opsiyonel |
| Ops endpoint cross-tenant misuse | LOW | X-Ops-Key + SystemInfo audit log + ValidateOpsAuth basic/bearer |
| Gemma rate limit on warmup | LOW | Batch endpoint 50 msg/call throttle; sequential per-locale |
