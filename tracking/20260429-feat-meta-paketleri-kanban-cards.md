# 20260429-feat-meta-paketleri-kanban-cards — Tracking

> **Slug:** `20260429-feat-meta-paketleri-kanban-cards` | **Risk:** MEDIUM (iter 1 escalation per Codex CQ5)
> **Plan JSON:** [arch/plans/20260429-feat-meta-paketleri-kanban-cards.json](../arch/plans/20260429-feat-meta-paketleri-kanban-cards.json)
> **Status:** **DONE** (Codex iter 2 → Q FORCE PASS TOOL_LIMITATION 2026-04-29 13:05 UTC)
> **Migration:** 041 (3 INSERT + DO $verify$ INV-SEED-029 count + per-row drift check)

## Codex Iter 0 → Iter 2 → Q FORCE PASS Sonuc

**Iter 0 FAIL** (gpt-5.5-2026-04-23, 20811 tokens, 2026-04-29 11:50 UTC) — 6 blocker:
- CQ2 (real bug): ON CONFLICT DO NOTHING + count-only verify → pre-existing stale row sessiz pass
- CQ5 (policy): Risk LOW migration policy violation
- CQ11 (UNKNOWN): kanban_cards schema diff'te yok
- Q1 (FAIL): Idempotency manuel pre-existing stale icin yanlis (CQ2 ile ayni kok)
- Q2 (UNKNOWN): Spec dosyalari diff'te yok
- Q3 (UNKNOWN): Migration 037 CHECK constraint diff'te yok

**Iter 1 fixes (4):** Risk LOW→MEDIUM + Migration §2 per-row content drift check + arch/db/kanban-board.sql canonical mirror comment + spec dosyalari allowed_files.

**Iter 1 FAIL** (gpt-5.5-2026-04-23, 31802 tokens, 2026-04-29 12:25 UTC) — 5 blocker (iter 0 ana sorunlari CQ2/CQ11/Q1/Q3 RESOLVED):
- CQ5/Q4: tracking md line 82+139 hala "LOW" (line 3 MEDIUM ama metin icinde inconsistency)
- CQ9/Q2 (real bug): meta-ads-insights spec "Dashboard widget Marketing servisi okur (Backend proxy degil)" microservice isolation kuralina aykiri
- CQ12: INV-SEED-029 catalog entry sadece missing modunu dokumante, drift modunu eksik

**Iter 2 fixes (3):** Tracking md LOW→MEDIUM consistency + meta-ads-insights §3 Backend proxy zorunlu (microservice isolation, FEAT-EFS/MCC/CAPI iki-hop pattern) + errors.md INV-SEED-029 Mode A (missing/count) + Mode B (content drift) iki-seviyeli failure mode dokumantasyonu.

**Iter 2 FAIL** (gpt-5.5-2026-04-23, 31586 tokens, 2026-04-29 13:00 UTC) — **11/12 CQ PASS + 2/4 CoVe PASS**:
- Iter 1 ana sorunlari (CQ5/CQ9 mimari/CQ12) RESOLVED
- Tek kalan blocker: **CQ9/Q2/Q4 ayni kok TOOL_LIMITATION** — Codex "Marketing servisi (:7112) project context service registry'de yok" diyor, ama gerceklik:
  - tracking/README.md Mikroservis Port Haritasi: `Marketing | 7112 | Implemented | PKT-6C2`
  - session-memory P5-EFS DONE+DEPLOYED 2026-04-22 10/10 HEALTHY
  - src/Invekto.Marketing/ klasoru var
  - arch/db/marketing.sql canonical mirror var
  - Codex chunked review context'inde diff disindaki dosyalar yok, evidence eksik

