# Invekto Messaging API — Entegrasyon Kılavuzu (Geliştirici / Agent için)

> **Kime:** Invekto Messaging dışındaki bir projenin (ve oradaki Claude agent'ının) bizim API'mize erişip
> **mesaj ve template göndermesi** için pratik entegrasyon rehberi.
> **Tam referans:** `Invekto-Messaging-API-Documentation.pdf` (tüm endpoint'ler) · Postman:
> `postman/WapCRM-External-API.postman_collection.json`

---

## 0. Hızlı Başlangıç (3 adım)

1. **Auth hazırla:** Her isteğe `X-CIB-SecretKey: <api_key>` header'ı ekle. Çağıran sunucunun **IP'si** WapCRM'de firmaya tanımlı olmalı.
2. **Kanal + kullanıcı öğren:** `GET /api/Instances` → `instanceID`; `GET /api/users` → `userID`.
3. **Gönder:** `POST /api/chatoperation` (metin) veya `template` alanıyla (şablon).

- **Base URL:** `https://cxapi.wapcrm.net`
- **Content-Type:** `application/json`
- **Ortak yanıt:** `{ status, message, statusCode, requestID, data }` — `status=false` ise `statusCode` (bkz. PDF §9 / `GET /help/statuscodes`).

---

## 1. Kimlik Doğrulama

| Gereksinim | Açıklama |
|---|---|
| `X-CIB-SecretKey` header | Firma API anahtarı (app.wapcrm.net → Ayarlar → API). Her istekte zorunlu. |
| IP whitelist | Çağıran sunucu IP'si firmaya tanımlı olmalı; aksi halde `509`. |

> İki güvenlik katmanı da **otomatik** uygulanır; ek bir şey yapmana gerek yok, sadece header + sabit IP.

---

## 2. Normal Mesaj Gönderme

`POST /api/chatoperation`

```jsonc
{
  "instanceID": 15,            // GET /api/Instances
  "userID": 1,                 // GET /api/users  (veya incom=4 + userKey)
  "chatPhoneNumber": "905XXXXXXXXX",
  "messageType": 1,            // 1=metin, 2=resim
  "messageText": "Merhaba"
}
```
- **Resim:** `messageType:2` + `fileUrl` (public, auth'suz) + `fileName`.
- **Zoho/e-posta kullanıcı:** `userID` yerine `incom:4` + `userKey:"kisi@firma.com"`.
- **FB/IG/Telegram:** `chatPhoneNumber` yerine `chatAccountId`.

### C# örneği (HttpClient)
```csharp
var http = new HttpClient { BaseAddress = new Uri("https://cxapi.wapcrm.net/") };
http.DefaultRequestHeaders.Add("X-CIB-SecretKey", apiKey);
var body = new { instanceID = 15, userID = 1, chatPhoneNumber = "905XXXXXXXXX", messageType = 1, messageText = "Merhaba" };
var res = await http.PostAsJsonAsync("api/chatoperation", body);
var json = await res.Content.ReadAsStringAsync(); // { status, statusCode, requestID, ... }
```

---

## 3. Template (Şablon) Gönderme

Akış: **listele → doldur → gönder**.

### 3.1 Şablonları listele
`POST /api/templates` body `{ "instanceID": 15 }` →
her şablon: `templateId`, `preview`, **`requiredInputs`** (senin doldurman gerekenler) ve **`fixedValues`** (yönetici sabit atadı — sistem dolduracak, sadece bilgi).

```jsonc
"requiredInputs": [
  { "kind": "text",  "location": "BODY",   "paramKey": "ad" },
  { "kind": "media", "location": "HEADER", "mediaType": "image", "note": "public resim URL'i ver" }
],
"fixedValues": [
  { "location": "BODY", "paramKey": "destek", "value": "0850 000 00 00" }   // sistem bunu gönderecek; SEN GÖNDERME
]
```
**requiredInputs alanları:** `kind` (text|media) · `location` (BODY|HEADER|BUTTON) · `paramKey` (text için doldurulacak anahtar → gönderimde `parameters[].paramKey` ile eşleşir) · `mediaType` (media için image|video|document) · `note` (açıklama).
**fixedValues alanları:** `location` · `paramKey` · `value` (gönderimde o alanda gidecek sabit değer) · `note`. Bu alanları `parameters`'a **koyma** — sistem otomatik doldurur; liste sadece "bu alanda ne gidecek" bilgisidir. Sabit medya bu listede yoktur (`fixedNote`).

### 3.2 Gönder
`POST /api/chatoperation` + `template` alanı:
```jsonc
{
  "instanceID": 15, "userID": 1, "chatPhoneNumber": "905XXXXXXXXX",
  "template": {
    "templateId": "siparis_bilgi",
    "parameters": [ { "paramKey": "ad", "value": "Ahmet" } ],   // requiredInputs text alanları
    "headerMedia": { "url": "https://.../resim.jpg", "fileName": "resim.jpg" }  // sadece dynamic medya varsa
  }
}
```

**Kurallar:**
- `parameters` = `requiredInputs`'taki her **text** alan için `{ paramKey, value }`. Bu, body/header değişkenlerini **ve dinamik butonları** kapsar (location BODY/HEADER/BUTTON fark etmez — hepsi `parameters` ile gider).
- `headerMedia` = `requiredInputs`'ta **media** varsa; **public** erişilebilir URL ver. Sabit medyalı şablonda **gönderme** (sistem ekler).
- Eksik zorunlu alan → `622`; şablon yok → `621`.

**Dinamik buton (URL/kupon):** Bir URL butonunun adresinde `{{1}}` varsa (ör. `https://site.com/{{1}}`), `requiredInputs`'ta `location: "BUTTON"` bir text alanı döner (`paramKey` örn. `"btn1_url"`). Değerini diğer text alanlar gibi `parameters` içinde gönderirsin; sistem butonun `{{1}}` kısmına yerleştirir:
```jsonc
{ "instanceID": 15, "userID": 1, "chatPhoneNumber": "905XXXXXXXXX",
  "template": { "templateId": "...", "parameters": [ { "paramKey": "btn1_url", "value": "kullanici-123" } ] } }
```
Sabit (değişkensiz) butonlar için bir şey göndermene gerek yok.

---

## 4. Diğer İşlemler (özet)

| İhtiyaç | Endpoint |
|---|---|
| Müşteri durumu (Lead aşaması vb.) oku/güncelle | `POST /api/customer-feature-groups/{catalog,customer-selections,update}` |
| Filtreli müşteri listesi | `POST /api/customer-export` (+ `/api/customer-filter-options`) |
| Pazarlama izni (opt-out/in) | `POST /api/{optout,optin,optout/status}` |
| Görüşme/mesaj raporu | `POST /api/{messagelistforphone,conversations}`, `GET /api/callresults` |

> Tüm bu endpoint'lerin detayı: `WapCRM-External-API-Documentation.pdf`. Hazır istekler: Postman collection.

---

## 4.5 Dönen Veri Alanları (Response)

Tüm yanıtlar `{ status, message, statusCode, requestID, data }` zarfıyla gelir. `data` içeriği endpoint'e göre değişir; en sık kullanılanlar:

| Endpoint | `data` alanları |
|---|---|
| `GET /api/users` | `userID` (kimlik), `userFullName` (ad soyad), `userName` (kullanıcı adı/e-posta) |
| `GET /api/Instances` | `instanceID`, `account` (telefon/kullanıcı adı), `instanceName`, `instanceType` (1=WhatsApp,2=IG,3=FB,4=Telegram,5=Voip,6=SMS), `connectionType` (WhatsApp: "QR Code"\|"WABA"; diğerlerinde kanal tipi adı) |
| `GET /api/dynamicfields` | `fieldKey` (name,email,cf1..cf10), `fieldName` (görünen ad) |
| `POST /api/templates` | `templateId`, `preview`, `requiredInputs[]` (kind/location/paramKey/mediaType/note), `fixedValues[]` (location/paramKey/value/note — yönetici sabit atadı, sistem doldurur), `fixedNote` |
| `POST /api/chatoperation` | `status=true` → gönderim kabul edildi; `data` chatserver gönderim sonucu |
| `POST /api/customer-export` | `paging` (page/pageSize/totalCount/totalPages/hasMore/note) + `customers[]` (customerId, name, phoneNumber, representativeId, representativeName) |
| `POST /api/customer-filter-options` | `labels[]` (id,name), `customFields[]` (fieldKey, name, type, options) |
| `POST /api/customer-feature-groups/customer-selections` | grup başına: `featureGroupID`, `featureGroupName`, `selectionMode` (1=çoklu,2=tek,3=metin), `selections[]` (featureID, featureName, rgbCode) |

> **Her endpoint'in tam `data` alan açıklaması** `WapCRM-External-API-Documentation.pdf` içinde, ilgili endpoint'in "Yanıt alanları" tablosundadır.

---

## 4.6 Webhook (Gelen Mesaj + Teslimat Durumu / Ack)

Webhook aboneliği aktifse, **gelen mesajlar** ve **gönderdiğin mesajların durum (ack) bildirimleri**
tanımlı `WebhookUrl`'ine `POST application/json` ile gönderilir. (Detay: `invekto-messaging-webhook-payloads.md`.)

**İki payload tipini ayırt et:**
- `messages` (dizi) varsa → **gelen mesaj** (kök seviyede `InstanceID` string).
- `Status` / `StatusText` varsa → **ack/durum** (`InstanceMessageID` + `Status`, `messages` yok; `InstanceID` int).

```jsonc
// Gelen mesaj
{ "InstanceID": "15",
  "messages": [ { "id": "wamid...", "body": "Merhaba", "type": "chat", "time": 1780578426,
                  "chatId": "905XXXXXXXXX@c.us", "fromMe": false } ] }

// Ack (durum)  — Status: 1=Sent, 2=Delivered, 3=Viewed(okundu), 4=NotSent
{ "InstanceID": 15, "InstanceMessageID": "wamid...", "Status": 3, "StatusText": "Viewed",
  "ReasonDetailForNotSent": null }
```

**Korelasyon (gönderim ↔ teslimat):**
- `POST /api/chatoperation` send yanıtının **`data`** alanı = gönderilen mesajın **wamid**'idir (`requestID` değil).
- Bu `data` (wamid), ack'in **`InstanceMessageID`'si ile birebir aynıdır** → send anında `data`'yı sakla, ack gelince `InstanceMessageID` ile eşle.
- Gelen mesajda da `messages[].id` = ack `InstanceMessageID`.

**Tüketim kuralları:** Sıra garantisi yok → idempotent işle (`InstanceMessageID + Status` ile mükerrer yut),
durumu geriye düşürme (Viewed > Delivered > Sent), `InstanceID`'yi string'e normalize et, medyada gerçek dosya `body`'deki public URL'dir.

---

## 5. Hata Yönetimi

- `status=false` ise `statusCode`'a bak (TR+EN liste: PDF §9 veya `GET /help/statuscodes`).
- Sık karşılaşılanlar: `400` (apikey yok), `509` (IP whitelist), `503/512` (kullanıcı/incom), `506` (kanal), `622` (template eksik alan), `911` (sistem).
- Her yanıtta `requestID` döner — destek/log için sakla.

---

## 6. Agent için Notlar
- **Geriye uyumluluk:** Mevcut `chatoperation` davranışı sabittir; sadece `template` alanı opsiyonel eklentidir.
- **İdempotency:** Tekrarlı gönderimde mükerrer mesaj riskine dikkat (kendi tarafında dedupe yap; API otomatik dedupe etmez).
- **Rate:** Aşırı hızlı toplu gönderimde `301/302` (limit) dönebilir; backoff uygula.
- Şüphede kalırsan Postman collection'daki örneği baz al; body şekilleri birebir uyumludur.
