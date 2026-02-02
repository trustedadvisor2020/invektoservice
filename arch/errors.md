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
| AUTH | Auth | Authentication hataları |
| DB | Database | Veritabanı hataları |
| VAL | Validation | Validasyon hataları |
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

---

## CA - ChatAnalysis Errors

| Code | Description | User Message |
|------|-------------|--------------|
| INV-CA-001 | Invalid payload | Geçersiz istek formatı. |
| INV-CA-002 | Processing failed | Analiz işlemi başarısız oldu. |

---

## AUTH - Authentication Errors

| Code | Description | User Message |
|------|-------------|--------------|
| INV-AUTH-001 | Token expired | Oturumunuz sona erdi. Lütfen tekrar giriş yapın. |
| INV-AUTH-002 | Invalid token | Geçersiz oturum. |
| INV-AUTH-003 | Unauthorized | Bu işlem için yetkiniz bulunmuyor. |

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
