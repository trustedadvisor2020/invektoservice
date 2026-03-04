<!-- VERSION: 6.0 | UPDATED: 2026-02-28 | Persist After Compact | Progressive Disclosure -->
<!-- COMPACT SONRASI: Auto workflow aktif kalır. Interview → Plan → Dev → Build → /rev → MCP Codex → Commit -->
[InvektoServis Global Base Prompt]

You are an AI developer working inside the InvektoServices repository.
Pipeline: DevAgent implements → `/rev` → MCP codex_review (automated) → Q owns decisions + override rights.

## SESSION BOOTSTRAP

1. **Auto Workflow aktif** (plan mode dahil)
2. **Oku:** `arch/session-memory.md`, `arch/lessons-learned.md`
3. **Interview:** AskUserQuestion ile gri noktaları çöz
4. **AC Gate:** Interview sonunda min 2 başarı kriteri sorusu sor, Q'dan teyit al
5. **PP-006:** Q'yu challenge et — "Ya X olursa?", alternatif sun, trade-off belirt

## CODEX UTANSIN DOKTRINI

Her satır yazılmadan ÖNCE 5 soru:

| # | Soru | Cevap yoksa |
|---|------|-------------|
| 1 | Bu satır hata durumunda ne yapar? | Yazma — önce error path tasarla |
| 2 | Null/empty/unexpected gelirse? | Yazma — önce guard ekle |
| 3 | 10.000 concurrent user'da ne yapar? | Yazma — önce scale düşün |
| 4 | Codebase'deki mevcut pattern'a uyuyor mu? | Yazma — önce pattern'ı bul |
| 5 | Codex bunu sorar mı? | Yazma — önce soruyu kendin sor ve cevapla |

**Hedef:** iteration=0, Codex "no issues found" desin.

## CRITICAL RULES WITH EXAMPLES

### Error Handling

```csharp
// WRONG — broad catch, silent failure, no error code
try { await _db.ExecuteAsync(sql); }
catch (Exception ex) { _logger.LogError(ex.Message); }

// RIGHT — typed catch, INV error code, user-friendly message
try {
    await _db.ExecuteAsync(sql);
}
catch (NpgsqlException ex) {
    _logger.LogError(ex, "INV-3001: Failed to update campaign {CampaignId}", campaignId);
    throw new InvektoException("INV-3001", "Kampanya güncellenemedi.");
}
```

Error codes: `arch/errors.md` (INV-1xxx API, INV-2xxx Auth, INV-3xxx DB).

### Null Safety

```csharp
// WRONG — null-forgiving operator
var name = user!.Profile!.DisplayName;

// RIGHT — explicit null handling
var name = user?.Profile?.DisplayName ?? "Unknown";
if (user?.Profile is null) {
    _logger.LogWarning("INV-1005: User profile missing for {UserId}", userId);
    return Results.BadRequest(new { error = "INV-1005", message = "Kullanıcı profili bulunamadı." });
}
```

### N+1 Query

```csharp
// WRONG — N+1 query in loop
foreach (var campaign in campaigns) {
    campaign.Stats = await _db.GetStatsAsync(campaign.Id); // N queries!
}

// RIGHT — batch query
var allStats = await _db.GetStatsBatchAsync(campaigns.Select(c => c.Id));
foreach (var campaign in campaigns) {
    campaign.Stats = allStats[campaign.Id];
}
```

### Microservice Isolation

```csharp
// WRONG — direct DB access across service boundary
var user = await _otherServiceDb.GetUserAsync(userId);

// RIGHT — API call to other service
var user = await _httpClient.GetFromJsonAsync<UserDto>($"/api/users/{userId}");
```

### Minimal Diff

```
WRONG: Plan'da "endpoint ekle" diyor → endpoint + refactor + rename + cleanup
RIGHT: Plan'da "endpoint ekle" diyor → sadece endpoint eklenir
```

### Build After Every Edit

```bash
# Full solution build
powershell -NoProfile -Command "dotnet build C:\CRMs\InvektoServices\InvektoServis.sln --no-restore -v q"

# Single service build
powershell -NoProfile -Command "dotnet build C:\CRMs\InvektoServices\src\Invekto.{Name}\Invekto.{Name}.csproj --no-restore -v q"
```

Shared değiştiyse → full solution build. Build fails → fix immediately.

## WORKFLOW SUMMARY

```
Q paket ister → Interview → AC Gate → Plan JSON → Q onay → Dev → Build PASS → /rev (MCP) → Commit
```

- **Codex review yapılmadan commit yapılamaz** (LOW dahil)
- Paket bazlı: birden fazla GR tek pakette (1 interview + 1 plan + sıralı dev + 1 review)
- Max 3 iteration. Geçemezse escalate: DECISION_CONFLICT | TOOL_LIMITATION | PLAN_ASSUMPTION_WRONG
- Plan: `arch/plans/{YYYYMMDD-feature-name}.json`, schema: `arch/contracts/plan-schema.json`

> Detay: `references/workflow-detail.md`

## ENTERPRISE CODE QUALITY

1. **Production-grade:** error handling, edge cases, performance, maintainability
2. **System integrity:** mevcut işlevselliği bozma
3. **Heavy-load ready:** thousands of concurrent users, thread-safety, no memory leaks
4. **Prefer existing patterns:** yeni mimari ancak gerektiğinde
5. **Unclear → ASK Q**

## PRE-FLIGHT CHECK

- `arch/` docs oku (contracts, errors)
- DB-Code sync: tablo/kolon var mı?
- Microservice awareness: hangi servisi etkiliyor?
- Codebase'de benzer pattern ara

## Q-FACING OUTPUT

3-6 satır: Summary, Risk, Status, Next action. Log dump yasak.

## FINAL PRINCIPLE

Speed never overrides correctness.
