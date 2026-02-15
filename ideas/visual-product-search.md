# Visual Product Search (VPS) — Görsel ile Ürün Arama Servisi

> **Tarih:** 2026-02-14
> **Kaynak:** Q interview (brainstorm)
> **Durum:** FİKİR AŞAMASI — Phase 3C olarak roadmap'e eklendi
> **Roadmap:** [phases/phase-3c.md](phases/phase-3c.md) (Hafta 25-28)
> **Servis:** `Invekto.VisualSearch` (port 7111)
> **Bağlı Servisler:** Bağımsız mikro servis, Invekto Main App veya herhangi bir e-ticaret ile entegre olabilir

---

## Problem

E-ticaret müşterileri şu davranışı sıkça yapıyor:

1. Instagram'da (veya başka bir sosyal medyada) bir ürün görüyorlar (örn. bir elbise)
2. Ekran görüntüsü alıyorlar
3. WhatsApp'tan mağazaya gönderiyorlar: *"Bu var mı? Bedeni/rengi var mı?"*
4. Mağaza çalışanı görsele bakıp **elle katalogdan arıyor** — yavaş, hata yapılabiliyor
5. Müşteri cevap beklerken başka mağazaya yazıyor → **satış kaçıyor**

**Bu döngü her gün binlerce kez tekrarlanıyor.** Özellikle moda, giyim, aksesuar, ev tekstili gibi görsel-ağırlıklı sektörlerde çok yaygın.

---

## Çözüm

**Visual Product Search (VPS):** Müşterinin gönderdiği görseli AI ile analiz edip, mağazanın ürün kataloğundan eşleşen (veya benzer) ürünleri otomatik bulan ve stok/beden/renk bilgisiyle birlikte müşteriye anında dönen mikro servis.

### Müşteri Deneyimi (Hedef)

```
Müşteri: [📸 Instagram screenshot gönderir]

Bot (< 15 sn):
┌─────────────────────────────────────┐
│ 🔍 Bu ürünü bulduk!                │
│                                     │
│ Kırmızı Midi Elbise - ₺899         │
│ Bedenler: S ✅  M ✅  L ❌  XL ✅   │
│ Renkler: Kırmızı ✅ Siyah ✅        │
│                                     │
│ 👉 Satın al: magaza.com/urun/12345 │
│                                     │
│ 📌 Benzer ürünler:                  │
│  1. Bordo Midi Elbise - ₺799       │
│  2. Kırmızı Maxi Elbise - ₺949    │
│  3. Pembe Midi Elbise - ₺849      │
└─────────────────────────────────────┘
```

Eğer birebir eşleşme bulunamazsa:
```
Bot:
┌─────────────────────────────────────┐
│ 🔍 Bu ürünün aynısı katalogumuzda  │
│    yok, ama benzerlerini bulduk:    │
│                                     │
│ 1. [Ürün kartı]                     │
│ 2. [Ürün kartı]                     │
│ 3. [Ürün kartı]                     │
│                                     │
│ 💬 Yardımcı olabilir miyim?        │
└─────────────────────────────────────┘
```

---

## Neden Bağımsız Mikro Servis?

| Soru | Cevap |
|------|-------|
| Neden InvektoServices'te? | Multi-tenant SaaS — herhangi bir e-ticaret kullanabilir |
| Invekto'ya bağımlı mı? | **HAYIR** — tamamen bağımsız, kendi API'si var |
| Invekto ile nasıl çalışır? | Invekto'nun kanal adaptörleri (WhatsApp, IG DM) üzerinden tetiklenebilir |
| Başka platformlar? | Shopify, WooCommerce, Trendyol mağazaları, custom e-ticaret — hepsi kullanabilir |
| Gelir modeli? | Ayrı SaaS ürünü olarak fiyatlandırılabilir veya Invekto'nun premium tier'ı |

---

## Mimari

