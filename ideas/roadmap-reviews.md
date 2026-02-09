# Invekto — Uzman Review'ları

> Ana dosya: [roadmap.md](roadmap.md)
> Bu dosya: 4 uzman perspektifinden review + aksiyonlar
> Son güncelleme: 2026-02-08
> Referans: [whatisinvekto.md](whatisinvekto.md) — Invekto mevcut ürün envanteri

---

## Bağlam

Bu dosya 4 uzman perspektifinden review ve aksiyonları içerir.

**Mevcut durum:** Invekto 50+ aktif müşteriye sahip, 50-200K TL MRR üreten çalışan bir üründür. Multi-tenant auth, 7 kanal Unified Inbox, gelişmiş routing, CRM, VOIP, kapsamlı raporlama mevcut. Fiyatlandırma: $25/agent + $40/kanal. Mevcut müşteriler ağırlıklı sağlık klinikleri + otel/turizm.

**Ana soru:** "Çalışan ürüne ne ekleyerek büyüme ivmesi yaratırız?"

---

## 1. April Dunford (Positioning Gözüyle)

### Orijinal Analiz

**Ne doğru:**
- Niche seçimi net: Trendyol/HB satıcıları → sonra sağlık
- Avatar (Mehmet / Dr. Ayşe) iyi yazılmış
- ROI hesapları somut
- Persona seti gerçekçi (E1/E2 ve klinik)
- 75 senaryo saha gerçeklerinden geliyor
- Capability mapping disiplinli yapılmış

**Kritik problem — KATEGORİ YOK:**

Roadmap'te Invekto aynı anda 4 kategori:

| # | Kategori | Nerede geçiyor |
|---|----------|----------------|
| 1 | WhatsApp CRM | Product Story |
| 2 | AI Agent platformu | Phase 3 AgentAI |
| 3 | Revenue OS | Phase 5 Revenue Agent |
| 4 | E-ticaret otomasyon aracı | Trendyol entegrasyonları |

**"Bu dört ürün ayrı ayrı satılır. Sen kategori yaratmıyorsun, 4 kategoriye aynı anda girmeye çalışıyorsun."**

**Müşteri anlayamıyor: "Invekto hangi rafta duruyor?"**

### Yeniden Değerlendirme (Mevcut Duruma Göre)

**Gerçeklik Dunford'un teşhisini GÜÇLENDIRDI — ama çözümü DEĞİŞTİRDİ:**

Dunford'un orijinal önerisi:
> *"Trendyol ve Hepsiburada satıcıları için WhatsApp üzerinden sipariş sonrası yükü otomatik kapatan AI."*

**Problem:** Invekto'nun 50+ müşterisinin çoğu **e-ticaret değil, sağlık + otel/turizm**. Wedge positioning'i sadece e-ticarete kilitlemek **mevcut müşteri tabanını görmezden gelmek** demek.

**Dunford çözümü — Şemsiye + Niche:**

| Seviye | Positioning |
|--------|-------------|
| **Üst şemsiye** | "WhatsApp'tan gelen müşteri mesajlarını AI ile otomatik yöneten iş asistanı" |
| **E-ticaret niche** | "Kargo ve iade sorularını otomatik çözer, temsilci maliyetini düşürür" |
| **Diş klinikleri niche** | "Fiyat sorularını randevuya çevirir, no-show'u %60 azaltır" |
| **Estetik klinikleri niche** | "Lead'leri hastaya dönüştürür, medikal turizmi ölçekler" |

**Dunford'un zorlayacağı soru hâlâ geçerli:**
> "Bu 3 niche'e aynı anda çıkmak = 3 ayrı satış mesajı, 3 ayrı landing page, 3 ayrı demo. Bunu tek kişiyle yapabilir misin?"

**Q'nun cevabı (2026-02-08):** Tek platform, 3 ayrı offer. Ortak altyapı %95+ aynı. Reklam ve web ayrı, ürün aynı. Risk mitigasyonu: Phase 0'da hangi niche'te ilgi yok → o ertelenir.

**Aksiyon durumu:**

| # | Aksiyon | Durum |
|---|---------|-------|
| 1 | Positioning tek cümle | ✅ Üst şemsiye + 3 niche tanımlı |
| 2 | Landing page hero | ⬜ 3 ayrı landing page tasarlanacak (/sellers, /dental, /clinics) |
| 3 | Feature'ları wedge'e bağla | ✅ 3 niche bazlı feature→sonuç eşleşmesi var |
| 4 | "WhatsApp CRM" → "AI İş Asistanı" | ✅ Positioning dili güncellendi |
| 5 | Satış deck güncelleme | ⬜ 3 niche bazlı satış materyalleri |

