# Test Plani

> Son guncelleme: 2 Mart 2026
> Prensip: Pragmatik test — kritik akislari koru, gereksiz test yazma
> Deploy: MCP ile otomatik, risk dusuk

---

## 1. Mevcut Durum

| Proje | Framework | Test Sayisi | Durum |
|-------|-----------|-------------|-------|
| **InvektoServices** | xUnit 2.5.3 + FluentAssertions + Bogus + NSubstitute | 30+ test (3 proje + 4 E2E) | IYYY |
| **InvektoServices Tools** | Playwright (Python) + Custom Simulator (Node.js) | UI + E2E senaryolari | VAR |
| **InvektoChat** | - | 0 | YOK |
| **InvektoHelp** | - | 0 | YOK |
| **InvektoWebsite** | - | 0 | YOK |

### InvektoServices Test Detayi

**Test Projeleri:**
- `tests/Invekto.Backend.Tests/` — Health, Ops API, Log Reader
- `tests/Invekto.ChatAnalysis.Tests/` — Analyze API, Health
- `tests/InvektoServis.Tests/` — Kapsamli:
  - AgentAI: Escalation, Feedback, Health, OrderCard, SuggestFlow
  - Backend: Auth, Health, Lead API, Ops API
  - Knowledge: Document Upload, FAQ CRUD, Health, Intent, Search, Template Catalog
  - Outbound: Broadcast, Campaign, Consent, DataDeletion, DeliveryStatus, Health, OptOut, TemplateCRUD
  - E2E: AgentSuggestWithKnowledge, BroadcastLifecycle, CampaignFullLifecycle, LeadToOutbound

**Araclar:**
- `tools/simulator/` — Node.js E2E senaryo runner
- `tools/ui-tester/` — Python Playwright otomasyon
- `tools/whatsapp-analyzer/` — Mesaj analiz testi

---

## 2. Test Stratejisi

### Prensip: Risk Bazli Test

```
YUKSEK RISK (mutlaka test yaz):
  ├── Billing / odeme akislari (PARA!)
  ├── Auth / permission (GUVENLIK!)
  ├── Tenant izolasyonu (VERI SIZINTISI!)
  └── Kota enforcement (IS KURALLARI!)

ORTA RISK (test var, yenilerini ekle):
  ├── CRUD operasyonlari (mevcut testler yeterli)
  ├── Mikroservis iletisimi (health check'ler var)
  └── Flow Builder (E2E senaryo ile)

DUSUK RISK (test gereksiz):
  ├── InvektoWebsite (statik site, MCP deploy, hata olsa gorursun)
  ├── InvektoHelp (icerik sitesi, build = test)
  └── InvektoChat (MVP, hizla degisiyor, stabillesince yaz)
```

### Her Proje Icin Plan

#### InvektoServices (Mevcut + Genislet)

| Alan | Mevcut | Eklenecek | Oncelik |
|------|--------|-----------|---------|
| Auth/Permission | AuthTests var | **FeatureGuard middleware testleri** | P0 |
| Billing | Yok | **Subscribe, ChangePlan, Cancel, Webhook testleri** | P0 |
| Kota | Yok | **QuotaCheck middleware testleri** | P0 |
| Tenant izolasyonu | Yok | **Cross-tenant data leak testi** | P0 |
| Onboarding | Yok | **Onboarding API testleri** | P1 |
| Mevcut servisler | 30+ test | Yeni endpoint'lere test ekle | Surekli |

**Kural:** Her yeni billing/permission endpoint'i icin en az 1 happy path + 1 error path test.

#### InvektoWebsite (Minimal)

| Alan | Ne Yapilacak | Oncelik |
|------|-------------|---------|
| Build testi | `npm run build` basarili mi? (CI icin) | P2 |
| Form testi | Iletisim formu calisiyor mu? (Playwright) | P3 |
| Sayfa 404 | Kirik linkler var mi? | P3 |

**Simdilik:** Manuel smoke test + MCP deploy yeterli.

#### InvektoChat (Ertelensin)

| Alan | Ne Yapilacak | Oncelik |
|------|-------------|---------|
| - | MVP stabil olana kadar test yazma | ERTELENSIN |
| - | Stabil oldugunda: Jest + React Native Testing Library | ILERİDE |

#### InvektoHelp (Build = Test)

| Alan | Ne Yapilacak | Oncelik |
|------|-------------|---------|
| Build testi | `next build` basarili mi? | YETERLI |
| Icerik kontrolu | MDX parse hatasi var mi? | Build basarisina dahil |

