<!-- VERSION: 1.0 | UPDATED: 2026-02-01 | Persist After Compact | Auto Workflow Active -->
<!-- COMPACT SONRASI: Auto workflow aktif kalır. Interview → Plan → Dev → Build → /rev → Codex → Commit -->
# InvektoServis

Başka sistemler tarafından kullanılacak, kendi içinde bağımsız mikro servisler barındıran platform.

## 🚀 SESSION INIT (CRITICAL - HER SESSION BAŞINDA)

**Her session başladığında (plan modunda bile) şu adımlar OTOMATİK uygulanır:**

1. **Auto Workflow Aktif:** Ne istenirse istensin, auto.md kuralları geçerli
2. **Kritik Dosyaları Oku:**
   - `arch/session-memory.md` → Son durumu anla
   - `arch/active-work.md` → Devam eden işler
   - `arch/lessons-learned.md` → Tekrarlanan hatalar
   - `.claude/agents/INVEKTO_BASE.prompt.md` → Global kurallar
3. **Interview ile Başla:** Q ne isterse, önce AskUserQuestion ile gri noktaları çöz

**BU ADIMLAR ATLANAMAZ!** Plan mode veya başka mode farketmez.

## Naming & Roles

- The developer is **Q**. Always refer to Q in comments, logs, and explanations.
- You are a coding agent working inside the **InvektoServis** monorepo. Assume **no prior memory** outside what is in this repository and this file.
- When in doubt about requirements or tradeoffs, explicitly ask Q before proceeding with risky or irreversible changes.

## Tech Stack

