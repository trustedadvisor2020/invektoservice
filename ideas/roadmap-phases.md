# Invekto — Phase Planı (Revenue-First)

> Ana dosya: [roadmap.md](roadmap.md)
> Senaryolar: [roadmap-scenarios.md](roadmap-scenarios.md)
> Uzman review'ları: [roadmap-reviews.md](roadmap-reviews.md)

---

### Phase 0 — Müşteri Validasyonu + Grand Slam Offer (Hafta 1-2)

> *"The longer you wait to sell, the longer you wait to learn."*

**Müşteri ne kazanıyor:** Henüz ürün yok. Müşteri söz alıyor.
**Revenue milestone:** 1 ödeme alan müşteri
**Kod yazılmaz.** Sadece iş geliştirme.

#### Adım adım:

```
Adım 0.1: Niche seç
├── Karar: Trendyol/HB satıcıları (50-500 sipariş/gün)
├── Neden: Hızlı satış döngüsü (1 hafta), net ağrı noktası, ödeme gücü var
└── Alternatifler ertelendi: Kurumsal (3-6 ay döngüsü), Hizmet sektörü (düşük ARPU)

Adım 0.2: 10 potansiyel müşteriyle görüş
├── Nereden bul: Trendyol satıcı forumları, LinkedIn, WhatsApp grupları
├── Soru listesi:
│   ├── Günde kaç WhatsApp mesajı alıyorsun?
│   ├── En çok ne soruyorlar?
│   ├── Temsilci başına maliyet nedir?
│   ├── Otomatik cevap olsa ne kadar ödersin?
│   └── En büyük 3 sorunun ne?
└── Hedef: Pattern'i doğrula, fiyat algısını öğren

Adım 0.3: Grand Slam Offer tasarla
├── Hayalin Sonuç: "Kargo sorularının %50'si otomatik cevaplanır"
├── Garanti: "30 günde sonuç yoksa 2. ay ücretsiz"
├── Kıtlık: "İlk 10 satıcıya özel fiyat"
└── Fiyat: 3.000-5.000 TL/ay (temsilci maliyetinin %10-20'si)

Adım 0.4: İlk müşteriyi kapat
├── Teklif sun
├── Ödeme al (ilk ay önceden)
└── Bu müşterinin spesifik ihtiyaçlarını haritalandır
```

**Phase 0 çıkışında elimizde:**
- 1 ödeyen müşteri
- Doğrulanmış ağrı noktası
- Net ihtiyaç listesi
- Fiyat validasyonu

**Phase 0 sonunda yoksa DURMA sinyali:**
- 10 görüşmede 0 ilgi → Niche yanlış, pivot et
- İlgi var ama ödeme yok → Offer zayıf, güçlendir
- Ödeme var → Phase 1'e geç

---

### Phase 1 — İlk Müşterinin Problemini Çöz (Hafta 3-6)

> *"Sell it, then build it."*

**Müşteri ne kazanıyor:** "Kargom nerede" sorularına otomatik cevap
**Revenue milestone:** 1 aktif ödeyen müşteri, ayda 3.000-5.000 TL
**Satış dili:** "Kargo sorularınızı otomatik cevaplayacağız"

#### Adım adım:

```
Adım 1.1: Trendyol API entegrasyonu (minimal)
├── Yapılacak:
│   ├── Trendyol API key setup (müşterinin key'i)
│   ├── Sipariş çekme (GET orders — sadece son 30 gün)
│   ├── Kargo tracking bilgisi çekme
│   └── Orders cache (PostgreSQL'de basit tablo)
├── Yapılmayacak:
│   ├── Webhook listener (henüz gereksiz — polling yeterli)
│   ├── Hepsiburada (henüz müşteri yok)
│   ├── Kargo firması API'leri (Trendyol tracking yeterli)
│   └── Ödeme gateway
└── Servis: Invekto.Integrations oluştur (minimal)

Adım 1.2: WhatsApp → kargo cevap pipeline (minimal)
├── Yapılacak:
│   ├── Mevcut ChatAnalysis'in intent detection kısmını genişlet
│   ├── Intent: "kargom nerede" / "sipariş durumu" / "ne zaman gelecek"
│   ├── Intent eşleşince → Trendyol'dan sipariş ara → tracking bilgisi dön
│   └── Eşleşmezse → temsilciye devret (human handoff)
├── Yapılmayacak:
│   ├── 20 intent taxonomy (3 intent yeterli başlangıçta)
│   ├── RAG / Knowledge base
│   ├── Reply generation / tone presets
│   └── Guardrails / PII detection
└── Servis: Backend endpoint + ChatAnalysis genişlemesi

Adım 1.3: Basit dashboard (müşteriye gösterilecek)
├── Yapılacak:
│   ├── Kaç soru geldi
│   ├── Kaç tanesi otomatik cevaplandı
│   ├── Kaç tanesi temsilciye devredildi
│   └── Günlük/haftalık trend
├── Yapılmayacak:
│   ├── SLA tracker
│   ├── QA scoring
│   ├── Conversation mining
│   └── Revenue attribution
└── Servis: Mevcut React dashboard genişler

Adım 1.4: Auth (MINIMAL)
├── Yapılacak:
│   ├── Email + şifre login (Backend'e basit endpoint)
│   ├── JWT token (refresh yok, 24h expiry)
│   ├── Dashboard'u auth arkasına al
│   └── Müşteri verisi izolasyonu (tenant_id filtreleme)
├── Yapılmayacak:
│   ├── SSO
│   ├── 2FA
│   ├── Session management
│   ├── Brute-force protection
│   ├── IP allowlist
│   └── Ayrı Auth microservice (Backend içinde yeterli)
└── Servis: Backend'e eklenir (ayrı servis DEĞİL)
```

**DB Tabloları (Phase 1 — minimal):**
```sql
-- Tek PostgreSQL instance

-- Users (Backend içinde)
users (id, tenant_id, email, password_hash, role, created_at)

-- Integrations
integration_accounts (id, tenant_id, provider, api_key_encrypted, status, created_at)
orders_cache (id, tenant_id, provider, external_order_id, customer_phone, tracking_code, order_status, order_data_json, synced_at, created_at)

-- Metrics (basit sayaçlar)
auto_reply_log (id, tenant_id, intent, question_text, was_resolved, created_at)
```

**User First-Value Flow (Lenny kuralı — ürün bu olmadan öğrenmez):**

