# Session Prompt — Faz 1: Guvenlik Temeli

> Bu prompt'u yeni session'a yapistir. InvektoServices uzerinde calisacak.
> Calisma dizini: c:\CRMs\InvektoServices

---

## Baglam

Invekto SaaS lansman plani Faz 0 tamamlandi. Kararlar:
- Plan tierlari: **Baslangic / Profesyonel / Kurumsal**
- Her plan bir ozellik havuzu tanimlar. Firma bazinda SuperAdmin toggle yapar.
- Domain: super.invekto.com (ops), crm.invekto.com (firma)
- Billing: QNB SanalPos (Faz 2'de, simdi degil)
- Detaylar: `c:\CRMs\INVEKTO\` klasorundeki plan dosyalarinda

## Mevcut Durum

- `tenant_registry` tablosu var: plan_tier (VARCHAR 20), features_json (JSONB, bos), settings_json
- `InseFeatures[]` INMA JWT'den geliyor, frontend nav filtering calisiyor
- Backend permission enforcement YOK — sadece frontend UI gizliyor
- `hasFeature()` frontend fonksiyonu var ama tier desteği yok

## Bu Session'da Yapilacaklar

### Adim 1: Plan Havuz Tanimlarini Olustur

`plan_definitions` tablosu veya config dosyasi olustur. Icerik:

```
baslangic:
  available: WebChat(on), FlowBuilder(basic), Analytics(basic), Integrations(basic)
  quotas: messages_per_month=1000, max_users=5, max_flows=3

profesyonel:
  available: WebChat(on), FlowBuilder(basic,premium), Knowledge(basic),
             Outbound(basic), Appointments(on), Analytics(basic,premium),
             Integrations(basic), AgentAI(on)
  quotas: messages_per_month=10000, max_users=20, max_flows=-1

kurumsal:
  available: Hepsi, hepsi premium dahil
  quotas: hepsi sinirsiz (-1)
```

### Adim 2: Backend Permission Middleware

`FeatureGuardMiddleware` veya `[RequireFeature]` attribute sistemi ekle:
- Request → TenantContext → tenant_registry'den plan_tier + features_json oku (cache)
- features_json'da feature "off" veya yok → 403 `INV-FEATURE-DISABLED`
- features_json'da tier yetersiz → 403 `INV-TIER-INSUFFICIENT`
- Plan havuzunda olmayan feature atanmissa → loglama + 403
- Mevcut endpoint'lere attribute ekle (FlowBuilder, Knowledge, Outbound, vs.)

### Adim 3: Kota Sistemi

- `plan_quotas` tablosu olustur (plan_tier, metric, quota_limit)
- `tenant_usage` tablosu olustur (tenant_id, metric, period_start, period_end, usage_count)
- `[RequireQuota("messages_per_month")]` attribute
- Mesaj gonderme endpoint'inde metering: usage_count++
- Kota asildiginda: 429 `INV-QUOTA-EXCEEDED`

### Adim 4: features_json Aktivasyonu

- tenant_registry.features_json kolonunu aktif et
- SuperAdmin API: `PUT /api/v1/ops/tenants/:id/features` — firma bazinda toggle
- Validasyon: plan havuzunda olmayan ozellik atanamaz
- tenant_registry.plan_tier'i `basic` yerine `baslangic`, `pro` yerine `profesyonel`, `enterprise` yerine `kurumsal` olarak guncelle

### Adim 5: Frontend Guncelleme

- `hasFeature()` → plan havuzu + features_json kontrolu
- `hasFeatureTier(feature)` → "premium" | "basic" | "off" doner
- Feature "off" → menu'de gizle
- Feature "basic" → premium kisimlar kilitli + "Upgrade" badge
- Feature "premium" → tam erisim

### Adim 6: super.invekto.com / crm.invekto.com Ayrim

- Layout.tsx: hostname'e gore opsOnly sayfalari goster/gizle
- Veya VITE_APP_MODE env variable ile build-time ayrim
- super.invekto.com: Tenant listesi, ozellik yonetimi, servis sagligi, billing (ileride)
- crm.invekto.com: Firma dashboard, sadece o firmanin ozellikleri

### Adim 7: Testler

Her adim icin testler yaz:
- Feature guard: acik/kapali/tier yetersiz senaryolari
- Kota: limit icinde / asildi senaryolari
- Plan havuz validasyonu: havuz disi ozellik atanirsa reddet
- Tenant izolasyonu: cross-tenant erisim testi

## Kurallar

- `c:\CRMs\INVEKTO\03-BILLING-VE-PERMISSION-PLANI.md` dosyasini referans al
- Mevcut arch/ ve CLAUDE.md kurallarini takip et
- Her adimi tamamlayinca testlerini calistir
- plan_tier degerleri artik: `baslangic`, `profesyonel`, `kurumsal` (Turkce)
- Kod yazarken insan yuku minimumda tut, AI maksimum yuku tasisin
