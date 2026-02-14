# Phase 3C — Visual Product Search (VPS)

> **Hafta:** 25-28
> **MRR Hedefi:** 800K-1M TL
> **Müşteri Hedefi:** 110+ (mevcut) + VPS-only müşteriler
> **Bağımlılık:** Phase 2 (pgvector altyapısı), Phase 3A (Integrations servisi)
> **Durum:** ⬜ Başlamadı
>
> **Konsept (2026-02-14):** E-ticaret müşterilerinin Instagram'dan screenshot alıp "Bu var mı?"
> diye sorması problemi. AI ile görsel analiz → katalogdan eşleşen/benzer ürünleri otomatik bulma.
> Detaylı fikir dokümanı: [../visual-product-search.md](../visual-product-search.md)

---

## Durum Takibi

| Alt Gereksinim | Durum | Tamamlanma Tarihi | Notlar |
|----------------|-------|-------------------|--------|
| **MVP (Web)** | | | |
| GR-3C.1 VPS Core Engine | ⬜ Başlamadı | — | CLIP + Vector Search |
| GR-3C.2 Product Catalog Management | ⬜ Başlamadı | — | Katalog API + Embedding indexing |
| GR-3C.3 Web Search Interface | ⬜ Başlamadı | — | Upload widget + sonuç UI |
| GR-3C.4 Tenant Management | ⬜ Başlamadı | — | Multi-tenant + API keys |
| **Full (Kanallar)** | | | |
| GR-3C.5 WhatsApp Entegrasyonu | ⬜ Başlamadı | — | WA Business API image handling |
| GR-3C.6 Instagram DM Entegrasyonu | ⬜ Başlamadı | — | IG Graph API image handling |
| GR-3C.7 Analytics & Dashboard | ⬜ Başlamadı | — | Arama logları + stok insight |
| **SIZE/FIT AI** | | | |
| GR-3C.8 Size/Fit AI (Beden Önerisi) | ⬜ Başlamadı | — | VPS sinerji, boy/kilo → beden önerisi |

---

## Özet

Müşteri görsel gönderir → AI analiz eder → katalogdan eşleşen/benzer ürünleri bulur → stok/beden/renk bilgisiyle otomatik yanıt döner. < 15 saniye.

**Satış dili:** "Müşteriniz gece 2'de Instagram'dan screenshot atıp 'Bu var mı?' diye sordu. 10 saniyede otomatik cevap aldı — siz uyurken satış yaptınız."

**Neden ayrı phase?**
- Tamamen yeni mikro servis + yeni AI pipeline (CLIP + Vector DB)
- Bağımsız SaaS ürünü potansiyeli — sadece Invekto müşterileri değil, herhangi bir e-ticaret sitesi kullanabilir
- Kendi gelir modeli ($29-199/ay)
- Phase 3B'deki e-ticaret GR'larından bağımsız ilerleyebilir

**Yeni Mikro Servis:**

| Servis | Port | Sorumluluk |
|--------|------|------------|
| `Invekto.VisualSearch` | 7109 | Görsel ürün arama, CLIP embedding, vector search, katalog yönetimi |

---

## Teknik Altyapı

### AI Pipeline

```
Görsel Girdi → Preprocessing → CLIP Embedding → Vector Search → Result Enrichment → Yanıt
                                      ↓
                               [Fallback: Claude Vision]
                               Ürünü "anlat" → metadata arama
```

### Paylaşılan Altyapı (Phase 2'den)

| Bileşen | Phase 2'de | VPS'te |
|---------|-----------|--------|
| pgvector | Knowledge embeddings (metin) | Ürün görseli embeddings |
| PostgreSQL | Tenant config, bilgi tabanı | Tenant config, katalog, arama logları |

### Yeni Altyapı (VPS'e özel)

| Bileşen | Açıklama |
|---------|----------|
| CLIP model | Görsel → embedding dönüşümü (self-host veya API) |
| Object Storage | Ürün görselleri (S3/MinIO) |
| Image Processor | Resize, normalize, screenshot UI elementlerini temizle |

---

## Gereksinimler — MVP (Web)

### GR-3C.1: VPS Core Engine

> **Servis:** `Invekto.VisualSearch` (port 7109) — YENİ
> **Bağımlılık:** Phase 2 GR-2.1 (pgvector altyapısı)

- [ ] **3C.1.1** VisualSearch servis iskeletini oluştur (port 7109, health check, tenant izolasyon)
- [ ] **3C.1.2** Image Processor modülü:
  - Resize (max 512x512 for CLIP)
  - Normalize (RGB, aspect ratio)
  - Quality check (min resolution, blur detection)
  - Screenshot detection (Instagram/sosyal medya UI elementlerini crop)
