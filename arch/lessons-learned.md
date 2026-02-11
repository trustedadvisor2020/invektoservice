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
| Yeni Mikroservis Checklist (OTOMATIK) | Her yeni servis eklenmesinde | Q sormadan tamamla: 1) appsettings.Production.json (E:\\ path + placeholder secrets) 2) Backend appsettings.json + Production'a Microservice section 3) dev-to-invekto-services.bat (build+upload) 4) install-services.bat (NSSM blok) 5) firewall-rules.bat (port) 6) session-memory.md (port tablosu, deploy, servisler) |

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

---

## Code Review Insights

| Date | Finding | Action Taken |
|------|---------|--------------|
| (template) | Error handling eksik | Tüm catch bloklarına log eklendi |
| TONIVA | NOLOCK + keyset pagination = dirty reads riski | Export'ta kabul edilebilir trade-off, kritik işlemlerde NOLOCK kullanma |

---

## How to Add

1. `/learn` komutunu çalıştır
2. Önerilen eklemeleri incele
3. "onay" de

**KURAL:** Sadece proje-spesifik öğrenimler eklenir. Genel best practice eklenmez.
