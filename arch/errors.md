# InvektoServis Error Codes

> **KURAL:** Tüm hata mesajları bu dosyadaki kodları kullanmalı.
> **KOD DOSYASI:** `src/Invekto.Shared/Constants/ErrorCodes.cs`

## Format

```
INV-{SERVICE}-{NUMBER}
```

- `INV` = InvektoServis prefix
- `SERVICE` = Servis kodu (GEN, BE, CA, etc.)
- `NUMBER` = 3 haneli numara

## Servis Kodları

| Code | Service | Açıklama |
|------|---------|----------|
| GEN | General | Genel hatalar |
| BE | Backend | Backend API hataları |
| CA | ChatAnalysis | Chat Analysis microservice hataları |
| AT | Automation | GR-1.1: Chatbot/Flow Builder hataları |
| AUTH | Auth | Authentication hataları |
| INT | Integration | GR-1.9: Entegrasyon köprüsü hataları |
| DB | Database | Veritabanı hataları |
| VAL | Validation | Validasyon hataları |
| OB | Outbound | GR-1.3/3.15/3.26/3.29: Broadcast, campaign, consent, compliance hatalari |
| IG | Integrations | GR-3.4/3.6: Marketplace & kargo entegrasyonlari |
| AP | Appointments | GR-2.4: Randevu Motoru hatalari |
| KN | Knowledge | GR-2.1: Knowledge Service (RAG) hatalari |
| AD | Attribution | GR-3.14: Ads Attribution hatalari |
| MK | Marketing | GR-3.21/3.22: Google Yorum, Referans, Medikal Turizm hatalari |
| MT | Metrics | PKT-3: Analitik/metrik hatalari |
| EXT | External | Dış servis hataları |

---

## GEN - General Errors

| Code | Description | User Message |
|------|-------------|--------------|
| INV-GEN-001 | Unknown error | Beklenmeyen bir hata oluştu. |
| INV-GEN-002 | Timeout | İşlem zaman aşımına uğradı. |
| INV-GEN-003 | Validation error | Geçersiz veri formatı. |

---

## BE - Backend Errors

| Code | Description | User Message |
|------|-------------|--------------|
| INV-BE-001 | Microservice unavailable | Servis geçici olarak kullanılamıyor. |
| INV-BE-002 | Microservice timeout | Servis yanıt vermedi. Lütfen tekrar deneyin. |
| INV-BE-003 | Microservice error (5xx) | Servis hatası. Lütfen tekrar deneyin. |
| INV-BE-004 | Microservice invalid response | Servis geçersiz yanıt döndü. |
| INV-BE-005 | Microservice client error (4xx) | İstek hatası. Lütfen parametreleri kontrol edin. |
| INV-BE-010 | Message log query failed | Mesaj kayitlari yuklenemedi. |

---

## CA - ChatAnalysis Errors

| Code | Description | User Message |
|------|-------------|--------------|
| INV-CA-001 | Invalid payload | Geçersiz istek formatı. |
| INV-CA-002 | Processing failed | Analiz işlemi başarısız oldu. |
| INV-CA-003 | WapCRM API error | CRM servisine bağlanılamadı. |
| INV-CA-004 | WapCRM timeout | CRM servisi yanıt vermedi. |
| INV-CA-005 | Claude API error | Analiz servisi hatası. |
| INV-CA-006 | Claude timeout | Analiz servisi yanıt vermedi. |
| INV-CA-007 | No messages found | Bu numara için mesaj bulunamadı. |

---

## AUTH - Authentication Errors

| Code | Description | User Message |
|------|-------------|--------------|
| INV-AUTH-001 | Token expired | Oturumunuz sona erdi. Lütfen tekrar giriş yapın. |
| INV-AUTH-002 | Invalid token | Geçersiz oturum. |
| INV-AUTH-003 | Unauthorized | Bu işlem için yetkiniz bulunmuyor. |

---

## AT - Automation Errors (GR-1.1)

