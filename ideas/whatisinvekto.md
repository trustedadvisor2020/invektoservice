# Invekto (WapCRM) — Mevcut Ürün Envanteri

> Tarih: 2026-02-08
> Kaynak: Q interview
> Amaç: Invekto'nun bugün ne yaptığını belgelemek, roadmap gap analizi için referans

---

## Genel Tanım

**Invekto** (eski adıyla WapCRM), WhatsApp ve diğer mesajlaşma kanallarını işletmeler için merkezi bir CRM sistemine çeviren SaaS platformdur. 50+ aktif müşteriye hizmet vermektedir.

---

## Tech Stack

| Bileşen | Teknoloji |
|---------|-----------|
| Backend | .NET / C# (ASP.NET Core) |
| Frontend | Angular |
| Veritabanı | SQL Server |
| Hosting | SaaS (Cloud) — tek merkezi instance |
| Eklenti Servisler | InvektoServis (Node.js mikro servisler — bu repo) |

---

## Aktif Müşteri & Gelir

| Metrik | Değer |
|--------|-------|
| Aktif müşteri (firma) | 50+ |
| MRR aralığı | 50-200K TL |
| Deploy modeli | SaaS (multi-tenant) |

---

## Desteklenen Kanallar

| Kanal | Durum |
|-------|-------|
| WhatsApp (Meta Cloud API) | ✅ |
| WhatsApp (BSP üzerinden) | ✅ |
| Instagram DM | ✅ |
| Facebook Messenger | ✅ |
| Telegram | ✅ |
| SMS | ✅ |
| VOIP / Ses | ✅ |

> **7 kanal** tek panelden yönetiliyor.

---

## Mevcut Özellikler (VAR)

### 1. Merkezi Mesajlaşma (Unified Inbox)
- Tüm kanallar tek panelden yönetiliyor
- Sohbet etiketleme (tagging) ve filtreleme
- Dosya/medya gönderimi (resim, PDF, ses kaydı vb.)
- Otomatik karşılama mesajı (welcome/greeting)
- Sohbet transferi (agent'tan agent'a)

### 2. Multi-User & Chat Routing
- Bir hattı birden fazla agent yönetebilir
- Kanal bazlı bağımsız routing ayarları
- Akıllı atama algoritmaları:
  - Random
  - Sıralı
  - O gün en az sohbeti olan agent
  - Yeni mesaj atmak için en çok bekleyen agent
- Mevcut müşteri → eski agent'ına yönlendirilir
- Agent offline ise → algoritmaya göre başka agent'a atanır

### 3. Şablon Mesajlar (Templates & Quick Replies)
- Önceden hazırlanmış şablon mesajlar
- Hızlı cevap (quick reply) desteği

### 4. Proaktif Mesaj Gönderimi (Outbound)
- Müşteriye ilk mesajı atabilme (outbound)
- WhatsApp template message desteği

### 5. Raporlama & Analiz
- Dashboard: mesaj istatistikleri, agent performansı
- Yanıt süreleri, chat hacmi metrikleri
- Chat analizi: InvektoServis ChatAnalysis API üzerinden sentiment/satın alma niyeti analizi (Claude Haiku, 15 kriter)

### 6. Agent Yönetimi
- Agent performans takibi (yanıt süresi, çözüm sayısı)
- Online/offline durum takibi
- Supervisor canlı izleme (monitor) ve devralma (takeover)

### 7. CRM Özellikleri
- Müşteri kartı (her numara otomatik contact olarak kaydedilir)
- Not ekleme
- Etiketleme (tagging)
- 10 adet custom field
- **Pipeline YOK**

### 8. Kimlik Doğrulama & Yetki (Auth)
- Multi-tenant: firma adı + kullanıcı adı + parola ile giriş
- Subdomain sistemi YOK
- 2 rol: User (agent) ve Supervisor
- **SSO/OAuth YOK**

### 9. Güvenlik & Uyumluluk
- GDPR / KVKK uyumlu veri yönetimi
- Erişim izinleri
- Veri maskelenmesi

