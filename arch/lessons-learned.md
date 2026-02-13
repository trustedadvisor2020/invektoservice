# Lessons Learned

> Q düzeltmelerinden öğrenilen dersler. `/learn` komutuyla güncellenir.

## Common Mistakes

| Date | Category | Mistake | Solution | Prevention |
|------|----------|---------|----------|------------|
| (template) | DB | Varsayılan değer unuttum | ALTER TABLE ile eklendi | CREATE TABLE'da her zaman DEFAULT belirt |
| TONIVA | SQL | Deploy Manager GO hatası | GO kaldırıldı | mssql driver GO desteklemiyor |
| TONIVA | SQL | Aynı batch'te kolon ekle+kullan | EXEC() dynamic SQL | Compile-time vs runtime ayrımı |
| TONIVA | Codex | Em dash encoding sorunu | Double dash (--) kullan | **Em dash (—) YASAK! Her yerde double dash (--) kullan** |
| TONIVA | Codex | Empty catch block `return false` - hatayı gizliyor | `logger.warn` eklendi | **Empty catch YASAK - her zaman log at veya rethrow yap** |
| TONIVA | DB | Aynı işi yapan 2 fonksiyon - biri lock'sız kaldı | Her ikisine de lock eklendi | **Aynı resource'a erişen TÜM fonksiyonları kontrol et** |
| TONIVA | Git | Önceki session'lardan kalan staged dosyalar commit scope'unu bozdu | `git reset HEAD` + selective staging | **Session başında `git status` ile temiz slate kontrolü - staged dosyalar önceki işten olabilir** |
| TONIVA | Retry | Backoff/limit olmadan retry queue | Backoff + max retry zorunlu | Retry mekanizmalarında HER ZAMAN: max_retry_count + exponential_backoff |
| TONIVA | Queue | Completion check sonsuz queue bekler | Drain mekanizması ekle | Queue'lar için: max_queue_size + timeout + drain_on_stop |
| TONIVA | Memory | Lambda event handler leak - her call için handler birikti | `EventHandlers` class ile sakla | **Event'e += ile lambda eklersen, -= için AYNI referans lazım - dictionary'de sakla** |
| TONIVA | EF | Singleton DbContext concurrent kullanım - "A second operation started" | IDbContextFactory + scoped context | **DbContext thread-safe DEĞİL! Concurrent erişim için IDbContextFactory kullan** |
| TONIVA | EF | AddDbContext IDbContextFactory register ETMİYOR | AddPooledDbContextFactory kullan | **IDbContextFactory için AddPooledDbContextFactory GEREKLİ** |
| TONIVA | EF | Entity property eklendi, DB migration yapıldı, ama EF kolon mapping unutuldu | DbContext'e `HasColumnName` eklendi | **Entity property = DB migration + EF mapping (snake_case eşleştir)** |
| TONIVA | API | Backend API response değişti ama type tanımları güncellenmedi | API typings güncellendi | **Backend API değişikliğinde type tanımlarını da GÜNCELLE - TypeScript type safety** |
| TONIVA | Race | SHARED mode + retry/stats query = race condition | Mutex lock + transaction isolation | **SHARED dosya + queue/retry logic = RACE CONDITION RİSKİ! Mutex zorunlu** |
| TONIVA | Workflow | lessons-learned'da pattern VAR ama kod yazarken uygulanmadı | Kod yazmadan ÖNCE lessons-learned oku | **Her session başında lessons-learned.md OKU - pattern'lar belgelenmiş ama uygulanmıyor!** |
| TONIVA | PowerShell | `@" "@` heredoc syntax Git commit mesajında hata verdi | Commit mesajını temp dosyaya yazıp `git commit -F` | **PowerShell heredoc Windows'ta güvenilmez - uzun/multiline commit mesajları için temp dosya kullan** |
| TONIVA | SQL | GUID array'i mssql driver INT'e çevirmeye çalıştı | STRING_SPLIT + TRY_CAST kullan | **GUID array parametresi için placeholder array değil, string birleştir + STRING_SPLIT kullan** |
| TONIVA | SQL | Kolon adını yanlış varsaydım | Schema dosyasından doğru kolon adını kontrol et | **Yeni sorgu yazarken kolon adını VARSAYMA - schema tek kaynak** |
| TONIVA | SQL | `MERGE WITH (HOLDLOCK)` aggressive locking - concurrent deadlock | HOLDLOCK kaldırıldı + retry mekanizması | **MERGE sorgularında HOLDLOCK genellikle gereksiz - concurrent erişimde deadlock riski, retry pattern zorunlu** |
| TONIVA | Config | Env variable sadece yoksa ekleniyordu - kaynak dosyada yanlış değer kopyalandı | Her zaman override et | **Env variable kopyalama scriptlerinde kritik değerler HER ZAMAN override edilmeli** |
| TONIVA | UI | HTTP error'da polling durmuyor - sonsuz döngü | `clearInterval` + state reset eklendi | **HTTP error catch'inde polling/interval durdurmayı kontrol et - sadece log yetmez** |
| TONIVA | API | Token ve response null check eksik - undefined crash | Her fonksiyona `if (!token) return` + optional chaining eklendi | **API çağrısı yapan her fonksiyonda: 1) Token kontrolü 2) Response optional chaining 3) Fallback değer** |
| TONIVA | Logging | Production'da `logger.info` görünmedi | `logger.warn` + `console.log` birlikte kullanıldı | **Production debug için `logger.warn` veya `console.log` kullan - info seviyesi production'da kapalı olabilir** |
| TONIVA | SQL | OUTER APPLY N satır = N*2 subquery, yavaş response | 3 aşamalı bulk fetch pattern | **Detaylı bilgi gerektiren listelerde OUTER APPLY yerine bulk query pattern kullan** |
| 2026-02-02 | Config | .NET servis port'u default'a düştü - Kestrel ConfigureKestrel eksikti | `builder.WebHost.ConfigureKestrel(options.ListenAnyIP(port))` eklendi | **Yeni servis oluştururken Kestrel port binding'i kontrol et - yoksa .NET random port atar** |
| 2026-02-03 | PowerShell | `Invoke-RestMethod` HTTPS'te TLS hatası veriyor (self-signed cert) | `curl.exe -k` ile bypass | **Windows PowerShell + self-signed cert = curl.exe kullan** |
| 2026-02-09 | Workflow | arch/ dosyaları (session-memory, active-work) güncellenmeden commit yapıldı | Her adım sonrası güncelle | **Her commit/task sonrası arch/session-memory.md + arch/active-work.md GÜNCELLEMEDEN devam etme** |
| 2026-02-09 | Git | GitHub Push Protection gerçek API key içeren dosyayı reddetti | Placeholder kullan, soft reset + recommit | **Production config'lerde GERÇEK secret OLMAMALI - REPLACE_WITH_ACTUAL_KEY placeholder kullan** |
| 2026-02-09 | Dashboard | Yeni mikroservis eklenince dashboard'da görünmedi | 6 dosya manuel güncelleme | **Yeni servis = Backend (config+Program.cs+Client) + Dashboard (HealthCard+DependencyMap+TestPanel) güncelle** |
| 2026-02-11 | Deploy | Yeni servis eklenince deploy script guncellenmedi | AgentAI eklendi | **Yeni mikroservis = dev-to-invekto-services.bat'a OTOMATIK ekle (REMOTE_, LOCAL_, build step, upload step, marker, output)** |
| 2026-02-11 | Deploy | Yeni servis eklenince install-services.bat guncellenmedi | AgentAI eklendi | **Yeni mikroservis = arch/deploy/install-services.bat'a OTOMATIK ekle (NSSM blok, log dir, start, status, test URL, manage)** |
| 2026-02-11 | Deploy | Yeni servis eklenince firewall-rules.bat guncellenmedi | AgentAI eklendi | **Yeni mikroservis = arch/deploy/firewall-rules.bat'a OTOMATIK ekle (port + localhost/external karar ver)** |
| 2026-02-11 | Config | Yeni servis eklenince Backend appsettings guncellenmedi | AgentAI eklendi | **Yeni mikroservis = Backend appsettings.json + appsettings.Production.json'a Microservice section OTOMATIK ekle (Url, LogPath, ozel timeout)** |
| 2026-02-11 | Config | Production config placeholder'lar Q'ya birakildi, Q tekrar sordu | Tum config'ler otomatik dolduruldu | **Yeni mikroservis = appsettings.Production.json E:\\ path, port, connection string OTOMATIK doldur. Sadece secret key'ler REPLACE_WITH_ACTUAL_KEY kalir** |
| 2026-02-11 | Codex | Yeni step type (api_call) eklendi ama mevcut webhook-only broadcast kodu guncellenmedi (`step.webhook` null dereference) | `step.type === 'api_call'` guard + optional chaining | **Yeni variant/type eklerken TUM mevcut erisim noktalarini tara - sadece yeni kodu yazmak yetmez, eski kodun yeni type'i handle ettigini dogrula** |
| 2026-02-11 | Codex | Multi-step senaryoda inter-step data aktarimi icin hardcoded placeholder kullanildi (`REPLACE_WITH_SUGGESTION_ID`) | `{{step_N.field}}` template + `resolveStepRefs()` chaining mekanizmasi | **Cok adimli akislarda adimlar arasi veri aktarimi OTOMATIK olmali - manuel placeholder yerine runtime resolver yaz** |
| 2026-02-11 | Codex | Plan JSON `files_changed` listesi, unstage sonrasi gercek staged dosyalarla senkronize edilmedi | `files_changed`'i actual `git diff --cached` ile esitle | **Plan JSON metadata degisikliginden sonra (stage/unstage) `files_changed` ve `files_count`'u MUTLAKA guncelle** |
| 2026-02-11 | Auth | Yeni proxy endpoint (`/api/v1/automation/webhook`) eklendi ama JWT middleware prefix listesine eklenmedi - endpoint korumasiz kaldi | `UseJwtAuth` prefix listesine `/api/v1/automation/` eklendi | **Yeni endpoint eklerken JWT middleware prefix listesini kontrol et - yeni path mevcut prefix'lerle uyusmuyorsa YENI prefix ekle** |
| 2026-02-11 | Codex | Yorum "All traffic via Backend" yaziyordu ama kod dogrudan servis erisimini de destekliyordu - yorum/kod celiskisi | Yorum guncellendi: "Production: Backend proxy / Debug: direct" | **Mimari yorumlar (routing, trafik akisi, auth) MUTLAKA kodun gercek davranisiyla eslessin - mutlak ifadeler sadece gercekten mutlaksa kullanilsin** |
| 2026-02-11 | Node.js | `res.json()` catch'inde `res.text()` cagrildi - body stream zaten tukenmis ("Body already been read") | `res.text()` ile raw oku, sonra `JSON.parse()` dene | **Node.js fetch: body stream TEK SEFER okunur! Once text(), sonra JSON.parse() - asla json() catch'inde text() cagirma** |
| 2026-02-11 | Deploy | Yeni servis eklenince deploy-watcher.ps1 ve restart-services.bat guncellenmedi - servis durdurulmadi, DLL kilitli kaldi, FTP transfer basarisiz | InvektoAgentAI her iki script'e eklendi | **Yeni mikroservis = deploy-watcher.ps1 ($services array) + restart-services.bat (stop/start/status/test URL) OTOMATIK guncelle** |
| 2026-02-11 | API | Async Task.Run hata verdiginde caller'a bildirim yapilmadi - 30s sessiz timeout | Error callback mekanizmasi eklendi (orchestrator catch + Task.Run catch) | **Fire-and-forget async islemde MUTLAKA error callback/notification mekanizmasi koy - sessiz timeout YASAK** |
| 2026-02-11 | API | Cok katmanli error handling duplicate callback uretti (orchestrator + Task.Run ayri ayri gonderdi) | Orchestrator catch blogu kendi error callback'ini gonderir, Task.Run sadece orchestrator disindaki hatalari yakalar | **Error callback/notification TEK katmanda gonder - multi-layer error handling'de hangi katmanin notify ettigini ACIKCA belirle** |
| 2026-02-11 | DB | Yeni DB tablosu olusturuldu ama DB kullanicisina GRANT verilmedi - permission denied | `GRANT ALL ON ALL TABLES/SEQUENCES TO invekto` | **Yeni tablo/sequence olusturulunca DB kullanicisina GRANT vermeyi UNUTMA - schema DDL + GRANT birlikte** |
| 2026-02-11 | DB | FK constraint olan tabloya INSERT denemesi - parent tablo (tenant_registry) bos | Once tenant_registry'ye INSERT, sonra child tablolara | **FK constraint olan tablolarda INSERT sirasi: ONCE parent (tenant_registry), SONRA child (chatbot_flows, chat_sessions, vb.)** |
| 2026-02-12 | Deploy | `wwwroot/flow-builder/` SPA build output'u `dotnet publish` sirasinda yoktu - Vite build calistirilmadan publish yapildi, sunucuda JS dosyalari eksik kaldi | `npx vite build` tekrar calistirildi, sonra publish | **SPA (Vite/React) olan .NET projede `dotnet publish` ONCE `vite build` calistir - build output wwwroot'ta fiziksel olarak yoksa publish'e dahil edilmez** |
| 2026-02-12 | Deploy | Production'da SPA'ya dev port (3002) uzerinden erismeye calisildi - firewall acildi ama calismadi | Port 3002 kapatildi, Backend:5000/flow-builder/ kullanildi | **Vite dev server (3002) sadece local dev icindir. Production'da SPA, Backend:5000 uzerinden serve edilir (wwwroot static files). Dev port'u sunucuda ACMA** |
| 2026-02-12 | Dashboard | Q "bat dosyasi ve watcher bilmiyor" dedi, 6 deploy script kontrol edildi - hepsi zaten Outbound iceriyordu. Gercek eksik: DependencyMap.tsx | DependencyMap.tsx'e Outbound node + arrow eklendi | **Q'nun tanimladigi problemi varsayma - ONCE tum ilgili dosyalari kontrol et, sonra gercek eksikligi bul. Kullanici farki farkli yorumlayabilir** |
| 2026-02-12 | API | `MapFallbackToFile("/flow-builder/{**slug}")` static dosyalari (.js, .css) da yakaladi - browser'a index.html dondu | `{*path:nonfile}` constraint kullanildi | **SPA fallback pattern'de `{**slug}` yerine `{*path:nonfile}` kullan - slug dosya uzantili istekleri de yakalar** |
| 2026-02-12 | API | Root cause yerine workaround denendi (explicit StaticFileOptions, diagnostic endpoint) - 4+ deploy dongusu harcandi | Codex'e soruldu, `:nonfile` constraint dogru fix cikti | **Static file + SPA sorunlarinda once routing pipeline'i incele (fallback pattern, middleware sirasi) - UseStaticFiles wwwroot altindaki tum klasorleri zaten serve eder** |
| 2026-02-13 | Workflow | Interview'da ayni konuyu 2 kez sordum - Q "detay goster" dedi, detay sonrasi tekrar option listesi sundum, Q reddetti | Detay gosterdikten sonra en kapsamli secenekle devam et | **Ayni konuda 2. AskUserQuestion YASAK.** Q "detay goster" dediyse bilgi istiyor, tekrar soru degil. Goster ve devam et. |
| 2026-02-13 | Codex | FlowSummaryBar localStorage catch blogu bos birakildi - lessons-learned'da "Empty catch YASAK" pattern'i VARDI ama yine uygulanmadi (Codex iter 1 FAIL) | console.warn eklendi | **catch blogu yazdigin AN'da "Empty catch YASAK" kuralini hatirla.** Pattern belgelenmis olmasi yetmiyor, her catch yazilisinda bilinc gerekli. |
| 2026-02-13 | Codex | Error code semantic reuse: `AutomationUnknownNodeType` node execution exception icin de kullanildi - farkli failure mode ayni code | Yeni `INV-AT-021 AutomationNodeExecutionFailed` eklendi | **Her failure mode icin ayri error code kullan.** "Benzer gorunuyor" ≠ "ayni anlama geliyor". Unknown type ≠ execution error. |
| 2026-02-13 | Codex | Tenant isolation check'te `GetSessionTenantId() == null` durumunda 403 dondu - session yok/expired durumunu tenant mismatch gibi handle etti, INV-AT-018/019 error contract kirildi | Guard `sessionTenant != null &&` olarak degistirildi - null = session yok, business logic'e birak | **Auth guard'da lookup null donerse 403 verme - entity bulunamadiginda business logic'in dogru error code'u donmesine izin ver.** `null` her zaman "yetkisiz" demek degil, "yok" da olabilir. |
| 2026-02-13 | Codex | Fire-and-forget cleanup `.catch(() => {})` bos birakildı - Codex CQ2 "silent failure" yakaladi | `.catch((err) => { console.warn(...) })` eklendi | **Fire-and-forget bile olsa catch blogu bos birakilMAZ.** console.warn yeter - hata gizlenmez, debug kolaylasir. Lessons-learned'da "Empty catch YASAK" pattern'i 3. kez tekrarlandi. |
| 2026-02-14 | Codex | Bare `catch` (tip belirtmeden) kullanildi - tum exception'lar ayni handle edildi, JSON parse hatasi ile genel hata ayirt edilemiyor | `catch(JsonException)` + `catch(Exception ex)` typed catches, her biri farkli log mesaji | **`catch` blogu yazarken tip BELIRT.** `catch { }` veya `catch(Exception) { }` yerine beklenen exception type'ini ayri yakala. "Empty catch YASAK" + "Typed catch ZORUNLU" |
| 2026-02-14 | Codex | Error durumunda `healthScore = null` birakildi - UI component `score != null` kontrolu yapiyordu, null = badge gizli = kullanici hatayi GOREMEZ | `healthScore = 0` set edildi - badge "Sorunlu" (kirmizi) olarak render edildi, tooltip'te hata mesaji | **Error fallback degeri null BIRAKMA** - UI'da "varsa goster" pattern'i varsa null = gizle demek. Degraded/default deger set et ki UI feedback chain kirilmasin. |
| 2026-02-14 | Codex | Silent fallback path: LogicSwitchHandler bos cases + FlowValidator bos handle_id durumunda sessizce default'a dustu - Codex CQ2 yakaladi | Warning log (StepWarn) ve validation warning eklendi | **Sadece catch degil, TUM silent fallback/default path'lerde uyari uret.** `if (empty) return default` yaziyorsan `logger.warn` veya `warnings.Add` ekle. "Sessiz gecis" = debug kabusuna davet. |
| 2026-02-14 | Codex | Cross-layer contract mismatch: `is_empty` operator value gerektirmez ama backend RequiredFields + shared contract `required` + frontend farkli davraniyordu - 3 katman tutarsiz | Backend contextual check, frontend conditional render, shared contract guncellendi - 3 katman birlikte | **Operator/mode bazli field semantigi degistiginde 3 KATMANI BIRLIKTE guncelle:** 1) Backend validator 2) Frontend UI (conditional render) 3) Shared contract (arch/contracts/). Tek katman fix = gelecekte tutarsizlik. |
| 2026-02-14 | Codex | Plan JSON `allowed_files` degistirilen dosyayi (arch/contracts/) icermiyordu + `files_count` tutarsizdi - 3 iter CQ3 FAIL | allowed_files'a eklendi, files_count guncellendi | **`/rev` oncesi: `git diff --cached --name-only` ciktisini `allowed_files` + `files_changed` + `files_count` ile BIREBIR karsilastir.** UC'U DE senkron olmali. (2026-02-11 lesson guclendirildi) |
| 2026-02-14 | Codex | Graceful degradation `return null` path'lerinde log yoktu -- IntentDetector 3 yerde null donuyordu ama NEDEN null oldugu kayboluyordu. Codex pre-existing kodu bile yakaladi | Her `return null` path'ine `_logger.SystemWarn` eklendi (null JSON, empty content, empty intent) | **`return null` graceful degradation yaziyorsan HER path'te NEDEN null dondugunu logla.** Null != hata degil, ama null'un SEBEBI bilinmeli. "Empty catch YASAK" kuralinin graceful-return versiyonu. |