| Code | Description | User Message |
|------|-------------|--------------|
| INV-AT-001 | Invalid flow config | Chatbot akis konfigurasyonu gecersiz. |
| INV-AT-002 | Flow not found | Bu tenant icin chatbot akisi tanimlanmamis. |
| INV-AT-003 | FAQ not found | SSS kaydi bulunamadi. |
| INV-AT-004 | Intent detection failed | Niyet algilama servisi hatasi. |
| INV-AT-005 | Session expired | Sohbet oturumu sona erdi. |
| INV-AT-006 | Flow validation failed | Chatbot akis dogrulamasi basarisiz. |
| INV-AT-007 | Flow not found by ID | Belirtilen chatbot akisi bulunamadi. |
| INV-AT-008 | Flow activation conflict | Bu tenant icin zaten aktif bir akis var. |
| INV-AT-009 | Invalid flow config version | Desteklenmeyen akis konfigurasyonu versiyonu. |
| INV-AT-010 | Invalid API key | Gecersiz API anahtari. |
| INV-AT-011 | Max loop count exceeded | Sonsuz dongu limiti asildi, node: {node_id} |
| INV-AT-012 | Unknown node type | Desteklenmeyen node tipi: {type} |
| INV-AT-013 | No pending input expected | Beklenmeyen kullanici girdisi |
| INV-AT-014 | Unknown input type | Bilinmeyen girdi tipi: {type} |
| INV-AT-015 | Graph validation failed | Akis dogrulamasi basarisiz: {reason} |
| INV-AT-016 | Required field missing | Zorunlu alan eksik, node '{node_id}': {field} |
| INV-AT-017 | Expression evaluation failed | Ifade degerlendirme hatasi, node '{node_id}': {reason} |
| INV-AT-018 | Simulation session not found | Simulasyon oturumu bulunamadi. |
| INV-AT-019 | Simulation session expired | Simulasyon oturumunun suresi doldu. |
| INV-AT-020 | Flow not found for simulation | Simulasyon icin akis bulunamadi. |
| INV-AT-021 | Node execution failed | Node calisma hatasi ({node_id}): {reason} |
| INV-AT-022 | API call SSRF blocked | API adresi guvenlik kontrolunden gecemedi (dahili adresler engellenmistir). |
| INV-AT-023 | API call timeout | API cagrisi zaman asimina ugradi ({timeout_ms}ms). |
| INV-AT-024 | API call HTTP error | API cagrisi HTTP hatasi ({status_code}): {reason} |
| INV-AT-025 | Knowledge intent fetch failed | Intent bilgileri alinamadi, varsayilan intent seti kullaniliyor. |
| INV-AT-026 | VIP detection failed | VIP/B2B tespit islemi basarisiz (akis etkilenmez). |
| INV-AT-027 | Return deflection failed | Iade deflection islemi basarisiz. |
| INV-AT-028 | Return reason classify failed | Iade nedeni siniflandirma basarisiz. |
| INV-AT-029 | Coupon assign failed | Kupon atama basarisiz. |
| INV-AT-030 | Webhook flow not found | Webhook icin akis bulunamadi. |
| INV-AT-031 | Webhook flow not webhook_trigger type | Bu akis webhook ile tetiklenemez. |
| INV-AT-032 | Webhook execution failed | Webhook akis yurutmesi basarisiz. |
| INV-AT-033 | Cron expression invalid | Gecersiz cron ifadesi. |
| INV-AT-034 | Schedule execution failed | Zamanlama akis yurutmesi basarisiz. |

---

## AA - AgentAI Errors

| Code | Description | User Message |
|------|-------------|--------------|
| INV-AA-001 | Invalid request payload | Gecersiz istek formati. |
| INV-AA-002 | Reply generation failed | AI cevap onerisi olusturulamadi. |
| INV-AA-003 | Intent detection failed | Niyet algilama basarisiz. |
| INV-AA-004 | No conversation context | Sohbet gecmisi saglanmadi. |
| INV-AA-005 | Claude API timeout | AI servisi zaman asimina ugradi. |
| INV-AA-006 | Invalid feedback payload | Gecersiz geri bildirim formati. |
| INV-AA-007 | Knowledge service unavailable | Bilgi bankasi servisi gecici olarak kullanilamiyor (oneri uretildi, kaynak referansi yok). |
| INV-AA-008 | Language detection failed | Dil algilama basarisiz, varsayilan dil kullanildi. |
| INV-AA-009 | Conversation summary failed | Konusma ozeti olusturulamadi, ham gecmis kullanildi. |

