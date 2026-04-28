# FEAT-PILOT-KANBAN-SUPERADMIN — Pilot Tracking Kanban (SuperAdmin Dashboard)

**Slug:** `20260428-feat-pilot-kanban-superadmin`
**Risk:** MEDIUM
**Status:** REVIEW (iteration 1 — tenant_id filter fix + ON CONFLICT idempotency + INV codes)
**Owner:** Claude + Q
**Started:** 2026-04-28
**Plan:** [`arch/plans/20260428-feat-pilot-kanban-superadmin.json`](../arch/plans/20260428-feat-pilot-kanban-superadmin.json)

## Özet

Q'nun "tüm gidişat" izleme isteği için SuperAdmin Dashboard'a Pilot Kanban sayfası. 5 kolon (BLOCKED/TODO/IN_PROGRESS/BACKLOG/DONE), kart click → sağdan slide-in drawer (tüm detay inline). Read-only — `/wrap` workflow Step 3.5 olarak otomatik kart status update önerir, Q onayı sonrası API PATCH gönderir.

## Q Kararları

| Soru | Karar | Anlamı |
|------|-------|--------|
| Scope | 1a Platform-level | board_key='dent-pilot', tenant_id NULL — SuperAdmin only |
| Persistence | 2a PostgreSQL | Migration 035 + endpoint, deploy ile gelir |
| /wrap update | 3c Hibrit | Otomatik öner + Q onayla → API PATCH |
| UI etkileşim | Drawer (modal değil) | Sağdan slide-in, drag-drop YOK, read-only |
| History | YOK | Sadece updated_at + completed_at |
| Detay | Drawer inline | Tüm detay (markdown render) drawer'da |
| Seed | Migration INSERT | ~48 kart Dent pilot için, idempotent |

## Acceptance Criteria

| ID | Kriter | Status |
|----|--------|--------|
| AC1 | Migration 035 idempotent + ~48 kart seed | PENDING |
| AC2 | GET `/api/ops/kanban/{board_key}` 200 | PENDING |
| AC3 | PATCH `/api/ops/kanban/{board_key}/cards/{slug}` 200 | PENDING |
| AC4 | Dashboard nav'da "Pilot Kanban" (opsOnly) | PENDING |
| AC5 | PilotKanbanPage 5 kolon + drawer | PENDING |
| AC6 | /wrap Step 3.5 hibrit kanban sync | PENDING |

## Mimari

### DB Schema (Migration 035)
```sql
CREATE TABLE kanban_cards (
  id BIGSERIAL PRIMARY KEY,
  board_key VARCHAR(64) NOT NULL,
  card_slug VARCHAR(128) NOT NULL,
  tenant_id BIGINT NULL,                                  -- NULL = platform-level (e.g. dent-pilot SuperAdmin); tenant-scoped boards = X
  status VARCHAR(20) NOT NULL CHECK (status IN ('BLOCKED','TODO','IN_PROGRESS','BACKLOG','DONE')),
  category VARCHAR(20) NOT NULL CHECK (category IN ('CUSTOMER','OPS','DEV','DECISION','UI','DOC')),
  priority VARCHAR(8) NOT NULL DEFAULT 'P2' CHECK (priority IN ('P0','P1','P2','P3')),
  position INTEGER NOT NULL DEFAULT 100,
  title VARCHAR(255) NOT NULL,
  summary TEXT,
  body_markdown TEXT,
  owner VARCHAR(64),
  eta VARCHAR(32),
  source_file VARCHAR(255),
  source_anchor VARCHAR(64),
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  completed_at TIMESTAMPTZ,
  UNIQUE (board_key, card_slug)
);
CREATE INDEX idx_kanban_cards_board_status ON kanban_cards(board_key, status, position);
CREATE INDEX idx_kanban_cards_board_updated ON kanban_cards(board_key, updated_at DESC);
CREATE INDEX idx_kanban_cards_tenant ON kanban_cards(tenant_id, board_key) WHERE tenant_id IS NOT NULL;
-- Tenant isolation: repository all queries use 'WHERE tenant_id IS NOT DISTINCT FROM @tenant' (null-safe equality).
-- Platform-level board (dent-pilot): tenantId=null → matches NULL rows.
-- Tenant-scoped board (tenant-X-pilot): tenantId=X → matches X rows only (cross-tenant isolation enforced).
```