| Component | Stack |
|-----------|-------|
| Backend | (Servis bazlı - her mikro servis kendi stack'ini tanımlar) |
| Frontend | (İhtiyaca göre) |
| Database | (Servis bazlı - SQL Server, PostgreSQL, MongoDB, etc.) |
| API Gateway | (İhtiyaca göre) |
| Message Queue | (İhtiyaca göre - RabbitMQ, Kafka, etc.) |

## Mikro Servis Mimarisi

```
InvektoServis/
├── services/                    # Bağımsız mikro servisler
│   ├── service-a/              # Her servis kendi dizininde
│   │   ├── src/
│   │   ├── package.json
│   │   └── README.md
│   ├── service-b/
│   └── ...
├── shared/                      # Paylaşılan kod
│   ├── contracts/              # API kontratları
│   ├── utils/                  # Ortak utility'ler
│   └── types/                  # Paylaşılan type'lar
├── gateway/                     # API Gateway (opsiyonel)
└── deploy/                      # Deploy scriptleri
```

### Mikro Servis Kuralları

1. **Bağımsızlık:** Her servis kendi başına deploy edilebilir
2. **İzolasyon:** Servisler arası iletişim sadece API/Event üzerinden
3. **Kendi DB'si:** Her servis kendi database'ine sahip olabilir
4. **Versiyon:** Her servis bağımsız versiyonlanır

## Infrastructure

- **Domain Yapısı:**
  - Production: `api.invekto.com` (veya tanımlanacak)
  - Staging: `dev.invekto.com`

## Environment Separation

**Dev PC and Production Server are DIFFERENT machines!**

| Aspect | Dev PC | Production Server |
|--------|--------|-------------------|
| Machine | Developer's local PC | Remote Server |
| OS | Windows | Windows/Linux |
| Services | Manuel `npm run dev` | Docker/PM2/K8s |
| Path | `C:\CRMs\InvektoServis\` | `/app/invekto/` veya benzeri |

**Windows PowerShell Rules (CRITICAL):**
- **ALWAYS use PowerShell wrapper for Bash tool:** `powershell -NoProfile -Command "..."`
- NEVER use raw bash/Linux syntax on Windows
- `&&` chaining does NOT work - use `;` to chain commands
- Example: `powershell -NoProfile -Command "cd c:\path; npm run build"`

## Commands

| Task | Command |
|------|---------|
| Service dev | `cd services/{name} && npm run dev` |
| Service build | `cd services/{name} && npm run build` |
| Service test | `cd services/{name} && npm test` |
| All services | `npm run dev:all` (root'tan) |

> 💡 Auto workflow otomatik uygulanır - `/auto` yazmaya gerek yok.
>
> **COMPACT SONRASI:** Auto workflow aktif kalır. Session sıfırlansa bile tüm kod değişiklikleri auto.md kurallarını takip eder: Interview → Plan → Dev → Build → /rev → Codex → Commit

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
   - Logic seems wrong or inconsistent → ASK Q
   - Missing information to implement correctly → ASK Q
   - Multiple valid approaches exist → ASK Q
   - Something "smells wrong" → ASK Q

5. **🎯 Q Interview (MANDATORY):**
   - Her kod değişikliği öncesi interview yap
   - **Konu ne kadar açık görünürse görünsün, TÜM gri noktalar çözülene kadar sor**
   - "Açık görünüyor" ≠ "Soru sormaya gerek yok"
   - Her varsayım = potansiyel yanlış yön
   - Q "skip interview" demeden koda geçme
   - **🔴 ŞEYTANIN AVUKATLIĞI (PP-006):** Q'yu challenge et, alternatifler sun, edge case'leri sor, trade-off'ları belirt - Q "uyandırılmak" istiyor, pasif kalmak DEĞİL!

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

## Critical Rules

### Ignored Folders

- **`temp/`** - Geçici dosyalar. Git'e ekleme, kod yazarken dikkate alma.

### 🔴 SINGLE SOURCE OF TRUTH: DB Schema (MOST CRITICAL)

**Her servis için DB şeması için tek gerçek kaynak tanımla!**

| DB Değişikliği | Şema dosyasına YANSIT |
|----------------|------------------------|
| Yeni tablo | ✅ CREATE TABLE ekle |
| Yeni kolon | ✅ CREATE TABLE + ALTER migration |
| Kolon silme | ✅ CREATE TABLE'dan çıkar |
| Yeni index | ✅ CREATE INDEX ekle |
| Yeni FK/constraint | ✅ ADD CONSTRAINT ekle |

**KURAL:** Kod yazarken yeni tablo/kolon kullanacaksan → **ÖNCE şemaya ekle, SONRA kodu yaz!**

### ⚠️ DB-CODE SYNC CHECK

**Kod ve DB senkronize olmayabilir!** Her yeni özellik yazarken:

1. **Tablo var mı?** - Kodda kullanılan tablo DB'de gerçekten var mı kontrol et
2. **Kolon var mı?** - Kullanılan her kolon DB'de mevcut mu kontrol et
3. **Veri tipi doğru mu?** - Kolon tipleri kod beklentisiyle uyuşuyor mu
4. **Migration gerekli mi?** - Yeni tablo/kolon lazımsa önce migration yaz

**ASLA varsayma - her zaman kontrol et!**

### 🐍 SNAKE_CASE CONVENTION (DB & CODE)

**Tüm DB kolon adları `snake_case` olmalı!** PascalCase veya camelCase YASAK.

| ❌ Yanlış | ✅ Doğru |
|-----------|----------|
| `UserId` | `user_id` |
| `CreatedAt` | `created_at` |
| `ServiceName` | `service_name` |

### Mikro Servis İzolasyonu

**Bir serviste yapılan değişiklik diğer servisleri ETKİLEMEZ!**

| Soru | Cevap |
|------|-------|
| Bu değişiklik hangi servis(ler)i etkiliyor? | Belirle |
| Etkilemediğim servisler için regression riski var mı? | Kontrol et |
| Shared kod değişiyorsa | TÜM etkilenen servisleri test et |

---

1. **DB:** Servis bazlı - her mikro servis kendi DB yapısını tanımlar
2. **Auth:** Servisler arası SERVICE_TOKEN veya OAuth2
3. **Errors:** Use `arch/errors.md` codes (INV-xxx)
4. **Contracts:** Never invent schema. Use `arch/contracts/*.json`

## Architecture Reference

**🚨 KURAL: Kod yazmadan ÖNCE ilgili `arch/` dokümanını oku!**

| Yazacağın Kod | Önce Oku |
|---------------|----------|
| DB değişikliği | `arch/db/` + servis şeması |
| Error handling | `arch/errors.md` |
| API contract | `arch/contracts/` |
| Yeni endpoint | `arch/endpoints.md` |
| Yeni servis | `arch/docs/microservice-guide.md` |

All rules in `arch/`:
- `arch/env.md` - Environment variables
- `arch/errors.md` - Error codes (INV-*)
- `arch/contracts/` - Data contracts
- `arch/db/` - Schema definitions
- `arch/endpoints.md` - Endpoint registration rules
- `arch/logging.md` - Log format
- `arch/plans/` - Feature implementation plans
- `arch/session-memory.md` - Session context
- `arch/active-work.md` - In-progress task tracker
- `arch/lessons-learned.md` - Common mistakes and patterns
- `arch/docs/` - Teknik dokümanlar

## Agent Prompts

All agents in `.claude/agents/`:
- `INVEKTO_BASE.prompt.md` - Global rules (inherited by all)
- `INVEKTO_PLAN_AGENT.prompt.md` - Planning (for /auto)
- `INVEKTO_DEV_AGENT.prompt.md` - Implementation (for /auto)
- `INVEKTO_AUDIT_AGENT.prompt.md` - Codebase audit (Q triggers manually)

Skills in `.claude/commands/`:
- `auto.md` - Default workflow (otomatik uygulanır, /auto yazmaya gerek yok)
- `rev.md` - Review protocol (v3.0 - /rev komutu)
- `aha.md` - Detaylı aha moment analizi (`/aha` ile çağrılır)
- `learn.md` - Session learnings kayıt (`/learn` ile çağrılır)
- `push.md` - Git push shortcut (`/push` ile çağrılır)
- `test-ui.md` - Semi-autonomous UI testing (`/test-ui` ile çağrılır, Playwright + Python)

**AHA Moments:**
- **Plan içinde (zorunlu):** Her plan 5 basit AHA suggestion içerir (UX/SPEED/RELIABILITY/SALES/SUPPORT)
- **Detaylı analiz (opsiyonel):** `/aha` komutu ile derin analiz yapılabilir

## 🤖 Sub-Agents (Otomatik Tetikleme)

**Q'nun agent adı hatırlamasına GEREK YOK!** Aşağıdaki durumlarda ilgili agent OTOMATİK çağrılmalı:

### Otomatik Tetikleme Kuralları

| Durum | Agent | Tetikleme |
|-------|-------|-----------|
| Build gerekli | `build-runner` | Kod değişikliği sonrası |
| DB sorgusu gerekli | `db-query` | Veri sorulduğunda |

### Agent Detayları

| Agent | Model | Güvenlik |
|-------|-------|----------|
| `build-runner` | haiku | Sadece build komutları |
| `db-query` | haiku | **SADECE SELECT** - write YASAK |

---

## Workflow (v3.1 - Copy-Paste)

> **🔄 PERSIST AFTER COMPACT:** Bu bölüm session sıfırlansa bile geçerlidir.

**AUTO WORKFLOW = DEFAULT DAVRANIS**

**Her kod degisikligi otomatik olarak auto.md kurallarini takip eder.**
`/auto` yazmaya GEREK YOK - sadece ne istedigini soyle.

**v3.1 Farki:**
- Interview: AskUserQuestion tool ile (duz metin YASAK)
- Plan JSON: TUM risk seviyeleri icin ZORUNLU
- Codex review: TUM risk seviyeleri icin ZORUNLU (LOW dahil)
- Copy-paste yontemine DONDU

**Otomatik Akis:**
1. Q bir sey ister -> AskUserQuestion ile interview
2. Agent risk'i belirler (LOW/MEDIUM/HIGH/CRITICAL)
3. Plan JSON olusturulur (TUM risk seviyeleri)
4. Implement -> Build
5. /rev -> Q copy-paste -> Codex -> PASS/FAIL (TUM risk seviyeleri)

**Review Akisi (v3.1 - Copy-Paste):**
```
DevAgent kod yazar -> Build PASS
    |
DevAgent /rev calistirir (TUM risk seviyeleri)
    |
🚨 ZORUNLU: Q'ya Codex prompt gosterilir
    |
Q AYRI Codex penceresine yapistirir
    |
Codex 2 BLOK uretir (Code Quality + CoVe)
    |
Q verdict bildirir
    |
DevAgent /rev verdict PASS|FAIL
    |
PASS -> commit -> DONE
FAIL -> fix -> /rev (max 3 iter)
```

**🚨 HARD RULE:** /rev sonrasi Codex prompt'u Q'ya gosterilmeden ASLA commit yapilamaz!

**Escalation Kategorileri (3 iter sonrasi):**
| Kategori | Aciklama |
|----------|----------|
| DECISION_CONFLICT | Tasarim karari gerekiyor |
| TOOL_LIMITATION | Arac/framework limiti |
| PLAN_ASSUMPTION_WRONG | Plan varsayimi yanlis |
| SCOPE_INSUFFICIENT | Scope yetersiz |
| ARCHITECTURE_CONFLICT | Mimari celiski |

**Q'nun yapacagi:** Interview cevapla -> Plan onayla -> Copy-paste koprusu -> Izle.

**Risk-Based Trigger:**
| Risk | Build PASS Sonrasi |
|------|-------------------|
| LOW | /rev -> Q copy-paste -> Codex |
| MEDIUM | /rev -> Q copy-paste -> Codex |
| HIGH | /rev -> Q copy-paste -> Codex |
| CRITICAL | /rev + Q onay bekle |

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
- Clear instruction = direkt başla, auto workflow otomatik uygulanır
- Q override komutları: `STOP`, `SKIP CODEX`, `FORCE PASS` (sadece Q'nun açık izni ile)

## Architecture Compliance

**Before writing code:**
1. Read relevant arch/ files
2. Check existing patterns in codebase
3. Verify contract fields exist in arch/contracts/
4. Use error codes from arch/errors.md
5. Never invent new schemas - ask if needed

**Code review checklist:**
- [ ] Uses existing patterns, not new inventions
- [ ] Error codes match arch/errors.md
- [ ] No hardcoded endpoints/ports
- [ ] Mikro servis izolasyonu korunuyor
- [ ] Shared kod değişikliği varsa tüm servisler kontrol edildi

---

**Full Q-Mode reasoning protocol and failure handling rules are defined in `INVEKTO_BASE.prompt.md`.**
