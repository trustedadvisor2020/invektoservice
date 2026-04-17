# INMA ↔ INSE Unification Roadmap

## P0 — Dent Adavista Pilot İçin Şart (~14-19g)

Bu paket bitmeden Dent pilotu başlamaz.

### UP0.1 — Contract Discipline (S, 1g)
- [ ] `Invekto.Shared/Contracts/Inma/*.cs` — INMA DTO'ları tek kaynak
- [ ] OpenAPI spec fetch + codegen (INMA Swagger'dan C# client)
- [ ] Her iki tarafta contract change process (breaking vs non-breaking)

### UP0.2 — SSO (M, 3-4g)
- [ ] INMA JWT format: `{ userId, companyCode, role, exp }`
- [ ] INMA ekibinden public key al → INSE JWT middleware doğrulasın
- [ ] INSE'de user tablosu kaldır (ya da read-only view olarak INMA'ya proxy)
- [ ] Role map: INMA role → INSE permission matrix
- [ ] Logout bridge: INMA logout → INSE session invalidate

### UP0.3 — Unified Tenant (S, 1-2g) — **Lazy Provisioning IN-PROGRESS**
- [x] INSE `tenant_registry.inma_code VARCHAR(100)` + unique partial index (migration `009-tenant-inma-code.sql`, 2026-04-01)
- [x] `tenant_registry_auto_id_seq` sequence (START 100_000_000) — lazy auto-provision için fresh tenant_id kaynağı (migration `016-tenant-registry-auto-gen-seq.sql`, 2026-04-17)
- [x] Tenant 5050 backfill (`inma_code='5050'`) — 016 migration içinde
- [ ] **Lazy provisioning login akışında (IN-PROGRESS, şu an yazılıyor):** `InmaTokenIntrospector` welcome 200 + `tenant_registry` miss → `nextval('tenant_registry_auto_id_seq')` ile fresh tenant_id + `INSERT ... ON CONFLICT (inma_code) DO NOTHING`
  - Seed: default flow + default template pack + feature flags (token claim `InseFeatures`'ten)
  - **INMA-bağımsız** — `tenant.created` event, bulk import, tenant list export — **hiçbiri gerekli değil**
- [ ] `tenant.updated` / `tenant.deactivated` event handler (UP1 scope — feature flag değişikliği + deactivate cascade)

> **2026-04-17 kararı:** J10 (tenant list export) + `tenant.created` webhook + bulk SQL backfill — **üçü de İPTAL**. Login-time lazy provisioning tek path. INMA'dan bu konuda hiçbir şey istenmiyor.

### UP0.4 — Domain & UX (M, 4-5g)
- [ ] Reverse proxy (nginx): `app.invekto.com/` → INMA Angular, `/ai/*` → INSE React
- [ ] Shared design tokens (CSS custom props): renk, font, spacing, radius
- [ ] INMA sidebar'a INSE menu entries: Flows, AI Agents, Campaigns, Funnel, Appointments
- [ ] INSE React bileşenleri bağımsız çalışacak + INMA shell içinde iframe fallback

### UP0.5 — Shared Data Layer (M, 3-4g)
- [ ] `contact_ref` tablosu (INSE): `tenant_id, inma_contact_id, inse_contact_id`
- [ ] INSE contact cache (INMA read API'den hydrate, TTL 5dk)
- [ ] **10 custom field mapping** — INMA field key'leri INSE'de sabit (bkz. Dent mapping)
- [ ] Contact update flow: INSE write → INMA API (custom field update endpoint)

### UP0.6 — Feature Flags (S, 1-2g)
- [ ] INMA'da firma bazlı "INSE feature" flag'leri: `ai_agent`, `flow_builder`, `drip`, `appointments`, `funnel`, `scoring`
- [ ] INSE her request başında check + 5dk cache
- [ ] UI: flag kapalıysa INMA menu'de görünmesin

### UP0.7 — Bidirectional Sync (S, 1g) — webhook zaten var
- [ ] INMA webhook → INSE `/api/inbound/inma/{tenantId}` (per-tenant config)
- [ ] Event tipleri: `message.received`, `message.sent`, `contact.updated`, `tenant.created`
- [ ] Retry / DLQ (webhook hata durumları)

### UP0.8 — Joint API Gereksinimleri (INMA ekibi, koordinasyon)
Detay + brief: [inma-team-kickoff-brief.md](inma-team-kickoff-brief.md)
- [ ] **J1** Template variable render (`{{name}}` substitution on send)
- [ ] **J2** Contact `opted_out` flag + API
- [ ] **J-HSM** Template `meta_approval_status` / `meta_category` alanları mevcut template listesi endpoint response'una eklensin
- [ ] **J-WND** Contact `last_inbound_at` alanı mevcut contact endpoint response'una eklensin (24h window state)
- [ ] **J4** Bulk send endpoint — **Opsiyonel/Backlog** (2026-04-17, pilot kritik değil; INSE tarafı Hangfire+throttle queue ile 200 lead'i yönetir). Scale (1000+ hedef) tetiklediğinde aktive edilir.
- [x] **J5** SSO JWT public key — **İPTAL** (introspection pattern ile çözüldü, 2026-04-16 `bfd57ae`)
- [x] **J10** Tenant list export — **İPTAL** (2026-04-17, lazy provisioning ile UP0.3 çözüldü, reconciliation gereksiz)
- [x] `tenant.created` webhook — **İPTAL** (2026-04-17, lazy provisioning UP0.3'ü çözdü)

## P1 — UX Polish (~5-8g, v1.1)

### UP1.1 — Feature Surfacing (M, 4-5g)
- [ ] INMA sohbet ekranına INSE widget slot'ları:
  - Intent etiketi (üst bilgi)
  - Flow state ("Nurture Day 3/14")
  - Suggested reply chip'leri (**J3**)
  - Custom field paneli (pipeline, scoring)
- [ ] Widget'lar iframe ile (v1.1) ya da Web Components (v2)

### UP1.2 — Template Media Library Sharing (S, 2g) — **J6**
- INSE template builder INMA media library'den medya seçebilsin

### UP1.3 — Unified Admin Navigation (S, 2g)
- INMA sidebar'da "AI & Automation" grubu altında tüm INSE özellikleri

### UP1.4 — Notification Center (M, 3-4g INSE + S, 1g INMA) — **YENİ**
INMA'da in-app bildirim yok. INSE kendi notification altyapısını kurar, INMA UI'da widget olarak görünür.
- [ ] INSE tarafı:
  - [ ] `notifications` tablosu (tenant_id, user_id, type, title, body, link, read, created_at)
  - [ ] `INotificationService.PublishAsync(tenantId, userId, event)` — flow engine, offer service, appointment service kullanır
  - [ ] REST: `GET /api/notifications`, `POST /api/notifications/mark-read`
  - [ ] SSE/WebSocket push (unread count + new notification event)
  - [ ] React `<NotificationBell/>` component (bell icon + badge + dropdown + notification page)
  - [ ] Event tipleri: `flow.completed`, `offer.sent/accepted`, `appointment.booked`, `xray.uploaded`, `sla.breach`, `nurture.exit`, `lead.handoff_required`
- [ ] INMA tarafı (**J9**):
  - [ ] Header'ın sağ üstünde INSE widget için DOM slot
  - [ ] iframe veya Web Component embed

## P2 — Advanced (~5-7g, v2)

### UP2.1 — Unified WebSocket (**J7**, M, 3-4g)
Tek WS gateway, INMA+INSE event'leri birleşik stream

### UP2.2 — Full Audit Log (**J8**, S, 2g)
INMA kritik aksiyonlarında INSE'ye event publish, INSE audit store'ında birleşik trail

### UP2.3 — Notification Center Birleşimi
(Q cevabına göre — INMA'da varsa integrate, yoksa INSE yapar)

## Paralel: INSE Platform Gap Fix (~8-12g)

Bunlar unification'dan bağımsız ama Dent için şart:
- **G3** Template A/B rotation — 1-2g
- **G6** Flow state persistence — 2-3g
- **G7** Hangfire migration — 5-7g

## Bağımlılık Grafiği

```
UP0.1 Contracts ──┬─→ UP0.2 SSO ──┐
                  │               ├─→ UP0.5 Shared Data ──┐
                  └─→ UP0.3 Tenant┘                       │
                                                           │
UP0.4 Domain/UX (bağımsız, paralel) ────────────────────→ │
UP0.6 Feature Flags (UP0.3 sonrası) ──────────────────→   │
UP0.7 Bidirectional Sync (UP0.1 sonrası) ─────────────→   │
UP0.8 Joint APIs (INMA ekibi — paralel başlasın) ──→      │
                                                           ▼
                                              DENT PILOT READY
                                              (+ G3, G6, G7 paralel)
```

## Kritik Bağımlılık: INMA Ekibi

P0'ın **UP0.8 (J1 template render, J2 opted_out, J-HSM template approval status, J-WND 24h window)** adımları INMA ekibine bağlı. UP0.2 (J5) introspection ile çözüldü; UP0.3 lazy provisioning (migration 016 IN-PROGRESS) ile INMA-bağımsız; J10 + tenant.created event + J4 bulk-send — hepsi iptal/opsiyonel.
- **Action:** INMA ekibiyle **kickoff meeting** — bu 4 maddeyi onlara anlat, sprint planla
- Paralel: INSE tarafında bağımsız (UP0.1, 0.4, 0.6, 0.7) ilerle