---

## DB - Database Errors

| Code | Description | User Message |
|------|-------------|--------------|
| INV-DB-001 | Connection failed | Veritabanı bağlantısı kurulamadı. |
| INV-DB-002 | Query timeout | Sorgu zaman aşımına uğradı. |
| INV-DB-003 | Duplicate entry | Bu kayıt zaten mevcut. |

---

## VAL - Validation Errors

| Code | Description | User Message |
|------|-------------|--------------|
| INV-VAL-001 | Invalid format | Geçersiz format: {field} |
| INV-VAL-002 | Required field | Zorunlu alan: {field} |
| INV-VAL-003 | Out of range | Değer geçerli aralıkta değil: {field} |

---

## INT - Integration Errors (GR-1.9)

| Code | Description | User Message |
|------|-------------|--------------|
| INV-INT-001 | Webhook payload invalid | Geçersiz webhook formatı. |
| INV-INT-002 | Callback to Main App failed | Main App'e bildirim gönderilemedi. |
| INV-INT-003 | Unknown webhook event type | Bilinmeyen event tipi. |
| INV-INT-004 | Tenant not found in registry | Bu tenant kayıtlı değil. |

---

## OB - Outbound Errors (GR-1.3)

| Code | Description | User Message |
|------|-------------|--------------|
| INV-OB-001 | Invalid broadcast payload | Gecersiz toplu mesaj istegi. |
| INV-OB-002 | Template not found | Mesaj sablonu bulunamadi. |
| INV-OB-003 | Rate limit exceeded (queued) | Gonderim limiti asildi, mesajlar kuyrukta bekliyor. |
| INV-OB-004 | Recipient opted out | Alici mesaj almak istemiyor (opt-out). |
| INV-OB-005 | Broadcast not found | Toplu mesaj kaydi bulunamadi. |
| INV-OB-006 | Delivery status update failed | Teslimat durumu guncellenemedi. |
| INV-OB-007 | Invalid template payload | Gecersiz sablon formati. |
| INV-OB-008 | No matching trigger template | Bu event icin esle&#351;en sablon bulunamadi. |
| INV-OB-009 | Message send callback failed | Mesaj gonderim callback'i basarisiz oldu. |
| INV-OB-010 | Too many recipients (max 1000) | Alici sayisi siniri asildi (max 1000). |
| INV-OB-011 | Template language not available | Istenen dilde sablon bulunamadi, varsayilan dil kullanildi. |
| INV-OB-012 | No template for language | Bu dilde sablon tanimlanmamis. |
| INV-OB-013 | Invalid campaign payload | Gecersiz kampanya istegi. |
| INV-OB-014 | Campaign not found | Kampanya bulunamadi. |
| INV-OB-015 | Campaign already active | Kampanya zaten aktif/zamanlanmis. |
| INV-OB-016 | Conversion record failed | Donusum kaydi olusturulamadi. |
| INV-OB-017 | AI personalization unavailable | AI kisisellistirme servisi kullanilamiyor. |
| INV-OB-018 | Consent not given | Alici pazarlama izni vermemis. |
| INV-OB-019 | Invalid consent payload | Gecersiz izin kaydi istegi. |
| INV-OB-020 | Data deletion failed | Veri silme islemi basarisiz oldu. |

---

## IG - Integrations Errors (GR-3.4/3.6)

| Code | Description | User Message |
|------|-------------|--------------|
| INV-IG-001 | Invalid account payload | Gecersiz entegrasyon hesabi istegi. |
| INV-IG-002 | Account not found | Entegrasyon hesabi bulunamadi. |
| INV-IG-003 | Provider sync failed | Saglayici senkronizasyonu basarisiz. |
| INV-IG-004 | Order not found | Siparis bulunamadi. |
| INV-IG-005 | Provider connection failed | Saglayici baglanti testi basarisiz. |
| INV-IG-006 | Invalid order query | Gecersiz siparis sorgusu. |
| INV-IG-007 | Cargo tracking unavailable | Kargo takip bilgisi kullanilamiyor. |