### 10. Ekip İşbirliği
- Ortak gelen kutusu
- Dosya/medya gönderimi
- Sesli mesaj desteği
- Agent aktivite kayıtları

### 11. Entegrasyonlar
- Shopify entegrasyonu
- Zoho entegrasyonu
- Webhook API bağlantıları
- InvektoServis API entegrasyonu (ChatAnalysis)

### 12. Çoklu Dil Desteği (Multi-language)
- Arayüz ve/veya mesajlaşma için çoklu dil desteği mevcut

### 13. VOIP / Çağrı Merkezi
- CRM içinde telefon görüşmeleri
- Arama kaydı ve raporlama

---

## Mevcut Olmayan Özellikler (YOK)

| Özellik | Durum | Roadmap Phase |
|---------|-------|---------------|
| AI Agent Assist (cevap önerisi) | ❌ | Phase 1 (C8) |
| AI Auto-Resolution (otomatik çözümleme) | ❌ | Phase 3 (AgentAI) |
| Knowledge Base / RAG | ❌ | Phase 3 (Knowledge) |
| Chatbot / Flow Builder | ❌ | — |
| Toplu mesaj gönderimi (Broadcast) | ❌ | Phase 2 (Outbound v2) |
| Follow-up otomasyonu | ❌ | Phase 2 (Outbound) |
| Mesaj zamanlama (Schedule) | ❌ | — |
| CSAT anketi (müşteri memnuniyeti) | ❌ | — |
| Kara liste / numara engelleme | ❌ | — |
| Çalışma saati yönetimi / SLA | ❌ | — |
| Audit log (işlem geçmişi) | ❌ | Phase 4 (Audit) |
| Sales pipeline | ❌ | — |
| Randevu yönetimi / takvim | ❌ | Phase 1 (Diş niche) |
| Ödeme entegrasyonu (iyzico/PayTR) | ❌ | Phase 5 (Revenue Agent) |
| Reklam attribution / UTM tracking | ❌ | Phase 2 (Estetik niche) |
| Trendyol / Hepsiburada API | ❌ | Phase 1 (E-ticaret niche) |
| Mobil uygulama | ❌ | — |
| Internal note (iç not) | ❌ | — |
| SSO / OAuth | ❌ | Phase 4 (Auth) |

---

## InvektoServis İlişkisi

**InvektoServis** = Invekto'nun kullanacağı AI/analiz mikro servisleri.

```
Invekto (Ana Uygulama)           InvektoServis (Eklenti Servisler)
┌─────────────────────┐          ┌─────────────────────────┐
│ .NET / C# / Angular │  ─API──> │ Node.js mikro servisler │
│ SQL Server          │          │                         │
│ 50+ müşteri         │          │ - ChatAnalysis (:7101)  │
│ 7 kanal             │          │ - Backend/GW (:5000)    │
│ Multi-tenant SaaS   │          │ - (gelecek servisler)   │
└─────────────────────┘          └─────────────────────────┘
```

Invekto, InvektoServis'i API üzerinden çağırır. InvektoServis bağımsız mikro servisler olarak çalışır ve Invekto'ya AI/analiz yetenekleri kazandırır.

---

## Özet: Invekto Güçlü ve Zayıf Yanları

### Güçlü
- 7 kanal desteği (rakiplerin çoğundan fazla)
- 50+ aktif müşteri, kanıtlanmış ürün
- Gelişmiş routing algoritmaları
- VOIP entegrasyonu (differentiator)
- Multi-tenant SaaS altyapısı hazır
- KVKK/GDPR uyumu mevcut
- Supervisor monitoring/takeover

### Zayıf (Roadmap ile kapatılacak)
- AI yetenekleri henüz yok (Agent Assist, Auto-Resolution)
- Otomasyon zayıf (chatbot, flow builder, follow-up, schedule yok)
- Niche-özel özellikler yok (randevu, e-ticaret API, ödeme)
- Analitik temel düzeyde (SLA, CSAT, audit log yok)
- Mobil uygulama yok