```
┌──────────────────────────────────────────────────────────────┐
│                     CHANNEL LAYER                             │
│                                                              │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐    │
│  │ WhatsApp │  │ Instagram │  │   Web    │  │  Custom  │    │
│  │ Business │  │  Graph   │  │  Upload  │  │   API    │    │
│  │   API    │  │   API    │  │   SDK    │  │          │    │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘    │
│       │              │              │              │          │
│       └──────────────┴──────┬───────┴──────────────┘          │
│                             ▼                                 │
│                    ┌─────────────────┐                        │
│                    │ Channel Router  │  ← Tenant ID + kanal  │
│                    │ & Normalizer    │    tanıma              │
│                    └────────┬────────┘                        │
└─────────────────────────────┼────────────────────────────────┘
                              ▼
┌──────────────────────────────────────────────────────────────┐
│                     CORE VPS ENGINE                           │
│                                                              │
│  ┌──────────────────┐    ┌─────────────────────────────┐     │
│  │ Image Processor   │    │ Product Understanding        │     │
│  │ ─────────────────│    │ ─────────────────────────── │     │
│  │ • Resize/crop     │    │ • Vision AI ile ürün tanıma  │     │
│  │ • Normalize       │───>│ • Kategori: elbise, ayakkabı │     │
│  │ • Quality check   │    │ • Renk, desen, stil          │     │
│  │ • Screenshot algı │    │ • Metin (varsa marka/fiyat)  │     │
│  └──────────────────┘    └──────────┬──────────────────┘     │
│                                      ▼                        │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ Vector Search Engine                                  │    │
│  │ ──────────────────────────────────────────────────── │    │
│  │ • Görsel embedding → Tenant namespace'inde ara        │    │
│  │ • Birebir eşleşme skoru > threshold → TAM EŞLEŞME    │    │
│  │ • Skor < threshold → BENZER ÜRÜNLER (top 5)           │    │
│  │ • Hibrit: embedding benzerliği + metadata filtre      │    │
│  │   (kategori=elbise AND renk=kırmızı)                  │    │
│  └──────────────────────┬───────────────────────────────┘    │
│                          ▼                                    │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ Result Enricher                                       │    │
│  │ ──────────────────────────────────────────────────── │    │
│  │ • Tenant API'sinden stok/beden/renk/fiyat çek        │    │
│  │ • Ürün linki oluştur                                  │    │
│  │ • Uygunluk durumu: ✅ var / ❌ tükendi                │    │
│  └──────────────────────┬───────────────────────────────┘    │
│                          ▼                                    │
│  ┌──────────────────────────────────────────────────────┐    │
│  │ Response Builder                                      │    │
│  │ ──────────────────────────────────────────────────── │    │
│  │ • Kanal formatına uygun mesaj (WhatsApp card, web UI) │    │
│  │ • Dil desteği (TR/EN/multi)                           │    │
│  │ • Ürün kartı + satın al linki + benzer öneriler       │    │
│  └──────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│                     DATA LAYER                                │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐   │
│  │ Vector DB     │  │ PostgreSQL   │  │ Object Storage   │   │
│  │ (Qdrant)     │  │              │  │ (S3/MinIO)       │   │
│  │ ─────────── │  │ ──────────── │  │ ──────────────── │   │
│  │ Product      │  │ Tenant config│  │ Product images   │   │
│  │ embeddings   │  │ Search logs  │  │ (original +      │   │
│  │ per tenant   │  │ Analytics    │  │  processed)      │   │
│  │ namespace    │  │ API keys     │  │                  │   │
│  └──────────────┘  └──────────────┘  └──────────────────┘   │
└──────────────────────────────────────────────────────────────┘
```

---

## Teknik Seçenekler & Trade-off'lar

### 1. Görsel AI Motoru

| Seçenek | Artı | Eksi | Maliyet |
|---------|------|------|---------|
| **OpenAI CLIP (self-host)** | Ücretsiz, hızlı embedding, kanıtlanmış | GPU gerekli, fashion-specific değil | GPU sunucu maliyeti |
| **Fashion-CLIP** | CLIP'in fashion fine-tune'u, daha doğru | Daha az genel, bakım zor | GPU sunucu maliyeti |
| **Claude Vision API** | Ürünü çok iyi "anlar" (renk, stil, kategori) | Embedding üretmez, yavaş, pahalı | ~$0.01-0.03/görsel |
| **Google Vision AI** | Product Search özelliği var | Pahalı, vendor lock-in | ~$1.50/1000 görsel |
| **Hibrit (Önerilen)** | CLIP embedding + Claude/GPT anlama | Daha karmaşık ama en iyi sonuç | Orta |

