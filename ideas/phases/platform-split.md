# Invekto Main App vs InvektoServis — Özellik Dağılım Tablosu

> Son güncelleme: 2026-02-08
> **Invekto Main App:** .NET / Angular / SQL Server — mevcut 50+ müşterili çalışan ürün
> **InvektoServis:** Node.js / PostgreSQL — AI, otomasyon ve niche-özel eklenti katmanı

---

## Özet

```
Invekto Main App (.NET)                    InvektoServis (Node.js)
┌────────────────────────────┐             ┌────────────────────────────┐
│ Unified Inbox (7 kanal)    │             │ Invekto.Backend    :5000   │
│ Chat Routing (4 algoritma) │    API      │ Invekto.ChatAnalysis:7101  │
│ CRM + Contact              │◄──────────►│ Invekto.Automation  :7108  │
│ Auth (multi-tenant)        │             │ Invekto.AgentAI     :7105  │
│ Templates                  │             │ Invekto.Outbound    :7107  │
│ VOIP                       │             │ Invekto.Integrations:7106  │
│ Raporlama                  │             │ Invekto.Knowledge   :7104  │
│ Agent Yönetimi             │             │ Invekto.Audit       :7103  │
│ SQL Server DB              │             │ PostgreSQL DB (+ pgvector)  │
└────────────────────────────┘             └────────────────────────────┘
```

---

## Phase 0 — Müşteri Analizi (Hafta 1-2)

> Bu phase'te kod yazılmaz — tamamen iş geliştirme ve validasyon.

| Özellik | Taraf | Açıklama |
|---------|-------|----------|
| Niche validasyon görüşmeleri | — | Kod yok |
| Grand Slam Offer tasarımı | — | Kod yok |
| Mevcut müşteri churn analizi | **Main App** | SQL Server'dan mevcut veriyle analiz |

---

## Phase 1 — Core Otomasyon (Hafta 3-8)

| # | Özellik | Main App (.NET) | InvektoServis (Node.js) | Notlar |
|---|---------|:-:|:-:|--------|
| 1.1 | **Chatbot / Flow Builder** | — | ✅ `Automation` :7108 | Yeni servis |
| 1.2 | **AI Agent Assist (suggested reply)** | UI: öneri paneli göster | ✅ `AgentAI` :7105 | Main App UI'da agent önerileri gösterecek |
| 1.3 | **Broadcast / Toplu Mesaj** | — | ✅ `Outbound` :7107 | Yeni servis, WhatsApp API üzerinden |
| 1.4 | **Trigger Sistemi (event-based)** | Event'leri webhook/API ile bildir | ✅ `Outbound` :7107 | Main App event üretir, Outbound tetiklenir |
| 1.5 | **Dinamik Şablon Değişkenleri** | Template render UI | ✅ `AgentAI` :7105 | Değişken çözümleme InvektoServis'te |
| 1.6 | **Çalışma Saati Yönetimi** | ✅ Config UI + saklama | `Automation` :7108 kontrol | Main App saat tanımlar, Automation uygular |
| 1.7 | **Otomatik Etiketleme (AI)** | ✅ Etiket sistemi mevcut | `AgentAI` :7105 AI karar | Main App etiketler, AI hangi etiketi atacağına karar verir |
| 1.8 | **Otomasyon Dashboard** | — | ✅ React Dashboard | Mevcut dashboard genişler |
| 1.9 | **Diş: Fiyat Sorusu Pipeline** | — | ✅ `ChatAnalysis` + Backend | Intent detection + otocevap |
| 1.10 | **Diş: Basit Randevu Motoru** | — | ✅ Backend :5000 | Yeni endpoint'ler |
| 1.11 | **Estetik: Lead Pipeline** | — | ✅ `ChatAnalysis` + Backend | Intent + lead tracking |
| 1.12 | **KVKK Minimum Koruma** | ✅ Disclaimer template | ✅ Opt-in akışı | Her iki tarafta kurallar |
| 1.13 | **Invekto ↔ InvektoServis Entegrasyonu** | ✅ API endpoint'leri aç | ✅ Token validation + tenant sync | KRİTİK: İki platform arası köprü |

### Phase 1 — Entegrasyon Detayı

