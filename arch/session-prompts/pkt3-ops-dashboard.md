# PKT-3: Ops Dashboard — Session Prompt

> **Paket:** PKT-3 Ops Dashboard
> **Scope:** GR-2.5 Otomasyon Dashboard + Log İyileştirme + WA-4 BI Dashboard
> **Risk:** MEDIUM (yeni DB tabloları + yeni Dashboard sayfası + Backend API genişleme, ama yeni mikroservis YOK)
> **Tahmini:** 2 GR, 12+ alt madde, ~20-30 dosya

---

## GR Scope

### GR-2.5: Otomasyon Dashboard + Log İyileştirme

> **Servis:** Dashboard (React) + Backend genişleme
> **Kaynak:** `ideas/phases/phase-2.md` satır 163-189

| # | Alt Madde | Açıklama |
|---|-----------|----------|
| 2.5.1 | Deflection rate | Kaç mesaj otomatik cevaplandı / toplam |
| 2.5.2 | Handoff rate | Kaç tanesi temsilciye devredildi |
| 2.5.3 | Trend grafikleri | Günlük/haftalık trend |
| 2.5.4 | Akıllı Özet Kartları | Log stream'de operasyon özeti |
| 2.5.5 | Log summary field | Log entry'lere `summary` field ekle |
| 2.5.6 | Intent performance | Hangi intent ne kadar çözüyor |
| 2.5.7 | Top unanswered | Bilgi tabanında eksik ne var |
| 2.5.8 | Müşteri bazlı deflection | Tenant bazlı deflection rate |
| 2.5.9 | Basit FRT | First Response Time ölçümü (Phase 6 SLA hazırlığı) |
| 2.5.10 | Conversation metadata | Süre, intent, resolution, sentiment log'u (Phase 6 Mining hazırlığı) |
| 2.5.11 | DB tabloları | `daily_metrics` + `conversation_metadata` |

**Yapılmayacak:**
- ❌ Tam SLA tracker (Phase 6)
- ❌ QA scoring (Phase 6)
- ❌ Revenue attribution (Phase 5)
- ❌ Script compliance check (Phase 4)

### WA-4: BI Dashboard

> **Kaynak:** `arch/active-work.md` WA Fazları tablosu

| # | Alt Madde | Açıklama |
|---|-----------|----------|
| WA-4.1 | Agent performans raporu | Agent bazlı cevaplama hızı, mesaj sayısı, çözüm oranı |
| WA-4.2 | Conversion raporu | Satış dönüşüm oranları, sıcak/soğuk lead takibi |
| WA-4.3 | Trend raporları | Günlük/haftalık mesaj trafik ve kategori trendleri |

**Not:** WA-4 verileri WA-5 C# servisinden (Port 7109) veya direkt DB'den gelebilir. WA-5 şu anda stages 1-3'ü (cleaner, threader, stats) destekliyor. NLP stages 4-7 (intent, FAQ, sentiment, product) PKT-4'te yapılacak — WA-4 Dashboard'u mevcut stage 1-3 verilerini görselleştirir.

---

## DB Şeması (Oluşturulacak)

```sql
-- GR-2.5.11: daily_metrics + conversation_metadata
-- Dosya: arch/db/backend-metrics.sql (YENİ)

CREATE TABLE IF NOT EXISTS daily_metrics (
    id BIGSERIAL PRIMARY KEY,
    tenant_id INTEGER NOT NULL,
    date DATE NOT NULL,
    total_messages INTEGER NOT NULL DEFAULT 0,
    auto_resolved INTEGER NOT NULL DEFAULT 0,
    human_handled INTEGER NOT NULL DEFAULT 0,
    avg_response_time_sec NUMERIC(10,2),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_daily_metrics_tenant_date UNIQUE (tenant_id, date)
);

CREATE TABLE IF NOT EXISTS conversation_metadata (
    id BIGSERIAL PRIMARY KEY,
    tenant_id INTEGER NOT NULL,
    conversation_id TEXT NOT NULL,
    duration_sec INTEGER,
    primary_intent TEXT,
    resolution_type TEXT, -- 'auto_resolved' | 'human_handled' | 'abandoned'
    sentiment_score NUMERIC(3,2), -- -1.00 to 1.00
    agent_id TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_daily_metrics_tenant_date ON daily_metrics(tenant_id, date);
CREATE INDEX idx_conversation_metadata_tenant ON conversation_metadata(tenant_id, created_at);
CREATE INDEX idx_conversation_metadata_intent ON conversation_metadata(tenant_id, primary_intent);
```

---

## Mevcut Dashboard Yapısı