**Dunford'a sorulacak soru:**
> "50+ müşterili çalışan ürün var ama kimse Invekto'yu 'AI platformu' olarak tanımıyor. Mevcut müşteriler 'WhatsApp CRM' olarak biliyor. AI eklediğimizde re-positioning nasıl yapılır? Mevcut müşteriyi korkutmadan nasıl pivot edilir?"

---

## 2. Jason Lemkin (SaaS Ölçek Gözüyle)

### Orijinal Analiz

**Ne doğru:**
- Phase 0'da satışa çıkma fikri çok iyi
- Revenue-first yaklaşım mantıklı
- Phase bazlı genişleme gerçekçi
- Security Phase-1'e alınmış ✅
- Audit + tenant izolasyonu var
- WhatsApp policy farkındalığı mevcut

**Orijinal kritik problemler:**
1. Auth Phase 4 = çok geç
2. Core retention metric yok
3. Customer onboarding akışı yok
4. Expansion modeli tanımlı değil
5. Timeline gerçekçi değil

### Yeniden Değerlendirme (Mevcut Duruma Göre)

**Lemkin'in 5 kritik probleminden 3'ü ÇÖZÜLDÜ — geriye 2 kaldı:**

#### ✅ ÇÖZÜLMÜŞ — Auth artık geç değil

Orijinal teşhis: "Auth Phase 4 = çok geç, kurumsal müşteri SSO sorar"

**Gerçeklik:** Auth **zaten var!** Multi-tenant, firma+user+parola, 2 rol (User, Supervisor). SSO/OAuth = Phase 4'te kurumsal müşteri talebi gelince. Audit = Phase 4. **Lemkin'in korkusu geçersiz** — Invekto zaten 50+ müşteriyle çalışıyor, hiçbiri SSO'dan churn etmedi.

**Yeni risk:** SSO talep eden kurumsal müşteri gelirse hızlı tepki. Kural: Phase 3'te ≥3 "SSO var mı?" sorusu → SSO'yu çek.

#### ✅ ÇÖZÜLMÜŞ — Customer onboarding çalışıyor

Orijinal teşhis: "Müşteri ilk 30 dakikada ne kuruyor? İlk değer ne zaman geliyor?"

**Gerçeklik:** Invekto **white-glove onboarding** yapıyor. Q ekibi müşterinin kanallarını bağlıyor, eğitim veriyor. İlk değer = **ilk mesajın Unified Inbox'ta görünmesi**. 50+ müşteri bu süreçten başarıyla geçti.

**Yeni soru:** AI/otomasyon özelliği geldiğinde onboarding süreci nasıl değişecek?

| Adım | Bugünkü Onboarding | AI Eklendikten Sonra |
|------|--------------------|--------------------|
| 1 | Kanal bağlama | Kanal bağlama |
| 2 | Agent ekleme + routing ayarı | Agent ekleme + routing ayarı |
| 3 | Welcome mesajı yazma | Welcome mesajı yazma |
| 4 | Eğitim + canlıya alma | **+ Chatbot flow ayarlama** |
| 5 | — | **+ AI assist açma** |
| 6 | — | **+ İlk broadcast** |
| 7 | — | **AHA moment: "Gerçekten otomatik cevapladı!"** |

#### ✅ ÇÖZÜLMÜŞ — Expansion modeli belirli

Orijinal teşhis: "Revenue driver'lar eksik — seat pricing? volume? credits?"

**Gerçeklik:** Mevcut fiyatlandırma çalışıyor: $25/agent + $40/kanal. Artı 5 yeni driver planlandı:

| Driver | Phase | Açıklama |
|--------|-------|----------|
| Agent Seat | **Mevcut** | $25/agent — çalışıyor |
| Channel Fee | **Mevcut** | $40/kanal — çalışıyor |
| AI Credits | Phase 1+ | AI otomatik cevap kullanımı (paket bazlı) |
| Automation Tier | Phase 1+ | Chatbot/otomasyon seviyesine göre plan |
| Broadcast Volume | Phase 1+ | Toplu mesaj gönderim limiti + aşım |
| Conversation Volume | Phase 3+ | Aylık konuşma limiti + aşım ücreti |
| Integration Count | Phase 2+ | Entegrasyon sayısına göre tier |

