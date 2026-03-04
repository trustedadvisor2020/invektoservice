<!-- Status: REFERENCE -->
# Invekto (WapCRM) — Mevcut Ürün Envanteri

> Tarih: 2026-02-08
> Kaynak: Q interview (kapsamlı)
> Amaç: Invekto'nun bugün ne yaptığını belgelemek, roadmap gap analizi için referans
> Durum: TAMAMLANDI

---

## Genel Tanım

**Invekto** (eski adıyla WapCRM), WhatsApp ve diğer mesajlaşma kanallarını işletmeler için merkezi bir CRM sistemine çeviren SaaS platformdur. **50+ aktif müşteriye** hizmet vermekte, **50-200K TL MRR** üretmektedir. Müşteri tabanı ağırlıklı olarak **hizmet sektöründen** (sağlık klinikleri + otel/turizm) oluşmaktadır.

---

## Tech Stack

| Bileşen | Teknoloji |
|---------|-----------|
| Backend | .NET / C# (ASP.NET Core) |
| Frontend | Angular |
| Veritabanı | SQL Server |
| Hosting | SaaS (Cloud) — tek merkezi instance, multi-tenant |
| Eklenti Servisler | InvektoServis (Node.js mikro servisler — ayrı repo) |

---

## İş Metrikleri

| Metrik | Değer |
|--------|-------|
| Aktif müşteri (firma) | 50+ |
| MRR aralığı | 50-200K TL |
| Fiyatlandırma | $25/agent + $40/kanal |
| Deploy modeli | SaaS (multi-tenant) |
| Onboarding | White-glove (biz kuruyoruz, eğitim veriyoruz) |
| Müşteri bulma | Karma (web + referans + doğrudan satış) |
| Destek modeli | Karma (WhatsApp + dokümantasyon + birebir destek) |
| Ekip büyüklükleri | Karışık (1-3'ten 30+ agent'a kadar) |

### Müşteri Profili

| Özellik | Detay |
|---------|-------|
| Ağırlıklı sektör | Hizmet (sağlık klinikleri + otel/turizm) |
| Alt sektörler | Diş klinikleri, estetik, otel, turizm |
| Randevu/rezervasyon bazlı | 5-15 müşteri |
| En çok kullanılan özellik | Unified Inbox + Routing |
| En çok istenen özellik | Chatbot/Otomasyon, Broadcast, Mobil App |

### Rekabet & Satış

| Konu | Detay |
|------|-------|
| Güçlü satış argümanı | 7 kanal tek inbox |
| En büyük satış itirazı | "Chatbot/AI yok mu?" |
| En büyük churn sebebi | Otomasyon eksikliği |
| Rakipler | Yerli WhatsApp CRM çözümleri |
| En acil sorun | Eksik özellikler (teknik borçtan çok) |

---

## Desteklenen Kanallar (7 Kanal)

| Kanal | Durum | Not |
|-------|-------|-----|
| WhatsApp (Meta Cloud API) | ✅ | Resmi API |
| WhatsApp (BSP üzerinden) | ✅ | Business Solution Provider |
| Instagram DM | ✅ | Sadece DM, post/story yorumları YOK |
| Facebook Messenger | ✅ | |
| Telegram | ✅ | |
| SMS | ✅ | |
| VOIP / Ses | ✅ | Çağrı merkezi entegrasyonu dahil |

---

## Mevcut Özellikler (VAR) — Detaylı

### 1. Merkezi Mesajlaşma (Unified Inbox)
- Tüm 7 kanal tek panelden yönetiliyor
- Sohbet etiketleme (tagging) ve filtreleme — **agent'lar manuel etiketliyor**
- Dosya/medya gönderimi (resim, PDF, ses kaydı vb.)
- Sohbet transferi (agent'tan agent'a)
- Sohbet kapatma (manuel close) — **otomatik close YOK**
- Tam sohbet geçmişi — agent müşterinin tüm önceki konuşmalarını görebiliyor
- Sohbet durumu: açık/kapalı düğmesi + etiketler ile yönetim

### 2. Multi-User & Chat Routing (Gelişmiş)
- Bir hattı birden fazla agent yönetebilir
- **Agent grupları** tam destek (Satış Ekibi, Destek Ekibi vb.)
- Kanal bazlı bağımsız routing ayarları — **her kanalın kendi routing'i var**
- 4 akıllı atama algoritması:
  - Random
  - Sıralı
  - O gün en az sohbeti olan agent
  - Yeni mesaj atmak için en çok bekleyen agent