```
┌─────────────────────────────────────────────────────────┐
│  İLK KULLANICI DENEYİMİ (Day 1)                        │
│                                                         │
│  1. Connect Trendyol                                    │
│     └── API key gir → test bağlantısı → ✓ Bağlandı     │
│                                                         │
│  2. Enable "Kargo Soruları Otomasyonu"                  │
│     └── Tek toggle: ON                                  │
│                                                         │
│  3. İlk otomatik cevap gönderildi                       │
│     └── WhatsApp'tan gelen "kargom nerede" → AI cevap   │
│     └── ⚡ AHA MOMENT: "Gerçekten otomatik cevapladı!"  │
│                                                         │
│  4. Dashboard'da ilk sonuç                              │
│     └── "Bugün X mesaj otomatik cevaplandı"             │
│     └── "Y dakika tasarruf edildi"                      │
│     └── "Deflection rate: %Z"                           │
│                                                         │
│  Day 7: Haftalık rapor → "%30 mesaj otomatik çözüldü"   │
│  Day 30: "Bu ay 450 mesaj otomatik, 1 temsilci tasarruf" │
│          └── Ödeme yenileme kararı BURADA verilir       │
└─────────────────────────────────────────────────────────┘

Bu akış tanımlı değilse:
  → Onboarding ekranı tasarlanamaz
  → İlk değer anı (aha moment) ölçülemez
  → 30 gün sonra neden kaldığını bilemezsin
```

**Phase 1 sonunda elimizde:**
- 1 müşteri aktif kullanıyor
- Kargo soruları otomatik cevaplanıyor
- Deflection rate ölçülüyor (hedef: %30+)
- İlk case study verileri birikiyor
- User first-value flow çalışıyor (connect → enable → aha → dashboard)

