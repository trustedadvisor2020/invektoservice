# Rebrand: Invekto → Chatinbox AI — Migration Plan

> **Durum:** DRAFT (plan-only, hiçbir kod değişmedi) · **Tarih:** 2026-06-24 · **Sahip:** Q
> **Kaynak:** `chatinbox-rebrand-audit` workflow (8 alan denetçisi + completeness critic, 2261+ kod / 6105 docs / 580 DB / 520 deploy occ taraması)
> **Hedef repo:** https://github.com/trustedadvisor2020/ChatinboxAI.git (yeni klasör + yeni repo)

## Execution Log

- **2026-06-24 — Track A Faz A1-A3 ✅ COMMIT `d33a4c42` (branch `rebrand/chatinbox`, Codex /rev PASS iter1):**
  - git mv: 14 src + 3 test klasörü/csproj + `InvektoServis.sln`→`Chatinbox.sln` + postman.
  - Kod kimliği rename: 581/582 dosya, case-sensitive dot-anchored `Invekto.`→`Chatinbox.` + `InvektoServis.{Tests,sln,postman}` selektif.
  - **Korundu (doğrulandı):** `"InvektoServis"` JWT issuer literal (B6'ya donuk), lowercase `invekto`/DB/domain (Track B), `/api/invekto/welcome` (2), `invekto-` wire prefix (4).
  - **Build gate: `Chatinbox.sln` 0 Error / 44 Warning (rename-dışı, önceden var).** Residual `Invekto.` tracked src/tests = 0.
  - Kalan Track A: A4 (hook regex + PROJECT_CONFIG build-cmd), A5 (frontend wordmark/title/copy + postman download literal TenantsPage/DashboardPage.tsx + SPA Vite rebuild), A6 (yeni repo — PUSH onayı + D2 lock gerekir).

## 0. Q Kararları (kilitli)

| Karar | Seçim |
|---|---|
| Rename derinliği | **FULL** — runtime kimlikleri dahil (DB adı, JWT issuer, domain, SSL cert, e-posta) |
| Production | **Şimdi cutover** (sunucu path'leri, NSSM servisleri, canlı deployment) |
| Aile kapsamı | **Sadece bu repo** (InvektoServices) — InvektoChat/Website/Help/Toniva HARİÇ |
| Ürün adı / token | **"Chatinbox AI"** · namespace kökü `Chatinbox` · DB/issuer lowercase `chatinbox` |

## 1. Stratejik Çerçeve — Dürüst Pushback (güven: yüksek)

**Yanlış-varsayım uyarısı:** "Invekto hiçbir yerde geçmeyecek" + "production cutover şimdi" tek bir big-bang olarak yorumlanırsa, **patlama yarıçapı 35 ödeyen canlı müşteriyi kapsar** ve geri dönüşü en zor yer burası. Audit, prod cutover'ın aynı anda şunları gerektirdiğini gösterdi: DB downtime + yeni wildcard SSL cert + 14 NSSM servis remove/reinstall + C:\Invekto→C:\Chatinbox dosya taşıma + **her tenant'ın WhatsApp webhook'unun INMA/Meta tarafında yeniden kaydı** + cross-service JWT issuer convergence.

**Öneri (Q onayına tabi):** İşi **iki ayrılabilir track'e** böl. Bu, Q'nun "cutover şimdi" kararını iptal etmez — sadece güvenli sıraya koyar:

- **Track A — Kod + Repo rebrand (düşük risk, hemen yapılabilir):** Namespace/solution/proje/frontend/docs/tooling rename + yeni ChatinboxAI.git repo. Production'a HİÇ dokunmaz. Tamamlanınca Q'nun elinde tamamen Invekto'suz bir kod tabanı + yeni repo olur.
- **Track B — Production cutover (yüksek risk, planlı bakım penceresi):** DB rename + cert + path/NSSM + domain/DNS + webhook re-registration. Track A yeşil build + push olduktan SONRA, gated maintenance window'da.

> Track A, "yeni repo + kodda Invekto yok" hedefini production'ı riske atmadan TAM karşılar. Track B bağımsız planlanabilir/rehearse edilebilir. Big-bang yapmak zorunda değiliz; Q isterse aynı oturumda ardışık da yapılır ama gate'ler korunur.

## 1.5 Codex Review (bağımsız ikinci görüş, gpt-5.5) — Verdict: **MODIFY**

Codex Track A/B ayrımını **doğru ve fazla temkinli DEĞİL** olarak onayladı, ama prod cutover'ı "hâlâ fazla yıkıcı, durable state'i (Hangfire job, webhook mutation, NSSM rollback, DB role auth, session state, partner callback drift) hafife alıyor" dedi. 3 maddi değişiklik planın gövdesine işlendi:

- **M1 — DB rename'i ANA cutover'dan çıkar (D7 değişti).** Internal kimlik, müşteri görmüyor → downtime/risk iş değerini hak etmiyor. **MD5 footgun:** rol şifresi MD5 ile saklıysa `ALTER ROLE RENAME` şifreyi bozar (rol adı MD5 salt'ın parçası). Postgres 16 genelde SCRAM ama varsayma — `SELECT rolname, rolpassword FROM pg_authid WHERE rolname='invekto'` ile kontrol (md5* ile başlıyorsa rename login'i kırar). DB rename ayrıca pgAdmin/backup/monitoring/psql/Hangfire/orphan-pool dahil **TÜM** bağlantıların kapanmasını ister. → **DB adı `invekto` KALIR** (full-depth şartsa ayrı sonraki bakım, Faz B5).
- **M2 — NSSM paralel-install (Faz B1 değişti).** `nssm remove <eski>` cutover anında YAPMA. Yeni servisleri stopped kur, eski servisleri kurulu bırak; stop-old → start-new → validate → **eskiyi GÖZLEM penceresinden sonra sil**. Anında rollback (start-old).
- **M3 — JWT semantiğini cutover penceresinde DONDUR (D4 değişti).** Issuer karşılaştırması **case-sensitive**: `Chatinbox`≠`chatinbox`≠`InvektoServis`≠`InvektoBackend`. Issuer/audience değişimi cutover'dan AYRI, kendi smoke-test'li adımı olur; önce tam **token-producer envanteri** (kim mint ediyor: Backend login, service-to-service, WebChat operator, Marketing, Integrations, VoiceAI/Runtime, long-lived/background token'lar). Eski token'lar düşecekse bu **explicit forced-logout** olarak kabul edilmeli.

> **Mutabık olduğumuz:** Track A/B ayrımı, dual-domain pencere, MUST-NOT-TOUCH, old-runtime'ı cutover'da yok etmeme. **Benim kör noktam:** DB rename'i ana pencereye koymuştum (MD5 footgun + connection-kill kapsamını hafife aldım); NSSM'i remove-then-install yapmıştım (rollback yavaş); issuer case-sensitivity'sini ve audience riskini yeterince ayırmamıştım. **Ayrışma:** yok — Codex'in 3 düzeltmesi de kabul edildi, plan revize edildi.

## 2. MUST-NOT-TOUCH — kör find-replace bunları BOZAR

Bunlar partner/wire/üçüncü-taraf kimlikleri; "tüm domainleri/invektoları düzelt" geçişi yanlışlıkla dokunursa canlı sistem kırılır:

| Kimlik | Yer | Neden dokunulmaz |
|---|---|---|
| `/api/invekto/welcome` | `InmaTokenIntrospector.cs:108`, Backend `Program.cs:9132` | **WapCRM partner endpoint**'i — her INMA/wappflex son-kullanıcı login'i buradan validate olur. Rename = tüm müşteri login'i kırılır |
| `invekto-` OriginRequestIdPrefix | `CustomerSelectionChangedEvent.cs:173`, Backend `Program.cs:2486`, `ActionSetCustomerStatusHandler.cs:128` | **Canlı wire-protocol** — WapCRM'e ClientRequestID olarak gider, loop-suppression için echo edilir. Değişirse uçuştaki ack/loop korelasyonu desync olur |
| `cxapi.wapcrm.net`, `WapCrm`, `app.wapcrm.net`, `app.wappflex.com`, `developer.wapcrm.net` | appsettings BaseUrl, Backend `Program.cs:616` CORS | Partner-owned domain/origin. CORS origin'leri INMA iframe için |
| INMA IP'leri `91.151.84.79`, `78.135.105.53`, `78.135.105.25`, MSSQL `91.151.84.77` | Webhook:AllowedIps, `.mcp.json` | INMA kanalları + readonly MSSQL |
| INMA/wappflex JWT secret VALUE (`Jwt:SecretKey`) | `JwtSettings.cs` | **Rotate ETME** — değişirse tüm aktif oturum + service-to-service token düşer |
| `vector` extension, `hangfire` schema | `knowledge.sql`, `011-hangfire-schema.sql` | pgvector + Hangfire 3rd-party schema isimleri |
| INMA claim adları (CompanyCode, ChatRole, InseFeatures), `inma_*` source claims | `InmaTokenIntrospector.cs` | Partner token şeması |
| `SSH_USER=Administrator` | `.mcp.json`, server-ops | Sunucu hesabı, Invekto-owned değil |
| MLPCM, mlpcm, MicroSIP, Toniva, toniva-bridge.proto | çeşitli | Partner/diğer-ürün kimlikleri |

## 3. Çözülmesi Gereken Açık Kararlar (execution ÖNCESİ)

| # | Karar | Öneri | Neden gate |
|---|---|---|---|
| D1 ✅ | **Yeni public domain + subdomain MAP** | **`chatinbox.net`** (KİLİT) → `chatinbox.net` = chatinbox.net. Subdomain map: super/ai/app/chat/voice/voiceruntime → `*.chatinbox.net`. Wildcard cert `*.chatinbox.net` | Cert, DNS, CORS, CxapiReconcile, Zoho/Meta'nın HEPSİ buna bağlı |
| D2 ✅ | **Git history stratejisi** | **Fresh repo tek "Initial commit"** (KİLİT — öneri) → ChatinboxAI.git'te Invekto sıfır (working tree + log). Eski `invektoservice.git` ARŞİV olarak durur (history kaybolmaz). | "Invekto hiçbir yerde" literal → fresh. Q onayı bekleniyor (aksi: full-history taşı) |
| D3 | **`InvektoServis` token politikası** | `InvektoServis` → `Chatinbox` (uzun token ÖNCE replace → `ChatinboxServis` hatası önlenir), sln/test-root `Chatinbox.sln`/`Chatinbox.Tests` | Naive `Invekto`→`Chatinbox` `ChatinboxServis` üretir |
| D4 | **Canonical JWT issuer** *(Codex M3)* | **Cutover penceresinde DEĞİŞTİRME.** Ayrı adımda: önce token-producer envanteri → tek case-tutarlı string'e yakınsat → prod issuer'ları null TUT (sıfır auth riski). Issuer+Audience **case-sensitive** (`Chatinbox`≠`chatinbox`); eski token düşüşü = explicit forced-logout | Bugün heterojen: null / InvektoServis / InvektoBackend / lowercase invekto. Audience da aynı riskte (VoiceAI) |
| D5 | **Error-code prefix** | `INV-` kalsın mı yoksa `CHX-`/`CB-` mi? | Runtime-emitted token; C# + errors.md + invariant-check.ps1 + PROJECT_CONFIG |
| D6 | **MCP server ID'leri** (`invekto-ops`/`invekto-postgres`) | **KORU** — keyfi local ID, fonksiyonel fayda yok, değişirse 4 dosyaya cascade | Rename riski > fayda |
| D7 ✅ | **DB rename — GEREKLİ (Q), ama ANA cutover'dan ÇIKTI** *(Codex M1)* | Q: "db rename lazım" → YAPILACAK ama **ayrı Faz B5'te** (app+domain+webhook GÜNLERCE stabil olduktan sonra, izole bakım penceresi). Ana cutover'da DB adı `invekto` KALIR. **MD5 footgun:** `pg_authid.rolpassword` md5* ise rol rename şifreyi bozar → önce SCRAM/reset. DB rename TÜM bağlantı kapanışı ister | "müşterileri kopartmayalım" → DB downtime'ı domain cutover'dan ayır |
| D8 ✅ | **CxapiWebhookReconcile prod modu** | **Mekanizma KODDAN DOĞRULANDI** (`CxapiWebhookReconcileJob.cs`): auto-SET gerçek, `OwnedWebhookHosts` migration için tasarlanmış. Default `Enabled=false`; **prod değeri B0'da `invekto-ops` ile sunucudan teyit.** true→auto-migrate (OwnedHosts'a eski+yeni), false→manuel | Sıralama her iki halde aynı: OwnedWebhookHosts ÖNCE |
| D9 | **Zoho config ölü mü?** | Memory: Zoho INSE'den çıkıyor (FEAT-INMA-PIPELINE-V2). Ölüyse `ai.invekto.com/integrations/zoho/callback` re-registration ATLA | Canlıysa Zoho-console external blocker |
| D10 | **Rollback trigger + dual-domain pencere süresi** | Açık fail koşulu (>N tenant webhook fail / HTTPS down / 401 storm) → DNS-revert; eski cert/DNS/webhook kaydı N gün canlı kalsın | Erken cleanup = rollback imkânsız |
| D11 | **Secret rotation kapsamı** | `dev-to-invekto-services.bat` plaintext FTP creds (SİL+rotate), SSH ops password, WebChat prod DB password | Cutover hijyeni |

## 4. ⚠️ EN TEHLİKELİ BULGU — CxapiWebhookReconcileJob (audit-sourced, doğrulanacak)

`src/Invekto.Outbound/Services/CxapiWebhookReconcileJob.cs` → `PublicBaseUrl` (default `https://services.invekto.com`, appsettings.json:42) bind eder ve sweep'te **her tenant'ın webhook URL'ini WapCRM/cxapi tarafında** `{PublicBaseUrl}/api/v1/webhook/event?companyId={tenant_id}` olarak **otomatik yeniden yazar**.

- Domain config değişince bu job, deploy + sweep sonrası canlı partner webhook'ları kendiliğinden yeni host'a re-point eder.
- **Sıra kritik:** `PublicBaseUrl` değişmeden ÖNCE `OwnedWebhookHosts` (`CxapiWebhookReconcileOptions:302`) listesine **HEM eski `services.invekto.com` HEM yeni host** eklenmeli — yoksa eski URL "foreign/customer-owned" sayılıp stale bırakılır → ack ingress sessizce ölür.
- **Aksiyon:** D8'i doğrula. Bu touchpoint hiçbir alan denetçisinin sahiplenmediği "no-man's-land" — execution'da Outbound ekibine açıkça ata.

> Verification dürüstlüğü: Bu davranış audit ajanı tarafından kod okunarak raporlandı, ben bağımsız doğrulamadım. Cutover öncesi `CxapiWebhookReconcileJob.cs` + options + prod appsettings elle teyit edilmeli.

---

## TRACK A — Kod + Repo Rebrand (düşük risk, production'a dokunmaz)

### Faz A1 — Hazırlık
1. Yeni dal: `rebrand/chatinbox` (master'dan). Tüm Track A burada, tek atomik iş.
2. Regenerable artifact temizliği (diff'i kirletmesin): tüm `src/**/bin`, `src/**/obj`, `tests/**/bin`, `tests/**/obj`, `deploy_output/` sil.
3. D2/D3/D5 kararlarını kilitle.

### Faz A2 — Kod kimlikleri (ATOMİK — kısmi durum = build fail)
> Sıra: **uzun token önce** (`InvektoServis` → `Chatinbox`), sonra `Invekto` → `Chatinbox`.

1. `git mv` 14 `src\Invekto.*` klasörü → `src\Chatinbox.*` + 3 test klasörü (`Invekto.Backend.Tests`, `Invekto.ChatAnalysis.Tests` → `Chatinbox.*`; `InvektoServis.Tests` → `Chatinbox.Tests`).
2. `git mv` her `.csproj` dosya adını yeni klasöre eşle.
3. `git mv InvektoServis.sln` → `Chatinbox.sln`; içindeki 17 `Project()` ad+yol girişini düzelt. **17 proje GUID'i + solution-folder GUID'leri DEĞİŞMEZ** (VS re-link churn önler).
4. Text find-replace: `*.cs` (557 namespace + 334 using), `*.csproj` (RootNamespace + ProjectReference Include + postman Content path), `*.sln`.
5. 11 explicit `<RootNamespace>` güncelle; Backend/Marketing/Shared (RootNamespace YOK → dosya adından türer) yeni adla tutarlı mı doğrula.
6. Postman: `postman/InvektoServis.postman_collection.json` rename + internal `name` + Backend `Program.cs` 2 string ref (L3098/L3105) + Backend.csproj Content Include — birlikte.
7. **ServiceConstants.cs** 12 service-name string'i (`Invekto.Backend`...): namespace token değişimi mekanik AMA string VALUE'ları health endpoint + frontend map-key + simulator + NSSM ile coupled (bkz §5 cross-cutting). Lockstep değiştir.
8. **Faz A2 build gate:** `dotnet restore` + clean build `Chatinbox.sln` GREEN + 3 test projesi PASS. Yeşil olmadan ilerleme YOK.

### Faz A3 — Test assertion'ları (runtime string'leri yansıtanlar)
- `HealthTests.cs` `'Invekto.Backend'`, ChatAnalysis health `'Invekto.ChatAnalysis'`, `IcsBuilderTests.cs` PRODID `'-//Invekto//'` → kaynak değerlerle lockstep.
- `CustomerStatusFlowSuppressionTests.cs` `OriginRequestIdPrefix=='invekto-'` → **DEĞİŞTİRME** (wire constant, §2). Test olduğu gibi kalır.
- `TestJwtTokenHelper.cs` TestSecretKey literal: cosmetic, opsiyonel.

### Faz A4 — Tooling / hooks / config (kod rename'den SONRA)
> Hook regex'leri kod rename'den SONRA güncellenmeli (yoksa un-renamed kodda misfire) ama prod cutover'dan ÖNCE.
1. Quality-gate regex'leri: `invariant-check.ps1`, `build-reminder.ps1`, `check-shared-microservice.ps1`, `dotnet-check.sh` → `Invekto\.` → `Chatinbox\.`, `Invekto.Shared` → `Chatinbox.Shared`. (Aksi halde isolation/service ihlali sessizce tespit edilmez — fail-open.)
2. `PROJECT_CONFIG.json`: project_name, build cmd (sln+csproj), base_prompt filename, deploy host/path/NSSM, error-prefix (D5).
3. `deploy-verify.ps1` path/service guard regex'leri — Track B path/NSSM kararıyla eşle (timing: prod cutover ile).
4. Agent prompt dosyaları `INVEKTO_*` → `CHATINBOX_*` (opsiyonel; yapılırsa PROJECT_CONFIG/CLAUDE.md/CONTEXT.md/session-init.ps1 referrer'ları aynı commit'te).
5. D6: MCP server ID'leri `invekto-ops`/`invekto-postgres` KORUNUR (öneri) → settings.json/settings.local.json/.mcp.json server key'leri DEĞİŞMEZ. Sadece SSH_HOST/SERVER_BASE_PATH Track B'de.
6. Aktif prose: `CLAUDE.md`, `CONTEXT.md`, `/deploy`+`/deploy-info`, `tracking/README.md`, `pilot-launch-roadmap.md`, session-init banner.
7. **EXCLUDE (history, D2):** `arch/plans/*.json` (~3000 occ), `arch/*-archive.md`, `lessons-learned-archive.md`, lessons/session-memory historical text — sweep'e path-filter ekle.

### Faz A5 — Frontend rebrand + SPA rebuild
1. Wordmark: `InvektoLogo.tsx` SVG `<text>invekto</text>` → `chatinbox`, aria-label'lar, `InvektoMark` 'i'. Component rename opsiyonel (Layout.tsx + LoginPage.tsx import'ları lockstep).
2. Title'lar: `index.html` `<title>Invekto One</title>` → `Chatinbox AI`, `App.tsx` dinamik title'lar, settings sayfaları document.title.
3. Görünür copy: `OnboardingGuidePage.tsx` (tüm e-posta/rapor template'leri + "Invekto Destek Ekibi" imzaları), `DataImportPage`/`ProjectsPage` iletişim copy'si, `wa-error-codes.ts`, LeadIntake canonical-field copy, `alt="Invekto"` tag'leri.
4. **Host string'leri (D1'e BAĞLI):** `super/ai/app/voice.invekto.com` → `InvektoLogo.tsx`, `LoginPage.tsx`, `App.tsx`, `VoiceTestPage.tsx`, `inmaBridge.ts` (login mode + iframe origin allowlist gate'ler). D1 kilitlenmeden başlamaz.
5. Service-display map-key'leri (`HealthCard/LogStream/TestPanel/DependencyMap` `.replace('Invekto.','')`) → backend assembly rename ile lockstep (§5).
6. `voice-poc.js:74` `BACKEND_BASE='https://super.invekto.com'` → ayrı static asset (SPA build'de DEĞİL); VoiceRuntime'a elle re-upload (critic bulgusu, kolay unutulur).
7. SE/ scenario app + `ui-mocks/*.html` + `content/**/*.mdx` (39 occ) — sahipsiz kalmasın, açıkça ata.
8. **SPA REBUILD:** Vite build → yeni hashed `wwwroot/app/assets/*` + `wwwroot/app/index.html` (~89 baked-in string). **Built bundle'ı ELLE düzenleme** (hash mismatch). Regenerated bundle'ı commit'le.

### Faz A6 — Yeni repo cutover
1. `git remote` → `https://github.com/trustedadvisor2020/ChatinboxAI.git` (D2 stratejisine göre fresh-init veya remote-swap).
2. Yeni klasör: `C:\CRMs\Chatinbox\` (veya Q tercihi) — invs worktree launcher RepoRoot güncellenir (ayrı not).
3. Residual grep: tracked source'ta `Invekto`/`InvektoServis` kalmadı (partner token'lar §2 hariç) doğrula.
4. `.gitignore` line 79 `*.postman_collection.json` → renamed collection `-f` ile veya kural düzelt.

> **Track A çıktısı:** Build-green, test-pass, Invekto'suz kod tabanı + ChatinboxAI.git. Production HÂLÂ Invekto altyapısında, dokunulmadı, çalışıyor.

---

## TRACK B — Production Cutover (yüksek risk, gated bakım penceresi)

> **Ön koşul:** Track A merged + green. D1, D7, D8, D10, D11 kilitli. Yeni cert + DNS + mailbox hazır.

### Faz B0 — Prerequisites (cutover'dan günler önce)
1. **Backup:** Prod `invekto` DB → `pg_dump -Fc` timestamped (rollback artifact, ZORUNLU). VM snapshot varsa al.
2. Yeni wildcard SSL cert `*.chatinbox.net` (.pfx) edin → sunucuda stage (`C:\Chatinbox\certs\`). **Eski `*.invekto.com` cert cutover bitene kadar yerinde KALSIN** (dual-domain pencere).
3. **DNS kayıtları** (canlı DNS'ten doğrulanan IP'lerle; henüz cutover değil, eski invekto.com resolve etmeye devam etsin):

   **invekto.com topolojisi (2026-06-24 doğrulandı — İKİ IP):**
   | invekto subdomain | IP | Kim |
   |---|---|---|
   | services / chat / ai / super / voice .invekto.com | `213.238.172.214` | **BİZİM prod sunucu** (`C:\Invekto`, Backend/WebChat/voice) |
   | app.invekto.com + apex invekto.com | `91.151.84.3` | **INMA** (91.151.84.x range; tenant legacy login) |
   | MX | `invekto-com.mail.protection.outlook.com` | **Microsoft 365** |

   **chatinbox.net A kayıtları → BİZİM sunucu `213.238.172.214`. Önerilen wildcard:**
   ```
   chatinbox.net.        A   213.238.172.214      # apex (veya landing)
   *.chatinbox.net.      A   213.238.172.214      # services/chat/ai/super/voice tek kayıt
   ```
   (Açık alternatif: services/chat/ai/super/voice/voiceruntime.chatinbox.net ayrı A → 213.238.172.214.)

   **SSL cert doğrulaması (wildcard = DNS-01 zorunlu):**
   ```
   _acme-challenge.chatinbox.net.   TXT   "<CA-token>"   # geçici/ACME
   ```

   **Mail — `destek@chatinbox.net` (invekto M365'te → M365 mirror; mailbox canlı olunca):**
   ```
   chatinbox.net.               MX     0  chatinbox-net.mail.protection.outlook.com
   chatinbox.net.               TXT    "v=spf1 include:spf.protection.outlook.com -all"
   autodiscover.chatinbox.net.  CNAME  autodiscover.outlook.com
   selector1._domainkey.chatinbox.net.  CNAME  <M365 DKIM>
   selector2._domainkey.chatinbox.net.  CNAME  <M365 DKIM>
   _dmarc.chatinbox.net.        TXT    "v=DMARC1; p=quarantine; ..."
   chatinbox.net.               TXT    "MS=ms########"   # M365 domain doğrulama (geçici)
   ```
   > Mail bu hafta zorunlu DEĞİL (`destek@` şu an sadece WebChat operator login identity; gerçek inbox kurulana kadar A kayıtları yeter).

   **Opsiyonel — CAA:** `chatinbox.net. CAA 0 issue "letsencrypt.org"` (mis-issuance koruması).

   **AÇIK KARARLAR (Q):** (a) `app.chatinbox.net`+apex → bizim box (213.238.172.214) mu yoksa apex=landing + `app.` INMA-fazına ertele mi? (invekto'da app/apex INMA'da). (b) wildcard mı açık-liste mi (öneri: **wildcard**, tek A+tek cert).
   > **INMA-ertelenmiş paralel-domain (Q kısıtı "bu hafta INMA'ya dokunamam"):** chatinbox.net ayağa kalkar (DNS+cert+Kestrel SNI iki host), invekto.com **birincil** kalır, **PublicBaseUrl=services.invekto.com DEĞİŞMEZ** → webhook'lar invekto.com'da, reconcile partner-yazması yapmaz, INMA dokunuşu SIFIR. Webhook re-point + INMA iframe src = sonraki faz (INMA müsait olunca).
4. `destek@chatinbox.net` mailbox oluştur (WebChat operator login identity).
5. MCP control-plane (`server-ops/index.mjs`): SSH_HOST/SERVER_BASE=`C:\Chatinbox`/SERVICES map yeni NSSM adları/`chatinbox-ops` — cutover'ı sürebilmek için ÖNCE.

### Faz B1 — Maintenance window (Codex M1+M2+M3: old-runtime YIKILMAZ, DB rename YOK, JWT DONUK)
1. **CxapiReconcile guard (D8 true ise):** `OwnedWebhookHosts` = `['services.invekto.com', yeni-host]` SET ET → sonra PublicBaseUrl değiştir. (§4)
2. **Filesystem (non-destructive):** `robocopy C:\Invekto\* C:\Chatinbox\` (servisler + certs + Backups + scripts + logs + nssm.exe). **Eski C:\Invekto\ DOKUNULMAZ** — rollback kaynağı.
3. **Yeni NSSM servislerini STOPPED kur** (Chatinbox*, AppDirectory=`C:\Chatinbox\{Service}\current`, Application=`Chatinbox.*.exe`, AppStdout/AppStderr) — **eski Invekto* servisleri kurulu+çalışır KALIR.**
4. **Server-side appsettings.Production.json** (gitignored, git commit'le GELMEZ — C:\Chatinbox kopyasında elle): Kestrel cert path → yeni `*.chatinbox.net` pfx; **ConnectionStrings `Database=invekto;Username=invekto` AYNEN KALIR** (M1 — DB rename yok); **JWT Issuer/Audience DEĞİŞMEZ** (M3 — semantik donuk); Microservice LogPath → C:\Chatinbox; DbBackup OutputDir → `C:\Chatinbox\Backups`; Webhook:AllowedIps (INMA IP'leri DEĞİŞMEZ).
5. **Cutover anı:** eski Invekto* servislerini `nssm stop` → yeni Chatinbox* servislerini `nssm start`. (Pencere = stop+start, dosya taşıma değil; dakikalar.)
6. **DNS:** `chatinbox.net` subdomain'leri prod IP'ye yönelt. `services.invekto.com` transition boyunca resolve etsin.
7. **Rollback (anında):** sorun → yeni servisleri `nssm stop` + eski Invekto* servisleri `nssm start` (C:\Invekto\ + eski cert + eski DNS hâlâ canlı). DB hiç değişmediği için DB rollback'i YOK.

### Faz B2 — External re-registration (partner tarafı)
1. **INMA/WapCRM webhook re-registration:** her canlı tenant'ın webhook URL'i `services.invekto.com/...` → `services.chatinbox.net/...`. (D8 true → CxapiReconcile sweep otomatik yapar AMA per-tenant ack ile doğrula; false → manuel.)
2. **Meta/Facebook App console:** leadgen callback `app.invekto.com/api/inbound/meta/leadgen/{tenantId}`, App Domains, Privacy/Terms URL, `hub.verify_token` handshake yeniden.
3. **Zoho (D9):** ölü değilse `ai.invekto.com/integrations/zoho/callback` Zoho-console'da re-register.
4. **VoiceRuntime SIP/AudioSocket:** `voiceruntime.invekto.com:8090` + `:5060` müşteri Asterisk PBX dialplan'lerinde — her ses-tenant'ı admin'i güncellemeli (external device coupling). Provisioned ses tenant'ı varsa tek tek.
5. **DentAdavista seed data:** prod DB'de `invekto.com/static/...` ve `app.invekto.com` URL'li customer-facing template body'leri → DB UPDATE (data migration, dosya değil). Statik asset host eski URL'i servis etmeye devam etmeli yoksa canlı mesajlar 404.

### Faz B3 — Doğrulama (go/no-go gate)
- `chatinbox-ops server-health all` → 12 servis + yeni cert HTTPS.
- `https://services.chatinbox.net/health`, `chat.chatinbox.net`, WebChat operator login (yeni issuer mint+validate), INMA SSO login (`/api/invekto/welcome` DEĞİŞMEDİ), service-to-service Backend→Knowledge call.
- Hangfire dashboard recurring job'lar kayıtlı, pgvector knowledge query döner, `tenant_registry` okunur.
- 1 tenant uçtan uca: INMA inbound → ack roundtrip + outbound.
- Manuel DbBackup → `C:\Chatinbox\Backups\chatinbox-*.dump` + `pg_restore --list`.

### Faz B4 — Stabilizasyon + destructive cleanup (gözlem penceresinden SONRA)
> D10 rollback-trigger penceresi temiz geçince:
- **Eski Invekto* NSSM servislerini `nssm remove`** (artık gerek yok; rollback penceresi kapandı — M2).
- Eski `*.invekto.com` cert binding revoke, eski invekto.com DNS kaldır, `C:\Invekto\` leftover decommission.
- Açığa çıkan FTP/SSH credential rotate (D11). `dev-to-invekto-services.bat` SİL (plaintext FTP creds — secret leak; rebrand bunu silmek için tetikleyici).

### Faz B5 — (OPSİYONEL, ertelenmiş) DB rename — full-depth şartsa (Codex M1)
> Sadece Q "DB adı da `chatinbox` olacak" derse. App+domain+webhook cutover GÜNLERCE stabil olduktan SONRA, ayrı bakım penceresi.
1. **MD5 footgun kontrolü:** `SELECT rolname, rolpassword FROM pg_authid WHERE rolname='invekto'`. md5* ile başlıyorsa rol rename şifreyi kırar → önce SCRAM'a geçir veya rename sonrası şifre reset planla.
2. `pg_dump -Fc` backup (zorunlu).
3. TÜM bağlantıları kapat — sadece 12 servis değil: pgAdmin, monitoring, backup job, psql, Hangfire worker, migration runner, orphan pool. `SELECT count(*) FROM pg_stat_activity WHERE datname='invekto'` = 0.
4. `ALTER ROLE invekto RENAME TO chatinbox;` + `\c postgres` + `ALTER DATABASE invekto RENAME TO chatinbox;` (grants/ownership otomatik taşınır).
5. Connection string `Database/Username=chatinbox` tüm server-side appsettings.Production.json + restart. pgvector + Hangfire job state + tenant_registry doğrula.
> **Alternatif (downtime'sız):** DB adını `invekto` bırak, sadece `chatinbox` login rolü ekle (`GRANT invekto TO chatinbox`), connection string Username=chatinbox. Object/schema/Hangfire/extension erişimini TEST et.

### Faz B6 — (OPSİYONEL, ertelenmiş) JWT issuer convergence (Codex M3)
> Cutover stabil olduktan sonra, kendi smoke-test'li adımı.
1. Token-producer envanteri çıkar (Backend login / service-to-service / WebChat operator / Marketing / Integrations / VoiceAI-Runtime / long-lived / background job).
2. Tek case-tutarlı issuer string seç (öneri: prod'da issuer null KAL → sıfır risk; sadece WebChat self-issued operator token'ı + enforcing servisleri tutarlı hale getir).
3. WebChat mint+validate literal'i (Program.cs:221,222,383,385) + Integrations/Marketing/VoiceAI config'i atomik değiştir → her servis kendi içinde tutarlı. Audience'ı da unutma (VoiceAI="InvektoServices").
4. Forced-logout kabul: enforcing servislerde eski token'lar 1 kez re-login ister.

---

## 5. Cross-Cutting Risk Register

| Risk | Açıklama | Mitigasyon |
|---|---|---|
| **CxapiWebhookReconcile auto-mutation** (CRITICAL) | Domain config değişimi canlı partner webhook'larını otomatik re-point eder; OwnedWebhookHosts eski host'u içermezse stale → ack ölür | §4; OwnedWebhookHosts'a eski+yeni host ÖNCE; D8 doğrula |
| **`invekto-` wire prefix** (CRITICAL) | Brand cosmetics DEĞİL, canlı WapCRM ClientRequestID/loop-suppression | DEĞİŞTİRME (§2) |
| **`/api/invekto/welcome`** (CRITICAL) | Tüm müşteri login'i bu partner path'ten validate | DEĞİŞTİRME (§2) |
| **Kod rename lockstep** (CRITICAL) | folder+csproj+sln+RootNamespace+557 ns+334 using+ProjectRef hepsi birlikte; biri eksik = build fail | Atomik dal + build gate (Faz A2) |
| **Heterojen JWT issuer** (CRITICAL) | null / InvektoServis / InvektoBackend / lowercase invekto — uniform rename VoiceAI+WhatsAppAnalytics'te 401 üretir; issuer+audience case-sensitive | **Cutover'da DONUK (M3)**; ayrı Faz B6'da token-producer envanteri sonrası converge; prod issuer null KAL |
| **Cert ≠ yeni domain** (CRITICAL) | `*.invekto.com` cert `chatinbox.net`'i kapsamaz; repo-only rebrand prod'u eski cert'te bırakır | B0: yeni cert ÖNCE |
| **Service-Name string 5-katman coupling** (HIGH) | `Invekto.Backend` = C# ns + health field + 4 frontend panel key + simulator filter + NSSM map | Hepsi tek atomik deploy'da |
| **Server-side gitignored configs** (HIGH) | Gerçek prod appsettings git'te YOK; repo-only rebrand "tamamlandı" görünür ama prod hâlâ 'invekto' | B1.6 elle uygula |
| **Hook regex fail-open** (HIGH) | Kod Chatinbox.* olunca eski regex'ler isolation ihlalini sessizce kaçırır | Faz A4: kod rename'den hemen SONRA |
| **E:\ vs C:\ drift** (MEDIUM) | arch/deploy scripts + 4/5 template hâlâ E:\Invekto (deprecated); rebrand kopyalarsa yanlış drive geri gelir | Drive+brand birlikte düzelt veya stale script'leri sil |

## 6. Rollback Stratejisi (faz-bazlı)

- **Track A (kod/repo):** Tamamı git-tracked → `git reset --hard` / `git revert`. Dış state mutasyonu YOK. Built bundle commit'li → önceki commit'in `wwwroot/app/`'ini checkout + InvektoBackend restart eski SPA'yı geri getirir.
- **Track B / DB:** Ana cutover'da DB DEĞİŞMEDİĞİ için DB rollback'i YOK (M1). (Faz B5 yapılırsa: `ALTER DATABASE/ROLE RENAME` ters + connection string revert; bozulursa B5 `pg_dump` restore.)
- **Track B / NSSM:** Eski Invekto* servisleri B4'e kadar kurulu+çalışabilir kalır → rollback = yeni Chatinbox* `nssm stop` + eski Invekto* `nssm start` (M2, anında, install gerekmez).
- **Track B / cert+HTTPS:** Kestrel cert path eski `star.invekto.com.pfx`'e revert + restart.
- **Track B / webhook 401 storm:** DNS'i invekto.com'a geri yönelt (resolve ediyor) + INMA'dan eski URL'leri canlı tutmasını iste.
- **Altın kural:** B4 destructive cleanup'ı STABLE gözlem penceresi geçene kadar ERTELE. Eski C:\Invekto\, eski cert, eski DNS, eski INMA/Meta/WapCRM kayıtları silinmedikçe her şey reversible.

## 7. Tahmini Efor (kaba)

| Track / Faz | Efor | Risk |
|---|---|---|
| A1-A4 kod+tooling | ~yarım–1 gün (mekanik, build gate'li) | Düşük (git revert) |
| A5 frontend+SPA | ~yarım gün | Düşük |
| A6 yeni repo | ~1-2 saat | Düşük |
| B0 prereq (cert/DNS/mailbox) | Dış süreç (cert/DNS propagation = saatler-gün) | Orta |
| B1 maintenance window | **Dakikalar** (stop-old/start-new; dosya kopya önceden, DB değişmez) | Orta (M1+M2 ile düştü; anında rollback) |
| B2 external re-registration | Saatler (per-tenant + Meta/Zoho/PBX) | **Yüksek** (partner coupling — asıl risk burada) |
| B3-B4 doğrulama+cleanup | Gözlem penceresi gün(ler) | Orta |
| B5-B6 opsiyonel (DB+JWT) | Ayrı bakım, app stabil olduktan sonra | Orta (izole edildi) |