- [ ] **3C.1.3** CLIP Embedding modülü:
  - CLIP model yükleme (ViT-B/32 veya ViT-L/14)
  - Görsel → 512-dim embedding vektörü
  - Batch embedding (katalog indexleme için)
- [ ] **3C.1.4** Vector Search modülü:
  - pgvector ile cosine similarity search
  - Tenant bazlı namespace izolasyonu
  - Threshold-based: skor > 0.85 → birebir eşleşme, < 0.85 → benzer ürünler
  - Top-K sonuç (varsayılan K=5)
  - Metadata filtering (kategori, renk, cinsiyet — hibrit arama)
- [ ] **3C.1.5** Result Enricher modülü:
  - Eşleşen ürünlerin stok/beden/renk bilgisini tenant API'sinden çek
  - Ürün linki oluştur
  - Uygunluk durumu: ✅ var / ❌ tükendi
- [ ] **3C.1.6** Claude Vision Fallback:
  - Vector search sonuç skoru < 0.5 → Claude Vision ile görseli "anlat"
  - Çıktı: kategori, renk, stil, desen, marka (varsa)
  - Metadata bazlı arama (CLIP yerine metin bazlı)
- [ ] **3C.1.7** DB — Core:
  ```sql
  -- Arama istekleri ve sonuçları
  visual_searches (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    channel VARCHAR(20) NOT NULL,        -- 'web', 'whatsapp', 'instagram'
    image_url TEXT NOT NULL,
    image_hash VARCHAR(64),              -- duplicate detection
    embedding VECTOR(512),               -- CLIP embedding
    search_method VARCHAR(20),           -- 'clip', 'vision_fallback', 'metadata'
    top_match_id UUID,                   -- en iyi eşleşme
    top_match_score FLOAT,
    results_count INT,
    response_time_ms INT,
    customer_phone VARCHAR(20),
    created_at TIMESTAMPTZ DEFAULT NOW()
  );

  -- Arama sonuç detayları
  visual_search_results (
    id UUID PRIMARY KEY,
    search_id UUID REFERENCES visual_searches(id),
    product_id UUID NOT NULL,
    similarity_score FLOAT NOT NULL,
    rank INT NOT NULL,
    was_clicked BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW()
  );
  ```

---

### GR-3C.2: Product Catalog Management

> **Servis:** `Invekto.VisualSearch`
> **Bağımlılık:** GR-3C.1 (embedding pipeline)

- [ ] **3C.2.1** Catalog API (CRUD):
  - `POST /catalog/products` — tekil ürün ekle (görsel URL + metadata)
  - `PUT /catalog/products/:id` — güncelle
  - `DELETE /catalog/products/:id` — sil (embedding de silinir)
  - `GET /catalog/products` — liste (pagination + filter)
- [ ] **3C.2.2** Batch Import:
  - CSV upload (ürün adı, SKU, kategori, renk, beden, fiyat, görsel URL)
  - JSON import (Trendyol/Google Shopping feed formatı)
  - Background processing + progress tracking
- [ ] **3C.2.3** Embedding Indexing Pipeline:
  - Ürün eklendi → görsel indir → CLIP embedding üret → pgvector'e kaydet
  - Ürün güncellendi (görsel değişti) → embedding yeniden üret
  - Ürün silindi → embedding sil
  - Bulk re-index komutu (tüm kataloğu yeniden indexle)
- [ ] **3C.2.4** Catalog Sync (Webhook):
  - `POST /catalog/webhook` — ürün eklendi/güncellendi/silindi event'i
  - Tenant kendi e-ticaret sisteminden webhook gönderir
  - Idempotent işleme (aynı event tekrar gelse sorun olmasın)
- [ ] **3C.2.5** Integrations Connector (Phase 3A bağlantısı):
  - `Invekto.Integrations` (:7106) üzerinden Trendyol/HB ürün feed'i çekme
  - Periyodik sync (her 6 saat) + webhook bazlı anlık sync