**Başarı kriterleri (Phase 2'ye geçiş şartı):**
- [ ] Deflection rate %30+ (otomatik cevaplanan / toplam)
- [ ] Müşteri 2. ay ödeme yapıyor (churn etmedi)
- [ ] Müşteri "X da olsa iyi olurdu" diyor → Phase 2 scope'u netleşiyor
- [ ] Time to first automated reply < 24h (onboarding'den ilk otomatik cevaba)

**Core SaaS Metrics (Lemkin kuralı — bunlar yoksa MRR kağıt üstünde kalır):**

```
┌─────────────────────────────────────────────────────────┐
│  PHASE 1-2 İÇİN ZORUNLU METRİKLER                      │
│                                                         │
│  1. Time to First Automated Reply (TTFAR)               │
│     └── Onboarding'den ilk otomatik cevaba kadar süre   │
│     └── Hedef: < 24h                                    │
│                                                         │
│  2. Weekly Deflection % (haftalık cohort)                │
│     └── Otomatik çözülen / toplam mesaj                 │
│     └── Phase 1 hedef: %30+ | Phase 2 hedef: %40+      │
│                                                         │
│  3. 30-Day Logo Retention                               │
│     └── 30 gün sonra hâlâ ödeyen müşteri oranı         │
│     └── Hedef: %80+ (< %80 = ürün sıkıntısı)           │
│                                                         │
│  4. Activation Tanımı                                   │
│     └── "Customer is LIVE" = en az 1 otomatik cevap     │
│         gönderildi + müşteri dashboard'a baktı          │
│                                                         │
│  5. Net Logo Churn (aylık)                              │
│     └── Kaybedilen müşteri / toplam müşteri             │
│     └── Hedef: < %10/ay (Phase 1-2'de)                  │
│                                                         │
│  ⚠️ Bu metrikler ölçülmüyorsa büyüme yanıltıcıdır.     │
│  MRR artıyor ama churn gizli büyüyorsa → catastrophe.   │
└─────────────────────────────────────────────────────────┘
```

> **Lemkin uyarısı — Auth zamanlama riski:**
> Auth Phase 4'te ama kurumsal müşteri SSO/audit olmadan pipeline'a bile girmez.
> Çözüm: Phase 3 sonunda kurumsal talep yoğunsa Auth'u Phase 3.5'e çek.
> "Kurumsal gelince yaparız" = "Kurumsal gelmeyecek çünkü bunlar yok."
> Takip metrikleri: Phase 2-3'te kaç kurumsal "SSO var mı?" diye sordu? ≥3 ise hızlandır.

---

### Phase 2 — 5-10 Müşteriye Ölçekle (Hafta 7-12)

> *"Make it work for one, then make it work for many."*

**Müşteri ne kazanıyor:** Kargo + iade + sipariş durumu otomatik, temsilci hızlanıyor
**Revenue milestone:** 5-10 aktif müşteri, MRR 15.000-50.000 TL
**Satış dili:** "Temsilci sayınızı artırmadan 2x mesaj yönetin"

#### Adım adım:

```
Adım 2.1: Intent genişletme (müşteri feedback'ine göre)
├── Phase 1'de öğrenilenler:
│   ├── Müşteriler en çok ne soruyor? → intent ekle
│   ├── Otomatik cevap yanlış mı veriyor? → doğruluk artır
│   └── Hangi sorularda temsilciye düşüyor? → çözülebilir mi?
├── Yapılacak:
│   ├── Intent sayısını 3 → 10-12'ye çıkar
│   │   ├── Kargom nerede (mevcut)
│   │   ├── Sipariş durumu (mevcut)
│   │   ├── Ne zaman gelecek (mevcut)
│   │   ├── İade nasıl yapılır (YENİ) → [S3: İade Çevirme akışına bağlanır]
│   │   ├── Ürün değişimi (YENİ)
│   │   ├── Fatura istiyorum (YENİ)
│   │   ├── Sipariş iptal (YENİ)
│   │   ├── Ürün stok durumu (YENİ)
│   │   ├── "Toptan fiyat var mı?" / "100 adet lazım" (YENİ) → [S5: B2B Lead]
│   │   └── Negatif yorum sinyali (YENİ) → [S1: Yorum Kurtarma prep]
│   ├── Confidence threshold ayarı (düşük güven → insan)
│   └── Multi-turn conversation (takip sorusu sorabilme)
└── Servis: ChatAnalysis / AgentAI genişleme

Adım 2.1b: B2B / VIP Lead Tespiti (YENİ — Senaryo S5)
├── Yapılacak:
│   ├── B2B sinyal algılama ("toptan", "100 adet", "kurumsal fatura")
│   ├── VIP flag + otomatik etiketleme
│   ├── Sales team alert (email/webhook)
│   ├── Müşteri geçmişi tarama (daha önce büyük sipariş vermiş mi?)
│   └── Özel teklif akışı başlatma (template)
├── Yapılmayacak:
│   ├── Tam CRM pipeline (basit flag + alert yeterli)
│   └── Otomatik fiyatlama (sales team devralır)
└── Servis: ChatAnalysis intent + Backend alert

Adım 2.2: Agent Assist v1 (temsilciyi hızlandır)
├── Yapılacak:
│   ├── Suggested reply (AI'ın önerdiği cevabı 1 tıkla gönder)
│   ├── Sipariş kartı (konuşma yanında müşterinin son siparişi)
│   └── Basit escalation notu (devredince AI özet bırakır)
├── Yapılmayacak:
│   ├── Tone presets (tek ton yeterli başta)
│   ├── "Neden bu cevap" açıklaması
│   ├── Next Best Action
│   ├── Guardrails (Phase 4'te)
│   └── PII prevention (Phase 4'te)
└── Servis: Backend + ChatAnalysis

Adım 2.3: Hepsiburada eklenmesi (ikinci marketplace)
├── Yapılacak:
│   ├── HB API entegrasyonu (Trendyol pattern'inin kopyası)
│   ├── Sipariş sync + tracking
│   └── Müşteri hangi platformdan geliyorsa oradan çek
├── Yapılmayacak:
│   ├── Kargo firması doğrudan API (marketplace tracking yeterli)
│   └── Webhook (polling devam)
└── Servis: Invekto.Integrations genişler

Adım 2.4: Onboarding otomasyonu (5-10 müşteriye ölçeklenme)
├── Yapılacak:
│   ├── Self-service Trendyol/HB API key girişi
│   ├── Basit tenant setup wizard
│   ├── Default intent ayarları (her müşteriye aynı başlangıç seti)
│   └── Tenant veri izolasyonu güçlendirme
├── Yapılmayacak:
│   ├── SSO
│   ├── Tam multi-tenant policy engine
│   └── Admin panel (basit config yeterli)
└── Servis: Backend + Dashboard

Adım 2.5: Kargo entegrasyonu (opsiyonel — müşteri istiyorsa)
├── Yapılacak (eğer müşteri talep ediyorsa):
│   ├── Aras Kargo tracking API
│   ├── Yurtiçi Kargo tracking API
│   └── Kargo durumu değişince proaktif mesaj opsiyonu
├── Yapılmayacak:
│   ├── PTT (düşük hacim)
│   └── Otomatik bildirim (manuel tetikleme yeterli başta)
└── Servis: Invekto.Integrations genişler

Adım 2.6: Outbound Engine v1 — Temel (YENİ)
├── Yapılacak:
│   ├── Basit trigger engine (event-based: sipariş teslim edildi, yorum geldi)
│   ├── Template engine (değişkenli mesaj şablonları)
│   ├── Gönderim kuyruğu + rate limiting (WhatsApp 24h window)
│   ├── Opt-out yönetimi ("STOP" → unsubscribe)
│   ├── Delivery status tracking (sent/delivered/read/failed)
│   └── Tenant-bazlı ON/OFF
├── Yapılmayacak:
│   ├── AI-generated personalization (Phase 3)
│   ├── Multi-language templates (Phase 3-4)
│   ├── Conversion tracking (Phase 3)
│   └── Campaign yönetimi (Phase 3)
├── Kullanacak senaryolar: S1 prep, S3 follow-up, S5 alert
└── Servis: Invekto.Outbound (YENİ — port 7107) veya Backend içinde başlar

Adım 2.7: İade Çevirme v1 — Basit (YENİ — Senaryo S3 prep)
├── Yapılacak:
│   ├── "İade etmek istiyorum" intent'i algıla
│   ├── Neden sor (kalite/beden/renk/hasarlı/fikrini değiştirdi)
│   ├── Nedene göre aksiyon:
│   │   ├── Beden/renk → değişim öner
│   │   ├── Fikrini değiştirdi → kupon/indirim öner
│   │   └── Kalite/hasar → iade sürecini başlat
│   └── Basit conversion tracking (iade → çevrildi/çevrilemedi)
├── Yapılmayacak:
│   ├── Otomatik kupon oluşturma (temsilci onay verir)
│   └── Marketplace iade API entegrasyonu (sadece bilgi + yönlendirme)
└── Servis: ChatAnalysis intent + Backend
```

**DB Tabloları (Phase 2 eklentileri):**
```sql
-- Phase 1 tablolarına ek

-- Agent Assist
suggested_replies (id, tenant_id, conversation_id, intent, reply_text, was_accepted, created_at)

-- Daha zengin metrics
daily_metrics (id, tenant_id, date, total_messages, auto_resolved, human_handled, avg_response_time_sec, created_at)

-- Outbound Engine v1
outbound_templates (id, tenant_id, name, trigger_event, message_template, variables_json, is_active, created_at, updated_at)
outbound_messages (id, tenant_id, template_id, recipient_phone, message_text, status, sent_at, delivered_at, read_at, failed_reason, created_at)
  -- status: 'queued' | 'sent' | 'delivered' | 'read' | 'failed'
outbound_optouts (id, tenant_id, phone, reason, created_at)

-- B2B / VIP Lead Detection
vip_flags (id, tenant_id, customer_phone, flag_type, signal_text, sales_notified, created_at)
  -- flag_type: 'b2b_signal' | 'high_value' | 'repeat_customer'

-- İade Çevirme Tracking
return_deflections (id, tenant_id, conversation_id, original_intent, reason_category, action_taken, was_deflected, created_at)
  -- reason_category: 'size' | 'color' | 'quality' | 'damaged' | 'changed_mind'
  -- action_taken: 'exchange_offered' | 'coupon_offered' | 'return_started'
```

**Phase 2 sonunda elimizde:**
- 5-10 aktif müşteri
- MRR 15.000-50.000 TL
- Deflection rate %40+ (hedef)
- Trendyol + HB entegrasyonu çalışıyor
- Agent Assist temel seviyede çalışıyor
- Outbound Engine v1 çalışıyor (basit trigger + template)
- B2B/VIP lead tespiti aktif
- İade çevirme akışı temel seviyede çalışıyor
- İlk case study yayınlanabilir
- Product-market fit sinyalleri net

**Başarı kriterleri (Phase 3'e geçiş şartı):**
- [ ] 5+ aktif ödeyen müşteri
- [ ] Churn rate <%20 (ayda 1'den az kayıp)
- [ ] Müşteriler "ürün sorularına da cevap verse" diyor → Knowledge ihtiyacı doğuyor
- [ ] "AI yanlış cevap veriyor" şikayeti → RAG ihtiyacı doğuyor
- [ ] Outbound mesaj gönderiliyor, delivery rate %90+ (teknik çalışıyor)
- [ ] En az 1 B2B lead yakalandı ve sales team'e aktarıldı

---

### Phase 3 — Knowledge Base + Akıllı Agent (Hafta 13-20)

> *"AI accuracy = retention. Yanlış cevap veren AI, müşteri kaybettirir."*

**Müşteri ne kazanıyor:** "AI artık şirket verisinden cevap veriyor, sallama yapmıyor"
**Revenue milestone:** 15-25 müşteri, MRR 50.000-125.000 TL
**Satış dili:** "Ürün sorularınızı da otomatik cevaplayacağız — kendi verinizle"

#### Adım adım:

```
Adım 3.1: Knowledge Service oluştur
├── Yapılacak:
│   ├── Invekto.Knowledge microservice (port 7104)
│   ├── PDF upload + chunking (ürün katalog, SSS, politikalar)
│   ├── FAQ editor (hızlı soru-cevap girişi)
│   ├── Embeddings pipeline (pgvector)
│   ├── Retrieval API (topK + tenant izolasyonu)
│   └── Kaynak referanslı cevap ("pricing.pdf sayfa 3'e göre...")
├── Yapılmayacak:
│   ├── URL crawl (PDF + FAQ yeterli başta)
│   ├── Document versioning (v1 yeterli, versiyonlama sonra)
│   └── Knowledge gap report (veri birikmesi lazım)
└── Servis: Invekto.Knowledge (YENİ)

Adım 3.2: AgentAI orchestrator oluştur
├── Yapılacak:
│   ├── Invekto.AgentAI microservice (port 7105)
│   ├── Pipeline: message → intent → knowledge lookup → response → output
│   ├── Intent sayısını 10 → 15-20'ye çıkar
│   │   ├── Mevcut: kargo, iade, sipariş, iptal, fatura, stok, değişim...
│   │   ├── YENİ: fiyat sorusu, ürün karşılaştırma, garanti, kampanya...
│   │   └── YENİ: genel SSS (iade politikası, teslimat süresi, ödeme yöntemleri)
│   ├── Kaynak yoksa "insana devret" kuralı
│   └── Mevcut ChatAnalysis'in 15 kriterli analizi korunur (ayrı servis)
├── Yapılmayacak:
│   ├── Revenue Agent (Phase 5'te)
│   ├── Guardrails (Phase 4'te)
│   ├── PII detection (Phase 4'te)
│   └── Next Best Action (Phase 5'te)
└── Servis: Invekto.AgentAI (YENİ)

Adım 3.3: Agent Assist v2
├── Yapılacak:
│   ├── Reply generation artık Knowledge'dan beslenecek
│   ├── "Neden bu cevap" açıklaması + kaynak referansı
│   ├── Tone presets (formal / kısa / samimi)
│   └── Multi-turn: AI takip sorusu sorabiliyor
├── Yapılmayacak:
│   ├── Guardrails (Phase 4)
│   ├── PII prevention (Phase 4)
│   └── Handoff notes auto-generate (Phase 4)
└── Servis: AgentAI + Dashboard UI

Adım 3.4: Outbound Engine v2 — Tam Kapsamlı (YENİ)
├── Phase 2'deki temel engine genişletiliyor:
├── Yapılacak:
│   ├── Campaign yönetimi (kampanya oluştur, hedef kitle seç, zamanlama)
│   ├── AI-generated personalization (müşteri geçmişine göre mesaj)
│   ├── Conversion tracking (mesaj → aksiyon: cevap/satın alma/randevu)
│   ├── A/B testing (2 şablon → hangisi daha iyi dönüyor)
│   ├── Multi-language template desteği (TR/EN — sağlık niche'i için hazırlık)
│   ├── Time-based trigger'lar (T+Xh delay, recurring schedule)
│   └── ROI dashboard (kampanya bazlı gelir etkisi)
├── Yapılmayacak:
│   ├── Arapça template (Phase 4-5)
│   └── Predictive send-time (veri yetersiz henüz)
├── Desteklenen senaryolar: S1 tam, S3 tam, S4 prep, S7 prep
└── Servis: Invekto.Outbound genişleme

Adım 3.5: Negatif Yorum Kurtarma Akışı (YENİ — Senaryo S1)
├── Yapılacak:
│   ├── Trendyol Review API entegrasyonu (1-2 yıldız yorum tespiti)
│   ├── Otomatik tetikleme: yorum geldi → AI mesaj hazırla
│   ├── Mesaj akışı:
│   │   ├── T+0: "Memnuniyetsizliğiniz için özür dileriz. Ne yapabiliriz?"
│   │   ├── Çözüm kabul → yorum güncelleme ricası
│   │   └── T+48h: Cevap yoksa 1 kez daha dene
│   ├── Yorum recovery tracking (kurtarılan/kurtarılamayan)
│   └── Kurtarma oranı dashboard'da göster
├── Yapılmayacak:
│   ├── Otomatik yorum cevaplama (sadece WhatsApp mesajı)
│   └── Sahte yorum tespiti
└── Servis: Integrations (Trendyol Review API) + Outbound

Adım 3.6: İade Çevirme v2 — Tam Akış (YENİ — Senaryo S3 genişleme)
├── Phase 2'deki basit akış genişletiliyor:
├── Yapılacak:
│   ├── Otomatik kupon oluşturma (tenant tanımlı limitler içinde)
│   ├── Değişim stok kontrolü (Integrations'tan stok sorgula)
│   ├── İade çevirme başarı oranı (%): çevrilen / toplam iade talebi
│   ├── Kurtarılan gelir dashboard'da göster
│   └── Follow-up (T+24h: "Değişim ürününüz yolda, memnun musunuz?")
├── Yapılmayacak:
│   ├── Marketplace iade API otomasyonu (temsilci hâlâ panel'den yapar)
│   └── Otomatik iade onayı (risk var — temsilci onay verir)
└── Servis: AgentAI + Outbound + Integrations

Adım 3.7: Multi-Language AI (YENİ — Sağlık niche'i hazırlık)
├── Yapılacak:
│   ├── ChatAnalysis'e language detection ekle
│   ├── AgentAI response'unu tespit edilen dilde döndür
│   ├── Knowledge base multi-language support (aynı FAQ, farklı diller)
│   ├── Outbound template'lerde dil seçimi
│   └── Desteklenen diller: TR, EN (AR Phase 4-5'te)
├── Yapılmayacak:
│   ├── Arapça (henüz — sağlık niche'i kanıtlanmadan)
│   └── Otomatik çeviri (ayrı dil şablonları, makine çevirisi değil)
├── Neden şimdi: Sağlık niche'ine giriş için hazırlık + e-ticaret'te
│   yabancı müşteri desteği
└── Servis: ChatAnalysis + AgentAI + Knowledge

Adım 3.8: Dashboard genişletme
├── Yapılacak:
│   ├── Knowledge management UI (doc yükle, FAQ ekle)
│   ├── Intent performance (hangi intent ne kadar çözüyor)
│   ├── Top unanswered questions (bilgi tabanında eksik ne var)
│   ├── Müşteri bazlı deflection rate
│   ├── Outbound campaign dashboard (gönderim/okunma/dönüşüm)
│   ├── İade çevirme oranı + kurtarılan gelir
│   └── Yorum kurtarma oranı + etki
└── Servis: Dashboard
```

**DB Tabloları (Phase 3 — Knowledge + Outbound v2 + Yorum + İade):**
```sql
-- Knowledge DB (aynı PostgreSQL instance, ayrı schema)
documents (id, tenant_id, title, source_type, status, created_at, updated_at)
chunks (id, document_id, tenant_id, content, chunk_index, metadata_json, created_at)
-- pgvector column: chunks tablosuna embedding vector eklenir
faqs (id, tenant_id, question, answer, category, lang, created_at, updated_at)
  -- lang: 'tr' | 'en' (multi-language FAQ)
tags (id, tenant_id, name, created_at)
document_tags (document_id, tag_id)

-- Outbound v2 (Phase 2 tablolarına ek)
outbound_campaigns (id, tenant_id, name, trigger_type, target_criteria_json, template_id, schedule_json, status, stats_json, created_at, updated_at)
  -- trigger_type: 'event' | 'time' | 'condition'
  -- status: 'draft' | 'active' | 'paused' | 'completed'
outbound_conversions (id, tenant_id, message_id, campaign_id, conversion_type, value_amount, created_at)
  -- conversion_type: 'reply' | 'purchase' | 'appointment' | 'review_updated'

-- Yorum Kurtarma (S1)
review_alerts (id, tenant_id, provider, external_review_id, rating, review_text, customer_phone, recovery_status, created_at, updated_at)
  -- provider: 'trendyol' | 'hepsiburada'
  -- recovery_status: 'detected' | 'contacted' | 'resolved' | 'updated' | 'failed'

-- İade Çevirme v2 (Phase 2 return_deflections genişler)
-- return_deflections tablosuna ek kolonlar:
--   coupon_code, coupon_amount, exchange_product_id, saved_revenue_amount
```

**Phase 3 sonunda elimizde:**
- 15-25 müşteri (e-ticaret)
- MRR 50.000-125.000 TL
- Deflection rate %50+ (knowledge sayesinde artış)
- AI kaynaklı cevap veriyor
- Müşteriler kendi dokümanlarını yükleyebiliyor
- "Yanlış cevap" şikayetleri %80 azalmış
- Outbound Engine tam çalışıyor (kampanya + conversion tracking)
- Yorum kurtarma aktif, recovery rate ölçülüyor
- İade çevirme tam akışta, kurtarılan gelir ölçülüyor
- Multi-language AI (TR/EN) çalışıyor → sağlık niche'ine hazır
- **Sağlık niche pilot:** İlk 1-2 klinikle görüşme başlayabilir

**Başarı kriterleri (Phase 4'e geçiş şartı):**
- [ ] 15+ aktif ödeyen müşteri
- [ ] En az 3 müşteri knowledge base'i aktif kullanıyor
- [ ] Deflection rate %50+ (Knowledge ile)
- [ ] Outbound conversion rate %5+ (mesaj → aksiyon)
- [ ] İade çevirme oranı %15+ (çevrilen / toplam iade)
- [ ] Multi-language çalışıyor (en az TR + EN)
- [ ] Kurumsal müşteri talepleri geliyor → "SSO var mı? Audit log var mı?"
- [ ] Sağlık niche'inde en az 2 klinikle görüşülmüş

---

### Phase 4 — Enterprise Altyapı: Auth + Audit + Güvenlik (Hafta 21-28)

> *"Security'yi müşteri isteyince yap — ama isteyince hızlı yap."*

**Müşteri ne kazanıyor:** "Kurumsal güvenlik sertifikası — IT ekibiniz onay verir"
**Revenue milestone:** 25-40 müşteri + ilk kurumsal kontrat, MRR 125.000-300.000 TL
**Satış dili:** "Bankalar bile onaylıyor: SSO, 2FA, audit log, KVKK uyumlu"

> **Neden şimdi?** Phase 3'te büyük müşteriler kapıyı çalmaya başlıyor.
> "Demo güzel ama SSO yok, audit yok — procurement veto" diyorlar.
> Artık ihtiyaç kanıtlanmış, yatırım yapılabilir.

#### Adım adım:

```
Adım 4.1: Auth Service (tam kapsamlı)
├── Yapılacak:
│   ├── Invekto.Auth microservice (port 7102)
│   ├── Backend'deki basit auth → Auth service'e taşı
│   ├── JWT + Refresh token
│   ├── Session management (device list, revoke, timeout policy)
│   ├── Failed login log + brute-force protection (5 deneme → 15dk kilit)
│   ├── IP allowlist (CIDR destekli)
│   ├── Google OIDC (SSO)
│   ├── Microsoft OIDC (SSO) + Azure tenant id
│   ├── TOTP 2FA (QR + 10 backup code)
│   ├── 2FA enforcement policy (tenant bazlı)
│   ├── Country allowlist (GeoIP)
│   └── Tenant bazlı policy engine
└── Servis: Invekto.Auth (YENİ)

Adım 4.2: Audit Service
├── Yapılacak:
│   ├── Invekto.Audit microservice (port 7103)
│   ├── Append-only event store
│   ├── Event schema: event_type, actor_id, tenant_id, resource_type, resource_id, ip, user_agent, correlation_id
│   ├── Kritik event coverage:
│   │   ├── Login/logout
│   │   ├── Permission change
│   │   ├── Data export
│   │   ├── Conversation view/delete
│   │   ├── Knowledge update/delete
│   │   ├── AI prompt change
│   │   └── Configuration change
│   ├── Search API (tarih/actor/resource/event_type filtre)
│   ├── Audit UI (Supervisor erişimli)
│   └── Retention policy per tenant + scheduled purge job
└── Servis: Invekto.Audit (YENİ)

Adım 4.3: PII Koruma
├── Yapılacak:
│   ├── PII detector (TC, telefon, email, IBAN, adres)
│   ├── Mesaj görüntülemede maskeleme
│   ├── Temsilci outgoing mesajında PII uyarısı (TC/IBAN tespit → uyar)
│   ├── Export'da PII redaction toggle
│   └── Legal hold (açıkken purge çalışmaz)
└── Servis: Audit + AgentAI entegrasyonu

Adım 4.4: Guardrails (AgentAI'a eklenir)
├── Yapılacak:
│   ├── Banned phrases / sensitive content (tenant-level kurallar)
│   ├── PII prevention on outgoing (TC/IBAN tespit → blokla veya onay)
│   ├── AI aksiyonlarını audit'e logla
│   └── Escalation auto-notes (devredilince AI özet)
└── Servis: AgentAI genişleme

Adım 4.5: Admin Panel
├── Yapılacak:
│   ├── Tenant yönetimi (kullanıcı davet, rol atama)
│   ├── Security policy paneli (SSO/2FA/IP/timeout ayarları)
│   ├── Audit log viewer (filtre + export)
│   └── PII redaction ayarları
└── Servis: Dashboard genişleme

Adım 4.6: Sağlık Niche Girişi (YENİ — Phase 3'te hazırlanan altyapı ile)
├── Yapılacak:
│   ├── Sağlık sektörüne özel intent seti:
│   │   ├── "İmplant ne kadar?" → fiyat aralığı + ücretsiz muayene teklifi [S6]
│   │   ├── Randevu alma/değiştirme/iptal
│   │   ├── "Ağrım var" → acil triage (doktor alert vs randevu)
│   │   └── Tedavi bilgi soruları (Knowledge base'den)
│   ├── Randevu yönetimi modülü (basit slot engine):
│   │   ├── Müsait slot gösterme
│   │   ├── Randevu onayı → otomatik WhatsApp teyit
│   │   ├── Randevu değişikliği/iptali
│   │   └── Google Calendar / basit takvim sync
│   ├── No-Show Önleme akışı [S7]:
│   │   ├── T-48h hatırlatma + onay iste ("Evet geliyorum" / "İptal")
│   │   ├── T-2h son hatırlatma
│   │   ├── İptal → slot'u otomatik boşalt → bekleme listesine sor
│   │   └── No-show tracking + rate dashboard
│   ├── İlk 2-3 klinik ile pilot program
│   └── Sağlık Grand Slam Offer ile satış
├── Yapılmayacak:
│   ├── Tedavi sonrası takip otomasyonu (Phase 5)
│   ├── Google yorum motoru (Phase 5)
│   ├── Medikal turizm multi-language (Phase 5)
│   └── HBYS entegrasyonu (çok erken)
└── Servis: AgentAI + Outbound + Backend (yeni endpoint'ler)
```

**DB Tabloları (Phase 4 — Auth + Audit + Sağlık):**
```sql
-- Auth DB
auth_users (id, tenant_id, email, password_hash, sso_provider, sso_subject, totp_secret, backup_codes, status, created_at, updated_at)
sessions (id, user_id, device_id, ip, country, user_agent, expires_at, created_at)
login_attempts (id, email, ip, device_info, reason, created_at)
tenant_policies (id, tenant_id, session_timeout_min, max_failed_attempts, ip_allowlist_json, country_allowlist_json, sso_required, tfa_required, created_at, updated_at)

-- Audit DB
audit_events (id, tenant_id, event_type, actor_id, resource_type, resource_id, ip, user_agent, correlation_id, payload_before_json, payload_after_json, created_at)
retention_policies (id, tenant_id, resource_type, retain_days, legal_hold, created_at, updated_at)

-- Sağlık Niche — Randevu Yönetimi
appointments (id, tenant_id, patient_phone, patient_name, service_type, slot_start, slot_end, status, reminder_sent_48h, reminder_sent_2h, confirmed, no_show, created_at, updated_at)
  -- status: 'booked' | 'confirmed' | 'cancelled' | 'completed' | 'no_show'
appointment_slots (id, tenant_id, day_of_week, start_time, end_time, max_capacity, is_active, created_at)
waitlist (id, tenant_id, patient_phone, preferred_date, preferred_time, status, created_at)
```

**Phase 4 sonunda elimizde:**
- 25-40 müşteri e-ticaret + 3-5 klinik (sağlık pilot)
- MRR 125.000-300.000 TL (e-ticaret) + 25.000-75.000 TL (sağlık)
- SSO/2FA/Audit/PII enterprise-ready
- İlk kurumsal kontrat imzalanabilir
- Procurement checklist'in %90'ı karşılanıyor
- Sağlık niche'i pilot çalışıyor (no-show önleme + fiyat→randevu)

**Başarı kriterleri (Phase 5'e geçiş şartı):**
- [ ] En az 1 kurumsal müşteri SSO ile bağlandı
- [ ] Audit log çalışıyor, Supervisor erişiyor
- [ ] PII maskeleme aktif
- [ ] Kurumsal satış pipeline'ı açıldı
- [ ] Sağlık pilot: en az 2 klinik aktif, no-show %25→%10 altında
- [ ] Sağlık pilot: fiyat→randevu dönüşüm oranı %20+

---

### Phase 5 — Revenue Agent: Satış Yapan AI (Hafta 29-36)

> *"Support AI para toplar. Revenue Agent satış yapar."*

**Müşteri ne kazanıyor:** "AI sadece soruları cevaplamıyor, satış da yapıyor"
**Revenue milestone:** 40-60 müşteri, MRR 300.000-500.000 TL + premium tier
**Satış dili:** "Lead'den ödemeye kadar AI satış yapar — ürün önerir, teklif verir, randevu alır"

#### Adım adım:

```
Adım 5.1: Revenue Agent — Lead Katmanı
├── Yapılacak:
│   ├── Lead qualification script
│   │   ├── Bütçe / termin / ürün ihtiyacı soruları
│   │   ├── AI konuşma akışından otomatik bilgi toplama
│   │   └── Lead score (0-100 + nedenleri)
│   ├── Offer / Appointment
│   │   ├── Internal slot engine (basit takvim API)
│   │   ├── Proposal message templates (teklif + şartlar + geçerlilik süresi)
│   │   └── Follow-up otomasyonu (T+24h teklif hatırlatma)
│   └── Payment link
│       ├── iyzico veya PayTR link oluşturma
│       └── Ödeme geldi → otomatik teyit mesajı
└── Servis: AgentAI genişleme

Adım 5.2: Revenue Agent — Satış Katmanı (fullidea'da EKSİK olan kısım)
├── Yapılacak:
│   ├── Product Recommendation Engine
│   │   ├── Konuşma context'inden ürün/hizmet eşleştirme
│   │   ├── Müşteri geçmişine göre kişisel öneri
│   │   └── "Bunu alanlar şunu da aldı" basit collaborative filtering
│   ├── Bundle / Upsell / Cross-sell Rules
│   │   ├── Tenant tanımlı kurallar (X+Y = %10 indirim)
│   │   ├── Upsell tetikleyicileri (düşük paket → yüksek paket öner)
│   │   └── Cross-sell (ana ürün + aksesuar/hizmet)
│   ├── Margin Awareness
│   │   ├── Ürün marj bilgisi (düşük/orta/yüksek tier)
│   │   ├── Düşük marjlı ürün sorulduğunda yüksek marjlı alternatif sun
│   │   └── İndirim limitleri (agent max %X indirim yapabilir)
│   └── Inventory-Aware Suggestions
│       ├── Stok durumu kontrolü (Integrations'tan)
│       ├── Stokta yoksa alternatif öner
│       └── Düşük stokta "son X adet" urgency mesajı
└── Servis: AgentAI + Integrations genişleme

Adım 5.3: Ürün Kataloğu
├── Yapılacak:
│   ├── Product import (CSV / Trendyol sync)
│   ├── Marj tier atama (düşük/orta/yüksek)
│   ├── Bundle rule tanımlama UI
│   └── Stok sync (marketplace'den otomatik)
└── Servis: Integrations genişleme

Adım 5.4: Abandoned Cart Recovery
├── Yapılacak:
│   ├── Sepet terk tespiti (Trendyol/HB webhook veya sync ile)
│   ├── Trigger kuralları (T+2h ilk hatırlatma, T+24h ikinci)
│   ├── Kişiselleştirilmiş mesaj (sepetteki ürüne özel)
│   ├── Opt-out compliance ("STOP" → otomatik unsubscribe)
│   └── Recovery rate tracking
└── Servis: Integrations + Outbound

Adım 5.5: Sipariş Sonrası Proaktif Satış (YENİ — Senaryo S4)
├── Yapılacak:
│   ├── Teslimat tamamlandı trigger → T+3 gün "Memnun musunuz?"
│   ├── Memnun → cross-sell önerisi (Ürün Kataloğu'ndan)
│   ├── Memnun değil → iade çevirme akışına yönlendir
│   ├── Kişiselleştirilmiş öneri (son satın alma + kategori bazlı)
│   └── Post-purchase conversion tracking
├── Yapılmayacak:
│   ├── Tam recommendation engine (basit rule-based yeterli)
│   └── Otomatik satın alma (link gönder, karar müşteride)
└── Servis: Outbound + AgentAI + Integrations

Adım 5.6: Click-to-WhatsApp Attribution
├── Yapılacak:
│   ├── Meta click id capture (lead source = campaign/adset/ad)
│   ├── Pipeline auto-tagging (label + segment + UTM mapping)
│   └── Attribution dashboard (hangi kampanya ne kadar lead getirdi)
└── Servis: Backend + Dashboard

Adım 5.7: Sağlık Niche Genişleme (YENİ — Senaryo S8, S9, S10)
├── Yapılacak:
│   ├── Tedavi Sonrası Takip Otomasyonu [S8]:
│   │   ├── Tedavi tamamlandı → T+1 gün "Nasıl hissediyorsunuz?"
│   │   ├── T+7 gün kontrol soruları (ağrı, şişlik, vs.)
│   │   ├── T+30 gün "Kontrol randevusu alalım mı?"
│   │   ├── Şikayet varsa → doktora alert (acil/normal sınıflandırma)
│   │   └── Takip compliance tracking (hasta cevapladı mı?)
│   ├── Google Yorum + Referans Motoru [S10]:
│   │   ├── Tedavi başarılı + hasta memnun → yorum rica mesajı
│   │   ├── Google Maps review link gönder
│   │   ├── Referans kodu üret → "Arkadaşınıza %10 indirim"
│   │   ├── Referral tracking (kim kimi getirdi)
│   │   └── Yorum oranı dashboard'da göster (%3 → %15+ hedef)
│   ├── Medikal Turizm Lead Yönetimi [S9]:
│   │   ├── Arapça template desteği (TR/EN/AR)
│   │   ├── Yabancı hasta akışı: fiyat + konaklama + transfer paketi
│   │   ├── Döviz bazlı fiyatlandırma (EUR/USD/GBP)
│   │   ├── Consultation booking (online muayene slot)
│   │   └── Multi-language follow-up otomasyonu
│   └── Gelişmiş Randevu Yönetimi:
│       ├── Bekleme listesi optimizasyonu (iptal → otomatik doldur)
│       ├── Doktor bazlı slot yönetimi (specialist vs genel)
│       ├── Randevu hatırlatma kişiselleştirme (hasta geçmişine göre)
│       └── No-show prediction (veri yeterliyse basit model)
├── Yapılmayacak:
│   ├── HBYS (Hastane Bilgi Yönetim Sistemi) entegrasyonu
│   ├── Tıbbi kayıt yönetimi (compliance riski çok yüksek)
│   └── Otomatik tıbbi tavsiye (sadece bilgi + yönlendirme)
└── Servis: AgentAI + Outbound + Integrations genişleme
```

**DB Tabloları (Phase 5 eklentileri):**
```sql
-- Revenue Agent
leads (id, tenant_id, phone, name, source, score, status, qualification_data_json, created_at, updated_at)
lead_appointments (id, tenant_id, lead_id, slot_start, slot_end, status, created_at)
  -- Not: Phase 4'teki appointments tablosu sağlık niche'i için. Bu lead appointments.
payment_links (id, tenant_id, lead_id, provider, amount, currency, external_link_id, status, created_at)

-- Product Catalog
products (id, tenant_id, name, sku, category, price, margin_tier, stock_qty, status, metadata_json, created_at, updated_at)
bundle_rules (id, tenant_id, trigger_product_id, offer_product_id, rule_type, discount_pct, priority, created_at)
  -- rule_type: 'upsell' | 'cross_sell' | 'bundle'
recommendation_log (id, tenant_id, conversation_id, recommended_product_id, action, created_at)
  -- action: 'shown' | 'accepted' | 'rejected'

-- Abandoned Cart
abandoned_carts (id, tenant_id, customer_phone, cart_items_json, abandoned_at, reminder_count, recovered, created_at)

-- Post-Purchase Proactive Sales (S4)
post_purchase_triggers (id, tenant_id, order_id, customer_phone, trigger_type, scheduled_at, sent_at, response, conversion_product_id, created_at)
  -- trigger_type: 'satisfaction_check' | 'cross_sell' | 'review_request'

-- Sağlık Niche Genişleme (S8, S9, S10)
treatment_followups (id, tenant_id, patient_phone, treatment_type, followup_day, message_sent, patient_responded, complaint_detected, doctor_alerted, created_at)
  -- followup_day: 1, 7, 30
referrals (id, tenant_id, referrer_phone, referee_phone, referral_code, discount_pct, status, created_at)
  -- status: 'generated' | 'shared' | 'used' | 'expired'
review_requests (id, tenant_id, patient_phone, treatment_type, satisfaction_score, review_link_sent, review_posted, platform, created_at)
  -- platform: 'google' | 'trendyol' (e-ticaret de kullanabilir)
medical_tourism_leads (id, tenant_id, patient_phone, patient_country, lang, treatment_interest, accommodation_needed, transfer_needed, budget_currency, status, created_at, updated_at)
```

**Support AI vs Revenue Agent farkı:**

| | Support AI (Phase 1-4) | Revenue Agent (Phase 5) |
|---|---|---|
| Müşteri sorar | Cevap verir | **Proaktif ürün önerir** |
| Ürün bilgisi | Yok | Katalog + stok + marj biliyor |
| Upsell | Yok | "Bu paketi alırsanız %20 tasarruf" |
| Marj | Yok | Yüksek marjlı alternatif sunar |
| Stok | Yok | "Son 3 adet" / alternatif önerir |
| Sepet terk | Yok | Otomatik hatırlatma |
| Lead | Yok | Skorlama + randevu + ödeme |
| Sonuç | **Para toplar** | **Satış yapar** |

**Phase 5 sonunda elimizde:**
- 40-60 müşteri e-ticaret + 8-15 klinik sağlık
- MRR 300.000-500.000 TL (e-ticaret) + 60.000-150.000 TL (sağlık)
- Revenue Agent aktif satış yapıyor
- Abandoned cart recovery çalışıyor
- Post-purchase proaktif satış aktif
- Attribution tracking ile reklam ROI ölçülüyor
- Sağlık niche'i tam çalışıyor (tedavi takibi + yorum + referans + medikal turizm)
- 3 dil destekleniyor (TR/EN/AR)
- Premium tier ($$$) oluşturulabilir

**Pricing tier önerisi (Phase 5'ten itibaren — Dual Niche):**

**E-ticaret Fiyatlandırma:**

| Tier | İçerik | Fiyat |
|------|--------|-------|
| Starter | Auto-resolution (kargo/iade) + Agent Assist | 3.000 TL/ay |
| Growth | + Knowledge + Outbound + İade Çevirme | 7.500 TL/ay |
| Pro | + Revenue Agent + Abandoned Cart + Attribution | 12.000 TL/ay |
| Enterprise | + SSO/2FA/Audit + Dedicated support | 15.000+ TL/ay |

**Sağlık Fiyatlandırma:**

| Tier | İçerik | Fiyat |
|------|--------|-------|
| Klinik | Fiyat→Randevu + No-Show Önleme + Agent Assist | 7.500 TL/ay |
| Klinik Pro | + Tedavi Takibi + Yorum Motoru + Referans | 15.000 TL/ay |
| Medikal Turizm | + Multi-language + Konaklama/Transfer paketi | 25.000+ TL/ay |

---

### Phase 6 — Operasyon + Analytics + Ölçek (Hafta 37-48)

> *"You can't improve what you don't measure."*

**Müşteri ne kazanıyor:** "Ekip performansını ölç, büyürken kontrolü kaybet"
**Revenue milestone:** 60-100 müşteri, MRR 500.000-1.000.000 TL
**Satış dili:** "Dashboard'dan her şeyi izle: yanıt süreleri, temsilci kalitesi, gelir etkisi"

#### Adım adım:

```
Adım 6.1: SLA Tracker
├── Response time / resolve time ölçümü
├── Tenant bazlı SLA hedefleri (örn: 2dk ilk yanıt)
├── Breach alerts (mail/webhook)
└── SLA dashboard

Adım 6.2: QA Scoring
├── AI destekli temsilci değerlendirme
├── Script compliance skoru
├── Sentiment analizi
├── Resolution quality skoru
└── Manager review queue

Adım 6.3: Conversation Mining
├── Win/loss phrase analizi (hangi cümleler satış getiriyor)
├── Top complaint drivers (en çok ne şikayet ediliyor)
├── Top conversion patterns (satışı kapatan kalıplar)
└── Trend analizi (haftalık/aylık değişim)

Adım 6.4: Knowledge Gap Report
├── Son 7/30 gün top 50 unanswered intents
├── "Doc ekle" aksiyonu (direkt knowledge'a yönlendir)
└── AI accuracy trend (zaman içinde iyileşme)

Adım 6.5: Revenue Attribution Dashboard
├── Kanal bazlı gelir (WhatsApp / Instagram / Web)
├── Kampanya bazlı ROI
├── Agent bazlı satış performansı
├── AI vs Human karşılaştırması
└── Abandoned cart recovery rate ve değeri
```

---

### Phase 7 — Genişleme: Omnichannel + Global (Hafta 49+)

> *"Dominate one channel, then expand."*

**Müşteri ne kazanıyor:** "Tüm kanalları tek yerden yönet"
**Revenue milestone:** 100+ müşteri, MRR 1.000.000+ TL

```
Genişleme alanları (müşteri talebine göre önceliklendir):
├── Instagram DM entegrasyonu
├── Facebook Messenger
├── SMS fallback (WhatsApp ulaşmadığında)
├── Shopify / WooCommerce (global müşteri talebi varsa)
├── Voice message transcription (Whisper API)
├── Predictive analytics / churn prediction (yeterli veri biriktiyse)
└── Compliance scanner (KVKK/GDPR otomatik tarama)
```