---

## 3. Yeni Testler (Faz 1-2 ile Birlikte)

### 3.1 Permission Middleware Testleri (Faz 1)

```
TEST: Feature guard — feature acik
  Given: Tenant plan_tier = "pro", FlowBuilder = "premium"
  When: GET /api/v1/flows
  Then: 200 OK

TEST: Feature guard — feature kapali
  Given: Tenant plan_tier = "basic", Knowledge = "off"
  When: GET /api/v1/knowledge/search
  Then: 403 { "error": "INV-FEATURE-DISABLED" }

TEST: Feature guard — tier yetersiz
  Given: Tenant plan_tier = "basic", FlowBuilder = "basic"
  When: POST /api/v1/flows (premium ozellik kullaniyor)
  Then: 403 { "error": "INV-TIER-INSUFFICIENT" }

TEST: Kota asildi
  Given: Tenant usage = 1001, limit = 1000
  When: POST /api/v1/messages
  Then: 429 { "error": "INV-QUOTA-EXCEEDED" }
```

### 3.2 Billing Testleri (Faz 2)

```
TEST: Yeni abonelik olustur
  Given: Tenant aboneligi yok
  When: POST /api/v1/billing/subscribe { plan: "pro" }
  Then: 200, subscription status = "active", fatura olusur

TEST: Plan degistir (upgrade)
  Given: Tenant plan = "basic"
  When: POST /api/v1/billing/change-plan { plan: "pro" }
  Then: 200, plan_tier = "pro", features guncellenir

TEST: Plan degistir (downgrade)
  Given: Tenant plan = "pro"
  When: POST /api/v1/billing/change-plan { plan: "basic" }
  Then: 200, donem sonunda etkili olacak

TEST: Abonelik iptal
  Given: Tenant plan = "pro", aktif
  When: POST /api/v1/billing/cancel
  Then: 200, status = "cancelled", donem sonuna kadar aktif

TEST: Gecikmi odeme → suspend
  Given: Tenant status = "past_due", 7 gun gecti
  When: NightlyBatch calisir
  Then: status = "suspended", features kapanir

TEST: Webhook — basarili odeme
  Given: QNB webhook gelir, odeme basarili
  When: POST /api/v1/billing/webhook
  Then: Fatura "paid" olarak guncellenir

TEST: Tenant izolasyonu
  Given: Tenant A (id=100) ve Tenant B (id=200)
  When: Tenant A, Tenant B'nin faturasini isterse
  Then: 403 veya 404
```

### 3.3 E2E Senaryo (Simulator ile)

```
SENARYO: Firma yasam dongusu
  1. INMA'dan firma olusturulur (mock)
  2. Ilk giris → onboarding wizard
  3. Sektor secimi → sablon kurulumu
  4. Abonelik olusturma (pro plan)
  5. Mesaj gonderme (kota icinde)
  6. Flow olusturma
  7. Kota kontrolu
  8. Plan degistirme (basic'e downgrade)
  9. Premium feature erisim kontrolu (403)
  10. Abonelik iptal
```

---

## 4. Test Calistirma

### Manuel (Dev sirasinda)

```bash
# Tum testler
cd /c/CRMs/InvektoServices
dotnet test InvektoServis.sln

# Sadece billing testleri
dotnet test tests/InvektoServis.Tests --filter "FullyQualifiedName~Billing"

# Sadece permission testleri
dotnet test tests/InvektoServis.Tests --filter "FullyQualifiedName~FeatureGuard"

# UI testi (Playwright)
cd tools/ui-tester
python cli.py --target localhost:5000
```

### Deploy Oncesi (Otomatik)

```
MCP deploy komutu calistirildiginda:
  1. dotnet test (backend testleri)
  2. Build basarili mi?
  3. Evet → deploy
  4. Hayir → dur, hata raporu
```

**Not:** CI/CD pipeline su an yok. MCP + pre-deploy test yeterli. Takim buyuyunce GitHub Actions eklenebilir.

---

## 5. AI Katki Ozeti

| Gorev | AI Payi |
|-------|---------|
| Test kodu yazma (xUnit) | %90 (Claude Code yazar) |
| Test senaryosu tasarimi | %70 (AI onerir, Q dogrular) |
| E2E senaryo olusturma | %80 |
| Test debug / fix | %85 |
| Test coverage analizi | %90 |