- [ ] **3C.2.6** DB — Catalog:
  ```sql
  -- Ürün kataloğu (tenant bazlı)
  vps_products (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    external_id VARCHAR(100),            -- tenant'ın kendi ürün ID'si
    sku VARCHAR(100),
    name VARCHAR(500) NOT NULL,
    category VARCHAR(200),
    subcategory VARCHAR(200),
    color VARCHAR(100),
    size VARCHAR(50),
    gender VARCHAR(20),                  -- 'male', 'female', 'unisex'
    brand VARCHAR(200),
    price DECIMAL(10,2),
    currency VARCHAR(3) DEFAULT 'TRY',
    image_url TEXT NOT NULL,
    image_hash VARCHAR(64),
    embedding VECTOR(512),               -- CLIP embedding
    stock_status VARCHAR(20),            -- 'in_stock', 'out_of_stock', 'low_stock'
    product_url TEXT,                    -- satın alma linki
    metadata JSONB,                      -- ek bilgiler (beden listesi, renk varyantları, vb.)
    is_active BOOLEAN DEFAULT TRUE,
    indexed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
  );

  CREATE INDEX idx_vps_products_tenant ON vps_products(tenant_id);
  CREATE INDEX idx_vps_products_category ON vps_products(tenant_id, category);
  CREATE INDEX idx_vps_products_embedding ON vps_products USING ivfflat (embedding vector_cosine_ops);
  ```

---

### GR-3C.3: Web Search Interface

> **Servis:** `Invekto.VisualSearch` + Frontend (widget)
> **Bağımlılık:** GR-3C.1 (search engine)

- [ ] **3C.3.1** Search API Endpoint:
  - `POST /search` — görsel upload → sonuç listesi döndür
  - Input: image file (multipart) veya image URL
  - Output: eşleşen ürünler (skor, ürün bilgisi, stok durumu, link)
  - Response < 15 saniye
- [ ] **3C.3.2** Embeddable Web Widget:
  - `<script>` tag ile herhangi bir siteye gömülebilir
  - "Görsel ile Ara" butonu → dosya seçimi veya kamera
  - Drag & drop desteği
  - Sonuç kartları (ürün görseli, ad, fiyat, stok, link)
  - Mobil uyumlu (responsive)
- [ ] **3C.3.3** Widget Konfigürasyonu:
  - Tenant API key ile authenticate
  - Renk/tema özelleştirme (mağaza tasarımına uyum)
  - Dil seçimi (TR/EN)
  - Sonuç sayısı limiti
- [ ] **3C.3.4** "Arıyorum..." Loading State:
  - Görsel yüklendi → "🔍 Ürününüzü arıyorum..." animasyonu
  - Progress indicator
  - < 15 saniye timeout → "Sonuç bulunamadı, lütfen tekrar deneyin"

---

### GR-3C.4: Tenant Management

> **Servis:** `Invekto.VisualSearch`

- [ ] **3C.4.1** Tenant Onboarding API:
  - `POST /tenants` — yeni tenant oluştur
  - API key generate (public key for widget, secret key for catalog API)
  - Plan seçimi (Starter/Growth/Pro)
- [ ] **3C.4.2** Usage Tracking:
  - Arama sayısı / ay
  - Katalog boyutu (ürün sayısı)
  - Kanal bazlı kullanım
  - Plan limitlerine yaklaşınca uyarı
- [ ] **3C.4.3** Tenant Dashboard (Basit):
  - Toplam arama sayısı
  - En çok aranan ürünler
  - Eşleşme oranı (bulunan / toplam arama)
  - Katalog durumu (ürün sayısı, son sync)
- [ ] **3C.4.4** DB — Tenant:
  ```sql
  vps_tenants (
    id UUID PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    api_key_public VARCHAR(64) UNIQUE NOT NULL,
    api_key_secret VARCHAR(64) UNIQUE NOT NULL,
    plan VARCHAR(20) DEFAULT 'starter',  -- 'starter', 'growth', 'pro', 'enterprise'
    max_searches_per_month INT DEFAULT 500,
    max_products INT DEFAULT 5000,
    channels_enabled JSONB DEFAULT '["web"]',
    webhook_url TEXT,                    -- stok bilgisi çekmek için
    config JSONB,                        -- widget theme, language, etc.
    invekto_tenant_id UUID,              -- Invekto müşterisiyse, bağlantı
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
  );

  vps_usage (
    id UUID PRIMARY KEY,
    tenant_id UUID REFERENCES vps_tenants(id),
    month VARCHAR(7) NOT NULL,           -- '2026-03'
    search_count INT DEFAULT 0,
    channel_web INT DEFAULT 0,
    channel_whatsapp INT DEFAULT 0,
    channel_instagram INT DEFAULT 0,
    match_found_count INT DEFAULT 0,
    similar_shown_count INT DEFAULT 0,
    click_count INT DEFAULT 0,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
  );
  ```

---

## Gereksinimler — Full (Kanallar)

### GR-3C.5: WhatsApp Entegrasyonu

> **Servis:** `Invekto.VisualSearch` + Invekto Ana Uygulama
> **Bağımlılık:** GR-3C.1 (search engine), Invekto WhatsApp altyapısı