**Lemkin'in kuralı yine geçerli:** Expansion revenue = net churn'ü negatife çeviren mekanizma. Mevcut müşterilere AI upsell = net negatif churn potansiyeli.

#### ⚠️ HÂLÂ AÇIK — Core retention metric

Orijinal teşhis: "MRR yazıyor ama activation tanımı, logo churn hedefi, 'customer is live' ne demek belli değil."

**Mevcut durumu:** SaaS metrikleri kısmen tanımlandı ama henüz **ölçülmüyor**:

| Metrik | Tanım | Bugün Ölçülüyor mu? |
|--------|-------|---------------------|
| TTFAR (Time to First Auto-Resolution) | AI ilk otomatik cevabına kadar geçen süre | ❌ AI yok henüz |
| Weekly Deflection % | AI'ın otomatik çözdüğü mesaj oranı | ❌ AI yok henüz |
| 30-Day Logo Retention | İlk 30 gün churn oranı | ⚠️ Ölçülebilir ama raporlanmıyor |
| Activation | "İlk mesajın Inbox'ta görünmesi" | ⚠️ Tanımlı, ölçülmüyor |
| Net Logo Churn | Aylık müşteri kaybı | ⚠️ İzleniyor ama hedef yok |

**Aksiyon:** Phase 0'da mevcut müşteri retention/churn verisi çıkarılmalı. Phase 1'de AI metrikleri (TTFAR, deflection) başlamalı.

#### ⚠️ HÂLÂ AÇIK — Timeline

Orijinal teşhis: "90 gün gerçekçi değil. Auth+Audit+Knowledge+AgentAI+Trendyol tek kişiyle minimum 5-6 ay."

**Mevcut durumu:** Auth, CRM, Inbox, Routing **zaten yapılmış** — bu dev yükü düşüyor. Ama Phase 1 hâlâ ağır:

| Phase 1 Kapsamı | Tahmini Süre |
|-----------------|--------------|
| Invekto.Automation (chatbot engine) | 3-4 hafta |
| Invekto.AgentAI (agent assist) | 2-3 hafta |
| Invekto.Outbound (broadcast) | 2-3 hafta |
| Invekto ↔ InvektoServis entegrasyonu | 2-3 hafta |
| Test + stabilizasyon | 1-2 hafta |
| **Toplam Phase 1** | **10-15 hafta** |

> Roadmap "3-8 hafta" diyor. Tek kişiyle **10-15 hafta** daha gerçekçi.
> **Lemkin'in uyarısı hâlâ geçerli:** Timeline'ı Q'ya karşı dürüst tut.

**Aksiyon durumu:**

| # | Aksiyon | Durum |
|---|---------|-------|
| 1 | Core SaaS Metrics kutusu | ✅ Korunuyor — Phase 1'de AI metrikleri başlayacak |
| 2 | Auth zamanlama | ✅ Auth zaten mevcut, SSO Phase 4'te talep gelirse |
| 3 | Onboarding flow | ✅ White-glove mevcut, AI onboarding planlandı |
| 4 | Expansion model | ✅ 7 driver tanımlı |
| 5 | Timeline revizyon | ⚠️ Mevcut yük düştü ama Phase 1 hâlâ 10-15 hafta |
| 6 | Mevcut churn/retention veri çıkarma | ⬜ Phase 0'da yapılmalı |
| 7 | AI metrik dashboard'u | ⬜ Phase 1'de TTFAR + deflection % ölçümü |

**Lemkin'e sorulacak soru:**
> "50-200K TL MRR yapan, 50+ müşterili ürünüm var. AI/otomasyon ekleyince mevcut müşterilere upsell yapabilir miyim yoksa yeni paket mi satmalıyım? Grandfathering stratejisi ne olmalı?"

---

## 3. Lenny Rachitsky (Product Gözüyle)

### Orijinal Analiz

**Ne doğru:**
- Senaryolar (S1–S10) çok güçlü
- Pain → feature eşleşmesi iyi
- Mehmet avatarı gerçek
- Scenario → capability mapping çok iyi yapılmış

**Orijinal kritik problemler:**
1. PRIMARY USER FLOW yok
2. AI öğrenme eğrisi yok

### Yeniden Değerlendirme (Mevcut Duruma Göre)

**Lenny'nin "user flow yok" teşhisi DÖNÜŞTÜ:**

Lenny'nin orijinal korkusu:
> "Mehmet Invekto'ya girer → sonra ne olur?" tanımlı değil.

