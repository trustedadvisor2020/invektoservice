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
| OB | Outbound | GR-1.3: Broadcast & trigger engine hataları |
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
