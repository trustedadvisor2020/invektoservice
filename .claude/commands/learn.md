---
name: recording-lessons
description: Analyzes session corrections and records lessons to arch/lessons-learned.md. Use after mistakes, failed reviews, Q corrections, or at session end to capture reusable patterns.
---

# /learn - Session Learning (v2.0)

> **Persist After Compact:** Works even after session reset.

Analyzes Q's corrections during the session and saves them to `arch/lessons-learned.md`.

---

## Auto vs Manual Mode

| Mode | Trigger | Q Approval |
|------|---------|------------|
| **Auto** | Session sonu, DONE sonrasi | Onay GEREKMEZ - dogrudan ekle |
| **Manual** | Q `/learn` yazdiginda | Preview goster, Q onaylar |

**Auto mode:** Session sonunda ogrenilecek seyleri dogrudan `arch/lessons-learned.md`'ye ekle (Q onay beklemeden).
**Manual mode:** Q `/learn` yazarsa preview goster, Q onay verirse kaydet.

---

## Usage

### 1. `/learn` - Session Analysis (Manual)

Analyze the entire session, list learnable items, show preview, save with Q's approval.

### 2. `/learn "topic"` - Specific Topic (Manual)

Add the specified topic to lessons-learned.md with Q's approval.

### 3. Auto Mode (Session End)

Agent automatically records lessons at session end without waiting for Q approval.

---

## Devil's Advocate (PP-006)

> **Canonical source:** `INVEKTO_BASE.prompt.md` SEYTANIN AVUKATLIGI section.

Challenge what's worth recording:
- "Is this really a project-specific lesson, or just general knowledge?"
- "Did Q's correction reveal a deeper architectural issue?"
- "Are we treating the symptom or the root cause?"
- Don't record surface-level fixes - dig for the underlying pattern
- Push back if Q wants to skip learning from a painful failure

---

## Step 1: Signal Detection

Scan the session for:

**Q Corrections:** "no", "wrong", "actually", "not like that", Q's code change requests, rejected approaches

**Recurring Patterns:** Same mistake/correction 2+ times, multiple fixes in same file, repeated similar questions

**Approved Approaches:** "yes", "correct", "exactly", "good", Q satisfaction after Build PASS, patterns that got Codex PASS

**Error -> Solution Chains:** Build FAIL -> fix -> PASS, Codex FAIL -> fix -> PASS, Q correction -> apply -> approval

---

## Step 2: Filtering

For each signal, check:

- Is it project-specific? (Unique to this project)
- Is it repeatable? (Will occur again in the future)
- Is it new? (Not already in lessons-learned.md)
- Is it actionable? (Concrete prevention possible)

**3 out of 4 YES -> ACCEPT, otherwise -> REJECT**

---

## Step 3: Categorization

| Signal Type | Target Table |
|-------------|-------------|
| I made a mistake, Q corrected | Common Mistakes |
| This approach worked well | Patterns That Work |
| Don't do this | Anti-Patterns to Avoid |
| Caught during review | Code Review Insights |

---

## Step 4: Preview (Manual Mode Only)

Show Q:

```md
## /learn Findings

### To Add: Common Mistakes
| Date | Category | Mistake | Solution | Prevention |
|------|----------|---------|----------|------------|
| {date} | {category} | {mistake} | {solution} | {prevention} |

### To Add: Patterns That Work
| Pattern | Where Used | Why It Works |
|---------|------------|--------------|
| {pattern} | {where} | {why} |

### Rejected (with reasons):
- "{signal}" -> General best practice, not project-specific

**Approve?** (yes / no / edit)
```

**Auto mode:** Skip preview, directly save.

---

## Step 5: Save

After approval (manual) or directly (auto):

1. Read `arch/lessons-learned.md`
2. Add new row to relevant table
3. Date format: `YYYY-MM-DD`
4. Duplicate check: Don't add if same mistake already exists

### Cross-Project Lessons (shared-lessons.md)

Eğer öğrenilen ders InvektoServices'a özgü DEĞİLSE ve diğer projelerde de geçerliyse:
1. `C:\Users\taner\.claude\workflow\shared-lessons.md` dosyasını oku
2. Uygun bölüme ekle (Tekrarlayan Hatalar, Anti-Pattern'ler, veya Cross-Project Teknik Kurallar)
3. Duplicate check — zaten varsa ekleme

---

## Format Rules

### Common Mistakes Row
```
| {YYYY-MM-DD} | {Category} | {Brief mistake description} | {Solution} | {Future prevention} |
```

**Category options:** DB, SQL, API, UI, Auth, Config, Codex, Risk, Workflow, Deploy, PowerShell, Git, Logging

### Patterns That Work Row
```
| {Pattern name} | {Where used} | {Why it works} |
```

### Anti-Patterns Row
```
| {Anti-pattern name} | {Problem} | {Better approach} |
```

---

## Integration

**With Auto Workflow:**
- `/learn` does NOT interrupt auto workflow
- After every DONE: Agent auto-records lessons (no Q approval needed)
- After every FAIL: Agent asks if this mistake should be recorded

**With /rev:**
- `/learn` can be suggested after `/rev verdict FAIL`
- Same mistake in 3 iterations -> automatic recording

---

## Q Override

| Command | Effect |
|---------|--------|
| `STOP` | Halt operation |
| `edit: {change}` | Modify the suggestion |
| `only mistakes` | Only add Common Mistakes |
| `skip` | Skip /learn for this session |