**Gerçeklik:** 50+ müşteri Invekto'yu her gün kullanıyor. User flow **var ve çalışıyor:**

```
Bugünkü flow (50+ müşteri bunu yapıyor):
1. Agent giriş yapar
2. Inbox'ta bekleyen mesajları görür (7 kanal)
3. Mesaja tıklar → sohbet açılır
4. Geçmişi görür, cevap yazar veya template kullanır
5. Gerekirse transfer eder veya etiketler
6. Sohbeti kapatır (manuel)
7. Raporlarda performansını görür
```

**Lenny'nin şimdi sorması gereken:**
> "Tamam mevcut flow çalışıyor. Ama AI eklendiğinde user flow nasıl DEĞİŞECEK? Agent AI'yı nasıl keşfedecek, nasıl güvenecek, nasıl benimseyecek?"

#### Kritik: AI Discovery Flow (YENİ)

Mevcut 50+ müşteriye AI özelliği geldiğinde:

```
MEVCUT AGENT FLOW + AI KATMANI:

1. Agent giriş yapar
2. Inbox'ta bekleyen mesajları görür
3. Mesaja tıklar → sohbet açılır
4. ⚡ YENİ: AI cevap önerisi görünür (sağ panelde)
   → Agent okur → kabul eder / düzenler / reddeder
5. ⚡ YENİ: Bazı mesajlar "AI otomatik cevapladı" etiketi ile gelir
   → Agent sadece kontrol eder
6. ⚡ YENİ: Chatbot konuşmalarında "devral" butonu
   → AI çözemediğinde agent devralır
7. Sohbeti kapatır
8. ⚡ YENİ: Dashboard'da "AI kurtardığı saat" metriği
```

#### AI Güven Eğrisi (Trust Ladder)

| Dönem | Agent Davranışı | Sistem Davranışı |
|-------|----------------|------------------|
| **Hafta 1** | AI önerisini okuyor, kendi yazıyor | Sadece öneri (asla otomatik gönderme) |
| **Hafta 2** | AI önerisini kabul etmeye başlıyor | Kabul oranı ölçülüyor |
| **Hafta 3-4** | Agent AI'ya güveniyor, bazı soruları AI'ya bırakıyor | "Otomatik cevapla" özelliği açılıyor (agent izniyle) |
| **Ay 2+** | Agent supervisory role'e geçiyor | AI çoğu soruyu otomatik çözüyor, agent sadece kontrol |

**AI Yanlış Cevap Protokolü:**
1. Agent AI önerisini reddeder → feedback "yanlış" olarak kaydedilir
2. 3 aynı tip yanlış → o intent kategorisi otomatik moda alınmaz
3. Supervisor "override all" yapabilir → o müşteri için AI kapanır
4. Dashboard'da "AI accuracy %" gösterilir

#### User Flow durumu

| Soru | Cevap |
|------|-------|
| İlk kullanıcı 1. gün ne yapıyor? | ✅ White-glove onboarding ile kanal bağlıyor, ilk mesajı görüyor |
| İlk değer anı (aha moment) neresi? | ✅ İlk mesajın Inbox'ta görünmesi. AI sonrası: İlk AI önerisinin doğru çıkması |
| 7 gün sonra ne görüyor? | ✅ Mesaj hacmi, routing çalışıyor. AI sonrası: AI accuracy %'si yükseliyor |
| 30 gün sonra neden kalıyor? | ✅ Ekip verimliliği artıyor. AI sonrası: "X saat AI kurtardı" metriği |

**Aksiyon durumu:**

| # | Aksiyon | Durum |
|---|---------|-------|
| 1 | User First-Value Flow | ✅ AI eklenmesi için yeni flow tanımlandı |
| 2 | Day 1-7-30 akışı | ✅ Mevcut akış + AI katmanı |
| 3 | Aha moment | ✅ 3 niche-özel aha (mevcut + AI) |
| 4 | AI güven eğrisi | ✅ Trust Ladder + yanlış cevap protokolü |
| 5 | Human-in-the-loop flow | ✅ Agent override, 3 yanlış = disable |
| 6 | Product layer UI mockup | ⬜ AI önerisi ekranı, chatbot builder UI |
| 7 | AI discovery flow (mevcut müşteri) | ⬜ 50+ müşteriye AI nasıl tanıtılacak? |