**Önerilen yaklaşım: Hibrit**
1. **CLIP** → Görsel embedding üret, vector search yap (hız)
2. **Claude Vision** → Eşleşme bulunamazsa, görseli "anlat" (kategori, renk, stil) → metadata bazlı arama (doğruluk)

### 2. Vector Database

| Seçenek | Artı | Eksi | Maliyet |
|---------|------|------|---------|
| **Qdrant (self-host)** | Ücretsiz, hızlı, namespace desteği, filtering | Yönetim gerekli | Sunucu maliyeti |
| **Qdrant Cloud** | Managed, kolay | Ücretli | ~$25/ay (başlangıç) |
| **Pinecone** | Managed, popüler | Pahalı, ABD odaklı | ~$70/ay (starter) |
| **pgvector** | PostgreSQL extension, ekstra DB yok | Büyük ölçekte yavaş | Ücretsiz |

**Önerilen: Qdrant (self-host başla, gerekirse Cloud'a geç)**
- 50K ürün × N tenant = milyonlarca vektör → Qdrant bunu rahat taşır
- Namespace (collection) bazlı tenant izolasyonu
- Metadata filtering (kategori, renk, cinsiyet) desteği

### 3. Kanal Entegrasyonu

| Kanal | API | Maliyet | Zorluk |
|-------|-----|---------|--------|
| **WhatsApp Business** | Meta Cloud API | Gelen ücretsiz, giden ~$0.05-0.15/mesaj | Orta — webhook setup, media download |
| **Instagram DM** | Instagram Graph API (Messenger Platform) | Ücretsiz | Orta — app review gerekli |
| **Web** | Kendi SDK/Widget | Ücretsiz | Düşük — basit upload endpoint |
| **Custom API** | REST API | - | Düşük — tenant kendi entegrasyonunu yapar |

### 4. Ürün Kataloğu Senkronizasyonu

Tenant'ın ürün kataloğunu VPS'e nasıl aktaracağız?

| Yöntem | Açıklama | Uygun Olduğu Durum |
|--------|----------|---------------------|
| **API Push** | Tenant kendi ürünlerini API ile gönderir | Custom e-ticaret |
| **Feed URL** | XML/JSON ürün feed'i (Trendyol/Google Shopping formatı) | Marketplace mağazaları |
| **Shopify/WooCommerce Connector** | Platform API'sinden otomatik çek | Shopify/WooCommerce kullanıcıları |
| **Manuel Upload** | CSV/Excel ile toplu yükleme | Küçük mağazalar |
| **Webhook** | Ürün eklendiğinde/güncellendiğinde otomatik sync | Real-time güncel kalmak |

---

## Maliyet Analizi (Tahmini)

### Altyapı (Aylık)

| Bileşen | Küçük (10 tenant) | Orta (100 tenant) | Büyük (500 tenant) |
|---------|--------------------|--------------------|---------------------|
| VPS Sunucu (GPU - CLIP) | ~$50-100 | ~$100-300 | ~$300-800 |
| Qdrant (self-host) | ~$20 | ~$50 | ~$150 |
| PostgreSQL | ~$15 | ~$30 | ~$80 |
| Object Storage | ~$5 | ~$20 | ~$100 |
| Claude Vision API* | ~$10 | ~$100 | ~$500 |
| **Toplam** | **~$100-150/ay** | **~$300-500/ay** | **~$1,100-1,600/ay** |

*Claude Vision sadece fallback'te kullanılırsa maliyet düşük kalır

### Birim Maliyet (Arama Başına)

| Bileşen | Maliyet/arama |
|---------|---------------|
| CLIP embedding | ~$0.001 (self-host GPU amortize) |
| Vector search | ~$0.0001 |
| Claude Vision (fallback) | ~$0.01-0.03 |
| WhatsApp giden mesaj | ~$0.05-0.15 |
| **Toplam (CLIP only)** | **~$0.05-0.15** |
| **Toplam (CLIP + Vision)** | **~$0.06-0.18** |

### Fiyatlandırma Modeli (Öneriler)

| Plan | Fiyat | İçerik |
|------|-------|--------|
| Starter | $29/ay | 500 arama/ay, 1 kanal, 5K ürün |
| Growth | $79/ay | 2,000 arama/ay, 3 kanal, 20K ürün |
| Pro | $199/ay | 10,000 arama/ay, tüm kanallar, 50K ürün |
| Enterprise | Custom | Sınırsız, dedicated infra, SLA |

---

## MVP Scope (Faz 1)

**Hedef:** Tek bir kanalda (Web upload) çalışan, temel görsel arama.

| Bileşen | MVP'de Var | MVP Sonrası |
|---------|------------|-------------|
| Web upload ile görsel arama | ✅ | |
| CLIP embedding + Qdrant search | ✅ | |
| Birebir + benzer sonuçlar | ✅ | |
| Stok/beden/renk bilgisi | ✅ | |
| Multi-tenant (tenant API key) | ✅ | |
| Ürün kataloğu API (push) | ✅ | |
| CSV upload ile katalog | ✅ | |
| Tenant yönetim paneli (basit) | ✅ | |
| WhatsApp entegrasyonu | | ✅ Faz 2 |
| Instagram DM entegrasyonu | | ✅ Faz 2 |
| Claude Vision fallback | | ✅ Faz 2 |
| Shopify/WooCommerce connector | | ✅ Faz 3 |
| Feed URL (XML/JSON) sync | | ✅ Faz 3 |
| Analytics dashboard | | ✅ Faz 3 |
| A/B test (sonuç sıralaması) | | ✅ Faz 4 |
| Recommendation engine | | ✅ Faz 4 |

### MVP Minimum Dosyalar

```
services/visual-product-search/
├── src/
│   ├── server.ts                 # Express/Fastify server
│   ├── routes/
│   │   ├── search.ts             # POST /search — görsel gönder, sonuç al
│   │   ├── catalog.ts            # CRUD /catalog — ürün kataloğu yönetimi
│   │   └── tenant.ts             # Tenant yönetimi
│   ├── services/
│   │   ├── image-processor.ts    # Resize, normalize, quality check
│   │   ├── embedding.ts          # CLIP model ile embedding üret
│   │   ├── vector-search.ts      # Qdrant'ta ara
│   │   ├── catalog-sync.ts       # Ürün ekleme/güncelleme/silme → embedding güncelle
│   │   └── result-enricher.ts    # Stok/fiyat bilgisi ekle
│   ├── adapters/
│   │   └── web.ts                # Web upload adapter (MVP)
│   ├── db/
│   │   ├── schema.sql            # Tenant, arama logları, katalog metadata
│   │   └── migrations/
│   └── config/
│       └── index.ts
├── package.json
├── Dockerfile
└── README.md
```

---

## Riskler & Zorluklar

| Risk | Seviye | Mitigasyon |
|------|--------|-----------|
| **CLIP doğruluğu düşük olabilir** | 🟠 Yüksek | Fashion-CLIP veya fine-tune; Claude Vision fallback |
| **Screenshot kalitesi kötü** | 🟡 Orta | Image preprocessing (crop, enhance, Instagram UI elementlerini çıkar) |
| **Ürün görselleri tutarsız** | 🟡 Orta | Katalog yüklenirken image quality check; birden fazla açıdan görsel iste |
| **GPU maliyeti yüksek** | 🟡 Orta | Başlangıçta API bazlı (OpenAI embedding API), ölçeklendikçe self-host |
| **WhatsApp API limitleri** | 🟡 Orta | Rate limiting, queue sistemi |
| **Multi-tenant veri izolasyonu** | 🟠 Yüksek | Qdrant namespace + row-level security PostgreSQL'de |
| **Catalog sync gecikmesi** | 🟡 Orta | Webhook bazlı real-time sync + periyodik full sync |

---

## Rekabet Analizi

| Rakip | Ne Yapıyor | Fark |
|-------|-----------|------|
| **Syte.ai** | Visual search for fashion | Enterprise, çok pahalı ($5K+/ay), API only |
| **ViSenze** | Visual commerce AI | Enterprise, Asya odaklı |
| **Google Lens** | Genel görsel arama | Mağazaya özel değil, stok bilgisi yok |
| **Pinterest Lens** | Görsel arama + shopping | Platform bağımlı |
| **Algolia** | Search-as-a-service | Metin bazlı, görsel arama yeni/zayıf |

**VPS'in farkı:**
- **Kanal entegre:** WhatsApp/Instagram DM'den direkt çalışıyor (rakiplerin hiçbirinde yok)
- **Uygun fiyat:** $29/ay'dan başlıyor (Syte $5K+)
- **Türkiye pazarı:** Türk e-ticaret altyapısına uyumlu (Trendyol feed, iyzico vb.)
- **Stok entegre:** Sadece ürünü bulmaz, stok/beden/renk durumunu da gösterir
- **Invekto sinerji:** Zaten WhatsApp CRM kullanan mağazalar için doğal eklenti

---

## AHA Moments (5 Öneri)

| Kategori | AHA | Etki |
|----------|-----|------|
| **UX** | Müşteri görsel gönderdiğinde "🔍 Arıyorum..." animasyonu + <15sn sonuç — mağaza çalışanı hiçbir şey yapmıyor | Müşteri WOW anı |
| **SPEED** | "Gece 2'de görsel gönderdim, 10 saniyede cevap geldi" — 7/24 otomatik | Satış kaçırmama |
| **RELIABILITY** | Bulamadığında "benzer ürünler" önerisi — asla "bulunamadı" deyip bırakmıyor | Müşteri elde tutma |
| **SALES** | Her arama = satın alma niyeti sinyali → CRM'e "sıcak lead" olarak düş | Dönüşüm artışı |
| **SUPPORT** | Mağaza sahibi dashboard'da "en çok aranan ama stoğumda olmayan ürünler" raporu → tedarik kararı | Stok optimizasyonu |

---

## Invekto Entegrasyonu (Opsiyonel)

VPS bağımsız çalışır ama Invekto ile kullanıldığında ek değer:

```
Invekto (WhatsApp CRM)              VPS (Visual Product Search)
┌────────────────────────┐          ┌──────────────────────────┐
│ Müşteri WhatsApp'tan   │          │                          │
│ görsel gönderir         │  API    │ Görseli analiz et        │
│                         │────────>│ Ürünleri bul             │
│ Invekto görseli algılar │         │ Stok bilgisi ekle        │
│ VPS'e iletir            │<────────│ Sonucu döndür            │
│                         │         │                          │
│ Müşteriye otomatik      │         │                          │
│ ürün kartı gönderir     │         │                          │
│                         │         │                          │
│ CRM'e "sıcak lead"     │         │                          │
│ olarak kaydeder         │         │                          │
└────────────────────────┘          └──────────────────────────┘
```

**Invekto Flow Builder entegrasyonu:**
- Flow Builder'da "Görsel Arama" node'u → Müşteri görsel gönderdiğinde tetiklenir
- `visual_search` trigger type → VPS API'yi çağırır → sonucu müşteriye iletir

---

## Sonraki Adımlar

- [ ] Q karar: Bu servise başlama zamanı geldi mi? Roadmap'te nereye oturuyor?
- [ ] Teknik PoC: CLIP + Qdrant ile basit bir görsel arama prototipi
- [ ] Maliyet doğrulama: Gerçek GPU/API maliyetlerini test et
- [ ] Pazar araştırması: Türkiye'deki e-ticaret mağazalarına bu özelliği sorun
- [ ] Invekto entegrasyon planı: Flow Builder'a "Görsel Arama" node'u ekleme planı
