# FM-1b: Flow Monitor Sayfasi

> **Durum:** DONE | **Tarih:** 2026-03-05 | **Codex:** iter 1 FORCE PASS (CQ3 false positive)
> **Spec:** `arch/specs/flow-monitor.md` | **Risk:** MEDIUM

## Kapsam

3-panel layout ile flow execution izleme sayfasi.

### Layout

```
+------------------+----------------------------+------------------+
| FILTRELER (top bar: flow dropdown, status, tarih, telefon)       |
+------------------+----------------------------+------------------+
| Execution List   | Timeline / Trace Detail    | AI Chat Panel    |
| (sol ~300px)     | (orta, flex-1)             | (sag ~300px)     |
+------------------+----------------------------+------------------+
```

## Acceptance Criteria

| # | Kriter | Durum |
|---|--------|-------|
| AC-1 | Monitor sayfasi Analiz kategorisinde gorunur (FlowBuilder flag) | PASS |
| AC-2 | 3-panel layout: sol execution list, orta timeline, sag AI placeholder | PASS |
| AC-3 | Ust filtreler: flow secimi, status, tarih araligi, telefon arama | PASS |
| AC-4 | Execution secince node trace timeline (gelen/giden mesajlar, eventlar) | PASS |
| AC-5 | 5sn polling ile auto-refresh | PASS |
| AC-7 | Tenant izolasyonu: her tenant sadece kendi flow'larini gorur | PASS |
| AC-8 | Admin panel flow monitor gormez | PASS |

## Backend Degisiklikleri

- Yeni endpoint: GET `/api/v1/flows/{tenantId}/executions` (cross-flow, 4 filtre, 7-gun default)
- Mevcut endpoint yeniden kullanim: GET `/api/v1/flows/{tenantId}/{flowId}/executions/{logId}`
- Backend proxy route: `/api/v1/flow-builder/monitor/{tenantId}/executions`
- JWT auth prefix guncellendi
- Yeni error code: INV-AT-049

## Frontend Deliverables

- [x] FlowMonitorPage.tsx — Ana sayfa + layout (tum component'ler tek dosyada)
- [x] ExecutionListPanel — Sol panel: execution listesi + pagination
- [x] ExecutionTimeline — Orta panel: node trace timeline
- [x] MonitorFilterBar — Ust filtre bari (4 filtre + debounced phone search)
- [x] App.tsx route ekleme (lazy-loaded)
- [x] Zustand store: flow-monitor-store.ts
- [x] 5sn polling mekanizmasi
- [x] AI Chat placeholder panel (FM-1c icin hazir)

## Files Changed

- ErrorCodes.cs, errors.md (INV-AT-049)
- AutomationRepository.cs (ListMonitorExecutionsAsync + MonitorExecutionSummary DTO)
- Automation/Program.cs (cross-flow endpoint, explicit snake_case response)
- Backend/Program.cs (proxy route + JWT auth prefix)
- api.ts (getMonitorExecutions)
- flow.ts (MonitorExecutionSummary, MonitorFilters types)
- flow-monitor-store.ts (new Zustand store)
- FlowMonitorPage.tsx (new page)
- App.tsx (route registration)