```
src/Invekto.Backend/Dashboard/
├── src/
│   ├── App.tsx                  # Routes: /, /logs, /knowledge
│   ├── main.tsx                 # BrowserRouter
│   ├── lib/
│   │   ├── api.ts               # OpsApiClient (Basic Auth, sessionStorage)
│   │   └── utils.ts             # cn() helper
│   ├── hooks/
│   │   ├── useAuth.ts           # Auth context
│   │   └── usePolling.ts        # Auto-refresh hook
│   ├── pages/
│   │   ├── DashboardPage.tsx    # Health cards, error timeline, dependency map, test panel
│   │   ├── LogsPage.tsx         # Log stream + search
│   │   ├── LoginPage.tsx        # Basic auth login
│   │   └── KnowledgePage.tsx    # Doc upload + FAQ manager
│   └── components/
│       ├── Layout.tsx           # Sidebar nav (Dashboard, Logs, Knowledge)
│       ├── HealthCard.tsx       # Service health + endpoint discovery
│       ├── ErrorTimeline.tsx    # Error stats chart
│       ├── DependencyMap.tsx    # Service dependency vis
│       ├── TestPanel.tsx        # API test panel
│       └── ui/                  # Card, Badge, Button, Input, Select
```

**Tech stack:** React 18 + TypeScript + Vite + TailwindCSS + react-router-dom + recharts (zaten installed)
**Auth:** Basic Auth (sessionStorage), OpsApiClient singleton
**Pattern:** usePolling hook ile auto-refresh, api.ts üzerinden tüm API çağrıları

**Mevcut sayfalar:**
- `/` → DashboardPage (health cards, error timeline)
- `/logs` → LogsPage (log stream)
- `/knowledge` → KnowledgePage (doc upload, FAQ)

**Eklenecek:**
- `/analytics` → AnalyticsPage (YENİ — GR-2.5 metrikleri + WA-4 raporları)
- Layout.tsx sidebar'a "Analytics" nav item ekle

---

## Backend API Yapısı

**Mevcut ops endpoint'ler:** `/api/ops/health`, `/api/ops/logs/*`, `/api/ops/stats/errors`, `/api/ops/services/*/restart`, `/api/ops/endpoints`, `/api/ops/postman`

**Eklenecek endpoint'ler (Backend Program.cs):**

```
GET  /api/ops/analytics/metrics?tenantId=&from=&to=     → daily_metrics data
GET  /api/ops/analytics/deflection?tenantId=&from=&to=  → deflection/handoff rates
GET  /api/ops/analytics/intents?tenantId=&from=&to=     → intent performance breakdown
GET  /api/ops/analytics/unanswered?tenantId=&limit=     → top unanswered questions
GET  /api/ops/analytics/frt?tenantId=&from=&to=         → FRT statistics
GET  /api/ops/analytics/conversations?tenantId=&from=&to= → conversation metadata list
```

**WA-4 endpoint'ler (WhatsAppAnalytics servisinden proxy veya direkt DB):**
```
GET  /api/ops/analytics/wa/agents?tenantId=             → agent performance
GET  /api/ops/analytics/wa/trends?tenantId=&from=&to=   → message trends
```

---

## Veri Toplama Stratejisi

**Metrik verileri nereden gelecek?**

1. **Deflection/Handoff rate:** Automation servisi her mesaj işlediğinde `auto_reply_log` tablosuna yazıyor. Bu veriden aggregation yapılabilir VEYA her işlemde Backend'e callback ile `daily_metrics` artırılabilir.

2. **Intent performance:** Automation servisi `chat_sessions` tablosunda `detected_intent` tutuyor. Bu veriden aggregation.

3. **FRT:** `auto_reply_log` tablosundaki timestamp'lerden hesaplanabilir.

4. **Conversation metadata:** Automation servisi conversation bittiğinde Backend'e metadata POST edebilir, veya periyodik aggregation.

5. **WA-4 verisi:** `whatsapp-analytics.sql` tabloları: `wa_analyses`, `wa_cleaned_messages`, `wa_conversations`, `wa_conversation_stats`, `wa_agent_stats`, `wa_product_mentions`, `wa_hourly_stats` — bunlardan direkt query.

**ÖNERİ:** Aggregation yaklaşımı — Automation/Backend'de real-time counter artırmak yerine, IHostedService ile periyodik (her 5dk) aggregation yaparak `daily_metrics` tablosunu doldurmak daha güvenli ve mevcut servislerde değişiklik minimumda kalır.

---

## Kritik Bağımlılıklar

| Bağımlılık | Durum | Etki |
|-----------|-------|------|
| Dashboard SPA (React 18 + Vite) | ✅ Mevcut | Yeni sayfa + component ekleme |
| Backend Program.cs | ✅ Mevcut | Yeni API endpoint'ler |
| recharts kütüphanesi | ✅ Installed | Trend grafikleri için |
| Automation DB (chat_sessions, auto_reply_log) | ✅ Mevcut | Kaynak veri |
| WA Analytics DB (wa_* tabloları) | ✅ Mevcut | WA-4 kaynak veri |
| daily_metrics + conversation_metadata tabloları | ❌ Yeni | Oluşturulacak |

