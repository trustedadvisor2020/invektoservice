<!-- VERSION: 5.1 | UPDATED: 2026-02-16 | Persist After Compact | Auto Workflow Active | MCP Codex Review -->
<!-- COMPACT SONRASI: Auto workflow aktif kalir. Interview -> Plan -> Dev -> Build -> /rev -> MCP Codex -> Commit -->
# InvektoServis

Multi-tenant SaaS mikro servis platformu. .NET 8, PostgreSQL, React 18.

## SESSION INIT (CRITICAL - HER SESSION BASINDA)

**Her session basladiginda (plan modunda bile) su adimlar OTOMATIK uygulanir:**

1. **Auto Workflow Aktif:** Ne istenirse istensin, auto.md kurallari gecerli
2. **Kritik Dosyalari Oku:**
   - `arch/session-memory.md` -> Son durumu anla
   - `arch/active-work.md` -> Devam eden isler
   - `arch/lessons-learned.md` -> Tekrarlanan hatalar
   - `.claude/agents/INVEKTO_BASE.prompt.md` -> Global kurallar
3. **Interview ile Basla:** Q ne isterse, once AskUserQuestion ile gri noktalari coz

**BU ADIMLAR ATLANAMAZ!** Plan mode veya baska mode farketmez.

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

## Proje Yapisi

```
InvektoServices/                     # Root (C:\CRMs\InvektoServices)
├── InvektoServis.sln                # Solution file
├── src/
│   ├── Invekto.Backend/             # API Gateway + Dashboard
│   ├── Invekto.Automation/          # Chatbot flows, FAQ, auto-reply
│   ├── Invekto.AgentAI/             # AI reply suggestion (OpenAI)
│   ├── Invekto.Knowledge/           # RAG, document chunks, pgvector
│   ├── Invekto.Outbound/            # Broadcast, templates, triggers
│   ├── Invekto.WhatsAppAnalytics/   # WA message analysis pipeline
│   └── Invekto.Shared/              # Shared DTOs, error codes, utils
├── arch/                            # Architecture docs (source of truth)
│   ├── contracts/                   # JSON schemas, plan schema
│   ├── db/                          # SQL migrations per service
│   ├── plans/                       # Plan JSONs + diffs
│   ├── deploy/                      # Deploy scripts, bat/ps1
│   ├── session-memory.md            # Session context
│   ├── active-work.md               # In-progress task tracker
│   ├── lessons-learned.md           # Common mistakes and patterns
│   ├── errors.md                    # Error codes (INV-*)
│   ├── endpoints.md                 # Endpoint registration rules
│   ├── logging.md                   # Log format
│   └── docs/                        # Technical documents
└── .claude/
    ├── agents/                      # Agent prompts (BASE, PLAN, DEV, AUDIT)
    └── commands/                    # Skills (auto, rev, learn, push, aha, test-ui)
```

### Mikro Servis Kurallari

1. **Bagimsizlik:** Her servis kendi basina deploy edilebilir
2. **Izolasyon:** Servisler arasi iletisim sadece API/Event uzerinden
3. **Kendi DB'si:** Her servis kendi tablolarina sahip (ayni PostgreSQL instance)
4. **Shared:** Ortak kod `Invekto.Shared` uzerinden paylasilir

## Environment Separation

**Dev PC and Production Server are DIFFERENT machines!**

