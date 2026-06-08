# Invekto ↔ cxapi — G12 + G13 Açık Sorulara Cevaplar

> **Kimden:** Invekto Messaging API ekibi · **Kime:** Invekto entegrasyon ekibi · **Tarih:** 2026-06-08
> **Özet:** G12 (teslimat korelasyonu) **2 teyit (C1+C2)** ve G13.1 (language) net cevaplandı → **gate açılabilir.**

---

## 🔴 G12 — Teslimat-durumu korelasyonu

### ✅ C1 — `/api/chatoperation` SEND yanıtı wamid'i döndürüyor mu? → **EVET**

**Gönderim yanıtının `data` alanı, gönderilen mesajın WhatsApp id'sini (wamid) taşır.**

- Chatserver, Cloud API'ye mesajı gönderdikten sonra dönen `messages[0].id` (= wamid) değerini yakalar ve send yanıtına koyar.
- Yani korelasyon anahtarınız **`data`** alanıdır — **`requestID` DEĞİL.** `requestID` yalnızca dahili istek-log GUID'idir, mesajla ilgisi yoktur.
- Bu `data` (wamid), sonradan gelen **ack'in `InstanceMessageID`'si ile BİREBİR aynıdır.**

```jsonc
// SEND yanıtı (Invekto'nun okuyacağı)
{
  "status": true,
  "message": "Success",
  "statusCode": "",
  "requestID": "b3c5...-...",                 // dahili log id — KORELASYON İÇİN KULLANMAYIN
  "data": "wamid.HBgMOTA1XXXXXXXX..."          // ★ gönderilen mesajın wamid'i — KORELASYON ANAHTARI
}
```

```jsonc
// Sonradan gelen ACK (webhook)  — aynı wamid
{
  "InstanceID": 15,
  "InstanceMessageID": "wamid.HBgMOTA1XXXXXXXX...",   // ★ = send yanıtındaki data
  "Status": 3, "StatusText": "Viewed",
  "ReasonDetailForNotSent": null
}
```

**Yapmanız gereken:** Send anında `data`'yı `external_message_id`'ye yazın; ack geldiğinde `InstanceMessageID` ile bu satırı bulun.

> **Doğrulama:** Tek bir gerçek test gönderimi yapıp send yanıtının `data`'sını okuyun; ardından gelen ack'in `InstanceMessageID`'si ile aynı olduğunu göreceksiniz (ikisi de `wamid....`).

---

### ✅ C2 — cxapi ack'i bizim webhook URL'imize mi düşüyor? → **EVET**

- Ack (durum) webhook'ları, chatserver tarafından **firmanın tanımlı `WebhookUrl`'ine** push edilir.
- Bu, mesajın **nasıl gönderildiğinden bağımsızdır** — köprüden mi yoksa cxapi `/api/chatoperation`'dan mı gönderildi fark etmez. Ack, WhatsApp'tan chatserver'a gelir, chatserver mesajın firmasını bulup o firmanın `WebhookUrl`'ine yollar.
- **Koşul:** İlgili firmada webhook aboneliği aktif (`SubscribedWebhookActive = true`) ve `WebhookUrl` = sizin `…/api/v1/webhook/delivery-status` adresiniz olmalı.

→ cxapi-direkt gönderilen mesajların teslimat durumları **sizin URL'inize düşer**, Main App köprüsüne takılmaz.

---

### ✅ G12.3 — Firma kimliği / tenant routing → **InstanceID globally-unique**

- Ack'te `companyCode` yok ama **`InstanceID` (int) global olarak benzersizdir** — sistemdeki kanal (CompanyInstance) birincil anahtarıdır, **tek bir firmaya** aittir.
- Yani **yedek sorunuzun cevabı EVET:** bir `InstanceID` global olarak tek bir firmaya aittir.
- İki routing seçeneğiniz var:
  1. **InstanceID → tenant** (en basit): Invekto tarafında hangi InstanceID hangi tenant'a ait kaydedip ack'i doğrudan routing edebilirsiniz. wamid'e bile gerek yok.
  2. **wamid → send-satırı → tenant** (C1 ile): send anında sakladığınız wamid satırının tenant'ını kullanın.

