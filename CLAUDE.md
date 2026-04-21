<!-- VERSION: 5.6 | UPDATED: 2026-04-11 | TONIVA auto parity port: review-policy v3.2, /evolve + /instinct-status, code-simplifier, service-isolation-checker, check-shared-microservice + deploy-verify hooks -->
<!-- COMPACT SONRASI: Auto workflow aktif kalir. Interview -> Plan -> Dev -> Build -> /rev -> MCP Codex -> Commit -->
<!-- HARITA YAKLAŞIMI: Bu dosya kisa tutuluyor, detaylar INVEKTO_BASE.prompt.md + arch/ dosyalarinda. -->
# InvektoServices

Multi-tenant SaaS mikro servis platformu. .NET 8, PostgreSQL, React 18.

## SESSION INIT

Her session basladiginda otomatik:

1. **Auto Workflow aktif** — global `auto` skill kurallari (`~/.claude/skills/auto`)
2. **Kritik dosyalari oku (TEK STANDART LISTE, shared v6.1 uyumlu):**
   - `arch/session-memory.md` (son durum + execution queue + recently completed)
   - `tracking/pilot-launch-roadmap.md` **(PILOT MODE AKTIF — execution queue + devam protokolu)**
   - `tracking/README.md` (paket durumu)
   - `arch/lessons-learned.md` (son 100 satir)
   - `.claude/agents/INVEKTO_BASE.prompt.md` (global rules)
3. **PILOT MODE DAVRANIS (2026-04-21 itibariyla):**
   - `tracking/pilot-launch-roadmap.md` Master Queue'da ilk `PENDING` paketi bul
   - Q'ya "Siradaki: P{N} {slug} — baslayalim mi?" sor
   - Onay → `/auto` workflow (interview → plan → dev → build → /rev → commit)
   - Paket DONE → roadmap Status=DONE + session-memory Recently Completed + `/clear` oner
   - **Atlamak yasak** — sira roadmap'tedir. Q override icin `SKIP P{N}` / `PAUSE` / `REORDER` komutlari kullanir.
4. **Interview ile basla:** AskUserQuestion ile gri noktalari coz (roadmap paketine ozel sorular)

> **active-work.md KULLANILMIYOR** (shared v6.1, 2026-03-04). Execution queue session-memory.md icinde.
> **Pilot Mode:** Tum tracking + queue otoritesi `tracking/pilot-launch-roadmap.md`'dedir. Session-memory son durum detayi.

## Naming & Roles

- The developer is **Q**. Always refer to Q in comments, logs, and explanations.
- You are a coding agent working inside the **InvektoServis** monorepo. Assume **no prior memory** outside what is in this repository and this file.
- When in doubt about requirements or tradeoffs, explicitly ask Q before proceeding with risky or irreversible changes.

## Tech Stack

