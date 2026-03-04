# Invekto Ekosistem Raporu

> Son guncelleme: 2 Mart 2026
> Durum: Aktif analiz, lansman oncesi

---

## 1. Kapsam

| # | Proje | Domain | Rol | Tech | Durum |
|---|-------|--------|-----|------|-------|
| 1 | **InvektoServices** | Backend + Dashboard | Beyin | .NET 8, PostgreSQL, Claude AI | %95, production |
| 2 | **InvektoWebsite** | invekto.com | Vitrin | Node.js, Express, EJS, Tailwind | Production, canli |
| 3 | **InvektoChat** | Mobil operator app | Saha araci | React Native, Expo, SignalR | MVP, erken |
| 4 | **InvektoHelp** | docs.invekto.com | Bilgi merkezi | Next.js 16, MDX, Tailwind | %40 icerik |

**Kapsam disinda:** TONIVA (VoIP CRM), Toniva Softphone (ileride Invekto Softphone olacak), GFolder, WapCRM (legacy).

---

## 2. Domain Yapisi

| Domain | Hedef | Durum |
|--------|-------|-------|
| `invekto.com` | Pazarlama sitesi + Solution Finder AI | CANLI |
| `crm.invekto.com` | Firma dashboard (tenant bazli CRM) | PLANLI — ayrilacak |
| `super.invekto.com` | Invekto One SuperAdmin paneli | PLANLI — ayrilacak |
| `docs.invekto.com` | Dokumantasyon / Yardim merkezi | CANLI |
| `chat.invekto.com:7443` | WebChat servisi (SignalR) | CANLI |

---

## 3. Mimari

```
┌──────────────────────────────────────────────────────────────────┐
│                         INMA (Kaynak Sistem)                     │
│  Firma olusturma, kullanici yonetimi, kanal tanimlama            │
│  CompanyCode, InseFeatures[], ChatRole, FullName, Lang           │
│  (SQL Server, disaridan yonetilir)                               │
└────────┬───────────────────────────────────────────┬─────────────┘
         │ JWT token exchange                        │
         ▼                                           ▼
┌─────────────────────┐                  ┌──────────────────────────┐
│ super.invekto.com   │                  │ crm.invekto.com          │
│ (Invekto One Panel) │                  │ (Firma Dashboard)        │
│                     │                  │                          │
│ • Tenant listesi    │  impersonate     │ • Flow Builder           │
│ • Servis sagligi    │ ──────────────▶  │ • Knowledge              │
│ • Global template   │                  │ • Kampanyalar            │
│ • Mesaj log         │                  │ • Randevular             │
│ • Intent yonetimi   │                  │ • Analizler / RI         │
│ • Billing yonetimi  │                  │ • Entegrasyonlar         │
│ • Plan override     │                  │ • Pazarlama              │
│                     │                  │ • Ayarlar + Abonelik     │
└─────────────────────┘                  └───────────┬──────────────┘
                                                     │
┌────────────────────────────────────────────────────────────────────┐
│                   INVEKTO SERVICES (12 Mikroservis)                │
│                                                                    │
│  Backend:5000 │ ChatAnalysis:7101 │ Appointments:7102              │
│  Knowledge:7104 │ AgentAI:7105 │ Integrations:7106                 │
│  Outbound:7107 │ Automation:7108 │ WhatsAppAnalytics:7109          │
│  Marketing:7112 │ WebChat:7113                                     │
│                                                                    │
│  DB: PostgreSQL + pgvector                                         │
│  Auth: INMA JWT → INSE JWT (8 saat gecerlilik)                     │
│  Tenant: tenant_registry (plan_tier, features_json, settings_json) │
└────────┬──────────────────────────┬──────────────────┬─────────────┘
         │                          │                  │
         ▼                          ▼                  ▼
┌────────────────┐    ┌──────────────────┐   ┌───────────────────┐
│ invekto.com    │    │ InvektoChat      │   │ docs.invekto.com  │
│ (Website)      │    │ (Mobil App)      │   │ (InvektoHelp)     │
│ Pazarlama +    │    │ Operator mesaj   │   │ MDX tabanli       │
│ Solution Finder│    │ SignalR/WebSocket│   │ dokumantasyon     │
└────────────────┘    └──────────────────┘   └───────────────────┘
```

