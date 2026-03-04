# Dokumantasyon Plani (AI-Driven)

> Son guncelleme: 2 Mart 2026
> Platform: InvektoHelp (docs.invekto.com)
> Prensip: AI yazar, insan review eder. Haftada 4 makale. 6 haftada %40 → %85.

---

## 1. Mevcut Durum

### Invekto CRM Dokumanları (content/)

| Kategori | Mevcut | Hedef | Eksik |
|----------|--------|-------|-------|
| 01-baslarken | 1 makale | 4 makale | Giris, ilk adimlar, sektor rehberi |
| 02-iletisim | 2 makale | 6 makale | Mesaj turleri, sohbet yonetimi, AI asistan |
| 03-ayarlar | 4 makale | 8 makale | Otomasyon, bildirimler, entegrasyon ayarlari |
| 04-musteriler | 1 makale | 4 makale | CRM detay, segmentasyon, etiketler |
| 05-raporlar | 2 makale | 5 makale | Dashboard, RI, disa aktarma |
| 06-entegrasyonlar | 2 makale | 6 makale | Widget kurulumu, e-ticaret, API |
| **TOPLAM** | **12 makale** | **33 makale** | **21 makale** |

### Invekto One Dokumanları (InvektoServices/content/)

| Kategori | Mevcut | Hedef | Eksik |
|----------|--------|-------|-------|
| 01-genel-bakis | Var | Var | Guncelleme gerekebilir |
| 02-servisler | Var | Genisletilecek | Her servis icin ayri sayfa |
| 03-flow-builder | Var | Var | Premium/basic ayrimi eklenecek |
| 04-hesap-yonetimi | Var | Var | Billing dokumanı eklenecek |
| 05-ipuclari | Var | Genisletilecek | Sektore ozel ipuclari |
| **YENİ: Billing** | - | 3 makale | Plan, odeme, fatura |
| **YENİ: API Dokumani** | - | 5 makale | REST API referans |

---

## 2. AI Yazim Sureci

### Her Makale Icin Akis

```
1. Q veya Teknik Destek makale konusunu belirler
   ↓
2. Claude Code'a komut verilir:
   "docs.invekto.com icin [konu] hakkinda makale yaz.
    InvektoHelp MDX formatini kullan.
    Prism komponentlerini kullan (Step, Callout, Frame, QuickSummary).
    Turkce yaz, teknik terimler Ingilizce kalabilir.
    Ekran goruntuleri icin placeholder birak."
   ↓
3. AI makaleyi MDX formatinda yazar
   ↓
4. Teknik Destek review eder:
   - Icerik dogru mu?
   - Adimlar mantikli mi?
   - Ekran goruntuleri eklenmeli mi?
   ↓
5. Ekran goruntuleri InvektoHelp admin mode ile yuklenir
   ↓
6. Commit + Deploy (MCP ile)
```

### AI Icin Kurallar

InvektoHelp'in CLAUDE.md ve AI_OPERATING_MANUAL.md dosyalari zaten su kurallari tanimliyor:
- Flat design, Stripe-inspired minimal UI
- MDX + Prism komponentleri zorunlu (Step, Callout, Frame, Badge, QuickSummary, SeeAlso)
- SEO: 120-160 karakter description, JSON-LD schema
- Task Management Mode destegi (adim bazli checklist)
- Turkce, acik dil, jargon minimumu

**Ek kural:** Her makalede `<QuickSummary>` ile 3-5 maddelik ozet ile basla. Kullanici hizli baksin.

---

## 3. Icerik Takvimi (6 Hafta)

### Hafta 1-2: P0 — Baslarken + Iletisim (en cok aranan)

| # | Makale | Kategori | AI Payi | Reviewer |
|---|--------|----------|---------|----------|
| 1 | Invekto CRM'e giris ve ilk ayarlar | 01-baslarken | %95 | Teknik Destek |
| 2 | Hesabiniza kullanici ekleme | 01-baslarken | %90 | Teknik Destek |
| 3 | Sektorunuze ozel baslangic rehberi | 01-baslarken | %85 | Q |
| 4 | Mesaj paneli kullanim kilavuzu | 02-iletisim | %90 | Teknik Destek |
| 5 | Sohbet yonetimi: atama, etiket, kapatma | 02-iletisim | %90 | Teknik Destek |
| 6 | AI Asistan: nasil calisir, nasil ayarlanir | 02-iletisim | %80 | Q |
| 7 | Canli sohbet (WebChat) widget kurulumu | 06-entegrasyonlar | %90 | Teknik Destek |
| 8 | Otomatik cevaplar ve hazir mesajlar | 02-iletisim | %90 | Teknik Destek |

### Hafta 3-4: P1 — Ayarlar + Entegrasyonlar

