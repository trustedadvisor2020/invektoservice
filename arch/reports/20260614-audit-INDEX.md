# Refactor Audit — INDEX (sabah giriş noktası) — 2026-06-14

> **TÜM .NET kod tabanı tarandı: 14 servis, ~115k satır, 493 dosya.** Read-only, sıfır commit/deploy.
> **Toplam: 176 doğrulanmış bulgu — 8 critical · 34 high · 70 medium · 64 low.**
> İki detay raporu:
> - [Çekirdek (Backend/Outbound/Automation/Shared)](20260614-core-services-audit.md) — 98 bulgu (4C/14H)
> - [Genişletilmiş (10 servis)](20260614-extended-services-audit.md) — 78 bulgu (4C/20H)

## ⚠️ Doğrulama statüsü (önce oku)
Bunlar **multi-agent LLM bulgusu + adversarial LLM doğrulaması** (kod-seviyesi, file:line kanıtlı). **Build/Codex ile DOĞRULANMADI.** Bu bir triage backlog'u, ground-truth değil. Her onaylanan madde `auto`+Codex döngüsünden geçecek — kalan false-positive'leri o yakalar. **Sıfır kod değişti.** Güçlü validasyon sinyali: audit, repo'nun kendi `hot-lessons` kuralı olan "COUNT(*) bigint → GetInt32 InvalidCastException"i bağımsız olarak 6 serviste yeniden buldu.

---

## 8 CRITICAL (sabah ilk iş)

| # | Servis | Dosya:satır | Sorun |
|---|--------|-------------|-------|
| C1 | Backend | `src/Invekto.Backend/Data/LeadRepository.cs:347-365` | `COUNT(*)` (bigint) `GetInt32` → InvalidCastException |
| C2 | Integrations | `src/Invekto.Integrations/Data/IntegrationsRepository.cs:562-580` | `COUNT(*)` `GetInt32` → InvalidCastException (review recovery stats kırık) |
| C3 | Appointments | `src/Invekto.Appointments/Data/AppointmentsRepository.cs:670-700` | `GetAvailableSlotsAsync` `COUNT(*)` `GetInt32` → crash |
| C4 | Knowledge | `src/Invekto.Knowledge/Data/TemplateRepository.cs:787-801` | `GetPublishedForComparisonAsync` `group_tag` kolonu eksik → IndexOutOfRangeException |
| C5 | VoiceAI | `src/Invekto.VoiceAI/Services/VoiceTranscriptionService.cs:42` | `TraceIdentifier` ham Windows path'inde (`:` her transcription'ı crashler) + filename path traversal |
| C6 | Shared | `src/Invekto.Shared/Constants/ErrorCodes.cs:67-71, 689-694` | INV-BE-090..094: beş kod **değeri** ikişer farklı sabite atanmış (kontrat collision) |
| C7 | Shared | `src/Invekto.Shared/Constants/ErrorCodes.cs:86, 701` | INV-BE-110 tek değer iki sabite (`LeadIntakeInternalAuthInvalid` + `FieldMappingDbUnavailable`) |
| C8 | Outbound | `src/Invekto.Outbound/Program.cs:301, 1378-1449` | internal opt-out/outbox-drain endpoint'leri JWT-blokeli — **⚠️ runtime'da service-JWT mint ediliyorsa FALSE-POZİTİF; Codex/build'de ilk teyit edilecek** |

---

## Birleşik Cross-Cutting Pattern'ler (iki audit de aynı yere işaret etti)