| Veri Akışı | Yön | Açıklama |
|------------|-----|----------|
| Yeni mesaj geldi | Main App → InvektoServis | Webhook/API ile mesaj ilet |
| AI cevap önerisi | InvektoServis → Main App | AgentAI önerir, Main App UI'da gösterir |
| Otomatik cevap gönder | InvektoServis → Main App | Automation karar verir, Main App WhatsApp'a gönderir |
| Etiket ata | InvektoServis → Main App | AI etiket belirler, Main App CRM'de uygular |
| Broadcast gönder | InvektoServis → Main App | Outbound mesajları hazırlar, Main App gönderir |
| Sohbet kapatıldı | Main App → InvektoServis | Trigger event'i |
| Tenant/user bilgisi | Main App → InvektoServis | Auth token validation |

---

## Phase 2 — Niche Güçlendirme (Hafta 9-16)

| # | Özellik | Main App (.NET) | InvektoServis (Node.js) | Notlar |
|---|---------|:-:|:-:|--------|
| 2.1 | **Intent Genişletme (10-12 intent)** | — | ✅ `ChatAnalysis` / `AgentAI` | Sadece AI tarafı |
| 2.2 | **B2B / VIP Lead Tespiti** | ✅ VIP flag CRM'de göster | ✅ `ChatAnalysis` algılar | AI algılar, CRM gösterir |
| 2.3 | **Agent Assist: Sipariş Kartı** | ✅ UI'da sipariş paneli | ✅ `AgentAI` + `Integrations` | Veri InvektoServis'ten, UI Main App'te |
| 2.4 | **Trendyol API Entegrasyonu** | — | ✅ `Integrations` :7106 | Yeni servis |
| 2.5 | **Hepsiburada API Entegrasyonu** | — | ✅ `Integrations` :7106 | Trendyol pattern kopyası |
| 2.6 | **Kargo Entegrasyonu (opsiyonel)** | — | ✅ `Integrations` :7106 | Aras/Yurtiçi tracking |
| 2.7 | **Onboarding Otomasyonu** | ✅ Tenant setup UI (varsa) | ✅ Backend wizard | Self-service API key girişi |
| 2.8 | **Outbound E-ticaret Trigger'ları** | Event bildir | ✅ `Outbound` :7107 | Teslim/iade event'leri |
| 2.9 | **İade Çevirme v1** | — | ✅ `ChatAnalysis` + Backend | Intent + aksiyon |
| 2.10 | **Diş: Randevu Motoru v2** | ✅ Google Calendar OAuth UI | ✅ Backend sync logic | Calendar sync iki taraflı |
| 2.11 | **Diş: Doktor Bazlı Slot** | ✅ Doktor tanım UI (varsa) | ✅ Backend slot yönetimi | Doktor bilgisi Main App'ten |
| 2.12 | **Klinik Outbound v1** | — | ✅ `Outbound` :7107 | Hatırlatma + follow-up |
| 2.13 | **Estetik: Lead Mgmt v2** | ✅ Pipeline view UI (CRM'de) | ✅ Backend scoring + tracking | Lead data iki tarafta |
| 2.14 | **Estetik: Basit Ads Attribution** | — | ✅ Backend + Dashboard | UTM capture + raporlama |
| 2.15 | **Multi-Language v1** | ✅ Dil tercihi UI | ✅ `ChatAnalysis` dil algılama | Algılama AI'da, template Main App'te |

---

## Phase 3 — AI Derinleştirme (Hafta 17-24)

| # | Özellik | Main App (.NET) | InvektoServis (Node.js) | Notlar |
|---|---------|:-:|:-:|--------|
| 3.1 | **Knowledge Service (RAG)** | — | ✅ `Knowledge` :7104 | Yeni servis, pgvector |
| 3.2 | **AgentAI Orchestrator** | — | ✅ `AgentAI` :7105 | Pipeline genişleme |
| 3.3 | **Agent Assist v2 (tone, kaynak)** | ✅ UI: tone seçici, kaynak göster | ✅ `AgentAI` üretir | UI Main App'te |
| 3.4 | **Knowledge Management UI** | — | ✅ Dashboard (doc yükle, FAQ) | Ayrı dashboard |
| 3.5 | **Outbound v2 (kampanya, A/B)** | — | ✅ `Outbound` :7107 | Kampanya yönetimi |
| 3.6 | **Negatif Yorum Kurtarma (S1)** | — | ✅ `Integrations` + `Outbound` | Trendyol Review API |
| 3.7 | **İade Çevirme v2** | — | ✅ `AgentAI` + `Outbound` | Kupon + stok kontrol |
| 3.8 | **Multi-Language AI (TR/EN)** | ✅ Dil template'leri | ✅ `ChatAnalysis` + `AgentAI` | Algılama + response |
| 3.9 | **Intent Performance Dashboard** | — | ✅ Dashboard | Analitik raporlar |

---

## Phase 4 — Enterprise (Hafta 25-32)

| # | Özellik | Main App (.NET) | InvektoServis (Node.js) | Notlar |
|---|---------|:-:|:-:|--------|
| 4.1 | **SSO (Google/Microsoft OIDC)** | ✅ **Ana uygulama** | — | Mevcut auth'un üzerine |
| 4.2 | **2FA (TOTP)** | ✅ **Ana uygulama** | — | QR + backup codes |
| 4.3 | **Session Management** | ✅ **Ana uygulama** | — | Device list, revoke, timeout |
| 4.4 | **Brute-force Protection** | ✅ **Ana uygulama** | — | 5 deneme → kilit |
| 4.5 | **IP / Country Allowlist** | ✅ **Ana uygulama** | — | CIDR + GeoIP |
| 4.6 | **Tenant Policy Engine** | ✅ **Ana uygulama** | — | SSO/2FA zorunluluk ayarları |
| 4.7 | **Audit Service** | — | ✅ `Audit` :7103 | Yeni servis |
| 4.8 | **PII Koruma (maskeleme)** | ✅ UI maskeleme | ✅ `Audit` + `AgentAI` detector | Tespit AI'da, maskeleme UI'da |
| 4.9 | **Guardrails** | — | ✅ `AgentAI` genişleme | Banned phrases, PII prevention |
| 4.10 | **Admin Panel** | ✅ Kullanıcı/rol yönetimi UI | ✅ Dashboard (audit viewer) | İki ayrı admin alanı |
| 4.11 | **QA & Mining Hazırlık** | — | ✅ `Audit` + Dashboard | Metadata log toplama |
| 4.12 | **Sağlık Enterprise (SLA, multi-şube)** | ✅ Şube tanım UI | ✅ `Audit` + Dashboard | Şube izolasyonu her iki tarafta |

---

## Phase 5 — Revenue & Ölçek (Hafta 33-40)

| # | Özellik | Main App (.NET) | InvektoServis (Node.js) | Notlar |
|---|---------|:-:|:-:|--------|
| 5.1 | **Revenue Agent — Lead Qualification** | — | ✅ `AgentAI` genişleme | AI konuşma akışı |
| 5.2 | **Revenue Agent — Satış (upsell/cross)** | — | ✅ `AgentAI` + `Integrations` | Öneri motoru |
| 5.3 | **Ürün Kataloğu** | — | ✅ `Integrations` | Import + sync |
| 5.4 | **Payment Link (iyzico/PayTR)** | — | ✅ Backend | Ödeme link oluşturma |
| 5.5 | **Abandoned Cart Recovery** | — | ✅ `Integrations` + `Outbound` | Sepet terk tespiti + hatırlatma |
| 5.6 | **Post-Purchase Proaktif Satış** | — | ✅ `Outbound` + `AgentAI` | Memnuniyet + cross-sell |
| 5.7 | **Click-to-WhatsApp Attribution** | — | ✅ Backend + Dashboard | Meta click id capture |
| 5.8 | **Tedavi Sonrası Takip (S8)** | — | ✅ `AgentAI` + `Outbound` | T+1/7/30 takip |
| 5.9 | **Google Yorum + Referans (S10)** | — | ✅ `Outbound` + `Integrations` | Yorum rica + referral |
| 5.10 | **Medikal Turizm Leads (S9)** | — | ✅ `AgentAI` + `Outbound` | Multi-lang + döviz |

---

## Phase 6 — Operasyon & Analytics (Hafta 41-48)

| # | Özellik | Main App (.NET) | InvektoServis (Node.js) | Notlar |
|---|---------|:-:|:-:|--------|
| 6.1 | **SLA Tracker** | ✅ SLA config UI | ✅ Backend + Dashboard ölçüm | Config Main App, ölçüm InvektoServis |
| 6.2 | **QA Scoring (C13)** | — | ✅ `ChatAnalysis` + Dashboard | Otomatik skor |
| 6.3 | **Conversation Mining** | — | ✅ `ChatAnalysis` + `Knowledge` | Win/loss, complaint, trend |
| 6.4 | **Knowledge Gap Report** | — | ✅ `Knowledge` + Dashboard | Unanswered intents |
| 6.5 | **Revenue Attribution Dashboard** | — | ✅ Dashboard | Tam gelir izleme |

---

## Phase 7 — Genişleme (Hafta 49+)

| # | Özellik | Main App (.NET) | InvektoServis (Node.js) | Notlar |
|---|---------|:-:|:-:|--------|
| 7.1 | **Mobil Uygulama** | ✅ API endpoint'leri (mevcut) | ✅ Yeni mobil app (RN/Flutter) | Mobil her iki API'yi tüketir |
| 7.2 | **Yeni Kanal Entegrasyonları** | ✅ **Ana uygulama** (kanal yönetimi) | ✅ `Integrations` (marketplace) | Mesajlaşma kanalları Main App'te |
| 7.3 | **Voice Transcription** | — | ✅ Whisper API entegrasyonu | Ses→metin |
| 7.4 | **Video Call** | ✅ VOIP genişleme | — | Mevcut VOIP üzerine |
| 7.5 | **Predictive Analytics** | — | ✅ `AgentAI` + Backend | ML modelleri |
| 7.6 | **Multi-Currency** | ✅ Fiyat gösterim UI | ✅ Backend döviz hesaplama | Her iki tarafta |
| 7.7 | **Timezone Scheduling** | — | ✅ `Outbound` genişleme | Zamanlama |
| 7.8 | **Compliance Scanner** | — | ✅ Yeni modül | KVKK/GDPR tarama |

---

## Özet Sayılar

| Taraf | Toplam Özellik | Açıklama |
|-------|---------------|----------|
| **Sadece Main App** | ~15 | Auth/SSO/2FA, session, IP allowlist, kanal yönetimi, VOIP |
| **Sadece InvektoServis** | ~45 | AI, otomasyon, knowledge, outbound, integrations, dashboard |
| **Her İki Taraf (entegrasyon)** | ~15 | Agent Assist UI, etiketleme, lead CRM, PII, SLA, mobil |

### Neden Bu Dağılım?

| Main App (.NET) | InvektoServis (Node.js) |
|-----------------|------------------------|
| Kullanıcı etkileşimi (UI/UX) | AI / ML işlemleri |
| Auth / güvenlik / session | Otomasyon / flow engine |
| Mesaj gönderme/alma (kanal API) | Intent detection / response generation |
| CRM veri saklama | Embedding / RAG / pgvector |
| Mevcut müşteri verisi (SQL Server) | Yeni servis verileri (PostgreSQL) |
| Kanal yönetimi (7 kanal) | Entegrasyon API'leri (Trendyol/HB) |
| Template yönetimi | Broadcast / kampanya motoru |

### Kritik Entegrasyon Noktaları

| # | Entegrasyon | Main App Görevi | InvektoServis Görevi | Risk |
|---|-------------|-----------------|---------------------|------|
| 1 | **Mesaj akışı** | Mesaj geldi → webhook ile bildir | AI analiz + otocevap kararı | Latency (<200ms hedef) |
| 2 | **Auth token** | JWT üret | JWT validate et | Token format uyumu |
| 3 | **Tenant sync** | tenant_id (SQL Server) | tenant_id (PostgreSQL) | ID eşleşmesi KRİTİK |
| 4 | **Etiket/CRM** | Etiket uygula | AI etiket belirle | API contract uyumu |
| 5 | **Cevap gönder** | WhatsApp API'ye gönder | Cevap metnini hazırla | Rate limiting |
| 6 | **Agent UI** | Öneri paneli göster | Öneri üret | Realtime (WebSocket?) |
| 7 | **Broadcast** | WhatsApp template gönder | Kuyruğa al + zamanlama | WhatsApp policy uyumu |