**Lenny'ye sorulacak soru:**
> "Mevcut müşteriler Invekto'yu 'mesajlaşma CRM'i' olarak kullanıyor. AI eklediğimizde 'AI platformu'na dönüşecek. Bu geçişi müşteri nasıl yaşamalı? Kademeli mi, büyük patlama mı? Beta group mu?"

---

## 4. Alex Hormozi (Offer/Pricing Gözüyle)

### Orijinal Analiz

**Ne doğru:**
- Teknik ürün güçlü
- Türkiye senaryoları gerçek
- ROI hesaplamaları var

**Ana problem — OFFER YOK:**

**"Bu kadar ağır sistem KOBİ'ye satılmaz."**

Şu an sattığın şeyler: WhatsApp CRM, AI agent, Trendyol entegrasyonu.
→ Bunlar feature. OFFER değil.

### Yeniden Değerlendirme (Mevcut Duruma Göre)

**Hormozi'nin teşhisi HÂLÂ GEÇERLİ — ama çözümü DEĞİŞTİ:**

Orijinal problem: "Offer yok, feature satıyorsun"

**Gerçeklik:** Invekto **zaten satılıyor** — ama Hormozi haklı: **feature satılıyor, sonuç satılmıyor**.

| Bugünkü Satış Dili | Hormozi Dili |
|--------------------|----|
| "7 kanal tek inbox" | "Mesaj kaçırma oranınız %0'a düşer" |
| "Akıllı routing var" | "Müşteri 30 sn'de doğru kişiye bağlanır" |
| "VOIP entegrasyonu" | "Tek ekrandan hem yaz hem ara" |
| "$25/agent" | "Temsilci başına günlük maliyetiniz sadece 25 TL" |

**Mevcut müşteriler feature için aldı — ama KALMASI İÇİN sonuç görmeli.**

#### 3 Grand Slam Offer (Tanımlandı)

**OFFER 1: Invekto for Sellers (E-ticaret — Yeni Müşteri Kazanım)**

| Bileşen | Detay |
|---------|-------|
| **Sonuç vaadi** | "Kargo/iade sorularının %50'sini otomatik cevapla" |
| **Karar verici** | Marketplace satıcısı (Mehmet) |
| **Fiyat** | 3.000-5.000 TL/ay |
| **Garanti** | 30 günde %50 oto-cevap yoksa 2. ay ücretsiz |
| **Risk reversal** | "Biz kuruyoruz, siz izliyorsunuz" |
| **Kıtlık** | İlk 10 Trendyol satıcısına özel lansman fiyatı |
| **Niche özel** | C11 (Trendyol/HB API) + C7 (Knowledge) |

**OFFER 2: Invekto for Dental (Diş — Mevcut Müşteri Güçlendirme)**

| Bileşen | Detay |
|---------|-------|
| **Sonuç vaadi** | "Fiyat sorularını randevuya çevir, no-show'u %60 azalt" |
| **Karar verici** | Klinik sahibi (Dr. Burak) |
| **Fiyat** | 7.500 TL/ay |
| **Garanti** | 30 günde no-show düşmezse 2. ay ücretsiz |
| **Risk reversal** | "Mevcut sisteminiz aynen çalışır, AI katmanı üstüne biner" |
| **Kıtlık** | İlk 5 kliniğe özel fiyat |
| **Niche özel** | Randevu motoru + No-show önleme + C7 (Knowledge) + C5/C6 (KVKK) |

**OFFER 3: Invekto for Clinics (Estetik — Mevcut Müşteri + Yeni)**

| Bileşen | Detay |
|---------|-------|
| **Sonuç vaadi** | "Lead'leri hastaya dönüştür, medikal turizmi ölçekle" |
| **Karar verici** | Klinik sahibi (Dr. Selin) |
| **Fiyat** | 15.000-25.000 TL/ay |
| **Garanti** | 30 günde randevu dönüşümü artmazsa 2. ay ücretsiz |
| **Risk reversal** | "Mevcut iletişiminiz kesintisiz devam eder" |
| **Kıtlık** | İlk 5 kliniğe özel fiyat |
| **Niche özel** | C10 (Revenue) + C12 (Ads Attribution) + C4 (Reporting) + Multi-lang |

#### Hormozi Değer Denklemi

```
Değer = (Hayalin Sonucu × Gerçekleşme İhtimali) / (Zaman Gecikmesi × Harcanan Efor)
```