### API
- `GET /api/ops/kanban/{board_key}` → `KanbanBoardDto { cards: KanbanCardDto[], updated_at }`
- `PATCH /api/ops/kanban/{board_key}/cards/{cardSlug}` → güncel `KanbanCardDto`
  - Body: `{ status: 'DONE' | ..., completed_at?: ISO }`
  - Status DONE → `completed_at = NOW()` otomatik

### Frontend
- `PilotKanbanPage.tsx`: 5 kolon CSS Grid, polling 60s
- `KanbanDrawer.tsx`: Sağdan 480px slide-in, ESC + overlay click kapatma
- `Layout.tsx` ALL_NAV_ITEMS: `{ path: '/pilot-kanban', label: 'Pilot Kanban', icon: Kanban, opsOnly: true, section: 'Yönetim' }`

### /wrap Step 3.5 (Hibrit)
1. Tara: git commit message + plan JSON status + execution queue completion
2. Slug match: card_slug ↔ commit slug (e.g. "feat(b-meta)" → kart slug "b-meta")
3. Q'ya öneri sun: "Kanban: 2 kart güncellenecek — onaylıyor musun?"
4. Onay → API PATCH gönder (her kart için)
5. Hiç match yoksa: "Kanban sync: değişiklik yok"

## Dosya Listesi

**Yeni (12):**
- `arch/db/migrations/035-kanban-cards.sql`
- `src/Invekto.Shared/Contracts/Kanban/KanbanCardDto.cs`
- `src/Invekto.Shared/Contracts/Kanban/KanbanBoardDto.cs`
- `src/Invekto.Shared/Contracts/Kanban/KanbanStatus.cs`
- `src/Invekto.Shared/Contracts/Kanban/KanbanCategory.cs`
- `src/Invekto.Shared/Contracts/Kanban/KanbanPatchRequest.cs`
- `src/Invekto.Backend/Data/KanbanRepository.cs`
- `src/Invekto.Backend/Endpoints/KanbanEndpoints.cs`
- `src/Invekto.Backend/Dashboard/src/pages/PilotKanbanPage.tsx`
- `src/Invekto.Backend/Dashboard/src/components/KanbanDrawer.tsx`
- `arch/plans/20260428-feat-pilot-kanban-superadmin.json`
- `tracking/20260428-feat-pilot-kanban-superadmin.md`

**Modified (5):**
- `src/Invekto.Backend/Program.cs` — endpoint registration
- `src/Invekto.Backend/Dashboard/src/lib/api.ts` — kanban API functions
- `src/Invekto.Backend/Dashboard/src/components/Layout.tsx` — nav entry
- `src/Invekto.Backend/Dashboard/src/App.tsx` — route
- `C:/Users/taner/.claude/commands/wrap.md` — Step 3.5

## Seed Kartları (Hedef ~48)

Kategoriler:
- 🔴 BLOCKED (Müşteri/3rd-party): B.1-B.8, C.1-C.7, D.1-D.2, K1-K10
- 📋 TODO (Invekto Ops): E.1-E.8, D.3-D.5, F.1-F.3, monitoring 9.1-9.4, training 10.1-10.4, contract quirks
- 🚧 IN_PROGRESS: 17.4.1-17.4.5 (Lead listesi, AppointmentsPage, Doktor yönetimi vs.)
- ⏸ BACKLOG (Post-pilot): B-C2, B0, FEAT-CLINIC-METADATA, UI #10-#18, Boşluk #5-#9, lessons-learned archive
- ✅ DONE: 15/15 paket, B-META, P10 Hangfire, Migration 031/033/034, Paket A plan_tier

## Riskler / Mitigation

| Risk | Mitigation |
|------|-----------|
| /wrap false positive (yanlış kart match) | Hibrit: Q onaylamadan PATCH gönderilmez |
| Drawer büyük markdown render lag | Body markdown <5KB tutulur, simple md→html (no library) |
| Migration seed çakışma | ON CONFLICT (board_key, card_slug) DO NOTHING |
| SuperAdmin auth bypass | TenantsPage ile aynı /api/ops/* gate, tenant mode'da 403 |
| Polling 60s eski data | Drawer açıkken son fetch zamanı göster |

## Notes

- Read-only Dashboard: Q manuel edit etmez, /wrap günceller
- Body markdown: minimal subset (headers, lists, links, code, bold/italic)
- Drawer width: 480px (md+ ekranlar), full-screen on mobile
- Colors: existing palette (--bad/--info/--warn/--accent2/--good)
- API auth: same as TenantsPage (Ops mode JWT)
