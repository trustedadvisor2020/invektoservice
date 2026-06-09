# Durable Decisions (ADR-lite)

> Kalıcı kararlar — `session-memory.md` OVERWRITE edildiği için rationale'ı sadece orada yaşayan kararlar kaybolur. Burası güvenli evi. Yeni kalıcı karar → buraya.

- **P0-3 dual-gate inert modeli:** Projects∩BulkSend allowlist = ∅ tutulur, CxapiSend section prod'da hiç eklenmez. Neden: erken allowlist = cross-tenant delivery ambiguity + mükerrer gönderim riski.
- **cxapi send-engine 4-PR split (PR-1 schema / PR-2 client / PR-3 cutover / PR-4 HSM):** her PR no-op/inert iner, davranış sadece gate açılınca aktif. Neden: risk küçültme.
- **PKT-14'ü 4 slice + SS-A/B/C/D'ye böl.** Neden: Codex review yükü + diff boyutu sınırı (max 200 satır/paket).
- **ON DELETE RESTRICT (SET NULL değil) composite FK'da.** Neden: tenant_id NOT NULL → SET NULL imkânsız + soft-delete-as-archive → run history korunur.
- **inert/reserved kolonlar kasıtlı (YAGNI değil):** PR-1/055 precedent; ileri slice'lar migration'sız iner.
- **Repo pre-built SPA bundle commit'ler (deploy build-on-deploy YAPMAZ):** wwwroot/app as-is publish → SPA source değişince commit'li bundle yenilenmeli.
- **Single shared Postgres = mimari ilke (db-per-service DEĞİL):** isolation = no cross-service CODE ref + her query tenant-scoped (`arch/tenant-isolation.md`). Codex'in tekrarlayan "isolation ihlali" flag'i bu yüzden FP.
- **env-var-ONLY secret policy (F0/F2/F3/F4):** `API_KEY ?? config[...]` fallback YASAK; NSSM AppEnvironmentExtra canonical.
- **Cross-origin auth = URL-bridge / SameSite=None cookie / postMessage** (localStorage origin-scoped, port farkı bile yeter).
- **Codex review zorunlu (LOW dahil, SKIP yok); hedef CODEX UTANSIN iter=0.** FORCE PASS sadece Q açık izniyle.
- **OpenAI Realtime Beta→GA migration (Beta 2026-05-07 disable):** yeni iş GA shape kullanmalı.
- **Meta App mimarisi:** single-app multi-subscription multi-tenant (per-tenant ayrı app değil).
- **INMA = pipeline source-of-truth; INSE = consumer/cache/3-way sync hub** (FEAT-PIPELINE temeli).