1. **GetInt32-on-bigint (COUNT/SUM)** — *en yüksek ROI, en yüksek güven.* 6+ serviste garantili runtime crash sınıfı. Fix: `::int` cast veya `GetInt64`. Behavior-preserving.
2. **Broad `catch(Exception)`** — yaygın (WhatsAppAnalytics/Program.cs 30+, Backend/Program.cs 18+), `NpgsqlException` yanlış INV koduna map'leniyor. Repo'nun #1 hard-fail kuralı.
3. **null-forgiving `!`** — `ExecuteScalarAsync` sonuçlarında; NULL'da NRE. Repo hard-fail.
4. **Fail-open security defaults** — auth, config key boşken sessizce kapanıyor (ChatAnalysis `InternalApiKey`, WhatsAppAnalytics `OpsKey`). Yanlış-konfigli prod = açık endpoint.
5. **Per-request secret `Headers.Add`/`DefaultRequestHeaders`** — non-ASCII secret'te FormatException + cross-tenant sızıntı riski. Fix: `TryAddWithoutValidation` + per-request `HttpRequestMessage`.
6. **Non-atomic multi-statement DB mutation** (TOCTOU) — fetch+update ayrı statement/connection'da (ör. Appointments slot booking race).

**Mimari sağlam:** Her iki sentez de net — sıfır microservice-isolation ihlali, sıfır cross-service project ref, sıfır cross-service DTO duplikasyonu. Bu **bakım borcu, mimari çöküş değil.** Program.cs rewrite YOK.

---

## Önerilen auto-batch sırası (sabah)

Sırala: güven × ROI × düşük-risk. Her batch = bir `auto` paketi (interview→plan→dev→build→/rev Codex→commit). **Hiçbiri deploy edilmez — `commit'te dur` kuralın.**

| Sıra | Paket | Kapsam | Risk | Neden önce |
|------|-------|--------|------|-----------|
| **1** | **GetInt32-on-bigint sweep** | C1+C2+C3 + Marketing/WhatsAppAnalytics/Outbound repo'larındaki tüm COUNT/SUM `GetInt32` siteleri → `::int`/`GetInt64` | MEDIUM | Mekanik, behavior-preserving, yüksek-güven, gerçek crash. Shared'e dokunmaz (servis-içi SQL). |
| **2** | **Error-code collision fix** | C6+C7 (Shared/ErrorCodes.cs) — çakışan sabitlere yeni benzersiz değer | MEDIUM-HIGH | Kontrat bug'ı. **Shared = full-solution build.** Wire kodu değişeceği için client bağımlılığı kontrolü (interview gate). |
| **3** | **Fail-open auth fix** | ChatAnalysis + WhatsAppAnalytics: config key boşsa fail-CLOSED | MEDIUM-HIGH | Güvenlik. Davranış değişir (interview: yanlış-konfigli ama çalışan instance kırılır mı?). |
| **4** | **VoiceAI path crash + traversal** | C5 (sanitize TraceIdentifier + filename) | MEDIUM | Tek servis, crash + güvenlik. |
| **5** | **Outbound internal-endpoint erişimi** | C8 — önce Codex/build ile FALSE-POZİTİF mi teyit; gerçekse internal auth path | MEDIUM | Teyit-önce. |
| **6** | **broad catch + null-forgiving sweep** | Servis servis, endpoint-grubu bazında (rewrite DEĞİL) | LOW-MEDIUM | Yüksek hacim, çoğu diagnostic/behavior-preserving. İncremental. |
| **7+** | Kalan high/medium (per-servis quick-win'ler) | İki raporun §4 Quick Wins + §7 listeleri | değişken | ROI'ye göre. |

**DOKUNMA (iki raporun §6'sı):** Logging reader, FlowValidator DFS, sanctioned background sweep'ler, scheduler-host ProjectReference, FlowEngineV2 — stabil, refactor risk-karşılığı-getirisiz.

---

## Notlar
- Severity'ler verifier tarafından düzeltildi (review'ın "high"i çoğu yerde "medium"a indi — abartı eleme). Tablolardaki severity = düzeltilmiş.
- 68 bulgu adversarial doğrulamada **refute edildi** (false-positive/sanctioned) — şeffaflık için her raporun §8'inde.
- Bir sonraki adım senin: hangi batch'leri onaylıyorsun? Onay sonrası her biri auto+Codex ile sırayla.

_Read-only multi-agent audit (core-services-audit + extended-services-audit), 2026-06-14. 289 ajan, ~10.6M token, sıfır kod değişikliği._
