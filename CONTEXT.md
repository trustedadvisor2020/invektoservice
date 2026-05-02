<!-- VERSION: 1.0 | CREATED: 2026-05-02 | Source: mattpocock/skills CONTEXT.md pattern -->
<!-- PURPOSE: Pure domain vocabulary. Implementation rules live in CLAUDE.md + INVEKTO_BASE.prompt.md + arch/. -->
<!-- WHEN TO UPDATE: Yeni domain terim kodbaza girince /wrap discipline'inde guncelle. Stale glossary > yok glossary degil. -->

# CONTEXT — InvektoServices Domain Glossary

Bu dosya **terimleri** tutar — kurallari degil. Agent her session'da "Solution Finder scoring nedir", "Shared sizinti ne demek", "Pilot Mode hangi state'te" diye tahmin etmesin diye.

## Tenant & Service Model

### Tenant
Multi-tenant SaaS musterisi. Postgres'te shared schema + `tenant_id` scoped (NOT per-tenant DB — TONIVA'dan farkli). Her query `tenant_id` filter zorunlu.

### Microservice / Mikroservis
Bagimsiz deploy edilebilir .NET 8 servisi. Ornek: `InvektoSales`, `InvektoChat`, `InvektoPulse`. Her biri kendi NSSM Windows servisi olarak prod'da kosar.

### Mikroservis Izolasyonu
Servis A, servis B'nin internal kodunu cagirmaz. Cross-service iletisim **sadece** API contract uzerinden (`arch/contracts/*.json`). Hook: `service-isolation-checker` (advisory, manuel cagri).

### Invekto.Shared
Ortak DTO'lar, constants, utilities (`Invekto.Shared/`). Tum servisler bunu reference'lar. **Shared degisirse:** full solution build + tum servisleri etkileyebilen contract degisikligi kontrolu sart.

### Shared Sizintisi (anti-pattern)
Mikroservis-spesifik bir logic'in (ornek: Sales-only fiyatlama) Shared'a kacmasi. Shared = sadece **gercekten ortak** olan kod. Coklu service tarafindan kullanilan = shared, tek service tarafindan kullanilan = AYNI service icinde kalir.

## AI / Embedding Pipeline

### pgvector
PostgreSQL extension, vector similarity search. InvektoServices'te embedding storage + query backend.

### Embedding
Metin/dokuman → sayisal vektor (Gemini multimodal). Storage: pgvector kolonu `embedding vector(N)`.

### Solution Finder
AI-driven hizmet onerisi motoru. Frontend: InvektoWebsite. Backend: InvektoServices.
- **Input:** Musteri serbest metin sorgusu (problem tanimi)
- **Output:** Sirali hizmet/paket onerisi listesi
- **Scoring:** embedding similarity + metadata filter + business rules (tier, fiyat, sektor)

### Tool-calling
LLM'in deterministic hesap (fiyat, tarih, ROI) icin code-tool cagirmasi. Hallucination guard.

### Retrieval-Gated
LLM cevap verirken sadece retrieved context'ten konusur. "Bilmiyorsam soyle" system prompt zorunlu.

## Architecture / Source-of-Truth

### arch/ Folder (Architecture Truth)
**Kod yazmadan once arch/ oku.** Kural celisirse arch kazanir; kodu fix.
- `arch/db/*.sql` — schema truth (yeni tablo/kolon → once buraya)
- `arch/contracts/*.json` — API contracts (asla schema icat etme)
- `arch/errors.md` — error codes (`INV-xxx`)
- `arch/endpoints.md` — yeni endpoint kataloglu
- `arch/docs/microservice-guide.md` — yeni servis nasil eklenir
- `arch/quality-grades.md` — kalite durumu
- `arch/contracts/plan-schema.json` — Plan JSON contract
- `arch/specs/` — feature spec template (`_TEMPLATE.md`)

### Plan JSON
`/auto` Phase 2 ciktisi, structured plan. Contract: `arch/contracts/plan-schema.json`. Implementation buradan dogrudan kosturulabilir.

### Pilot Mode (2026-04-21+)
Aktif execution moda. `tracking/pilot-launch-roadmap.md` = master queue, sira otoriter.
- Bos onaydan paket yakalama: ilk `PENDING` pakete sor
- Q overrides: `SKIP P{N}`, `PAUSE`, `REORDER`
- Atlamak yasak — sira roadmap'tedir

## Code Quality Discipline

