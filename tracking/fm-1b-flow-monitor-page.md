# FM-1b: Flow Monitor Sayfasi

> **Durum:** PLANNED | **Tarih:** - | **Codex:** -
> **Spec:** `arch/specs/flow-monitor.md` | **Risk:** MEDIUM

## Kapsam

3-panel layout ile flow execution izleme sayfasi.

### Layout

```
+------------------+----------------------------+------------------+
| FILTRELER (top bar: flow dropdown, status, tarih, telefon)       |
+------------------+----------------------------+------------------+
| Execution List   | Timeline / Trace Detail    | AI Chat Panel    |
| (sol ~280px)     | (orta, flex-1)             | (sag ~320px)     |
+------------------+----------------------------+------------------+
```

## Acceptance Criteria

| # | Kriter | Durum |
|---|--------|-------|
| AC-1 | Monitor sayfasi Analiz kategorisinde gorunur (FlowBuilder flag) | - |
| AC-2 | 3-panel layout: sol execution list, orta timeline, sag AI chat | - |
| AC-3 | Ust filtreler: flow secimi, status, tarih araligi, telefon arama | - |
| AC-4 | Execution secince node trace timeline (gelen/giden mesajlar, eventlar) | - |
| AC-5 | 5sn polling ile auto-refresh | - |
| AC-7 | Tenant izolasyonu: her tenant sadece kendi flow'larini gorur | - |
| AC-8 | Admin panel flow monitor gormez | - |

## Backend Degisiklikleri

- Yeni endpoint: GET `/api/v1/flows/{tenantId}/executions` (cross-flow, filtreleme destekli)
- Mevcut endpoint yeniden kullanim: GET `/api/v1/flows/{tenantId}/{flowId}/executions/{logId}`
- Backend proxy route'lari

## Frontend Deliverables

- [ ] FlowMonitorPage.tsx — Ana sayfa + layout
- [ ] ExecutionListPanel.tsx — Sol panel: execution listesi
- [ ] ExecutionTimeline.tsx — Orta panel: node trace timeline
- [ ] MonitorFilterBar.tsx — Ust filtre bari
- [ ] App.tsx route ekleme
- [ ] Zustand store: flow-monitor-store.ts
- [ ] 5sn polling mekanizmasi
