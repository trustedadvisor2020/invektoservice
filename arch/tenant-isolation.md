# Tenant Isolation Kurallari

> **Source of truth:** Bu dosya tenant izolasyonu ve SuperAdmin impersonate icin canonical kurallardir.

## Temel Prensipler

1. **TenantId INMA'dan gelir.** InvektoServices tenant olusturmaz/silmez. Sadece INMA JWT'den veya webhook `?companyId=` parametresinden okur.
2. **Her tablo `tenant_id INTEGER NOT NULL REFERENCES tenant_registry(tenant_id)` icerir.** Istisna: `message_log` (SuperAdmin, FK yok, `tenant_id=0` izinli).
3. **Her SQL sorgusu `WHERE tenant_id = @tid` icerir.** Istisna yok. Unutmak = cross-tenant data leak.
4. **tenantId her zaman method parametresidir.** Constructor'da veya field'da TUTULMAZ. Singleton repo + concurrent request = leak riski.

## TenantRepositoryBase

**Dosya:** `src/Invekto.Shared/Data/TenantRepositoryBase.cs`

Tenant-scoped repo'lar icin abstract base class. Sagladiklari:
- `protected readonly PostgresConnectionFactory _db`
- `protected readonly JsonLinesLogger _logger`
- Constructor null guard

**Kimler inherit eder:**
- Tenant-scoped veri okuyup yazan tum repo'lar (AgentAI, Automation, Appointments, Outbound, vb.)

**Kimler inherit ETMEZ:**
- `KnowledgeRepository` — `KnowledgeConnectionFactory` (pgvector) kullanir
- `WhatsAppAnalytics/AnalyticsRepository` — `AnalyticsConnectionFactory` kullanir
- `TenantRegistryRepository` — tenant_registry tablosunun kendisini okur, tenant-scoped degil

## SuperAdmin Impersonate (Option C)

SuperAdmin herhangi bir tenant'a "giris yapar". Cross-tenant query yoktur; her zaman tek tenant context'inde calisir.

### Akis

```
SuperAdmin (Basic Auth) ──→ GET /api/ops/tenants ──→ Firma listesi
     │
     ├─ "Giris Yap" (tenant #42)
     ├──→ POST /api/ops/tenants/42/impersonate
     │    ├─ ValidateOpsAuth ✓
     │    ├─ tenant aktif mi? ✓
     │    └─ GenerateToken(42, "admin", "ops_impersonate", 8h)
     │
     ├─ Frontend JWT kaydeder → window.location.href = '/'
     ├─ Artik tenant #42 admin'i → standard JwtAuthMiddleware calisir
     └─ Banner "Cikis" → removeTokens() → Basic Auth'a donus
```

### Guvenlik Kurallari

| Kural | Aciklama |
|-------|---------|
| `id <= 0` → 400 | SuperAdmin sentinel (tenant_id=0) impersonate edilemez |
| `!is_active` → 403 | Pasif tenant'a giris yapilamaz |
| `ValidateOpsAuth` gate | Sadece ops admin (Basic Auth veya role=admin JWT) cagirabilir |
| `source=ops_impersonate` | JWT'de kaynak belirtilir, frontend banner gosterir |
| Audit log | Her impersonate islem log'a yazilir |

### JWT Detaylari

Impersonate JWT claims:
- `tenant_id` = hedef tenant
- `user_id` = 0 (gercek kullanici degil)
- `role` = admin
- `source` = ops_impersonate
- Expiry: 8 saat

Bu JWT mevcut `JwtAuthMiddleware` tarafindan normal admin JWT gibi islenir. Ek middleware veya TenantContext degisikligi GEREKMEZ.

### Frontend Davranisi

- **Banner:** Impersonate aktifken sayfanin ustunde amber banner + "Cikis" butonu
- **Sidebar:** opsOnly items gizlenir (tenantId ≠ 0) — SuperAdmin tenant'in gorusunu gorur
- **Cikis:** `removeTokens()` → JWT silinir, Basic Auth credentials (in-memory) korunur → ops mode

### Endpoint'ler

| Method | Path | Auth | Aciklama |
|--------|------|------|---------|
| GET | `/api/ops/tenants` | Ops (Basic/Bearer admin) | Tum tenantlari listele |
| POST | `/api/ops/tenants/{id}/impersonate` | Ops (Basic/Bearer admin) | Tenant admin JWT uret |

### Error Codes

| Code | Aciklama |
|------|---------|
| INV-BE-011 | Tenant list query failed |
| INV-BE-012 | Tenant impersonate failed |

## Yeni Repo Yazarken Checklist

- [ ] `WHERE tenant_id = @tid` HER sorguda var mi?
- [ ] tenantId method parametresi mi (field degil)?
- [ ] TenantRepositoryBase'den inherit edildi mi? (uygunsa)
- [ ] `NpgsqlException` endpoint'te catch edilip error code ile donuluyor mu?
- [ ] nullable kolonlar `reader.IsDBNull()` ile kontrol ediliyor mu?
