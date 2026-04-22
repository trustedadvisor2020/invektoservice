# InvektoServis Error Codes

<!-- FORMAT: agent-first (v6.0). YAML block is the source of truth. -->

> **KURAL:** Tüm hata mesajları bu dosyadaki kodları kullanmalı.
> **KOD DOSYASI:** `src/Invekto.Shared/Constants/ErrorCodes.cs`
> **FORMAT:** `INV-{SERVICE}-{NUMBER}` — SERVICE kodları aşağıda, NUMBER 3 haneli.

## Service Codes

```yaml
services:
  GEN:  { name: General,       description: Genel hatalar }
  BE:   { name: Backend,       description: Backend API hataları }
  CA:   { name: ChatAnalysis,  description: Chat Analysis microservice hataları }
  AT:   { name: Automation,    description: "GR-1.1: Chatbot/Flow Builder hataları" }
  AA:   { name: AgentAI,       description: AgentAI hataları }
  AUTH: { name: Auth,          description: Authentication hataları }
  DB:   { name: Database,      description: Veritabanı hataları }
  VAL:  { name: Validation,    description: Validasyon hataları }
  INT:  { name: Integration,   description: "GR-1.9: Entegrasyon köprüsü hataları" }
  OB:   { name: Outbound,      description: "GR-1.3/3.15/3.26/3.29: Broadcast, campaign, consent hataları" }
  IG:   { name: Integrations,  description: "GR-3.4/3.6: Marketplace & kargo entegrasyonları" }
  AP:   { name: Appointments,  description: "GR-2.4: Randevu Motoru hataları" }
  KN:   { name: Knowledge,     description: "GR-2.1: Knowledge Service (RAG) hataları" }
  AD:   { name: Attribution,   description: "GR-3.14: Ads Attribution hataları" }
  MK:   { name: Marketing,     description: "GR-3.21/3.22: Google Yorum, Referans, Medikal Turizm hataları" }
  MT:   { name: Metrics,       description: "PKT-3: Analitik/metrik hataları" }
  WC:   { name: WebChat,       description: Website webchat hataları }
  EXT:  { name: External,      description: Dış servis hataları }
  WA:   { name: WhatsAppAnalytics, description: "WA Analytics pipeline + Revenue Intelligence hataları" }
  VA:   { name: VoiceAI,          description: "PKT-11: Voice Message AI — STT + intent hataları" }
  JOB:  { name: HangfireJob,      description: "G7: Hangfire recurring/enqueued job altyapı hataları" }
  SEED: { name: DeploymentSeed,   description: "Tenant seed SQL postcondition assertions (one-shot deploy; PL/pgSQL RAISE EXCEPTION — no ErrorCodes.cs mirror)" }
```

## Error Registry

