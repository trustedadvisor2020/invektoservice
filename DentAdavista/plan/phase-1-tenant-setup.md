# Faz 1 — Tenant Provisioning (INMA-Native)

**Süre:** 0.5 gün | **Bağımlılık:** Faz 0 + Unified P0 (SSO + unified tenant)

## Hedef
Dent Adavista **INMA'da firma olarak** var (veya eklenir). INSE `CompanyCode=dentadavista` otomatik tenant row açar. Kullanıcılar INMA'ya login, INSE özelliklerini aynı UI'dan kullanır.

> **Mimari:** INMA = source of truth (firma + kullanıcı + mesaj). INSE = enrichment layer (AI/flow/custom fields). Detay: `unified-platform-architecture.md`

## Adımlar

### 1.1 INMA Firma Kaydı (source of truth)
- [ ] INMA'da Dent Adavista firma kaydı var mı kontrol et — yoksa INMA admin ile aç
- [ ] `CompanyCode` = `dentadavista` (INSE tenant_id ile birebir)
- [ ] INMA API ayarları: `X-CIB-SecretKey` al (screenshot: `4e048de4-...`)
- [ ] INMA WebHook URL: `https://{inse-domain}/api/inbound/inma/dentadavista` kaydet

### 1.2 INSE Tenant Auto-Provision
- [ ] INSE `tenants` tablosuna `id=dentadavista`, `name`, `locale_default=en-US`, `locale_fallback=tr-TR`
- [ ] **Event-driven:** INMA firma create event → INSE auto-provision (unified P0, madde 2)
- [ ] Feature flags (INMA'dan çek): `ai_agent_enabled=true`, `flow_builder=true`, `custom_fields_max=50`

### 1.2 Branding
- [ ] Logo upload (tenant assets)
- [ ] Primary/secondary renk (müşteri kurumsal)
- [ ] E-mail gönderen adı: "Güneş — Dent Adavista"
- [ ] WhatsApp business profile (Faz 2'de tamamlanır, placeholder)

### 1.3 Kullanıcılar & Roller (INMA'da yaratılır, INSE SSO ile kullanır)
- [ ] INMA'da 3 kullanıcı: `gunes`, `coordinator`, `admin` (Dent Adavista firma scope)
- [ ] INMA rol → INSE permission map:
  - INMA `admin` → INSE `tenant_admin` (full: flow edit, template edit, custom field)
  - INMA `manager` → INSE `manager` (offer, reports)
  - INMA `agent` → INSE `agent` (supervise AI, manual reply)
- [ ] SSO doğrulama: INMA login → INSE widget'ı token ile açılıyor
- [ ] INSE'de **ayrı user tablosu YOK** (unified P0, madde 1)

### 1.4 Dil & Timezone
- [ ] UI default: EN
- [ ] Timezone: Europe/Dublin (müşteri operasyonu İrlanda'da)
- [ ] Clinic timezone ayrı: Europe/Istanbul (dentist takvimi)

### 1.5 Smoke Test
- [ ] Login her 3 kullanıcı için ✅
- [ ] Dashboard boş state render ✅
- [ ] Tenant isolation: başka tenant data'sı görünmüyor ✅

## Deliverable
- Production tenant canlı
- `DentAdavista/plan/tenant-credentials.md` (şifre yok, sadece URL + user listesi)

## Çıkış Kriteri
3 kullanıcı login olabiliyor, boş tenant hazır, branding uygulandı.

## Riskler
- Multi-tenant isolation test kritik — `service-isolation-checker` agent'ı çalıştır