| Aspect | Dev PC | Production Server |
|--------|--------|-------------------|
| Machine | Developer's local PC | Remote Server |
| OS | Windows 11 | Windows Server |
| Services | `dotnet run` | NSSM Windows Services |
| Path | `C:\CRMs\InvektoServices\` | `E:\InvektoServices\` |

**Windows PowerShell Rules (CRITICAL):**
- **ALWAYS use PowerShell wrapper for Bash tool:** `powershell -NoProfile -Command "..."`
- NEVER use raw bash/Linux syntax on Windows
- `&&` chaining does NOT work - use `;` to chain commands
- Example: `powershell -NoProfile -Command "dotnet build C:\CRMs\InvektoServices\InvektoServis.sln --no-restore -v q"`

## Build Commands

| Task | Command |
|------|---------|
| Full solution build | `powershell -NoProfile -Command "dotnet build C:\CRMs\InvektoServices\InvektoServis.sln --no-restore -v q"` |
| Single service build | `powershell -NoProfile -Command "dotnet build C:\CRMs\InvektoServices\src\Invekto.{Name}\Invekto.{Name}.csproj --no-restore -v q"` |
| Run a service | `powershell -NoProfile -Command "dotnet run --project C:\CRMs\InvektoServices\src\Invekto.{Name}\Invekto.{Name}.csproj"` |

- Shared degistiyse -> Full solution build
- Build fails -> fix immediately before continuing

> Auto workflow otomatik uygulanir - `/auto` yazmaya gerek yok.
>
> **COMPACT SONRASI:** Auto workflow aktif kalir. Session sifirlanra bile tum kod degisiklikleri auto.md kurallarini takip eder: Interview -> Plan -> Dev -> Build -> /rev -> Codex -> Commit

## #1 KURAL: CODEX UTANSIN

> **"Kod o kadar ince, dikkatli ve kusursuz yazilacak ki, Codex review'a baktiginda utansin.
> Ilk adimda hersey PASS olacak. Bu bir dilek degil, KURAL."** -- Q

> **Canonical source:** `INVEKTO_BASE.prompt.md` CODEX UTANSIN DOKTRINI section.

Her satir yazilmadan ONCE 5 soru cevaplanir: (1) hata durumu, (2) null/unexpected, (3) 10K concurrent, (4) pattern uyumu, (5) Codex soru sorar mi? Cevap yoksa o satir YAZILMAZ.

**Sifir tolerans:**
- Bos catch, broad `catch(Exception)`, null-forgiving `!.`, N+1 query, scope disi degisiklik = **aninda FAIL**
- "Sonra duzeltirim" = YASAK. Kod yazilirken dogru yazilir.
- Codex review'i gereksiz hissettirmek = gercek basari metrigi

**Basari metrigi:** `/rev` -> Codex verdict = PASS, iteration = 0

---

## Enterprise Code Quality Standards

**MANDATORY for ALL code written in this codebase:**

1. **Enterprise-Grade Quality:** All code must be production-ready, not just "working". Consider edge cases, error handling, performance, and maintainability.

2. **System Integrity First:** Never just "complete the task". Every change must:
   - Not break existing functionality
   - Improve overall system health where possible
   - Consider impact on other components/services

3. **Rule & Pattern Compliance:** All code must follow:
   - Existing codebase patterns
   - arch/ documentation rules
   - Contract schemas exactly as defined

4. **Ask Q When Unclear:**
   - Logic seems wrong or inconsistent -> ASK Q
   - Missing information to implement correctly -> ASK Q
   - Multiple valid approaches exist -> ASK Q
   - Something "smells wrong" -> ASK Q

5. **Q Interview (MANDATORY):**
   - Her kod degisikligi oncesi interview yap
   - **Konu ne kadar acik gorunurse gorunsun, TUM gri noktalar cozulene kadar sor**
   - "Acik gorunuyor" =/= "Soru sormaya gerek yok"
   - Her varsayim = potansiyel yanlis yon
   - Q "skip interview" demeden koda gecme
   - **SEYTANIN AVUKATLIGI (PP-006):** Q'yu challenge et, alternatifler sun, edge case'leri sor, trade-off'lari belirt - Q "uyandirilmak" istiyor, pasif kalmak DEGIL!

6. **Heavy Load Ready:** System will serve **thousands of concurrent users** under stress. Code must:
   - Handle concurrent access safely
   - Avoid memory leaks and resource exhaustion
   - Be optimized for performance
   - Degrade gracefully under load

7. **User-Friendly Error Messages:** Errors must be:
   - **Specific:** Not "An error occurred" but "Service 'UserAuth' failed: Token expired"
   - **Actionable:** Tell user what they can do to fix it
   - **Localized context:** Include relevant IDs, names, values
   - Use error codes from `arch/errors.md`

## Self-Review Protocol (Kod Yazarken Otomatik)

> **Canonical source:** `INVEKTO_BASE.prompt.md` SELF-REVIEW PROTOCOL section.
> Tam CQ1-CQ8 + AQ1-AQ6 tablosu INVEKTO_BASE'de tanimlanir.

**Her dosya edit sonrasi CQ1-CQ8 + AQ1-AQ6 kontrol et.**
FAIL olan varsa -> Codex'e gondermeden ONCE duzelt.
Self-review sonucunu Q'ya kisa goster: `Self-Review: 14/14 PASS` veya `Self-Review: CQ2 FAIL - fixing...`
Bu, Codex review'i ORTADAN KALDIRMAZ - sadece ilk filtreleme katmani.

## Critical Rules

### Ignored Folders

- **`temp/`** - Gecici dosyalar. Git'e ekleme, kod yazarken dikkate alma.
- **`deploy_output/`** - Build output. Secret leak vektoru - git add -A oncesi dikkat.

### SINGLE SOURCE OF TRUTH: DB Schema (MOST CRITICAL)

**Her servis icin DB semasi icin tek gercek kaynak: `arch/db/*.sql`**

| DB Degisikligi | Sema dosyasina YANSIT |
|----------------|------------------------|
| Yeni tablo | CREATE TABLE ekle |
| Yeni kolon | CREATE TABLE + ALTER migration |
| Kolon silme | CREATE TABLE'dan cikar |
| Yeni index | CREATE INDEX ekle |
| Yeni FK/constraint | ADD CONSTRAINT ekle |

**KURAL:** Kod yazarken yeni tablo/kolon kullanacaksan -> **ONCE semaya ekle, SONRA kodu yaz!**

### DB-CODE SYNC CHECK

**Kod ve DB senkronize olmayabilir!** Her yeni ozellik yazarken:

1. **Tablo var mi?** - Kodda kullanilan tablo DB'de gercekten var mi kontrol et
2. **Kolon var mi?** - Kullanilan her kolon DB'de mevcut mu kontrol et
3. **Veri tipi dogru mu?** - Kolon tipleri kod beklentisiyle uyusuyor mu
4. **Migration gerekli mi?** - Yeni tablo/kolon lazimsa once migration yaz

**ASLA varsayma - her zaman kontrol et!**

### SNAKE_CASE CONVENTION (DB & CODE)

**Tum DB kolon adlari `snake_case` olmali!** PascalCase veya camelCase YASAK.

### Mikro Servis Izolasyonu

**Bir serviste yapilan degisiklik diger servisleri ETKILEMEZ!**

| Soru | Cevap |
|------|-------|
| Bu degisiklik hangi servis(ler)i etkiliyor? | Belirle |
| Etkilemedigim servisler icin regression riski var mi? | Kontrol et |
| Shared kod degisiyorsa | TUM etkilenen servisleri test et |

---

1. **DB:** PostgreSQL 16 + pgvector. Schema: `arch/db/*.sql`
2. **Auth:** JWT (Invekto.Shared/Auth/JwtGenerator.cs) + Backend proxy
3. **Errors:** Use `arch/errors.md` codes (INV-xxx)
4. **Contracts:** Never invent schema. Use `arch/contracts/*.json`

## Architecture Reference

**KURAL: Kod yazmadan ONCE ilgili `arch/` dokumanini oku!**

| Yazacagin Kod | Once Oku |
|---------------|----------|
| DB degisikligi | `arch/db/` + servis semasi |
| Error handling | `arch/errors.md` |
| API contract | `arch/contracts/` |
| Yeni endpoint | `arch/endpoints.md` |
| Yeni servis | `arch/docs/microservice-guide.md` |

## Agent Prompts

All agents in `.claude/agents/`:
- `INVEKTO_BASE.prompt.md` - Global rules v5.1 (canonical source for Bootstrap + PP-006 + Self-Review + MCP Codex)
- `INVEKTO_PLAN_AGENT.prompt.md` - Planning v5.0 (interview + JSON plan)
- `INVEKTO_DEV_AGENT.prompt.md` - Implementation v5.1 (self-review + paket dev + MCP Codex)
- `INVEKTO_AUDIT_AGENT.prompt.md` - Codebase audit (Q triggers manually)

Skills in `.claude/commands/`:
- `auto.md` - Default workflow v5.1 (otomatik uygulanir, /auto yazmaya gerek yok, MCP Codex)
- `rev.md` - Review protocol v5.1 (/rev komutu, MCP automated)
- `aha.md` - Detayli aha moment analizi (`/aha` ile cagrilir)
- `learn.md` - Session learnings kayit v2.0 (`/learn` ile cagrilir, auto mode destekli)
- `push.md` - Git push shortcut (`/push` ile cagrilir, secret scan BLOCKING)
- `test-ui.md` - Semi-autonomous UI testing (`/test-ui` ile cagrilir, Playwright + Python)

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

---

## Workflow (v5.1 - 8 Paket Stratejisi + MCP Codex Review)

> **PERSIST AFTER COMPACT:** Bu bolum session sifirlanra bile gecerlidir.

**AUTO WORKFLOW = DEFAULT DAVRANIS**

**Her kod degisikligi otomatik olarak auto.md kurallarini takip eder.**
`/auto` yazmaya GEREK YOK - sadece ne istedigini soyle.

**v5.1 Farki (2026-02-16):**
- **MCP Codex Review:** Copy-paste yerine `mcp__codex-review__codex_review` tool ile otomatik
- **Paket bazli yurutme:** Tekli GR dongusu yerine 2-3 GR/paket (v5.0'dan devam)
- Interview: Paket scope'unda AskUserQuestion ile (tek interview tum GR'ler icin, max 4 soru/batch)
- Plan JSON: Paket bazli (birden fazla GR tek plan'da, `packet_id` + `gr_list`)
- Codex review: Paket bazli (tum GR'lerin diff'i tek MCP call'da)
- Paket ici GR'ler arasi interview/review YOK, sadece build check

**Paket Akisi:**
1. Q paket ister (veya siradaki paket baslar)
2. AskUserQuestion ile paket scope'unda interview (tum GR'ler icin)
3. Agent risk'i belirler (LOW/MEDIUM/HIGH/CRITICAL)
4. Plan JSON olusturulur (paket bazli, tum GR'ler tek plan'da)
5. Implement (GR'ler sirali, her GR sonrasi build check)
6. /rev -> MCP codex_review (OTOMATIK) -> PASS/FAIL

**8 Paket Referansi:** `arch/active-work.md` -> Execution Queue

**Review Akisi (v5.1 - MCP Automated):**
```
DevAgent kod yazar -> Self-Review -> Build PASS
    |
DevAgent /rev calistirir (TUM risk seviyeleri)
    |
MCP codex_review tool OTOMATIK cagrilir (copy-paste YOK)
    |
Codex API structured JSON doner (CQ1-8 + CoVe + verdict)
    |
DevAgent verdict'i isler, Q'ya ozet gosterir
    |
PASS -> commit -> DONE
FAIL -> fix -> /rev (max 3 iter)
```

**HARD RULE:** /rev sonrasi Codex review yapilmadan ASLA commit yapilamaz!

**Escalation Kategorileri (3 iter sonrasi):**
| Kategori | Aciklama |
|----------|----------|
| DECISION_CONFLICT | Tasarim karari gerekiyor |
| TOOL_LIMITATION | Arac/framework limiti |
| PLAN_ASSUMPTION_WRONG | Plan varsayimi yanlis |
| SCOPE_INSUFFICIENT | Scope yetersiz |
| ARCHITECTURE_CONFLICT | Mimari celiski |

**Q'nun yapacagi:** Interview cevapla -> Plan onayla -> Codex sonucunu izle -> Override gerekirse FORCE PASS/SKIP CODEX.

**Risk-Based Trigger:**
| Risk | Build PASS Sonrasi |
|------|-------------------|
| LOW | /rev -> MCP Codex (otomatik) |
| MEDIUM | /rev -> MCP Codex (otomatik) |
| HIGH | /rev -> MCP Codex (otomatik) |
| CRITICAL | /rev -> MCP Codex + Q onay bekle |

## Execution

- Execute without interruption for clear tasks
- Read arch/ before any task
- If rule conflicts with code, fix code (arch is truth)
- No tests, no docs unless requested

**Execution discipline:**
- Treat any **surprise** (unexpected error, missing file, different output) as a signal your mental model is wrong. Stop, explain to Q what surprised you, and update your plan.
- If you lose track of the original goal or constraints, say so explicitly (`"I'm losing the thread"`) and reconstruct the goal from this file + the latest instructions from Q before continuing.

## Ask Before Acting

**MUST ask Q if:**
- Requirements unclear or ambiguous
- Multiple valid approaches exist
- New pattern not in existing codebase
- Changing shared contracts/schemas
- Adding new dependencies
- Modifying auth/security logic
- Adding new microservice

**Proceed directly (auto workflow implicit):**
- Clear instruction = direkt basla, auto workflow otomatik uygulanir
- Q override komutlari: `STOP`, `SKIP CODEX`, `FORCE PASS` (sadece Q'nun acik izni ile)

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

**Full rules defined in `INVEKTO_BASE.prompt.md` (canonical source for Bootstrap, PP-006, Self-Review, Build Commands).**
