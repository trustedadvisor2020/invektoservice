# SPEC: Flow Monitor & Versioning

> **Spec ID:** SPEC-FM | **Paket:** FM-1 | **Risk:** MEDIUM
> **Yazar:** Q | **Son Guncelleme:** 2026-03-05 | **Durum:** DRAFT

## 1. Intent (Ne & Neden)

Firmalar (tenantlar) flow'larinin icerisindeki gelen/giden mesajlari ve eventlari canli olarak izlemek istiyor. Chat sayfasi benzeri 3-panel layout ile execution'lari gorup, AI asistan ile flow'u direkt duzeltebilmeli. Ek olarak her flow save'inde otomatik surum tutulacak, rollback ve aktif surum gosterimi saglanacak.

## 2. Acceptance Criteria

| # | Kriter | Dogrulama Yontemi |
|---|--------|-------------------|
| AC-1 | Monitor sayfasi Analiz kategorisinde gorunur (FlowBuilder feature flag) | Manual test |
| AC-2 | 3-panel layout: sol execution list, orta timeline, sag AI chat | Manual test |
| AC-3 | Ust filtreler: flow secimi, status, tarih araligi, telefon arama | Manual test |
| AC-4 | Execution secince node trace timeline gosterilir (gelen/giden mesajlar, eventlar) | Manual test |
| AC-5 | 5sn polling ile auto-refresh (Faz 1), SSE/WS (Faz 2) | Manual test |
| AC-6 | AI chat paneli: flow duzenleme + execution analizi + otomatik oneri | Manual test |
| AC-7 | Tenant izolasyonu: her tenant sadece kendi flow'larini gorur | DB query |
| AC-8 | Admin panel flow monitor gormez (opsOnly degil, tenant-only) | Manual test |
| AC-9 | Her flow save'inde otomatik version artar (v1, v2, v3...) | DB query |
| AC-10 | Flow builder'da aktif surum numarasi ve tarihi goruntulenir | Manual test |
| AC-11 | Rollback: eski surume geri donebilme | Manual test |
| AC-12 | Surum formati: "v1 - 5 Mar 2026" | Manual test |

## 3. Architectural Decisions

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| Polling (5sn) Faz 1, SSE Faz 2 | WS altyapisi yok, polling ile %90 UX karsilanir | EXPECTED: no WebSocket in Faz 1 |
| AI panel monitor sayfasinda, ayri component | Flow builder AiChatPanel'den farkli: execution context'i de var | Yeni component, reuse degil |
| flow_versions tablosu, chatbot_flows'dan ayri | Her save icin full JSONB snapshot, chatbot_flows'a extra sutun degil | EXPECTED: ayri tablo |
| Tenant-only sayfa (opsOnly yok) | Admin flow gormeyecek, sadece impersonate ile | EXPECTED: feature flag = FlowBuilder |
| Version integer (auto-increment per flow) | Basit, no semver | EXPECTED: simple versioning |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| Flow Config V2 | `arch/contracts/automation-flow-v2.json` |
| DB Schema (mevcut) | `arch/db/automation.sql`, `arch/db/flow-execution-log.sql` |
| DB Schema (yeni) | `arch/db/flow-versions.sql` |
| Error Codes | `arch/errors.md` INV-AU-xxx |

## 5. Scope Boundaries

### In Scope
- Flow Monitor sayfasi (3-panel layout)
- Execution listesi + filtreleme
- Node trace timeline goruntuleme (gelen/giden mesajlar, eventlar)
- AI chat paneli (flow duzenleme + execution analizi + oneri)
- Auto-refresh (5sn polling)
- Flow versioning (auto-increment, rollback, aktif surum gosterimi)
- Flow builder'da surum numarasi gosterme ve surum degistirme

### Out of Scope (Explicit)
- SSE/WebSocket real-time (Faz 2)
- Version diff karsilastirma
- Flow export/import
- Traffic heatmap (ayri spec: FB-5b)

### Degismeyen Alanlar (Pre-existing)
- flow_execution_log tablosu ve mevcut API endpoint'leri
- FlowEngineV2, node handler'lar
- Mevcut FlowLogPanel (flow builder icindeki panel, kalacak)

## 6. Service Boundaries

