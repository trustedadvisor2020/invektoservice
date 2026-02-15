# Invekto Platform Roadmap v4.0

> Kaynak: Hormozi değer denklemi + 75 senaryo analizi + **Q interview (Invekto mevcut durum analizi)**
> Tarih: 2026-02-08
> Durum: DRAFT — Q onayı bekleniyor
> Felsefe: **Mevcut müşteriyi güçlendir → Yeni müşteri kazan. Otomasyon first. Niche derinleştir.**
> Önceki: v3.1 (2026-02-06) — "Sıfırdan başla" varsayımıyla yazılmıştı. v4.0 gerçekliğe dayalı.

---

## Dosya Yapısı

| Dosya | İçerik |
|-------|--------|
| **roadmap.md** *(bu dosya)* | Navigator — strateji, mimari, özet |
| [whatisinvekto.md](whatisinvekto.md) | **Invekto mevcut ürün envanteri** — 50+ müşterili çalışan ürünün tam analizi |
| [roadmap-phases.md](roadmap-phases.md) | Phase 0-6 detaylı plan, DB tabloları, başarı kriterleri |
| [roadmap-scenarios.md](roadmap-scenarios.md) | 75 senaryo (25 e-ticaret + 25 diş + 25 klinik/estetik) + Outbound Engine gereksinimleri |
| [roadmap-reviews.md](roadmap-reviews.md) | 4 uzman review (Dunford, Lemkin, Lenny, Hormozi) + aksiyonlar |

---

## Temel Prensip

```
v1 (Mühendis kafası):     Altyapı → Altyapı → AI → Entegrasyon → Revenue
v3 (Hormozi kafası):      Müşteri bul → 1 Çözüm → Revenue → Ölçekle → Altyapıyı ekle
v4 (GERÇEKLİK):          Mevcut müşteriyi güçlendir → Otomasyon → Niche derinleştir → Yeni müşteri
```

> **v4 NEDEN FARKLI:** Invekto sıfırdan başlamıyor. 50+ aktif müşteri, 50-200K TL MRR,
> 7 kanal, gelişmiş routing, CRM, VOIP — hepsi zaten var. Eksik olan **otomasyon ve AI katmanı**.
> Bu hem satış engeli ("Chatbot yok mu?") hem churn sebebi (otomasyon eksikliği).
> Detay: bkz [whatisinvekto.md](whatisinvekto.md)

Her phase'in 3 soruya cevabı olmalı:
1. **Mevcut müşteri ne kazanıyor?** (churn düşür, upsell artır)
2. **Yeni müşteri neden gelir?** (satış argümanı ne?)
3. **Minimal ne yapılmalı?** (overengineering yok)

---

## Mevcut Durum

> **ÖNEMLİ:** Invekto 2 parçadan oluşur. Ana uygulama (.NET) ve eklenti servisler (InvektoServis/Node.js).
> Bu roadmap her iki tarafı da kapsıyor.

### Invekto Ana Uygulama (Çalışan Ürün — .NET/Angular/SQL Server)

| Bileşen | Durum |
|---------|-------|
| Unified Inbox | ✅ 7 kanal (WA Cloud API, WA BSP, IG DM, FB Messenger, Telegram, SMS, VOIP) |
| Chat Routing | ✅ Gelişmiş — 4 algoritma, grup bazlı, kanal bazlı |
| Templates | ✅ Hızlı cevap şablonları (dinamik değişken YOK) |
| Outbound | ✅ Temel — tek tek mesaj gönderimi (broadcast YOK) |
| CRM | ✅ Otomatik contact, etiketler, 10 custom field (pipeline YOK) |
| Auth | ✅ Multi-tenant (firma + user + parola), 2 rol (User, Supervisor) |
| Raporlama | ✅ Kapsamlı — mesaj, agent performans, kanal dağılımı, ek metrikler |
| Agent Yönetimi | ✅ Performans, online/offline, supervisor monitor/takeover |
| VOIP | ✅ Çağrı merkezi, arama kaydı, raporlama |
| Multi-language | ✅ Çoklu dil desteği |
| KVKK/GDPR | ✅ Uyumlu |
| Entegrasyonlar | ✅ Shopify, Zoho, Webhook API |
| Müşteri sayısı | **50+** (ağırlıklı: sağlık klinikleri + otel/turizm) |
| MRR | **50-200K TL** |
| Fiyatlandırma | $25/agent + $40/kanal |

### InvektoServis Eklenti Servisler (Bu Repo — .NET 8 / C#)

| Bileşen | Durum |
|---------|-------|
| `Invekto.Backend` | Gateway, Ops Dashboard, Flow Builder SPA serve, port 5000 |
| `Invekto.ChatAnalysis` | Claude Haiku ile 15 kriterli chat analizi, port 7101 |
| `Invekto.Automation` | ✅ Chatbot flow engine (v1+v2), 12 node handler, port 7108 |
| `Invekto.AgentAI` | ✅ AI reply suggestion + intent detection + feedback learning, port 7105 |
| `Invekto.Outbound` | ✅ Broadcast + trigger engine + rate limiting + opt-out, port 7107 |
| `Invekto.Shared` | DTOs, logging, error codes, JWT auth, PostgreSQL factory |
| Dashboard | React + Vite — health monitoring, log viewer |
| Flow Builder | React + Vite + React Flow — visual chatbot flow editor |

### Kritik Eksikler (Satış engeli + churn sebebi)

