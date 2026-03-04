<!-- VERSION: 5.4 | UPDATED: 2026-02-28 | Opus 4.6 Language Tuning | Persist After Compact -->
<!-- COMPACT SONRASI: Auto workflow aktif kalir. Interview -> Plan -> Dev -> Build -> /rev -> MCP Codex -> Commit -->
<!-- HARITA YAKLAŞIMI: Bu dosya kisa tutuluyor, detaylar INVEKTO_BASE.prompt.md + arch/ dosyalarinda. -->
# InvektoServis

Multi-tenant SaaS mikro servis platformu. .NET 8, PostgreSQL, React 18.

## SESSION INIT

Her session basladiginda otomatik:

1. **Auto Workflow aktif:** auto.md kurallari gecerli
2. **Kritik dosyalari oku:** `arch/session-memory.md`, `tracking/README.md`, `arch/lessons-learned.md`, `.claude/agents/INVEKTO_BASE.prompt.md`
3. **Interview ile basla:** AskUserQuestion ile gri noktalari coz

Bu adimlar plan mode dahil her session icin gecerlidir.

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
| Path | `C:\CRMs\InvektoServices\` | `E:\InvektoServices\` |
| Services | `dotnet run` | NSSM Windows Services |

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

## Agents & Skills

**Agents** (`.claude/agents/`):
- `INVEKTO_BASE.prompt.md` - Global rules v5.2 (canonical source)
- `INVEKTO_PLAN_AGENT.prompt.md` - Planning (interview + JSON plan)
- `INVEKTO_DEV_AGENT.prompt.md` - Implementation (self-review + MCP Codex)
- `INVEKTO_AUDIT_AGENT.prompt.md` - Codebase audit (Q triggers manually)

**Skills** (`.claude/commands/`):
- `auto.md` - Default workflow (otomatik, /auto yazmaya gerek yok)
- `rev.md` - Review protocol (/rev, MCP automated)
- `learn.md` - Session learnings (/learn)
- `push.md` - Git push (/push, secret scan BLOCKING)
- `aha.md` - Aha moment analizi (/aha)
- `test-ui.md` - UI testing (/test-ui, Playwright)
- `sync-check.md` - Arch/kod senkronizasyon kontrolu (/sync-check)
- `wrap.md` - Phase kapama (/wrap: tracking + learn + push + prompt)
- `session-prompt.md` - Session devam promptu (/session-prompt, sifir yan etki)

## Sub-Agents (Otomatik Tetikleme)

**Q'nun agent adi hatirlamasina GEREK YOK!** Asagidaki durumlarda ilgili agent OTOMATIK cagrilmali:

| Durum | Agent | Tetikleme |
|-------|-------|-----------|
| Build gerekli | `build-runner` | Kod degisikligi sonrasi |
| DB sorgusu gerekli | `db-query` | Veri soruldugunda |

| Agent | Model | Guvenlik |
|-------|-------|----------|
| `build-runner` | haiku | Sadece dotnet build komutlari |
| `db-query` | haiku | **SADECE SELECT** - write YASAK |

### Cross-Service Research

Birden fazla servisi arastiran sorgularda `Explore` subagent kullan.
Ana context sadece sonuclari alir, arastirma detaylarini degil.

## Hooks (Mekanik Zorlama)

`.claude/hooks/` altinda 2 lokal hook aktif + 1 global hook:

| Hook | Tetikleme | Davranis |
|------|-----------|----------|
| `build-reminder.ps1` | PostToolUse: Edit/Write (.cs) | Non-blocking - build hatirlatmasi + remediation inject |
| `invariant-check.ps1` | PostToolUse: Edit/Write (.sql/.cs) | Non-blocking - snake_case, error code, isolation kontrol |
| `~/.claude/hooks/secret-scan.ps1` | PreToolUse: Bash (GLOBAL) | **BLOCKING** (exit 2) - secret tespit ederse engeller |

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