### Codex Review (UTANSIN)
**LOW dahil tum risk seviyeleri zorunlu.** Skip yok. `/rev` icinde MCP `mcp__codex-review__codex_review` cagrir. Verdict = PASS sart, FAIL → iterate.
- Iteration = 0 hedefi. Self-review CQ1-CQ8 + AQ1-AQ6 ile pre-empt.

### CODEX UTANSIN Pattern
Pre-commit self-review checklist (`INVEKTO_BASE.prompt.md` sections 2-4). 14 madde — code quality (CQ) + architecture quality (AQ). Codex review'a gitmeden once Q kendi kontrolunu yapar.

### Self-Review CQ1-CQ8
Kod-level checklist: edge cases, error handling, naming, readability, performance, security, testability, idiomatic C#.

### AQ1-AQ6
Architecture-level checklist: contract uyumu, isolation, error code dogrulu, snake_case, hardcode yok, mevcut pattern reuse.

## DB Conventions

### snake_case
**Tum Postgres kolon adlari** snake_case. C# DTO'lari ozellige map'lenir (`[Column("user_id")]`). Hook tarafindan enforce.

### Schema Source-of-Truth
`arch/db/*.sql` — yeni tablo/kolon **once buraya, sonra C#**. Migration order = file naming.

### Contracts Asla Icat Edilmez
`arch/contracts/*.json` zaten varsa kullan. Yeni alan gerek → ONCE Q'ya sor (shared contract degisikligi tum servisleri etkiler).

## Error Codes

`arch/errors.md` — format `INV-xxx`. Yeni hata: kod once buraya kayit, sonra throw. User-friendly mesaj zorunlu (specific + actionable + localized).

## Workflow Vocabulary

### Auto Workflow (DEFAULT)
`/auto` yazmaya gerek yok. Akis: Interview → Plan JSON → Dev (build check) → /rev → MCP Codex → Commit. Compact sonrasi devam eder.

### Worktree Session
`invs` komutu yeni izole worktree: `c:\CRMs\InvektoServices-session-{ISO}\`, branch `session/{ISO}`. /wrap merge sonrasi otomatik temizler.

### /evolve, /instinct-status
Yuksek confidence pattern → skill'e donusur (/evolve). Ogrenilen pattern ozeti (/instinct-status). Hedef klasorler: `.claude/skills/code/`, `.claude/skills/risk/`, `.claude/skills/process/`.

### Sub-Agent Routing
- `build-runner` (haiku) — yalniz `dotnet build`
- `db-query` (haiku) — **SADECE SELECT**, write yasak
- `service-isolation-checker` (haiku) — Shared/cross-service kontrol
- `code-simplifier` (opus) — behavior-preserving refactor

### Lesson Lookup via Sub-Agent
Karmasik debug oncesi Explore subagent ile `arch/lessons-learned.md` + `arch/lessons-learned-archive.md` taranir. Main context kirletilmez — sadece ozet doner (~300 word).

## Deployment

### Dev PC ≠ Production Server
- **Dev PC:** `C:\CRMs\InvektoServices\`, `dotnet run`
- **Production:** `C:\Invekto\{Service}\current\`, NSSM Windows Service `Invekto-{Service}`
- NSSM binary: `C:\Invekto\nssm.exe`

> Eski `E:\InvektoServices\` path'i **GECERSIZ** (2026-04-10 cleanup).

### Deploy Truth
`.claude/commands/deploy.md` + `.claude/commands/deploy-info.md` — primary `/deploy` komutu.

### Hooks (Mekanik Zorlama)
6 lokal + 1 global:
- `session-init.ps1`, `build-reminder.ps1`, `invariant-check.ps1`, `check-shared-microservice.ps1`, `deploy-verify.ps1` — non-blocking
- `~/.claude/hooks/secret-scan.ps1` — **BLOCKING** (exit 2)
- `~/.claude/hooks/block-dangerous-git.ps1` — **BLOCKING** (exit 2, 2026-05-02 itibariyla aktif — destructive git ops)

## Ignored Folders

- `temp/` — gecici, git'e ekleme
- `deploy_output/` — build output, secret leak vektoru, `git add -A` oncesi dikkat

## Glossary Maintenance

**Bu dosya senin icin degil — gelecekteki agent oturumlari icin.** Yeni terim:
1. /wrap discipline'i bu dosyaya 1 paragraf ekler
2. .NET / Postgres / pgvector standart terimlerini buraya kopyalama — link yeter
3. **Implementation kurali yazma** — kurallar CLAUDE.md / arch/ / INVEKTO_BASE.prompt.md'de.

Source pattern: `mattpocock/skills` — engineering/grill-with-docs CONTEXT.md format.