- [ ] **3C.5.1** Invekto Webhook Entegrasyonu:
  - Invekto'dan "müşteri görsel gönderdi" event'i al
  - Görseli indir, VPS'e gönder, sonucu Invekto'ya döndür
- [ ] **3C.5.2** WhatsApp Mesaj Formatı:
  - Ürün kartı (interactive message — list/button)
  - Birden fazla ürün → carousel veya numaralı liste
  - "Arıyorum..." ara mesajı (ilk 2-3 saniyede)
- [ ] **3C.5.3** Conversation Context:
  - Müşteri ikinci görsel gönderirse → yeni arama
  - Müşteri "2. ürünü istiyorum" derse → seçim yönetimi
  - Müşteri "beden S var mı?" derse → stok detay sorgusu
- [ ] **3C.5.4** Flow Builder Entegrasyonu:
  - `visual_search` trigger type → Flow Builder'da "Görsel Arama" node'u
  - Müşteri görsel gönderdiğinde tetiklenir
  - Sonucu bir sonraki node'a geçirir (ürün bilgisi)

---

### GR-3C.6: Instagram DM Entegrasyonu

> **Servis:** `Invekto.VisualSearch` + Invekto Ana Uygulama
> **Bağımlılık:** GR-3C.1 (search engine), Invekto Instagram DM altyapısı

- [ ] **3C.6.1** Instagram DM Image Handler:
  - DM'den gelen görseli al (Instagram Graph API / Messenger Platform)
  - VPS'e gönder → sonucu DM'den geri yolla
- [ ] **3C.6.2** Instagram-özel UX:
  - "Bu ürün Instagram'da gördüğünüz gibi değil mi?" doğrulama
  - Ürün linki + "DM'den sipariş ver" butonu
  - Stok yoksa → benzer ürünler öner

---

### GR-3C.7: Analytics & Dashboard

> **Servis:** `Invekto.VisualSearch` + Dashboard
> **Bağımlılık:** GR-3C.1-4 (tüm MVP)

- [ ] **3C.7.1** Arama Analytics:
  - Günlük/haftalık/aylık arama sayısı trendi
  - Kanal bazlı dağılım (web/WhatsApp/IG)
  - Eşleşme oranı trendi (iyileşiyor mu?)
  - Ortalama yanıt süresi
- [ ] **3C.7.2** Ürün Insight'ları:
  - **En çok aranan ürünler** (top 10)
  - **Aranan ama bulunamayan** → stok önerisi ("bu tarz ürün eklemelisiniz")
  - **En çok tıklanan** sonuçlar (satın alma niyeti yüksek)
  - Kategori bazlı arama dağılımı
- [ ] **3C.7.3** Conversion Tracking:
  - Arama → tıklama → satın alma funnel'ı
  - VPS üzerinden gelen satış geliri (tenant API callback ile)
  - ROI hesaplama: VPS maliyeti vs VPS üzerinden gelen gelir
- [ ] **3C.7.4** Stok Önerisi Raporu:
  - "Son 30 günde 47 kişi kırmızı midi elbise aradı, stoğunuzda yok"
  - Kategori bazlı talep trendi
  - Tenant'a email/dashboard notification

---

## Fiyatlandırma Modeli

| Plan | Fiyat | Arama/ay | Ürün Limiti | Kanallar |
|------|-------|----------|-------------|----------|
| **Starter** | $29/ay | 500 | 5,000 | Web |
| **Growth** | $79/ay | 2,000 | 20,000 | Web + WhatsApp |
| **Pro** | $199/ay | 10,000 | 50,000 | Web + WhatsApp + Instagram |
| **Enterprise** | Custom | Sınırsız | Sınırsız | Tümü + dedicated infra |

**Invekto Mevcut Müşterileri İçin:**
- Invekto planına ek olarak VPS modülü → $29/ay'dan başlayan add-on
- Invekto Pro/Enterprise müşterilerine dahil (upsell fırsatı)

---

## Gereksinimler — Size/Fit AI

### GR-3C.8: Size/Fit AI (Akıllı Beden Önerisi)

> **Servis:** `Invekto.VisualSearch` genişletme (veya bağımsız modül)
> **Sektör:** E-ticaret (giyim, ayakkabı)
> **Kaynak:** [../size-fit-ai.md](../size-fit-ai.md)
> **Sinerji:** VPS ile birleşik deneyim ("ürünü bul + bedeni öner")

- [ ] **3C.8.1** Body Estimation Engine:
  - Boy + kilo + cinsiyet → tahmini vücut ölçüleri (göğüs/bel/kalça/omuz)
  - NLP ile WhatsApp mesajından beden bilgisi çıkarma
