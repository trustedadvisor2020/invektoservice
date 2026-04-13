# INMA ↔ INSE Unification

**Vizyon:** İki ayrı backend + iki ayrı frontend + iki ayrı ekip; **tek ürün hissi**.

## Topoloji

| Katman | Stack | DB | Ekip |
|--------|-------|-----|------|
| **INMA** | Angular + backend (TBD) | **SQL Server** (ayrı sunucu + şema) | Ayrı ekip |
| **INSE** | React 18 + .NET 8 | **PostgreSQL 16** + pgvector | Q + Claude |
| **Köprü** | REST (auth `X-CIB-SecretKey`) + Webhook (per-tenant, INMA → INSE) + SSO | — | — |

## İlke

1. **Shared DB YOK** — farklı DBMS, mümkün değil. Entegrasyon API + webhook katmanında.
2. **INMA source-of-truth:** firma, kullanıcı, contact, mesaj, medya.
3. **INSE enrichment layer:** flow, intent, AI agent, drip, appointment, funnel, scoring.
4. **Single domain + single feel:** `app.invekto.com/*` path-based, shared design tokens.
5. **INMA'ya mantıklı olduğu sürece dokunulabilir** — ama INMA ekibiyle koordinasyon.
6. **Kullanıcı iki ayrı uygulama hissetmez.**

## Dosyalar

| Dosya | İçerik |
|-------|--------|
| [inma-feature-audit.md](inma-feature-audit.md) | INMA'da var/yok envanteri (Q 2026-04-13) |
| [gap-matrix.md](gap-matrix.md) | Kim ne yapacak: INMA / INSE / Joint |
| [domain-ux.md](domain-ux.md) | Tek domain, reverse proxy, design tokens (Madde 0) |
| [sso.md](sso.md) | INMA JWT → INSE auto-accept (Madde 1) |
| [unified-tenant.md](unified-tenant.md) | CompanyCode = tenant_id, auto-provision (Madde 2) |
| [shared-data.md](shared-data.md) | Contact ownership, custom fields, cache (Madde 4-5) |
| [feature-flags.md](feature-flags.md) | INMA license → INSE feature gate (Madde 9) |
| [contracts.md](contracts.md) | Shared DTO contract discipline (Madde 12) |
| [roadmap.md](roadmap.md) | P0 → P1 → P2 sıralı plan |

## Downstream Etki

- **Dent Adavista pilot** (`C:/CRMs/InvektoServices/DentAdavista/plan/`) — bu unification'ın ilk gerçek kullanıcısı. Unification P0 bitmeden pilot başlamaz.

## Öncelik

| Paket | Süre | Şart mı? |
|-------|------|---------|
| P0 (temel unification) | 14-19g | 🔴 Dent için şart |
| P1 (UX polish) | +5-8g | 🟡 v1.1 |
| P2 (advanced) | +5-7g | 🟢 v2 |
| INSE platform gap (G3+G6+G7) | +8-12g | 🔴 paralel |