| Servis | Rol | Degisiklik Tipi |
|--------|-----|-----------------|
| Automation (7108) | Core logic | Yeni endpoint'ler: versioning CRUD, monitor query'leri |
| Backend (5000) | Proxy | FlowBuilderClient'a yeni proxy route'lar |
| Dashboard SPA | UI | Yeni sayfa: FlowMonitorPage, yeni component'ler |

## 7. Risk & Mitigation

| Risk | Olasilik | Mitigation |
|------|----------|------------|
| Execution log tablosu buyurse performans | MED | Index mevcut, pagination zorunlu, tarih filtre default 7 gun |
| AI panel flow'u bozarsa | LOW | Version rollback mevcut, AI degisikligi de yeni version olusturur |
| Tenant izolasyonu sizdirirsa | HIGH | WHERE tenant_id = @tid her query'de zorunlu, Codex CQ kontrol |

## 8. DB Schema: flow_versions

```sql
CREATE TABLE IF NOT EXISTS flow_versions (
    id              SERIAL PRIMARY KEY,
    flow_id         INTEGER NOT NULL,
    tenant_id       INTEGER NOT NULL,
    version_number  INTEGER NOT NULL,          -- Auto-increment per flow
    flow_config     JSONB NOT NULL,            -- Full snapshot
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by      VARCHAR(100),              -- Kimin save ettigi (user/ai)
    CONSTRAINT fk_fv_flow FOREIGN KEY (flow_id) REFERENCES chatbot_flows(flow_id) ON DELETE CASCADE,
    CONSTRAINT fk_fv_tenant FOREIGN KEY (tenant_id) REFERENCES tenant_registry(tenant_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_flow_versions
    ON flow_versions (flow_id, version_number);

CREATE INDEX IF NOT EXISTS idx_flow_versions_flow
    ON flow_versions (flow_id, version_number DESC);
```

## 9. API Endpoints (Yeni)

### Flow Monitor
| Method | Path | Aciklama |
|--------|------|----------|
| GET | `/api/v1/flows/{tenantId}/executions` | Tum flow'larin execution listesi (filtreleme destekli) |
| GET | `/api/v1/flows/{tenantId}/executions/{logId}` | Tek execution detayi |

### Flow Versioning
| Method | Path | Aciklama |
|--------|------|----------|
| GET | `/api/v1/flows/{tenantId}/{flowId}/versions` | Surum listesi |
| GET | `/api/v1/flows/{tenantId}/{flowId}/versions/{versionNumber}` | Belirli surum config'i |
| POST | `/api/v1/flows/{tenantId}/{flowId}/versions/{versionNumber}/rollback` | Surume geri don |

> **Not:** Mevcut PUT `/api/v1/flows/{tenantId}/{flowId}` save endpoint'i versioning'i otomatik tetikleyecek (save = yeni version).

## 10. UI Layout

```
+------------------+----------------------------+------------------+
| FILTRELER (top bar: flow dropdown, status, tarih, telefon)       |
+------------------+----------------------------+------------------+
| Execution List   | Timeline / Trace Detail    | AI Chat Panel    |
| (sol ~280px)     | (orta, flex-1)             | (sag ~320px)     |
|                  |                            |                  |
| [#123 running]   | > Trigger: "merhaba"       | [Soru sor...]    |
| [#122 completed] | > Mesaj: "Hosgeldiniz"     |                  |
| [#121 error]     | > Menu: 3 secenek          | AI: Bu flow'da   |
| [#120 handed_off]| > Intent: "satis" (0.92)   | hata orani %15.  |
|                  | > Handoff: Gruba atandi    | Node 'Menu'yu    |
|                  |                            | optimize edeyim  |
|                  |                            | mi?              |
+------------------+----------------------------+------------------+
```

## 11. Faz Plani

| Faz | Kapsam | Oncelik |
|-----|--------|---------|
| FM-1a | DB migration + versioning backend + flow builder surum gosterimi | P0 |
| FM-1b | Monitor sayfasi: 3-panel layout + execution list + timeline | P0 |
| FM-1c | Monitor AI chat paneli (flow duzenleme + analiz + oneri) | P1 |
| FM-2 | SSE/WebSocket real-time upgrade | P2 |
