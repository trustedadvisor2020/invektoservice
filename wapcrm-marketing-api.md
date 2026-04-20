# WapCRM API — Dinamik Mesaj & Marketing Opt-Out Dokümanı

Bu doküman WapCRM.API'nin **dinamik mesaj içeriği** ve **marketing opt-out** özelliklerini açıklar. Dış projeler ve entegrasyonlar için referans amaçlıdır.

- **Base URL:** `https://cxapi.wapcrm.net`
- **Auth:** Tüm endpoint'ler `X-CIB-SecretKey` header ile kimlik doğrular
- **Content-Type:** `application/json`

---

## İçindekiler
1. [Mesaj Gönderme (chatoperation)](#1-mesaj-gönderme-chatoperation)
2. [Dinamik Mesaj](#2-dinamik-mesaj)
3. [Kullanılabilir Dinamik Alanlar](#3-kullanılabilir-dinamik-alanlar-dynamicfields)
4. [Marketing Opt-Out Kontrolü (MessageCategory)](#4-marketing-opt-out-kontrolü-messagecategory)
5. [Opt-Out Yönetim Endpoint'leri](#5-opt-out-yönetim-endpointleri)
6. [Hata Kodları Referans Tablosu](#6-hata-kodları-referans-tablosu)

---

## 1. Mesaj Gönderme (chatoperation)

Müşterilerinize mesaj göndermek için kullanılan ana endpoint.

### Endpoint
```
POST https://cxapi.wapcrm.net/api/chatoperation
```

### Header
```
X-CIB-SecretKey: <api_key>
Content-Type: application/json
```

### Request Body
```json
{
  "InstanceID": 101,
  "UserID": 5,
  "ChatPhoneNumber": "90111223344",
  "ChatAccountID": "",
  "MessageType": 1,
  "MessageText": "Merhaba, projemiz hakkında bilgi almak ister misiniz?",
  "FileUrl": "",
  "FileName": "",
  "InCom": 0,

  "DynamicMessage": false,
  "DynamicMessageFields": [],

  "MessageCategory": null
}
```

### Alan Açıklamaları

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|---------|----------|
| `InstanceID` | int | Evet | Kanal (instance) ID'si |
| `UserID` | int | Evet | Mesajı atan kullanıcı ID'si |
| `ChatPhoneNumber` | string | WhatsApp için | Alıcının telefon numarası (WhatsApp/Voip/SMS) |
| `ChatAccountID` | string | Sosyal için | Alıcının hesap ID'si (Instagram/Telegram/Facebook) |
| `MessageType` | int | Evet | Mesaj tipi |
| `MessageText` | string | Evet | Mesaj içeriği |
| `FileUrl` | string | Dosyalı ise | Gönderilecek dosyanın URL'i |
| `FileName` | string | Dosyalı ise | Dosya adı |
| `InCom` | int | Evet | Entegrasyon kimliği (WapCRM için `0`, Zoho için `4`) |
| `DynamicMessage` | bool | Hayır | Dinamik mesaj özelliği aktif mi (default `false`) |
| `DynamicMessageFields` | string[] | Dinamik ise | Mesajda kullanılacak placeholder alanları |
| `MessageCategory` | string | Hayır | `"marketing"` veya `"transactional"` (opt-out kontrolü için) |

### Başarılı Yanıt
```json
{
  "Status": true,
  "Message": "Success",
  "StatusCode": "0",
  "RequestID": "b3f8e2a1-9c4d-4e7f-8a2b-1d5c6e7f8a9b",
  "Data": "<chat_server_message_id>"
}
```

### Hata Yanıtı (örnek)
```json
{
  "Status": false,
  "Message": "ACCESS DENIED",
  "StatusCode": "506",
  "RequestID": "c4a9d3b2-8e5f-4a1c-9b3d-2e6f7a8b9c0d"
}
```

---

## 2. Dinamik Mesaj

`MessageText` içine `{{alan_adi}}` formatında placeholder yazarak kişiselleştirilmiş mesaj gönderme özelliği.

### Nasıl Çalışır
1. `DynamicMessage: true` gönderilir
2. `DynamicMessageFields` içinde kullanılacak alan adları listelenir
3. Sistem alıcının Customer bilgilerinden değerleri çeker ve placeholder'ları değiştirir
4. ChatServer'a temizlenmiş (placeholder'sız) mesaj gider

### Örnek Request
```json
{
  "InstanceID": 101,
  "UserID": 5,
  "ChatPhoneNumber": "90111223344",
  "MessageType": 1,
  "MessageText": "Merhaba {{name}}, {{datalistname}} listesinden size özel bir teklifimiz var!",
  "InCom": 0,
  "DynamicMessage": true,
  "DynamicMessageFields": ["name", "datalistname"]
}
```

Gönderilecek fiili metin (örnek):
> "Merhaba Hamdi Yılmaz, Kadıköy Konakları listesinden size özel bir teklifimiz var!"

### Desteklenen Placeholder'lar

| Placeholder | Kaynak | Açıklama |
|-------------|--------|----------|
| `{{name}}` | `Customer.Name` | Müşteri adı |
| `{{email}}` | `Customer.Email` | E-posta |
| `{{note}}` | `Customer.Note` | Not |
| `{{pushname}}` | `Customer.PushName` | WhatsApp profil adı |
| `{{datalistname}}` | `DataList.Name` | Müşterinin bağlı olduğu data list adı |
| `{{cf1}}` – `{{cf10}}` | `Customer.CF1`–`CF10` | Özel alanlar (tenant-specific; etiketleri `/api/dynamicfields` ile alınır) |

### Önemli Kurallar
- `DynamicMessage: true` ise `DynamicMessageFields` **boş olamaz**
- Listedeki her alan için `MessageText` içinde en az bir `{{alan}}` placeholder'ı **olmalı**
- Desteklenmeyen bir alan gönderilirse hata döner (901)
- Müşteri kaydı bulunamazsa hata döner (903)
- Alanın değeri müşteride boş/NULL ise hata döner (905)
- Placeholder eşleştirme **büyük/küçük harf duyarlı değildir**
- WhatsApp için `CustomerPhones.Phone` ile, sosyal için `CustomerSocialMediaAccounts.AccountID + InstanceType` ile müşteri bulunur

### Örnek Hata Yanıtı (alan değeri yok)
```json
{
  "Status": false,
  "Message": "Dynamic field 'email' has no value for this customer",
  "StatusCode": "905",
  "RequestID": "..."
}
```

---

## 3. Kullanılabilir Dinamik Alanlar (dynamicfields)

Dinamik mesajda kullanılabilecek tüm alanları (sabit + tenant'a özel CF'ler) döner.

### Endpoint
```
POST https://cxapi.wapcrm.net/api/dynamicfields
GET  https://cxapi.wapcrm.net/api/dynamicfields
```

### Header
```
X-CIB-SecretKey: <api_key>
```

### Request
Body yok (veya boş). API key header'dan firma tespit edilir.

### Başarılı Yanıt
```json
{
  "Status": true,
  "Message": "Success",
  "StatusCode": "0",
  "RequestID": "...",
  "Data": [
    { "FieldKey": "name", "FieldName": "Müşteri Adı" },
    { "FieldKey": "email", "FieldName": "E-Posta" },
    { "FieldKey": "note", "FieldName": "Not" },
    { "FieldKey": "pushname", "FieldName": "Push Name" },
    { "FieldKey": "datalistname", "FieldName": "Data List" },
    { "FieldKey": "cf1", "FieldName": "Şehir" },
    { "FieldKey": "cf2", "FieldName": "Firma Adı" }
  ]
}
```

- `FieldKey` → placeholder içinde kullanılır (`{{cf1}}`)
- `FieldName` → UI'da kullanıcıya gösterilecek etiket
- Sabit alanlar (`name`, `email`, `note`, `pushname`, `datalistname`) her tenant için dönulur
- `cf1`-`cf10` sadece tenant DB'deki `CustomFields` tablosunda **aktif** olanlar için döner

---

## 4. Marketing Opt-Out Kontrolü (MessageCategory)

Alıcılar, pazarlama mesajı almak istemediklerini belirtebilir. Bu durumda `chatoperation` gönderimleri otomatik olarak bloke edilir — fakat **sadece** `MessageCategory` açıkça gönderilirse.

### MessageCategory Davranışı

| Değer | Davranış |
|-------|----------|
| *(gönderilmedi)* / `null` | **Hiçbir opt-out kontrolü yapılmaz**, ek DB sorgusu atılmaz — eski istemci davranışı |
| `"marketing"` | Opt-out flag'leri kontrol edilir; bloklu ise 906 veya 907 döner |
| `"transactional"` | Kontrol atlanır; opt-out olsa bile mesaj gider (sipariş, randevu hatırlatma vb.) |

> Geriye tam uyumludur. Mevcut istemciler hiçbir değişiklik yapmazlarsa eskisi gibi çalışmaya devam eder.

### Kontrol Mantığı (marketing gönderirken)
1. İlgili Chat kaydında `IsMarketingBlocked = 1` ise → **906** döner
2. İlgili Contact (CustomerPhones veya CustomerSocialMediaAccounts) `IsMarketingBlocked = 1` ise → **907** döner
3. Değilse mesaj gönderilir

### Invariant (DB tutarlılığı)
"Bir kanal açıksa kişi tamamen kapalı değildir." Contact-level flag (`907`) sadece o kişinin **hiçbir kanalından** mesaj alamayacağı durumu temsil eder. Tek bir kanal açılırsa (`/api/optin` scope=channel) global flag otomatik olarak 0'a çekilir.

### Örnek — Marketing Mesaj Gönderme
```json
{
  "InstanceID": 101,
  "UserID": 5,
  "ChatPhoneNumber": "90111223344",
  "MessageType": 1,
  "MessageText": "%50 indirim fırsatı!",
  "InCom": 0,
  "MessageCategory": "marketing"
}
```

**Eğer bu numara/chat opt-out'luysa dönen yanıt:**
```json
{
  "Status": false,
  "Message": "Chat is blocked for marketing messages",
  "StatusCode": "906",
  "RequestID": "..."
}
```
veya global bloklu ise:
```json
{
  "Status": false,
  "Message": "Contact is blocked for marketing messages",
  "StatusCode": "907",
  "RequestID": "..."
}
```

### Örnek — Transactional Mesaj (Opt-Out Bypass)
```json
{
  "InstanceID": 101,
  "UserID": 5,
  "ChatPhoneNumber": "90111223344",
  "MessageType": 1,
  "MessageText": "Randevunuz 21.04.2026 14:00'da. Değişiklik için aramanız yeterlidir.",
  "InCom": 0,
  "MessageCategory": "transactional"
}
```
Opt-out flag'i olsa bile 200 OK döner ve mesaj iletilir.

---

## 5. Opt-Out Yönetim Endpoint'leri

Contact'ların pazarlama mesajlarına kapalı olma durumunu yönetir. İki kapsam (scope) destekler:

- **`all`** → kişinin o iletişim kanalındaki **tüm chat'leri** (tüm instance'lar) bloke edilir; Contact-level global flag de set edilir
- **`channel`** → sadece belirtilen `InstanceID`'deki chat bloke edilir; global flag etkilenmez (ya da açık tutulur — aşağıya bak)

### 5.1 Opt-Out — `POST /api/optout`

#### Endpoint
```
POST https://cxapi.wapcrm.net/api/optout
```

#### Header
```
X-CIB-SecretKey: <api_key>
```

#### Request Body
```json
{
  "IdentifierType": "phone",
  "Identifier": "90111223344",
  "InstanceID": 101,
  "Scope": "all",
  "Reason": "customer_request",
  "Source": "api"
}
```

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|---------|----------|
| `IdentifierType` | string | Evet | `"phone"` veya `"social"` |
| `Identifier` | string | Evet | Phone ise telefon numarası, social ise AccountID |
| `InstanceID` | int | Evet | İlgili kanal ID; InstanceType buradan türetilir |
| `Scope` | string | Evet | `"all"` (tüm kanallar) veya `"channel"` (sadece bu kanal) |
| `Reason` | string | Hayır | Sebep metni (audit için saklanır) |
| `Source` | string | Hayır | Kaynak (`"customer_request"`, `"manual_admin"`, `"api"`, `"webhook"` vb.) |

#### Başarılı Yanıt (yeni opt-out, scope=all, 3 chat etkilendi)
```json
{
  "Status": true,
  "Message": "Success",
  "StatusCode": "0",
  "RequestID": "b3f8e2a1-9c4d-4e7f-8a2b-1d5c6e7f8a9b",
  "Data": {
    "IdentifierType": "phone",
    "Identifier": "90111223344",
    "Scope": "all",
    "IsMarketingBlocked": true,
    "MarketingBlockedAt": "2026-04-19T14:32:18",
    "MarketingUnblockedAt": null,
    "MarketingBlockReason": "customer_request",
    "MarketingBlockSource": "api",
    "AffectedChatCount": 3,
    "AlreadyOptedOut": false,
    "WasOptedOut": false
  }
}
```

#### Idempotent Yanıt (zaten opted-out)
```json
{
  "Status": true,
  "Message": "Already opted out",
  "StatusCode": "909",
  "RequestID": "...",
  "Data": {
    "IdentifierType": "phone",
    "Identifier": "90111223344",
    "Scope": "all",
    "IsMarketingBlocked": true,
    "MarketingBlockedAt": "2026-04-19T14:32:18",
    "MarketingUnblockedAt": null,
    "MarketingBlockReason": "customer_request",
    "MarketingBlockSource": "api",
    "AffectedChatCount": 0,
    "AlreadyOptedOut": true,
    "WasOptedOut": false
  }
}
```
- `AlreadyOptedOut: true` → yeni bir şey güncellenmedi
- `Reason` ve `Source` **ilk opt-out**'taki değerlerdir (override EDİLMEZ — audit korunur)
- `AffectedChatCount: 0` → hiçbir chat güncellenmedi

#### Hata: Kişi Bulunamadı
```json
{
  "Status": false,
  "Message": "Contact not found",
  "StatusCode": "908",
  "RequestID": "..."
}
```

#### Hata: Validation
```json
{
  "Status": false,
  "Message": "Invalid Scope (all | channel)",
  "StatusCode": "401",
  "RequestID": "..."
}
```

#### Opt-In Sonrası Yeni Opt-Out Davranışı
Kişi daha önce opt-in yapmış (açık) sonra tekrar opt-out geldi:
- `MarketingBlockedAt` → **yeni tarih** (güncellenir)
- `MarketingUnblockedAt` → **korunur** (önceki izin tarihi silinmez, audit için)
- `MarketingBlockReason`, `MarketingBlockSource` → yeni değerlerle güncellenir

---

### 5.2 Opt-In — `POST /api/optin`

Opt-out'u geri alır (flag'i kaldırır).

#### Request Body
Opt-out ile aynı yapı. `Reason` ve `Source` opsiyonel, kullanılmaz (ancak gönderilirse hata olmaz).

```json
{
  "IdentifierType": "phone",
  "Identifier": "90111223344",
  "InstanceID": 101,
  "Scope": "channel"
}
```

#### Başarılı Yanıt (kişi önceden opt-out'tu)
```json
{
  "Status": true,
  "Message": "Success",
  "StatusCode": "0",
  "RequestID": "...",
  "Data": {
    "IdentifierType": "phone",
    "Identifier": "90111223344",
    "Scope": "channel",
    "IsMarketingBlocked": false,
    "MarketingBlockedAt": null,
    "MarketingUnblockedAt": "2026-04-19T15:10:45",
    "MarketingBlockReason": null,
    "MarketingBlockSource": null,
    "AffectedChatCount": 1,
    "AlreadyOptedOut": false,
    "WasOptedOut": true
  }
}
```
- `WasOptedOut: true` → opt-out durumundan geri döndü
- `MarketingUnblockedAt` → opt-in tarihi (audit için)

#### No-op Yanıt (zaten açıktı)
```json
{
  "Status": true,
  "Message": "Success",
  "StatusCode": "0",
  "Data": {
    "WasOptedOut": false,
    "AffectedChatCount": 0
  }
}
```

#### Önemli Davranış — Channel Opt-In Global'i de Açar
`scope: "channel"` ile opt-in yapıldığında, eğer Contact-level global flag blokluysa o da otomatik olarak 0'a çekilir (DB tutarlılığı için). Diğer chat'ler kendi `IsMarketingBlocked = 1` flag'leriyle bloklu kalmaya devam eder.

**Senaryo:**
```
T0  - scope=all opt-out  → global=1, chat1=1, chat2=1, chat3=1
T1  - channel opt-in (chat2) → global=0, chat1=1, chat2=0, chat3=1
                                 ↑
                                 Otomatik olarak açıldı (tutarlılık)
```

---

### 5.3 Opt-Out Status — `POST /api/optout/status`

Kişinin global ve kanal bazlı durumunu sorgular.

#### Request Body
```json
{
  "IdentifierType": "phone",
  "Identifier": "90111223344",
  "InstanceID": 101
}
```
- `Scope` bu endpoint için zorunlu değil
- `InstanceID` social için hâlâ zorunlu (InstanceType türetmek için)

#### Başarılı Yanıt
```json
{
  "Status": true,
  "Message": "Success",
  "StatusCode": "0",
  "RequestID": "...",
  "Data": {
    "IdentifierType": "phone",
    "Identifier": "90111223344",
    "IsGlobalBlocked": false,
    "GlobalBlockedAt": "2026-04-10T10:00:00",
    "GlobalUnblockedAt": "2026-04-19T15:10:45",
    "GlobalBlockReason": "customer_request",
    "GlobalBlockSource": "api",
    "Channels": [
      {
        "InstanceID": 101,
        "InstanceName": "Kadıköy Konakları",
        "InstanceType": "Whatsapp",
        "IsMarketingBlocked": false,
        "MarketingBlockedAt": "2026-04-10T10:00:00",
        "MarketingUnblockedAt": "2026-04-19T15:10:45",
        "MarketingBlockReason": "customer_request",
        "MarketingBlockSource": "api"
      },
      {
        "InstanceID": 102,
        "InstanceName": "Tuzla Marina Evleri",
        "InstanceType": "Whatsapp",
        "IsMarketingBlocked": true,
        "MarketingBlockedAt": "2026-04-10T10:00:00",
        "MarketingUnblockedAt": null,
        "MarketingBlockReason": "customer_request",
        "MarketingBlockSource": "api"
      }
    ]
  }
}
```

---

## 6. Hata Kodları Referans Tablosu

Tüm hata kodları `/help/statuscodes` endpoint'inden de alınabilir.

### Genel (200-500)

| Kod | Açıklama |
|-----|----------|
| 400 | API key yok |
| 401 | Model geçersiz |
| 404 | Parametreler uygun formatta değil |
| 405 | Müşterinin chat server'ına ulaşılamadı |
| 408 | Chat server aktif değil |
| 409 | `ChatAccountID` boş (sosyal kanallar için) |
| 500 | API key bulunamadı |
| 501 | API durumu pasif |
| 502 | Firma durumu pasif |
| 503 | Belirtilen kullanıcı bulunamadı |
| 505 | Firmanın kanal listesi yok |
| 506 | Firmaya ait belirtilen InstanceID yok |
| 507 | Firmaya ait aktif kanal yok |
| 508 | Firmada tanımlı IP adresi yok |
| 509 | Request yapılan IP tanımlı IP'lerde yok |
| 510 | Günlük mesaj adedi tanımlı değil |
| 511 | Dakikalık mesaj adedi tanımlı değil |
| 513 | Kullanıcının tanımlı kanalı yok |
| 514 | Kullanıcının bu kanala yetkisi yok |

### Mesaj Limitleri (300)

| Kod | Açıklama |
|-----|----------|
| 301 | Günlük mesaj sayısına ulaşıldı |
| 302 | Dakikalık mesaj sayısına ulaşıldı |

### Mesaj İçeriği (600)

| Kod | Açıklama |
|-----|----------|
| 600 | Desteklenmeyen mesaj tipi |
| 601 | Mesaj metni 1000 karakterden fazla |
| 602 | Dosya URL'i yok |
| 603 | Dosya indirilemedi |

### Dinamik Mesaj (900-905)

| Kod | Açıklama |
|-----|----------|
| 900 | `DynamicMessageFields` boş (DynamicMessage=true ama alan listesi gönderilmedi) |
| 901 | Desteklenmeyen dinamik alan adı |
| 902 | Mesaj metninde belirtilen placeholder bulunamadı |
| 903 | Müşteri bulunamadı (telefon/hesap ile eşleşme yok) |
| 905 | Alanın değeri müşteride boş/NULL |

### Marketing Opt-Out (906-909)

| Kod | Açıklama |
|-----|----------|
| 906 | Bu chat pazarlama mesajlarına kapalı (chat-level block) |
| 907 | Kişi tüm kanallarda pazarlama mesajlarına kapalı (contact-level global block) |
| 908 | Opt-out için kişi bulunamadı |
| 909 | Zaten opted-out durumunda (idempotent yanıt, yeni işlem yapılmadı) |

### Sistem (911)

| Kod | Açıklama |
|-----|----------|
| 911 | Sistem hatası |

---

## 7. Entegrasyon Önerileri (Agent'lara Not)

1. **Geriye uyumluluk:** Mevcut `chatoperation` çağrılarına `MessageCategory` eklemek zorunda değilsiniz; eklemezseniz opt-out kontrolü çalışmaz. Sadece pazarlama mesajı atıyorsanız `"marketing"` ekleyin.

2. **Transactional olanları işaretleyin:** Randevu, sipariş, şifre bilgilendirmesi gibi mesajlara `MessageCategory: "transactional"` ekleyerek opt-out'tan etkilenmelerini engelleyin.

3. **Opt-out akışı:** Kullanıcı "beni listeden çıkar" dediğinde:
   - Tercihi tek kanal ise → `scope: "channel"` ile `/api/optout`
   - Tüm kanallar ise → `scope: "all"` ile `/api/optout`
   - Yanıttaki `AffectedChatCount` kaç chat etkilendiğini gösterir

4. **Idempotent çağrılar:** `/api/optout` aynı kişi için tekrar çağrılırsa 200 + `AlreadyOptedOut: true` döner — retry mekanizması güvenle kullanılabilir.

5. **Dinamik mesajda önce field listesini çekin:** `/api/dynamicfields` ile UI'da kullanıcıya gösterilecek alan listesi alınır; tenant'ın CustomField yapılandırmasına göre değişkendir.

6. **Audit bilgisi:** Opt-out/opt-in tarihleri silinmez; `MarketingBlockedAt` ve `MarketingUnblockedAt` bilgileri Contact ve Chat satırlarında audit için korunur.

7. **Hata yakalama:** `Status == false` olduğunda `StatusCode` üzerinden ayrım yapın. 906/907 retry yapmaya değmez (kullanıcı flag'i kaldırmadıkça bloke kalır). 405/408/911 geçici sistem hatası olabilir, retry mantıklı.