**Mevcut Invekto (AI öncesi):**
```
Hayalin Sonucu: Tüm mesajlar tek yerde          → ORTA
Gerçekleşme: Garanti çalışıyor                   → YÜKSEK
Zaman: White-glove onboarding 1-2 gün            → DÜŞÜK
Efor: Q ekibi kuruyor                            → DÜŞÜK

Değer = (Orta × Yüksek) / (Düşük × Düşük) = İYİ
```

**Invekto + AI Otomasyon (Phase 1 sonrası):**
```
Hayalin Sonucu: Mesajların yarısı otomatik çözülür → ÇOK YÜKSEK
Gerçekleşme: 30 gün garanti + case study            → YÜKSEK
Zaman: 1 hafta kurulum                              → DÜŞÜK
Efor: Biz kuruyoruz, siz izliyorsunuz               → DÜŞÜK

Değer = (Çok Yüksek × Yüksek) / (Düşük × Düşük) = MUAZZAM
```

> **Hormozi bunu beğenirdi:** Mevcut ürüne AI eklemek = küçük değişiklik, büyük değer artışı. Sıfırdan ürün satmaktan çok daha kolay.

#### Mevcut Müşteriye Upsell

Hormozi'nin soracağı:
> "50+ müşteriniz $25/agent ödüyor. AI eklediğinizde +$X isteyeceksiniz. Mevcut müşteri neden kabul etsin?"

**Cevap stratejisi:**
1. **Beta invitation:** "İlk 10 müşteriye AI ücretsiz" → ürün kullandırılır, case study oluşur
2. **Kademeli geçiş:** İlk ay ücretsiz, sonra AI tier eklenir
3. **Sonuç göster:** "Bu ay AI 120 mesajınızı otomatik cevapladı → $X/ay ödeyerek bu hizmeti koruyun"
4. **Grandfathering:** Mevcut plan + AI = eski fiyat + AI farkı (mevcut fiyat korunur)

**Aksiyon durumu:**

| # | Aksiyon | Durum |
|---|---------|-------|
| 1 | Offer yapısı tanımla | ✅ 3 Grand Slam Offer |
| 2 | Pricing tiers belirle | ⚠️ Mevcut fiyat var, AI tier planlandı ama kesinleşmedi |
| 3 | Risk reversal mekanizması | ✅ 3 niche-özel garanti |
| 4 | "Bugün al" incentive | ✅ Kıtlık (ilk 10/5 müşteri) |
| 5 | Feature → outcome dil değişikliği | ✅ Niche bazlı sonuç dili |
| 6 | Mevcut müşteriye upsell stratejisi | ⬜ Beta + kademeli geçiş + grandfathering |
| 7 | AI pricing A/B testi | ⬜ 3 farklı AI fiyat noktası test edilmeli |

**Hormozi'ye sorulacak soru:**
> "Mevcut müşteriler $25/agent + $40/kanal ödüyor. AI eklediğimde fiyatı artırmam lazım ama churn riskini alamam. En iyi upsell mekanizması ne? Value-based pricing mi, usage-based mi?"

---

## Q KARARI: 3 NİCHE PARALEL GİRİŞ (2026-02-08)

> **Karar:** Q, 4 uzmanın "önce 1 niche kanıtla" önerisine rağmen 3 niche'e paralel girmeye karar verdi.
>
> **Gerekçe:**
> - Ortak altyapı %95 aynı (C1+C2+C3+C8 tüm sektörlerde)
> - Türkiye pazarı küçük, tek niche'te tavan düşük
> - Sağlık ARPU 3-5x daha yüksek
> - Reklam ve web siteleri sektör bazlı ayrılacak
> - Sağlık klinikleri + otel ZATEN müşteri. Ertelemek = mevcut müşteriyi ihmal etmek.
>
> **Yapı:** Tek platform (Invekto) + 3 ayrı offer (Sellers / Dental / Clinics)
>
> **Risk mitigasyonu:**
> - Phase 0'da 3 niche'te de 10'ar görüşme yapılır
> - Hangisinde 0 ilgi → o niche ertelenir (pivot değil, erteleme)
> - Ortak altyapı tek codebase — sektör farkı = config, kod değil
>
> **Etki:** Tüm dokümanlar (roadmap.md, roadmap-phases.md, roadmap-scenarios.md, whatisinvekto.md) 3 niche paralel giriş kararına göre hizalanmıştır. Phase 0-2 paralel validasyon + satış + ölçekleme içerir.

---

## GENEL SONUÇ (4 Uzman Konsensüs)