**Q FORCE PASS** (2026-04-29 13:05 UTC, cevap=A) — TOOL_LIMITATION escalation:
- Onceki paketlerde benzer durumlarda (FEAT-PHOTO 2026-04-28 iter 4 + P5-EFS iter 4) ayni pattern
- CODEX UTANSIN iter=0 hedefi karsilanmadi (3 iter), real bugs RESOLVED
- Kalan kanit eksikligi diff-scoping artefakti (Marketing servisi GERCEKTEN DEPLOYED, sadece bu paketin diff'inde gozukmuyor)
- Risk MEDIUM, scope 7 dosya allowed_files, ADDITIVE only, idempotent + drift-detect verify

## Özet

3 Meta paketinin (FEAT-META-CAPI / FEAT-META-ADS-INSIGHTS / FEAT-META-MARKETING-API) DB-backed kanban'a (`kanban_cards` tablosu) audit pattern (Migration 038/039) ile eklenmesi. Bu paket **sadece metadata** — kanban UI'da kart gorulmesi icin SQL data INSERT, Meta entegrasyon kodu YOK. Build N/A. Service deploy gereksiz.

## Geri Plan (Niye Bu Paket?)

Onceki turn'de 3 Meta paketi spec/tracking/README/session-memory/roadmap markdown dosyalarina eklendi (commits pending). Q sordu: **"kanbandaki kodları nedir bu görevlerin"** — fark edildi ki **DB-backed kanban'a INSERT edilmedi**, Dashboard `/yol-haritasi/inse` sayfasinda kartlar **gorulmuyordu**. Q karari (cevap=B): Migration formal yol (atomik tx + idempotent + DO $verify$ + Codex review). Audit Batch A (Migration 038) ve FEAT-OBI/MEDIPOL (Migration 039) pattern'i izlenir.

## Scope

### In Scope
- `arch/db/migrations/041-feat-meta-paketleri-kanban-cards.sql` — 3 INSERT + DO $verify$ block (count + per-row drift check)
- `arch/db/kanban-board.sql` — canonical mirror comment-only update (iter 1 CQ11/Q3 fix)
- `arch/errors.md` +1 INV-SEED-029 entry (postcondition fail-loud)
- `arch/features/meta-conversions-api.md` — FEAT-META-CAPI spec (iter 1 Q2 content sync evidence)
- `arch/features/meta-ads-insights.md` — FEAT-META-ADS-INSIGHTS spec (iter 1 Q2 content sync evidence)
- `arch/plans/20260429-feat-meta-paketleri-kanban-cards.json` — plan JSON Codex review
- `tracking/20260429-feat-meta-paketleri-kanban-cards.md` — bu dosya

### Out of Scope (Explicit)
- Meta entegrasyon feature kodu (CAPI client / Ads Insights client / dispatcher / Hangfire queue / Dashboard editor) — D029/D030 chunk planlari
- Spec dosyalari (arch/features/meta-conversions-api.md + meta-ads-insights.md) — onceki turn'de olusturuldu
- Roadmap.md / README.md / session-memory.md guncelleme — onceki turn'de yapildi
- Frontend kanban UI davranis degisikligi — mevcut runtime DEV filter aktifse 3 yeni kart otomatik gorur
- Pixel/Token Q manuel provision — Q on-kosul, bu paket scope DISI
- Marketing API standard access App Review — D031 backlog activation gate ($50k+/ay)

## Kanban Kart Detaylari

| Ref Code | Card Slug | Title | Status | Category | Priority | Position | depends_on |
|----------|-----------|-------|--------|----------|----------|----------|------------|
| **D029** | `feat-meta-capi` | FEAT-META-CAPI Conversions API (server-side) | TODO | DEV | P1 | 240 | NULL |
| **D030** | `feat-meta-ads-insights` | FEAT-META-ADS-INSIGHTS Reporting Widget (read-only) | BACKLOG | DEV | P2 | 250 | `D029` |
| **D031** | `feat-meta-marketing-api` | FEAT-META-MARKETING-API Campaign Create/Manage | BACKLOG | DEV | P3 | 260 | `D029,D030` |

## Q Kararlari (onceki turn yansimasi — kart body_markdown'inda yer alir)

| # | Soru | Q Cevabi |
|---|------|----------|
| 1 | Pilot tenant | **Dent Adavista** ilk rollout (kod multi-tenant generic) |
| 2 | Test pixel ayri mi, prod+test_event_code mi? | **(Q "bilmiyorum" — Claude oneri)** Prod pixel + `test_event_code` — Q teyit bekliyor (D029 chunk B dev'inde) |
| 3 | Token expiry warning kanali | **Dashboard alert** (Hangfire daily check + tenant Dashboard banner < 7 gun) |
| 4 | Schedule event hook | **Ikisi de** (Appointments + Lead pipeline `appointment_booked`, deterministic event_id) |
| 5 | consent=false → CAPI gonderim | **Hard reject** (Marketing dispatcher gate, KVKK/GDPR uyumu) |

## Acceptance Criteria

| # | Kriter | Status |
|---|--------|--------|
| AC1 | Migration 041 idempotent prod-ready: 3 INSERT + ON CONFLICT DO NOTHING + tum field constraint uyumlu (ref_code regex + depends_on regex + category=DEV + priority P1/P2/P3 + board_key=inse) | PENDING |
| AC2 | Migration 041 postcondition DO $verify$ INV-SEED-029 fail-loud: 3 ref_code count check + RAISE EXCEPTION + actionable mesaj + tani SQL | PENDING |
| AC3 | arch/errors.md +1 INV-SEED-029 entry (description + user_message TR) | PENDING |
| AC4 | tracking dokumantasyonu (bu dosya) standart format | PENDING |
| AC5 | Build N/A (.cs/.tsx degismiyor, dotnet build skip) | PENDING |
| AC6 | Migration prod execute MCP postgres + DO $verify$ NOTICE log + UI smoke (3 kart gorunur) | PENDING (post-Codex) |

## Deploy Plan

1. **/rev Codex review** (MEDIUM risk per migration guardrail — iter 0 LOW classified, iter 1 escalated)
2. Codex PASS sonrasi commit master (HEREDOC + Co-Authored-By)
3. **MCP invekto-postgres execute** Migration 041 (atomic, transactional wrapper) — DO $verify$ INV-SEED-029 NOTICE log gozlemle
4. **Post-execute SELECT verify:**
   ```sql
   SELECT ref_code, card_slug, status, position, depends_on
     FROM kanban_cards
    WHERE ref_code IN ('D029','D030','D031')
    ORDER BY ref_code;
   ```
   Beklenen: 3 satir, position 240/250/260, status TODO/BACKLOG/BACKLOG, depends_on NULL/'D029'/'D029,D030'
5. **UI smoke (~1dk):** Dashboard `/yol-haritasi/inse?category=DEV` URL'sinde 3 yeni kart sag kolon (BACKLOG) ve sol kolon (TODO) altinda gorunur. Kart aciliyor, body_markdown render ediyor (5 Q karari + dependencies + cross-link spec).
6. **Service deploy YOK** — kanban_cards salt-data, KanbanRepository SELECT runtime'da fresh dondurur

## Rollback Plan

```sql
BEGIN;
DELETE FROM kanban_cards
 WHERE board_key = 'inse'
   AND ref_code IN ('D029','D030','D031');
COMMIT;
```

Frontend etkisi: 3 kart kaybolur, mevcut kanban davranisi bozulmaz.

## Cross-References

- **Spec dosyalari:**
  - [arch/features/meta-conversions-api.md](../arch/features/meta-conversions-api.md) — FEAT-META-CAPI tam spec (§0 Q kararlari, AC-1..AC-10)
  - [arch/features/meta-ads-insights.md](../arch/features/meta-ads-insights.md) — FEAT-META-ADS-INSIGHTS tam spec
- **Tracking dosyalari (per-feature):**
  - [tracking/feat-meta-capi.md](feat-meta-capi.md) — CAPI tracking + Q kararlari + chunk breakdown
  - [tracking/feat-meta-ads-insights.md](feat-meta-ads-insights.md) — Ads Insights tracking
- **Markdown tracker'lar (onceki turn):**
  - tracking/README.md master tablosu (3 yeni satir FEAT-META-CAPI/ADS-INSIGHTS/MARKETING-API)
  - arch/session-memory.md Execution Queue (3 entry FEAT-PIPELINE altinda)
  - tracking/roadmap.md Backlog Idea (3 satir EVALUATED/IDEA)
- **Pattern referans migration'lar:**
  - Migration 038 (audit data fix) — DO $verify$ INV-SEED-028 pattern
  - Migration 039 (FEAT-OBI + FEAT-MEDIPOL D027/D028) — INSERT pattern + ON CONFLICT DO NOTHING + position artis
- **Kanban schema:**
  - Migration 035 (kanban_cards table create)
  - Migration 036 (ref_code kolonu + CHECK constraint regex)
  - Migration 037 (depends_on kolonu + CHECK constraint regex INV-SEED-027)

## Bagimliliklar (Sonraki Adim)

D029 (FEAT-META-CAPI) chunk A baslayinca:
1. **Q manuel:** Business Manager → Events Manager → **Pixel/Dataset olustur**
2. **Q manuel:** Business Manager → Settings → System Users → **Token gen** (`ads_management` + `business_management` + `ads_read` cift permission, D030 ile bundle)
3. **App Review submission** (Pixel verify hafif yol, CAPI use-case)
4. Yeni paket: `arch/plans/<date>-feat-meta-capi-chunk-a.json` Shared DTO + IMetaCapiClient + MockClient

## Risk Analizi

- **Migration ADDITIVE only** (DROP/ALTER yok), idempotent re-run safe
- **Postcondition fail-loud** (INV-SEED-029) — sessiz drift onlenir
- **Service deploy yok** — runtime kod degismiyor
- **Frontend etkisi:** PilotKanbanPage runtime SELECT 3 yeni kart otomatik dondurur, ekstra binding yok
- **Rollback simple:** DELETE 3 row, prod sema bozulmaz
- **Risk: MEDIUM** (DB migration paketi — LOW guardrail violation, iter 1 escalation per Codex CQ5; ADDITIVE only, idempotent, fail-loud, additive ama migration kategorisi MEDIUM minimum)
