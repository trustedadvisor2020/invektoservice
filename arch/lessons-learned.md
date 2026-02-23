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

---

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
| 10 Paket Execution Stratejisi (v5.1) | Phase 2-3 | 24 dongu -> 8 paket -> buyuk PKT split ile 10 paket, yonetilebilir boyut |
| Pre-phase tech research + faz dosyasina embed | Phase 3C/3D CLIP/MediaPipe | Faz basladiginda karar alinmis, zaman kaybi yok |
| PKT split: isimli strateji + Q secimi | PKT-6 -> 6A/6B/6C (Strategy C) | Coklu secenek -> Q bilincli karar verir, tek oneri dayatma yok |
| Karsilastirma tablosu (latency/cost/CPU/prod-ready) | CLIP + MediaPipe arastirmasi | Q hizli karar verir, duz metin yerine tablo net |

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

---

## Maintenance Rules

1. `/learn` komutu veya auto mode ile guncellenir
2. **3 Ay Kurali:** 50+ giris olunca 3 aydan eski girdiler `arch/lessons-learned-archive.md`'ye tasinir
3. Sadece proje-spesifik ogrenimler eklenir - genel best practice eklenmez
4. TONIVA girdileri kalici olarak arsiv dosyasinda
5. Yeni giris eklerken ilgili kategori basliginin altina ekle