Cross-tenant sızıntı riski yok: hem `InstanceID` (kanal→firma) hem `wamid` (globally unique) tekil.

---

## ✅ G13 — Onaylı şablon (HSM)

### G13.1 / G13.2 — `language` → **send'de GEREKMİYOR**

- **Her dil = ayrı şablon = ayrı `templateId`** (Meta böyle üretir). Dil, `templateId`'nin kendisine gömülüdür.
- **`/api/chatoperation` template gönderiminde ayrı bir `language` alanı GEREKMEZ.** İstek modelimizde (`template`) `language` property'si **yoktur**; gönderirseniz **yok sayılır** (zararsız). Doğru dilin `templateId`'sini seçmeniz yeterli.
  → Örnek body'nizden `"language": "tr_TR"` satırını **kaldırabilirsiniz.**
- **`POST /api/templates` yanıtı şablonun dilini DÖNDÜRÜR:** her template'te `language` alanı vardır (`"tr"`, `"en"`, `"en_US"` ...). Picker bu alanla dili etiketleyebilir/gruplayabilir.

```jsonc
// POST /api/templates yanıtından (her template'te language var)
{ "templateId": "1664033448202974", "name": "text", "language": "en", "category": "MARKETING", "paramFormat": "named", ... }
```

→ **Tek küçük teyidiniz kapandı:** send body'de `language` alanı gerekmez; dil `templateId`'den gelir. `/api/templates` dili döner.

### G13.3 — Dinamik buton URL → **artık DESTEKLENİYOR** (ertelemenize gerek yok)

- WABA'daki dinamik URL butonu (`url: "https://site.com/{{1}}"`) artık destekleniyor.
- `POST /api/templates` yanıtında, böyle bir buton için `requiredInputs`'ta şu alan döner:
  ```jsonc
  { "kind": "text", "location": "BUTTON", "paramKey": "btn1_url", "note": "URL buton parametresi" }
  ```
- Gönderimde değeri diğer text alanlar gibi `parameters` içinde yollarsınız; sistem butonun `{{1}}` kısmına yerleştirir:
  ```jsonc
  "parameters": [ { "paramKey": "btn1_url", "value": "kullanici-123" } ]
  ```
- Statik butonlar (sabit URL / quick reply / telefon) için bir şey göndermenize gerek yok.

> İsterseniz PR-4'ü statik-buton kapsamıyla yazabilirsiniz; dinamik buton hazır olduğunda yukarıdaki tek alanla eklenir.

### G13.4 — Idempotency → **tamamen sizde**

- API tarafında dedupe yapılmaz; API'nin tanıdığı bir idempotency anahtarı **yoktur.** Mükerrer önleme (özellikle retry'da) tamamen Invekto tarafındadır.

---

## Özet

| # | Cevap |
|---|-------|
| **C1** | ✅ Send yanıtı `data` = gönderilen mesajın **wamid**'i = ack `InstanceMessageID`. Korelasyon `data`'dan (requestID değil). |
| **C2** | ✅ Ack, firmanın `WebhookUrl`'ine düşer (send yolu fark etmez). SubscribedWebhookActive + URL sizin endpoint olmalı. |
| **G12.3** | ✅ `InstanceID` globally-unique → tek firma. InstanceID→tenant veya wamid→tenant routing. |
| **G13.1** | ✅ Send'de `language` gerekmez (templateId dil-özgül). `/api/templates` `language` döner. |
| **G13.3** | ✅ Dinamik URL butonu destekleniyor (`requiredInputs` paramKey `btn1_url` → `parameters`). |
| **G13.4** | ✅ Idempotency tamamen Invekto tarafında. |

→ **G12 blocker'ı C1+C2 ile kalkar; G13 tamamen net.** PR-3b (wamid capture from `data` + tenant routing) ve PR-4 (şablon) yazılabilir.
