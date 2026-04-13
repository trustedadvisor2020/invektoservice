# Lessons Learned

> Q duzeltmelerinden ogrenilenler. `/learn` komutuyla guncellenir.
>
> **Arsiv Kurali:** 50+ giris olunca son 3 ay aktif kalir, eski girdiler `arch/lessons-learned-archive.md`'ye tasinir.
> TONIVA girdileri kalici olarak arsiv dosyasinda.

---

## Common Mistakes

### Codex Review

| Date | Mistake | Solution | Prevention |
|------|---------|----------|------------|
| 2026-04-15 | MCP `codex_review` tool `diff_file_path` parametresini pratikte okumuyor — sadece inline `git_diff` içeriğini review ediyor. Iter 0'da "Git diff content is not provided inline" diyerek 9/12 CQ için UNKNOWN verdi. | `git_diff` parametresine TAM diff metnini (hunk'lar dahil) inline paste et. 40KB altı diff'ler için tek seferde, üstü için file-by-file chunk. `diff_file_path` sadece ikinci derece fallback/audit için; içerik olarak güvenme. | **MCP codex_review çağırırken `git_diff` parametresine her zaman FULL inline diff geç. `diff_file_path` yalnız audit trail. Diff 40KB üstüyse chunk et. Boş/özet diff = UNKNOWN spam → otomatik FAIL.** |
| 2026-04-15 | LOW risk paketlerde CoVe verification_questions boş bırakılabilir sanılıyordu (schema zorunlu değil). Codex iter 1'de "no verification questions provided; review precondition not met for LOW risk" → COVE_VERDICT=FAIL → OVERALL=FAIL. | LOW risk paketlerde bile 2-3 CoVe sorusu ekle. Paketle ilgili kritik invariant'ları sor (örn. "Was wire format preserved?", "Do stale references remain?"). | **Her plan JSON'unda (risk seviyesi bağımsız) en az 2 CoVe verification_question tanımla. Codex COVE section'ı boş bırakamaz — boş = FAIL varsayar. Data/Auth/Lifecycle/Process kategorilerinden seç.** |
| 2026-04-15 | Shared DTO'nun namespace'ini değiştirirken eski `using Invekto.Shared.DTOs.Integration;` satırını komple silmek. O namespace'te başka tipler (OutgoingCallback, CallbackActions, WebhookEventTypes) da var → Backend build 28 error. | Shared namespace'te paylaşılan tipler ayrı kontrol et: namespace içindeki TÜM tipleri grep'le, sadece taşınan tipler için using'i keep/drop kararı ver. Namespace sadece 1 tip içermiyorsa drop ETME, sadece yenisini ekle. | **Namespace/using refactor'larında: ÖNCE `ls <namespace-folder>/` çalıştır. Tek dosya varsa drop OK. Çok dosya varsa eski using'i KORU, yenisini YAN EKLE. Büyük Program.cs gibi dosyalarda build feedback hızlı (16s), sürpriz = mental model yanlış sinyali.** |
| 2026-04-13 | Müşteri istek/planı aldığımda başta platform-level sorunları atlayıp hemen müşteri-scope pilota odaklanma eğilimi. Dent Adavista planı 3 iterasyonda yeniden çerçevelendi: (1) müşteri pilot, (2) Dent + INMA entegrasyon, (3) INMA↔INSE unification platform + Dent downstream. Her aşamada "daha derin soru" ortaya çıktı (iki ayrı uygulama değil tek native ürün vb.) | Yeni büyük iş gelince ÖNCE "bu bir platform işi mi, downstream mı?" sorusunu sor. Müşteri pilotu başlatmadan ÖNCE: ownership modeli (kim neyi yapıyor), kullanıcı deneyimi (tek app mi?), mevcut sistemde ne var/yok envanteri | **Yeni müşteri/feature geldiğinde plan açmadan önce 5 soru: (1) Bu özellik sistem-wide mi, müşteri-özel mi? (2) Mevcut bir sistemde (INMA, 3rd party) zaten var mı? (3) Kullanıcı kaç farklı yerden girecek? (4) Ownership/team sınırı nerede? (5) Platform yatırımı gerekir mi yoksa müşteri-config yeterli mi? — bunları cevaplamadan fazlara bölme.** |
| 2026-04-13 | UP0.1 scope'u başta "tüm INMA DTO'ları taşı" olarak düşünüldü — 10+ caller etkilenecekti. MVP'ye küçültüldü: sadece inline DTO'lar (Backend/Data) taşındı, mevcut Shared DTO'ları UP0.1b'ye bırakıldı. Sonuç: iteration=0, ~30dk iş | "Hijyen refactor"larda atomik scope: her paket breaking-change-free olmalı, tek sorumluluk. Mevcut kullanıcısı olan dosyalara dokunmak ayrı paket | **Refactor paketlerini atomik tut: "inline DTO'lar" ayrı paket, "Shared namespace rename" ayrı paket, "caller using update" ayrı paket. 10+ caller = ayrı risk dilimi. Her paket: diff <100 line, build tek seferde PASS, Codex iter=0 hedef.** |
| 2026-04-13 | Yeni müşteri feature'ı planlarken INMA'da zaten ne olduğunu sormadan INSE'de sıfırdan kurma eğilimi. Örn: custom fields → sistem-wide Definition Registry + JSONB (4-5g iş) tasarlandı. Q sonradan "INMA'nın zaten 10 tenant field'ı var" dedi → tüm paket iptal | Entegrasyon projelerinde build-list'i çıkarmadan ÖNCE "existing system has" envanteri. Feature-by-feature var/yok sor. Müşterinin ikinci sistemi varsa, zaten çözdüğü problemleri yeniden çözme | **Entegrasyon/unification projelerinde mutlaka `existing-system-feature-audit.md` yap: her feature için var/yok/kısmen. Yoksa dolduran şeyi inşa ederken zaten var olanı kullanan versiyon da kontrol et. "INMA'da var mı?" 42 feature'lık liste ile sor.** |
| 2026-04-11 | WhatsAppAnalytics minimal API: DI ile inject edilen servisler (AnalyticsRepository, JsonLinesLogger, vb.) .NET 8 minimal API'de explicit `[FromServices]` olmadan bazı endpoint'lerde implicit `[FromBody]` gibi davranıyor veya routing ambiguity çıkartıyor. Build PASS ama runtime 400/415 | Tüm DI servislerine `[FromServices]` attribute ekle (Microsoft.AspNetCore.Mvc namespace) | **.NET 8 minimal API'de DI inject edilen complex type servislerde her zaman explicit `[FromServices]` kullan. Primitive param (int, string) veya HttpContext/CancellationToken implicit OK. Yeni endpoint yazarken: `[FromServices] MyRepo repo` pattern** |
| 2026-04-11 | Translation DetectLanguageHandler: DTO'ya yeni `Message` field + `ResolvedText` resolver eklendi (INMA compat için) ama handler hâlâ `request.Text` okuyordu → INMA "message" gönderdiğinde 400 "Metin boş olamaz" | `request.ResolvedText` kullan (Text varsa onu, yoksa Message) | **DTO'ya alternatif field + resolver property eklerken TÜM consumer'ları da güncelle. Tek nokta: Grep yeni DTO'nun eski field'ini (`request.Text`) tüm handler/service'lerde değiştir. Field rename değil — yeni resolver eklerken** |
| 2026-04-11 | LanguageDetector heuristic tamamen Turkish-specific char frekansı bazlıydı → Arabic/Russian/Korean/Chinese için "en" dönüyordu. Multi-language tenant için yanlış target detection | Unicode script-based detection: `c >= '\u0600' && c <= '\u06FF'` (Arabic), `'\u0400'-'\u04FF'` (Cyrillic), vb. 13 dil desteği | **Multi-language language detection: ÖNCE Unicode script range check (dominant non-Latin script = o dil), SONRA AI verification, SON çare heuristic. Sadece char frekansı = Latin script varsayımı = yanlış** |
| 2026-03-27 | JsonLinesLogger API'sinde SystemWarn/SystemError tek string alıyor, 2 parametre (errorCode, message) ile çağrıldığında build fail. Info/Warn/Error ise RequestContext istiyor, int tenantId değil | `SystemWarn($"[{ErrorCodes.X}] message")` pattern — error code mesaj içine embed. Tenant-aware log için `StepInfo(message, requestId)` | **Yeni mikroservis yazarken JsonLinesLogger API'sini KONTROl ET: SystemWarn(string), SystemError(string), StepInfo(string, requestId), StepError(string, requestId). İmza uyumsuzluğu = build fail** |
| 2026-03-27 | `(int)tid!` null-forgiving operator Codex CQ5 FAIL — TenantId ctx.Items'tan alınırken pattern match yerine cast+suppress | `ctx.Items.TryGetValue("TenantId", out var tid) && tid is int t ? t : 0` | **ctx.Items'tan değer alırken `is int t` pattern match kullan, `(int)tid!` null-forgiving KULLANMA. Tüm mikroservislerde aynı pattern** |
| 2026-03-27 | catch(JsonException) { } (empty body) Codex CQ2 FAIL tetikledi — settings_json parse hatası sessizce yutuluyordu | catch(JsonException ex) { _logger.SystemWarn(...error_code...) } ile değiştirildi | **catch(JsonException) DAHİL tüm typed catch bloklarında log + error code ZORUNLU. Empty catch body = Codex CQ2 silent failure = garantili FAIL** |
| 2026-03-27 | Marketing endpoint'te DatabaseConnectionFailed (generic) kullanıldı, Codex CQ12 feature-specific error code istedi | Yeni INV-MK-024 (MarketingFollowUpQueryFailed) oluşturuldu | **Her yeni endpoint'te feature-specific error code kullan, generic DatabaseConnectionFailed değil. Önceden ErrorCodes.cs'e ekle** |
| 2026-03-26 | Codex /rev iteration 1: sadece fix diff gönderildi (partial diff), Codex tam kodu göremedi → CoVe tüm sorulara UNKNOWN verdi | İterasyon fix'lerinde HER ZAMAN full `git diff --cached` gönder, sadece değişen kısım DEĞİL | **Codex'e HER iterasyonda full staged diff gönder. Partial diff = UNKNOWN verdict = gereksiz iteration** |
| 2026-03-26 | Codex OperationCanceledException catch'lerini silent failure olarak değerlendirdi — 3 iterasyon sürdü | Tüm OperationCanceledException catch bloklarına error code + log ekle | **Tüm typed catch bloklarında (OperationCanceledException dahil) error code ile log yap. Boş catch = Codex FAIL** |
| 2026-03-03 | Bash heredoc strips `\\` in JSON files: `cat > file << 'EOF'` ile yazılan dosyalarda `\\` → `\` dönüşüyor (geçersiz JSON). Backend `C:\Invekto\Backend\logs` → `JsonReaderException: 'I' is an invalid escapable character` | PowerShell in-place: `$c = Get-Content $f -Raw; [IO.File]::WriteAllText($f, $c.Replace('\','\\'))` | **appsettings.Production.json'ı ASLA bash heredoc ile yazma. server-upload (SFTP binary) veya PowerShell [IO.File]::WriteAllText kullan** |
| 2026-03-03 | Bank 3DPay callback public endpoint Codex CQ5 false positive: QNB 3DPay bankanın browser'ı POST redirect yaptığı endpoint JWT taşıyamaz. Codex "missing JWT auth" diye 3 iter fail etti | order_id UNIQUE constraint tenant güvenliğini sağlar. SQL comment + FORCE PASS | **3D payment callback endpoint'leri by design public (no JWT). Banka browser'ı redirect yapar, token taşıyamaz. Security: order_id UNIQUE + AND status='pending'. Codex false positive = FORCE PASS** |
| 2026-03-03 | Production server path yanlış biliniyordu: `E:\InvektoServices\` DEĞİL — gerçek path `C:\Invekto\`, service dirs `C:\Invekto\Backend\current\` | server-exec ile `Get-ChildItem "C:\" -Directory` ile tespit edildi | **Production server: `C:\Invekto\{Service}\current\`. NSSM service adları: `InvektoBackend` formatında (Invekto prefix + PascalCase)** |
| 2026-03-03 | .NET 8 EndpointMiddleware authorization metadata check: Automation onboarding-stats endpoint sürekli 500 veriyordu. `[Authorize]` veya `RequireAuthorization()` yok ama .NET 8 SDK implicit auth metadata ekliyor. Log spam (her Backend aggregation call'ı fail) | `builder.Services.AddAuthorization()` + `app.UseAuthorization()` — no policies = no-op ama framework check'i tatmin eder | **.NET 8 `Microsoft.NET.Sdk.Web` projelerde endpoint 500 + "authorization metadata" hatası görürsen: AddAuthorization() + UseAuthorization() ekle. Explicit `[Authorize]` olmasa bile framework implicit metadata ekleyebilir. Tüm mikro servislere proaktif ekle** |
| 2026-03-03 | Flow pre-flow enrichment sequential bottleneck: Automation her mesajda Knowledge'a HTTP + DB query sıralı çalıştırıyordu (~3s eklenen latency). Tenant intents ve settings her mesajda yeniden fetch ediliyordu | Task.WhenAll ile parallelleştir + ConcurrentDictionary cache (5-min TTL). Knowledge search timeout 15s→5s (pgvector sub-second dönmeli) | **Mikroservisler arası HTTP call'lar sıralı (sequential) ise Task.WhenAll ile parallelleştir. Sık değişmeyen veriler (tenant config, intents) için in-memory cache (5-min TTL) kullan. Timeout'lar gerçek response süresiyle orantılı olmalı** |
| 2026-03-03 | ikas Private App vs Admin App: ikas'ta iki uygulama tipi var. Admin App (builders.ikas.com) → Authorization Code Flow, redirect_uri gerektirir, SaaS entegrasyon içindir. Private App (mağaza admin → Uygulamalar → Özel Uygulamalar → Standart Uygulama) → client_credentials flow, server-side entegrasyon için doğru tip. Client_credentials gereken bir entegrasyonda Admin App oluşturulmuş → 401/422 döndü | Private App (Standart Uygulama) oluşturunca düzeldi | **ikas entegrasyonunda MUTLAKA Private App (Standart Uygulama) kullan. Admin App = Authorization Code, SaaS marketplace içindir, client_credentials DESTEKLEMEZ** |
| 2026-03-03 | ikas GraphQL field names wrong until live test: hasNextPage, stock, packages, packages.status → doğruları: `hasNext`, `stocks` (Variant'ta array), `orderPackages`, `orderPackageFulfillStatus` | GraphQL introspection (`__type(name:"OrderPackage"){fields{name}}`) ile production'a deploy etmeden önce doğrula | **GraphQL API entegrasyonunda field names ASLA varsayma. Live API ile test et veya introspection çalıştır. ikas'ta: pagination.hasNext (not hasNextPage), Variant.stocks[] (array, not stock object), Order.orderPackages (not packages)** |
| 2026-03-03 | Cross-tenant reverse lookup Codex CQ5 false positive: GetTenantIdByClientIdAsync intentionally cross-tenant — IMarketplaceProvider backward compat için tenantId'siz raw credential geçiyor, method resolve ediyor. Codex "tenant_id filter eksik" diye fail verdi (4 iter) | `/// Intentionally cross-tenant:` comment + Q FORCE PASS | **Reverse lookup (credentials → tenantId) by design cross-tenant. Comment ile "Intentionally cross-tenant: [reason]" yaz, 2 iter Codex yine fail ederse FORCE PASS** |
| 2026-03-01 | MSSQL multi-instance dedup: Ayni telefon numarasi birden fazla MSSQL instance'da olabilir (orn. EbruModa 3 instance). INSERT ON CONFLICT icinde ayni conversation_id iki kez olursa PG "cannot affect row a second time" hatasi verir | ComputeAsync'te GroupBy conversation_id + First() ile dedup, upsert oncesi | **Cross-instance MSSQL sorgularinda PG upsert oncesi MUTLAKA dedup yap. UNIQUE constraint batch icindeki duplicate'lari yakalamaz** |
| 2026-03-01 | Codex catch(Exception) false positive: Program.cs'de 10+ mevcut catch(Exception ex) pattern var ama Codex "typed catch only" diye flag'liyor | Mevcut codebase pattern'ini kontrol et, 2 iter ayni false positive = FORCE PASS | **Codex'in kural iddiasi ile mevcut codebase pattern'i cakisirsa, codebase kazanir. grep ile mevcut usage sayisini goster** |
| 2026-03-04 | PostgreSQL partial index `WHERE expires_at < NOW()` fails: `NOW()` is STABLE not IMMUTABLE — PG rejects non-IMMUTABLE functions in index predicates | Plain index without WHERE clause: `CREATE INDEX idx ON tbl(expires_at);` — still efficient for cleanup queries | **PG partial index predicate'inde SADECE IMMUTABLE fonksiyonlar kullanılabilir. NOW(), CURRENT_TIMESTAMP = STABLE → reddedilir. Expiry index'leri WHERE clause'suz oluştur** |
| 2026-03-04 | Codex DeleteExpiredAsync cross-tenant false positive: Cache cleanup DELETE WHERE expires_at < NOW() tenant_id yok — Codex "every DB query MUST filter tenant_id" diyor. Comment + Q1 hint eklendi ama Codex 3 iter ayni flag. Ayri: catch(Exception) yeni kodda NpgsqlException/HttpRequestException/InvalidOperationException/OperationCanceledException ile degistirilmeli | Comment eklendi + typed catches yapildi, ama Codex DELETE tenant_id'de inat etti → FORCE PASS | **Cross-tenant maintenance queries (cache cleanup, stats aggregation) Codex'te tenant_id false positive verecek — 1 iter document + FORCE PASS. Yeni kodda catch(Exception) yerine: DB ops=NpgsqlException, HTTP=HttpRequestException, timeout=OperationCanceledException, parse=JsonException/InvalidOperationException** |
| 2026-04-01 | JWT middleware prefix trailing slash: UseJwtAuth'a eklenen `/api/v1/translate/` (trailing slash) sadece alt-path'leri eşleştiriyor (`/translate/batch`, `/translate/detect`), tam `/api/v1/translate` (no slash) eşleşmiyor. Endpoint kendi auth yapıyorsa middleware listesine eklenmemeli | `/api/v1/translate/` prefix'i UseJwtAuth'dan çıkarıldı — endpoint kendi JWT + API key dual-auth'unu yapıyor | **Endpoint kendi auth'unu yapıyorsa (JWT OR API key dual-auth) UseJwtAuth listesine EKLEME — middleware Bearer token zorunlu kılıyor ve alternative auth path'i engelliyor. Ayrıca: trailing slash prefix match dikkat — `/foo/` sadece `/foo/bar` eşleşir, `/foo` eşleşmez** |
| 2026-04-01 | Inma companyCode string vs İnse tenant_id int: Inma `X-Tenant-Id` header'ında string code ("voila") gönderiyor, İnse `int.TryParse` ile parse ediyor → fail. Numeric code ("5050") çalışıyor, string ("voila") çalışmıyor | `tenant_registry.inma_code` kolonu + `ResolveTranslateTenantAsync`: string→PG lookup→auto-provision. MSSQL dependency yok | **Cross-system tenant resolution: Dış sistemden gelen tenant identifier her zaman string olabilir. int.TryParse başarısızsa fallback mapping tablosu (inma_code) + auto-provision pattern kullan. MSSQL bağımlılığı yerine İnse tarafında kendi mapping'i tut** |
| 2026-04-01 | Production appsettings'te InmaManagementDb connection string yoktu — MSSQL-dependent resolver çalışmadı ("Inma bağlantısı yapılandırılmamış") | MSSQL dependency kaldırıldı, sadece PostgreSQL ile çözüm | **Production config'de olmayan dependency'e bağlı kod yazma. Resolver'da graceful degradation: önce local DB (PostgreSQL), external DB (MSSQL) sadece varsa** |
| 2026-03-02 | Codex bare catch in NEW code is legitimate FAIL, not false positive: TenantPlanCache ParseFeaturesJson/ParseQuotas had bare `catch` with no logging. Unlike existing Program.cs catch(Exception) which is established pattern, NEW code bare catch = real CQ2/CQ5 violation. Also: static helper methods that need logger → make instance methods from start | (1) Remove `static` from parse helpers, (2) catch(JsonException ex) with `_logger.SystemWarn`, (3) catch(NpgsqlException ex) for DB errors | **NEW code catch kuralı: bare catch YASAK, typed exception + logging ZORUNLU. Mevcut codebase catch(Exception) = false positive, YENİ kodda catch(Exception)/bare catch = gerçek hata. Static helper'lar logger gerektiriyorsa instance method yap** |
| 2026-03-02 | Codex post-commit quality patch: Diff is delta (SSRF+typed exceptions) but CoVe questions target full feature (sitemap fallback, embedding loop, SourceType default). All 4 CoVe = UNKNOWN because those features are in already-committed code, not in diff | Q FORCE PASS. CoVe questions were written for the full feature, not the delta patch | **Post-commit quality patch'lerde CoVe questions FULL feature'i target ediyorsa, delta diff'ten cevap bulunamaz = UNKNOWN. Bu structural limitation'dir, 1 iter = FORCE PASS** |
| 2026-03-02 | IPv4-mapped IPv6 adresleri (::ffff:10.0.0.1) SSRF guard'i bypass etti — IsPrivateAddress sadece raw IPv4 ve native IPv6 kontrol ediyordu | `if (ip.IsIPv4MappedToIPv6) return IsPrivateAddress(ip.MapToIPv4());` recursive check eklendi | **SSRF guard'da IPv4-mapped IPv6 MUTLAKA kontrol edilmeli. .NET'te `IPAddress.IsIPv4MappedToIPv6` + `MapToIPv4()` ile IPv4 eşdeğerine dönüştür, sonra normal private range kontrolü yap** |
| 2026-04-07 | MockIntentDetector Türkçe karakter uyumsuzluğu: keyword'ler ASCII (`"dis"`, `"sikayet"`) ama kullanıcı Türkçe (`"diş"`, `"şikayet"`) yazıyor. `StringComparison.OrdinalIgnoreCase` Türkçe ş/ç/ğ/ı/ö/ü eşleştiremiyor. Simulation'da "diş dolgusu" tanınmıyor → generic fallback | `CultureInfo.GetCultureInfo("tr-TR").CompareInfo.IndexOf(IgnoreCase)` kullan, `ToLowerInvariant` yerine `ToLower(trCulture)` | **Türkçe metin karşılaştırmasında `OrdinalIgnoreCase` veya `ToLowerInvariant` KULLANMA — ş≠s, ç≠c, ğ≠g, ı≠i eşleşmez. `CultureInfo("tr-TR")` ile `CompareInfo.IndexOf(IgnoreCase)` veya `ToLower(trCulture)` kullan. Hardcoded keyword'ler daima Türkçe karakterle yazılmalı** |
| 2026-03-26 | Kestrel IP whitelist'te IPv6-mapped IPv4 uyuşmazlığı: Config'de `91.151.84.79` ama Kestrel `::ffff:91.151.84.79` olarak görüyor → HashSet.Contains eşleşmiyor → 401 | IP whitelist yüklenirken her IP için hem raw hem `::ffff:` mapped versiyonunu HashSet'e ekle. `IPAddress.IsIPv4MappedToIPv6` + `MapToIPv4()` ile normalize | **IP whitelist/allowlist kullanan HER yerde (webhook, CORS, rate-limit) IPv4 ve IPv6-mapped versiyonunu birlikte kaydet. Kestrel dual-stack socket IPv4 bağlantılarını `::ffff:x.x.x.x` formatında raporlar** |
| 2026-03-02 | GenerateEmbeddingsAsync while(true) loop: basarisiz embedding'ler "without embedding" kalir, sonraki batch ayni chunk'lari tekrar getir → sonsuz dongu | `if (embedded == 0) break;` — batch'te sifir ilerleme varsa dur. Keyword search hala calisir | **Embedding loop'larda MUTLAKA zero-progress guard ekle. Basarisiz chunk'lar tekrar gelmez diye varsayma — DB'den silinmedikce ayni batch tekrarlanir** |
| 2026-03-02 | SSRF guard sadece IP literal kontrol ediyordu (IsRfc1918) — hostname'leri DNS resolve etmeden geciriyordu. IPv6 private ranges ve link-local adresleri de eksikti | DNS resolution (GetHostAddressesAsync) + IPv4 private (10/172.16/192.168/169.254) + IPv6 private (fc00::/7, fe80::/10) + per-page IsUrlSafeAsync (DNS rebinding koruması) | **SSRF guard MUTLAKA DNS resolve etmeli (hostname → IP), sadece IP literal kontrol yetmez. IPv6 ULA + link-local dahil edilmeli. Crawl servislerinde per-request DNS check (DNS rebinding koruması)** |
| 2026-03-02 | Codex "PostgreSQL ONLY" false positive: MSSQL cross-DB read 5 dosyada established pattern (MssqlReaderService, BatchClassification, Benchmark, InsightResponseTime, InsightAgentLeaderboard). Codex CQ5'te "violates non-negotiable rule" diyor | grep ile 5 mevcut MSSQL usage dosyasini goster. Q interview'da "MSSQL direkt" secimi onaylandi | **"PG only" kurali OUR data storage icin. Customer MSSQL read = established cross-DB pattern, violation degil. Codex bu ayrimi yapamaz** |
| 2026-03-02 | Codex ops auth + sector-scoped table false positive 3 iter tekrarlandi: (1) "Missing JWT on ops endpoints" — ops use X-Ops-Key by design, (2) "Missing tenant_id filter" — wa_sector_* tables are sector-scoped, no tenant_id column, (3) "catch(Exception) in WA" — 30+ existing instances in WA Program.cs | Each iter summary'de aciklandi ama Codex ayni flag'i tekrarladi. 3 iter = FORCE PASS | **Codex persistent false positive recipe: ops auth + sector tables + WA catch pattern. 2 iter = document + FORCE PASS. Her iter fixable real issues'i ayir, sadece onlari fix et** |
| 2026-03-05 | .NET 8 Minimal API `Results.Ok()` uses `JsonSerializerDefaults.Web` = camelCase. PascalCase DTO `FlowId` → `flowId`, NOT `flow_id`. Frontend TS types use snake_case (`flow_id`). Anonymous object'te explicit snake_case property isimleri kullan: `flow_id = e.FlowId` | Anonymous object'te `id = e.Id, flow_id = e.FlowId` explicit mapping yap | **Minimal API endpoint'lerde frontend snake_case bekliyorsa, anonymous object property isimlerini EXPLICIT snake_case yaz. `e.FlowId` shorthand kullanma — camelCase serialize olur, snake_case degil** |
| 2026-03-05 | Versioning MAX+1 query tenant_id eksikti: `SELECT COALESCE(MAX(version_number),0) FROM flow_versions WHERE flow_id=@fid` — multi-tenant'ta baska tenant'in version_number'i max olabilir. Codex CoVe-1 yakaladi | `WHERE flow_id=@fid AND tenant_id=@tid` eklendi | **Auto-increment MAX+1 pattern'de composite key'in TUM kolonlarini WHERE'e koy. flow_id tek basina yetmez, tenant_id MUTLAKA dahil edilmeli** |
| 2026-03-05 | Non-fatal versioning pattern: Save endpoint'te version olusturma basarisiz olursa save basarisiz olmamali. Catch blogu NpgsqlException ile version hatasi logla, save devam etsin | `try { CreateFlowVersionAsync } catch (NpgsqlException) { log warning }` — save response'a version=0 don | **Yardimci islemler (versioning, logging, analytics) ana islemi BLOKE ETMEMELI. Non-fatal try/catch + typed exception + warning log. Ana islem (save) basarili donmeli** |
| 2026-03-05 | C# sealed class inheritance: `FlowVersionDetail : FlowVersionSummary` icin FlowVersionSummary sealed olunca CS0509 hatasi | sealed kaldirip normal class yap | **DTO class hierarchy gerektiren durumlarda base class'i sealed yapMA. sealed = inheritance yasak** |
| 2026-03-04 | AI Wizard system prompt'ta onay sonrası flowconfig üretim garantisi yoktu: AI "Evet, bu planla devam et" sonrası failure-path analizi gösteriyordu ama flowconfig JSON üretmiyordu. Edit mode'da "uygula/yap/değiştir" trigger words too narrow | System prompt'a geniş keyword listesi eklendi (evet, tamam, devam et, bu planla devam et). "AYNI YANIT İÇİNDE" + "SADECE metin gönderip bekleme" zorunluluğu. Frontend'de autoApplyPending flag ile flowconfig geldiğinde otomatik canvas'a uygulama | **AI wizard system prompt'unda onay keyword'leri geniş tut (sadece "uygula" değil "evet/tamam/devam" da). Kullanıcı onayı = aynı yanıtta flowconfig üretme zorunluluğu. Frontend'de auto-apply pattern: flag + useEffect + acceptChanges → onApply** |
| 2026-03-02 | StreamReader + JsonDocument endpoint'te using olmadan olusturuldu — resource leak. `new StreamReader(ctx.Request.Body)` ve `JsonDocument.Parse(bodyText)` IDisposable | `using (var reader = new StreamReader(...))` + `using var bodyDoc = JsonDocument.Parse(...)` | **IDisposable nesneler (StreamReader, JsonDocument, HttpResponseMessage) MUTLAKA using block icinde kullan. Endpoint handler'larda ozellikle dikkat** |
| 2026-03-02 | ExecuteScalarAsync null-forgiving `(long)(await cmd.ExecuteScalarAsync(ct))!` — PG'den boxed int64 veya int32 donebilir, null da olabilir | `var result = await cmd.ExecuteScalarAsync(ct); return result is long id ? id : Convert.ToInt64(result);` | **ExecuteScalarAsync donusunde null-forgiving KULLANMA. Boxing farkli tip dondurebilir (int32/int64). is pattern match + Convert.ToInt64 kullan** |
| 2026-02-26 | Cross-session mega-commit'te Codex context eksikligi: (1) GR-7 approval baska session'dan, Codex goremez → Q2 UNKNOWN, (2) tracking/README "scope creep" ama her commit'te guncellenir, (3) intentional auth removal Codex tarafindan CQ8 breaking change olarak flag'leniyor | Her cross-session chunk'in summary'sine "intentional, approved in session X" ekle. 2+ iter ayni false positive = FORCE PASS | **Cross-session cleanup commit'lerde Codex her degisikligin context'ini goremez. Summary'ye her degisikligin orijinal session/approval bilgisini yaz. 2 iter sonra FORCE PASS** |
| 2026-02-26 | 143KB diff icin 3-chunk service-based grouping calisti (WA 87KB, Backend 40KB, Automation+Knowledge 15KB). File-by-file chunking 30 MCP call gerektirecekti | Service bazli gruplama: ayni service'in dosyalarini tek chunk'a koy | **Codex chunking icin service-based gruplama kullan (file-by-file degil). Her grup ~50KB altinda tutmaya calis, 80KB'ye kadar sorunsuz** |
| 2026-02-24 | Codex chunked review'da ops-level tool icin "tenant_id missing" false positive — 3 iterasyon boyunca ayni hatayi veriyor | Summary'de "ops-level tool, tenant_id intentionally omitted, documented in plan intentional_exclusions" acikca belirtildi ama Codex hala fail verdi | **Ops-level (non-tenant) tool'larda Codex tenant_id false positive verecek — bu beklenen davranis. 2+ iterasyon ayni sorun = FORCE PASS** |
| 2026-02-24 | Codex "GRANT ALL required" dedi ama mevcut codebase explicit GRANT (SELECT, INSERT, UPDATE, DELETE) kullaniyor | Dispute — mevcut pattern dogru | **Codex "project rule" diye uydurabilir. Mevcut schema dosyalarini kontrol et, Codex'in iddiasiyla eslestir. Eslesmiyorsa dispute** |
| 2026-02-24 | catch(Exception) endpoint body deserialization'da Codex tarafindan reddedildi | catch(JsonException) ile degistirildi — JsonSerializer.DeserializeAsync primarily JsonException firlatir | **Minimal API endpoint'lerde request body parse icin catch(JsonException) kullan, catch(Exception) degil** |
| 2026-02-22 | JWT decode-only fallback (ReadJwtToken) imza dogrulamasi atliyor — forged JWT ile admin erisimi | Tum decode-only fallback'ler silindi, SecretKey yoksa 503 reject | **ReadJwtToken ASLA auth path'te kullanilmaz — her zaman ValidateToken (imza dogrulama) kullan. Fallback = guvenlik acigi** |
| 2026-02-22 | MockEnabled=true prod config'de kaldi — sifresiz admin token uretti | Prod'da MockEnabled=false yapildi, acil restart | **Her yeni boolean feature flag icin PROD CONFIG KONTROLU yap. Default false olsa bile prod override'i kontrol et** |
| 2026-02-22 | ChatAnalysis /api/v1/analyze authsuz — disaridan Claude API maliyeti tetiklenebilir + SSRF | X-Internal-Api-Key header + IsAllowedCallbackUrl SSRF korumasi | **Internal service endpoint = auth gereksiz DEMEK DEGIL. Firewall + kod seviyesinde cift katmanli koruma sart** |
| 2026-02-22 | Codex CQ8 FAIL verdi cunku auth eklenmesi breaking change — ama bu kasitli security fix | Q FORCE PASS: guvenlik hardening dogasi geregi breaking change'dir | **Security fix'lerde CQ8 breaking change beklenir — Codex'e context ver veya FORCE PASS kullan** |
| 2026-02-22 | Claude model ID `claude-sonnet-4-6-20250514` 404 dondu — 4.6 modellerde tarih suffix'i yok | Dogru ID: `claude-sonnet-4-6` (tarih yok). Eski modeller: `claude-sonnet-4-20250514` (tarihli) | **Claude 4.6 model ID'leri tarih suffix'i icermez (`claude-sonnet-4-6`, `claude-opus-4-6`). Sadece eski modellerde tarih var. platform.claude.com/docs/en/about-claude/models/overview kontrol et** |
| 2026-02-22 | Sunucuda NSSM servis adi `InvektoBackend` iken `Backend` ile restart denendi — servis bulunamadi | `Get-Service *invekto*` ile gercek adi bul, NSSM servisleri `Invekto` prefix'i ile kayitli | **NSSM servis adlari Windows servis adiyla AYNI olmayabilir. server-status MCP tool ile kontrol et** |
| 2026-02-22 | `claude-3-5-haiku-20241022` deprecated (Nisan 2026 emekli) ama ChatAnalysis ve WhatsAppAnalytics'te hala kullaniliyordu | Tum servislerde `claude-haiku-4-5-20251001`'e yukseltildi | **Yeni Claude modeli ciktiginda TUM servislerdeki model referanslarini tara: `grep -r 'claude-' src/` ile deprecated model tespit et** |
| 2026-02-20 | Dashboard localStorage'a fb_session yazdi ama FlowBuilder sessionStorage'dan okudu — iframe icinde paylasilmadi | FlowBuilder loadSession: once localStorage sonra sessionStorage dene | **iframe ile session paylasiminda HER ZAMAN localStorage kullan (sessionStorage browsing context'e ozel, iframe icinde paylasILMAZ)** |
| 2026-02-20 | executeWithRefresh 401'de tum token'lari sildi — INMA session'da ops endpoint 401 donunce sidebar kayboldu | isInmaSession() guard ile INMA token'lari koru, sadece ops/Basic Auth session'larinda wipe | **401 handler'da auth type kontrol et: her 401 "session gecersiz" demek DEGIL, endpoint auth mismatch de olabilir** |
| 2026-02-11 | Yeni step type (api_call) eklendi ama mevcut webhook-only kodu guncellenmedi | `step.type === 'api_call'` guard + optional chaining | **Yeni variant/type eklerken TUM mevcut erisim noktalarini tara** |
| 2026-02-11 | Multi-step senaryoda hardcoded placeholder kullanildi | `{{step_N.field}}` template + `resolveStepRefs()` | **Adimlar arasi veri aktarimi OTOMATIK olmali** |
| 2026-02-11 | Plan JSON `files_changed` unstage sonrasi senkronize edilmedi | `git diff --cached` ile esitle | **Stage/unstage sonrasi files_changed + files_count GUNCELLE** |
| 2026-02-11 | Yorum "All traffic via Backend" ama kod direct erisimi de destekliyordu | Yorum guncellendi | **Mimari yorumlar kodun gercek davranisiyla eslessin** |
| 2026-02-18 | Codex MCP'ye condensed/kisaltilmis git_diff gonderildi → false positive CQ fail (namespace eksik, Layout children yok gibi) | Full diff file okunup tam icerik git_diff'e gonderilmeli | **Codex'e ASLA condensed diff gonderme — diff_file_path fallback ancak git_diff <50 char ise calisir, inline condensed diff kullanilirsa dosyayi gormez** |
| 2026-02-18 | Chunked review'da backend sorusu (Q1/Q2/Q3) frontend chunk'ta, frontend sorusu (Q4/Q5) backend chunk'ta UNKNOWN cikiyor | Yapısal sinir — cross-chunk UNKNOWN FAIL sayılır | **HIGH risk chunked diff'te CoVe sorulari chunk bazli tasarla: her chunk sadece kendi sorusunu cevaplayacak sorular icermeli** |
| 2026-02-13 | FlowSummaryBar localStorage catch bos birakildi | console.warn eklendi | **catch yazdigi AN "Empty catch YASAK" kuralini hatirla** |
| 2026-02-13 | Error code semantic reuse: farkli failure mode ayni code | Yeni INV-AT-021 eklendi | **Her failure mode icin AYRI error code** |
| 2026-02-13 | Tenant isolation null = 403 dondu, session yok durumunu mismatch gibi handle etti | Guard `sessionTenant != null &&` | **Auth guard null = 403 verme, null = "yok" olabilir** |
| 2026-02-13 | Fire-and-forget `.catch(() => {})` bos birakildi | `.catch((err) => { console.warn(...) })` | **Fire-and-forget bile olsa catch bos birakilMAZ** |
| 2026-02-14 | Bare `catch` tip belirtmeden - tum exception'lar ayni handle | `catch(JsonException)` + `catch(Exception ex)` | **catch tip BELIRT - "Empty catch YASAK" + "Typed catch ZORUNLU"** |
| 2026-02-14 | Error fallback `healthScore = null` - UI badge gizlendi | `healthScore = 0` + tooltip | **Error fallback null BIRAKMA - degraded/default deger set et** |
| 2026-02-14 | Silent fallback path: bos cases sessizce default'a dustu | Warning log + validation warning eklendi | **TUM silent fallback/default path'lerde uyari uret** |
| 2026-02-14 | Cross-layer contract mismatch: 3 katman tutarsiz | Backend + frontend + shared contract birlikte guncellendi | **Field semantigi degistiginde 3 KATMANI BIRLIKTE guncelle** |
| 2026-02-14 | Graceful degradation return null path'lerinde log yoktu | Her path'e SystemWarn eklendi | **return null yaziyorsan NEDEN null dondugunu logla** |
| 2026-02-23 | Codex diff-only review'da mevcut ErrorCodes constant'i (KnowledgeIntentReadFailed) "undefined" olarak isaretledi — build 0 error ile gecmesine ragmen | FORCE PASS: Codex sadece diff goriyor, mevcut dosyadaki tanimlari gormuyor | **Codex CQ5/CQ8 "undefined constant" diyorsa ve build PASS ise = false positive. Build kaniti Codex'i override eder** |
| 2026-02-23 | Error code semantic reuse: KnowledgeIntentReadFailed create/update/delete icin de kullanildi | 3 yeni error code eklendi: INV-KN-033 (Create), INV-KN-034 (Update), INV-KN-035 (Delete) | **Her CRUD operasyonu icin AYRI error code. ReadFailed'i write ops icin KULLANMA** |
| 2026-02-23 | IntentPatternFullDto PascalCase serialize oldu, frontend snake_case bekledi — intent adlari UI'da bos | `[JsonPropertyName("intent_name")]` + `using System.Text.Json.Serialization` | **Yeni DTO = JSON field format HEMEN kontrol et. `System.Text.Json` != `.Serialization` — attribute icin `.Serialization` using ZORUNLU** |
| 2026-02-22 | Yeni dosya (InstanceRepository.cs) `git diff HEAD`'de gorunmuyor — Codex verify edemedi | `git add` ile staged hale getirip `git diff --cached` ile dahil et | **YENI dosyalar untracked ise diff'te GORUNMEZ — Codex review oncesi `git add` yap** |
| 2026-02-22 | Onceki session'dan kalan uncommitted degisiklikler diff'e karisiyor — Codex scope creep (CQ3) diyor | Diff scope'unu sadece ilgili dosyalarla sinirla, veya onceki degisiklikleri once commit et | **Birden fazla session uncommitted degisiklik birikmesin — her session sonunda commit/push yap** |
| 2026-02-22 | null-forgiving operator `instanceId!` Codex CQ5 fail ettirdi — guard oncesinde kontrol edilmis olsa bile | Guard sonrasi yeni non-nullable local variable olustur (`var resolved = instanceId ?? ""`) | **null-forgiving `!` operator KULLANMA — guard ile garantilenmis bile olsa Codex fail eder** |
| 2026-02-22 | GetFlowAsync return tuple'a yeni eleman eklenince v1 FlowEngine.cs derleme hatasi verdi (CS8132) | Tum consumer'lara `_` discard eklendi | **Tuple return type degistirince TUM consumer'lari tara: `grep -r 'GetFlowAsync' src/` — v1 engine unutulmamali** |
| 2026-02-22 | Codex V3 UNKNOWN verdi cunku method signature diff'te yoktu — sadece call-site degisikligi gordu | Build PASS tuple arity'yi kanitlar — context eksikligi false positive | **Codex partial diff'te method signature goremezse UNKNOWN verir — build PASS kanit olarak yeterlidir, FORCE PASS kullan** |
| 2026-02-14 | Codex yanlis plan icin review yapti | Q'ya uyari verildi | **Review prompt'unda PLAN ADI + SLUG acikca belirt** |
| 2026-02-14 | allowed_files + files_count tutarsiz - 3 iter CQ3 FAIL | allowed_files'a eklendi | **`/rev` oncesi: git diff ciktisini allowed_files + files_changed + files_count ile BIREBIR karsilastir** |
| 2026-02-15 | allowed_files diff'teki tum dosyalari icermiyordu - CQ3 scope violation | Eksik dosyalar eklendi | **`/rev` oncesi: allowed_files BIREBIR karsilastir** (3. tekrar) |
| 2026-02-15 | Recovery claim query aktif pipeline kayitlarini da yakaladi | Stale timeout eklendi | **Claim query'lerinde stale timeout (updated_at check) ekle** |
| 2026-02-15 | FOR UPDATE SKIP LOCKED transaction-scoped - uzun islemlerde yetersiz | Stale timeout + progress update | **FOR UPDATE SKIP LOCKED uzun islemlerde kullanilAMAZ** |
| 2026-02-15 | 4 iter ayni Q3 farkli acilardan FAIL | Terminal state + stale timeout + heartbeat uc'u birlikte | **Multi-instance recovery 3 AYAK gerektirir** |
| 2026-02-15 | Plan JSON schema okunmadan olusturuldu - 6+ alan hatasi | Schema okunup yeniden yazildi | **Plan JSON ONCE plan-schema.json'i OKU - tahmin YASAK** |
| 2026-02-15 | "Batch" aslinda N+1'di - per-row UPDATE transaction icinde | Multi-row UPDATE FROM VALUES | **"Batch" = tek SQL birden fazla satir, dongu icinde tek UPDATE DEGIL** |
| 2026-02-15 | Diff dosyasi kendini iceriyor - files_count tutarsiz | `':!arch/plans/diffs/*'` exclude | **Diff stats'ta self-referencing dosyayi EXCLUDE et** |
| 2026-02-16 | CountConfirmedForSlotAsync + MarkReminderSentAsync tenant_id WHERE clause eksik | tenant_id parametre + WHERE clause eklendi | **Her repository query'sinde tenant_id WHERE clause ZORUNLU - mevcut pattern'den kopyalarken bile kontrol et** |
| 2026-02-16 | TaskCanceledException catch'te sadece `break` vardi, log yoktu | SystemWarn + appointment ID + progress count eklendi | **catch icinde break/continue/return yaziyorsan NEDEN'i logla** |
| 2026-02-16 | Codex CQ3 false positive: diff context satirlarini yeni ekleme zannetti | Q FORCE PASS (2 iter sonra) | **Codex diff context satiri (boslukla baslar) ile ekleme (+) satiri karistirabilir - false positive olarak belgele** |
| 2026-02-16 | Codex CQ4 duplicate: her repo'daki GetTenantHealthInfoAsync'i DRY violation zannetti | Q FORCE PASS - mimari karar (mikro-servis izolasyonu) | **Mikro-servis izolasyonu = bilinçli duplikasyon. Codex'e architectural decision olarak belgele, 2. iter'de Q escalation** |
| 2026-02-16 | ErrorCodes constant adi yanlis kullanildi (KnowledgePhotoBlockedHealth vs KnowledgePhotoBlockedHealthTenant) | ErrorCodes.cs'ten dogrusu kontrol edildi | **ErrorCodes constant kullanirken ONCE ErrorCodes.cs'teki tam adi kontrol et** |
| 2026-02-17 | PKT-4 Codex 7 iter: her iter yeni typed catch/IDisposable/silent-catch buldu | Her iter fixlendi, son iter PASS | **Codex iter sayisi yuksek = pre-write 5 soru kontrolu yetersiz yapilmis demek. static metod logger erisimsiz = silent catch riski** |
| 2026-03-03 | Codex CQ2/CQ5 contradiction loop: CQ2 "Task.Run silent failure" FAIL → catch(Exception) eklendi → CQ5 "bare catch forbidden" FAIL. Inner methods zaten catch(NpgsqlException) ile korunuyor ama Codex outer Task.Run'i gormuyor | Revert to `_ = Task.Run(...)` (inner typed catch yeterli). Q FORCE PASS | **Task.Run fire-and-forget'te inner method catch(NpgsqlException) varsa outer catch(Exception) EKLEME — Codex CQ2/CQ5 dongu yaratir. 2 iter ayni contradiction = FORCE PASS** |
| 2026-03-03 | JsonDocument.Parse().RootElement kullanimi IDisposable leak — endpoint'te JsonDocument dispose olunca RootElement invalid | `using var doc = JsonDocument.Parse(...)` + `doc.RootElement.Clone()` ile bagimsiz kopya | **JsonDocument.RootElement'i dispose sonrasi kullanamazsin. Clone() ile kopyala, sonra using ile JsonDocument'i kapat** |
| 2026-02-17 | Large diff (122KB+) git_diff inline truncation = ALL UNKNOWN verdict | diff_file_path fallback kullanildi | **Diff >50KB ise git_diff bos birak + diff_file_path ile disk'ten oku** |
| 2026-02-17 | Static method logging yapamaz = silent failure (ClassifyByKeyword) | static -> instance method degistirip _logger eristirme | **Exception catch + return null/continue olan metodlar INSTANCE olsun (logger erisimi icin)** |
| 2026-02-17 | Claude batch processing catch(Exception) cok genis | ArgumentOutOfRangeException + InvalidOperationException typed catches | **Batch mapping/processing = ArgumentOutOfRange + InvalidOperation yeterli, Exception gereksiz genis** |
| 2026-02-17 | 345KB diff Codex context window asti - MODEL_ERROR | Diff'i servis bazli split (Part1 137KB + Part2 209KB) | **Diff >200KB ise servis bazli split review yap, full diff gonderme** |
| 2026-02-17 | Secret scan hook `password\s*[:=]` appsettings.json'da yakaladi | Password/SecretKey field'larini dev template'den cikar | **Dev appsettings.json'da password field BIRAKMA - Production.json'da olsun** |
| 2026-02-17 | Codex CQ1 `!` reddetti sonra `??` fallback'i de reddetti (dongusal) | Q FORCE PASS (2 iter sonra) | **Codex dongusal cikis yolu yoksa 2. iter'de Q FORCE PASS escalation** |
| 2026-02-17 | NpgsqlBatch per-message audit loop yerine batch insert | BatchInsertAuditTrailAsync tek batch | **N+1 insert dongusu yerine NpgsqlBatch kullan - ozellikle audit trail gibi bulk insert'lerde** |
| 2026-02-17 | SQL string concatenation (5 method) Codex CQ5 FAIL | const string + COALESCE/conditional WHERE refactor | **Dinamik WHERE/SET icin string concat YASAK - `@param IS NULL OR col = @param` veya COALESCE pattern kullan** |
| 2026-02-17 | DateOnly.Parse query param'da try-catch yok - unhandled 500 | FormatException catch + 400 response | **User input parse (DateOnly, int, etc.) MUTLAKA TryParse/try-catch ile dogrula** |
| 2026-02-17 | Fire-and-forget `_ = service.MethodAsync()` exception kayboldu | `.ContinueWith(OnlyOnFaulted)` + SystemWarn | **Fire-and-forget Task = ContinueWith(OnlyOnFaulted) ile LOGLA** |
| 2026-02-17 | HttpRequestMessage/HttpResponseMessage dispose edilmedi | `using var` eklendi | **HTTP message nesneleri MUTLAKA using ile sarmalani** |
| 2026-02-17 | Chunked Codex review CoVe UNKNOWN - logic baska chunk'ta | Chunk'lar arasi verdict birlestirildi | **Chunked review'da CoVe UNKNOWN = baska chunk'ta PASS olabilir, cross-reference kontrol et** |
| 2026-02-17 | Codex 8 iter scheduler cross-tenant query'ye tenant_id parametresi istiyor | Q FORCE PASS (scheduler by design) | **Scheduler (IHostedService) query'leri cross-tenant = verification question'da ACIKCA belirt, 3 iter sonra Q FORCE PASS** |
| 2026-02-17 | Generic "Database error" user-facing mesajlar CQ1 FAIL | Operation-specific mesajlar yazildi | **User-facing error mesajlari operasyon adini icermeli: "Lifecycle start failed" > "Database error"** |
| 2026-02-17 | HandleLastStepAsync escalation logic bug: response durumuna bakmadan escalation gonderiyor | Combined condition: `!IsNullOrEmpty(target) && !responded` | **Codex GERCEK logic bug bulabilir - PASS beklentisi ile ilerlemek yerine her FAIL'i ciddi al** |
| 2026-02-17 | Empty cycle (idle timer tick) log'lanmayinca Q5 FAIL | SystemInfo idle cycle log eklendi | **Timer-based IHostedService'te idle tick'ler bile loglanmali (log volume kabul edilebilir)** |
| 2026-02-17 | Backend Program.cs logger adi `jsonLog` ama `jsonLogger` yazildi | Endpoint imzasindaki parametre adi kontrol edildi | **Logger kullanirken ONCE endpoint method signature'daki parametre adini kontrol et** |
| 2026-02-17 | 6 lead endpoint'te NpgsqlException catch eksik - Codex CQ1 FAIL | Her endpoint'e NpgsqlException catch eklendi | **DB cagiran TUM endpoint'ler (sadece repo degil) NpgsqlException catch ZORUNLU** |
| 2026-02-17 | GetPendingFollowUpsAsync tenant_id parametresi yoktu | `int tenantId` + WHERE tenant_id = @tid eklendi | **Scheduled/helper query'ler de tenant_id filtresi ZORUNLU - "internal" olsa bile** |
| 2026-02-17 | Chunked review: 5 chunk, tum CQ PASS ama CoVe UNKNOWN = Q FORCE PASS | Chunking artifact olarak belgelendi | **Tum CQ1-CQ8 PASS + CoVe sadece UNKNOWN = FORCE PASS uygun (real fail yok)** |
| 2026-02-17 | GET endpoint'lere NpgsqlException log eklenince jsonLog scope'ta yok - CS0103 | `JsonLinesLogger jsonLog` DI parametresi eklendi | **NpgsqlException catch'e log eklerken endpoint signature'da jsonLog VAR MI kontrol et** |
| 2026-02-17 | Backend MapPost("/redeem") ama Marketing PUT endpoint bekliyor - CQ8 FAIL | MapPost -> MapPut + ProxyPost -> ProxyPut degistirildi | **Proxy route HTTP method'u downstream servisin method'uyla BIREBIR eslessin** |
| 2026-02-17 | Codex diff'te untracked dosyalar yok - tum CQ UNKNOWN | `git add` ile stage ettikten sonra diff olusturuldu | **Yeni dosyalar iceren PKT'de /rev oncesi `git add` ile stage et, yoksa diff bos gelir** |
| 2026-02-18 | GenerateResponseAsync'te catch(Exception ex) + ParseResponse'ta catch(Exception ex) — 2 iter gerekti | iter 0: GenerateResponseAsync fix, iter 1: ParseResponse fix | **Typed catch fix yaparken TUM metotlari tara, sadece ilk bulunanı degil — ayni dosyadaki diger generic catch'leri de kontrol et** |
| 2026-02-18 | ParseResponse'ta sadece JsonDocument.Parse + TryGetProperty var, generic catch gereksiz | Generic catch kaldirildi, sadece JsonException kaldi | **catch(Exception) eklemeden ONCE: metotta hangi exception tipleri mumkun? JsonDocument = JsonException, string ops = exception atmaz → generic catch GEREKSIZ** |

### UI / Dashboard

| Date | Mistake | Solution | Prevention |
|------|---------|----------|------------|
| 2026-03-05 | Dashboard sidebar navy renk paleti (navy-300/#8898AA, navy-400/#6B7C93) v3 concept'in slate paletinden (#64748b, #94a3b8) gözle görülür şekilde farklı — Q farkı yakaladı | Layout.tsx'te tüm `text-navy-*` ve `hover:text-navy-*` referanslarını `text-slate-*` eşdeğerleriyle değiştir. `nav-item-active` CSS class'ı teal-50/teal-600 kullan | **Renk paleti değişikliğinde sadece CSS class eklemek yetmez — component'teki inline Tailwind class'larını da güncelle. navy vs slate farklı paletler: navy-300=#8898AA, slate-400=#94a3b8** |
| 2026-03-05 | SSH SFTP paralel upload: 5-6+ eşzamanlı channel açınca "Channel open failure" hatası | Dosyaları sırayla (sequential) upload et, paralel batch yapma | **SFTP upload'larda max 3-4 paralel bağlantı. 5+ dosya varsa sequential upload kullan** |
| 2026-03-05 | Dashboard `npx tsc` pre-existing hatalar yüzünden başarısız — ama `npx vite build` sorunsuz çalışıyor | Sadece UI/CSS değişikliklerinde `npx vite build` kullan (tsc skip) | **Dashboard'da pre-existing TS hataları var (FlowBuilder, OnboardingGuide, etc.). Sadece UI tweaks için `npx vite build` yeterli, full `tsc` şart değil** |
| 2026-03-05 | Q "menü formatı" deyince v3 concept'in menü adlarını kopyaladım — Q'nun istediği sadece FORMAT (section headers, spacing, colors), menü İTEMLARI mevcut INSE'den gelecekti | Format = visual structure (section headers, spacing, active/hover colors, font size). Items = mevcut uygulamanın NavItem listesi | **"Format" ve "içerik" ayrımını net yap. Q "format buradan" diyorsa sadece visual styling al, menü isimlerini/yapısını mevcut uygulamadan koru** |
| 2026-02-21 | `response!.IsSuccessStatusCode` null-forgiving - yield return icinde try-catch kullanilamayinca sendError flag pattern'i response null olabilir durumunu kacirir | `response == null \|\| !response.IsSuccessStatusCode` null check + ternary errorBody | **yield return icinde null-forgiving KULLANMA — flag pattern'de null check AYRICA yap** |
| 2026-02-21 | `new HttpClient()` constructor'da — IHttpClientFactory DI pattern'i ihlali | `AddHttpClient<T>()` + constructor injection | **HttpClient her zaman DI uzerinden al, ASLA `new HttpClient()` yapma** |
| 2026-02-21 | Static lokal fonksiyon (ParseFlowSummaries) disaridaki `app.Logger`'a erisemiyor - CS8421 | ILogger parametresi ekle + caller'da `app.Logger` gonder | **Static local function'da logger gerekiyorsa parametreyle gonder** |
| 2026-02-21 | 6 chunk'li review'da 3 chunk PASS 3 chunk false positive — cross-chunk UNKNOWN + scope complaints | Q FORCE PASS (iter 2) | **6+ chunk'li review'da cross-chunk UNKNOWN kacinilmaz. 3 iter sonra real fix yoksa FORCE PASS escalation** |
| 2026-02-21 | SQL string interpolation (SET clause dynamic build) Codex CQ5 FAIL | COALESCE(@param, existing_column) + DBNull.Value | **Dinamik SET clause icin COALESCE pattern kullan — string interpolation YASAK (2. kez)** |
| 2026-02-21 | GetHistory static method logger'a erisemiyor - silent catch | Class'a ILogger ekle, static -> instance method | **Exception catch olan static method = silent failure riski. ILogger DI icin instance method yap (2. kez)** |
| 2026-02-18 | Chunked review CoVe UNKNOWN items cross-file verification (Q4 auth, Q5 CHECK constraints) | Manual verification: proxy auth = downstream JWT, CHECK = validation arrays 1:1 | **Chunked review UNKNOWN = manual cross-file verification yap, sonucu plan JSON verdict note'una yaz** |
| 2026-02-20 | Codex CQ5: FlowValidator validation mesajlari INV error code yok diye FAIL verdi — mevcut pattern zaten kullanmiyor | Q FORCE PASS — pre-existing pattern, yeni kod tutarli | **Codex pre-existing pattern'i yeni kodda FAIL verebilir — mevcut pattern degismediyse false positive** |
| 2026-02-20 | Chunked review her 2 iteration'da ayni Q2/Q4 UNKNOWN — AiIntentHandler ve FlowValidator karsi chunk'ta | CoVe sorularini chunk bazli tasarladik ama cross-chunk artifact devam etti | **Ayni UNKNOWN 2 iter tekrar ediyorsa Q FORCE PASS — chunking limitation, fix mumkun degil** |
| 2026-02-20 | Codex CQ5: IHostedService cross-tenant query + no-auth webhook 3 iter FAIL — her ikisi de Q'nun interview'da explicit karari | Q FORCE PASS (iter 2) | **Q interview kararlari (no auth, cross-tenant scheduler) Codex'e architectural decision olarak belirtilse bile CQ5 tekrar edebilir — 3 iter'de Q FORCE PASS** |
| 2026-02-20 | CQ2: secondary EndSessionAsync catch bloklari `/* swallow */` loglama yok — iter 1'de yakalandi | Tum 6 catch blokuna SystemWarn/StepWarn + session ID + ex.Message eklendi | **Cleanup catch bloklari bile loglamali — `/* swallow */` ASLA yazma, en az SystemWarn** |
| 2026-02-20 | Layout.tsx opsOnly filter tenant_id=0 superadmin'i de gizliyordu — quicklogin session varsa opsOnly sayfalar gorunmuyordu | `session && session.tenantId !== 0` guard eklendi | **opsOnly filter'da tenant_id=0 (superadmin) icin bypass ekle — session var ama tenant_id=0 = superadmin** |
| 2026-02-20 | INMA SSO URL token flow'da raw INMA JWT `fb_session`'a yazildi — FlowBuilder backend INSE JwtValidator ile dogruladigindan 401 dondu | Dashboard'da `exchangeInmaToken()` metodu: INMA JWT → exchange endpoint → INSE JWT → `fb_session` guncelleme | **Farkli signing key'li JWT'leri localStorage'da paylasirken DAIMA token exchange yap — decode-only != validated** |
| 2026-02-20 | Exchange endpoint `InmaAuth:SecretKey` olmadan 503 dondu — production config'de key yoktu | Decode-only fallback: signature skip, claim'leri okuyup INSE JWT uret | **Config-dependent endpoint'lerde graceful degradation ekle — hard 503 yerine decode-only fallback, yoksa frontend calismaz** |
| 2026-02-20 | WapCRM chatoperation API "Invalid Request Model" — `Incom=4` ile `userKey` (email) zorunlu ama bos string gonderildi | `Incom`+`userKey` yerine `userID` (integer) kullan — Incom gonderme, sadece userID yeterli | **WapCRM API: Incom=4 → userKey ZORUNLU. Incom gonderme + userID kullan = daha basit, daha guvenli** |
| 2026-02-20 | INMA JWT `CompanyId` claim = INMA internal ID (11), `CompanyCode` claim = bizim tenant_id (5050) — Dashboard `CompanyId` kullaniyordu, Flow Builder bos geldi | 3 yerde CompanyId → CompanyCode degistirildi (Backend exchange endpoint, Dashboard getSession, exchangeInmaToken) | **INMA JWT claim mapping: CompanyId ≠ tenant_id! CompanyCode = bizim tenant_id. Webhook URL `?companyId=` parametresi de CompanyCode'a karsilik gelir** |
| 2026-02-20 | WapCRM `/api/users` endpoint'inden user listesi cekilebilir (`X-CIB-SecretKey` header ile) — BotUser (id=91) ACCESS DENIED, gercek user (id=12) calisir | Sadece gercek WapCRM user'i (Q'nun kendi hesabi) ile mesaj gonderilebilir | **WapCRM userID: BotUser/service account ACCESS DENIED olabilir — gercek user ID ile test et** |
| 2026-02-20 | PostAsJsonAsync `JsonSerializerDefaults.Web` kullanir = camelCase naming policy uygular — `Incom` → `incom`, `InstanceID` → `instanceID` olur | Dictionary + manual JsonSerializer.Serialize (no naming policy) + PostAsync kullan | **PostAsJsonAsync 3rd party API'ye gonderirken camelCase donusumu yapar — exact property name gerekiyorsa Dictionary + manual serialize kullan** |
| 2026-02-20 | Automation webhook endpoint WapCRM formatinda `messages` array + top-level `InstanceID` bekliyor — kendi DTO formatimiz (event_type/data) farkli | WapCRM formatinda `{messages:[{id,body,type,chatId,senderName,...}], InstanceID: "..."}` gonder | **Automation webhook = WapCRM raw format. Test icin WapCRM formatinda gonder (body=mesaj, chatId=telefon@c.us)** |
| 2026-02-21 | MainAppCallbackClient TimeoutMs=5000ms < WapCRM API latency (3-9s) — linked CTS OperationCanceledException general catch'e dustu, retry yaptı, duplicate mesaj | Dedicated `catch (OperationCanceledException)` eklendi (non-app-shutdown): `return true` (request sent, delivered say) | **Linked CTS timeout = HTTP request ZATEN gonderilmis — retry = duplicate. Timeout exception'i app-shutdown'dan AYRI yakala ve delivered say** |
| 2026-02-21 | Yeni session'da `__last_input` set edilmiyor — flow auto-chain trigger→welcome→ai_intent(WaitForInput) ilk mesaji kaybediyor | Orchestrator'da `state.Variables["__last_input"] = messageText` + AiIntentHandler first-visit __last_input check | **Yeni session = kullanicinin ilk mesaji state'e YAZILMALI, yoksa auto-chain sirasinda kaybolur** |
| 2026-02-21 | Dashboard analytics endpoint 401 — `inmaJwtValidator` production'da NULL (SecretKey yok), INSE validator INMA token dogrulayamaz | Exchange endpoint gibi decode-only fallback eklendi: `ReadJwtToken` + CompanyCode claim | **INMA token kullanan HER endpoint'e decode-only fallback ekle — sadece exchange degil, tum INMA-accessible endpoint'ler** |
| 2026-02-21 | Endpoint 500 "relation daily_metrics does not exist" — feature deploy edildi ama DB tablosu olusturulmadi | Production'da `backend-metrics.sql` calistirildi | **Feature deploy = DB migration BIRLIKTE. Endpoint kodu deploy etmeden ONCE gerekli tablolarin varligini dogrula** |
| 2026-02-21 | Flow Builder nodes (0,0) uzerinde yigildi — DB'deki flow_config node'larinda position alani yok | `needsAutoLayout()` + BFS `autoLayoutNodes()` eklendi | **DB'den gelen node verisi position icermeyebilir — loadFlow'da auto-layout fallback ZORUNLU** |
| 2026-02-21 | Intent node `n.map is not a function` — intents DB'de JSON string olarak sakli, array degil | Defensive parsing: `typeof === 'string' ? JSON.parse : direct` | **DB'den gelen JSONB icindeki nested array'ler string olabilir — HER ZAMAN type check + parse fallback** |
| 2026-02-22 | Codex CQ1/CQ5: Frontend store'da INV-XX-NNN error code yok diye FAIL — mevcut wizard-store.ts ayni pattern kullanıyor (user-friendly mesaj, error code yok) | Q FORCE PASS (iter 1) — false positive | **Frontend Zustand store'larda user-facing error mesajlari INV error code GEREKTIRMEZ — error code backend ErrorResponse pattern'i, frontend icin false positive** |
| 2026-02-22 | Mevcut wizard SSE altyapisi edit-mode icin yeniden kullanildi — yeni endpoint GEREKMEDI, sadece opsiyonel `flow_config` body parametresi eklendi | streamMessage'a optional param, backend'de BuildSystemPrompt dallanmasi | **Yeni feature icin ONCE mevcut altyapiyi incele — wizard endpoint zaten wizard_status kontrolu YAPMIYOR, her flow icin calisiyor** |
| 2026-02-22 | Codex FAIL: staged diff pre-existing uncommitted degisiklikleri icerdi (InstanceRepository, ExtractTenantFromBearer, WapCRM) — yeni working hours kodu temiz ama eski kod CQ2/CQ3/CQ5 FAIL | InstanceRepository unstage, Q FORCE PASS | **Buyuk dosyalarda (Program.cs) onceki session'dan kalan uncommitted degisiklikler diff'e dahil olur — /rev oncesi `git diff --cached` inceleyip sadece ilgili degisikliklerin staged oldugunu dogrula, gerekirse selective unstage yap** |
| 2026-02-22 | Program.cs'e yeni endpoint eklerken build `NpgsqlException not found` hatasi verdi — using Npgsql; eksikti, onceki session'da eklenen kod NpgsqlException kullaniyor ama using yoktu | `using Npgsql;` eklendi | **Yeni kod eklerken derleme hatalari TUM dosyanin using'lerinden kaynaklanabilir — onceki session'dan kalan eksik using'leri kontrol et** |
| 2026-02-22 | SPA merger: background agent FlowBuilder import prefix'lerini doubled (FbFb prefix) | Rename sonrasi tsc ile dogrulandi, manual fix | **Background agent ile toplu import fix yaparken prefix duplication riski — tsc --noEmit ile derhal dogrula** |
| 2026-02-22 | Codex V1 UNKNOWN: fb_session kalintisi var mi sorusuna diff disini goremedi | grep ile fb_session sifir referans kanitlandi, Q FORCE PASS | **Codex partial diff'te silinen kodun referanslarini dogrulayamaz — repo-wide grep ile kanit sun, FORCE PASS** |
| 2026-02-22 | SPA merger'da SettingsPage.tsx'e alakasiz margin degisikligi (ml-3) karisti — Codex CQ3 scope creep | Revert edildi | **Buyuk merge'lerde onceki session'dan kalan unstaged degisiklikler karismaz mi `git diff` ile kontrol et** |
| 2026-02-23 | 195KB diff 5 chunk'a bolundu, chunk 3+4 her iterasyonda ayni wa_* tenant_id false positive tekrarladi (4 kez) | Her iterasyonda false positive dokumante edildi, chunk 1/2/5 ilk iterasyonda PASS | **Cross-tenant analysis tablolari (wa_faq_clusters, wa_intents) Codex'te tenant_id false positive uretir — verification question'a "wa_* tables are cross-tenant by design" notu ekle** |
| 2026-02-23 | AdoptAsync catch(Exception) fix edildi ama ayni dosyadaki OnboardAsync'teki catch(Exception) gozden kacti — iter 2'de yakalandi | Iter 2'de fix edildi (NpgsqlException + HttpRequestException + JsonException) | **catch(Exception) fix yaparken TUM dosyayi tara — ayni dosyadaki diger generic catch'leri de kontrol et (2. kez, daha once ExtractorService icin de olmustu)** |
| 2026-02-23 | static helper metotlar (ExtractQuestion, ParseJsonArray) catch(JsonException) { return null } — silent failure, Codex CQ2 FAIL | static -> instance method + _logger.SystemWarn eklendi | **JSON parse eden private helper = silent failure riski. Instance method yap + INV error code ile logla (3. kez ayni pattern)** |
| 2026-02-23 | `new HttpClient` singleton service icinde — IHttpClientFactory pattern ihlali, Codex CQ6 FAIL | `AddHttpClient<TemplateAdoptionService>()` + constructor injection | **HttpClient DI kurali: ASLA `new HttpClient()` — her zaman `AddHttpClient<T>()` veya IHttpClientFactory (2. kez)** |
| 2026-02-23 | Baska session'dan yanlislikla commit edilmis kod (ffa938a) icin retrospektif 5-chunk Codex review yapildi — 4 iter, 14 gercek fix | Tum fix'ler uygulanip ayri commit atildi (dfd6a4b) | **Codex review OLMADAN commit atilmissa retrospektif review ZORUNLU — accidental commit = kalite borcu, sonradan fixle** |
| 2026-02-23 | React Router nested: main.tsx `<BrowserRouter>` + App.tsx `<Router>` = "You cannot render a Router inside another Router" crash | App.tsx'ten `<Router>` wrapper silindi, tek BrowserRouter main.tsx'te | **React Router: BrowserRouter TEK bir yerde olmali (main.tsx). App.tsx'e Router EKLEME** |
| 2026-02-23 | Lesson #109 sadece `intents` icin fix yapilmisti ama ayni sorun `options` (message_menu) ve `cases` (logic_switch) icin de vardi — flow editor acilirken crash | loadFlow'a TUM array field'lari normalize eden fonksiyon eklendi + DB migration | **Double-JSON fix yaparken TUM ayni patterndeki field'lari tara — tek field fix'i yetmez, ARRAY_FIELDS listesi olarak merkezi tut (3. kez ayni pattern)** |
| 2026-02-23 | `api.request()` her zaman `response.json()` cagiriyor ama welcome endpoint plain text donuyor — SyntaxError | getWelcome'da text() + try JSON.parse fallback | **API client'ta her endpoint JSON donecek varsaymasi YANLIS — harici servislere proxy yapan endpoint'ler plain text donebilir, response Content-Type kontrol et** |
| 2026-02-23 | `URLSearchParams.get('accesstoken')` case-sensitive — INMA `accessToken` (camelCase) gonderdiyse null donuyor | `params.get('accesstoken') \|\| params.get('accessToken')` her iki casing | **URLSearchParams case-sensitive! Dis sistem'den gelen param isimleri icin HER ZAMAN birden fazla casing dene** |
| 2026-02-23 | `Results.Redirect("/app/")` query string'i KORUMAZ — `/?accesstoken=...` → `/app/` (token kaybolur) | `Results.Redirect($"/app/{ctx.Request.QueryString}")` | **ASP.NET Redirect query string korumaz — SSO token flow gibi query-dependent redirect'lerde `ctx.Request.QueryString` ekle** |
| 2026-02-23 | `loginWithInma()` raw INMA JWT store ediyor ama `exchangeInmaToken()` CAGIRMIYOR — sadece URL SSO path exchange yapiyor | loginWithInma'ya `await api.exchangeInmaToken()` eklendi | **Her INMA login path'inde exchange ZORUNLU: URL SSO, credentials login, refresh sonrasi. Bir path'te varsa TUMU'nde olmali** |
| 2026-02-23 | `doRefresh()` INMA refresh token'i store ediyor → INSE JWT raw INMA JWT ile overwrite — sonraki API call'lar 401 | doRefresh icine `await this.exchangeInmaToken()` eklendi | **Token refresh = token TYPE degisebilir. Refresh sonrasi token tipini kontrol et ve gerekirse re-exchange yap** |
| 2026-02-23 | Mevcut localStorage'daki eski raw INMA JWT icin hicbir exchange calismadi — useAuth mount'ta exchange yapmiyordu | useEffect ile mount'ta `exchangeInmaToken()` (no-op if already INSE) | **Deploy sonrasi mevcut session'lar eski token formatinda kalir — mount-time token migration/exchange SART** |
| 2026-02-27 | BenchmarkProcessingService in-memory ConcurrentQueue — servis restart sonrasi pending DB job'lar kayboldu, benchmark #26-28 "pending" kaldi | Cancel old, restart service, create new benchmarks via API (which enqueues) | **In-memory queue kullanan BackgroundService restart edilince queue bos gelir — DB'den auto-pickup mekanizmasi yok. Pending job'lari cancel edip API uzerinden yeniden olustur** |
| 2026-02-27 | `/api/ops/benchmark/23/status` 404 — endpoint path'te `/status` suffix yok | Dogru endpoint: `/api/ops/benchmark/{id}` (Program.cs:792) | **Benchmark API endpoint'leri: GET `/{id}` (status), GET `/{id}/results`, GET `/{id}/metrics`, PUT `/{id}/ground-truth`. Status icin ayri suffix YOK** |
| 2026-02-27 | PowerShell `curl` alias → `Invoke-WebRequest` calisir ama output capture farkli. `curl.exe` output direkt, Invoke-WebRequest `.Content` property'den okunmali | `Invoke-WebRequest -UseBasicParsing` + `$r.Content` | **Sunucuda API call yaparken `Invoke-WebRequest -UseBasicParsing` kullan, `$r.Content` ile body al. `curl.exe` output capture sorunlu olabilir** |
| 2026-02-28 | NightlyBatch RunHour=2 sunucu LOCAL time kullanıyor — UTC+3 sunucuda 02:00 TR = 23:00 UTC'de çalıştı. NSSM servis adı `InvektoWhatsAppAnalytics` (not `WhatsAppAnalytics`) | `Restart-Service InvektoWhatsAppAnalytics -Force` kullanıldı, `nssm restart WhatsAppAnalytics` FAIL oldu | **NightlyBatchJob RunHour = sunucu LOCAL time (DateTime.Today kullanır). NSSM Windows service adı = `Invekto{ServiceName}` prefix'li (Get-Service ile doğrula)** |
| 2026-02-23 | PKT-2 Codex iter 0: Knowledge endpoint `.RequireAuthorization()` eksik — Codex CQ5 yakaladi | `.RequireAuthorization()` eklendi | **Yeni endpoint = `.RequireAuthorization()` ZORUNLU kontrol et. app.MapGet sonuna eklemeyi UNUTMA** |
| 2026-02-23 | `.RequireAuthorization()` ASP.NET built-in auth middleware gerektiriyor ama Backend custom JWT middleware (TenantContext) kullaniyor — 500 crash | `.RequireAuthorization()` kaldirildi, custom JWT middleware yeterli | **Backend'de `.RequireAuthorization()` KULLANMA — custom JwtAuthMiddleware + TenantContext pattern kullaniyoruz. RequireAuthorization = UseAuthorization middleware gerektirir, bizde yok** |
| 2026-02-23 | api.ts degisiklikleri onceki session'dan kayboldu — Edit tool ile eklenmis ama dosya disk'e yazilmamis | api.ts yeniden okunup degisiklikler tekrar eklendi | **Session devami oncesi `git status` ile tum beklenen degisikliklerin GERCEKTEN disk'te oldugunu dogrula — Edit tool basarili dese de VSCode buffer/disk uyumsuzlugu olabilir** |
| 2026-02-23 | PKT-2 Codex iter 1: `knStats!.TemplateAdoptionCount` null-forgiving operator CQ5 FAIL | `(knStats?.TemplateAdoptionCount ?? 0)` pattern'e donusturuldu | **null-forgiving `!.` KULLANMA (4. kez) — her zaman `?.` + `??` ile null-safe eris** |
| 2026-02-23 | PKT-2 Codex iter 1: StepWarn mesajlarinda `[INV-BE-050]` error code prefix yoktu — CQ1 FAIL | Tum StepWarn mesajlarina `[{ErrorCodes.BackendOnboardingStatusFailed}]` prefix eklendi | **Log mesajlarinda (StepWarn/StepError) DAIMA `[INV-XX-NNN]` error code prefix kullan** |
| 2026-02-23 | PKT-2 Codex iter 2: User-facing "Failed to compute onboarding status" generic — CQ5 FAIL | DB hatasi → "veritabani hatasi", HTTP hatasi → "servis baglanti hatasi" olarak ayrildi | **User-facing error mesajlari hata TIPINI icermeli (3. kez): DB vs HTTP vs timeout AYRI mesaj** |
| 2026-02-23 | PKT-2 chunked review'da Q4 (error codes) her chunk'ta UNKNOWN — ErrorCodes.cs chunk 1'de, kullanim chunk 2'de | Cross-chunk merged verdict: complementary UNKNOWN = PASS | **Chunked review'da ayni verification sorusu 2 chunk'ta UNKNOWN ise: chunk'lar birbirini TAMAMLIYOR olabilir — cross-check yap, merged PASS ver** |
| 2026-02-23 | MockIntentDetector sadece 5 generic intent biliyordu — sector-specific keyword'ler (dis, doktor, yemek) eslesmedi | Synonym dictionary + ExpandIntentKeywords: intent adini `_` ile split edip her parca icin synonym'leri ekle | **Keyword-based mock detector'a sector vocabulary ekle — custom intent isimleri dogrudan keyword olarak yetmez, synonym expansion ZORUNLU** |
| 2026-02-23 | Switch node 5+ output handle bottom'da overlapping — label'lar ust uste biniyor | BaseNode'a `outputPosition="right"` prop eklendi, right-side vertical output rendering | **5+ output handle olan node'larda bottom positioning overlap yapar — right-side positioning kullan** |
| 2026-02-23 | ai_intent sonrasi ai_faq node'u `__last_input`'u reuse ederek yeni input beklemeden sonuc dondu | utility_set_variable ile `__last_input` temizlendi (bos string set) — FAQ handler `!IsNullOrWhiteSpace(prev)` check'i ile yeni input bekler | **Flow'da node arasi `__last_input` tasiniyor — WaitForInput oncesi stale degeri temizle (utility_set_variable)** |
| 2026-02-23 | PowerShell `Set-Content -Encoding UTF8` dosyaya BOM (EF BB BF) ekliyor — git diff'te ilk satir degismis gorunuyor | `[System.IO.File]::WriteAllBytes()` ile BOM-free yazma veya `[byte[]]$bytes[3..($bytes.Length-1)]` ile BOM strip | **PowerShell ile dosya yazarken ASLA `Set-Content -Encoding UTF8` kullanma — BOM ekler. `[IO.File]::WriteAllBytes()` veya bash redirection (`>`) kullan** |
| 2026-02-23 | Codex CQ3 "scope creep" dedi flow mismatch detection icin (planda yok). Fix commit'te CQ8 "behavior change" dedi (silmek de sorun). Dongusal FAIL | Minimal guard: complex session reset yerine sadece `state.CurrentNodeId` reset (1 satir logic, repo call yok) | **Codex CQ3 vs CQ8 dongusune dusersen: tam silme yerine MINIMAL guard birak — behavior korunur, scope creep olmaz** |
| 2026-02-23 | `git checkout HEAD -- file` sonrasi VSCode dosyayi buffer'dan geri yazdi — staged diff'e alakasiz degisiklikler karisti | `git show HEAD:path > temp && [IO.File]::WriteAllBytes(target, bytes)` pattern ile VSCode bypass | **VSCode acikken git checkout/reset ile dosya geri yukleme GUVENILMEZ — git show + byte copy pattern kullan** |
| 2026-02-24 | ExtractFlowConfig sadece ` ```flowconfig ` esliyordu, Claude bazen ` ```json ` icerisinde FlowConfigV2 donduruyor — "Uygula" banner hic gorunmedi | ` ```json ` fallback: Regex.Matches ile TUM json bloklarini tara, ValidateFlowConfigJson ile version=2+nodes+edges kontrol et | **AI model ciktisini parse ederken TEK format bekleme — fallback extraction stratejisi ZORUNLU (flowconfig → json → raw parse)** |
| 2026-02-23 | Codex CoVe Q1 UNKNOWN: diff'te sadece guard gorunuyor, downstream persistence gorunmuyor — false positive | Build PASS + mevcut Codex review'da persistence verified = FORCE PASS | **Codex partial diff'te downstream persistence goremez — guard degisikligi + build PASS = FORCE PASS uygun** |
| 2026-02-23 | Codex CQ5 "tenant isolation" SuperAdmin ops query'de tid=NULL cross-tenant diye FAIL — ayni dosyadaki GetMessagesAsync de ayni pattern | Q FORCE PASS (iter 1) — SuperAdmin ops = cross-tenant by design | **SuperAdmin ops query'leri `(@tid IS NULL OR tenant_id = @tid)` pattern kullanir — Codex bunu tenant isolation ihlali sanir. Verification question'a "SuperAdmin ops, cross-tenant by design" notu ekle, 2 iter sonra FORCE PASS** |

### Deploy & Config

| Date | Mistake | Solution | Prevention |
|------|---------|----------|------------|
| 2026-02-02 | .NET servis port'u default'a dustu - Kestrel eksikti | `ConfigureKestrel(ListenAnyIP(port))` | **Yeni serviste Kestrel port binding kontrol et** |
| 2026-02-11 | Deploy script guncellenmedi (4 script) | AgentAI eklendi | **Yeni mikroservis = deploy scripts OTOMATIK guncelle** |
| 2026-02-11 | Backend appsettings guncellenmedi | Microservice section eklendi | **Yeni servis = appsettings.json + Production.json'a section ekle** |
| 2026-02-11 | Production config placeholder'lar Q'ya birakildi | Tum config'ler otomatik dolduruldu | **Production.json E:\\ path, port, conn string OTOMATIK doldur** |
| 2026-02-11 | deploy-watcher.ps1 ve restart-services.bat guncellenmedi | InvektoAgentAI eklendi | **Yeni servis = deploy-watcher + restart-services guncelle** |
| 2026-02-12 | SPA build output dotnet publish'te yoktu | `npx vite build` sonra publish | **SPA projesinde publish ONCE vite build calistir** |
| 2026-02-12 | Production'da dev port (3002) uzerinden erisildi | Backend:5000 uzerinden serve | **Vite dev server production'da ACMA** |
| 2026-02-14 | .bat'ta embedded PowerShell pipe CMD yorumladi | Ayri .ps1 + .bat wrapper | **PowerShell logic'i .bat'a GOMME** |
| 2026-02-14 | DLL transfer basarisiz - servis dosyayi kilitliyor | Servis durdur -> deploy -> baslat | **Deploy oncesi hedef servisi DURDUR** |
| 2026-02-14 | deploy -mirror production secrets'i sildi | Production.json'a tasindi | **Sunucuda appsettings.json DUZENLEME - deploy ezer** |
| 2026-02-18 | Deploy-watcher (flag polling) SSH/MCP deploy varken gereksiz | NSSM servisi kaldırıldı | **SSH doğrudan erişim varsa polling mekanizması KURMA — MCP server-deploy atomik akışı yeterli** |
| 2026-02-14 | Test script bos JWT key okudu | ConfigPath = Production.json | **Sunucu test scriptleri Production.json'dan okusun** |
| 2026-03-05 | WebChat ops 502 — Backend X-Internal-Key gonderdi ama WebChat production config'de `Microservice:InternalApiKey` yoktu → key="" → match fail → 401 → Backend null → 502 | Production config'e key eklendi + restart | **Yeni servis deploy'unda `Microservice:InternalApiKey` production config'te OLMALIDIR — Backend proxy auth icin zorunlu. Deploy checklist'e ekle** |
| 2026-02-14 | NuGet stable version yok - build failed | PdfPig (v0.1.13) | **NuGet eklerken stable version VAR MI kontrol et** |
| 2026-02-15 | FK `tenant_registry(id)` ama PK aslinda `tenant_id` | `tenant_registry(tenant_id)` duzeltildi | **FK yazarken hedef tablonun PK/kolon adini schema'dan DOGRULA** |

### DB & Schema

| Date | Mistake | Solution | Prevention |
|------|---------|----------|------------|
| 2026-02-11 | Yeni tablo olusturuldu ama GRANT verilmedi | GRANT ALL | **Yeni tablo = DDL + GRANT birlikte** |
| 2026-02-11 | FK constraint - parent tablo bos | Once parent INSERT | **FK tablolarda INSERT sirasi: parent -> child** |
| 2026-02-14 | Soft-delete + unique constraint = ghost duplicate | ON CONFLICT reactivate | **Soft-delete + unique constraint dikkat** |
| 2026-02-14 | Pgvector NpgsqlDbType.Unknown kullanildi | AddWithValue() kullan | **UseVector() register ettiysen explicit type verme** |
| 2026-02-15 | deleted_at IS NULL ama kolon yok | deleted_at check kaldirildi | **SQL'de kolon kullanmadan ONCE schema kontrol et** |
| 2026-02-23 | wa_faq_clusters.cluster_label INT ama GetString(0) okundu | GetInt32(0).ToString() | **Npgsql reader'da kolon tipini DB schema'dan dogrula, INT!=STRING** |
| 2026-02-23 | MCP server `sql.connect()` global pool → per-DB pool gerekli, EXEC/EXECUTE security hole, string interpolation SQL injection | Per-DB `ConnectionPool`, dangerous keyword block, parameterized queries | **MCP server yazarken: (1) global pool YASAK — per-resource pool, (2) EXEC/EXECUTE read-only violation, (3) table-schema icin parameterized query** |

### API & Auth

| Date | Mistake | Solution | Prevention |
|------|---------|----------|------------|
| 2026-02-11 | Yeni endpoint JWT middleware listesine eklenmedi | Prefix listesine eklendi | **Yeni endpoint = JWT prefix listesini kontrol et** |
| 2026-02-11 | Node.js res.json() catch'inde res.text() - body stream tukenmis | text() sonra JSON.parse() | **fetch body stream TEK SEFER okunur** |
| 2026-02-11 | Async hata 30s sessiz timeout | Error callback eklendi | **Fire-and-forget'te MUTLAKA error callback koy** |
| 2026-02-11 | Cok katmanli error handling duplicate callback | Tek katmanda gonder | **Error notification TEK katmanda** |
| 2026-02-12 | MapFallbackToFile slug static dosyalari yakaladi | `{*path:nonfile}` constraint | **SPA fallback icin `{*path:nonfile}` kullan** |
| 2026-02-12 | Root cause yerine workaround - 4+ deploy dongusu | routing pipeline incelendi | **SPA sorunlarinda once routing pipeline incele** |
| 2026-02-15 | PDF magic bytes check try-catch disinda | try-catch icine tasindi | **File I/O HER ZAMAN try-catch icinde** |
| 2026-02-23 | Ops credentials yanlis hatirandi (Invekto2024!) | Production config'den okundu (982wz14ndn6qGBYna) | **Ops auth credentials HATIRLAMAYA calisma, her zaman appsettings.Production.json'dan oku** |
| 2026-02-23 | Server path E:\\ olarak hatirandi ama C:\\Invekto\\ | C:\\Invekto\\Backend\\current\\ | **Production server path: C:\\Invekto\\{Service}\\current\\** |

### Git & Secrets

| Date | Mistake | Solution | Prevention |
|------|---------|----------|------------|
| 2026-02-09 | GitHub Push Protection gercek API key reddetti | Placeholder + soft reset | **Production config'de GERCEK secret OLMAMALI** |
| 2026-02-15 | Diff dosyasi gercek OpenAI key iceriyordu | Soft reset + placeholder | **Diff olusturduktan sonra secret taramasi yap** |
| 2026-02-15 | deploy_output/ gercek key iceriyordu - git add -A staged | Soft reset + placeholder | **git add -A oncesi deploy_output/ secret tarasi** |

### Codex Multi-Iteration (PKT-1 + PKT-2)

| Date | Mistake | Solution | Prevention |
|------|---------|----------|------------|
| 2026-02-15 | Contract-DTO field name mismatch (sources.title vs Question/DocumentTitle) | Unified Title property | **Contract field adini DTO'da BIREBIR kullan** |
| 2026-02-15 | DTO comment "auto-detect" ama kod auto-detect yapmiyor | Comment duzeltildi | **DTO comment = kodun GERCEK davranisini anlat** |
| 2026-02-15 | Lang fallback sonrasi message insert orijinal lang'i kullaniyordu | `template.Lang ?? lang` ile effectiveLang | **Fallback sonrasi "effective" degeri kullan, orijinal degeri degil** |
| 2026-02-15 | Cross-service boundary dogrulama (Q7 tone) 3 iter boyunca UNKNOWN | EXPECTED_UNKNOWN + Q FORCE PASS | **Cross-service dogrulama = EXPECTED_UNKNOWN olarak belgele, 1. iter'de Q escalation** |
| 2026-02-16 | PKT-2 iter 0: 3 gercek kod hatasi (tenant_id x2, silent failure). iter 1: 2 false positive kaldirildi | iter 1'de Q FORCE PASS | **Gercek kod hatalari 1 iteration'da fixlenir, false positive/mimari karar icin Q FORCE PASS** |

### Workflow & Process

| Date | Mistake | Solution | Prevention |
|------|---------|----------|------------|
| 2026-02-09 | arch/ dosyalari guncellenmeden commit yapildi | Her adim sonrasi guncelle | **Commit sonrasi arch/ GUNCELLEMEDEN devam etme** |
| 2026-02-09 | Yeni servis dashboard'da gorunmedi | 6 dosya guncelleme | **Yeni servis = Backend + Dashboard guncelle** |
| 2026-02-12 | Q'nun tanimladigi problemi varsaydim | Once tum dosyalari kontrol et | **Q'nun problemini VARSAYMA - once kontrol et** |
| 2026-02-13 | Interview'da ayni konuyu 2 kez sordum | Detay goster ve devam et | **Ayni konuda 2. AskUserQuestion YASAK** |
| 2026-02-14 | WA Analytics/Roadmap Phase isimleri karisti | Unique prefix ver | **Her workstream'e unique prefix (WA-, RP-, FB-)** |
| 2026-02-15 | Phase tamam ama arch dosyalari guncellenmedi | 3 dosya guncellendi | **DONE olunca OTOMATIK 3 dosya guncelle** |
| 2026-02-15 | Cross-file status stale - birden fazla dosya tutarsiz | Tum referans dosyalar birlikte | **Durum guncellemesini TUM referans dosyalarda yap** |
| 2026-02-15 | Tek musteri verisini genel urun gibi tanimladi | multi-tenant olarak duzeltildi | **Test verisi = ornek, urun = multi-tenant** |

### PowerShell & Python

| Date | Mistake | Solution | Prevention |
|------|---------|----------|------------|
| 2026-02-03 | Invoke-RestMethod HTTPS TLS hatasi | curl.exe -k | **self-signed cert = curl.exe kullan** |
| 2026-02-14 | PowerShell DateTime 'Utc' string hatasi | `[DateTimeKind]::Utc` enum | **DateTime'da timezone enum kullan** |
| 2026-02-14 | Python raw string icinde \uXXXX islenmez | Gercek karakteri yaz | **Python raw string + Unicode: gercek karakteri kullan** |
| 2026-02-14 | MiniBatchKMeans n_clusters > n_samples crash | `min(clusters, n)` guard | **KMeans: cluster <= sample guard ZORUNLU** |

### Logging

| Date | Mistake | Solution | Prevention |
|------|---------|----------|------------|
| 2026-02-14 | SystemInfo/Warn/Error 1 parametre aliyor, 3 ile cagrildi | Tek string formatina duzeltildi | **Logger kullanirken MEVCUT imzayi kontrol et** |
| 2026-03-04 | JWT middleware prefix `/api/v1/translate` dual-auth endpoint'e ulasmayi engelledi — middleware 401 donuyor, endpoint kodu hic calismadi | Prefix'e trailing slash eklendi `/api/v1/translate/` — exact path skip, sub-path'ler korunuyor | **Dual-auth endpoint (JWT + ApiKey) middleware prefix listesine eklenmemeli. Trailing slash ile sub-path korumasi saglanabilir** |
| 2026-03-04 | Claude Haiku system prompt olmadan ceviride aciklama donuyor ("I appreciate the request...") | System prompt eklendi: "You are a translation engine. Output ONLY the translation." | **Claude API'de ceviri/extract gibi strict output gereken islemlerde MUTLAKA system prompt kullan. User-only mesaj conversational mod tetikler** |

---

### UI / Frontend Design

| Date | Mistake | Solution | Prevention |
|------|---------|----------|------------|
| 2026-02-24 | Sidebar nav font 13px + icon 16px cok kucuk — Q "fontlar ve ikonlar cok ufak oldu" dedi | Font text-sm (14px), icon w-5 h-5 (20px), row h-10 (40px) | **Nav item minimum: text-sm + w-5 icon + h-10 row. 13px/16px asla kullanma** |
| 2026-02-24 | Root font-size 14px Q icin kucuk — 18px tercih etti | html { font-size: 18px } | **Q'nun font tercihi 18px root. Yeni SPA'larda bunu default yap** |
| 2026-02-24 | Onboarding nav item'a ozel gradient kutu eklendi — Q istemedi, "normal yazi olsun" dedi | Normal renderNavLink ile render, ozel CSS kaldirildi | **Nav item'lari uniform tut. Ozel kutu/card istemeden ekleme — Q minimalist tercih ediyor** |
| 2026-02-24 | Font size controls (T/px/T/reset) sidebar'da yer kapliyordu — Q "kaldir" dedi | Tum font size UI + state + localStorage tamamen silindi | **Font size UI sidebar'dan kaldirildi. Q tekrar isterse ekle, proaktif ekleme** |
| 2026-02-24 | Template Library'de 60 kart grid'i — Q "sablon ne oldugu anlasilmiyor, okunamiyor" dedi | Category-grouped accordion layout + Turkish labels + descriptions + examples | **Cok sayida benzer kartı grid'e doseme = okunamaz. Kategori gruplama + aciklama + orneklerle bilgiyi sindirilebilir yap** |
| 2026-02-24 | "Tumunu Ekle" butonu + genel ilerleme bar'i eklendi — Q "hedef hepsini eklemek degil, kaldir" dedi | Bulk adopt + overall progress kaldirildi | **Kullaniciya "hepsini ekle" dayatma. Secici benimseme doğru — her sablon bireysel secilmeli** |
| 2026-02-24 | Onboarding expanded panel'de "Henuz bilgi yok" + aciklama yazisi vardi — Q "kaldir, varsa listele yoksa henuz eklenmedi" dedi | Description kaldirildi, detail/fallback pattern eklendi | **Bos durum icin uzun aciklama yerine minimal fallback: "Henuz eklenmedi" yeterli** |
| 2026-03-05 | NSSM stdout.log'da eski hatalar goruldu, servis aslinda calisiyor | curl ile direkt test, JsonLines log tarih kontrolu | **NSSM log dosyalarina APPEND yapar — eski hatalar yeni hata gibi gorunur. Tarih kontrolu + direkt curl test ile dogrula** |
| 2026-03-05 | WebChat widget'da visitor mesajlari gorunmuyordu — appendMessage'daki duplicate check ayni content'li mesajlari engelliyordu | Duplicate check kaldirildi, SignalR echo'da visitor type skip | **Optimistic UI + SignalR echo = duplicate check yerine sender type filter. Ayni content ≠ ayni mesaj** |
| 2026-03-05 | Deploy sonrasi appsettings.Production.json eski degerlere donuyordu | Deploy config restore sonrasi regex ile kritik degerleri override et | **Deploy pipeline config backup/restore yapar — yeni config degerlerini deploy SONRASI override et** |

## Patterns That Work

### Architecture & Design

| Pattern | Where Used | Why It Works |
|---------|------------|--------------|
| Service isolation | Mikro servisler | Bagimsiz deploy, kolay test |
| Shared contracts | Servisler arasi | Type safety, API uyumu |
| IDisposable pattern | Resource management | Memory leak prevention |
| Denormalized counters | Aggregate tables | O(1) reads, atomic increment |
| Generic key-value settings API | Ayarlar | Yeni ayar = backend degisikligi gerekmez |
| Immutable graph pre-compute (HashSet/Dict) | FlowGraphV2 | O(1) lookup, thread-safe reuse |
| Contextual required field validation | FlowValidator | Runtime context check, false positive onler |
| Roadmap/teknik-detay hiyerarsisi | Docs | Summary -> tracking -> detail, tek kaynak |
| Plan review + iyilestirme adimi | Phase 3a | 8 mimari iyilestirme bulundu, plansiz baslamak kacirirdi |
| Keyword pre-filter before LLM | BatchClassification RI-8.1 | High-confidence regex matches skip expensive LLM call → cost savings, same accuracy for clear-cut outcomes |
| Parallel.ForEachAsync with bounded concurrency | BatchClassification RI-8.4 | maxDegreeOfParallelism=4 prevents overwhelming MSSQL while 4x faster loading |
| 10 Paket Execution Stratejisi (v5.1) | Phase 2-3 | 24 dongu -> 8 paket -> buyuk PKT split ile 10 paket, yonetilebilir boyut |
| Pre-phase tech research + faz dosyasina embed | Phase 3C/3D CLIP/MediaPipe | Faz basladiginda karar alinmis, zaman kaybi yok |
| PKT split: isimli strateji + Q secimi | PKT-6 -> 6A/6B/6C (Strategy C) | Coklu secenek -> Q bilincli karar verir, tek oneri dayatma yok |
| Karsilastirma tablosu (latency/cost/CPU/prod-ready) | CLIP + MediaPipe arastirmasi | Q hizli karar verir, duz metin yerine tablo net |
| GT auto-accept (tiered→GT) for rapid benchmarking | RI-Faz2 cross-validation | Tiered F1=1.0 tautological ama bagimsiz modelleri hizlica karsilastirabiliyorsun. Human GT = gelecekte |

### DB & Data

| Pattern | Where Used | Why It Works |
|---------|------------|--------------|
| IDbContextFactory + await using | Concurrent requests | Thread-safe DbContext |
| AddPooledDbContextFactory + scoped | DI setup | Factory + legacy scoped birlikte calisir |
| ConcurrentDictionary.TryAdd | Race prevention | Ilk gelen kazanir |
| Idempotent DB op (catch dup -> fetch) | Concurrent writes | Duplicate key yerine mevcut kaydi dondur |
| ON CONFLICT reactivation | FAQ CRUD | Soft-delete + unique birlikte calisir |
| Multi-value batch INSERT (50/batch) | KnowledgeRepository | N/50 round-trip, atomik |
| Multi-row UPDATE FROM VALUES | BatchUpdateChunkEmbeddings | Tek SQL, N/50 round-trip |
| Document tenant ownership check | Chunk insert | Multi-tenant izolasyon garantisi |
| SQL CASE WHEN conditional fetch | AutomationRepository | Gereksiz buyuk JSON cekilmez |
| Soft-delete + ON CONFLICT pattern | Soft-delete tablolar | Unique constraint + reactivation |
| Semantic search + keyword fallback | RetrievalService | Embedding fail -> keyword'e dus, graceful |
| `template.Lang ?? lang` effective value after fallback | TriggerProcessor lang fallback | Orijinal deger yerine "secilen" degeri kullan - data tutarliligi |
| 3-step fallback chain (specific → default → any) | Outbound lang template | Graceful degradation: her adimda daha genel, ama warning ile |
| Warning field in API response for fallback | TriggerWebhookResponse.Warning | Caller fallback oldugunu bilir, loglama yetmez |

### Deploy & Config

| Pattern | Where Used | Why It Works |
|---------|------------|--------------|
| ConfigureKestrel + ListenAnyIP | Port binding | Explicit port, launchSettings bagimsiz |
| curl.exe -k PowerShell'de | HTTPS API call | TLS sorunlari bypass |
| Ayri .ps1 + .bat wrapper | Deploy scripts | Pipe/escape sorunlari ortadan kalkar |
| appsettings.Production.json | Deploy-safe config | Sunucu secrets korunur |
| Yeni Mikroservis Checklist | Her yeni servis | 17 maddelik otomatik checklist |
| Yeni Servis Deploy Yardimi | Her yeni servis deploy'u | SQL fix + appsettings + NSSM + firewall + restart OTOMATIK sunulur (watcher artık gereksiz — SSH/MCP deploy) |
| Selective git staging | /rev workflow | Focused diff, Codex review kolaylasir |
| Port haritasi cross-check | Yeni servis planlama | Port cakismasi onlenir |

### Auth & Security

| Pattern | Where Used | Why It Works |
|---------|------------|--------------|
| JwtGenerator shared token factory | Invekto.Shared | JWT signing tek yerde |
| Auth guard null-safe (exists && mismatch) | Tenant isolation | null = yok, 403 sadece gercek mismatch |
| File cleanup on upload failure | Knowledge upload | Orphan dosya birakma |
| LLM anti-leak prompt kuralı | Tüm müşteriye görünen LLM servisleri | "Do NOT include reasoning, thinking, meta-commentary" — chain-of-thought sızıntısını önler. Haiku özellikle sızıntıya yatkın |
| LLM çeviri dilbilgisi kuralı | TranslationService system prompt | Türkçe SOV cümle yapısı + "lütfen" başa — LLM'ler İngilizce SVO sırasıyla devrik çeviri yapıyor |
| LLM çıktı test ederken cache temizle | Translation cache (message_translations) | Aynı metin cache'den gelir, yeni prompt test edilemez — DELETE FROM message_translations |
| Impersonate via existing JWT infra | SuperAdmin tenant switch | Yeni middleware gereksiz — GenerateToken + setSession + removeTokens mevcut altyapiyi yeniden kullaniyor |
| Basic Auth in-memory + JWT localStorage | Impersonate exit flow | removeTokens JWT siler ama credentials (in-memory) kalir — ops mode'a donus icin login gereksiz |
| window.location.href (navigate degil) | Impersonate giris/cikis | Full page reload tum hook'lari yeni session ile baslatir — React Router navigate stale state birakir |
| INMA JWT CompanyCode = tenant_id mapping | Token exchange + Dashboard session | CompanyId = INMA internal, CompanyCode = bizim tenant_id — webhook `?companyId=` parametresi de CompanyCode'a karsilik gelir |

### Workflow & Review

| Pattern | Where Used | Why It Works |
|---------|------------|--------------|
| Interview'da seytanin avukatligi | Auto workflow | Q challenge edilir, pasif kalmak degil |
| Codex escalation analizi | Iter 3 escalation | real vs false-positive kategorize et |
| 3-dosya arch update pattern | Her faz bitisi | session-memory + active-work + q-ops-checklist |
| OPS-N numarali ops checklist | q-ops-checklist.md | Kisa referans, cross-session takip |
| queueMicrotask deferred revalidation | flow-store.ts | UI donmuyor, validation otomatik |
| Step result chaining | Simulator | Onceki step'ten otomatik deger cekme |
| Error callback in async processing | Orchestrator | Sessiz timeout yerine gercek hata mesaji |
| Pre-write 5 soru gating (Codex Utansin) | Her kod satiri oncesi | Hata yazildiktan sonra degil, yazilmadan ONCE engellenir - iteration=0 |
| DRY canonical source + referans pattern | INVEKTO_BASE -> tum dosyalar | Kural tek yerde tanimlanir, tutarsizlik imkansiz |
| 4 nokta kural yayilimi | Doktrin yazimi | Yeni kural -> INVEKTO_BASE + CLAUDE.md + DEV_AGENT + MEMORY.md = hicbir session kacirmaz |
| Insights raporu -> CLAUDE.md feedback dongusu | Workflow v5.2 | Insights friction analizi -> mevcut kurallara karsilastir -> eksik kurallari ekle, var olanlari skip et |
| `/wrap` skill ile post-phase konsolidasyonu | Session kapama | 5 adim (tracking + learn + secret scan + push + prompt) tek komut, Q manual adim hatirlamaz |
| .gitignore proaktif audit | Git hygiene | deploy_output + usage-data + diffs eklenmezse secret leak + 10K+ pending dosya riski |
| Split Codex review (servis bazli) | PKT-5A 345KB diff | Full diff context asar, servis bazli split = her part <200KB, anlamli review |
| NpgsqlBatch bulk insert | Audit trail, batch ops | N+1 insert dongusu yerine tek batch = tek roundtrip, atomik |
| Cross-service client (Service→Service HTTP) | KnowledgeIntentClient (Automation→Knowledge) | Servisler arasi veri cekme icin typed HTTP client + fallback + timeout, DB dogrudan erisim YASAK |
| DB-driven intent pattern (seed + runtime) | PKT-6A AiIntentHandler | Hardcoded intent yerine DB'den cek, sektor bazli seed data ile bootstrap, runtime'da CRUD |
| WapCRM callback bridge (thin proxy) | Backend /api/v1/callback/wapcrm | Automation OutgoingCallback → WapCRM chatoperation format donusumu, instanceID message_log'dan, userID tenant_registry settings'den |
| Dynamic instanceID from message_log | WapCRM bridge | Gelen mesajin instance_id'si message_log'a yazilir, callback'te ayni phone+tenant icin son instance_id okunur — hangi hattan geldiyse oradan doner |
| Dual-source pipeline (CSV + MSSQL) via IAsyncEnumerable | CleanerService RunCoreAsync | Ayni temizleme/dedup/insert mantigi `IAsyncEnumerable<List<string[]>>` uzerinden calisiyor — CsvStreamReader veya MssqlReaderService farketmez, core logic tek yerde |
| Streaming SqlDataReader forward-only + SequentialAccess | MssqlReaderService | Milyonlarca satir icin buffered collection OLUSTURMA — `yield return` + `SqlDataReader` = constant memory, backpressure pipeline tarafindan yonetilir |
| Per-DB ConnectionPool (MCP server) | customer-mssql MCP | `sql.connect()` global tek pool = DB degisince sorun. Her DB icin ayri `new sql.ConnectionPool(config)` + idle timeout ile otomatik temizlik |
| Linked CTS timeout = request already sent | MainAppCallbackClient callback retry | CancellationTokenSource.CreateLinkedTokenSource + CancelAfter(timeout) OperationCanceledException firlatir AMA HTTP request zaten gonderilmistir — retry = duplicate mesaj. Timeout catch'inde `return true` (delivered say) |
| New session __last_input initialization | AutomationOrchestrator + AiIntentHandler | Yeni session'da `__last_input` set edilmezse flow auto-chain (trigger→welcome→ai_intent) kullanicinin ilk mesajini kaybeder. Orchestrator'da `state.Variables["__last_input"] = messageText` + Handler'da first-visit check |

### UI & Frontend

| Pattern | Where Used | Why It Works |
|---------|------------|--------------|
| Popup header'da entity adi | UI Modal | Kullanici neye baktigini hemen anlar |
| {*path:nonfile} SPA fallback | MapFallbackToFile | Static dosyalar fallback'e dusmez |
| Mevcut proxy pattern yeniden kullanma | FbProxyGet | Yeni endpoint 3 satir ile tamamlandi |
| Impersonation banner fixed + pt-10 | Layout.tsx | Fixed banner sidebar sticky'yi bozmaz, pt-10 icerik kaymasi onler |
| opsOnly filter tenant_id=0 bypass | Layout.tsx sidebar | SuperAdmin (tenant_id=0) opsOnly sayfalari gorebilir, impersonate (tenant_id≠0) goremez |
| Token + response null check | API calls | Auth yoksa sessiz cik, crash olmaz |
| Task agent ile paralel mass-replace | Stripe palette migration (40+ dosya) | Ana context korunur, dosya okuma/edit paralel isler, context bloat onlenir |
| Final grep dogrulama | Mass CSS class replacement | replace_all sonrasi kalan referanslari yakalar — ozellikle inline hex (#94a3b8) vs Tailwind class (slate-300) ayri pattern gerektirir |
| Style guide + kod tandem guncelleme | INSE-STYLE-GUIDE.md + Dashboard/FlowBuilder | Kod degistirip guide guncellenmezse drift olusur — her palette degisikliginde IKISI BIRDEN guncelle |

### Concurrency & Performance

| Pattern | Where Used | Why It Works |
|---------|------------|--------------|
| Off-peak scheduler | Heavy islemler | Gunduz timeout olmaz |
| Per-operation timeout + continue-on-error | Batch islemler | Bir kayit fail = digerlerine devam |
| Timer-based orphan cleanup | Resource management | Session sizmasini onler |
| IHostedService for cleanup timers | Background services | Clean shutdown |
| SET DEADLOCK_PRIORITY LOW + backoff + jitter | Non-critical jobs | Deadlock'ta victim olur |
| Deadlock retry with error code check | SQL retry | Sadece deadlock'a retry |

### UI/UX & Frontend

| Pattern | Where Used | Why It Works |
|---------|------------|--------------|
| Category-grouped layout with accordion | TemplateLibraryPage | 60 kart okunmaz — kategori gruplama + expand/collapse bilgiyi sindirilebilir yapar |
| Turkish labels + descriptions + examples mapping | INTENT_TR, FAQ_TOPICS | Snake_case slug'lar kullaniciya anlamsiz — Turkce label + aciklama + ornek musteri mesaji anlasilirlikta buyuk fark yapar |
| Iterative UI feedback loop (Q feedback → immediate fix → deploy) | Onboarding + Templates | Tek seferde mukemmel UI cikmaz. Q'nun 4-5 iterasyon feedback vermesi ve her birinin hemen uygulanmasi en iyi sonuc verir |
| DB data quality cleanup alongside UI redesign | FAQ templates | UI duzeltmek yetmez — 42 FAQ'nun 33'u duplicate/garbage idi. UI + data birlikte temizlenmeli |
| Per-category accent colors via inline style + hex | CategorySection | Tailwind class yerine inline hex — runtime'da dinamik renk, CSS class patlamasi yok |

---

## Anti-Patterns to Avoid

### Architecture

| Anti-Pattern | Problem | Better Approach |
|--------------|---------|-----------------|
| Direct DB access between services | Tight coupling | API uzerinden iletisim |
| Shared mutable state | Race conditions | Event-driven communication |
| Hardcoded ports | Cakisma riski | Config/environment'tan oku |
| Lessons-learned okumadan kod | Ayni hata tekrar | Session basinda OKU |

### Code Quality

| Anti-Pattern | Problem | Better Approach |
|--------------|---------|-----------------|
| Raw SQL concat | Injection riski | Parameterized queries |
| Generic error messages | User frustration | Specific + actionable errors |
| Empty catch block | Sessiz failure | Her zaman log veya rethrow |
| Singleton DbContext inject | "second operation" hatasi | IDbContextFactory + await using |
| AddDbContext for concurrent | Factory register edilmiyor | AddPooledDbContextFactory |
| SQL destructure kolon SELECT edilmemis | Undefined, sessiz bug | Destructure = SQL karsiligi kontrol |
| MapFallbackToFile {**slug} | Static dosyalar yakalanir | {*path:nonfile} constraint |

### Operations

| Anti-Pattern | Problem | Better Approach |
|--------------|---------|-----------------|
| Retry without backoff | CPU spike | Exponential backoff |
| Retry without limit | Sonsuz dongu | max_retry_count |
| Queue without drain | Stalled process | timeout + drain_on_stop |
| Startup'ta heavy DB islemi | 10+ dk startup | Scheduler (gece 02:00-05:00) |
| Polling catch'inde sadece log | Sonsuz polling | clearInterval + state reset |

### Process

| Anti-Pattern | Problem | Better Approach |
|--------------|---------|-----------------|
| AskUserQuestion ile strateji tartismasi | Q analiz istiyor, multi-choice degil | Analiz/tablo/karsilastirma sun |
| Kural birden fazla yerde tam tanimlamak (DRY ihlali) | 15+ dosyada tutarsizlik, guncelleme kaosu | Tek canonical source + diger dosyalar referans verir |

---

## Code Review Insights

| Date | Finding | Action Taken |
|------|---------|--------------|
| 2026-02-11 | Codex allowed_files 3 iter boyunca eksiklik yakaladi | Her /rev oncesi git diff ile allowed_files eslestir |
| 2026-02-14 | IntentDetector return null log yoktu - Codex pre-existing kodu bile yakaladi | Her return null'a SystemWarn ekle. Codex tum dosyayi tarar |
| 2026-02-15 | 3 iter'in 2'si plan JSON metadata fix'iydi | Schema-first yaklasim: plan-schema.json ONCE oku |
| 2026-02-15 | Codex comment-code mismatch yakaladi ("auto-detect" in DTO comment but no auto-detect in code) | Comment = kod, Codex comment'leri de review eder |
| 2026-02-15 | Cross-service dogrulama 3 iter boyunca UNKNOWN - erken escalation gerekiyordu | Architecturally unresolvable = iter 1'de Q escalation |
| 2026-02-15 | Phase 3C/3D'de .NET-native CLIP/MediaPipe paketi yok - hybrid mimari zorunlu | Arastirma notlarini faz dosyasina embed et, faz basinda plan yap |
| 2026-03-03 | Codex iter0 CQ1 generic error objects, iter1 CQ1 missing UI error feedback | 1) Ops endpoints: OpsUnauthorized + ErrorResponse.Create pattern kullan 2) Frontend catch: her zaman user-visible error state set et |
| 2026-03-03 | catch(Exception ex) when (ex is A or B) Codex CQ5 FAIL olarak yakaladi | Ayri typed catch bloklari kullan: catch(TaskCanceledException) + catch(HttpRequestException) |
| 2026-04-14 | Yeni error code secerken arch/errors.md range'i kontrol etmedim, INV-AT-053 zaten PKT-12'de kullaniliyordu | Yeni INV-xx-### eklemeden once `grep "INV-xx-" arch/errors.md \| tail -5` ile son kullanilani gor, sonraki bos numarayi sec |
| 2026-04-14 | allowed_files listesinde FlowEngineV2.cs yoktu ama dev sirasinda edit ettim → Codex iter 0 CQ3 PLAN_OUTDATED | Plan yazarken "bu degisikligi hangi dosyada yapacagim?" cevabini TUM dosyalar icin ver, implementation baslamadan grep ile genisletmeli dosyalari tespit et |
| 2026-04-14 | `phone!` null-forgiving operator Codex CQ5 fail (proje tamamen yasak) | IsNullOrEmpty kontrol sonrasi bile `!` kullanma; compiler null-state daralmiyor ama policy gereyi `!` kaldirilmali, gerekirse local variable'a assign et |
| 2026-04-13 | Yeni DB tablosu eklerken sadece migration yazmak yetmiyor: Codex iter 1 CQ5/CQ11 FAIL — arch/db/{service}.sql canonical schema + GRANT ALL + sequence GRANT de eklenmeli | Yeni tablo checklist: (1) migrations/NNN-xxx.sql + GRANT + sequence GRANT (2) arch/db/{service}.sql canonical sonuna aynı tablo + GRANT (3) repository field'ları eşleştir |
| 2026-04-13 | Codex iter 1 catch(Exception ex) CQ1/CQ5/CQ12 üçlü FAIL — 16 pre-existing bare catch olsa bile YENİ catch'lerde typed zorunlu | Yeni catch: NpgsqlException (DB) + JsonException (serialize) + OperationCanceledException (throw/shutdown) + HttpRequestException (callback) + InvalidOperationException (invalid state). Fallback catch(Exception) yok |
| 2026-04-13 | Ops endpoint success=anonymous + failure=ErrorResponse envelope Codex CQ10 FAIL (response-shape inconsistency) | Success path da strongly-typed class/record kullan. Top-level statement dosyalarında class declaration `public partial class Program { }` SONRASINA koy (CS8803 error aksi halde) |
| 2026-04-13 | MCP codex_review iter 0 git_diff param'ı boş + diff_file_path fallback Codex tarafından okunmadı → CQ'ler UNKNOWN/FAIL "insufficient evidence" | git_diff param'ına en az key code sections inline embed et (typed-catch örnekleri, SQL GRANT, endpoint response shape). Tam 55KB şart değil ama kritik snippet'ler şart |

---

## Maintenance Rules

1. `/learn` komutu veya auto mode ile guncellenir
2. **3 Ay Kurali:** 50+ giris olunca 3 aydan eski girdiler `arch/lessons-learned-archive.md`'ye tasinir
3. Sadece proje-spesifik ogrenimler eklenir - genel best practice eklenmez
4. TONIVA girdileri kalici olarak arsiv dosyasinda
5. Yeni giris eklerken ilgili kategori basliginin altina ekle