- Mevcut müşteri → eski agent'ına yönlendirilir
- Agent offline ise → algoritmaya göre aynı gruptan başka agent'a atanır

### 3. Otomatik Karşılama (Welcome Message)
- Yeni müşterilere ayrı welcome mesajı
- Daha önce sohbeti olan müşterilere ayrı welcome mesajı
- **Sabit metin** — dinamik değişken ({{isim}}) YOK
- **Tek trigger:** Sadece welcome, başka otomasyon trigger'ı YOK

### 4. Şablon Mesajlar (Templates & Quick Replies)
- Önceden hazırlanmış şablon mesajlar
- Hızlı cevap (quick reply) desteği
- **Dinamik değişken desteği YOK** ({{müşteri_adı}} gibi placeholder yok)
- WhatsApp template message yönetimi **Meta panelinden** yapılıyor (Invekto UI'ından değil)

### 5. Proaktif Mesaj Gönderimi (Outbound — Temel)
- Müşteriye ilk mesajı atabilme — **tek tek mesaj gönderimi**
- WhatsApp template message desteği
- **Toplu gönderim (broadcast) YOK**
- **Zamanlı gönderim (schedule) YOK**
- **Follow-up otomasyonu YOK**
- **Liste bazlı gönderim YOK**

### 6. Raporlama & Analiz (Kapsamlı)
- Dashboard: mesaj istatistikleri, agent performansı
- Yanıt süreleri, chat hacmi metrikleri
- Agent başına performans metrikleri
- Kanal bazlı dağılım
- Ek metrikler mevcut
- Chat analizi: InvektoServis ChatAnalysis API üzerinden sentiment/satın alma niyeti analizi (Claude Haiku, 15 kriter)

### 7. Agent Yönetimi
- Agent performans takibi (yanıt süresi, çözüm sayısı)
- Online/offline durum takibi
- Supervisor canlı izleme (monitor) ve devralma (takeover)
- **Shift/nöbet yönetimi YOK**
- **Auto idle timeout YOK** (X dk cevap vermezse başkasına ata)

### 8. CRM Özellikleri
- Müşteri kartı — **her numara otomatik contact olarak kaydedilir**
- Not ekleme
- Etiketleme (tagging) — filtreleme için kullanılıyor
- 10 adet custom field — **müşteriler aktif kullanıyor**
- **Sales pipeline YOK**
- **Müşteri segmentasyonu**: Etiketlerle yapılabilir (özel segment UI yok)

### 9. Kimlik Doğrulama & Yetki (Auth)
- Multi-tenant: firma adı + kullanıcı adı + parola ile giriş
- **Subdomain sistemi YOK**
- 2 rol: **User** (agent) ve **Supervisor**
- **SSO/OAuth YOK**
- **Admin rolü YOK** (supervisor üstü yetki)

### 10. Güvenlik & Uyumluluk
- GDPR / KVKK uyumlu veri yönetimi
- Erişim izinleri
- Veri maskelenmesi
- **Audit log YOK**

### 11. Ekip İşbirliği
- Ortak gelen kutusu
- Dosya/medya gönderimi
- Sesli mesaj desteği
- Agent aktivite kayıtları
- **Internal note (iç not) YOK** — müşterinin görmediği ekip içi not eklenemez

### 12. Entegrasyonlar
- Shopify entegrasyonu
- Zoho entegrasyonu
- Webhook API bağlantıları (dışa veri gönderme)
- InvektoServis API entegrasyonu (ChatAnalysis)
- **Trendyol / Hepsiburada API YOK**
- **Ödeme gateway YOK** (iyzico, PayTR vb.)

### 13. Çoklu Dil Desteği (Multi-language)
- Arayüz ve/veya mesajlaşma için çoklu dil desteği mevcut

### 14. VOIP / Çağrı Merkezi
- CRM içinde telefon görüşmeleri
- Arama kaydı ve raporlama

### 15. Web Erişimi
- WhatsApp'a yönlendiren click-to-chat düğmesi (web sitesine konulabiliyor)
- **Gömülü chat widget YOK** (canlı sohbet kutusu)
- **Mobil uygulama YOK** — sadece web erişimi

---

## Mevcut Olmayan Özellikler (YOK) — Tam Liste

### Otomasyon & AI (EN KRİTİK — Satış engeli + churn sebebi)

| Özellik | Etki | Roadmap Phase |
|---------|------|---------------|
| Chatbot / Flow Builder | 🔴 Kritik — 1 numaralı müşteri talebi | Phase 1 (Automation) |
| AI Agent Assist (cevap önerisi) | 🔴 Kritik — agent zaman kaybını azaltır | Phase 1 (AgentAI) |
| AI Auto-Resolution (otomatik çözümleme) | 🟠 Yüksek | Phase 3 (AgentAI) |
| Mesaj trigger/otomasyon sistemi | 🔴 Kritik — welcome dışında trigger yok | Phase 1 (Automation) |
| Follow-up otomasyonu | 🟠 Yüksek | Phase 2 (Outbound) |
| Otomatik etiketleme/kategorizasyon | 🟡 Orta | Phase 1 (AgentAI) |
| Post-close otomasyonu | 🟡 Orta — anket, takip mesajı | Phase 2 (Outbound) |
| Dinamik şablon değişkenleri | 🟠 Yüksek — {{isim}} gibi | Phase 1 (Automation) |
| Knowledge Base / RAG | 🟡 Orta | Phase 3 (Knowledge) |

### Outbound & Broadcast

| Özellik | Etki | Roadmap Phase |
|---------|------|---------------|
| Toplu mesaj gönderimi (Broadcast) | 🔴 Kritik — top 3 talep | Phase 1 (Outbound) |
| Mesaj zamanlama (Schedule) | 🟠 Yüksek | Phase 1 (Outbound) |
| Liste bazlı gönderim | 🟠 Yüksek | Phase 1 (Outbound) |
| Template yönetimi (Invekto UI'ından) | 🟡 Orta — şu an Meta panelinden | Phase 2 |

### Operasyonel

| Özellik | Etki | Roadmap Phase |
|---------|------|---------------|
| Çalışma saati yönetimi | 🟠 Yüksek — mesai dışı otomasyon | Phase 1 (Automation) |
| SLA takibi / eskalasyon | 🟡 Orta | Phase 4 (Enterprise) |
| Kara liste / numara engelleme | 🟡 Orta | Phase 2 |
| Internal note (iç not) | 🟡 Orta | Phase 2 |
| Auto idle timeout | 🟡 Orta — cevapsız sohbet yeniden ata | Phase 2 |
| Agent shift/nöbet yönetimi | 🟡 Orta | Phase 2 |
| Otomatik sohbet kapatma (auto-close) | 🟡 Orta | Phase 2 |
| Conversation history export | 🟢 Düşük | Phase 3 |
| CSAT anketi (müşteri memnuniyeti) | 🟡 Orta | Phase 2 |
| Audit log (işlem geçmişi) | 🟡 Orta | Phase 4 (Audit) |

### Niche-Özel

| Özellik | Etki | Roadmap Phase |
|---------|------|---------------|
| Randevu yönetimi / takvim | 🟠 Yüksek — mevcut klinik müşterileri | Phase 2 (Integrations) |
| Trendyol / Hepsiburada API | 🟡 Orta — e-ticaret niche'i | Phase 2 (Integrations) |
| Reklam attribution / UTM tracking | 🟡 Orta — estetik niche | Phase 2 (basit) / Phase 5 (tam) |
| Ödeme entegrasyonu (iyzico/PayTR) | 🟡 Orta | Phase 5 (Revenue Agent) |

### Platform

| Özellik | Etki | Roadmap Phase |
|---------|------|---------------|
| Mobil uygulama | 🔴 Kritik — top 3 talep | Phase 5 |
| Sales pipeline | 🟡 Orta | Phase 3 |
| Chat widget (gömülü) | 🟡 Orta | Phase 3 |
| IG/FB yorum yönetimi | 🟡 Orta — şu an sadece DM | Phase 3 |
| SSO / OAuth | 🟢 Düşük — kurumsal talep gelince | Phase 4 (Enterprise) |

---

## Senaryo Bazlı Mevcut Durum

### Sağlık Klinikleri (Mevcut müşteri tabanı)

| Senaryo | Bugün nasıl çözülüyor | Gap |
|---------|----------------------|-----|
| "Fiyat ne kadar?" | Şablon mesajla cevap | Otomatik cevap YOK, AI önerisi YOK |
| Randevu alma | Manuel (Invekto dışında) | Randevu motoru YOK |
| No-show takip | Manuel telefon araması | Otomatik hatırlatma YOK |
| Tedavi sonrası takip | Yapılmıyor | Follow-up otomasyonu YOK |

### Otel / Turizm (Mevcut müşteri tabanı)

| Senaryo | Bugün nasıl çözülüyor | Gap |
|---------|----------------------|-----|
| "Boş odanız var mı?" | Agent PMS'e bakıp cevaplıyor | PMS entegrasyonu YOK |
| Rezervasyon onayı | Manuel mesaj | Otomatik onay YOK |
| Check-in hatırlatma | Yapılmıyor | Outbound otomasyon YOK |

### E-ticaret (Potansiyel müşteri)

| Senaryo | Bugün nasıl çözülüyor | Gap |
|---------|----------------------|-----|
| "Kargom nerede?" | Agent Trendyol/HB paneline geçip bakıyor | Trendyol/HB API YOK |
| İade talebi | Şablon mesajla standart cevap | Otomatik iade akışı YOK |

---

## Agent Zaman Kaybı Analizi

Agent'ların cevap verirken en çok zaman kaybettiği alanlar (**hepsi** sorun):

| Zaman Kaybı | Sebep | Çözüm |
|-------------|-------|-------|
| Dış sisteme geçiş | Trendyol/HB/PMS paneline gidip bilgi arama | Entegrasyon + AI Agent Assist |
| Tekrar eden sorular | Aynı sorulara her seferinde yazma | Chatbot + FAQ otomasyon + AI |
| Müşteri geçmişi arama | "Bu müşteri ne sormuştu?" | ✅ Mevcut (tam geçmiş var) ama AI özet YOK |
| Transfer/eskalasyon | Doğru kişiye ulaşmak | ✅ Transfer var ama akıllı routing/eskalasyon YOK |

---

## InvektoServis İlişkisi

**InvektoServis** = Invekto'nun kullanacağı AI/analiz/otomasyon eklenti mikro servisleri.

```
Invekto (Ana Uygulama)              InvektoServis (Eklenti Servisler)
┌──────────────────────────┐        ┌──────────────────────────────┐
│ .NET / C# / Angular      │        │ Node.js mikro servisler      │
│ SQL Server               │        │                              │
│ 50+ müşteri, 7 kanal     │  API   │ Mevcut:                      │
│ Multi-tenant SaaS         │──────>│  - Backend/GW (:5000)        │
│ $25/agent + $40/kanal    │        │  - ChatAnalysis (:7101)      │
│                          │        │                              │
│ Güçlü: Inbox, Routing,  │        │ Gelecek:                     │
│   VOIP, Template, CRM   │        │  - Automation (:7108) [Ph1]  │
│                          │        │  - AgentAI (:7105)   [Ph1]  │
│ Zayıf: Otomasyon, AI,   │        │  - Outbound (:7107)  [Ph1]  │
│   Chatbot, Broadcast     │        │  - Integrations (:7106)[Ph2]│
└──────────────────────────┘        │  - Knowledge (:7104) [Ph3]  │
                                    │  - Audit (:7103)     [Ph4]  │
                                    └──────────────────────────────┘
```

---

## Kritik Bulgular

### 1. Roadmap ile gerçeklik uyuşmazlığı
Roadmap "MRR = 0, müşteri = 0" varsayıyor. Gerçek: **50+ müşteri, 50-200K TL MRR**. Roadmap'in "müşteri bul → ürün yap" yaklaşımı yerine **"mevcut müşteriyi güçlendir → yeni müşteri kazan"** stratejisi gerekiyor.

### 2. Otomasyon = 1 numaralı öncelik
- Satış engeli: "Chatbot/AI yok mu?"
- Churn sebebi: Otomasyon eksikliği
- Top talep: Chatbot, broadcast, mobil app

### 3. Mevcut müşteri tabanı roadmap niche'leriyle örtüşüyor
Klinik + otel müşterileri zaten var. E-ticaret niche'i yeni müşteri kazanım, sağlık niche'i mevcut müşteriyi güçlendirme fırsatı.

### 4. Core platform eksikleri niche'ten önce gelir
Chatbot, otomasyon trigger'ları, broadcast, çalışma saati yönetimi gibi temel özellikler sektör farketmez HER müşterinin ihtiyacı. Niche-özel özellikler (randevu, Trendyol API) bunlardan sonra gelir.

### 5. InvektoServis'in rolü netleşti
InvektoServis = Invekto'ya AI ve otomasyon beyni kazandıran eklenti katmanı. Ana uygulama (.NET) değişmeden, InvektoServis mikro servisleri ile yeni yetenekler ekleniyor.
