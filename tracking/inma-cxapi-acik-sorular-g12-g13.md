# INMA / WapCRM — cxapi Entegrasyonu Açık Sorular (G12 + G13)

> **Kimden:** Invekto ekibi · **Kime:** INMA / WapCRM API ekibi · **Tarih:** 2026-06-08
> **Konu:** Bulk WhatsApp gönderimini doğrudan **cxapi `/api/chatoperation`**'a taşıma + onaylı şablon (HSM) gönderimi.
> **✅✅ ÇÖZÜLDÜ (2026-06-08 akşam) — INMA tüm soruları cevapladı:** [`invekto-cxapi-answers-G12-G13.md`](invekto-cxapi-answers-G12-G13.md). **C1 ✅** send yanıtı `data` = wamid = ack `InstanceMessageID` (`requestID` DEĞİL) · **C2 ✅** ack firmanın `WebhookUrl`'ine düşer (send yolu fark etmez; `SubscribedWebhookActive=true` + URL = bizim endpoint koşuluyla) · **G12.3 ✅** `InstanceID` globally-unique = tek firma · **G13.1 ✅** send'de `language` gerekmez · **G13.3 ✅** dinamik buton URL artık destekleniyor. → **G12 blocker KALKTI; kalan tamamen implementation (PR-3b).** Gate PR-3b ship + pilot tenant WapCRM config doğrulaması ile açılır.
> **Durum:** Invekto tarafı kod **hazır ama default-OFF / inert** — ~~aşağıdaki noktalar netleşmeden hiçbir müşteri canlıya alınmıyor~~ **(G12/G13 NETLEŞTİ; PR-3b yazılınca canlıya alınır).**
> **Öncelik:** 🔴 **G12** (yüksek — gönderim güvenliği / multi-tenant, HÂLÂ BLOCKER) → ✅ **G13-language ÇÖZÜLDÜ** (INMA 2026-06-08: her dil = ayrı şablon; dinamik buton ertelendi).
> **🆕 INMA cevapları (2026-06-08):** G13 `language` **çözüldü** (↓ G13 INMA-cevabı) · G12 için yalnızca **mekanizma** teyit edildi (durum anlık webhook push + okundu/read aktif) ama **korelasyon anahtarı / payload şeması / firma kimliği / routing HÂLÂ açık** → G12 blocker devam.
> **🆕🆕 WEBHOOK SPEC GELDİ (2026-06-08 akşam):** `temp/WEBHOOK_OUTBOUND_PAYLOADS.md` (sizden geldi) **ack/teslimat webhook şemasını TAM belgeledi** → **G12.2 ✅** (alan-alan şema) + **G12.5 ✅** (korelasyon anahtarı = WhatsApp `wamid` = ack `InstanceMessageID`). **AMA GATE HÂLÂ KAPALI:** anahtar `wamid`'miş (`requestID` **değil**) ve kalan **2 net teyit** → (C1) `/api/chatoperation` **send yanıtı** wamid'i döndürüyor mu? · (C2) cxapi ack'i **bizim webhook URL'imize mi** düşüyor? Detay ↓ "Webhook spec sonrası".
> **Referanslar:** `WapCRM-External-API-Documentation.pdf` · `wapcrm-api-integration-guide-for-agents.md` (sizden geldi) · `WEBHOOK_OUTBOUND_PAYLOADS.md` (sizden geldi) · Invekto roadmap: `tracking/feat-projeler-cxapi-roadmap.md`

---

## 0. Neden bu entegrasyon?

Bugün Invekto, bulk WhatsApp'ı **INMA Main App köprüsü** üzerinden gönderiyor. İki şey için doğrudan cxapi'ye geçiyoruz:

1. **Onaylı şablon (HSM) gönderimi** — köprüden gönderilemiyordu, cxapi `template` alanı ile mümkün.
2. **Gerçek `statusCode` görünürlüğü** — gönderimin provider sonucunu (kabul/ret/limit) doğrudan görmek.

Akış değişmiyor; sadece **gönderim taşıması** (transport) köprüden cxapi'ye kayıyor. Opt-out zaten doğrudan cxapi/INMA tarafında.

### Mimari akış (hedef)

```
Invekto Outbound (sabit IP)
   │  POST /api/chatoperation   (X-CIB-SecretKey + instanceID + userID)
   ▼
cxapi.wapcrm.net  ──►  WhatsApp
   │  yanıt: { status, statusCode, requestID, data }
   ▼
... mesaj WhatsApp'a gider ...
   │
   ▼  (teslimat olunca)
TESLİMAT WEBHOOK  ──►  Invekto  (delivered / read / failed)
                         └── BURASI G12'NİN KONUSU ↓
```

