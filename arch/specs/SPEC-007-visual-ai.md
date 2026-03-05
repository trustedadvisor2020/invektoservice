# SPEC: Visual Product Search (VPS)

> **Spec ID:** SPEC-007 | **Paket:** PKT-7 | **Risk:** HIGH
> **Yazar:** Q | **Son Guncelleme:** 2026-03-05 | **Durum:** DRAFT

## 1. Intent (Ne & Neden)

Musteri gorsel gonderir (WhatsApp, IG DM veya web widget) -> AI goruntuden urun ozellikleri cikarir -> pgvector ile katalogdan eslesen/benzer urunleri bulur -> stok/beden/renk bilgisiyle otomatik yanit doner. < 15 saniye.

**Neden:** E-ticaret musterilerinin %40'i "buna benzer var mi?" sorusuyla geliyor. Gorsel arama bu ihtiyaci karsilar ve conversion'i arttirir. Ayni zamanda bagimsiz SaaS urunu potansiyeli var (Invekto disi musteriler).

## 2. Acceptance Criteria

| # | Kriter | Dogrulama Yontemi |
|---|--------|-------------------|
| AC-1 | Gorsel yukleme -> CLIP embedding -> pgvector cosine similarity ile top-5 sonuc donmeli | Manual test: ornek gorsel yukle, sonuc kontrol |
| AC-2 | End-to-end yanit suresi < 15 saniye (text search < 50ms) | DB query + API response time olcumu |
| AC-3 | Cosine similarity < 0.5 ise Claude Vision fallback tetiklenmeli | Dusuk eslesme gorseli ile test |
| AC-4 | Tenant izolasyonu: her tenant sadece kendi katalogunu gorebilmeli | Cross-tenant query testi |
| AC-5 | Urun katalog CRUD + batch import (CSV/JSON) calismali | API endpoint testi |
| AC-6 | WhatsApp gorsel mesaj -> VPS -> otomatik yanit akisi calismali | WA Business API ile end-to-end test |
| AC-7 | Web widget embed edilebilir ve responsive olmali | Tarayici testi |
| AC-8 | Usage tracking: arama/ay limiti tenant plan'a gore uygulanmali | Plan limiti asan tenant testi |

## 3. Architectural Decisions

| Karar | Neden | Codex Notu |
|-------|-------|------------|
| F3 Hybrid: Python sidecar (open_clip) + ONNX Runtime (.NET) | Image embedding arka planda Python, text search realtime .NET. GPU gerektirmez | EXPECTED: Python sidecar dis bagimliligi |
| Yeni mikroservis: Invekto.VisualSearch (port 7111) | Agir is yuku, bagimsiz olceklenebilirlik | EXPECTED: Yeni servis izolasyonu |
| pgvector HNSW index | Cosine similarity icin optimize, mevcut PostgreSQL uzerinde | EXPECTED: Extension kullanimi |
| Claude Vision fallback (skor < 0.5) | Dusuk eslesme durumunda gorsel aciklama -> metadata arama | EXPECTED: External API cagirisi |
| Integrations servisiyle Trendyol/HB feed sync | Mevcut e-commerce entegrasyonlarini yeniden kullanma | EXPECTED: Cross-service API cagirisi |

## 4. Contract References

| Contract | Dosya |
|----------|-------|
| API Request/Response | `arch/contracts/vps-api.json` (olusturulacak) |
| DB Schema | `arch/db/visual-search.sql` (olusturulacak) |
| Error Codes | `arch/errors.md` INV-VS-xxx |

## 5. Scope Boundaries

### In Scope
- GR-3C.1: VPS Core Engine (CLIP + vector search + Claude Vision fallback)
- GR-3C.2: Product Catalog Management (CRUD + batch import + embedding)
- GR-3C.3: Web Search Interface (API + widget)
- GR-3C.4: Tenant Management (API keys + usage + plan limitleri)
- GR-3C.5: WhatsApp Entegrasyonu (gorsel mesaj -> VPS -> yanit)
- GR-3C.6: Instagram DM Entegrasyonu
- GR-3C.7: Analytics & Dashboard
- GR-3C.8: Size/Fit AI

### Out of Scope (Explicit)
- Video analizi (sadece statik gorsel)
- Realtime goruntu akisi (kamera)
- Farkli embedding modelleri arasi A/B test
- Multi-region deployment
- Mobile SDK (sadece web widget + mesajlasma kanallari)

### Degismeyen Alanlar (Pre-existing)
- Mevcut Backend proxy yapisi (yeni endpoint eklenir, mevcut degismez)
- Mevcut Integrations servisi API'leri (sadece consumer olarak kullanilir)
- Mevcut tenant/auth yapisi (Backend uzerinden)
- Mevcut WhatsApp webhook yapisi (Automation servisi tetikler)

## 6. Service Boundaries

| Servis | Rol | Degisiklik Tipi |
|--------|-----|-----------------|
| VisualSearch (7111) | Core logic | **Yeni servis** |
| Backend (5000) | Proxy + auth | Yeni endpoint'ler |
| Integrations (7106) | Feed sync | Consumer (mevcut API kullanilir) |
| Automation (7108) | Flow trigger | visual_search node eklenir |
| Shared | DTO + constants | Yeni DTO'lar |

## 7. Risk & Mitigation

| Risk | Olasilik | Mitigation |
|------|----------|------------|
| CLIP model boyutu (>1GB) sunucu bellek | ORTA | ViT-B/32 ile basla (400MB), gerekirse quantize |
| Python sidecar yonetim karmasikligi | ORTA | NSSM ile Windows service, health check endpoint |
| pgvector HNSW index rebuild suresi (buyuk katalog) | DUSUK | Incremental indexing, off-peak rebuild |
| Claude Vision API maliyeti (fallback) | ORTA | Threshold tuning, cache, rate limit |
| Trendyol/HB feed format degisikligi | DUSUK | Adapter pattern, versiyonlu parser |
