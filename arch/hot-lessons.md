# Hot Lessons — Standing Rules

> **Her session yüklenir.** Sadece yüksek-önem + tekrar-eden + ileri-kural. Anlatı YOK. Tam arşiv (~360 satır): `lessons-learned.md` + `lessons-learned-archive.md` — SADECE Grep (codex/deploy/db öncesi ilgili keyword'ü tara). Asla tam Read.

## Codex review (en sık tekrarlayan)
- Shared-Postgres read (tenant_registry/tenant_instances) PR'ında /rev summary'ye BAŞTAN yaz: "single shared Postgres + Outbound zaten okur=precedent + query tenant-scoped + schema=<file:line>" → CQ5/CQ9/CQ11 FP döngüsünü iter0'da kapat.
- `null-forgiving operator (!)` = HARD CQ5 FAIL bu projede; pattern-bind/non-null guard kullan.
- Prior-slice şemasına CRUD yazarken migration diff'te olmaz → CQ11 FAIL: /rev summary'ye migration no + constraint adı + commit attest et.
- `git_diff` arg'ına placeholder/"SEE diff_file_path"/parafraz YAZMA → tüm CQ UNKNOWN→FAIL; EXACT `git diff` çıktısını VERBATIM geç.
- Staged dosyayı edit edince staged kopya GÜNCELLENMEZ → /rev öncesi yeniden `git add`.
- Routing değişikliği auto-escalate LOW→MEDIUM; MEDIUM CoVe ≥3 VQ + Auth/Data/Lifecycle span.
- Codex MEDIUM+ client/integration işinde implementasyon ÖNCESİ `codex_consult(critique)` ile planı strestle.
- Codex "project rule" iddiasını codebase prevalence + review-policy.md ile doğrula, yoksa corrected-premise ile reddet.
- Hosted/background SWEEP job'unda cross-tenant query (tüm tenantları tara) → CQ9 "tenant_id filter yok = FAIL" verir (summary'deki precedent iddiası TEK BAŞINA yetmez). Fix: sanctioned-exception'ı `arch/codex-context.md`'ye KAYITLA (Tenant Isolation + CQ9: request-path ≠ background sweep; precedent FetchPendingOutboxBatchAsync) → diff'e dahil et → iter1 PASS. Yeni INV kodu eklerken AYNI commit'te `arch/errors.md`'ye yaz (CQ12).

## Deploy & prod-truth
- "DONE/committed ≠ deployed": roadmap'te DONE migration prod'a HİÇ uygulanmamış olabilir → multi-paket deploy ÖNCESİ prod DB pre-flight (2x keşif).
- Migration pre-flight `pg_constraint`'e bakıyorsa, migration UNIQUE/partial INDEX ise göremez → `pg_indexes` da kontrol et.
- Prod secret-bearing `appsettings.Production.json`'a section eklerken full ConvertFrom/ConvertTo reserialize ETME → targeted-insert + .bak; boş `current\`'a recover deploy "config korunur" demez.
- NSSM "Running" ≠ healthy + değişmeyen Backend.dll latent regression gizler → port-listen + `/health` curl + canlı asset-hash doğrula; prod-down sorusunda MCP server-status'a değil `nssm status`'a güven.
- server-deploy MCP enum tüm servisleri içermez (VoiceRuntime/VoiceAI eksik) → yanlış dizine deploy riski; yeni servis DLL'i Shared.dll ile PAIR deploy (TypeLoadException guard).
- İki paralel Claude session aynı repo'da deploy edebilir → `git fetch`/working-tree + bundle-hash re-verify.

## DB & concurrency
- Catch `NpgsqlException` (base), sadece `PostgresException` değil → TÜM DB hatasını INV koduna map et.
- `tx.CommitAsync()` NpgsqlException'ı "rolled back, retry-safe" map etmek OVERCLAIM; commit sonrası fresh-conn re-query başarısızlığı ayrı failure mode.
- Junction-insert NON-locking pre-check (SELECT COUNT→INSERT) soft-delete race açar → FOR SHARE row-lock parent write ÖNCESİ.
- Snapshot-tabanlı confirm/dispatch'te parent-state (proje archived/HSM) confirm-zamanı atomik re-validate + claim.
- Yeni DB tablosu = ZORUNLU `GRANT ALL ON {table} TO invekto`; COUNT(*) bigint → `GetInt32` InvalidCastException, `::int` cast veya `GetInt64`.
- Delivery counter FUNNEL (+1) değil PARTITION (yeni bucket +1, canlı eski −1); webhook ack "her zaman 2xx" + cross-service forward typed-catch.

## Silent-failure / audit
- Silent success > silent reject TEHLİKELİ; "specific error code döndürüyor" ≠ "silent değil" — audit-table contract varsa HER reddedilen deneme AUDIT-BEFORE-SERVE throwing-insert.
- Claimed job ASLA stranded kalmasın (HER exception typed-catch+finally); broad `catch(Exception)` YASAK (Codex `when(filter)` ile bile broad sayar), typed INV response.

## Partner / INMA (overclaim guard)
- Partner "mekanizma var/aktif" cevabı ≠ blocker ÇÖZÜLDÜ → cevabı gate'in LİTERAL koşuluna + kanıta map et, "unblock" overclaim YAPMA (3x); "webhook" deyince yeni endpoint tasarlamadan ÖNCE mevcut per-tenant webhook wiring'i kontrol et.

## Tooling (PowerShell/Bash)
- Bash tool'da commit mesajı için PowerShell here-string `@'...'@` KULLANMA → subject'e `@ ` sızar; `Out-File -Encoding utf8` git-diff redirect'i mojibake → Codex'e ASLA PowerShell redirect diff verme; `$ProgressPreference='SilentlyContinue'` ile server-exec CLIXML stderr şişmesini önle.

<!-- hot-lessons sonu -->
