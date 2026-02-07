# Endpoint Registration Rule

## KURAL: Her yeni endpoint ZORUNLU olarak kayıt edilmeli

Yeni bir endpoint eklendiğinde **3 yerde** güncelleme yapılmalı:

### 1. Servis Program.cs - Discovery Endpoint

Her servisin `/api/ops/endpoints` MapGet handler'ındaki `EndpointInfo` listesine yeni endpoint ekle:

```csharp
new() { Method = "POST", Path = "/api/v1/new-endpoint", Description = "Açıklama", Auth = "none", Category = "API" }
```

**Category değerleri:**
| Category | Açıklama |
|----------|----------|
| `API` | Dışarıdan çağrılan business endpoint'ler |
| `Health` | Health/ready probe'ları |
| `Ops` | Dashboard/monitoring endpoint'leri |
| `Legacy` | Eski endpoint'ler (deprecation yolunda) |

**Auth değerleri:**
| Auth | Açıklama |
|------|----------|
| `none` | Auth gerektirmiyor |
| `Basic` | Ops Basic Auth |
| `Bearer` | JWT/Token auth (gelecekte) |

### 2. Postman Collection

`postman/InvektoServis.postman_collection.json` dosyasına yeni request ekle.

İlgili folder'a ekle:
- `Backend - Public API` → Business endpoint'ler
- `Backend - Ops Dashboard` → Ops endpoint'ler
- `ChatAnalysis - Direct` → ChatAnalysis endpoint'ler

### 3. Backend Aggregation (sadece yeni servis eklendiğinde)

Yeni bir mikro servis eklendiğinde `Backend/Program.cs` içindeki `/api/ops/endpoints` handler'ına yeni servisin discovery çağrısını ekle.

## Endpoint Naming Convention

```
/api/v{version}/{resource}          → Business API
/api/ops/{category}/{action}        → Ops/Dashboard
/health                             → Health check (standart)
/ready                              → Readiness probe (standart)
/api/ops/endpoints                  → Discovery (standart - her serviste)
```

## Checklist (Her Yeni Endpoint İçin)

- [ ] Program.cs'de endpoint tanımlandı
- [ ] Discovery listesine (`/api/ops/endpoints`) eklendi
- [ ] Postman collection güncellendi
- [ ] Auth doğru belirtildi (none/Basic/Bearer)
- [ ] Category doğru belirtildi (API/Health/Ops/Legacy)