---

## 4. Mevcut Auth Akisi

### Token Turleri

| Tur | Veren | Claims | Gecerlilik |
|-----|-------|--------|-----------|
| INMA JWT | INMA (Main App) | CompanyCode, ChatRole, InseFeatures, FullName, Lang | 8 saat |
| INSE JWT | InvektoServices | tenant_id, user_id, role, source | 8 saat |

### Auth Flow'lari

1. **INMA Token Exchange** — INMA JWT ile crm.invekto.com'a giris (SSO)
2. **INMA Login Proxy** — Kullanici adi/sifre ile INMA uzerinden giris
3. **Mock Login** — Dev/test icin (MockEnabled=true gerekli)
4. **SuperAdmin Impersonate** — Ops panel uzerinden tenant'a gecis

### Claim Mapping

```
INMA CompanyCode  →  INSE tenant_id  →  TenantContext.TenantId
INMA ChatRole     →  INSE role       →  TenantContext.Role (agent/admin)
INMA InseFeatures →  INSE features   →  InseSession.inseFeatures[]
```

---

## 5. Mevcut Tenant Modeli

### PostgreSQL: tenant_registry

| Kolon | Tip | Aciklama | Durum |
|-------|-----|----------|-------|
| tenant_id | INTEGER PK | INMA CompanyCode | AKTIF |
| tenant_name | VARCHAR(200) | Firma adi | AKTIF |
| is_active | BOOLEAN | Aktif/pasif | AKTIF |
| sector | VARCHAR(50) | eticaret, saglik, emlak, egitim... | AKTIF |
| plan_tier | VARCHAR(20) | basic, pro, enterprise | AKTIF ama enforce edilmiyor |
| features_json | JSONB | Ozellik override'lari | RESERVED — kullanilmiyor |
| settings_json | JSONB | Calisma saatleri, WapCRM config, threshold | AKTIF |
| callback_url | VARCHAR(500) | Override callback URL | AKTIF |

### Mevcut Feature Flags

Frontend'de nav filtering calisiyor:
- FlowBuilder, Knowledge, Outbound, Appointments, Analytics, Integrations, Marketing

**Kritik eksik:** Backend permission enforcement yok. Sadece UI gizliyor, API'yi kilitlemiyor.

---

## 6. Guclu Yanlar

- 12 mikroservis, cogu production'da HEALTHY
- Revenue Intelligence (RI) 8 paket tamamlanmis
- AI entegrasyonu (Claude) CRM + Website'da aktif
- Flow Builder ile otomasyon altyapisi hazir
- INMA'dan firma/kullanici/kanal hazir geliyor — sifirdan olusturmaya gerek yok
- WebChat widget canli, InvektoChat MVP'de
- Deploy MCP ile otomatik (SSH/SFTP, PM2, NSSM)
- Test altyapisi var (xUnit, 4 E2E, Playwright UI tester, Simulator)
- Dokumantasyon altyapisi profesyonel (Next.js + MDX + Prism komponentleri)

---

## 7. Kritik Eksikler

| # | Eksik | Durum | Etki |
|---|-------|-------|------|
| 1 | Billing / Odeme sistemi | Hic yok | BLOCKER — para alamazsin |
| 2 | Backend permission enforcement | Sadece frontend | BLOCKER — guvenlik acigi |
| 3 | Premium/Basic feature tiering | features_json bos | BLOCKER — plan farklilasmasi yok |
| 4 | Self-service firma paneli (abonelik) | Yok | BLOCKER — firma kendi planini goremiyor |
| 5 | super.invekto.com ayrimi | Tek dashboard | ONEMLI — ops ve firma karisik |
| 6 | Onboarding wizard | Auto-seed var, UX yok | ONEMLI — ilk giris deneyimi yok |
| 7 | Dokumantasyon icerigi | %40 dolu | ONEMLI — musteri bilgi bulamiyor |
| 8 | Frontend testleri | Chat, Help, Website'da 0 | DUSUK — su an risk az |