### ✅ Güçlü Yanlar:
- Teknik olarak güçlü
- Türkiye senaryoları gerçek
- AI vizyonu doğru
- Security sırası mükemmel
- Capability mapping disiplinli
- Persona seti gerçekçi
- Çalışan ürün + 50+ müşteri + mevcut gelir = güçlü konum

### Temel Alanların Durumu:

| # | Alan | Durum | Açıklama |
|---|------|-------|----------|
| 1 | Positioning | ✅ **Tanımlı** | Üst şemsiye + 3 niche positioning tanımlı |
| 2 | Ana ürün hikayesi | ✅ **Mevcut** | 50+ müşterili çalışan ürün = en güçlü hikaye |
| 3 | Kullanıcı yolculuğu | ✅ **Çalışıyor** | White-glove onboarding çalışıyor + AI flow planlandı |
| 4 | SaaS pricing motion | ⚠️ **Kısmen** | Mevcut pricing çalışıyor, AI pricing planlandı ama kesinleşmedi |
| 5 | Offer yapısı | ✅ **Tanımlı** | 3 Grand Slam Offer + Hormozi kuralı |
| 6 | Tek kişi kapasitesi | ⚠️ **Risk** | Mevcut yük düştü ama Phase 1 hâlâ 10-15 hafta |

### Açık Eksikler:

| # | Yeni Eksik | Açıklama | Hangi Uzman |
|---|------------|----------|-------------|
| N1 | Re-positioning stratejisi | Mevcut müşteri "CRM" biliyor, "AI platformu"na geçiş nasıl? | Dunford |
| N2 | Mevcut churn/retention verisi | 50+ müşterinin churn datası çıkarılmalı | Lemkin |
| N3 | AI metrik dashboard'u | TTFAR + deflection % ölçüm sistemi | Lemkin |
| N4 | AI discovery flow | 50+ müşteriye AI nasıl tanıtılacak? | Lenny |
| N5 | AI pricing model | Usage-based mi, tier-based mi, value-based mi? | Hormozi |
| N6 | Mevcut müşteriye upsell stratejisi | Beta → kademeli → grandfathering | Hormozi |
| N7 | UI mockup'lar | AI öneri ekranı, chatbot builder, broadcast UI | Lenny |

---

## TEK KRİTİK AKSİYON

### Tek aksiyon:
> **"Mevcut 50+ müşteriye AI'ı nasıl tanıtır, kullandırır, ücretlendirir, ve bunu yeni müşteri kazanım argümanına çeviririz?"**

Bu tek soru 4 uzmanın tüm açık aksiyonlarını kapsar:
- **Dunford:** Re-positioning (CRM → AI platformu) yapılacak
- **Lemkin:** Retention + upsell metrikleri izlenecek
- **Lenny:** AI discovery + trust flow tasarlanacak
- **Hormozi:** Upsell offer + AI pricing belirlenecek

### Önerilen cümle:

> **"Invekto helps businesses automatically manage WhatsApp conversations and 6 more channels using AI — already trusted by 50+ companies."**

Bu cümle:
- ✅ Kim için → Businesses (geniş, 3 niche dahil)
- ✅ Ne yapıyor → Automatically manage conversations
- ✅ Nasıl → Using AI
- ✅ Güven → Already trusted by 50+ companies
- ✅ Kanal → WhatsApp + 6 more

**Türkçe versiyonu:**
> **"Invekto, WhatsApp ve 6 kanaldan gelen müşteri mesajlarını AI ile otomatik yönetir — 50+ işletme zaten güveniyor."**

**Bu cümle her yerde tutarlı olmalı:**
- Landing page hero
- LinkedIn bio
- Satış pitch ilk cümle
- Demo başlangıcı
- Email signature

---

## Özet: 4 Uzmanın Verdikleri

| Uzman | Mevcut Durum | Kalan Aksiyon |
|-------|-------------|---------------|
| **Dunford** | ✅ 3 niche positioning tanımlı | Re-positioning stratejisi + 3 landing page |
| **Lemkin** | ✅ Auth mevcut, expansion tanımlı | Churn verisi çıkar + AI metrikleri + timeline |
| **Lenny** | ✅ Mevcut flow çalışıyor | AI discovery flow + UI mockup |
| **Hormozi** | ✅ 3 Grand Slam Offer tanımlı | AI pricing + mevcut müşteri upsell |

---

## Toplam Aksiyon Listesi (Öncelik Sıralı)

### 🔴 CRITICAL (Phase 0-1 öncesi zorunlu):