---

## Patterns That Work

| Pattern | Where Used | Why It Works |
|---------|------------|--------------|
| Service isolation | Mikro servisler | Bağımsız deploy, kolay test |
| Shared contracts | Servisler arası | Type safety, API uyumu |
| IDisposable pattern | Resource management | Memory leak prevention |
| Denormalized counters | Aggregate tables | O(1) reads, atomic increment on write |
| `sp_getapplock` distributed lock | Migrations, shared resources | Cluster-safe, session-scoped, timeout destekli, SQL Server native |
| Generic key-value settings API | Ayarlar | Yeni ayar eklerken backend değişikliği gerektirmez |
| Off-peak scheduler (gece saatleri) | Heavy işlemler | Yoğun işlemler gece çalışır, gündüz timeout/yavaşlık olmaz |
| Per-operation timeout + continue-on-error | Batch işlemler | Bir kayıt fail olursa diğerlerine devam eder, hata izole kalır |
| IDbContextFactory + await using | Concurrent requests | Thread-safe DbContext kullanımı |
| AddPooledDbContextFactory + scoped registration | DI setup | Factory + legacy scoped injection birlikte çalışır |
| ConcurrentDictionary.TryAdd deduplication | Race condition prevention | İlk gelen kazanır, diğerleri ignore/wait |
| Idempotent DB operation (catch duplicate → fetch) | Concurrent writes | Race condition'da duplicate key yerine mevcut kaydı döndür |
| Timer-based orphan cleanup | Resource management | Connection kopma sonrası session sızmasını önler |
| IHostedService for cleanup timers | Background services | Framework timer lifecycle'ı yönetir - clean shutdown |
| Shared utility + centralized import | DRY principle | Duplicate kod eliminate edilir, tek değişiklik noktası |
| `git checkout HEAD -- file` selective revert | Git workflow | Sadece hatalı dosyayı geri alır - tüm work kaybolmaz |
| Token + response null check pattern | API calls | Auth yoksa sessizce çık, API undefined dönerse crash olmaz |
| `SET DEADLOCK_PRIORITY LOW` + exponential backoff + jitter | Non-critical jobs | Deadlock'ta victim olur, jitter ile retry çakışması önlenir |
| Deadlock retry with error code check | SQL retry | `err.number === 1205` ile sadece deadlock'a retry, diğer hatalar fırlatılır |
| Interview'da şeytanın avukatlığı | Auto workflow interview | Q'yu challenge et, alternatifler sun, edge case'leri sor - Q "uyandırılmak" istiyor, pasif kalmak değer katmaz |
| Popup header'da entity adı ana başlık | UI Popup/Modal | Genel açıklama alt başlık, entity adı ana başlık - kullanıcı neye baktığını hemen anlar |
| `ConfigureKestrel + ListenAnyIP/ListenLocalhost` | Mikro servis port binding | Explicit port tanımı, launchSettings.json'a bağımlı değil |
| `curl.exe -k -H "header"` PowerShell'de | HTTPS API call (self-signed cert) | Invoke-RestMethod TLS sorunları bypass, JSON parse `ConvertFrom-Json` ile çalışır |
| Yeni Mikroservis Checklist (OTOMATIK) | Her yeni servis eklenmesinde | Q sormadan tamamla: 1) appsettings.Production.json (E:\\ path + placeholder secrets) 2) Backend appsettings.json + Production'a Microservice section 3) dev-to-invekto-services.bat (build+upload) 4) install-services.bat (NSSM blok) 5) firewall-rules.bat (port) 6) session-memory.md (port tablosu, deploy, servisler) 7) deploy-watcher.ps1 ($services array) 8) restart-services.bat (stop/start/status/test URL) 9) DB tablolari icin GRANT (SELECT/INSERT/UPDATE/DELETE + sequences) 10) tenant_registry'ye test tenant INSERT (FK constraint icin) |
| Step result chaining (`{{step_N.field}}`) | Simulator scenario runner | Onceki step'in response'undan otomatik deger cekme - multi-step E2E testlerde manuel placeholder'a gerek kalmaz |
| Selective git staging (scope discipline) | /rev workflow | `deploy_output/` ve UI refactor dosyalarini unstage edip sadece functional changes'i commit - diff'i focused tutar, Codex review kolaylasir |
| Error callback in async processing | Automation Task.Run + Orchestrator | Sessiz timeout yerine gercek hata mesaji aninda gorulur - "permission denied for table chatbot_flows" gibi spesifik hata simulator UI'da gorundu, log karistirmaya gerek kalmadi |
| Roadmap/teknik-detay hiyerarsisi (summary → tracking → detail) | `roadmap-phases.md` → `phase-1.md` → `flow-builder.md` | Paralel dokumanlar kacinamaz sekilde birbirine referans verir, tek kaynak (phase-1.md) durum takibi yapar, teknik detay ayri dosyada kalir |
| Mevcut proxy pattern'i yeniden kullanma (`FbProxyGet`) | Backend flow list endpoint | Yeni endpoint icin yeni client/helper yazmak yerine mevcut `FlowBuilderClient` + `FbProxyGet` yeniden kullanildi - 3 satir ile tamamlandi |
| `{*path:nonfile}` SPA fallback constraint | `MapFallbackToFile` | Sadece dosya uzantisi olmayan route'lari yakalar, .js/.css UseStaticFiles'a kalir |
| Plan review + iyilestirme adimi (impl oncesi) | Phase 3a plan review | 8 mimari iyilestirme bulundu (Strategy, safety limits, pure engine). Ozellikle IMP-8 sonraki faz icin kritik bagimlilik. Plansiz baslamak bunu kacirirdi. |
| SQL CASE WHEN conditional fetch | `AutomationRepository` flow list endpoint | List endpoint'te v1 flow'lar icin gereksiz buyuk JSON cekilmez (`CASE WHEN version='2' THEN flow_config::text ELSE NULL END`). N satir * config_size kadar bandwidth tasarrufu. Codex CQ6 yakaladi. |
| queueMicrotask deferred revalidation | Phase 2.5 flow-store.ts | React Flow interaction'lari bloklamadan her state degisikliginden sonra validation tetiklenir. UI donmuyor, 9 action'da kullanildi. |
| Immutable graph pre-compute (HashSet/Dict in constructor) | FlowGraphV2 `NodesWithIncoming` | O(E) linear scan yerine O(1) lookup. Immutable object constructor'da bir kez hesapla, thread-safe reuse. Codex iter 2 yakaladi. |
| Codex escalation analizi (real vs false-positive) | Phase 3a iter 3 escalation | Iter 3'te blocking issues'i "real fix" vs "by-design false positive" olarak kategorize edip Q'ya sunmak, FORCE PASS kararini kolaylastirdi. Codex stateless - cross-iteration context yok. |
| Auth guard null-safe pattern (`exists && mismatch`) | Phase 3b tenant isolation | Lookup null = entity yok, business logic handle etsin. `sessionTenant != null && tenant.TenantId != sessionTenant` seklinde yazilir — 403 sadece gercek mismatch'te. |
| Contextual required field validation (operator-aware) | FlowValidator logic_condition rule 4b | `is_empty` gibi unary operator'lerde value gereksiz. Static RequiredFields yerine runtime context check - false positive uyari onler, UX iyilestir. |