---

## Değişecek Dosyalar (Tahmin)

### Backend (C#)
- `src/Invekto.Backend/Program.cs` — Yeni `/api/ops/analytics/*` endpoint'ler
- `src/Invekto.Backend/Services/MetricsRepository.cs` — YENİ: daily_metrics + conversation_metadata CRUD
- `src/Invekto.Backend/Services/MetricsAggregationService.cs` — YENİ: IHostedService periyodik aggregation
- `src/Invekto.Shared/DTOs/Analytics/` — YENİ: MetricsDto, DeflectionDto, IntentPerformanceDto vb.

### Dashboard (React/TS)
- `src/Invekto.Backend/Dashboard/src/App.tsx` — `/analytics` route ekle
- `src/Invekto.Backend/Dashboard/src/components/Layout.tsx` — Analytics nav item
- `src/Invekto.Backend/Dashboard/src/pages/AnalyticsPage.tsx` — YENİ: Ana analytics sayfası
- `src/Invekto.Backend/Dashboard/src/components/analytics/` — YENİ: Chart bileşenleri
- `src/Invekto.Backend/Dashboard/src/lib/api.ts` — Analytics API methods

### Arch
- `arch/db/backend-metrics.sql` — YENİ: daily_metrics + conversation_metadata şeması
- `arch/errors.md` — Yeni hata kodları (INV-MT-001~)
- `arch/contracts/analytics-metrics.json` — YENİ: API contract

---

## Lessons Learned (PKT-3 İçin Relevant)

Bu dersler önceki PKT'lerden öğrenilmiştir, PKT-3'te tekrar EDİLMEMELİ:

1. **Her repository query'sinde `tenant_id` WHERE clause ZORUNLU** — mevcut pattern'den kopyalarken bile kontrol et
2. **catch tip BELIRT** — `catch(JsonException)` + `catch(Exception ex)`, bos catch YASAK
3. **return null yazıyorsan NEDEN null döndüğünü logla**
4. **Error fallback null BIRAKMA** — degraded/default değer set et
5. **SPA fallback için `{*path:nonfile}` kullan**
6. **Contract field adını DTO'da BIREBIR kullan**
7. **Yeni endpoint = JWT prefix listesini kontrol et**
8. **Logger kullanırken MEVCUT imzayı kontrol et** — SystemInfo/Warn/Error 1 parametre alıyor
9. **Production.json E:\\ path, port, conn string OTOMATIK doldur**
10. **recharts zaten kurulu** — yeni chart kütüphanesi eklemeye GEREK YOK

---

## İnterview Soruları (Agent'ın Q'ya Sorması Gereken)

Bu sorular paket scope'unu netleştirir, interview sırasında kullanılmalı:

1. **Veri toplama stratejisi:** Real-time counter mı, periyodik aggregation mı? (Önerimiz: aggregation)
2. **WA-4 scope:** WA stages 1-3 verileri yeterli mi, yoksa NLP verisi (PKT-4) beklemeli mi?
3. **Tenant seçimi:** Analytics sayfasında tenant dropdown mı olacak, yoksa tek tenant mı?
4. **Tarih aralığı:** Default görünüm son 7 gün mü, 30 gün mü?

---

## Paket Sırası (GR Execution Order)

1. **DB schema oluştur** → `arch/db/backend-metrics.sql`
2. **Backend: MetricsRepository** → CRUD + aggregation queries
3. **Backend: MetricsAggregationService** → IHostedService (periyodik)
4. **Backend: API endpoints** → `/api/ops/analytics/*`
5. **Shared: DTOs** → Analytics DTO'ları
6. **Dashboard: api.ts** → Analytics API methods
7. **Dashboard: AnalyticsPage** → Ana sayfa + chart bileşenleri
8. **Dashboard: Layout** → Nav item ekle
9. **Build** → Full solution build
10. **Self-review** → CQ1-CQ8 + AQ1-AQ6
11. **/rev** → MCP Codex review

---

## Risk Değerlendirmesi

| Risk | Seviye | Mitigation |
|------|--------|------------|
| Aggregation query performansı | MEDIUM | Partial index + date range sınırı |
| Mevcut servislerde değişiklik | LOW | Aggregation yaklaşımı mevcut servisleri değiştirmez |
| Dashboard build hataları | LOW | Mevcut SPA pattern'i takip et |
| WA-4 veri eksikliği | LOW | Stages 1-3 yeterli, NLP PKT-4'te |
| Codex scope violation | MEDIUM | allowed_files BIREBIR kontrol (3. kez tekrar eden hata!) |
