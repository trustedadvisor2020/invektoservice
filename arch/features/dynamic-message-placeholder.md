# SPEC: Dynamic Message Placeholder (INMA `{{placeholder}}` Integration)

> **Spec ID:** FEAT-DMP | **Paket:** Tek chunk all-in | **Risk:** MEDIUM
> **Yazar:** Q | **Son Guncelleme:** 2026-04-20 | **Durum:** IMPLEMENTED (plan: [20260420-feat-dmp-inma-dynamic-message.json](../plans/20260420-feat-dmp-inma-dynamic-message.json))

## Interview Decisions (2026-04-20, 7/7 locked)

| # | Karar | Not |
|---|-------|-----|
| Q1 | **Tek chunk all-in** (FEAT-J2 paterni) | Shared + Bridge + UI + Outbound + Automation tek commit |
| Q2 | **Hybrid picker** — TFM varsa semantic, yoksa raw | `NullTenantFieldMappingResolver` DMP'de default; FEAT-TFM geldiğinde swap edilir |
| Q3 | **Sabit demo preview** | `renderDynamicPreview()` helper (PlaceholderPicker.tsx) — hardcoded defaults |
| Q4 | **TFM-inclusive validation** | `DynamicMessageValidator` → resolver → allowlist kontrol |
| Q5 | **Flag default TRUE** | `tenant_settings.enable_dynamic_message` — admin escape hatch |
| Q6 | **i18n passthrough** | INMA FieldName (TR) aynen gösterilir; INSE override katmanı YOK |
| Q7 | **Reactive rollback** | INMA 901 → `outbound_messages.status='failed'` + INV-OB-034 + UI etiket; pre-send recurring job YOK |

## Error Codes (INV-OB-033..037)

FEAT-J2 INV-OB-026..032'i kullandı; DMP 033'ten başlar:
- **INV-OB-033** DynamicFieldValidationFailed (pre-send miss + INMA 900/902)
- **INV-OB-034** DynamicFieldUnsupported (INMA 901)
- **INV-OB-035** DynamicCustomerNotFound (INMA 903)
- **INV-OB-036** DynamicFieldValueNull (INMA 905)
- **INV-OB-037** DynamicFieldsFetchFailed (`/api/dynamicfields` HTTP/timeout/JSON failure)

## Scope Adjustments (implement-time)

- **CampaignsPage.tsx out of scope** — sayfa boş placeholder, composer yok. Marketing v2 PKT-6C3 sonrasi follow-up.
- **Marketing CampaignRunner out of scope** — service'te mevcut değil (MarketingRepository + TourismResponseGenerator + Program.cs). DTO alanı Shared'da hazır, Marketing composer implement edildiğinde entegre olur.
- **UI integration noktaları:** FlowBuilder `NodePropertyPanel` (message_text node) + `TemplateCreatePage` (content_json JSON composer). Her ikisi de cursor-position insert ile çalışır.


## 1. Intent (Ne & Neden)

INMA 2026-04-20 teslimatiyla `chatoperation` endpoint'ine **DynamicMessage** ozelligi ve yeni **`/api/dynamicfields`** lookup endpoint'i eklendi (`wapcrm-marketing-api.md`). INSE bugun mesaj metnini tamamen kendi substitution pipeline'iyla render ediyor; INMA'nin dynamic message ozelligi kullanilmiyor. Bu feature:

- **INMA'nin tenant Customer veritabanindaki** `Name, Email, Note, PushName, DataList.Name, CF1..CF10` alanlarini INSE'den placeholder olarak cagirmayi saglar
- Template / flow / campaign UI'larinda **`{{name}}`, `{{cf1}}` gibi** placeholder'lari tenant-specific etiketlerle (ornegin CF1='Sehir') gorunur hale getirir
- INSE substitution'i bypass ederek INMA substitution'una devreder — INMA'nin sakladigi kisisellestirme veri kaynagi source-of-truth olarak kullanilir (CF1..10 multi-channel tutarlilik)
- FEAT-TFM (Tenant Field Mapping) ile dogrudan kesisir: INMA CF1..10 etiketleri INSE 10-field semantic overlay'in **kaynagi** olur

## 2. Acceptance Criteria