```yaml
errors:
  # ── GEN — General ──
  - code: INV-GEN-001
    description: Unknown error
    user_message: Beklenmeyen bir hata oluştu.
  - code: INV-GEN-002
    description: Timeout
    user_message: İşlem zaman aşımına uğradı.
  - code: INV-GEN-003
    description: Validation error
    user_message: Geçersiz veri formatı.

  # ── BE — Backend ──
  - code: INV-BE-001
    description: Microservice unavailable
    user_message: Servis geçici olarak kullanılamıyor.
  - code: INV-BE-002
    description: Microservice timeout
    user_message: Servis yanıt vermedi. Lütfen tekrar deneyin.
  - code: INV-BE-003
    description: Microservice error (5xx)
    user_message: Servis hatası. Lütfen tekrar deneyin.
  - code: INV-BE-004
    description: Microservice invalid response
    user_message: Servis geçersiz yanıt döndü.
  - code: INV-BE-005
    description: Microservice client error (4xx)
    user_message: İstek hatası. Lütfen parametreleri kontrol edin.
  - code: INV-BE-010
    description: Message log query failed
    user_message: Mesaj kayıtları yüklenemedi.
  - code: INV-BE-011
    description: Tenant list query failed
    user_message: Firma listesi yüklenemedi.
  - code: INV-BE-012
    description: Tenant impersonate failed
    user_message: Firma girişi başarısız oldu.
  - code: INV-BE-020
    description: Wizard session creation failed
    user_message: Wizard oturumu oluşturulamadı.
  - code: INV-BE-021
    description: Wizard AI service unavailable
    user_message: AI servisi yapılandırılmamış.
  - code: INV-BE-022
    description: Wizard AI communication failed
    user_message: AI iletişim hatası.
  - code: INV-BE-023
    description: Wizard confirm failed
    user_message: Akış oluşturulamadı.
  - code: INV-BE-024
    description: Wizard invalid payload
    user_message: Geçersiz wizard isteği.
  - code: INV-BE-030
    description: WapCRM instance fetch failed
    user_message: WapCRM hat listesi alınamadı.
  - code: INV-BE-031
    description: Instance disable blocked (in use by flow)
    user_message: Hat bir akış tarafından kullanılıyor, devre dışı bırakılamaz.
  - code: INV-BE-040
    description: Working hours fetch failed
    user_message: Çalışma saatleri yüklenemedi.
  - code: INV-BE-041
    description: Working hours update failed
    user_message: Çalışma saatleri güncellenemedi.
  - code: INV-BE-050
    description: Onboarding status aggregation failed
    user_message: Onboarding durumu hesaplanamadı.
  - code: INV-BE-060
    description: Sector update failed
    user_message: Sektör güncellenemedi.
  - code: INV-BE-061
    description: Invalid sector value
    user_message: Geçersiz sektör değeri.
  - code: INV-BE-070
    description: RI Knowledge sync failed
    user_message: RI verisi Knowledge servisine aktarılamadı.
  - code: INV-BE-071
    description: RI rescue trigger to Outbound failed
    user_message: Kurtarma tetiklemesi başarısız oldu.
  - code: INV-BE-072
    description: RI Marketing sync failed
    user_message: RI verisi Marketing servisine aktarılamadı.
  - code: INV-BE-080
    description: Plan tier not found
    user_message: Plan tanımı bulunamadı.
  - code: INV-BE-081
    description: Tier name already exists (409 conflict)
    user_message: Bu plan adı zaten kullanılıyor.
  - code: INV-BE-082
    description: Cannot delete plan — active tenants on tier
    user_message: Bu plana bağlı aktif firmalar var, silinemez.
  - code: INV-BE-083
    description: features_json invalid format
    user_message: Özellik tanımı geçersiz formatta.
  - code: INV-BE-084
    description: Tenant plan update failed
    user_message: Firma plan güncellemesi başarısız oldu.
  # Backend Payment (INV-BE-085+) — QNB VPos
  - code: INV-BE-085
    description: Payment initiation failed
    user_message: Ödeme başlatılamadı.
  - code: INV-BE-086
    description: Payment callback invalid
    user_message: Ödeme geri dönüşü işlenemedi.
  - code: INV-BE-087
    description: Payment record not found
    user_message: Ödeme kaydı bulunamadı.
  - code: INV-BE-088
    description: Payment history query failed
    user_message: Ödeme geçmişi yüklenemedi.
  - code: INV-BE-089
    description: Payment amount invalid
    user_message: Geçersiz ödeme tutarı (sıfır veya negatif).

  # Backend Translation (INV-BE-090+) — Chat Translation API
  - code: INV-BE-090
    description: Translation API call failed
    user_message: Çeviri işlemi başarısız oldu.
  - code: INV-BE-091
    description: Language detection failed
    user_message: Dil algılama başarısız oldu.
  - code: INV-BE-092
    description: Translation cache error
    user_message: Çeviri önbellek hatası.
  - code: INV-BE-093
    description: Unsupported target language
    user_message: Desteklenmeyen hedef dil.
  - code: INV-BE-094
    description: Batch size exceeds limit (max 50)
    user_message: Toplu çeviri limiti aşıldı (maks. 50 mesaj).
  - code: INV-BE-095
    description: Empty or invalid source text
    user_message: Çevrilecek metin boş veya geçersiz.

  # FEAT-TFM MVP: Tenant Field Mapping (INV-BE-096..099)
  - code: INV-BE-096
    description: Field mapping invalid (type/regex/source format/duplicate slot)
    user_message: Geçersiz alan tanımı.
  - code: INV-BE-097
    description: Reserved semantic name (conflicts with InmaDynamicFieldKeys.Allowlist or leads core columns)
    user_message: Bu isim sistem alanı, kullanılamaz.
  - code: INV-BE-098
    description: Type=enum but enum_values null/empty
    user_message: Enum tipi için en az bir değer gerekli.
  - code: INV-BE-099
    description: Source not in cf1..cf10 range
    user_message: INMA kaynağı cf1..cf10 olmalı.
  - code: INV-BE-110
    description: tenant_settings.field_mapping DB read/write fail (TFM-specific, distinguish from generic INV-BE-001)
    user_message: Alan tanımları geçici olarak okunamıyor/kaydedilemiyor; birkaç saniye sonra tekrar deneyin.

  # FEAT-TFM MVP: explicit forbidden-cross-tenant write code (INV-AUTH-010)
  - code: INV-AUTH-010
    description: Cross-tenant write blocked (body-supplied tenant_id mismatches JWT claim)
    user_message: Bu işlem için yetkiniz yok (cross-tenant yazma engellendi).

  # ── CA — ChatAnalysis ──
  - code: INV-CA-001
    description: Invalid payload
    user_message: Geçersiz istek formatı.
  - code: INV-CA-002
    description: Processing failed
    user_message: Analiz işlemi başarısız oldu.
  - code: INV-CA-003
    description: WapCRM API error
    user_message: CRM servisine bağlanılamadı.
  - code: INV-CA-004
    description: WapCRM timeout
    user_message: CRM servisi yanıt vermedi.
  - code: INV-CA-005
    description: Claude API error
    user_message: Analiz servisi hatası.
  - code: INV-CA-006
    description: Claude timeout
    user_message: Analiz servisi yanıt vermedi.
  - code: INV-CA-007
    description: No messages found
    user_message: Bu numara için mesaj bulunamadı.

  # ── AUTH — Authentication ──
  - code: INV-AUTH-001
    description: Token expired
    user_message: Oturumunuz sona erdi. Lütfen tekrar giriş yapın.
  - code: INV-AUTH-002
    description: Invalid token
    user_message: Geçersiz oturum.
  - code: INV-AUTH-003
    description: Unauthorized
    user_message: Bu işlem için yetkiniz bulunmuyor.
  - code: INV-AUTH-004
    description: Missing or invalid tenant claim
    user_message: INMA token'ında CompanyCode/CompanyId claim'i eksik veya geçersiz.
  - code: INV-AUTH-005
    description: Feature not included in tenant's plan (FeatureGuardMiddleware)
    user_message: Bu özellik mevcut planınızda bulunmuyor.
  - code: INV-AUTH-006
    description: Feature requires higher subscription tier (Paket 2)
    user_message: Bu özellik daha yüksek bir plan gerektiriyor.
  - code: INV-AUTH-007
    description: Monthly usage quota exceeded (Paket 2)
    user_message: Aylık kullanım limitinize ulaştınız.
  - code: INV-AUTH-008
    description: INMA welcome endpoint introspection unavailable (network/transport fail; Backend cannot reach INMA API to validate token)
    user_message: INMA servisine ulaşılamadı, kısa süre sonra tekrar deneyin.
  - code: INV-AUTH-009
    description: Lazy auto-provision tenant resolve failed (PostgreSQL NpgsqlException during CompanyCode→tenant_id lookup/insert). Introduced 2026-04-17 (migration 016) to map opaque INMA CompanyCode string to INSE integer tenant_id via tenant_registry.inma_code.
    user_message: Tenant provision hatasi, kisa sure sonra tekrar deneyin.

  # ── AT — Automation (GR-1.1) ──
  - code: INV-AT-001
    description: Invalid flow config
    user_message: Chatbot akış konfigürasyonu geçersiz.
  - code: INV-AT-002
    description: Flow not found
    user_message: Bu tenant için chatbot akışı tanımlanmamış.
  - code: INV-AT-003
    description: FAQ not found
    user_message: SSS kaydı bulunamadı.
  - code: INV-AT-004
    description: Intent detection failed
    user_message: Niyet algılama servisi hatası.
  - code: INV-AT-005
    description: Session expired
    user_message: Sohbet oturumu sona erdi.
  - code: INV-AT-006
    description: Flow validation failed
    user_message: Chatbot akış doğrulaması başarısız.
  - code: INV-AT-007
    description: Flow not found by ID
    user_message: Belirtilen chatbot akışı bulunamadı.
  - code: INV-AT-008
    description: Flow activation conflict
    user_message: Bu tenant için zaten aktif bir akış var.
  - code: INV-AT-009
    description: Invalid flow config version
    user_message: Desteklenmeyen akış konfigürasyonu versiyonu.
  - code: INV-AT-010
    description: Invalid API key
    user_message: Geçersiz API anahtarı.
  - code: INV-AT-011
    description: Max loop count exceeded
    user_message: "Sonsuz döngü limiti aşıldı, node: {node_id}"
  - code: INV-AT-012
    description: Unknown node type
    user_message: "Desteklenmeyen node tipi: {type}"
  - code: INV-AT-013
    description: No pending input expected
    user_message: Beklenmeyen kullanıcı girdisi
  - code: INV-AT-014
    description: Unknown input type
    user_message: "Bilinmeyen girdi tipi: {type}"
  - code: INV-AT-015
    description: Graph validation failed
    user_message: "Akış doğrulaması başarısız: {reason}"
  - code: INV-AT-016
    description: Required field missing
    user_message: "Zorunlu alan eksik, node '{node_id}': {field}"
  - code: INV-AT-017
    description: Expression evaluation failed
    user_message: "İfade değerlendirme hatası, node '{node_id}': {reason}"
  - code: INV-AT-018
    description: Simulation session not found
    user_message: Simülasyon oturumu bulunamadı.
  - code: INV-AT-019
    description: Simulation session expired
    user_message: Simülasyon oturumunun süresi doldu.
  - code: INV-AT-020
    description: Flow not found for simulation
    user_message: Simülasyon için akış bulunamadı.
  - code: INV-AT-021
    description: Node execution failed
    user_message: "Node çalışma hatası ({node_id}): {reason}"
  - code: INV-AT-022
    description: API call SSRF blocked
    user_message: API adresi güvenlik kontrolünden geçemedi (dahili adresler engellenmiştir).
  - code: INV-AT-023
    description: API call timeout
    user_message: "API çağrısı zaman aşımına uğradı ({timeout_ms}ms)."
  - code: INV-AT-024
    description: API call HTTP error
    user_message: "API çağrısı HTTP hatası ({status_code}): {reason}"
  - code: INV-AT-025
    description: Knowledge intent fetch failed
    user_message: Intent bilgileri alınamadı, varsayılan intent seti kullanılıyor.
  - code: INV-AT-026
    description: VIP detection failed
    user_message: VIP/B2B tespit işlemi başarısız (akış etkilenmez).
  - code: INV-AT-027
    description: Return deflection failed
    user_message: İade deflection işlemi başarısız.
  - code: INV-AT-028
    description: Return reason classify failed
    user_message: İade nedeni sınıflandırma başarısız.
  - code: INV-AT-029
    description: Coupon assign failed
    user_message: Kupon atama başarısız.
  - code: INV-AT-030
    description: Webhook flow not found
    user_message: Webhook için akış bulunamadı.
  - code: INV-AT-031
    description: Webhook flow not webhook_trigger type
    user_message: Bu akış webhook ile tetiklenemez.
  - code: INV-AT-032
    description: Webhook execution failed
    user_message: Webhook akış yürütmesi başarısız.
  - code: INV-AT-033
    description: Cron expression invalid
    user_message: Geçersiz cron ifadesi.
  - code: INV-AT-034
    description: Schedule execution failed
    user_message: Zamanlama akış yürütmesi başarısız.
  - code: INV-AT-035
    description: Instance not found in tenant cache
    user_message: Hat tenant cache'inde bulunamadı.
  - code: INV-AT-036
    description: Instance disabled, message ignored
    user_message: Hat devre dışı, mesaj yoksayıldı.
  - code: INV-AT-037
    description: Instance unassigned, message ignored
    user_message: Hat bir akışa atanmamış, mesaj yoksayıldı.
  - code: INV-AT-038
    description: Onboarding stats retrieval failed
    user_message: Automation onboarding istatistikleri alınamadı.
  - code: INV-AT-039
    description: Execution log insert failed
    user_message: Akış yürütme logu oluşturulamadı.
  - code: INV-AT-040
    description: Execution log update failed
    user_message: Akış yürütme logu güncellenemedi.
  - code: INV-AT-041
    description: Execution log query failed
    user_message: Akış yürütme logları alınamadı.
  - code: INV-AT-042
    description: Knowledge search failed
    user_message: ai_faq node Knowledge servisi arama hatası (timeout, HTTP, parse). no_match'e yönlendirilir.
  - code: INV-AT-043
    description: Chunk summarization failed
    user_message: PDF chunk özetleme Claude API hatası. Chunk sonucu atlanır, no_match'e yönlendirilir.
  - code: INV-AT-044
    description: E-commerce action call failed
    user_message: E-ticaret işlemi başarısız.
  - code: INV-AT-045
    description: E-commerce action timeout
    user_message: E-ticaret işlemi zaman aşımına uğradı.
  - code: INV-AT-046
    description: Flow version not found
    user_message: Belirtilen surum bulunamadi.
  - code: INV-AT-047
    description: Flow version creation failed
    user_message: Surum olusturma basarisiz.
  - code: INV-AT-048
    description: Flow rollback failed
    user_message: Surum geri alma basarisiz.
  - code: INV-AT-049
    description: Flow monitor query failed
    user_message: Monitor verileri alinamadi.
  - code: INV-AT-050
    description: Monitor AI stream error
    user_message: AI servisi yanit veremedi.
  - code: INV-AT-051
    description: Monitor AI connection lost
    user_message: AI baglantisi kesildi.
  - code: INV-AT-052
    description: Monitor AI save failed
    user_message: AI degisiklik kaydetme basarisiz.

  # PKT-12: Review Rescue AI (INV-AT-053+)
  - code: INV-AT-053
    description: Rescue dispatch failed (template fetch or Outbound send)
    user_message: Kurtarma mesaji gonderilemedi.
  - code: INV-AT-054
    description: Rescue message delivery failed
    user_message: Kurtarma mesaji iletilemedi.
  - code: INV-AT-055
    description: Follow-up due query failed
    user_message: Takip listesi alinamadi.
  - code: INV-AT-056
    description: Follow-up message send failed
    user_message: Takip mesaji gonderilemedi.

  # G3: Template A/B Rotation
  - code: INV-AT-057
    description: message_text node has invalid text_variants JSON; falling back to single text field
    user_message: Sablon yuklenemedi, varsayilan mesaj kullanildi.

  # G6: Flow Wait Persistence
  - code: INV-AT-058
    description: flow_execution_state row insert/update failed (DB error). Wait state could not be persisted.
    user_message: Bekleme durumu kaydedilemedi.
  - code: INV-AT-059
    description: Resume from persisted wait state failed (deserialize / engine / callback error).
    user_message: Bekleyen akis devam ettirilemedi.
  - code: INV-AT-060
    description: action_wait_until node config invalid (no duration provided, out-of-bounds, or unparseable).
    user_message: Bekleme adimi yapilandirmasi gecersiz.

  # FEAT-WTP: Welcome Template Pack (rotation state + group_tag fetch)
  - code: INV-AT-061
    description: leads.faq_rotation_state upsert failed (NpgsqlException or update rejected); variant 0 fallback used.
    user_message: Varyant rotasyonu kaydedilemedi, varsayilan sablon kullanildi.

  # HFM-1/HFM-2: Human-feel + Multi-language
  - code: INV-AT-062
    description: message_text node has invalid text_chunks JSON; falling back to legacy text field (or \n\n auto-split).
    user_message: Mesaj parcalari yuklenemedi, tam mesaj gonderildi.
  - code: INV-AT-063
    description: preferred_locale upsert or language detection failed; fallback chain used ('en' or raw answer).
    user_message: Dil tercihi kaydedilemedi, varsayilan dil kullanildi.
  - code: INV-AT-064
    description: AiFaq post-match translation hop failed (Backend unreachable or timeout); original-language answer sent.
    user_message: Ceviri servisi kullanilamiyor, cevap kaynak dilinde gonderildi.
  - code: INV-AT-065
    description: IntentPrompts.{locale}.json embedded resource missing or malformed; fallback to Turkish default prompts.
    user_message: Dil kaynaklari bulunamadi, varsayilan dil kullanildi.

  # FEAT-WTP continued: template variant pool fetch + rotation state shape
  - code: INV-AT-066
    description: Knowledge template_catalog group_tag fetch failed (HTTP error / timeout / parse); fallback to inline text_variants or data.text.
    user_message: Sablon grubu yuklenemedi, varsayilan sablon kullanildi.
  - code: INV-AT-067
    description: leads.faq_rotation_state JSONB shape invalid (non-object or non-integer value); state reset to empty map, variant 0 used.
    user_message: Varyant rotasyon durumu bozuk; sifirlandi.

  # FEAT-LIW: Welcome flow enqueue from Backend intake endpoint
  - code: INV-AT-068
    description: TriggerWelcomeFlowJob welcome flow slug missing or empty (defensive guard; Backend always emits non-empty slug, so this fires only on contract violation). Lead still created, flow dispatch skipped.
    user_message: Hosgeldin flow bulunamadi.

  # FEAT-LIW Chunk B: Welcome flow real dispatch + wa-direct intake hook
  - code: INV-AT-069
    description: TriggerWelcomeFlowJob resolved a slug but no active matching row exists in chatbot_flows for the tenant (tenant renamed/disabled the welcome flow), OR the matched flow_config has no recognized welcome-trigger entry node (must be one of trigger_start / webhook_trigger / outbound_trigger; schedule_trigger is cron-only and rejected). Strictly a config gap, not an infra failure. Lead still created, flow dispatch skipped.
    user_message: Hosgeldin akisi tanimlanmamis.
  - code: INV-AT-070
    description: AutomationOrchestrator wa-direct hook could not reach Backend /api/internal/leads/intake/wa-direct after one retry (transient HTTP error, timeout, or non-2xx response). Chat reply path proceeds with leadId=null; lead row will be retried on the next inbound message from this contact.
    user_message: Lead kaydi gecici olarak yapilamiyor; sohbet devam ediyor.
  - code: INV-AT-071
    description: TriggerWelcomeFlowJob hit an execution-time infra failure (NpgsqlException during slug lookup, FlowGraphV2.Build returning null due to malformed flow_config, OperationCanceledException mid-execution, or InvalidOperationException from FlowEngineV2.ExecuteAsync). Distinct from INV-AT-069 which is strictly "no matching active row in chatbot_flows". Lead row already exists; welcome dispatch did not complete.
    user_message: Hosgeldin akisi calistirilamadi.

  # HFM-2: Backend Translation Warmup ops endpoint
  - code: INV-BE-090
    description: /ops/translation/warmup body invalid (tenantId/texts/locales missing or empty).
    user_message: Gecersiz parametreler; tenantId, texts, locales zorunlu.
  - code: INV-BE-091
    description: /ops/translation/warmup body contains an unsupported or malformed locale code.
    user_message: Gecersiz dil kodu.
  - code: INV-BE-092
    description: /ops/translation/warmup failed for a locale due to upstream HTTP error (Gemma/Claude unreachable or 5xx).
    user_message: Ceviri servisi gecici olarak kullanilamiyor.
  - code: INV-BE-093
    description: /ops/translation/warmup failed for a locale due to empty/invalid translation response.
    user_message: Ceviri sonucu okunamadi.
  - code: INV-BE-094
    description: /ops/translation/warmup cancelled mid-batch (client disconnected or timeout).
    user_message: Isletim iptal edildi.

  # FEAT-LIW: Lead Intake Webhook (INV-BE-100+) — Chunk A
  - code: INV-BE-100
    description: Lead intake API key missing/invalid (X-Invekto-Api-Key header)
    user_message: Gecersiz API anahtari.
  - code: INV-BE-101
    description: Lead intake source_slug path param format invalid (must match ^[a-z0-9][a-z0-9-]{0,49}$)
    user_message: Kaynak tanimi gecersiz.
  - code: INV-BE-102
    description: Lead intake consent canonical field missing from field map or payload
    user_message: Onay alani zorunlu.
  - code: INV-BE-103
    description: Lead intake field mapping resolution failed (required canonical field not mapped or source key missing)
    user_message: "Alan eslemesi eksik: {field}"
  - code: INV-BE-104
    description: Lead intake phone number failed E.164 normalization (libphonenumber parse error or invalid number)
    user_message: Telefon numarasi gecersiz.
  - code: INV-BE-105
    description: Lead intake consent value resolved to non-true (false, null, missing, or non-boolean)
    user_message: Onay degeri true olmali.
  - code: INV-BE-106
    description: Lead intake rate limit exceeded (100 req/min per API key, sliding window)
    user_message: Cok fazla istek, sonra deneyiniz.
  - code: INV-BE-107
    description: Lead intake tenant_landing_settings row missing for tenant (setup incomplete)
    user_message: Tenant ayarlari tamamlanmadi.
  - code: INV-BE-108
    description: Lead intake tenant_landing_settings.landing_field_map JSONB column is malformed (JsonException on parse). Tenant config fix required; endpoint returns 500 until the operator repairs the JSON.
    user_message: Tenant alan eslemesi bozuk, yetkili ile iletisime gecin.
  - code: INV-BE-109
    description: Lead intake request body missing or empty `fields` object. Caller sent null JSON or an empty fields map; no canonical value resolution can run.
    user_message: Istek govdesi bos veya eksik; fields alani zorunlu.

  # FEAT-LIW Chunk B: WA-direct internal endpoint (service-to-service Automation -> Backend)
  - code: INV-BE-110
    description: /api/internal/leads/intake/wa-direct internal auth failed (missing/empty X-Internal-Service-Token, mismatch with InternalServices:SharedSecret, or shared secret unconfigured at Backend). Endpoint returns 401 (missing) / 403 (mismatch) / 500 (unconfigured).
    user_message: Servisler arasi yetki gecersiz veya yapilandirilmamis.
  - code: INV-BE-111
    description: WA-direct intake payload phone field missing/empty or libphonenumber-csharp E.164 normalize failure. Endpoint returns 400.
    user_message: Telefon numarasi eksik veya gecersiz.
  - code: INV-BE-112
    description: WA-direct intake payload tenant_id has no row in tenant_registry. Defense-in-depth check guards against a buggy Automation caller (the shared-secret auth proves the caller is an Invekto service, but caller-supplied tenant_id is otherwise trusted blindly). Endpoint returns 400.
    user_message: Tanimsiz tenant; kayit reddedildi.

  # FEAT-LIW Chunk C: Dashboard settings endpoints (Dashboard-driven LIW config)
  - code: INV-BE-113
    description: /api/v1/tenant/landing/* mutation endpoint detected a row_version mismatch on the optimistic-concurrency guard (UPDATE ... WHERE updated_at = @expected returned 0 rows). Another tab/operator saved a change between the Dashboard's GET and the subsequent mutation. Endpoint returns 409; client is expected to refetch + retry.
    user_message: Ayarlar baska bir sekmede degistirildi, son hali yuklendi.
  - code: INV-BE-114
    description: /api/v1/tenant/landing/fieldmap (PUT) field map validation failure — required canonical field 'phone' or 'consent' is not mapped, an empty source-field string was supplied, or the same canonical target is mapped from multiple source rows (duplicates allowed only on 'metadata'). Endpoint returns 400 with errors[] array listing every failing row.
    user_message: Alan eslemesinde zorunlu canonical alan eksik veya hatali.
  - code: INV-BE-115
    description: /api/v1/tenant/landing/fieldmap (PUT) field map validation failure — canonical value outside the allowlist (name, phone, email, consent, utm_source, utm_medium, utm_campaign, utm_content, utm_term, referer, metadata). Endpoint returns 400.
    user_message: Tanimsiz canonical alan.
  - code: INV-BE-116
    description: /api/v1/tenant/landing/dry-run (POST) payload JSON parse failure. The Dashboard textarea contains invalid JSON. Endpoint returns 400.
    user_message: Dry-run payload gecersiz (JSON format hatasi).
  - code: INV-BE-117
    description: /api/v1/tenant/landing/apikey/rotate|revoke or /api/v1/tenant/landing/fieldmap (PUT) pre-check — JWT-bound tenant_id has no row in tenant_registry (auth drift, stale test JWT, or JWT minted for a deleted tenant). Pre-check runs BEFORE opening the settings transaction via TenantRegistryRepository.TenantExistsAsync; on miss the service returns 400 without touching tenant_landing_settings. Classifies caller-bug scenarios distinctly from real DB connectivity failures (INV-DB-001/503).
    user_message: Tanimsiz tenant. Lutfen sistem yoneticisine basvurun.

  # FEAT-MCC: Multi-City Campaign config (INV-BE-118..120 + INV-BE-121 transient)
  # Codes intentionally allocated AFTER INV-BE-117 (LIW Chunk C) — earlier spec draft
  # used INV-BE-090..091 which collide with Translation (INV-BE-090..095).
  - code: INV-BE-118
    description: /api/v1/tenant-settings/campaign-config PUT validation failure — campaign slug regex/uniqueness, max-campaigns cap (8), max cities/dates per campaign cap (20), date ordering (start_date <= end_date), or dates[].city referencing a non-existent campaigns[].cities[].slug. Endpoint returns 400 with the offending campaign slug + field in the error envelope. Same code surfaces in Automation when a {{campaign.X}} placeholder cannot resolve because the referenced slug/city/date is missing post-edit.
    user_message: Kampanya tanımı geçersiz veya bulunamadı; kampanya alanlarını gözden geçirin.
  - code: INV-BE-119
    description: Outbound message dispatch (Automation SendCallbackAsync OR Marketing FollowupStageJob.ExecuteAsync) is bound to a campaign whose active window has not started yet OR has already ended (NOW outside [start_date, end_date] inclusive in tenant_settings.timezone, OR campaign.active=false). Outbound is rejected without delivery; ops grep this code to count off-window suppressions. The check fires only when the rendered message contains a `{{campaign.*}}` placeholder OR the caller explicitly tagged the dispatch with a campaign slug; campaign-agnostic outbound is unaffected.
    user_message: Bu kampanyanın gönderim penceresi şu anda kapalı; mesaj gönderilmedi.
  - code: INV-BE-120
    description: /api/v1/tenant-settings/campaign-config PUT validation failure — campaign slug uses a reserved token (canonical reserved set: 'primary' kept for spec-default backward-compat; 'system'; 'default'; 'all'; tenant must pick a domain-meaningful slug). Endpoint returns 400 with the rejected slug.
    user_message: Bu kampanya adı sistem rezerv kelimesi; lütfen farklı bir slug seçin.
  - code: INV-BE-121
    description: tenant_settings.campaign_config DB read/write transient failure (Npgsql exception during GET/PUT or resolver fetch). Distinct from generic INV-BE-001 so ops dashboards can isolate campaign-config storage outages from broader Backend microservice unavailability. Resolver fall-through behaviour on read failure is to return an empty campaigns list (window guard becomes no-op, substitution renders empty string), preserving outbound flow continuity at the cost of stale config until the DB recovers.
    user_message: Kampanya ayarları geçici olarak okunamıyor/kaydedilemiyor; birkaç saniye sonra tekrar deneyin.

  # ── AA — AgentAI ──
  - code: INV-AA-001
    description: Invalid request payload
    user_message: Geçersiz istek formatı.
  - code: INV-AA-002
    description: Reply generation failed
    user_message: AI cevap önerisi oluşturulamadı.
  - code: INV-AA-003
    description: Intent detection failed
    user_message: Niyet algılama başarısız.
  - code: INV-AA-004
    description: No conversation context
    user_message: Sohbet geçmişi sağlanmadı.
  - code: INV-AA-005
    description: Claude API timeout
    user_message: AI servisi zaman aşımına uğradı.
  - code: INV-AA-006
    description: Invalid feedback payload
    user_message: Geçersiz geri bildirim formatı.
  - code: INV-AA-007
    description: Knowledge service unavailable
    user_message: Bilgi bankası servisi geçici olarak kullanılamıyor (öneri üretildi, kaynak referansı yok).
  - code: INV-AA-008
    description: Language detection failed
    user_message: Dil algılama başarısız, varsayılan dil kullanıldı.
  - code: INV-AA-009
    description: Conversation summary failed
    user_message: Konuşma özeti oluşturulamadı, ham geçmiş kullanıldı.

  # ── WC — WebChat ──
  - code: INV-WC-001
    description: Invalid visitor ID
    user_message: Geçersiz ziyaretçi kimliği.
  - code: INV-WC-002
    description: Conversation not found
    user_message: Sohbet bulunamadı.
  - code: INV-WC-003
    description: Message send failed
    user_message: Mesaj gönderilemedi.
  - code: INV-WC-004
    description: AI reply generation failed
    user_message: AI yanıtı oluşturulamadı.
  - code: INV-WC-005
    description: AI reply timeout
    user_message: AI yanıtı zaman aşımına uğradı.
  - code: INV-WC-006
    description: Invalid payload
    user_message: Geçersiz istek verisi.
  - code: INV-WC-007
    description: Auth login failed
    user_message: Giriş başarısız. E-posta veya şifre hatalı.
  - code: INV-WC-008
    description: Operator not found
    user_message: Operatör bulunamadı.
  - code: INV-WC-009
    description: Conversation already closed
    user_message: Bu sohbet kapatılmış.
  - code: INV-WC-010
    description: Hub connection failed
    user_message: Bağlantı kurulamadı.
  - code: INV-WC-011
    description: Automation webhook call failed
    user_message: Webhook bildirimi gönderilemedi.
  - code: INV-WC-012
    description: Automation webhook call timed out
    user_message: Webhook bildirimi zaman aşımına uğradı.

  # ── VA — VoiceAI (PKT-11) ──
  - code: INV-VA-001
    description: No audio file in request
    user_message: "'audio' alanında ses dosyası bulunamadı."
  - code: INV-VA-002
    description: Audio file exceeds size limit
    user_message: Ses dosyası çok büyük.
  - code: INV-VA-003
    description: Whisper API transcription failed
    user_message: Ses tanıma servisi şu anda kullanılamıyor.
  - code: INV-VA-004
    description: ChatAnalysis intent forwarding failed
    user_message: Niyet analizi servisi kullanılamıyor.
  - code: INV-VA-005
    description: Transcription log DB insert failed
    user_message: Transkripsiyon kaydı oluşturulamadı.
  - code: INV-VA-006
    description: Unsupported audio format
    user_message: Desteklenmeyen ses formatı.

  # ── DB — Database ──
  - code: INV-DB-001
    description: Connection failed
    user_message: Veritabanı bağlantısı kurulamadı.
  - code: INV-DB-002
    description: Query timeout
    user_message: Sorgu zaman aşımına uğradı.
  - code: INV-DB-003
    description: Duplicate entry
    user_message: Bu kayıt zaten mevcut.

  # ── VAL — Validation ──
  - code: INV-VAL-001
    description: Invalid format
    user_message: "Geçersiz format: {field}"
  - code: INV-VAL-002
    description: Required field
    user_message: "Zorunlu alan: {field}"
  - code: INV-VAL-003
    description: Out of range
    user_message: "Değer geçerli aralıkta değil: {field}"

  # ── OPS — Operational/diagnostic endpoints ──
  - code: INV-OPS-001
    description: Quicklogin tenant override invalid (non-numeric or negative)
    user_message: "tenant query param must be non-negative integer (e.g., ?tenant=5050)"

  # ── INT — Integration (GR-1.9) ──
  - code: INV-INT-001
    description: Webhook payload invalid
    user_message: Geçersiz webhook formatı.
  - code: INV-INT-002
    description: Callback to Main App failed
    user_message: Main App'e bildirim gönderilemedi.
  - code: INV-INT-003
    description: Unknown webhook event type
    user_message: Bilinmeyen event tipi.
  - code: INV-INT-004
    description: Tenant not found in registry
    user_message: Bu tenant kayıtlı değil.

  # INMA Bridge (INV-INT-100+) — Dashboard iframe postMessage bridge
  - code: INV-INT-100
    description: Bridge not ready (no trustedParentOrigin yet)
    user_message: Ana pencere ile bağlantı kurulamadı.
  - code: INV-INT-101
    description: Bridge disposed during pending refresh
    user_message: Oturum yenilenemeden kapatıldı.
  - code: INV-INT-102
    description: Refresh timeout (parent did not respond in 15s)
    user_message: Token yenileme zaman aşımına uğradı.
  - code: INV-INT-103
    description: Refresh failed (parent returned error)
    user_message: Token yenilenemedi.
  - code: INV-INT-104
    description: Invalid access token from parent
    user_message: Geçersiz oturum bilgisi.
  - code: INV-INT-105
    description: Invalid apiBaseUrl from parent (regex mismatch)
    user_message: Geçersiz API adresi.
  - code: INV-INT-106
    description: HTTP request failed after refresh retry
    user_message: İstek başarısız oldu.
  - code: INV-INT-107
    description: INMA -> INSE token exchange failed (postMessage bootstrap, Faz 2)
    user_message: Oturum açılamadı; lütfen tekrar deneyin.
  - code: INV-INT-108
    description: (DEPRECATED 2026-04-17) Welcome endpoint fetch failed after INMA bootstrap. Removed because welcome metadata is already returned by the exchange response — extra getWelcome call was redundant and produced misleading 401 noise post-exchange. Code number reserved (do not reuse).
    user_message: (deprecated)
  - code: INV-INT-109
    description: inma:navigate postMessage rejected (invalid path or unauthenticated session; no queue, parent must retry)
    user_message: Yönlendirme reddedildi.

  # Zoho OAuth (INV-INT-110+) — Adim 2 backend integration
  - code: INV-INT-110
    description: Unknown Zoho region (accounts-server URL not in registry)
    user_message: Zoho bağlantı bölgesi tanınmadı.
  - code: INV-INT-111
    description: OAuth state JWT invalid or expired
    user_message: Bağlantı oturumu geçersiz veya süresi dolmuş; tekrar deneyin.
  - code: INV-INT-112
    description: OAuth state tenant mismatch
    user_message: Bağlantı oturumu eşleşmedi.
  - code: INV-INT-113
    description: Zoho token exchange failed
    user_message: Zoho yetkilendirme başarısız oldu.
  - code: INV-INT-114
    description: Zoho token refresh failed
    user_message: Zoho oturumu yenilenemedi.
  - code: INV-INT-115
    description: Zoho connection not found for tenant
    user_message: Bu hesap için Zoho bağlantısı bulunamadı.
  - code: INV-INT-116
    description: Zoho region not configured (missing client_id/secret block)
    user_message: Zoho bölgesi yapılandırılmamış.
  - code: INV-INT-117
    description: Refresh token decryption failed
    user_message: Zoho oturum bilgisi okunamadı.
  - code: INV-INT-118
    description: Zoho disconnected by tenant (refresh blocked)
    user_message: Zoho bağlantısı kapalı.
  - code: INV-INT-119
    description: Zoho rate limit reached
    user_message: Zoho istek limiti aşıldı, lütfen sonra deneyin.
  # Zoho Sync (INV-INT-120+) — Adim 3 Paket 1: Source -> Zoho sync pipeline
  - code: INV-INT-120
    description: Zoho stage mapping not configured for zoho_event
    user_message: Zoho aşama eşleştirmesi yapılmamış, lütfen Zoho ayarlarını tamamlayın.
  - code: INV-INT-121
    description: Zoho Leads Blueprint not configured (Blueprint-only policy)
    user_message: Zoho Leads modülünde Blueprint aktif değil.
  - code: INV-INT-122
    description: Zoho Blueprint transition id not found for lead
    user_message: Zoho blueprint geçişi bulunamadı, eşleştirmeyi yenileyin.
  - code: INV-INT-123
    description: Zoho Lead not found
    user_message: Zoho kaydı bulunamadı.
  - code: INV-INT-124
    description: Internal service auth token missing/invalid (POST /api/internal/zoho/sync)
    user_message: Dahili servis yetki hatası.
  - code: INV-INT-125
    description: Zoho sync infrastructure failure (DB/transport/parse/unexpected Zoho API shape)
    user_message: Zoho senkronizasyonunda beklenmeyen bir hata oluştu, tekrar denenecek.
  - code: INV-INT-126
    description: Internal service auth shared secret not configured on integrations service
    user_message: Sistem yapılandırma hatası, lütfen yöneticiye bildirin.
  - code: INV-INT-127
    description: Backend-side Zoho sync HTTP transport failure (Adim 3 Paket 2, fire-and-forget dispatcher)
    user_message: Zoho ile senkronizasyon arka planda başarısız oldu, otomatik yeniden denenecek.
  # Zoho Dashboard UI (INV-INT-128+) — Adim 3 Paket 3-B1: Dashboard UI API surface
  - code: INV-INT-128
    description: Zoho sync log row not found for tenant (retry endpoint)
    user_message: Senkronizasyon kaydı bulunamadı.
  - code: INV-INT-129
    description: Zoho sync log row not in 'failed' state (retry allowed only on failed rows)
    user_message: Sadece başarısız senkronizasyon kayıtları tekrar denenebilir.
  - code: INV-INT-130
    description: Zoho refresh_token revoke best-effort failure during disconnect (non-fatal; local disconnect proceeds)
    user_message: Zoho bağlantısı kapatıldı (uzaktan iptal doğrulanamadı).
  # Zoho super-admin cross-tenant ops dashboard (INV-INT-131+) — Adim 3 Paket 3-C
  - code: INV-INT-131
    description: Super-admin cross-tenant Zoho ops read failure (DB/transport unexpected error)
    user_message: Zoho yönetim verisi okunamadı, tekrar deneyin.
  - code: INV-INT-132
    description: Force-disconnect target tenant has no active Zoho connection (already disconnected or never connected)
    user_message: Bu firma için aktif Zoho bağlantısı bulunamadı.
  - code: INV-INT-133
    description: Ops batch retry payload invalid (empty ids list or malformed body)
    user_message: Tekrar denenecek kayıt listesi boş veya geçersiz.
  - code: INV-INT-134
    description: Ops batch retry exceeded max 50 ids per request
    user_message: Toplu tekrar deneme en fazla 50 kayıt içerebilir.
  - code: INV-INT-135
    description: Ops query parameter validation failure (filter/status/event/date inputs on /api/internal/ops/zoho/*)
    user_message: Filtre değerleri geçersiz, kontrol edin.
  # Adim 4 P4.1: Stage Mapping editor full state coverage
  - code: INV-INT-136
    description: Lead_Status field not found in Zoho /crm/v6/settings/fields response (Lead_Status field missing or pick_list_values empty)
    user_message: Zoho Leads modulunde Lead_Status alani bulunamadi, alan ayarlarini kontrol edin.
  - code: INV-INT-FE-131
    description: Frontend-only fallback when upstream envelope is missing (P3-C OpsZohoPage extractError)
    user_message: Beklenmedik bir hata oluştu, tekrar deneyin.
  - code: INV-INT-FE-132
    description: Frontend-only fallback for Stage Mapping editor (P4 ZohoStageMappingPage extractError)
    user_message: Beklenmedik bir hata oluştu, tekrar deneyin.
  # Adim 4 P4.2: Stage Mapping discover — fallback aggregation observability
  - code: INV-INT-137
    description: All sampled Zoho leads returned RECORD_NOT_IN_PROCESS during blueprint aggregation (no lead is engaged in the blueprint workflow); distinct from BlueprintNotConfigured (no leads at all)
    user_message: Zoho'daki lead'ler Blueprint sürecine dahil değil. Manuel ID ile her satırı elle girin (Zoho → Setup → Automation → Blueprint → ilgili Blueprint → her geçişe tıklayın → URL'deki son segmenti kopyalayın).

  # FEAT-VCP Video Consultation Provider (INV-INT-140+) — Chunk A/B/C
  - code: INV-INT-140
    description: Video consultation provider (Chunk C GoogleMeetProvider) OAuth refresh token is invalid or expired. Tenant must reconnect their Google Workspace account via the Dashboard provider-settings flow. Declared in Chunk A, actively surfaced by Chunk C.
    user_message: Video consultation OAuth refresh token invalid or expired. Please reconnect calendar.
  - code: INV-INT-141
    description: Video consultation meeting create call failed (provider threw). Chunk B appointment handler catches provider exceptions and surfaces this code in the MeetingResult failure envelope; retry is safe because providers are idempotent for identical (tenant, title, start) tuples.
    user_message: Video consultation meeting create failed. Please try again or contact support.
  - code: INV-INT-142
    description: VideoProviderFactory returned null — tenant business state "provider not configured". Either tenant_settings.video_provider is null (never configured) or the configured value names an implementation not yet wired (e.g. 'googlemeet' before Chunk C ships). Distinct from INV-INT-143 (DB outage); caller maps this to a non-blocking appointment confirmation without a meeting link. NpgsqlException from the underlying probe is NOT collapsed into this code.
    user_message: Video consultation provider not configured for this tenant. Please set video_provider in settings.
  - code: INV-INT-143
    description: VideoProviderFactory.ResolveAsync surfaced an NpgsqlException while reading tenant_settings — DB outage or connectivity failure. Distinct from INV-INT-142 (legitimate "not configured" state). Chunk B's appointment handler catches the exception and surfaces this code with HTTP 503 so operators know to check database health rather than tenant configuration.
    user_message: Video consultation settings could not be read (database error). Please retry; if persistent, contact support.
  - code: INV-INT-144
    description: FEAT-VCP Chunk B — Appointments → Integrations POST /internal/video/meetings HTTP call failed (5xx / network failure / timeout). Hangfire AutomaticRetry (default 10 attempts exponential backoff) target. Distinct from INV-INT-141 (provider threw an ArgumentException on malformed input) and INV-INT-143 (DB outage inside factory resolve). Emitted by IntegrationsVideoClient in Appointments when the hop itself fails to complete — retry re-issues the POST once Integrations / network recovers.
    user_message: Video consultation setup temporarily unavailable. Retrying automatically.
  - code: INV-INT-145
    description: FEAT-VCP Chunk B — VideoReminderJob (or VideoMeetingCreationJob) fired but the appointment state changed between scheduling and firing — status is no longer 'confirmed', meeting_link was cleared, the appointment was deleted, or the reminder was already marked sent (idempotency guard). Informational / audit; no retry. Expected after cancel/complete transitions; frequent occurrences for a single appointment indicate orphan-job cleanup failure.
    user_message: (internal; reminder skipped because appointment state changed)
  - code: INV-INT-146
    description: FEAT-VCP Chunk B — Outbound POST /api/v1/outbound/trigger for video_meeting_confirmed / video_reminder_24h / video_reminder_1h returned a non-success status (5xx / network error / timeout). Hangfire AutomaticRetry target. Distinct from INV-INT-144 (provider hop) — this is the customer-facing WA dispatch failure. Most common root cause during pilot rollout is a missing outbound_templates row for the tenant + trigger_event tuple (ops should verify Dent-style manual seed post-deploy).
    user_message: Video consultation reminder delivery delayed. Retrying automatically.
  - code: INV-INT-147
    description: FEAT-VCP Chunk B — Internal shared-secret authentication rejected the inbound hop on POST /internal/video/meetings. Emitted for missing X-Internal-Service-Token header (401), invalid header value (403), and server-side InternalServices:SharedSecret misconfiguration (500). Distinct from INV-INT-142 (business state: provider not configured) so operators can distinguish "caller authentication failed" from "tenant hasn't picked a provider." Not customer-facing — the Appointments job treats any 4xx/5xx as a retriable hop failure and surfaces INV-INT-144 in its own logs.
    user_message: (internal; service-to-service auth rejected)

  # ── OB — Outbound (GR-1.3) ──
  - code: INV-OB-001
    description: Invalid broadcast payload
    user_message: Geçersiz toplu mesaj isteği.
  - code: INV-OB-002
    description: Template not found
    user_message: Mesaj şablonu bulunamadı.
  - code: INV-OB-003
    description: Rate limit exceeded (queued)
    user_message: Gönderim limiti aşıldı, mesajlar kuyrukta bekliyor.
  - code: INV-OB-004
    description: Recipient opted out
    user_message: Alıcı mesaj almak istemiyor (opt-out).
  - code: INV-OB-005
    description: Broadcast not found
    user_message: Toplu mesaj kaydı bulunamadı.
  - code: INV-OB-006
    description: Delivery status update failed
    user_message: Teslimat durumu güncellenemedi.
  - code: INV-OB-007
    description: Invalid template payload
    user_message: Geçersiz şablon formatı.
  - code: INV-OB-008
    description: No matching trigger template
    user_message: Bu event için eşleşen şablon bulunamadı.
  - code: INV-OB-009
    description: Message send callback failed
    user_message: Mesaj gönderim callback'i başarısız oldu.
  - code: INV-OB-010
    description: Too many recipients (max 1000)
    user_message: Alıcı sayısı sınırı aşıldı (max 1000).
  - code: INV-OB-011
    description: Template language not available
    user_message: İstenen dilde şablon bulunamadı, varsayılan dil kullanıldı.
  - code: INV-OB-012
    description: No template for language
    user_message: Bu dilde şablon tanımlanmamış.
  - code: INV-OB-013
    description: Invalid campaign payload
    user_message: Geçersiz kampanya isteği.
  - code: INV-OB-014
    description: Campaign not found
    user_message: Kampanya bulunamadı.
  - code: INV-OB-015
    description: Campaign already active
    user_message: Kampanya zaten aktif/zamanlanmış.
  - code: INV-OB-016
    description: Conversion record failed
    user_message: Dönüşüm kaydı oluşturulamadı.
  - code: INV-OB-017
    description: AI personalization unavailable
    user_message: AI kişiselleştirme servisi kullanılamıyor.
  - code: INV-OB-018
    description: Consent not given
    user_message: Alıcı pazarlama izni vermemiş.
  - code: INV-OB-019
    description: Invalid consent payload
    user_message: Geçersiz izin kaydı isteği.
  - code: INV-OB-020
    description: Data deletion failed
    user_message: Veri silme işlemi başarısız oldu.
  # INV-OB-021..023 reserved in ErrorCodes.cs by PKT-6B1 (ecom/clinic triggers, lead follow-up).
  # FEAT-J2 (INMA opt-out outbox + MessageCategory) starts at INV-OB-024.
  - code: INV-OB-024
    description: INMA opt-out outbox row enqueued, awaiting push
    user_message: Pazarlama kaldırma isteği INMA tarafına aktarılmak üzere sıraya alındı.
  - code: INV-OB-025
    description: INMA opt-out push failed (max attempts or INMA 908 contact-not-found)
    user_message: Pazarlama kaldırma INMA tarafına iletilemedi, destek ekibi inceleyecek.
  - code: INV-OB-026
    description: INMA opt-out push deferred (NoOp mode active, Mode=Http ile drain edilecek)
    user_message: Pazarlama kaldırma isteği kaydedildi, iletim geçici olarak bekliyor.
  - code: INV-OB-027
    description: WapCRM chatoperation returned 906 (chat-level marketing block)
    user_message: Bu sohbet için pazarlama mesajı gönderilemedi (alıcı kapatmış).
  - code: INV-OB-028
    description: WapCRM chatoperation returned 907 (contact-level global marketing block)
    user_message: Bu kişi tüm kanallarda pazarlama mesajı almak istemiyor.
  - code: INV-OB-029
    description: Manuel opt-out last-known instance lookup returned null
    user_message: Bu numara için sistemde geçmiş konuşma yok, önce etkileşim gerekli.
  - code: INV-OB-030
    description: Audit — opt-out bypassed for transactional event (informational)
    user_message: (İşlemsel mesaj izin olmadan iletildi — audit kaydı oluşturuldu.)
  - code: INV-OB-031
    description: enforce_message_category=TRUE and event_name null or outside allow-list
    user_message: Mesaj kategorisi belirsiz — flow yapılandırması güncellenmeli.
  - code: INV-OB-032
    description: Audit — admin ops /outbox/retry-skipped invoked (drain trigger)
    user_message: (Operasyon kaydı — skipped outbox girişleri yeniden kuyruğa alındı.)
  # ── FEAT-DMP: INMA DynamicMessage placeholder integration ──
  - code: INV-OB-033
    description: DynamicMessageValidator pre-send reject — MessageText placeholder outside INMA allowlist OR INMA 900/902 (empty fields / placeholder-text mismatch)
    user_message: Mesaj metnindeki dinamik alan desteklenmiyor — şablonu güncelleyin.
  - code: INV-OB-034
    description: INMA 901 — placeholder not supported by tenant (cf column inactive or unknown key)
    user_message: Kullanılan alan bu tenant için yapılandırılmamış.
  - code: INV-OB-035
    description: INMA 903 — phone/account not matched to any Customer row (DynamicMessage mode)
    user_message: Müşteri kaydı INMA'da bulunamadı; kişisel mesaj gönderilemedi.
  - code: INV-OB-036
    description: INMA 905 — placeholder field exists but Customer row has NULL value
    user_message: Kişiselleştirme alanının değeri boş; mesaj gönderilemedi.
  - code: INV-OB-037
    description: INMA /api/dynamicfields fetch failed (HTTP/timeout/malformed JSON / Status:false) — upstream transient
    user_message: INMA dinamik alanları yüklenemedi; daha sonra tekrar deneyin.
  - code: INV-OB-038
    description: Tenant has no WapCRM secret key configured; dynamic-fields proxy returns 422 so picker shows "configure INMA first"
    user_message: INMA entegrasyonu yapılandırılmamış. Yönetici tenant ayarlarından INMA bağlantısını eklemelidir.

  # ── IG — Integrations (GR-3.4/3.6) ──
  - code: INV-IG-001
    description: Invalid account payload
    user_message: Geçersiz entegrasyon hesabı isteği.
  - code: INV-IG-002
    description: Account not found
    user_message: Entegrasyon hesabı bulunamadı.
  - code: INV-IG-003
    description: Provider sync failed
    user_message: Sağlayıcı senkronizasyonu başarısız.
  - code: INV-IG-004
    description: Order not found
    user_message: Sipariş bulunamadı.
  - code: INV-IG-005
    description: Provider connection failed
    user_message: Sağlayıcı bağlantı testi başarısız.
  - code: INV-IG-006
    description: Invalid order query
    user_message: Geçersiz sipariş sorgusu.
  - code: INV-IG-007
    description: Cargo tracking unavailable
    user_message: Kargo takip bilgisi kullanılamıyor.
  - code: INV-IG-008
    description: Invalid review webhook payload
    user_message: Geçersiz değerlendirme webhook isteği.
  - code: INV-IG-009
    description: Review alert creation failed
    user_message: Değerlendirme uyarısı oluşturulamadı.
  - code: INV-IG-010
    description: Stock query failed
    user_message: Stok sorgusu başarısız.
  - code: INV-IG-011
    description: E-commerce provider not found
    user_message: E-ticaret sağlayıcısı bulunamadı.
  - code: INV-IG-012
    description: E-commerce product query failed
    user_message: Ürün sorgusu başarısız.
  - code: INV-IG-013
    description: E-commerce customer query failed
    user_message: Müşteri sorgusu başarısız.
  - code: INV-IG-014
    description: E-commerce order mutation failed
    user_message: Sipariş işlemi başarısız.
  - code: INV-IG-015
    description: OAuth2 token acquisition failed
    user_message: E-ticaret sağlayıcı kimlik doğrulaması başarısız.
  - code: INV-IG-016
    description: GraphQL query failed
    user_message: E-ticaret API sorgusu başarısız.

  # ── AP — Appointments (GR-2.4) ──
  - code: INV-AP-001
    description: Invalid slot payload
    user_message: Geçersiz slot tanımlama isteği.
  - code: INV-AP-002
    description: Slot not found
    user_message: Randevu slotu bulunamadı.
  - code: INV-AP-003
    description: Invalid booking payload
    user_message: Geçersiz randevu isteği.
  - code: INV-AP-004
    description: Slot fully booked
    user_message: Bu slot dolu, başka bir zaman seçin.
  - code: INV-AP-005
    description: Appointment not found
    user_message: Randevu bulunamadı.
  - code: INV-AP-006
    description: Already cancelled
    user_message: Randevu zaten iptal edilmiş.
  - code: INV-AP-007
    description: Invalid date/time
    user_message: Geçersiz tarih veya saat.
  - code: INV-AP-008
    description: Booking in the past
    user_message: Geçmiş tarihli randevu alınamaz.
  - code: INV-AP-009
    description: Reminder send failed
    user_message: Hatırlatma mesajı gönderilemedi.
  - code: INV-AP-010
    description: Outbound service unavailable
    user_message: Mesaj gönderim servisi geçici olarak kullanılamıyor.
  - code: INV-AP-011
    description: Invalid waitlist payload
    user_message: Geçersiz bekleme listesi isteği.
  - code: INV-AP-012
    description: Waitlist entry not found
    user_message: Bekleme listesi kaydı bulunamadı.
  - code: INV-AP-013
    description: Invalid pricing payload
    user_message: Geçersiz fiyat tanımlama isteği.
  - code: INV-AP-014
    description: Pricing not found
    user_message: Fiyat kaydı bulunamadı.
  - code: INV-AP-015
    description: Calendar sync failed
    user_message: Takvim senkronizasyon hatası.
  - code: INV-AP-016
    description: Invalid lifecycle payload
    user_message: Geçersiz tedavi takip isteği.
  - code: INV-AP-017
    description: Lifecycle not found
    user_message: Tedavi takip kaydı bulunamadı.
  - code: INV-AP-018
    description: Lifecycle already finished
    user_message: Tedavi takip süreci zaten tamamlanmış veya iptal edilmiş.
  - code: INV-AP-019
    description: Invalid lifecycle type
    user_message: Geçersiz takip tipi (post_treatment, plan_approval, pre_op).
  - code: INV-AP-020
    description: Lifecycle step send failed
    user_message: Takip mesajı gönderilemedi.

  # ── AD — Attribution (GR-3.14) ──
  - code: INV-AD-001
    description: Invalid attribution payload
    user_message: Geçersiz attribution isteği.
  - code: INV-AD-002
    description: Attribution not found
    user_message: Attribution kaydı bulunamadı.
  - code: INV-AD-003
    description: Invalid cost entry
    user_message: Geçersiz reklam maliyeti girişi.
  - code: INV-AD-004
    description: Cost not found
    user_message: Reklam maliyeti kaydı bulunamadı.
  - code: INV-AD-005
    description: Invalid lead status update
    user_message: Geçersiz lead durum güncellemesi.

  # ── MK — Marketing (GR-3.21/3.22) ──
  - code: INV-MK-001
    description: Invalid review request payload
    user_message: Geçersiz yorum talebi isteği.
  - code: INV-MK-002
    description: Review request not found
    user_message: Yorum talebi bulunamadı.
  - code: INV-MK-003
    description: Invalid referral payload
    user_message: Geçersiz referans isteği.
  - code: INV-MK-004
    description: Referral not found
    user_message: Referans kaydı bulunamadı.
  - code: INV-MK-005
    description: Referral code already exists
    user_message: Referans kodu zaten mevcut (tekrar deneyin).
  - code: INV-MK-006
    description: Invalid tourism lead payload
    user_message: Geçersiz medikal turizm lead isteği.
  - code: INV-MK-007
    description: Tourism lead not found
    user_message: Medikal turizm lead bulunamadı.
  - code: INV-MK-008
    description: Invalid tourism lead status
    user_message: Geçersiz lead durumu.
  - code: INV-MK-009
    description: Review stats query failed
    user_message: Yorum istatistikleri sorgusu başarısız.
  - code: INV-MK-010
    description: Tourism stats query failed
    user_message: Turizm istatistikleri sorgusu başarısız.
  - code: INV-MK-011
    description: Invalid risk assessment payload
    user_message: Geçersiz risk değerlendirmesi isteği.
  - code: INV-MK-012
    description: Review risk not found
    user_message: Risk kaydı bulunamadı.
  - code: INV-MK-013
    description: Invalid risk/rescue status
    user_message: Geçersiz risk veya kurtarma durumu.
  - code: INV-MK-014
    description: Rescue stats query failed
    user_message: Kurtarma istatistikleri sorgusu başarısız.
  - code: INV-MK-015
    description: Invalid rescue template payload
    user_message: Geçersiz kurtarma şablonu isteği.
  - code: INV-MK-016
    description: Rescue template not found
    user_message: Kurtarma şablonu bulunamadı.
  - code: INV-MK-017
    description: Invalid treatment catalog payload
    user_message: Geçersiz tedavi katalogu isteği.
  - code: INV-MK-018
    description: Treatment catalog item not found
    user_message: Tedavi katalogu kaydı bulunamadı.
  - code: INV-MK-019
    description: Invalid conversation payload
    user_message: Geçersiz konuşma isteği.
  - code: INV-MK-020
    description: Tourism conversation not found
    user_message: Turizm konuşması bulunamadı.
  - code: INV-MK-021
    description: Conversation stats query failed
    user_message: Konuşma istatistikleri sorgusu başarısız.
  - code: INV-MK-022
    description: Response generation failed
    user_message: Çok dilli cevap üretimi başarısız.
  - code: INV-MK-023
    description: Claude AI service unavailable
    user_message: Claude AI servisi kullanılamıyor.
  - code: INV-MK-024
    description: Follow-up due query failed
    user_message: Takip listesi alinamadi.

  # ── MK — Marketing FEAT-EFS Drip Sequence (INV-MK-050..055; gap from 024 intentional, reserved 025..049 for future PKT-12/13 follow-up rescue extensions) ──
  - code: INV-MK-050
    description: Followup sequence config invalid (slug/stages/template_slug shape)
    user_message: "Followup sequence yapilandirmasi gecersiz. Stage listesi, delay_days degerleri ve template_slug alanlarini kontrol edip tekrar kaydedin."
  - code: INV-MK-051
    description: Scheduled followup stage missing during execution (sequence edited mid-flight)
    user_message: "Belirtilen sequence stage bulunamadi. Sequence duzenlemesi sonrasi eski scheduled job calisiyor olabilir; etki yok, audit log alindi."
  - code: INV-MK-052
    description: Followup stage skipped because lead opted out (execution-time guard)
    user_message: "Takip mesaji gonderilmedi: musteri opt-out durumunda. Audit log olusturuldu."
  - code: INV-MK-053
    description: Followup sequence cap exceeded (max 5 stages OR cumulative window > 30 days/minutes)
    user_message: "Followup sequence cap asimi: max 5 stage / max 30 gun toplam pencere. Stage sayisini veya delay_days degerlerini dusurun."
  - code: INV-MK-054
    description: Followup sequence disabled (enabled=false at trigger time)
    user_message: "Followup sequence devre disi. Sequence ayarlarindan enable edip tekrar deneyin."
  - code: INV-MK-055
    description: Lead already has an active scheduled followup sequence (idempotency collision)
    user_message: "Lead icin zaten aktif scheduled sequence var. Mevcut sequence tamamlandiginda veya iptal edildiginde tekrar deneyin."
  - code: INV-MK-056
    description: Transient DB failure reading/writing followup tables (Npgsql)
    user_message: "Followup servisi gecici olarak kullanilamiyor (veritabani). Birkac saniye sonra tekrar deneyin."
  - code: INV-MK-057
    description: Upstream Marketing service HTTP transient failure (Backend/Automation to Marketing hop)
    user_message: "Followup servisine baglanilamiyor. Birkac saniye sonra tekrar deneyin."
  - code: INV-MK-058
    description: "Reserved: deferred follow-up paket inbound-reply pre-check failure (NoReplyCheckJob scheduling hook will adopt this code when auto-emit paket wires the concrete inbound source; unreferenced in MVP runtime)."
    user_message: "Takip tetiklenmesi dogrulanamadi (inbound sorgusu basarisiz). Audit log alindi; operator manuel tetikleyebilir."

  # ── KN — Knowledge (GR-2.1) ──
  - code: INV-KN-001
    description: Import path not found
    user_message: Belirtilen NLP dosya yolu bulunamadı.
  - code: INV-KN-002
    description: Import parse error
    user_message: Dosya parse hatası (JSON/CSV).
  - code: INV-KN-003
    description: Import DB error
    user_message: Veritabanı kayıt hatası.
  - code: INV-KN-004
    description: Search failed
    user_message: Arama sırasında hata oluştu.
  - code: INV-KN-005
    description: OpenAI timeout
    user_message: Embedding servisi zaman aşımı (anahtar kelime aramasına geçildi).
  - code: INV-KN-006
    description: OpenAI rate limit
    user_message: Embedding rate limiti aşıldı (anahtar kelime aramasına geçildi).
  - code: INV-KN-007
    description: OpenAI API error
    user_message: Embedding servisi hatası.
  - code: INV-KN-008
    description: FAQ not found
    user_message: Belirtilen FAQ bulunamadı.
  - code: INV-KN-009
    description: Invalid request
    user_message: Geçersiz istek formatı.
  - code: INV-KN-010
    description: pgvector missing
    user_message: pgvector eklentisi yüklü değil (sunucu hatası).
  - code: INV-KN-011
    description: File too large
    user_message: Dosya boyutu sınırı aşıldı.
  - code: INV-KN-012
    description: Invalid file type
    user_message: Desteklenmeyen dosya formatı.
  - code: INV-KN-013
    description: PDF extraction failed
    user_message: PDF içerik çıkarma hatası.
  - code: INV-KN-014
    description: Document not found
    user_message: Döküman bulunamadı.
  - code: INV-KN-015
    description: Upload failed
    user_message: Dosya yükleme hatası.
  - code: INV-KN-016
    description: Photo blocked (health tenant)
    user_message: Sağlık tenant'ları için hasta fotoğrafı yüklemesi engellendi (KVKK).
  - code: INV-KN-017
    description: Intent patterns not found
    user_message: Bu tenant için intent tanımları bulunamadı.
  - code: INV-KN-018
    description: Intent read failed
    user_message: Intent tanımları okunurken hata oluştu.
  - code: INV-KN-019
    description: Template not found
    user_message: Belirtilen şablon bulunamadı.
  - code: INV-KN-020
    description: Invalid template type
    user_message: Geçersiz şablon tipi (faq/message/intent/flow/scenario).
  - code: INV-KN-021
    description: Template slug conflict
    user_message: Bu slug zaten kullanılıyor.
  - code: INV-KN-022
    description: Template scope mismatch
    user_message: Scope/sector/tenant_id tutarsızlığı.
  - code: INV-KN-023
    description: Template not published
    user_message: Şablon henüz yayınlanmamış.
  - code: INV-KN-024
    description: Adoption already exists
    user_message: Bu tenant zaten bu şablonu benimsemiş.
  - code: INV-KN-025
    description: AB test invalid state
    user_message: Geçersiz A/B test durum geçişi.
  - code: INV-KN-026
    description: Template version not found
    user_message: Belirtilen versiyon bulunamadı.
  - code: INV-KN-027
    description: Cannot delete adopted template
    user_message: Benimsenmiş şablon silinemez.
  - code: INV-KN-028
    description: Onboarding failed
    user_message: Şablon dağıtımı sırasında hata oluştu (kısmi başarı).
  - code: INV-KN-029
    description: Seed from analysis failed
    user_message: Analiz verisinden şablon çıkarma hatası.
  - code: INV-KN-030
    description: Suggestion not found
    user_message: Belirtilen öneri bulunamadı.
  - code: INV-KN-031
    description: Comparison failed
    user_message: Şablon karşılaştırma sırasında hata oluştu.
  - code: INV-KN-032
    description: Invalid suggestion status
    user_message: Geçersiz öneri durum geçişi.
  - code: INV-KN-036
    description: Onboarding stats retrieval failed
    user_message: Knowledge onboarding istatistikleri alınamadı.
  - code: INV-KN-037
    description: Website URL invalid or unreachable
    user_message: Geçersiz veya erişilemeyen web sitesi URL'si.
  - code: INV-KN-038
    description: Sitemap not found or unparseable
    user_message: Sitemap.xml bulunamadı veya parse edilemedi.
  - code: INV-KN-039
    description: Website crawl failed
    user_message: Web sitesi tarama işlemi başarısız oldu.

  # ── MT — Metrics/Analytics (PKT-3) ──
  - code: INV-MT-001
    description: Metrics aggregation failed
    user_message: Metrik toplama hatası. Bir sonraki periyotta tekrar denenecek.
  - code: INV-MT-002
    description: Analytics query failed
    user_message: Analitik sorgusu başarısız oldu.
  - code: INV-MT-003
    description: Invalid date range
    user_message: Geçersiz tarih aralığı (başlangıç > bitiş veya negatif).

  # ── JOB — Hangfire Job Infrastructure (G7) ──
  - code: INV-JOB-001
    description: Hangfire storage connection failed
    user_message: Zamanlanmış görev altyapısı veritabanına bağlanamadı.
  - code: INV-JOB-002
    description: Job handler could not be resolved from DI
    user_message: Zamanlanmış görev yüklenemedi.
  - code: INV-JOB-003
    description: Duplicate recurring job registration
    user_message: Zamanlanmış görev yapılandırması çakışıyor.
  - code: INV-JOB-004
    description: Dashboard access denied (non-superadmin)
    user_message: Bu sayfayı görüntüleme yetkiniz yok.
  - code: INV-JOB-005
    description: Recurring job final failure (retries exhausted)
    user_message: Zamanlanmış görev başarısız oldu; tekrar deneme limiti aşıldı.
  - code: INV-JOB-006
    description: Hangfire orphan 'default' queue detected at startup. Jobs scheduled without explicit queue routing (missing [Queue(...)] attribute on the job class/method OR missing RecurringJobOptions.Queue on the registration) accumulate on the 'default' queue that no service worker listens to in this named-queue microservice topology. Surfaced as a structured WARN log at Backend startup (`[INV-JOB-006]` tag) with the stuck row count and drain SQL so ops can detect and remediate within the first deploy window rather than after a multi-day accumulation. Non-blocking probe; does not fail startup.
    user_message: Zamanlanmış görev altyapısında dinlenmeyen bir kuyrukta bekleyen iş tespit edildi (operasyonel uyarı).
  - code: INV-JOB-010
    description: DbBackup pg_dump exited non-zero or stderr signalled failure
    user_message: Veritabanı yedeği alınamadı.
  - code: INV-JOB-011
    description: DbBackup skipped - free disk below configured threshold
    user_message: Disk alanı yetersiz; yedek alınmadı.
  - code: INV-JOB-012
    description: DbBackup skipped - configured pg_dump binary not found on disk
    user_message: Yedek aracı bulunamadı (pg_dump).
  - code: INV-JOB-013
    description: DbBackup skipped - required configuration is missing (e.g. ConnectionStrings:PostgreSQL)
    user_message: Yedek yapılandırması eksik.

  # ── EXT — External ──
  - code: INV-EXT-001
    description: External API error
    user_message: Dış servis hatası.
  - code: INV-EXT-002
    description: External timeout
    user_message: Dış servis yanıt vermedi.

  # ── SEED — Deployment Seed Postconditions ──
  # NOT: SEED codes are raised by PL/pgSQL DO blocks at deployment time. They are
  # not consumed by runtime C# code and therefore have NO ErrorCodes.cs mirror.
  # These are operator-facing assertions that fail the seed transaction loudly
  # rather than allowing a silent no-op when existing state diverges.
  - code: INV-SEED-001
    description: Appointment slot postcondition failure (expected 4 slots, mismatch)
    user_message: Slot seed doğrulaması başarısız — beklenen 4 satır oluşmadı.
  - code: INV-SEED-002
    description: Chatbot flow postcondition failure (expected 1 row for flow_name, mismatch)
    user_message: Flow seed doğrulaması başarısız — tek satır beklendi.
  - code: INV-SEED-003
    description: Chatbot flow is_active=TRUE postcondition failure
    user_message: Flow seed doğrulaması başarısız — is_active beklentiyle eşleşmedi.
  - code: INV-SEED-004
    description: FAQ entries postcondition failure (expected >=36 rows)
    user_message: FAQ seed doğrulaması başarısız — satır sayısı yetersiz.
  - code: INV-SEED-005
    description: FAQ entries all-inactive postcondition failure (placeholder guard broken)
    user_message: FAQ seed doğrulaması başarısız — placeholder koruması kırıldı.
  - code: INV-SEED-006
    description: Tenant landing settings postcondition failure (expected 1 row)
    user_message: Landing seed doğrulaması başarısız — tek satır beklendi.
  - code: INV-SEED-007
    description: Tenant landing settings welcome_flow_slug divergent from expected
    user_message: Landing seed doğrulaması başarısız — mevcut slug beklentiyle uyuşmuyor, operator müdahalesi gerekli.
  - code: INV-SEED-008
    description: Tenant landing settings landing_field_map keys set mismatch
    user_message: Landing seed doğrulaması başarısız — field_map canonical keys setiyle uyuşmuyor.

```

## Adding New Codes

1. Find the service section in the YAML block above
2. Use the next available number for that service
3. Add entry with `code`, `description`, `user_message`
4. Mirror in `src/Invekto.Shared/Constants/ErrorCodes.cs`

```csharp
// ErrorCodes.cs example
public static class ErrorCodes
{
    public const string GeneralUnknown = "INV-GEN-001";
    public const string GeneralTimeout = "INV-GEN-002";
    public const string BackendMicroserviceUnavailable = "INV-BE-001";
}
```