| Eksik | Etki | Hedef Phase | Güncel Durum (2026-02-15) |
|-------|------|-------------|---------------------------|
| ~~Chatbot / Flow Builder / Otomasyon~~ | ~~🔴 #1 satış itirazı~~ | Phase 1 | ✅ **YAPILDI** — FlowEngine v2 + 12 node + Visual Builder. Kalan: iframe embed, auto-save, tema (FB-5) |
| ~~AI Agent Assist (cevap önerisi)~~ | ~~🔴 Agent zaman kaybı~~ | Phase 1 | ✅ **YAPILDI** — AgentAI servis + feedback learning. Kalan: otomatik etiketleme → Phase 2 |
| ~~Broadcast / toplu mesaj~~ | ~~🔴 Top 3 müşteri talebi~~ | Phase 1 | ✅ **YAPILDI** — Outbound servis + trigger engine |
| Otomasyon Dashboard | 🟠 Metrik ölçümü yok | Phase 2 | ⬜ Başlamadı (Phase 1'den taşındı) |
| Mobil uygulama | 🔴 Top 3 müşteri talebi | Phase 7 | ⬜ Başlamadı |
| Randevu motoru | 🟠 Mevcut klinik müşterileri bekliyor | Phase 2 (Hibrit v4.1) | ⬜ Başlamadı |
| Trendyol/HB API | 🟡 E-ticaret niche genişlemesi | Phase 3 (Hibrit v4.1) | ⬜ Başlamadı |

> Tam liste: bkz [whatisinvekto.md](whatisinvekto.md) — "Mevcut Olmayan Özellikler" bölümü

---

## Positioning (Tek Cümle — Dunford Kuralı)

> **Invekto (Bugün):** İşletmeler için WhatsApp ve 6 kanalı tek panelden yöneten CRM.
> 50+ müşteri, 7 kanal, gelişmiş routing, VOIP.
>
> **Invekto (Hedef):** WhatsApp'tan gelen müşteri mesajlarını AI ile otomatik yöneten iş asistanı.
> Otomasyon + AI + niche-özel çözümler.
>
> **English:** Invekto is a multi-channel CRM that manages WhatsApp, Instagram, and 5 more channels
> from one inbox — now adding AI automation to resolve customer inquiries automatically.
>
> Kaynak: 75 senaryo analizi + mevcut müşteri interview'ları

| Yapma | Yap |
|-------|-----|
| "AI-Powered Revenue & Support OS" | "7 kanallı CRM + AI otomasyon" |
| "Omnichannel CRM platform" | "Mesajları otomatik cevapla, agent'ları hızlandır" |
| "Sıfırdan ürün yapıyoruz" | "50+ müşterili ürüne AI katmanı ekliyoruz" |
| Mevcut müşteriyi unutup yeni niche ara | Mevcut müşteriyi güçlendir + yeni niche ekle |

### 3 Niche Paralel Giriş (Q Kararı — 2026-02-08)

> **Karar:** 3 niche'e aynı anda çıkılacak. Sağlık sektörü Phase 3-4'e ertelenmeyecek.
> Reklam ve web siteleri sektör bazlı ayrılacak. Ortak altyapı tek, offer'lar ayrı.

#### Üst Şemsiye Positioning

> **Invekto:** WhatsApp'tan gelen müşteri mesajlarını AI ile otomatik yöneten iş asistanı.
>
> - **E-ticaret satıcıları için:** Kargo ve iade sorularını otomatik çözer, temsilci maliyetini düşürür.
> - **Diş klinikleri için:** Fiyat sorularını randevuya çevirir, no-show'u %60 azaltır.
> - **Estetik klinikleri için:** Lead'leri hastaya dönüştürür, medikal turizmi ölçekler.

#### Ortak Altyapı (3 Sektörde %95+ Kullanım — 75 Senaryo Analizi)

| Capability | E-ticaret | Diş | Estetik | Toplam |
|------------|-----------|-----|---------|--------|
| **C8: Agent Assist** | 25/25 | 25/25 | 25/25 | **75/75 (%100)** |
| **C3: Templates** | 25/25 | 24/25 | 24/25 | **73/75 (%97)** |
| **C1: Unified Inbox** | 25/25 | 24/25 | 23/25 | **72/75 (%96)** |
| **C2: Routing** | 25/25 | 24/25 | 22/25 | **71/75 (%95)** |

> Bu 4 capability = ürünün omurgası. Sektör farketmez, tek codebase, farklı config.

#### Niche-Özel Capability'ler

| Capability | Hangi Niche | Neden |
|------------|------------|-------|
| C7: Knowledge/RAG | E-ticaret + Diş | Bilgi tutarlılığı (ürün bilgisi / tedavi bilgisi) |
| C11: E-commerce Integrations | Sadece E-ticaret | Trendyol/HB API |
| C10: Revenue Agent | Estetik ağırlıklı | Ödeme/depozit/lead dönüşümü |
| C12: Ads Attribution | Sadece Estetik | Click-to-WhatsApp kampanya tracking |
| C4: Reporting | Estetik ağırlıklı | Conversion takibi |
| C5/C6: Security | Diş + Estetik | KVKK sağlık verisi |

#### 3 Ayrı Offer (Tek Platform)

**OFFER 1: Invekto for Sellers (E-ticaret)**

| Bileşen | Detay |
|---------|-------|
| Sonuç vaadi | "Kargo/iade sorularının %50'sini otomatik cevapla" |
| Karar verici | Marketplace satıcısı (Mehmet) |
| Fiyat | 3.000-5.000 TL/ay |
| Garanti | 30 günde %50 oto-cevap yoksa 2. ay ücretsiz |
| Niche özel | C11 (Trendyol/HB API) + C7 (Knowledge) |

**OFFER 2: Invekto for Dental (Diş Klinikleri)**

| Bileşen | Detay |
|---------|-------|
| Sonuç vaadi | "Fiyat sorularını randevuya çevir, no-show'u %60 azalt" |
| Karar verici | Klinik sahibi (Dr. Burak) |
| Fiyat | 7.500 TL/ay |
| Garanti | 30 günde no-show düşmezse 2. ay ücretsiz |
| Niche özel | Randevu motoru + No-show önleme + C7 (Knowledge) + C5/C6 (KVKK) |

**OFFER 3: Invekto for Clinics (Estetik)**

| Bileşen | Detay |
|---------|-------|
| Sonuç vaadi | "Lead'leri hastaya dönüştür, medikal turizmi ölçekle" |
| Karar verici | Klinik sahibi (Dr. Selin) |
| Fiyat | 15.000-25.000 TL/ay |
| Garanti | 30 günde randevu dönüşümü artmazsa 2. ay ücretsiz |
| Niche özel | C10 (Revenue) + C12 (Ads Attribution) + C4 (Reporting) + Multi-lang |

#### Web & Reklam Stratejisi (3 Niche)

| Kanal | Yapı |
|-------|------|
| Landing page | 3 ayrı sayfa: /sellers, /dental, /clinics |
| Reklam | Sektör bazlı ayrı kampanyalar, farklı ağrı noktaları, farklı hedef kitle |
| Demo | Sektöre özel demo flow (e-ticaret: kargo, diş: randevu, estetik: lead) |
| Case study | Her sektörden ayrı case study hedeflenir |
| LinkedIn / Sosyal | Sektör bazlı content (satıcı grupları vs sağlık profesyoneli grupları) |
| SEO | 3 farklı anahtar kelime seti (WhatsApp otomasyon + sektör adı) |

#### Niche Bazlı Satış Dili Karşılaştırması

| | E-ticaret | Diş | Estetik |
|--|-----------|-----|---------|
| **Ağrı** | "Kargom nerede" mesaj yükü | "Fiyat ne kadar?" + no-show | Lead dönüşümü + medikal turizm |
| **Sonuç** | Temsilci maliyeti %40 düşer | No-show %60 azalır | Randevu dönüşümü %40 artar |
| **ROI dili** | 25.000 TL/ay tasarruf | Kayıp randevu geliri geri kazanılır | Lead başına maliyet düşer |
| **İlk AHA** | Kargo sorusu otomatik cevaplandı | Fiyat sorusu randevuya döndü | Instagram lead'i WhatsApp'a geçti |
| **Karar verici** | Satıcı / E-ticaret müdürü | Klinik sahibi / Diş hekimi | Klinik sahibi / Operasyon müdürü |
| **Satın alma süreci** | Hızlı (1 hafta) | Orta (2-3 hafta) | Orta-Yavaş (3-4 hafta) |

---

## Product Story

**Invekto = 7 kanallı CRM + AI otomasyon platformu**

*"50+ işletme zaten Invekto'dan mesajlarını yönetiyor. Şimdi AI ile otomatikleştiriyoruz."*

> **GERÇEKLİK NOTU:** Invekto "ürün arayan startup" değil, "büyümek isteyen çalışan ürün"dür.
> Mevcut güç: 7 kanal, gelişmiş routing, VOIP, CRM. Eksik: otomasyon, AI, chatbot.
> Hedef: Mevcut 50+ müşteriyi AI ile güçlendirmek + yeni sektörlere (e-ticaret, sağlık) genişlemek.

### Müşteri Avatarları (6 Persona)

#### [E1] Mehmet - Trendyol/HB D2C Satıcı (Primary)
```
İş: Trendyol'da günde 200+ sipariş yöneten e-ticaret satıcısı
Ekip: 3 WhatsApp temsilcisi
Günlük ağrı:
  → Günde 150+ "kargom nerede" mesajı
  → Temsilciler Trendyol panelinden sipariş arıyor, WhatsApp'a dönüyor — 5dk/mesaj
  → İade soruları karışıyor, yanlış bilgi veriliyor
  → Lead'ler Instagram'dan geliyor, takip edilmiyor
  → Sepet terk edenlere ulaşamıyor
Maliyet: 3 temsilci × 25.000 TL = 75.000 TL/ay

Invekto ile:
  → Kargo soruları otomatik cevaplanıyor
  → Temsilci 5dk yerine 30sn'de cevap veriyor
  → Lead'ler otomatik skorlanıyor
  → Sepet terk edenlere otomatik mesaj gidiyor
Tasarruf: 1 temsilci azaltma = 25.000 TL/ay
Invekto fiyatı: 5.000 TL/ay → 5x ROI
```

#### [E2] Ayşe - Shopify/WooCommerce D2C (5-20 kişi)
```
İş: Kendi sitesinden satış yapan orta ölçekli e-ticaret
Ekip: 5-20 kişi
Fark: Marketplace'e bağımlı değil, kendi müşteri tabanı var
```

#### [D1] Dr. Burak - Diş Klinik Sahibi
```
İş: 2-3 şubeli diş kliniği, günde 60+ WhatsApp mesajı
Ekip: 2 sekreter
Günlük ağrı:
  → Günde 30+ "fiyat ne kadar" mesajı
  → No-show oranı %25
  → Tedavi sonrası takip unutuluyor
```

#### [D2] Elif - Diş Kliniği Ön Büro Koordinatörü
```
İş: Tek şubeli klinikte randevu ve hasta iletişimi
Ekip: 1 sekreter yardımcısı
Fark: Operasyonel verimlilik odaklı
```

#### [A1] Dr. Selin - Estetik Klinik Sahibi
```
İş: Yüksek fiyatlı estetik işlemler (botox, dolgu, lazer)
Ekip: 3 kişi (sekreter + hasta koordinatörü + medikal turizm sorumlusu)
Günlük ağrı:
  → Yurtdışından hasta İngilizce yazıyor
  → Fiyat soruları hassas (işleme göre değişken)
```

#### [A2] Zeynep - Estetik Klinik Operasyon + Satış Sorumlusu
```
İş: Lead takibi, randevu dönüşümü, medikal turizm koordinasyonu
Ekip: 1-2 asistan
Fark: Satış odaklı + multi-language ihtiyacı
```

### Grand Slam Offer (E-ticaret)

```
"WhatsApp'tan gelen kargo ve iade sorularınızın %50'sini
 ilk 30 günde otomatik cevaplayacağız.

 Cevaplayamazsak 2. ay ücretsiz.

 İlk 10 Trendyol satıcısına özel lansman fiyatı."

Değer Denklemi:
  Hayalin Sonucu: Temsilci maliyeti %40 düşer         → YÜKSEK
  Gerçekleşme İhtimali: 30 gün garanti + case study    → YÜKSEK
  Zaman Gecikmesi: Hemen kurulum, 1 hafta sonuç        → DÜŞÜK
  Harcanan Efor: Biz kuruyoruz, siz izliyorsunuz       → DÜŞÜK

  Değer = (Yüksek × Yüksek) / (Düşük × Düşük) = ÇOK YÜKSEK
```

### Offer Stratejisi (Hormozi Kuralı)

**Feature satma, sonuç sat.**

| ❌ Feature Dili | ✅ Sonuç Dili |
|----------------|--------------|
| "AI-powered inbox" | "Kargo sorularının %50'si otomatik cevaplanır" |
| "Knowledge base entegrasyonu" | "Yanlış cevap oranı %80 düşer" |
| "Outbound engine" | "No-show oranı %60 azalır" |
| "Revenue agent" | "Sepet terk edenlerin %10'u geri döner" |

**Hormozi 4 Bileşen:**
- **Setup:** Biz kuruyoruz (white-glove onboarding)
- **Garanti:** 30 günde sonuç yoksa 2. ay ücretsiz
- **Kıtlık:** İlk 10 müşteriye özel fiyat
- **Risk reversal:** Para iade garantisi

### Sağlık Sektörü (Phase 0'dan Paralel Giriş — Mevcut Müşteri Tabanı)

> **Sağlık klinik müşterileri ZATEN VAR.** Erteleme yok — 3 niche Phase 0'dan itibaren paralel.
> Grand Slam Offer detayları: yukarıdaki "3 Niche Paralel Giriş" bölümünde Offer 2 (Dental) ve Offer 3 (Clinics).
> Sağlık avatarları: [D1] Dr. Burak (diş) ve [A1] Dr. Selin (estetik) — yukarıdaki persona setine bakın.

---

## Senaryo Portföyü (Özet)

> Detay: bkz [roadmap-scenarios.md](roadmap-scenarios.md)

**75 senaryo**, 3 sektör, 6 persona, toplam aylık etki potansiyeli hesaplanıyor:

| Sektör | Senaryolar | Personalar |
|--------|------------|------------|
| E-ticaret | 25 senaryo | [E1] Mehmet, [E2] Ayşe |
| Diş Klinikleri | 25 senaryo | [D1] Dr. Burak, [D2] Elif |
| Klinik/Estetik | 25 senaryo | [A1] Dr. Selin, [A2] Zeynep |

**Kritik bulgu:** Senaryoların çoğu **Outbound Engine** gerektiriyor. Bu olmadan gelir potansiyelinin büyük kısmı kilitli. Outbound Engine gereksinimleri ve detaylı senaryo tabloları: [roadmap-scenarios.md](roadmap-scenarios.md)

---

## Phase Planı (Özet — v4.0 Gerçeklik Bazlı)

> Detay: bkz [roadmap-phases.md](roadmap-phases.md)
> Mevcut durum: bkz [whatisinvekto.md](whatisinvekto.md)

**BAŞLANGIÇ NOKTASI:** 50+ müşteri, 50-200K TL MRR, 7 kanal CRM çalışıyor.

| Phase | Hafta | Odak | MRR Hedefi | Müşteri Hedefi |
|-------|-------|------|------------|----------------|
| **0** | 1-2 | ✅ Mevcut müşteri analizi + otomasyon stratejisi | 50-200K (mevcut) | 50+ (mevcut) |
| **1** | 3-8 * | 🔄 **Core Otomasyon** — chatbot, AI assist, broadcast, trigger sistemi | 200-300K | 60+ (mevcut + yeni) |
| **2** | 9-16 | **AI Derinleştirme + Kritik Niche (Hibrit v4.1)** — Knowledge/RAG, Agent Assist v2, multi-lang, randevu, dashboard, KVKK | 300-500K | 75+ |
| **3A** | 17-20 | **Platform Enablers (v4.3)** — Integrations servisi, Outbound v2, Randevu Advanced, Dashboard genişletme, Ads Attribution | 500-650K | 85+ |
| **3B** | 21-24 | **Niche Derinleştirme (v4.5)** — e-ticaret intent/B2B/iade, diş/estetik pipeline, outbound senaryolar, sağlık genişleme + **Voice AI + Review Rescue + Multilingual** | 650-800K | 100+ |
| **3C** | 25-28 | **Visual Product Search + Size/Fit AI (v4.5)** — görsel ürün arama + beden önerisi, CLIP + vector search, bağımsız SaaS | 800K-1M | 110+ |
| **3D** | 29-32 | **Face Analysis AI (v4.5)** — selfie → tedavi önerisi, MediaPipe + Claude Vision, estetik klinikler, bağımsız SaaS | 1M-1.2M | 120+ |
| **4** | 33-40 | **Enterprise** — SSO, audit, SLA, advanced analytics | 1.2-1.5M | 140+ |
| **5** | 33-40 | **Revenue & Ölçek** — ödeme, revenue agent, cart recovery, AR dil desteği | 1.2-2M | 170+ |
| **6** | 41-48 | **Operasyon & Analytics** — SLA, QA scoring (C13), conversation mining | 2M+ | 200+ |
| **7** | 49+ | **Genişleme** — mobil app, yeni kanallar/entegrasyonlar, global pazar | 2M++ | 200++ |

> \* Phase 1 timeline solo founder için **10-15 hafta** daha gerçekçi — bkz [roadmap-reviews.md](roadmap-reviews.md) Lemkin uyarısı.
> Sonraki phase'ler buna göre kayar.

Her phase'in detaylı adımları, DB tabloları, başarı kriterleri ve geçiş şartları: [roadmap-phases.md](roadmap-phases.md)

### Temel Varsayımlar

| Konu | Değer |
|------|-------|
| Başlangıç | **MRR = 50-200K, müşteri = 50+** (çalışan ürün) |
| Phase 0 | Mevcut 50+ müşteriyi analiz et + otomasyon stratejisi |
| Phase 1 öncelik | **Core otomasyon** (chatbot, AI, broadcast) — tüm sektörler |
| Sağlık niche | **Müşteri zaten var** — randevu motoru Phase 2'de |
| Auth | **Zaten var** — SSO/audit Phase 4'te genişletilecek |

### Sektör Bazlı Phase Planı (v4.0 — Mevcut Müşteri Tabanına Dayalı)

> **Fark:** Sağlık ve otel müşterileri ZATEN var. E-ticaret = yeni müşteri kazanım.
> Phase 1'deki core otomasyon TÜM sektörlere aynı anda fayda sağlıyor.

| Phase | Hafta | Tüm Sektörler (Core) | Sağlık (Mevcut Müşteri) | E-ticaret (Yeni Müşteri) | Otel (Mevcut Müşteri) |
|-------|-------|----------------------|-------------------------|--------------------------|----------------------|
| **0** | 1-2 | Mevcut müşteri ihtiyaç analizi | Klinik müşterileri dinle | E-ticaret pazar araştırması | Otel müşterileri dinle |
| **1** | 3-8 | Chatbot, AI Assist, Broadcast, Trigger | Tüm klinikler hemen faydalanır | Tüm potansiyel müşteriler faydalanır | Tüm oteller hemen faydalanır |
| **2** | 9-16 | Knowledge/RAG, Dashboard, Multi-lang | Randevu motoru + no-show + KVKK | RAG ile ürün bilgisi | RAG ile otel bilgisi |
| **3A** | 17-20 | Integrations, Outbound v2, Dashboard, Ads | Randevu Advanced | HB API + kargo | PMS entegrasyonu |
| **3B** | 21-24 | Niche intent, sağlık genişleme | Tedavi takip + yorum + medikal turizm | İade v1/v2, B2B, full attribution | Niche template |
| **4** | 25-32 | SSO, Audit, SLA, Analytics | Tam KVKK compliance | Enterprise security | Enterprise |
| **5** | 33-40 | Revenue Agent, Cart recovery | AR dil desteği | Cart recovery, cross-sell | Booking engine |
| **6** | 41-48 | SLA, QA Scoring (C13), Mining | Operasyonel mükemmellik | Operasyonel mükemmellik | Operasyonel mükemmellik |
| **7** | 49+ | Mobil app, yeni kanallar, global | Tüm niche'lere mobil erişim | Tüm niche'lere mobil erişim | Tüm niche'lere mobil erişim |

**MRR Büyüme Hedefi (Mevcut baz üzerine):**

```
Hafta:   1-2         3-8          9-16         17-24        25-32        33-40       41-48      49+
          │            │             │             │            │            │           │          │
Phase:    0            1             2             3            4            5           6          7
          │            │             │             │            │            │           │          │
BAZ:    50-200K     (mevcut)     (mevcut)      (mevcut)    (mevcut)    (mevcut)    (mevcut)   (mevcut)
YENİ:     0        +50-100K     +100-200K     +200-300K   +300-400K   +400-600K    +600K+     +800K+
TOPLAM: 50-200K    200-300K      300-500K      500-800K   800K-1.2M    1.2-2M       2M+        2M++
          │            │             │             │            │            │           │          │
Mevcut:  50+          50+           55+           65+          80+        100+        130+       130+
Yeni:     0           10+           20+           35+          50+         70+         70+        70+
Toplam:  50+          60+           75+          100+         130+        170+        200+       200+
```

> **Not:** "BAZ" = mevcut müşterilerden gelen gelir (korunacak + upsell ile artacak).
> "YENİ" = otomasyon/AI sayesinde kazanılacak yeni müşterilerden gelir.
> Mevcut müşteri churn'ü azaltma = yeni müşteri kazanmak kadar değerli.

---

## Mikro Servis Haritası (Evrimsel — Öncelik: Otomasyon > Niche > Enterprise)

> **Not:** Ana uygulama (Invekto/.NET) zaten Unified Inbox, Routing, Auth, CRM, VOIP, Raporlama içeriyor.
> InvektoServis = Invekto'ya AI, otomasyon ve niche-özel yetenekler kazandıran eklenti katmanı.

| # | Servis | Doğduğu Phase | Port | Tetikleyici |
|---|--------|---------------|------|-------------|
| 0 | `Invekto.Backend` | Mevcut | 5000 | Zaten var (Gateway + Ops Dashboard) |
| 0 | `Invekto.ChatAnalysis` | Mevcut | 7101 | Zaten var (sentiment + 15 kriter analiz) |
| 1 | `Invekto.Automation` | **Phase 1** | 7108 | **#1 öncelik: chatbot, trigger, flow engine** |
| 1 | `Invekto.AgentAI` | **Phase 1** | 7105 | **Agent Assist — cevap önerisi, intent detection** |
| 1 | `Invekto.Outbound` | **Phase 1** | 7107 | **Broadcast + toplu mesaj + zamanlama** |
| 2 | `Invekto.Knowledge` | **Phase 2** | 7104 | RAG + bilgi tabanı (AI doğruluğu artır) |
| 3 | `Invekto.Integrations` | **Phase 3A** | 7106 | Niche entegrasyonlar (Trendyol, PMS, randevu) |
| 3C | `Invekto.VisualSearch` | **Phase 3C** | 7111 | Görsel ürün arama + Size/Fit AI (CLIP + vector search, bağımsız SaaS) |
| 3D | `Invekto.FaceAnalysis` | **Phase 3D** | 7110 | Yüz analizi AI (MediaPipe + Claude Vision, estetik konsültasyon) |
| 4 | `Invekto.Audit` | Phase 4 | 7103 | Kurumsal müşteri talebi |

> **Not:** `Invekto.Auth` ayrı servis olarak yoktur — ana uygulama zaten auth'a sahip. SSO/2FA genişletmesi Phase 4'te.

---

## Veritabanı Stratejisi

> **İKİ VERİTABANI GERÇEKLİĞİ:**
> - Invekto ana uygulama: **SQL Server** (mevcut — 50+ müşteri verisi burada)
> - InvektoServis eklentiler: **PostgreSQL** (yeni servisler için)
> - İki DB arasında veri senkronizasyonu gerekecek (tenant_id bazlı)

| Servis | DB | Ne Zaman | Not |
|--------|-----|----------|-----|
| Invekto (ana) | SQL Server | **Mevcut** | Ana CRM verisi burada, dokunulmaz |
| Automation | PostgreSQL | Phase 1 | Chatbot flows, triggers, otomasyon kuralları |
| AgentAI | PostgreSQL | Phase 1 | Intent model, suggested replies log |
| Outbound | PostgreSQL | Phase 1 | Broadcast kampanyalar, gönderim kuyruğu |
| Knowledge | PostgreSQL + pgvector | **Phase 2** | RAG embeddings, bilgi tabanı |
| Integrations | PostgreSQL | **Phase 3** | Trendyol/HB sipariş cache, randevu slotları |
| Audit | PostgreSQL | Phase 4 | İşlem logları |

> Phase 1'de tek bir PostgreSQL instance yeterli. Servis başına ayrı DB, Phase 4'ten sonra.
> **KRİTİK:** Invekto SQL Server'daki tenant_id ile InvektoServis PostgreSQL'deki tenant_id eşleşmeli.

---

## Servis Bağımlılık Haritası (Evrimsel — v4.0)

```
                    Invekto Ana Uygulama (.NET)
                    ┌─────────────────────────────────┐
                    │ Unified Inbox, Routing, CRM,     │
                    │ Auth, Templates, VOIP, Raporlama │
                    │ 50+ müşteri, 7 kanal             │
                    └────────────┬────────────────────-─┘
                                 │ API
                                 ▼
Phase 1:                    Phase 2-3:                    Phase 4+:

┌──────────┐              ┌──────────┐                ┌──────────┐
│ Backend  │              │ Backend  │                │ Backend  │
│  :5000   │              │  :5000   │                │  :5000   │
└────┬─────┘              └────┬─────┘                └────┬─────┘
┌────┼────┬────┐         ┌────┼────┬────┬────┐      ┌────┼────┬────┬────┬────┐
│    │    │    │         │    │    │    │    │      │    │    │    │    │    │
▼    ▼    ▼    ▼         ▼    ▼    ▼    ▼    ▼      ▼    ▼    ▼    ▼    ▼    ▼
Auto Agent Outb Chat     Auto Agent Outb Integ Know  Auto Agent Outb Integ Know Audit
:7108:7105:7107:7101     :7108:7105:7107:7106:7104  :7108:7105:7107:7106:7104:7103

Phase 1 servisleri (CORE — tüm sektörlere fayda):
  → Automation (:7108) = chatbot, flow engine, trigger sistemi
  → AgentAI (:7105) = cevap önerisi, intent detection, agent assist
  → Outbound (:7107) = broadcast, zamanlı mesaj, toplu gönderim
  → ChatAnalysis (:7101) = mevcut analiz servisi (korunuyor)

Phase 2 eklentisi (AI DERİNLEŞTİRME + KRİTİK NİCHE — Hibrit v4.1):
  → Knowledge (:7104) = RAG, bilgi tabanı, AI doğruluğu

Phase 3 eklentisi (NİCHE GENİŞLEME + OUTBOUND v2 — Hibrit v4.1):
  → Integrations (:7106) = Trendyol/HB, randevu motoru, PMS

Phase 3C-3D eklentisi (GÖRSEL AI — v4.5):
  → VisualSearch (:7111) = görsel ürün arama + beden önerisi (CLIP + Size AI)
  → FaceAnalysis (:7110) = yüz analizi + tedavi eşleştirme (MediaPipe + Claude Vision)
```

---

## Mevcut ChatAnalysis'in Kaderi

`Invekto.ChatAnalysis` (port 7101) şu an Invekto ana uygulama tarafından API callback pattern'i ile çağrılıyor.

**Karar: Olduğu gibi kalsın + genişlesin.** ChatAnalysis ayrı servis olarak devam eder.

- ChatAnalysis = async analiz + callback (mevcut Invekto entegrasyonu — sentiment, 15 kriter)
- AgentAI (Phase 1'de doğar) = real-time intent detection + agent assist + reply suggestion
- Automation (Phase 1'de doğar) = chatbot flow engine + trigger sistemi

> **v4.0 DEĞİŞİKLİK:** AgentAI Phase 3'ten **Phase 1'e** çekildi. Sebebi: Agent Assist
> (cevap önerisi) #1 satış engeline ("Chatbot/AI yok mu?") doğrudan cevap veriyor.

---

## Mevcut Güçler ve Stratejik Kararlar

| Konu | Durum |
|------|-------|
| Başlangıç | **50+ müşteri, 50-200K MRR** (çalışan ürün) |
| #1 Öncelik | **Otomasyon + AI + Broadcast** (satış engeli + churn sebebi) |
| İlk 3 servis | **Automation + AgentAI + Outbound** (Phase 1) |
| Niche stratejisi | **3 paralel:** mevcut hizmet tabanı (sağlık+otel) + e-ticaret genişleme |
| Mevcut güçler | 7 kanal inbox, 4 algoritma routing, VOIP, CRM, multi-tenant auth |
| Satış engeli | **"Chatbot/AI yok mu?"** |
| Churn sebebi | **Otomasyon eksikliği** |
| Hedef MRR (40 hafta) | **1.2-2M TL** (mevcut baz + büyüme) |

---

## Teknik Tuzaklar

### Core (v4.0 ile eklenen)
1. **Ana uygulama (.NET) ile InvektoServis (Node.js) entegrasyonu** — İki farklı tech stack. API contract'ları net olmalı. Latency, hata yönetimi, retry mekanizması kritik.
2. **Mevcut müşteri verisiyle çalışma** — 50+ müşterinin mevcut verisi SQL Server'da. InvektoServis PostgreSQL kullanıyor. Veri senkronizasyonu veya çift okuma stratejisi gerekli.
3. **Chatbot/otomasyon mevcut akışları bozmamalı** — Mevcut routing, welcome mesajı, template sistemi çalışıyor. Yeni otomasyon bunların üstüne binmeli, değiştirmemeli.
4. **Broadcast WhatsApp policy riski** — Toplu mesaj = WhatsApp Business API kurallarına %100 uyumlu olmalı. Template approval, opt-out, 24h window, rate limiting zorunlu. İhlal → numara ban.

### Korunan (v3.1'den)
5. **"AI her şeyi çözer" sanma** — Phase 1'de 5-10 intent ile başla. Knowledge base olmadan genişletme.
6. **Entegrasyonlar "tek seferlik" yapılmaz** — Sync state + retry + idempotency zorunlu.
7. **Multi-tenant izolasyon** — Knowledge embeddings tenant bazlı olmak zorunda.
8. **Trendyol/HB API'leri kararsız** — Sprint tahminlerini %30 şişir.
9. **Müşteri feedback'i olmadan genişletme** — Her phase geçişi müşteri verisine dayansın.
10. **Outbound spam riski** — WhatsApp Business API kuralları sıkı. 24h window, template approval, opt-out zorunlu.
11. **Sağlık sektörü compliance** — AI tıbbi tavsiye vermemeli. Disclaimer zorunlu.
12. **Multi-language kalite** — Makine çevirisi yerine ayrı dil şablonları kullan.
13. **İade çevirme agresifliği** — Müşteriyi çok zorlama, 1 teklif + 1 follow-up + iade başlat.

---

## Uzman Review'ları (Özet)

> Detay: bkz [roadmap-reviews.md](roadmap-reviews.md)

| Uzman | Teşhis | Aksiyon | Durum |
|-------|--------|---------|-------|
| **Dunford** | Positioning bulanık (4 kategori) | Tek cümle positioning eklendi | ✅ |
| **Lemkin** | SaaS metrikleri eksik, Auth geç | Core metrics + Auth uyarısı + Expansion model eklendi | ✅ |
| **Lenny** | User journey tanımsız | First-Value Flow eklendi | ✅ |
| **Hormozi** | Offer katmanı eksik, feature satılıyor | Offer stratejisi + sonuç dili eklendi | ✅ |

**Açık aksiyonlar:**
- **Auth zamanlamasını izle** → Phase 3'te kurumsal talep ≥3 ise hızlandır
- **Landing page yazıldığında** → Dunford positioning'i test et, başka bir şey ekleme

---

## Expansion Model (Revenue Drivers)

> **Mevcut fiyatlandırma:** $25/agent + $40/kanal. Aşağıdaki driver'lar üzerine eklenir.

| Driver | Açıklama | Phase | Mevcut Durumda Var mı? |
|--------|----------|-------|----------------------|
| Agent Seat | Temsilci başına ücretlendirme | **Mevcut** | ✅ $25/agent |
| Channel Fee | Kanal başına ücretlendirme | **Mevcut** | ✅ $40/kanal |
| AI Credits | AI otomatik cevap kullanımı (paket bazlı) | Phase 1+ | ❌ YENİ |
| Automation Tier | Chatbot/otomasyon seviyesine göre plan | Phase 1+ | ❌ YENİ |
| Broadcast Volume | Toplu mesaj gönderim limiti + aşım | Phase 1+ | ❌ YENİ |
| Conversation Volume | Aylık konuşma limiti + aşım ücreti | Phase 3+ | ❌ YENİ |
| Integration Count | Entegrasyon sayısına göre tier | Phase 2+ | ❌ YENİ |

**Upsell Fırsatı (Mevcut Müşteriler):**
- Phase 1 çıktığında mevcut 50+ müşteriye AI/otomasyon paketi sunulabilir
- Mevcut $25/agent + $40/kanal fiyatına **+AI otomasyon tier** eklenebilir
- Bu = mevcut müşteri başına ARPU artışı = expansion revenue

**Lemkin Kuralı:** SaaS'ta büyüme = yeni müşteri + mevcut müşteri genişlemesi (expansion). Expansion model net churn'ü negatife çevirir.

---

## Revenue Timeline (v4.0 — Mevcut Baz Üzerine Büyüme)

```
Hafta:   1-2         3-8          9-16         17-24        25-32        33-40       41-48      49+
          │            │             │             │            │            │           │          │
Phase:    0            1             2             3            4            5           6          7
          │            │             │             │            │            │           │          │
Mevcut: 50-200K    koruma       koruma+       koruma+      koruma+      koruma+     koruma+    koruma+
                   upsell      upsell       upsell       upsell       upsell      upsell     upsell
Yeni:      0      +50-100K    +100-200K    +200-300K    +300-400K    +400-600K    +600K+     +800K+
TOPLAM: 50-200K   200-300K     300-500K     500-800K    800K-1.2M     1.2-2M       2M+        2M++
          │            │             │             │            │            │           │          │
Müşteri: 50+         60+           75+           100+        130+         170+       200+       200++
          │            │             │             │            │            │           │          │
Odak:   ANALİZ     OTOMASYON     AI+KRİTİK     NİCHE+SAĞLIK  ENTERPRISE   REVENUE   OPERASYON  GENİŞ.
                   +AI ASSIST    NİCHE(v4.1)   GENİŞLEME    +SSO+AUDIT   +ÖDEME    +SLA/QA    +MOBİL
                   +BROADCAST    +KNOWLEDGE    +OUTBOUND v2  +SLA         +CART     +ANALYTICS +GLOBAL
                   +TRIGGER      +RANDEVU+RAG  +TEDAVİ+ADS  +ANALYTICS   +AR DİL   +MINING    +YENİ CH.
```

---

## Açık Sorular (Q Kararı Gerekli)

### ✅ CEVAPLANMIŞ (v4.0 Interview ile)

| # | Soru | Cevap |
|---|------|-------|
| 1 | WapCRM ilişkisi | Invekto = WapCRM'in yeni adı. InvektoServis = eklenti servisler. |
| 2 | İlk müşteri var mı? | **50+ aktif müşteri var.** |
| 3 | Fiyat modeli | $25/agent + $40/kanal — mevcut ve çalışıyor |
| 4 | Niche | Mevcut müşteriler: sağlık + otel/turizm ağırlıklı. E-ticaret = yeni niche. |
| 5 | Sağlık niche zamanlaması | Klinik müşterileri zaten var, ertelemeye gerek yok |
| 6 | WhatsApp Business API | Hem Meta Cloud API hem BSP mevcut |
| 7 | Auth yapısı | Multi-tenant auth zaten var (firma + user + parola, 2 rol) |
| 8 | Ekip | Mevcut ürün çalışıyor, Q geliştiriyor |

### Hâlâ Açık Sorular

#### Otomasyon Stratejisi (v4.0 — EN ÖNCELİKLİ)
1. **Chatbot yaklaşımı:** Kural bazlı flow builder mı, AI bazlı conversation mı, yoksa hibrit mi?
2. **Otomasyon fiyatlandırması:** Mevcut $25/agent + $40/kanal üstüne AI/otomasyon nasıl fiyatlanacak?
3. **Broadcast limitleri:** Toplu mesaj gönderiminde müşteri başına limit ne olacak?
4. **Trigger sistemi scope'u:** Hangi event'ler trigger olabilecek? (yeni sohbet, etiket değişimi, sohbet kapatma, zamanlayıcı...)

#### Teknik
5. **InvektoServis ↔ Invekto entegrasyon yöntemi:** REST API, webhook, event bus? Latency beklentisi nedir?
6. **DB stratejisi:** InvektoServis PostgreSQL, Invekto SQL Server — veri senkronizasyonu nasıl olacak?
7. **Mobil uygulama teknolojisi:** Native (iOS/Android), React Native, Flutter?

#### İş Geliştirme
8. **Ödeme gateway:** iyzico mu PayTR mi? (Phase 5 için)
9. **Garanti modeli:** "30 günde sonuç yoksa X" tarzı garanti uygulayacak mıyız?
10. **E-ticaret niche:** Trendyol/HB satıcılarına ulaşma kanalı ne? (forumlar, LinkedIn, satıcı grupları?)
11. **Otel niche:** PMS (Property Management System) entegrasyonu hangi PMS'lerle? (Clock, Protel, HotelRunner?)