| # | Kriter | Dogrulama |
|---|--------|-----------|
| AC-1 | `IInmaDynamicFieldsClient` (Shared/Contracts/Inma/) — `Task<List<InmaDynamicField>> GetFieldsAsync(tenantId, ct)` | Unit test WireMock |
| AC-2 | `InmaDynamicFieldsCache` — tenant-level IMemoryCache 1h TTL + manual invalidate endpoint | Cache hit/miss test |
| AC-3 | `OutgoingCallback.Data.DynamicFields` (string[]? optional) — upstream send_message callbacks'in kullanildigi placeholder listesini tasir. **NOT: DTO alani FEAT-J2 AC8'de pre-provision edildi (2026-04-20 karari)**, FEAT-DMP scope'u sadece upstream set noktalarini aktive etmek + bridge'de `wapPayload['dynamicMessage']=true + dynamicMessageFields=[...]` extension eklemek | DTO test (FEAT-J2'de null-serialization, FEAT-DMP'de gercek data) |
| AC-4 | Backend `/api/v1/callback/wapcrm` bridge extension — callback.Data.DynamicFields non-empty ise `wapPayload['dynamicMessage']=true + wapPayload['dynamicMessageFields']=[...]` ekler; null/empty ise INSE mevcut substitution pipeline'i calisir (geriye tam uyumlu) | Bridge integration test |
| AC-5 | Template editor UI (`TemplateLibraryPage.tsx`) — `/api/dynamicfields` uzerinden tenant field listesi gelir, placeholder picker (dropdown) `{{cf1}}` formatinda insert, tenant-specific etiket ('Sehir') tooltip olarak gorunur | Dashboard screenshot |
| AC-6 | Flow builder `action_send_message` node editor — ayni placeholder picker + validation (listede olmayan placeholder -> editor uyari) | Flow editor test |
| AC-7 | Campaign/broadcast composer — placeholder picker + preview (gercek INMA customer verisi degil, sabit demo degerler) | UI test |
| AC-8 | Send-time validation — MessageText icindeki tum `{{x}}` placeholder'lari DynamicFields listesinde olmali (INMA 900/901/902 hatalarini onle). Eksik ise callback build reddedilir, INV-OB-026 loglanir | Unit test |
| AC-9 | Error mapping — INMA 900 (empty fields) / 901 (unsupported) / 902 (not found in text) / 903 (customer not found) / 905 (field value null) yanitlarinda outbound message status='failed' + spesifik error_code logged; UI dashboard'da opsiyonel 'Eksik kisiselestirme alani' etiketi | Integration test WireMock |
| AC-10 | FEAT-TFM integration hook — tenant field mapping editor (`/settings/field-mapping`) INMA `/api/dynamicfields` sonucunu 'source' dropdown'da 10 kalem olarak gosterir (hardcode degil, tenant-specific) | Cross-feature test |

## 3. Architectural Decisions

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| INMA substitution'i kullan, INSE kendi substitution'ini yapma | Multi-channel (WhatsApp + Instagram + SMS) tutarlilik, Customer DB INMA'da | CQ9: source-of-truth tek yerde |
| DynamicFields callback DTO'sunda (per-message) — tenant-level config degil | Her mesaj farkli alan setini kullanabilir, esnek | — |
| Tenant field cache 1h TTL | CustomFields tablosu nadir degisir, hot path (her broadcast) | CQ11: G3 cache pattern mirror |
| Validation send-time (callback build) — UI-time (editor) degil | UI state desync riski (tenant CF config degisir), son savunma backend | — |
| Fallback: DynamicFields null -> INSE kendi substitution yapar | Backward-compat, gradual rollout | CQ12: feature flag olmadan rollout |
| INSE template store'da placeholder raw saklanir (`{{name}}`) | Render zamani INMA'ya devredilir, template lokal olarak hazir | — |
| Reserved placeholder listesi (`name, email, note, pushname, datalistname, cf1..cf10`) — genisleme INMA'ya bagli | INMA contract sabit, tenant ekstra field ekleyemez | — |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| INMA dynamic fields API | `wapcrm-marketing-api.md` section 3 (`/api/dynamicfields`) |
| INMA chatoperation dynamic | `wapcrm-marketing-api.md` section 2 (`DynamicMessage`, `DynamicMessageFields`) |
| Shared Client | `Invekto.Shared/Contracts/Inma/IInmaDynamicFieldsClient.cs` (yeni) |
| Shared Cache | `Invekto.Shared/Services/InmaDynamicFieldsCache.cs` (yeni) |
| Callback DTO | `Invekto.Shared/DTOs/Integration/OutgoingCallback.cs` (`CallbackData.DynamicFields` ekle) |
| Bridge | `src/Invekto.Backend/Program.cs:7459-7600` (wapPayload genisletme) |
| Error Codes | INV-OB-026 (DynamicFieldValidationFailed), INV-OB-027 (DynamicCustomerNotFound — INMA 903), INV-OB-028 (DynamicFieldValueNull — INMA 905), INV-OB-029 (DynamicFieldUnsupported — INMA 901) |

## 5. Scope Boundaries

### In Scope
- `IInmaDynamicFieldsClient` + impl + tenant-level cache
- `CallbackData.DynamicFields` DTO alani
- Backend bridge extension (`dynamicMessage` + `dynamicMessageFields` payload)
- TemplateLibrary UI placeholder picker
- Flow builder `action_send_message` placeholder picker
- Campaign/broadcast composer placeholder picker
- Send-time validation (`{{x}}` set match)
- INMA 900-905 error code mapping + outbound status/log
- FEAT-TFM integration hook (mapping editor 'source' dropdown)

