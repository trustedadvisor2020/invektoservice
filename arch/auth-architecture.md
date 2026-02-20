# Auth Architecture

> **Source of truth:** Tum auth mekanizmalari, JWT tipleri ve erisim kaliplari.

## JWT Token Tipleri

Tum INSE JWT'ler `JwtGenerator.GenerateToken()` ile uretilir. Claims: `{ tenant_id, user_id, role, source }`.

| source Claim | role | tenant_id | Expiry | Uretildigi Yer | Kullanan |
|---|---|---|---|---|---|
| `backend_proxy` | `service` | arayanin tenantId | 5 dk | `GenerateServiceToken()` | Backend → Knowledge/diger servis cagrisi |
| `flow_builder_api_key` | `flow_builder` | tenant | 8 sa | `POST /api/v1/flow-builder/auth/login` | FlowBuilder SPA → Automation API |
| `ops_impersonate` | `admin` | hedef tenantId | 8 sa | `POST /api/ops/tenants/{id}/impersonate` | SuperAdmin firma icinde calisma |
| `ops_quicklogin` | `admin` | `0` | 8 sa | `POST /api/v1/ops/auth/quicklogin` | Dev-mode SuperAdmin (MockEnabled) |
| `inma_mock` | `admin` | `1` | 8 sa | `POST /api/v1/inma/auth/mock-login` | Dev-only INMA simulasyonu |

**INMA JWT'ler** (disaridan gelen) farkli claim isimleri tasir: `CompanyId`, `ChatRole`, `FullName`, `InseFeatures`. Exchange endpoint ile INSE JWT'ye donusturulur.

---

## Erisim Kaliplari

### A: INMA SSO Kullanici (URL Token)

```
INMA → ?accesstoken=<JWT> → useAuth URL parse → storeTokens
     → exchangeInmaToken() → POST /api/v1/inma/auth/exchange
     → INSE JWT → fb_session (FlowBuilder icin)
```

- Dashboard `useAuth.ts` URL'den token'i cikarir, state'e yazar, URL'den siler
- `exchangeInmaToken()` fire-and-forget: INMA JWT → INSE JWT donusumu
- FlowBuilder iframe `localStorage['fb_session']` okur

### B: INMA Credential Login (Form)

```
Dashboard login form → POST /api/v1/inma/auth/login → INMA proxy → JWT
```

### C: Ops Admin (Basic Auth)

```
Browser → Authorization: Basic <base64> → ValidateOpsAuth() → opsUsername/opsPassword
```

- JWT yok, tenant context yok
- `sessionStorage['ops_auth']` icinde saklanir
- Tum `/api/ops/*` endpoint'lere erisim

### D: Quick Login (Dev SuperAdmin)

```
POST /api/v1/ops/auth/quicklogin → MockEnabled gate → JWT(tenant_id=0, role=admin, source=ops_quicklogin)
```

### E: SuperAdmin Impersonate

```
POST /api/ops/tenants/{id}/impersonate → ValidateOpsAuth gate
     → JWT(tenant_id=hedef, role=admin, source=ops_impersonate)
```

Detay: `arch/tenant-isolation.md`

### F: Service-to-Service (Backend Proxy)

```
JwtGenerator.GenerateServiceToken(tenantId) → 5dk JWT(role=service, source=backend_proxy)
```

### G: Webhook IP Whitelist Bypass

```
Trusted IP (INMA server) → ?companyId=X → JwtAuthMiddleware IP check
     → Synthetic TenantContext(TenantId=X, UserId=0, Role=service)
```

- Sadece Backend'de aktif (`Webhook:AllowedIps` config)
- IPv6-mapped IPv4 normalize edilir (`::ffff:x.x.x.x` → `x.x.x.x`)
- `companyId` yoksa veya gecersizse → 400 (JWT'ye fallback YAPILMAZ)

### H: FlowBuilder API Key

```
POST /api/v1/flow-builder/auth/login → { tenant_id, api_key }
     → tenant_registry.settings_json->flow_builder_api_key kontrol
     → JWT(role=flow_builder, source=flow_builder_api_key)
```

---

## ValidateOpsAuth

`Backend/Program.cs` icinde tanimli local function. Tum `/api/ops/*` endpoint'lerinde kullanilir (~40+ endpoint).

Kabul ettigi auth tipleri:
1. **Basic Auth:** `opsUsername:opsPassword` (config'den)
2. **Bearer JWT (INSE):** `role == "admin"` (quicklogin, impersonate)
3. **Bearer JWT (INMA):** `ChatRole → role == "admin"` (INMA admin kullanicilar)

**KURAL:** Yeni ops endpoint eklediginde `ValidateOpsAuth` gate'i ILKE satir olarak ekle.

---

## JWT Middleware — Servis Bazli Prefix

| Servis | Auth-Gerektiren Prefix'ler | IP Whitelist |
|---|---|---|
| Backend | `/api/v1/webhook/`, `/api/v1/automation/`, `/api/v1/outbound/`, `/api/v1/flow-builder/flows/`, `/api/v1/attribution/`, `/api/v1/leads/` | EVET |
| Automation | `/api/v1/webhook/`, `/api/v1/flows/`, `/api/v1/faq/`, `/api/v1/simulation/`, `/api/v1/onboarding/`, `/api/v1/returns/` | Hayir |
| Knowledge | `/api/v1/knowledge/` | Hayir |
| AgentAI | `/api/v1/` | Hayir |
| Appointments | `/api/v1/` | Hayir |
| Integrations | `/api/v1/` | Hayir |
| Outbound | `/api/v1/` | Hayir |
| WhatsAppAnalytics | `/api/v1/wa/` | Hayir |
| Marketing | `/api/v1/` | Hayir |
| ChatAnalysis | JWT middleware YOK (Backend internal call) | Hayir |

**KURAL:** `/health`, `/ready`, `/api/ops/endpoints` her zaman public. Yeni prefix eklediginde `UseJwtAuth()` listesini guncelle.

---

## tenant_id = 0 Konvansiyonu

- `tenant_id = 0` = **SuperAdmin sentinel**
- `ops_quicklogin` JWT'si `tenant_id=0` uretir
- Sidebar `opsOnly` filter: `session.tenantId === 0` → opsOnly items gorunur
- `message_log` tablosu FK olmadan `tenant_id=0` kabul eder
- **Impersonate edilemez** (`id <= 0` → 400)

---

## Dosyalar

| Dosya | Amac |
|-------|------|
| `src/Invekto.Shared/Auth/JwtGenerator.cs` | Token uretimi |
| `src/Invekto.Shared/Auth/JwtValidator.cs` | INSE token dogrulama |
| `src/Invekto.Shared/Auth/InmaJwtValidator.cs` | INMA token dogrulama |
| `src/Invekto.Shared/Auth/TenantContext.cs` | Immutable tenant context (tenant_id, user_id, role) |
| `src/Invekto.Shared/Middleware/JwtAuthMiddleware.cs` | Shared middleware (JWT + IP bypass) |
| `src/Invekto.Backend/Dashboard/src/hooks/useAuth.ts` | React auth hook |
| `src/Invekto.Backend/Dashboard/src/lib/api.ts` | Token saklama, session decode, isImpersonating |