1. ⬜ **Mevcut müşteri churn/retention verisi çıkar** — Kaç müşteri kaybedildi, neden? (Lemkin)
2. ⬜ **AI pricing model belirle** — Usage-based / tier-based / value-based? (Hormozi)
3. ⬜ **Mevcut müşteriye AI tanıtım stratejisi** — Beta group seç, ücretsiz kullandır (Lenny + Hormozi)
4. ⬜ **Phase 1 timeline'ı gerçekçi yap** — 10-15 hafta, sprint planı (Lemkin)

### 🟠 HIGH (Phase 1 sırasında):

5. ⬜ **AI discovery flow tasarla** — Mevcut agent AI'yı nasıl keşfedecek? (Lenny)
6. ⬜ **3 niche landing page tasarla** — /sellers, /dental, /clinics (Dunford)
7. ⬜ **Upsell stratejisi kesinleştir** — Beta → kademeli → grandfathering (Hormozi)
8. ⬜ **AI metrik dashboard'u** — TTFAR, deflection %, accuracy % (Lemkin)

### 🟡 MEDIUM (Phase 2 öncesi):

9. ⬜ **Product layer UI mockup** — AI öneri ekranı, chatbot builder, broadcast UI (Lenny)
10. ⬜ **Satış deck güncelleme** — 3 niche bazlı satış materyalleri (Dunford)
11. ⬜ **Pricing A/B testi** — 3 farklı AI fiyat noktası validasyonu (Hormozi)
12. ⬜ **Re-positioning komünikasyonu** — Mevcut müşteriye "artık AI de var" nasıl duyurulur (Dunford)

### ✅ TAMAMLANDI:

13. ✅ Tek cümle positioning yazıldı + üst şemsiye (3 niche) eklendi
14. ✅ Core SaaS Metrics tanımlandı (TTFAR, Deflection %, Retention, Activation, Churn)
15. ✅ User First-Value Flow eklendi — 3 niche için ayrı ayrı + AI katmanı
16. ✅ Aha moment sabitlendi — 3 niche için ayrı (e-ticaret/diş/estetik)
17. ✅ 3 Niche paralel giriş kararı alındı + dokümanlar hizalandı
18. ✅ 3 ayrı Grand Slam Offer tasarlandı (Sellers / Dental / Clinics)
19. ✅ 3 niche Phase 0-2 paralel validasyon + satış adımları eklendi
20. ✅ 75 senaryo ortak capability analizi tamamlandı (C1/C2/C3/C8 = %95+ ortak)
21. ✅ AI güven eğrisi tanımlandı (Trust Ladder + yanlış cevap protokolü)
22. ✅ Expansion model tanımlandı (7 revenue driver)
23. ✅ Offer stratejisi tamamlandı (Hormozi kuralı + sonuç dili + risk reversal)
24. ✅ Auth sorunu çözüldü (mevcut auth + SSO Phase 4'te)
25. ✅ Onboarding flow çalışıyor (white-glove + AI onboarding planlandı)

---

## Son Not

> **"Bu roadmap çalışan bir ürüne AI katmanı ekleme planı — hem mühendislik hem satış tarafı düşünülmüş."**

**Mevcut durum özeti:**

| Alan | Durum |
|------|-------|
| Backend mimari | ✅ Net + mevcut gerçeklik |
| Senaryo mapping | ✅ Net |
| Phase planlama | ✅ Net + mevcut müşteri bazlı |
| Positioning | ✅ Üst şemsiye + 3 niche |
| Offer | ✅ 3 Grand Slam Offer |
| Onboarding | ✅ White-glove çalışıyor + AI planı |
| AI güven eğrisi | ✅ Trust Ladder tanımlı |
| Expansion model | ✅ 7 driver tanımlı |
| User flow | ✅ 50+ müşteri kullanıyor |
| Pricing | ⚠️ Mevcut var, AI pricing açık |

**Kalan ana riskler:**
1. **Phase 1 timeline gerçekçiliği** — 10-15 hafta, tek kişi
2. **AI pricing churn riski** — Fiyat artışı mevcut müşteriyi korkutabilir
3. **3 niche paralel yönetimi** — Pazarlama/satış kapasitesi yeterli mi?
4. **InvektoServis ↔ Invekto entegrasyon karmaşıklığı** — .NET ↔ Node.js

**Sonraki adım:** Phase 0'a başla — mevcut müşteri analizi + AI stratejisi kesinleştirme.