### Out of Scope (Explicit)
- Custom placeholder definitions (INMA contract sabit)
- Server-side preview with real customer data (sadece demo defaults)
- Multi-language placeholder (ornegin `{{name.tr}}`) — INMA contract desteklemez
- CF11+ destegi (INMA 10 ile sinirli)
- INMA customer data local mirror (sorgu her zaman INMA'ya gider via chatoperation)
- INMA 903 (customer not found) auto-create customer (out of scope)

### Degismeyen Alanlar (Pre-existing)
- INSE mevcut substitution pipeline (DynamicFields null ise aynen calisir)
- INMA API auth (X-CIB-SecretKey)
- Template store raw placeholder format (`{{name}}` degismez)

## 6. Service Boundaries

| Servis | Rol | Degisiklik |
|--------|-----|-----------|
| Backend | Bridge extension + TemplateLibrary UI + field mapping editor | wapPayload genislet, yeni endpoint `/api/v1/dynamic-fields` client proxy |
| Outbound | Broadcast composer -> DynamicFields callback set | MessageSenderService / BroadcastOrchestrator callback build |
| Automation | Flow send_message callback -> DynamicFields set | AutomationOrchestrator action_send_message executor |
| ChatAnalysis / WebChat | Degismiyor (user-initiated, placeholder genelde gereksiz) | - |
| Marketing | Marketing v2 campaign -> DynamicFields set | CampaignRunner callback build |

## 7. Data Model

Tablo degisikligi **yok** — INMA Customer DB source-of-truth, INSE mirror yapmiyor. Sadece DTO alan ekleme:

```csharp
// Invekto.Shared/DTOs/Integration/OutgoingCallback.cs
public sealed class CallbackData
{
    // ... mevcut alanlar ...

    /// <summary>INMA dynamic message fields (placeholder names, e.g. ["name", "cf1"])</summary>
    [JsonPropertyName("dynamic_fields")]
    public string[]? DynamicFields { get; init; }
}
```

Tenant cache TenantSettingsCache mevcut IMemoryCache'e piggyback (key: `inma:dynamicfields:{tenantId}`, 1h TTL).

## 8. Integration Points

```
Template Editor / Flow Builder / Campaign Composer
    ↓ user picks {{cf1}}
    ↓ validates against /api/v1/dynamic-fields (tenant-scoped)
    ↓ saves template with raw {{cf1}} text
    ↓
MessageSenderService / AutomationOrchestrator
    ↓ builds OutgoingCallback with DynamicFields=["cf1"]
    ↓
Backend /api/v1/callback/wapcrm (Bridge)
    ↓ if DynamicFields non-empty: wapPayload.DynamicMessage=true, DynamicMessageFields=[...]
    ↓ POST cxapi.wapcrm.net/api/chatoperation
    ↓
INMA chatoperation
    ↓ resolves {{cf1}} from Customer.CF1
    ↓ sends to WA / Instagram / etc
```

## 9. Risk & Mitigation

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|-----------|
| INMA 903 customer not found -> mesaj gitmez, kullanici farkinda degil | MEDIUM | MEDIUM | Send-time pre-check? (pahali). Fallback: outbound_messages.status='failed' + UI etiket 'Musteri INMA'da yok' |
| DynamicFields send-time validation kacarsa INMA 900/901/902 | LOW | LOW | Pre-send regex scan (callback build asamasinda) — geriye donuk test |
| INMA `/api/dynamicfields` response degisirse cache stale | LOW | LOW | Manual invalidate endpoint + 1h TTL |
| CF1..10 icerigi dusuk trust (tenant saf data giriyor) -> spam placeholder'lari | LOW | MEDIUM | INMA kendi content moderation yapar, INSE gecis kanali |
| Preview gercek veri yerine demo default -> UI'da yanlis context | LOW | LOW | Dashboard preview 'demo' rozetiyle clear ayirma |

## 10. Rollout Plan

1. **Faz 1** — `IInmaDynamicFieldsClient` + cache + `CallbackData.DynamicFields` DTO (Shared scope, breaking yok)
2. **Faz 2** — Bridge extension + integration tests + TemplateLibrary UI picker (non-destructive, DynamicFields null varsa eski davranis)
3. **Faz 3** — Flow builder + Campaign composer UI entegrasyonu
4. **Faz 4** — Validation + error mapping (INV-OB-026..029)
5. **Faz 5** — FEAT-TFM mapping editor hook (source dropdown INMA field listesi)
6. **Faz 6** — Dent pilotu: 1-2 template DynamicMessage'a cevrilir, CF1='City' kullanilir, A/B test INMA substitution vs INSE substitution

## 11. Bagimli / Ilgili Paketler

- **FEAT-J2** (Opt-Out + MessageCategory) — **PREREQUISITE**: FEAT-J2 tek chunk olarak prod'a cikmadan ve 48sa stabil olmadan FEAT-DMP baslamaz (CallbackData DTO ortak surface, catisma riski). J2'de CallbackData.DynamicFields alan zaten eklenmis olacak — DMP sadece set + bridge + UI entegrasyonu yapar.
- **FEAT-TFM** (Tenant Field Mapping) — AC-10 cross-hook; TFM semantic name -> INMA CF1..10 mapping icin 'source' dropdown
- **Marketing v2** (PKT-6C3) — Campaign composer placeholder picker
- **PKT-5B** (Platform UI+Adv) — TemplateLibrary editor extension
- **WEBCHAT** — Out of scope (user-initiated chat placeholder gereksiz)

## 12. Dosya Listesi (Tahmini)

```
src/Invekto.Shared/Contracts/Inma/IInmaDynamicFieldsClient.cs             [YENI]
src/Invekto.Shared/Contracts/Inma/HttpInmaDynamicFieldsClient.cs          [YENI]
src/Invekto.Shared/Contracts/Inma/Dtos/InmaDynamicField.cs                [YENI]
src/Invekto.Shared/Services/InmaDynamicFieldsCache.cs                     [YENI]
src/Invekto.Shared/DTOs/Integration/OutgoingCallback.cs                   [EDIT]
src/Invekto.Shared/Constants/ErrorCodes.cs                                [EDIT]
src/Invekto.Backend/Program.cs                                            [EDIT — bridge + /api/v1/dynamic-fields]
src/Invekto.Backend/Dashboard/src/pages/TemplateLibraryPage.tsx           [EDIT — picker]
src/Invekto.Backend/Dashboard/src/components/PlaceholderPicker.tsx        [YENI]
src/Invekto.Backend/Dashboard/src/pages/FlowBuilderPage.tsx               [EDIT — action_send_message picker]
src/Invekto.Outbound/Services/MessageSenderService.cs                     [EDIT — DynamicFields set]
src/Invekto.Outbound/Services/BroadcastOrchestrator.cs                    [EDIT — DynamicFields set]
src/Invekto.Automation/Services/AutomationOrchestrator.cs                 [EDIT — DynamicFields set]
src/Invekto.Marketing/Services/CampaignRunner.cs                          [EDIT — DynamicFields set]
arch/errors.md                                                            [EDIT]
arch/features/tenant-field-mapping.md                                     [EDIT — AC-X cross-link FEAT-DMP]
tests/InvektoServis.Tests/Integration/DynamicFieldsCacheTests.cs          [YENI]
tests/InvektoServis.Tests/Integration/WapCrmBridgeDynamicTests.cs         [YENI]
tests/InvektoServis.Tests/E2E/DynamicMessageE2ETests.cs                   [YENI]
```

## 13. Acik Sorular (Pre-Plan)

- Q1: `/api/dynamicfields` response'undaki `FieldName` (UI etiketi) tenant dili mi yoksa genel Turkce mi? i18n stratejisi var mi? (INMA'ya soru)
- Q2: FEAT-TFM ile cakisma: tenant FEAT-TFM editor'unde `roadshow_city` semantic -> `cf1` source mapping yapmis olsun. TemplateLibrary placeholder picker `{{cf1}}` mi, `{{roadshow_city}}` mi gostermeli? Semantic name INSE-side substitution gerektirir (INMA `{{cf1}}` bilir) — karar: **semantic picker goster**, callback build asamasinda `cf1`'e resolve et + DynamicFields=['cf1'] gonder.
- Q3: Preview UX: kullanici 'Merhaba {{name}}, {{cf1}} listesinden...' yazdiginda UI'da demo data nasil gosterilir? Sabit 'Ornek Musteri, Istanbul' mu yoksa son 1 lead'in verisi (INMA API ile pull) mi?
- Q4: Validation send-time — `{{roadshow_city}}` gibi TFM semantic placeholder'lari da INMA field ismine resolve edilip validation yapilir mi? (Evet, kurallari AC-8'e ekle.)
- Q5: Rollback: eger bir template hem DynamicMessage:true ile kaydedildi hem de INMA kontratinda bir field kaldirildi — backward compat?

## 14. Basari Metrikleri

- Adavista pilotunda 1 ay icinde en az 3 template DynamicMessage'a cevrilmis
- Send-time validation kacak orani <%0.5 (INV-OB-026 / total broadcast)
- Template editor placeholder picker kullanim orani (editor acan kullanicilar icinde) >50%
- INMA 901/902/905 hatalarinin outbound total icindeki orani <%1