---

## AP - Appointments Errors (GR-2.4)

| Code | Description | User Message |
|------|-------------|--------------|
| INV-AP-001 | Invalid slot payload | Gecersiz slot tanimlama istegi. |
| INV-AP-002 | Slot not found | Randevu slotu bulunamadi. |
| INV-AP-003 | Invalid booking payload | Gecersiz randevu istegi. |
| INV-AP-004 | Slot fully booked | Bu slot dolu, baska bir zaman secin. |
| INV-AP-005 | Appointment not found | Randevu bulunamadi. |
| INV-AP-006 | Already cancelled | Randevu zaten iptal edilmis. |
| INV-AP-007 | Invalid date/time | Gecersiz tarih veya saat. |
| INV-AP-008 | Booking in the past | Gecmis tarihli randevu alinamaz. |
| INV-AP-009 | Reminder send failed | Hatirlatma mesaji gonderilemedi. |
| INV-AP-010 | Outbound service unavailable | Mesaj gonderim servisi gecici olarak kullanilamiyor. |
| INV-AP-011 | Invalid waitlist payload | Gecersiz bekleme listesi istegi. |
| INV-AP-012 | Waitlist entry not found | Bekleme listesi kaydi bulunamadi. |
| INV-AP-013 | Invalid pricing payload | Gecersiz fiyat tanimlama istegi. |
| INV-AP-014 | Pricing not found | Fiyat kaydi bulunamadi. |
| INV-AP-015 | Calendar sync failed | Takvim senkronizasyon hatasi. |
| INV-AP-016 | Invalid lifecycle payload | Gecersiz tedavi takip istegi. |
| INV-AP-017 | Lifecycle not found | Tedavi takip kaydi bulunamadi. |
| INV-AP-018 | Lifecycle already finished | Tedavi takip sureci zaten tamamlanmis veya iptal edilmis. |
| INV-AP-019 | Invalid lifecycle type | Gecersiz takip tipi (post_treatment, plan_approval, pre_op). |
| INV-AP-020 | Lifecycle step send failed | Takip mesaji gonderilemedi. |

---

## AD - Attribution Errors (GR-3.14)

| Code | Description | User Message |
|------|-------------|--------------|
| INV-AD-001 | Invalid attribution payload | Gecersiz attribution istegi. |
| INV-AD-002 | Attribution not found | Attribution kaydi bulunamadi. |
| INV-AD-003 | Invalid cost entry | Gecersiz reklam maliyeti girisi. |
| INV-AD-004 | Cost not found | Reklam maliyeti kaydi bulunamadi. |
| INV-AD-005 | Invalid lead status update | Gecersiz lead durum guncellemesi. |

---

## MK - Marketing Errors (GR-3.21/3.22)

| Code | Description | User Message |
|------|-------------|--------------|
| INV-MK-001 | Invalid review request payload | Gecersiz yorum talebi istegi. |
| INV-MK-002 | Review request not found | Yorum talebi bulunamadi. |
| INV-MK-003 | Invalid referral payload | Gecersiz referans istegi. |
| INV-MK-004 | Referral not found | Referans kaydi bulunamadi. |
| INV-MK-005 | Referral code already exists | Referans kodu zaten mevcut (tekrar deneyin). |
| INV-MK-006 | Invalid tourism lead payload | Gecersiz medikal turizm lead istegi. |
| INV-MK-007 | Tourism lead not found | Medikal turizm lead bulunamadi. |
| INV-MK-008 | Invalid tourism lead status | Gecersiz lead durumu. |
| INV-MK-009 | Review stats query failed | Yorum istatistikleri sorgusu basarisiz. |
| INV-MK-010 | Tourism stats query failed | Turizm istatistikleri sorgusu basarisiz. |
| INV-MK-011 | Invalid risk assessment payload | Gecersiz risk degerlendirmesi istegi. |
| INV-MK-012 | Review risk not found | Risk kaydi bulunamadi. |
| INV-MK-013 | Invalid risk/rescue status | Gecersiz risk veya kurtarma durumu. |
| INV-MK-014 | Rescue stats query failed | Kurtarma istatistikleri sorgusu basarisiz. |
| INV-MK-015 | Invalid rescue template payload | Gecersiz kurtarma sablonu istegi. |
| INV-MK-016 | Rescue template not found | Kurtarma sablonu bulunamadi. |
| INV-MK-017 | Invalid treatment catalog payload | Gecersiz tedavi katalogu istegi. |
| INV-MK-018 | Treatment catalog item not found | Tedavi katalogu kaydi bulunamadi. |
| INV-MK-019 | Invalid conversation payload | Gecersiz konusma istegi. |
| INV-MK-020 | Tourism conversation not found | Turizm konusmasi bulunamadi. |
| INV-MK-021 | Conversation stats query failed | Konusma istatistikleri sorgusu basarisiz. |
| INV-MK-022 | Response generation failed | Cok dilli cevap uretimi basarisiz. |
| INV-MK-023 | Claude AI service unavailable | Claude AI servisi kullanilamiyor. |