---

## Anti-Patterns to Avoid

| Anti-Pattern | Problem | Better Approach |
|--------------|---------|-----------------|
| Direct DB access between services | Tight coupling | API üzerinden iletişim |
| Shared mutable state | Race conditions | Event-driven communication |
| Hardcoded ports | Çakışma riski | Config/environment'tan oku |
| Raw SQL concat | Injection riski | Parameterized queries |
| Generic error messages | User frustration | Specific + actionable errors |
| Retry without backoff | CPU spike, resource overload | Exponential backoff (1s, 2s, 4s...) |
| Retry without limit | Sonsuz döngü | max_retry_count (örn: 3) |
| Queue without drain | Stalled process | timeout + drain_on_stop |
| Empty catch block | Sessiz failure, debug zorluğu | Her zaman log at veya rethrow yap |
| SHARED + retry/queue pattern | Race condition, data corruption | Mutex lock + isolation level |
| Singleton DbContext inject | Concurrent request'lerde "second operation" hatası | IDbContextFactory + await using per-query |
| AddDbContext for concurrent access | IDbContextFactory register edilmiyor | AddPooledDbContextFactory kullan |
| Startup'ta heavy DB işlemi | Timeout, startup 10+ dk sürer | Scheduler kullan (gece 02:00-05:00) |
| Polling catch'inde sadece log | HTTP 404/500 sonrası sonsuz polling | `clearInterval` + state reset + UI feedback |
| Lessons-learned okumadan kod | Aynı hata tekrar | Session başında lessons-learned OKU |
| SQL query sonucunu destructure ederken kolon SELECT edilmemiş | Undefined value, sessiz bug | Destructure ettiğin her field için SQL sorgusunda karşılığını kontrol et |
| `MapFallbackToFile("{**slug}")` SPA subfolder'da | Static dosyalar (.js, .css) da fallback'e duser, browser MIME type hatasi alir | `{*path:nonfile}` constraint kullan |