---

## 🆕 Webhook spec sonrası (2026-06-08 akşam) — ack ✅ çözüldü, kalan **2 net teyit**

`WEBHOOK_OUTBOUND_PAYLOADS.md` ile **teslimat/ack webhook şeması tamamen netleşti.** Bu, eski "tek eksik = örnek payload" beklentisini karşıladı; ama beklenmedik şekilde **gerçek korelasyon anahtarını da değiştirdi** (artık biliyoruz ki anahtar `requestID` değil, `wamid`).

**✅ Çözülen (ack tarafı):**
- Ack payload şeması alan-alan belli: `InstanceID` (int), `InstanceMessageID` (string), `Status` (int: 1=Sent / 2=Delivered / 3=Viewed/okundu / 4=NotSent), `StatusText`, `ReasonDetailForNotSent` (yalnız failed'da dolu). **(G12.2 ✅)**
- **Korelasyon anahtarı = WhatsApp `wamid`** — ack'teki `InstanceMessageID` wamid'dir (`wamid.HBgM...`), globally-unique. Tip ayrımı: `messages[]` varsa gelen mesaj, `Status`/`StatusText` varsa ack. **(G12.5 ✅)**

**🔴 Kalan — gate'i açan 2 teyit (yalnız INMA cevaplayabilir):**

| # | Teyit | Neden kritik |
|---|-------|--------------|
| **C1 (send-yanıtı wamid)** | `/api/chatoperation` **SEND yanıtı** wamid'i döndürüyor mu? Bizim okuduğumuz yanıtta tek kimlik `requestID`. wamid `data{}` içinde mi geliyor, yoksa send-yanıtında hiç dönmüyor mu? | Gönderdiğimiz satıra wamid'i **send-zamanı** yazıp, sonradan gelen ack'in `InstanceMessageID`'siyle eşleştirmek için. `requestID ≠ wamid` ve send-yanıtında wamid yoksa → eşleştirecek anahtarımız **hiç yok** → tüm ack'ler düşer. |
| **C2 (routing hedefi)** | `WebhookUrl`'i bizim teslimat endpoint'imize (`…/api/v1/webhook/delivery-status`) bakan bir firma için, **cxapi `/api/chatoperation` ile gönderilen** mesajların ack'i o URL'e mi düşüyor (Main App köprüsüne takılmadan)? | Köprüye gidiyorsa cxapi-direkt gönderimde teslimat durumlarını **hiç görmeyiz** → canlıya alma imkânsız. |

> **En hızlı çözüm:** **Anonim bir gerçek `/api/chatoperation` send yanıtı + aynı mesajın ack payload'u** (çift) paylaşmanız C1'i byte-byte kanıtlar; o ack'in bizim URL'e düştüğünü görmek C2'yi kapatır. **Bu çift gelirse G12 tamamen çözülür.**

> **G12.3 (firma kimliği) kısmî:** ack'te `companyCode` **yok**, sadece `InstanceID` (int) var. Tenant routing'i **globally-unique `wamid` üzerinden** yapacağız (ack'i, send-zamanı sakladığımız wamid satırına bağlayıp o satırın tenant'ını kullanarak). C1 çözülürse bu da çözülür. *(Yedek soru: bir `InstanceID` global olarak tek bir firmaya mı ait?)*

---

## 🔴 G12 — Teslimat-durumu (delivery-status) korelasyonu **[BLOCKER]**

### Sorun

`/api/chatoperation` yanıtında **`requestID`** dönüyor. Sonradan WhatsApp teslimat durumları (iletildi/okundu/başarısız) bize **webhook (push)** ile geliyor. Gönderdiğimiz her mesajı, sonradan gelen teslimat callback'iyle **birebir eşleştirmemiz** gerekiyor.

### Neden kritik (multi-tenant)

Invekto **çok-kiracılı (multi-tenant)** bir platform: tek bir Invekto kurulumu **onlarca firmaya** hizmet veriyor. Teslimat webhook'umuz **tüm firmaların** callback'lerini **tek endpoint'te** karşılıyor. Eğer gelen callback'te;

- mesajı tekil tanımlayan **kararlı bir kimlik** yoksa, **veya**
- **hangi firmaya/instance'a** ait olduğunu söyleyen bir alan yoksa,

o zaman bir teslimat durumunu **yanlış firmanın mesajına** yazabiliriz (cross-tenant veri sızıntısı) veya durum bilinmediği için **mükerrer gönderim** riski doğar. Bu yüzden gönderim yolunu şu an **kilitli** (gate kapalı) tutuyoruz.

### INMA'nın netleştirmesi gerekenler

| # | Soru | Neden lazım |
|---|------|-------------|
| **G12.1** | `/api/chatoperation` yanıtındaki **`requestID`**, teslimat webhook'undaki mesaj kimliğiyle **BİREBİR AYNI MI?** Değilse ikisini bağlayan alan hangisi? (`messageId` / WhatsApp `wamid` / `providerMessageId` …) | Gönderim ↔ teslimat eşleşmesinin **anahtarı**. |
| **G12.2** | Teslimat webhook'unun **TAM payload şeması** nedir? (alan-alan: mesaj kimliği, durum değerleri, zaman damgası, hata kodu) | Webhook handler'ı doğru yazmak için. |
| **G12.3** | Payload'da **firma/instance kimliği** var mı? (`instanceID` / firma kodu / `companyCode`) | Callback'i **doğru tenant'a** yazmak için **zorunlu**. |
| **G12.4** | cxapi üzerinden gönderilen mesajların teslimat callback'leri **NEREYE** gidiyor — bizim `…/webhook/delivery-status` adresimize mi, yoksa mevcut INMA köprüsüne mi? | Köprüye gidiyorsa cxapi-direkt gönderimde durumları **hiç görmeyiz**. Gate'i kaldırmanın ön koşulu. |
| **G12.5** | WhatsApp **`wamid`** (kalıcı mesaj kimliği) webhook'ta geliyor mu? | En sağlam korelasyon anahtarı bu olabilir. |

### 🆕 INMA sözlü cevabı (2026-06-08) — **kısmî, blocker DEVAM**

| Konu | INMA cevabı | G12 etkisi |
|------|-------------|------------|
| Durum/okundu mekanizması | Gönderilen mesajın **durumu anlık webhook** ile push ediliyor. **Okundu (read)** bilgisi de **aktif** — sadece kılavuzda nasıl gönderildiği dokümante değil. | Mekanizma ✅ teyit (push, real-time) + `read` geçerli bir durum değeri (G12.2'nin bir parçası). |
| **Kalan açık** (webhook spec SONRASI güncellendi) | ~~tam payload şeması (G12.2)~~ ✅ · ~~`wamid` (G12.5)~~ ✅ — **çözüldü.** KALAN: **send-yanıtı wamid'i döndürüyor mu** (C1 ≈ G12.1, asıl anahtar `requestID` değil **`wamid`**) · callback'in **nereye** route'landığı, bizim webhook'a mı köprüye mi (C2 = G12.4) · firma kimliği `wamid`-routing'e bağlandı (G12.3, C1'e tabi). | 🔴 **G12 HÂLÂ BLOCKER** ama **2 teyide indi (C1+C2).** **Anonim `/api/chatoperation` send-yanıtı + eşleşen ack çifti** ikisini birden çözer. |

### ~~Onaylamanız için taslak webhook şeması~~ → ✅ ARTIK NET (WEBHOOK_OUTBOUND_PAYLOADS.md)

Taslak gerek kalmadı — gerçek ack şeması belgelendi. Bizim tahminimiz (`requestID`/`companyCode`/`timestamp`) **tutmadı**; gerçek şekil:

```jsonc
// WapCRM → Invekto  (ack / teslimat durumu push)  — WapCRMCloudAPIAckWebHookModel
{
  "InstanceID": 505024,                  // int — hangi kanal (companyCode YOK)
  "InstanceMessageID": "wamid.HBgM...",  // ★ korelasyon anahtarı = WhatsApp wamid (requestID DEĞİL)
  "Status": 3,                            // int enum: 1=Sent 2=Delivered 3=Viewed(okundu) 4=NotSent
  "StatusText": "Viewed",
  "ReasonDetailForNotSent": null          // yalnız Status=4'te dolu (provider hata detayı)
}
```

> **Bizim tarafta açılan implementation deltaları** (Invekto-içi, INMA'ya gitmez): mevcut `DeliveryStatusRequest` DTO bu şekille uyuşmuyor (lowercase string status + `timestamp` + `external_message_id`) → **ack-DTO/shim** gerekiyor (PascalCase, Status int→string map, `InstanceMessageID`→ext_id, `ReasonDetailForNotSent`→failed_reason, `InstanceID` int→string normalize). Ayrıca spec §4.4/§4.5 gereği **status-downgrade guard** (Viewed>Delivered>Sent, geriye düşürme) + **idempotency** (`tenant_id`+ext_id+status) — mevcut handler ikisini de yapmıyor. Bu 2 madde **INMA'ya bağlı DEĞİL**, PR-3b'de şimdi yazılabilir.

### Bu cevaplanınca (Invekto tarafı — PR-3b, tek atomik deploy)

C1+C2 gelince PR-3b şunları **tek atomik deploy** ile açar → **canlı-açılım gate'i (P0-3) kalkar**, pilot müşteri cxapi'den gönderebilir:
1. **Send-zamanı `wamid` yakala** (`/api/chatoperation` yanıtından, C1) → `external_message_id`'ye yaz. *(Bugün send sadece `requestID`'yi `provider_request_id`'ye yazıyor; `external_message_id` cxapi'de NULL.)*
2. **Ack-DTO/shim** (gerçek PascalCase ack şekli) + **status-downgrade guard** + **idempotency** *(bu madde INMA'ya bağlı değil, önden yazılabilir)*.
3. `external_message_id` üzerinde **`(tenant_id, external_message_id)` composite-UNIQUE** + **tenant-scoped lookup** + webhook'a **wamid→tenant_id routing**.

---

## ✅ G13 — Onaylı şablon (HSM) gönderim kontratı **[ÇÖZÜLDÜ — dinamik buton ertelendi]**

Gönderdiğiniz **entegrasyon kılavuzu** (`wapcrm-api-integration-guide-for-agents.md`) bunları **zaten netleştirdi** — teyit için listeliyoruz:

| Konu | Cevap (kılavuzdan) | Durum |
|------|--------------------|-------|
| `templateId` formatı | string slug, ör. `"siparis_bilgi"` (`POST /api/templates`'ten) | ✅ |
| Parametre tipi | **named** (positional değil): `parameters: [{ paramKey, value }]` | ✅ |
| Zorunlu alan/sayı kaynağı | şablonun **`requiredInputs[]`** listesi (`kind`/`location`/`paramKey`/`mediaType`) | ✅ |
| Header media davranışı | `requiredInputs`'ta `media/HEADER` varsa `headerMedia:{url,fileName}` (public URL); **sabit-medyalı** şablonda gönderme (sistem ekler) | ✅ |
| Hatalar | `621` şablon yok · `622` eksik zorunlu alan | ✅ |

### Kalan tek kritik açık + birkaç teyit

| # | Soru | Neden lazım |
|---|------|-------------|
| **G13.1 (kritik)** | **`language` (dil) nereden geliyor?** Kılavuzda dil alanı yok. WhatsApp HSM'ler dile özgüdür. (a) `templateId` slug'ı dili kapsıyor mu (her dil = ayrı slug), yoksa (b) gönderimde ayrı bir **`language`** alanı mı gerekiyor? Format ne (`tr` / `tr_TR`)? **`POST /api/templates` yanıtı şablonun dilini döndürüyor mu?** | Picker'da doğru dil varyantını seçmek + gönderimde doğru alanı yollamak için. |
| **G13.2** | Aynı `templateId` için **birden fazla dil varyantı** varsa nasıl ayrışıyor? | Çok-dilli şablon listesini doğru göstermek. |
| **G13.3** | `BUTTON` location'lı `requiredInputs` (dinamik buton URL/payload) gönderimde nasıl dolduruluyor? Örnek body var mı? | Buton içeren şablonları desteklemek. |
| **G13.4** | İdempotency: kılavuz "API otomatik dedupe etmez, kendi tarafında yap" diyor. Bizim göndereceğimiz bir **idempotency anahtarını** API tanıyor mu, yoksa tamamen bizde mi? | Mükerrer gönderimi önlemek (özellikle retry'da). |

### 🆕 INMA cevabı (2026-06-08) — `language` ÇÖZÜLDÜ + buton ERTELENDİ

| # | INMA cevabı | Sonuç |
|---|-------------|-------|
| **G13.1 / G13.2 (language)** | **Çok-dilli şablonda Meta her dil için AYRI şablon oluşturuyor** → her dil = ayrı şablon (ayrı `templateId` slug). | ✅ **ÇÖZÜLDÜ — Seçenek (a).** Dil slug'ın kendisine gömülü; gönderimde granular dil seçimi yok, **doğru dilin slug'ı seçilir**. `POST /api/templates` ayrı dil alanı döndürmüyor → picker dili slug/preview'dan etiketler. Bizim `WapCrmTemplateDto`'da zaten `language` alanı yok = tutarlı. **Tek küçük teyit kaldı:** `/chatoperation` template gönderimi ayrı bir `language` alanı **istiyor mu**, yoksa slug'tan mı türetiliyor? |
| **G13.3 (dinamik buton URL)** | WapCRM'in eklediği butonlar **hep statik** — bununla ilgili ayar yok. Dinamik link için: **bizdeki template seçilir → statik yerine "dinamik değer" seçilirse** URL dışarıdan (`parameters[]`) gönderilebilir. | ⏸️ **ERTELENDİ (Q kararı 2026-06-08):** dinamik URL şimdilik sonraya. PR-4 **statik-buton + text/media** şablonlarla yazılır. |
| **G13.4 (idempotency)** | (kılavuz: API dedupe etmiyor → bizde) | ✅ Değişmedi — idempotency tamamen Invekto tarafında. |

> **PR-4 dil tarafında AÇIK** (statik-buton kapsamı yeterli). Ama **canlıya alma hâlâ G12'ye bağlı** (P0-3 gate / PR-3b): PR-4 yazılabilir + test edilebilir, **send go-live G12 çözülünce.**

### Şu an kullandığımız gönderim şekli (teyit için)

```jsonc
// Invekto → cxapi  (onaylı şablon)
POST https://cxapi.wapcrm.net/api/chatoperation
{
  "instanceID": 15,
  "userID": 1,
  "chatPhoneNumber": "905XXXXXXXXX",
  "template": {
    "templateId": "siparis_bilgi",
    "language": "tr_TR",                                   // ← G13.1 ÇÖZÜLDÜ: dil slug'a gömülü (her dil=ayrı şablon). Bu alan GEREKSİZ olabilir — slug'tan türerse kaldırılır (tek teyit kaldı)
    "parameters": [ { "paramKey": "ad", "value": "Ahmet" } ],
    "headerMedia": { "url": "https://.../resim.jpg", "fileName": "resim.jpg" }  // sadece dynamic media varsa
  }
}
```

---

## ✅ Çözülmüş (sadece kayıt için)

- **G9** — INMA opt-out'u **server-side** uyguluyor (Invekto + INMA çift-koruma). ✓
- **G11** — Outbound sunucu IP'si firma **whitelist**'inde. ✓
- **Auth** — `X-CIB-SecretKey` (per-request, asla loglanmaz/saklanmaz) + sabit IP. ✓
- **Şablon akışı** — listele (`POST /api/templates`) → doldur (`requiredInputs` → `parameters`) → gönder. ✓
- **Rate-limit** — `301/302` görülürse backoff + jitter uyguluyoruz. ✓

---

## Özet & sonraki adım

| Blocker | Eksik | Açılacak iş (Invekto) |
|---------|-------|------------------------|
| 🔴 **G12** | Webhook spec ile **G12.2 ✅ + G12.5 ✅** çözüldü (ack şeması + `wamid`=anahtar). KALAN **2 teyit:** **C1** send-yanıtı wamid döndürüyor mu (≈G12.1) · **C2** cxapi ack'i bizim URL'e mi düşüyor (G12.4). Firma kimliği (G12.3) → `wamid`-routing'e bağlandı. | **PR-3b**: send-time wamid capture + ack-shim + downgrade/idempotency + ext_id composite-UNIQUE + tenant-scoped webhook → P0-3 gate kalkar |
| ✅ **G13** | ÇÖZÜLDÜ — `language` (G13.1/G13.2) = her dil ayrı şablon · dinamik buton (G13.3) **ERTELENDİ** · tek küçük teyit: send body `language` alanı gerekli mi? | **PR-4**: şablon picker (dil = slug seçimi) + per-recipient `parameters[]` + gönderim (statik-buton kapsamı) |

**En kritik:** Artık **yalnızca G12** ve **2 net teyide** indi: **C1** (`/api/chatoperation` send-yanıtı wamid'i döndürüyor mu?) + **C2** (cxapi ack'i bizim `…/webhook/delivery-status` URL'imize mi düşüyor?).
G13 çözüldü → **PR-4 yazılabilir** (dil = doğru slug seçimi); ancak **send go-live G12'ye bağlı** (P0-3 gate).

> **Tek hamlede çözüm:** anonim bir gerçek **`/api/chatoperation` send-yanıtı + aynı mesajın ack payload'u** (çift) paylaşın — C1 (paylaşılan iki kimliğin aynı `wamid` olup olmadığı) ve C2 (ack'in bizim URL'e düşmesi) birlikte kapanır.