---

## KN - Knowledge Errors (GR-2.1)

| Code | Description | User Message |
|------|-------------|--------------|
| INV-KN-001 | Import path not found | Belirtilen NLP dosya yolu bulunamadi. |
| INV-KN-002 | Import parse error | Dosya parse hatasi (JSON/CSV). |
| INV-KN-003 | Import DB error | Veritabani kayit hatasi. |
| INV-KN-004 | Search failed | Arama sirasinda hata olustu. |
| INV-KN-005 | OpenAI timeout | Embedding servisi zaman asimi (anahtar kelime aramasina gecildi). |
| INV-KN-006 | OpenAI rate limit | Embedding rate limiti asildi (anahtar kelime aramasina gecildi). |
| INV-KN-007 | OpenAI API error | Embedding servisi hatasi. |
| INV-KN-008 | FAQ not found | Belirtilen FAQ bulunamadi. |
| INV-KN-009 | Invalid request | Gecersiz istek formati. |
| INV-KN-010 | pgvector missing | pgvector eklentisi yuklu degil (sunucu hatasi). |
| INV-KN-011 | File too large | Dosya boyutu siniri asildi. |
| INV-KN-012 | Invalid file type | Desteklenmeyen dosya formati. |
| INV-KN-013 | PDF extraction failed | PDF icerik cikarma hatasi. |
| INV-KN-014 | Document not found | Dokuman bulunamadi. |
| INV-KN-015 | Upload failed | Dosya yukleme hatasi. |
| INV-KN-016 | Photo blocked (health tenant) | Saglik tenant'lari icin hasta fotografi yuklemesi engellendi (KVKK). |
| INV-KN-017 | Intent patterns not found | Bu tenant icin intent tanimlari bulunamadi. |
| INV-KN-018 | Intent read failed | Intent tanimlari okunurken hata olustu. |

---

## MT - Metrics/Analytics Errors (PKT-3)

| Code | Description | User Message |
|------|-------------|--------------|
| INV-MT-001 | Metrics aggregation failed | Metrik toplama hatasi. Bir sonraki periyotta tekrar denenecek. |
| INV-MT-002 | Analytics query failed | Analitik sorgusu basarisiz oldu. |
| INV-MT-003 | Invalid date range | Gecersiz tarih araligi (baslangic > bitis veya negatif). |

---

## EXT - External Service Errors

| Code | Description | User Message |
|------|-------------|--------------|
| INV-EXT-001 | External API error | Dış servis hatası. |
| INV-EXT-002 | External timeout | Dış servis yanıt vermedi. |

---

## Yeni Kod Ekleme

1. Servis kodunu belirle (GEN, BE, CA, etc.)
2. Sonraki boş numarayı bul (001, 002, etc.)
3. Bu dosyaya ekle
4. `ErrorCodes.cs` dosyasına ekle
5. Kodda kullan

## ErrorCodes.cs Örneği

```csharp
public static class ErrorCodes
{
    // General errors
    public const string GeneralUnknown = "INV-GEN-001";
    public const string GeneralTimeout = "INV-GEN-002";

    // Backend errors
    public const string BackendMicroserviceUnavailable = "INV-BE-001";
    public const string BackendMicroserviceTimeout = "INV-BE-002";
}
```