| Component | Stack |
|-----------|-------|
| Runtime | .NET 8 (C#) |
| Database | PostgreSQL 16 + pgvector |
| Frontend | React 18 + TypeScript + Vite |
| Solution | `InvektoServis.sln` (root) |
| Shared | `Invekto.Shared` (DTOs, constants, utilities) |

## Environment & Build

**Dev PC and Production Server are DIFFERENT machines!**

| Aspect | Dev PC | Production Server |
|--------|--------|-------------------|
| Path | `C:\CRMs\InvektoServices\` | `C:\Invekto\{Service}\current\` |
| NSSM binary | — | `C:\Invekto\nssm.exe` |
| Services | `dotnet run` | NSSM Windows Services (`Invekto-{Service}`) |

> **Not:** Eski `E:\InvektoServices\` path'i GEÇERSİZDİR (2026-04-10 cleanup). Tek gerçek deploy truth'u için `.claude/commands/deploy.md` ve `.claude/commands/deploy-info.md` oku.

**Windows PowerShell:** Detayli kurallar `shared-lessons.md`'de.
- `powershell -NoProfile -Command "..."` wrapper kullan
- `&&` calismaz → `;` ile chain et

> **Build:** `powershell -NoProfile -Command "dotnet build C:\CRMs\InvektoServices\InvektoServis.sln --no-restore -v q"`
> Shared degistiyse -> Full solution build. Build fails -> fix immediately. Full: `INVEKTO_BASE.prompt.md` section 7.

## Code Quality

> **Canonical:** `INVEKTO_BASE.prompt.md` sections 2-4 (CODEX UTANSIN + Enterprise Standards + Self-Review CQ1-CQ8 + AQ1-AQ6).
> Basari metrigi: `/rev` → Codex verdict = PASS, iteration = 0
> **Codex review yapilmadan commit yapilamaz** (LOW dahil tum risk seviyeleri).

## Critical Rules

### Ignored Folders

- **`temp/`** - Gecici dosyalar. Git'e ekleme, kod yazarken dikkate alma.
- **`deploy_output/`** - Build output. Secret leak vektoru - git add -A oncesi dikkat.

> **DB, snake_case, microservice isolation kurallari:** `INVEKTO_BASE.prompt.md` CRITICAL RULES section.
> **Schema source of truth:** `arch/db/*.sql` | **Error codes:** `arch/errors.md` (INV-xxx)
> **Contracts:** Never invent schema. Use `arch/contracts/*.json`

## Architecture Reference

**KURAL: Kod yazmadan ONCE ilgili `arch/` dokumanini oku!**

| Yazacagin Kod | Once Oku |
|---------------|----------|
| Paket durumu | `tracking/README.md` + per-paket dosya |
| DB degisikligi | `arch/db/` + servis semasi |
| Error handling | `arch/errors.md` |
| API contract | `arch/contracts/` |
| Yeni endpoint | `arch/endpoints.md` |
| Yeni servis | `arch/docs/microservice-guide.md` |
| Kalite durumu | `arch/quality-grades.md` |
| Fikir/backlog | `tracking/roadmap.md` (Backlog section) |
| Yeni feature spec | `arch/specs/` + `_TEMPLATE.md` |
| Codex review context | `arch/codex-context.md` |
| Review policy | `arch/review-policy.md` |

## Agents, Commands & Skills

**Local Agents** (`.claude/agents/`):
- `INVEKTO_BASE.prompt.md` — Global rules (canonical source)
- `INVEKTO_PLAN_AGENT.prompt.md` — Planning (interview + JSON plan)
- `INVEKTO_DEV_AGENT.prompt.md` — Implementation (self-review + MCP Codex)
- `INVEKTO_AUDIT_AGENT.prompt.md` — Codebase audit (Q triggers manually)
- `build-runner.md`, `db-query.md` — Helper agents (haiku)
- `code-simplifier.md` — Karmaşık kodu refactor eder (opus, manuel çağır)
- `service-isolation-checker.md` — Mikro servis izolasyonu doğrular (haiku, Shared değiştiğinde manuel çağır)

**Local Commands** (`.claude/commands/`, slash-invoked):
- `/brief` — Session brief (okur: lessons-learned + session-memory)
- `/codex` — Manuel Codex review trigger (MCP fallback)
- `/deploy` — Production deploy (NSSM services, 11 tenant)
- `/chat-export` — Sohbet export
- `/sync-check` — Arch/kod senkronizasyon kontrolü
- `/test-ui` — UI testing (Playwright)
- `/evolve` — Yüksek confidence pattern'i skill'e dönüştürür
- `/instinct-status` — Öğrenilmiş pattern'lerin özetini gösterir

**Local Skills** (`.claude/skills/`, repo'da):
- `rollout/SKILL.md` — Schema migration + multi-tenant backfill rollout (single Postgres, tenant_id scoped)
- `code/`, `risk/`, `process/` — `/evolve` komutunun hedef klasörleri (henüz evolved pattern yok)

**Global Skills** (`~/.claude/skills/`, repo'da değil — kullanıcı seviyesi):
`auto`, `rev`, `wrap`, `aha`, `push`, `learn`, `session-prompt`.
Bu skill'ler tüm projelerde ortak çalışır, local karşılığı YOKTUR.

> **Referans:** `deploy-info.md` (commands/ içinde) deploy teknik detaylarını taşıyan reference-only dosyadır; primary komut `/deploy`dir.

## Sub-Agents (Advisory, manuel cagrilir)

| Durum | Agent | Model | Guvenlik |
|-------|-------|-------|----------|
| Build gerekli (kod degisikligi sonrasi) | `build-runner` | haiku | Sadece `dotnet build` komutlari |
| DB sorgusu gerekli (Postgres) | `db-query` | haiku | **SADECE SELECT** — write YASAK |
| Shared/DTO veya cross-service dokunulduysa | `service-isolation-checker` | haiku | Read-only; isolation ihlali tespit |
| Karmaşık kod refactor gerekli | `code-simplifier` | opus | Behavior-preserving refactor |

> **ADVISORY, enforce DEGIL:** Bu agent'lar hook ile otomatik cagrilmaz. Gerektiginde Claude Code manuel `Agent` tool'u ile spawn eder. Duruma gore agent davranisi secilir.

### Cross-Service Research

Birden fazla servisi arastiran sorgularda `Explore` subagent kullan.
Ana context sadece sonuclari alir, arastirma detaylarini degil.

## Hooks (Mekanik Zorlama)

`.claude/hooks/` altinda 5 lokal hook aktif + 1 global hook + 1 opsiyonel:

| Hook | Tetikleme | Davranis |
|------|-----------|----------|
| `session-init.ps1` | SessionStart | Non-blocking - kritik dosya hatirlatma, workflow durumu |
| `build-reminder.ps1` | PostToolUse: Edit/Write (.cs) | Non-blocking - build hatirlatmasi + remediation inject |
| `invariant-check.ps1` | PostToolUse: Edit/Write (.sql/.cs) | Non-blocking - snake_case, error code, isolation kontrol |
| `check-shared-microservice.ps1` | PostToolUse: Edit/Write | Non-blocking - Shared/DTO veya cross-service uyarısı |
| `deploy-verify.ps1` | PostToolUse: invekto-ops server-upload/exec | Non-blocking - production path, NSSM, dev-forbidden kontroller |
| `~/.claude/hooks/secret-scan.ps1` | PreToolUse: Bash (GLOBAL) | **BLOCKING** (exit 2) - secret tespit ederse engeller |
| `dotnet-check.sh` | (opsiyonel, bağlı değil) | Real dotnet build per-service (yavaş, Q isterse settings.json'a ekle) |

## Workflow Reference

Auto workflow aktif - `/auto` yazmaya gerek yok.
Full v5.1 workflow: `auto.md` + `INVEKTO_BASE.prompt.md` section 1.
**Paket akisi:** Interview -> Plan JSON -> Dev (build check) -> /rev -> MCP Codex -> Commit
**COMPACT SONRASI:** Auto workflow aktif kalir, tum kurallar gecerli.

## Session Management

- Session continuation prompt veya next-session prompt istendiginde **SADECE prompt metnini uret**. Dosya okuma, arastirma veya implementation baslatma - Q acikca istemedikce.
- Q spesifik bir deliverable istediginde (prompt, checklist, plan, query) **direkt uret**. Codebase'de gezinme, dosya okuma veya tangential is baslatma - Q acikca istemedikce.

## Execution

- Execute without interruption for clear tasks
- Read arch/ before any task
- If rule conflicts with code, fix code (arch is truth)
- No tests, no docs unless requested

**Execution discipline:**
- Treat any **surprise** (unexpected error, missing file, different output) as a signal your mental model is wrong. Stop, explain to Q what surprised you, and update your plan.
- If you lose track of the original goal or constraints, say so explicitly (`"I'm losing the thread"`) and reconstruct the goal from this file + the latest instructions from Q before continuing.

**Context discipline:**
- Paket tamamlaninca Q'ya `/clear` oner (context temizligi)
- 3+ paket biriktiyse PROAKTIF `/clear` oner
- `/clear` sonrasi auto workflow SESSION BOOTSTRAP ile otomatik aktif olur

## Priority Hierarchy

Hedef ile kisit celisdiginde: **Safety > System Integrity > Q'nun Talimati > Hiz**
Goal completion asla safety/integrity'yi override edemez. Belirsizlikte dur ve sor.

## Ask Before Acting

**MUST ask Q if:**
- Requirements unclear or ambiguous
- Multiple valid approaches exist
- New pattern not in existing codebase
- Changing shared contracts/schemas
- Adding new dependencies
- Modifying auth/security logic
- Adding new microservice

**Proceed directly:** Clear instruction = direkt basla, auto workflow otomatik.
**Q overrides:** `STOP`, `SKIP CODEX`, `FORCE PASS` (sadece Q'nun acik izni ile)

## Architecture Compliance

**Before writing code:**
1. Read relevant arch/ files
2. Check existing patterns in codebase
3. Verify contract fields exist in arch/contracts/
4. Use error codes from arch/errors.md
5. Never invent new schemas - ask if needed
6. **Read `arch/contracts/plan-schema.json` BEFORE creating plan JSON**

**Code review checklist:**
- [ ] Uses existing patterns, not new inventions
- [ ] Error codes match arch/errors.md
- [ ] No hardcoded endpoints/ports
- [ ] Mikro servis izolasyonu korunuyor
- [ ] Shared kod degisikligi varsa tum servisler kontrol edildi

---

**Shared Lessons:** `C:\Users\taner\.claude\workflow\shared-lessons.md` — cross-project kurallar ve Q tercihleri.

**Full rules: `INVEKTO_BASE.prompt.md` (canonical source for Bootstrap, PP-006, Self-Review, Build Commands, CODEX UTANSIN).**