---

## Code Review Insights

| Date | Finding | Action Taken |
|------|---------|--------------|
| (template) | Error handling eksik | Tüm catch bloklarına log eklendi |
| TONIVA | NOLOCK + keyset pagination = dirty reads riski | Export'ta kabul edilebilir trade-off, kritik işlemlerde NOLOCK kullanma |
| 2026-02-11 | Codex `allowed_files` scope check 3 iter boyunca dosya eksikligi yakaladi (iter3: null deref + chaining, iter4: scope+auth+comment, iter5: PASS) | Her /rev oncesi `git diff --cached --name-only` ile `allowed_files` eslestirmesini manuel kontrol et |
| 2026-02-14 | IntentDetector.cs'de 3 `return null` path'inde log yoktu -- pre-existing kod (Phase 3a) ama Codex Phase 4b diff'inde yakaladi (dosya modified oldugu icin tum dosya tarandi) | Her `return null` oncesine `_logger.SystemWarn` eklendi. Codex modified dosyanin TAMAMINI tarar, sadece diff satirlarini degil. |

---

## How to Add

1. `/learn` komutunu çalıştır
2. Önerilen eklemeleri incele
3. "onay" de

**KURAL:** Sadece proje-spesifik öğrenimler eklenir. Genel best practice eklenmez.
