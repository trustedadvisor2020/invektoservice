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

  # ── EXT — External ──
  - code: INV-EXT-001
    description: External API error
    user_message: Dış servis hatası.
  - code: INV-EXT-002
    description: External timeout
    user_message: Dış servis yanıt vermedi.

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