| # | Makale | Kategori | AI Payi | Reviewer |
|---|--------|----------|---------|----------|
| 9 | Otomasyon kurallari ve tetikleyiciler | 03-ayarlar | %85 | Q |
| 10 | Bildirim ayarlari (email, push, ses) | 03-ayarlar | %90 | Teknik Destek |
| 11 | Entegrasyon ayarlari (genel) | 03-ayarlar | %85 | Teknik Destek |
| 12 | Calisma saatleri ve mesai disi mesajlar | 03-ayarlar | %95 | Teknik Destek |
| 13 | Shopify entegrasyonu | 06-entegrasyonlar | %85 | Q |
| 14 | Trendyol / Hepsiburada entegrasyonu | 06-entegrasyonlar | %85 | Q |
| 15 | WhatsApp API baglantisi | 06-entegrasyonlar | %80 | Q |
| 16 | Webhook ve API kullanimi | 06-entegrasyonlar | %75 | Q |

### Hafta 5-6: P2 — Musteriler + Raporlar + Billing

| # | Makale | Kategori | AI Payi | Reviewer |
|---|--------|----------|---------|----------|
| 17 | Musteri rehberi ve CRM | 04-musteriler | %90 | Teknik Destek |
| 18 | Musteri segmentasyonu ve etiketler | 04-musteriler | %90 | Teknik Destek |
| 19 | Musteri profili detaylari | 04-musteriler | %90 | Teknik Destek |
| 20 | Dashboard ve temel raporlar | 05-raporlar | %85 | Q |
| 21 | Revenue Intelligence (RI) nedir | 05-raporlar | %80 | Q |
| 22 | Rapor disa aktarma (Excel, PDF) | 05-raporlar | %90 | Teknik Destek |
| 23 | Abonelik ve plan yonetimi | YENİ: billing | %90 | Satis |
| 24 | Fatura goruntuleme ve indirme | YENİ: billing | %95 | Satis |

---

## 4. Blog Icerigi (AI-Driven, Surekli)

Blog InvektoWebsite'da (invekto.com/blog). Mevcut 4 makale var.

### AI Blog Yazim Sureci

```
1. Satis/Marketing konuyu belirler (veya AI anahtar kelime arastirmasi yapar)
2. Claude Code /blog-write ile makale yazar
3. /blog-seo-check ile SEO kontrol
4. /blog-analyze ile skor kontrol (hedef: 70+)
5. Satis review eder (icerik + ton)
6. Deploy
```

### Blog Takvimi (Haftada 2 makale)

| Hafta | Makale 1 | Makale 2 |
|-------|----------|----------|
| 1 | WhatsApp Business API nedir? (guncelle) | Canli destek yazilimi nasil secilir? |
| 2 | E-ticaret icin musteri iletisimi | Saglik sektoru icin hasta iletisimi |
| 3 | Chatbot vs canli destek: hangisi? | CRM nedir, neden onemli? (guncelle) |
| 4 | Musteri memnuniyeti olcme yontemleri | WhatsApp ile sepet kurtarma stratejileri |
| 5 | Cok kanalli iletisim nedir? | Emlak sektorunde dijital iletisim |
| 6 | KVKK uyumlu musteri iletisimi (guncelle) | AI destekli musteri hizmetleri |

**AI Payi:** %90 (AI yazar, Satis ekibi son okumayi yapar)

---

## 5. Otomatik Icerik Uretimi (Faz 5+)

Lansman sonrasi, AI ile surekli icerik dongusu:

| Kanal | Icerik Turu | Siklik | AI Payi |
|-------|-------------|--------|---------|
| Blog | SEO makaleleri | Haftada 2 | %90 |
| Dokumantasyon | Yeni ozellik dokumanları | Her release'de | %85 |
| Email | Musteriye newsletter | 2 haftada 1 | %80 |
| Sosyal medya | LinkedIn + Twitter postlari | Haftada 3 | %90 |
| WebChat | AI asistan bilgi tabani | Surekli guncelleme | %95 |
| YouTube | Scriptler (video Satis cekim yapar) | Ayda 2 | %70 script |

---

## 6. Kalite Kontrol

### Her Makale Icin Checklist

- [ ] MDX formati dogru (frontmatter, slug, description)
- [ ] QuickSummary ile basliyor
- [ ] Adimlar Step component ile yazilmis
- [ ] Uyarilar Callout component ile yazilmis
- [ ] Ekran goruntuleri Frame component ile sarili
- [ ] Ilgili makaleler SeeAlso ile baglanmis
- [ ] SEO: description 120-160 karakter
- [ ] Turkce, acik dil, jargon yok
- [ ] build basarili (next build hata vermiyor)

### Skor Hedefleri

- Blog: InvektoHelp blog-analyze skoru 70+
- Dokumantasyon: Icerik dogru + adimlar takip edilebilir + gorsel destekli

---

## 7. Sorumluluk Matrisi

| Rol | Gorev |
|-----|-------|
| **AI (Claude Code)** | Makaleleri yazar, SEO optimize eder, MDX formatlar |
| **Teknik Destek (2 kisi)** | CRM dokumanlarini review eder, ekran goruntuleri yukler |
| **Satis (2 kisi)** | Blog review, konu onerir, sosyal medya paylasir |
| **Q** | Teknik dokumanlar (API, RI, Invekto One), final onay |
