# PKT-7: Visual AI

> **Durum:** PLANNED | **Phase:** 3C

## Ozet

Müşteri görsel gönderir → AI analiz eder → katalogdan eşleşen/benzer ürünleri bulur → stok/beden/renk bilgisiyle otomatik yanıt döner. < 15 saniye.

**Yeni Servis:** `Invekto.VisualSearch` (port 7111)
**Bagimlilik:** PKT-5 (Integrations), PKT-6A (Intent), Phase 2 (pgvector)
**Detay:** ideas/archive/visual-product-search.md, ideas/archive/size-fit-ai.md

## GR Listesi

- **GR-3C.1** VPS Core Engine: CLIP embedding + vector search + Claude Vision fallback
- **GR-3C.2** Product Catalog Management: CRUD + batch import + embedding indexing
- **GR-3C.3** Web Search Interface: upload widget + sonuç UI + embeddable widget
- **GR-3C.4** Tenant Management: API keys + usage tracking + plan limitleri
- **GR-3C.5** WhatsApp Entegrasyonu: WA Business API image handling + conversation context
- **GR-3C.6** Instagram DM Entegrasyonu: IG Graph API image handling
- **GR-3C.7** Analytics & Dashboard: arama analytics + ürün insight + conversion tracking
- **GR-3C.8** Size/Fit AI: boy/kilo → beden önerisi + iade verisi entegrasyonu

## GR Detail

### GR-3C.1: VPS Core Engine
- 3C.1.1 Servis iskeleti (port 7111, health check, tenant izolasyon)
- 3C.1.2 Image Processor: resize 512x512, normalize, blur detection, screenshot crop
- 3C.1.3 CLIP Embedding: ViT-B/32 veya ViT-L/14, 512-dim vektör, batch embedding
- 3C.1.4 Vector Search: pgvector cosine similarity, threshold 0.85, top-K=5, metadata filtering
- 3C.1.5 Result Enricher: stok/beden/renk bilgisi, ürün linki
- 3C.1.6 Claude Vision Fallback: skor < 0.5 → "anlat" → metadata arama
- DB: visual_searches, visual_search_results

### GR-3C.2: Product Catalog Management
- 3C.2.1 Catalog API (CRUD): products
- 3C.2.2 Batch Import: CSV + JSON (Trendyol/Google Shopping feed)
- 3C.2.3 Embedding Indexing Pipeline: ekle → indir → CLIP → pgvector
- 3C.2.4 Catalog Sync Webhook: idempotent event processing
- 3C.2.5 Integrations Connector: Trendyol/HB feed sync (6 saat + webhook)
- DB: vps_products (embedding VECTOR(512), HNSW index)

### GR-3C.3: Web Search Interface
- 3C.3.1 Search API: POST /search, image file/URL → sonuç listesi, < 15sn
- 3C.3.2 Embeddable Widget: script tag, drag & drop, responsive
- 3C.3.3 Widget Config: API key auth, tema, dil, sonuç limiti
- 3C.3.4 Loading State + timeout

### GR-3C.4: Tenant Management
- 3C.4.1 Tenant Onboarding API: public/secret key, plan seçimi
- 3C.4.2 Usage Tracking: arama/ay, katalog boyutu, kanal dağılım
- 3C.4.3 Tenant Dashboard: arama, top ürünler, eşleşme oranı
- DB: vps_tenants, vps_usage

### GR-3C.5: WhatsApp Entegrasyonu
- 3C.5.1 Invekto Webhook: "görsel gönderdi" event → VPS → sonuç
- 3C.5.2 WA Mesaj Formatı: interactive list/button, carousel
- 3C.5.3 Conversation Context: ikinci görsel, ürün seçimi, beden sorusu
- 3C.5.4 Flow Builder: visual_search trigger node

### GR-3C.6: Instagram DM Entegrasyonu
- 3C.6.1 IG DM Image Handler: Graph API → VPS → DM yanıt
- 3C.6.2 IG-özel UX: doğrulama, ürün link, benzer öneri

### GR-3C.7: Analytics & Dashboard
- 3C.7.1 Arama Analytics: trend, kanal, eşleşme oranı, yanıt süresi
- 3C.7.2 Ürün Insight: top aranan, bulunamayan → stok önerisi
- 3C.7.3 Conversion Tracking: arama → tıklama → satın alma funnel
- 3C.7.4 Stok Önerisi Raporu: "47 kişi kırmızı midi elbise aradı, yok"

### GR-3C.8: Size/Fit AI
- 3C.8.1 Body Estimation: boy + kilo + cinsiyet → tahmini ölçüler
- 3C.8.2 Size Matching: fit skoru (sıkı/ideal/rahat), kalıp tercihi
- 3C.8.3 Ölçü tablosu API: S/M/L/XL → cm mapping
- 3C.8.4 İade verisi entegrasyonu: beden bazlı iade oranı
- 3C.8.5 VPS sinerji: görsel arama + beden önerisi birleşik
- 3C.8.6 WhatsApp beden intent: bilgi sor → öneri ver
- DB: size_recommendations, product_size_charts

## Teknik Strateji: F3 Hybrid

```
[Ürün yükleme - arka plan]          [Arama - gerçek zamanlı]
Product Upload                       User Search Query
    │                                    │
    ▼                                    ▼
Python Sidecar (open_clip)          ONNX Runtime (.NET)
    │ image embed ~300ms                 │ text embed ~15ms
    ▼                                    ▼
pgvector (HNSW) ◄──────────────────► cosine similarity
```

- Text search CPU'da 10-30ms, image embedding arka planda
- GPU gerektirmez, scale'de Triton GPU'ya geçiş kolay

## Fiyatlandirma

| Plan | Fiyat | Arama/ay | Ürün | Kanallar |
|------|-------|----------|------|----------|
| Starter | $29/ay | 500 | 5K | Web |
| Growth | $79/ay | 2K | 20K | Web + WA |
| Pro | $199/ay | 10K | 50K | Web + WA + IG |
| Enterprise | Custom | Sınırsız | Sınırsız | Tümü |

## Notlar

- Bağımsız SaaS ürünü potansiyeli (Invekto dışı müşteriler)
- 8 GR, ~30 alt madde