- [ ] **3C.8.2** Size Matching Algorithm:
  - Müşteri ölçüleri vs ürün ölçü tablosu → fit skoru
  - Her beden için uygunluk: sıkı / ideal / rahat
  - Kalıp tercihi sorusu (dar/normal/bol)
- [ ] **3C.8.3** Tenant ürün kataloğu ölçü tablosu API:
  - Ürün başına S/M/L/XL → cm ölçü mapping
  - Kalıp tipi (dar/normal/bol) ve kumaş esnekliği
- [ ] **3C.8.4** İade verisi entegrasyonu:
  - Beden bazlı iade oranı ("M alanların %38'i iade etti")
  - Memnuniyet yüzdesi = sosyal kanıt
- [ ] **3C.8.5** VPS sinerji:
  - Görsel arama + beden önerisi birleşik yanıt
  - "Bu ürünü bulduk + size L öneriyoruz + stok var"
- [ ] **3C.8.6** WhatsApp konuşma entegrasyonu:
  - "Beden ne alayım?" intent'i → beden bilgisi sor → öneri ver
  - Kalıp tercihi sorusu (dar/rahat)
- [ ] **3C.8.7** DB:
  ```sql
  size_recommendations (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    product_id UUID,
    customer_phone VARCHAR(20),
    customer_height INT,
    customer_weight INT,
    customer_gender VARCHAR(10),
    recommended_size VARCHAR(10),
    confidence FLOAT,
    fit_details JSONB,
    was_accepted BOOLEAN,
    was_returned BOOLEAN,
    return_reason VARCHAR(100),
    created_at TIMESTAMPTZ DEFAULT NOW()
  );

  product_size_charts (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    product_id UUID,
    size_label VARCHAR(10) NOT NULL,
    chest_cm INT,
    waist_cm INT,
    hip_cm INT,
    shoulder_cm INT,
    length_cm INT,
    fit_type VARCHAR(20),
    fabric_stretch BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW()
  );
  ```

---

## Çıkış Kriterleri (Phase 4'e Geçiş Şartı)

- [ ] VPS servis (:7109) production'da çalışıyor
- [ ] Web widget en az 3 e-ticaret sitesine gömülmüş
- [ ] Katalog indexleme < 50K ürün için < 1 saat
- [ ] Arama yanıt süresi < 15 saniye (P95)
- [ ] Eşleşme oranı > %60 (en az 1 benzer ürün buluyor)
- [ ] WhatsApp entegrasyonu çalışıyor (en az 1 Invekto müşterisi aktif)
- [ ] En az 1 VPS-only müşteri (Invekto dışı)
- [ ] Analytics dashboard aktif
- [ ] "Aranan ama bulunamayan" raporu çalışıyor
- [ ] Size/Fit AI: beden önerisi en az 3 tenant'ta aktif
- [ ] Size/Fit AI: iade oranı düşüşü ölçülebiliyor

---

## Risk & Mitigasyon

| Risk | Seviye | Mitigasyon |
|------|--------|-----------|
| CLIP doğruluğu fashion'da düşük | 🟠 Yüksek | Fashion-CLIP fine-tune; Claude Vision fallback |
| Screenshot kalitesi kötü | 🟡 Orta | Preprocessing: IG UI crop, enhance, blur check |
| GPU maliyeti başlangıçta yüksek | 🟡 Orta | Önce OpenAI CLIP API (veya HuggingFace Inference), ölçeklenince self-host |
| Tenant katalog uyumsuzluğu | 🟡 Orta | CSV template + validation + görsel kalite check |
| WhatsApp API rate limit | 🟡 Orta | Queue sistemi + priority (ücretli plan önce) |

---

## Notlar

- Phase 3C bağımsız bir SaaS ürünü olarak da çalışabilir
- Invekto müşterisi olmayan e-ticaret siteleri de VPS kullanabilir (API key ile)
- pgvector altyapısı Phase 2 Knowledge servisinden miras alınır
- Integrations servisi (Phase 3A :7106) ile Trendyol/HB ürün feed sync'i yapılabilir
- Flow Builder'da "Görsel Arama" node'u → Automation servisiyle entegre
- Detaylı fikir dokümanı: [../visual-product-search.md](../visual-product-search.md)
- **v4.5:** Size/Fit AI (GR-3C.8) eklendi — VPS ile birleşik "ürün bul + beden öner" deneyimi
- Detaylı Size/Fit fikir dokümanı: [../size-fit-ai.md](../size-fit-ai.md)
