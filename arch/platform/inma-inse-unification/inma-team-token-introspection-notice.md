# INMA Takımına Bildirim — INSE JWT Validation Pattern Değişikliği

> **Tarih:** 2026-04-16
> **Üretim Deploy:** 2026-04-16 19:33 UTC (commit `bfd57ae`)
> **Etki:** INSE Backend tarafı — INMA tarafında **ek geliştirme/değişiklik GEREKMEZ**
> **Hedef Okuyucu:** INMA backend ekibi, INMA Angular ekibi

---

## TL;DR

INSE Backend artık her INMA JWT validation işlemini WapCRM `/api/invekto/welcome` endpoint'ine sorarak yapacak. INMA tarafında değişiklik gerekmiyor — mevcut welcome endpoint zaten tüketiliyor. Önceki HS256 symmetric signature verify kaldırıldı çünkü:

- INMA'nın signing key'i WapCRM backend'in özel sırrı, paylaşılmıyor
- INMA Angular tarafı da aynı şekilde decode-only çalışıyor (jwt-decode)
- Frontend/downstream'de signature verify mimari olarak yanlış (key dağıtım = compromise)
- Forge token defansı için tek doğru yol: token'ı sahibine sormak (introspection)

5 dakikalık per-token cache ile beklenen `welcome` çağrı artışı **dakika başına ortalama 1-2** (tenant 5050 boyutunda).

---

## Öncesi vs Sonrası

### ÖNCEDEN (yanlış pattern)

INSE Backend'de `InmaJwtValidator` HS256 SymmetricSecurityKey ile imza doğruluyordu. Bunun için INMA'nın signing key'ine ihtiyaç vardı (32+ byte). 2026-04-16 öncesi bu key INMA ile koordine edilmemişti, prod'da `InmaAuth.SecretKey=""` set edilerek decode-only fallback'e düşürülmüştü (geçici güvenlik bypass'ı).

Üç problem:
1. **Mimari yanlış:** INMA signing key'i frontend'lere/downstream servislere paylaşılmıyor, paylaşılmamalı
2. **Bypass riski:** `decode-only fallback` forge token kabul edebilir (signature verify yok, payload doğrudan claims olarak okunuyor)
3. **Operational koordinasyon:** key rotation/algoritma değişikliği INMA ile koordineli SecretKey config update gerektiriyordu

### SONRASI (doğru pattern, deployed 2026-04-16)

```
Client → INSE API endpoint
         (Bearer <inma_jwt>)
            ↓
INSE Backend
   ↓ ExtractTenantFromBearer / ValidateOpsAuth / inma/auth/exchange
   ↓ InmaTokenIntrospector.ValidateAsync(token, ct):
       ├─ Cache lookup (key = SHA256(token).Substring(0,16))
       │   HIT (5dk içinde önce valid edilmiş) → Context return
       │   MISS ↓
       ├─ JWT decode (claims parse, signature verify YOK)
       │
       └─ HTTP GET {ApiBaseUrl}/api/invekto/welcome
              Authorization: Bearer <inma_jwt>
              Timeout: 5s
                   ↓
              200 OK → Cache set (5dk TTL) + Context return
              401 → INV-AUTH-001/002 (token expired/invalid) → INSE 401
              5xx/network → INV-AUTH-008 (introspection unavailable) → INSE 503
```

---

## INMA Tarafına Etkisi

### Değişiklik GEREKEN: HİÇBİR ŞEY

`/api/invekto/welcome` endpoint'i zaten mevcut ve INSE Dashboard tarafından da kullanılıyor (welcome metadata için). INSE Backend artık bu endpoint'i validation amaçlı da çağırıyor.

### Beklenen Trafik Artışı

5 dakika TTL cache + token başına 1 welcome çağrısı:

| Tenant boyutu | Aktif token sayısı | Welcome çağrı/dk (cache miss) |
|---------------|-------------------|-------------------------------|
| Küçük (1-5 user) | 1-5 token | ~0.2-1/dk (token başına 5dk'da 1) |
| Orta (10-50 user) | 10-50 token | ~2-10/dk |
| Büyük (100+ user) | 100+ token | ~20+/dk |

**Önemli:** Cache hit rate ≥%95 (kullanıcı 5dk içinde >>1 API çağrısı yapar). Yani %95 sorgular WapCRM'e ulaşmadan INSE içinde cevaplanır.

### Welcome Endpoint Davranış Beklentileri

INSE Backend'in introspection için welcome endpoint'inden beklediği davranış:

1. **200 OK**: Token geçerli (signature INMA'nın key'iyle valid + henüz expire olmamış)
2. **401 Unauthorized**: Token expired veya invalid signature → INSE kullanıcıyı 401 ile reddedecek
3. **403 Forbidden** veya diğer 4xx: INSE 401 olarak yorumlayacak (token problemi)
4. **5xx server error**: INSE 503 ile yanıtlayacak (transient outage), kullanıcıya "INMA servisine ulaşılamadı" mesajı

Welcome response body'si introspection için **kullanılmıyor** — sadece HTTP status önemli. INSE claims'leri zaten token payload'unu decode ederek alıyor.

---

## Güvenlik Modeli

### Forge Token Defansı

Saldırgan INMA imza key'ini bilmediği için forge JWT üretip INSE'ye gönderse:

1. INSE cache'de bu token yoksa (cache miss) → welcome çağrısı yapılır
2. INMA backend kendi key'iyle signature verify yapar → 401
3. INSE 401 ile reddeder → INSE JWT üretilmez → tenant erişimi YOK

Cache hit ancak **önceden welcome 200 dönmüş** token'lar için olur. Forge edilmiş token welcome'a hiçbir zaman 200 dönemez (INMA imza verify FAIL), dolayısıyla cache'e asla girmez.

### Revoke Latency

INMA tarafında bir kullanıcı pasifleştirildiğinde / token revoke edildiğinde, INSE'deki cache hit pencereleri max **5 dakika** stale data döndürebilir. Sonraki cache miss'te welcome 401 alır → kullanıcı 401 ile reddedilir.

5 dakika kabul edilebilir bir defense-in-depth penceresi olarak değerlendirildi. Daha sıkı SLA gerekirse:
- **Anlık revocation:** INMA → INSE webhook (`POST /api/v1/inma/token-revoked` ile cache invalidation) — backlog
- **TTL düşürme:** 5dk → 1dk (welcome trafiği 5x artar)

### PII Koruması

Cache key = `inma:tk:` + SHA256(token).Substring(0,16) (64-bit hex prefix). Raw token **memory'de cache key olarak saklanmıyor** — memory dump senaryosunda token leak olmaz.

---

## INSE Tarafında Etkilenen Endpoint'ler

INMA JWT ile authenticate olan tüm INSE endpoint'leri artık welcome introspection kullanıyor:

| Endpoint | Path | Trigger |
|----------|------|---------|
| INMA SSO Exchange | `POST /api/v1/inma/auth/exchange` | URL SSO flow (?accesstoken=) |
| INMA SSO Login | `POST /api/v1/inma/auth/login` | Mock/test login |
| Settings/Instance APIs | `/api/v1/settings/instances/*` (12 endpoint) | Bearer INMA JWT direct |
| Tenant Analytics | `GET /api/v1/analytics/tenant/summary` | Bearer INMA JWT direct |
| Ops Auth | `/api/ops/*` (admin role check) | Bearer INMA JWT (admin) |
| Ops Audit Identity | (internal logging) | Identity extraction |

**13+ endpoint** her INMA JWT validation'da welcome çağrısı tetikleyebilir (cache miss durumunda).

---

## Hata Senaryoları & Status Code Mapping

INSE artık şu HTTP status code mapping'i yapıyor:

| Senaryo | INSE Response | Error Code | Mesaj |
|---------|---------------|-----------|-------|
| Token geçerli | 200 + tenant context | — | — |
| Welcome 401 (expired/invalid) | 401 + envelope | `INV-AUTH-001` veya `INV-AUTH-002` | "INMA token expired or invalid" |
| Welcome 5xx / network fail / timeout | 503 + envelope | `INV-AUTH-008` | "INMA servisine ulaşılamadı, kısa süre sonra tekrar deneyin." |
| Token format invalid (decode fail) | 401 + envelope | `INV-AUTH-002` | "Token format is not a valid JWT" |
| Local exp claim < now-60s | 401 + envelope (welcome çağrısı yapılmaz) | `INV-AUTH-002` | "Token expired (local exp claim)" |
| ApiBaseUrl yapılandırılmamış | 503 + envelope | `INV-AUTH-008` | "INMA ApiBaseUrl not configured" |

---

## Test Senaryoları (INMA tarafının doğrulayabileceği)

INMA QA tarafından doğrulanması beklenen senaryolar:

### 1. Happy path — Yeni kullanıcı
- INMA login → JWT al
- INSE'de bu JWT ile `/api/v1/settings/instances` çağır
- **Beklenen:** İlk istekte INSE → INMA welcome 200 (cache miss). Sonraki istekler 5dk içinde welcome çağırmadan tamamlanır (cache hit).

### 2. Token expire
- 8 saat sonra (token natural expire) aynı JWT ile istek
- **Beklenen:** INSE → INMA welcome 401 → INSE kullanıcıya 401 + `INV-AUTH-001`

### 3. Token revoke (manuel pasifleştirme)
- INMA admin panelinden kullanıcıyı pasifleştir
- **Beklenen:** Max 5dk içinde (cache TTL süresinde) INSE 401 ile reddetmeye başlar

### 4. INMA outage simülasyonu
- WapCRM API'yi geçici olarak durdur (maintenance vs.)
- INSE'ye istek gelmesi
- **Beklenen:** INSE 503 + `INV-AUTH-008` mesajı; INMA tekrar ayağa kalkınca otomatik çalışır (cache miss → welcome 200)

### 5. Forge token testi
- Random/değiştirilmiş JWT (signature invalid) ile INSE'ye istek
- **Beklenen:** INSE → INMA welcome 401 → INSE 401 + `INV-AUTH-002`. INSE JWT üretilmez, hiçbir tenant erişimi sağlanmaz.

---

## INMA Tarafından Yapılması GEREKEN: HİÇBİR ŞEY

Bu refactor INSE Backend tarafında bağımsızdır. INMA tarafında:

- ✅ `/api/invekto/welcome` endpoint zaten mevcut, davranış değişikliği YOK
- ✅ Signing key paylaşımı GEREKMEZ (önceki UP0.2 talebi İPTAL)
- ✅ JWT format/claim değişikliği YOK (CompanyCode, ChatRole, FullName, Lang, InseFeatures aynı)
- ✅ INMA Angular tarafı dokunmaz (mevcut SSO postMessage akışı korunur)

Sadece **bilgilendirme** amaçlı bu doküman. INMA QA isteğe bağlı yukarıdaki senaryolarla doğrulayabilir.

---

## Sorularınız İçin

INSE tarafından bu refactor'a sahip olan kişi: **Q (Taner)** — InvektoServices repo `arch/plans/20260416-inma-token-introspection.json` ve commit `bfd57ae` referans.

Detay teknik dokümantasyon (INSE iç):
- `arch/lessons-learned.md` (3 yeni lesson: DI pattern, structured contract, single source of truth)
- `arch/session-memory.md` (deploy log, execution queue task 4e)

---

**Son Not:** Önceki "INMA public key paylaşımı" talebi (UP0.2 backlog) **tamamen iptal edilmiştir**. Bu pattern uzun vadede daha sürdürülebilir, daha güvenli ve INMA tarafına ek koordinasyon yükü getirmiyor.

---

## EK: `/api/v1/inma/nav` — Seçenek C Implementasyonu (2026-04-17)

INMA Angular dinamik menü implementasyonunuzu aldık. Backend tarafı bu çağrıyı destekleyecek şekilde deploy edildi (commit `c0536f9`, Backend redeploy 20:10 UTC).

**Yapılan değişiklikler:**

1. **CORS Policy `InmaNavCors`** aktif, sadece `/api/v1/inma/nav` endpoint'i için:
   - Origins: `https://app.wapcrm.net`, `https://app.wappflex.com`, `https://developer.wapcrm.net`, `http://localhost:4200`
   - Methods: `GET` (sadece read)
   - Headers: any (Bearer + standart preflight)
   - `AllowCredentials = false` (Angular HttpClient Bearer header explicit gönderir)

2. **JwtAuth middleware whitelist'inden çıkarıldı** — endpoint kendi auth gate'ini çalıştırır.

3. **Endpoint INMA JWT'yi welcome introspection ile validate eder:**
   - INSE JWT (Dashboard React) → JwtValidator (local signature verify, fast path)
   - INMA JWT (Angular) → InmaTokenIntrospector (welcome `/api/invekto/welcome` + 5dk cache)
   - Her ikisi de aynı `ExtractTenantFromBearer` helper üzerinden geçer

**Hata davranışı (Angular fallback için):**

| Senaryo | Status | Body |
|---------|--------|------|
| Token geçerli | 200 | `{ sections: [...] }` |
| Token expired/invalid (welcome 401) | 401 | `INV-AUTH-001` veya `INV-AUTH-002` envelope |
| INMA welcome unreachable (network/timeout) | 503 | `INV-AUTH-008` envelope |
| Bearer header missing | 401 | `INV-AUTH-002` envelope |
| Origin CORS dışı | (preflight reddedilir) | Browser CORS error |

**Test edebileceksiniz:**

```http
GET https://ai.invekto.com/api/v1/inma/nav
Authorization: Bearer <INMA JWT>
Origin: https://developer.wapcrm.net (veya http://localhost:4200)
```

Beklenen: 200 + `{ sections: [...] }` Lucide kebab-case icon string'leri ile.

**Not:** License/yetki filtresi henüz aktif değil (full tenant set döner). Tenant'a göre özelleştirilmiş menü backlog'da — ilerleyen pakette eklenecek. Şimdilik Angular tarafının Material Icons mapping + fallback menü implementasyonu yeterli.

---

## EK: White-label Origin Genişletmesi — `app.wappflex.com` (2026-06-11)

Medipol (MLPCM) tenant'ı WapCRM white-label portalı `https://app.wappflex.com` üzerinden INSE dashboard'u embed ediyor. Aşağıdaki listeler bu origin'i kapsayacak şekilde genişletildi (bridge whitelist + CORS listesi özdeş tutulur — 20260416 paketi Q1 kuralı):

1. **SPA postMessage bridge whitelist** (`INMA_ALLOWED_ORIGINS`): + `https://app.wappflex.com`
2. **CORS `InmaNavCors`**: + `https://app.wappflex.com`
3. **`apiBaseUrl` regex**: `*.wapcrm.net` yanında `*.wappflex.com` host'ları da kabul edilir (https zorunlu, tek seviye subdomain).

**INMA takımından doğrulama beklenen:** wappflex shell'in gönderdiği `inma:auth` mesajındaki `apiBaseUrl` değeri ve wappflex JWT'lerinin `https://testapi.wapcrm.net/api/invekto/welcome` introspection'ı ile doğrulanabildiği (aynı backend/signing key varsayımı — MLPCM daha önce `app.wapcrm.net` üzerinden çalıştığı için beklenen davranış).
