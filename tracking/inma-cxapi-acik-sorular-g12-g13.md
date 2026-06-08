# INMA / WapCRM — cxapi Entegrasyonu Açık Sorular (G12 + G13)

> **Kimden:** Invekto ekibi · **Kime:** INMA / WapCRM API ekibi · **Tarih:** 2026-06-08
> **Konu:** Bulk WhatsApp gönderimini doğrudan **cxapi `/api/chatoperation`**'a taşıma + onaylı şablon (HSM) gönderimi.
> **Durum:** Invekto tarafı kod **hazır ama default-OFF / inert** — aşağıdaki noktalar netleşmeden hiçbir müşteri canlıya alınmıyor.
> **Öncelik:** 🔴 **G12** (yüksek — gönderim güvenliği / multi-tenant, HÂLÂ BLOCKER) → ✅ **G13-language ÇÖZÜLDÜ** (INMA 2026-06-08: her dil = ayrı şablon; dinamik buton ertelendi).
> **🆕 INMA cevapları (2026-06-08):** G13 `language` **çözüldü** (↓ G13 INMA-cevabı) · G12 için yalnızca **mekanizma** teyit edildi (durum anlık webhook push + okundu/read aktif) ama **korelasyon anahtarı / payload şeması / firma kimliği / routing HÂLÂ açık** → G12 blocker devam.
> **Referanslar:** `WapCRM-External-API-Documentation.pdf` · `wapcrm-api-integration-guide-for-agents.md` (sizden geldi) · Invekto roadmap: `tracking/feat-projeler-cxapi-roadmap.md`

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
| **Kalan açık** | Korelasyon anahtarı (G12.1) · **tam payload şeması** (G12.2) · **firma/instance kimliği** (G12.3) · callback'in **nereye** route'landığı, bizim webhook'a mı köprüye mi (G12.4) · `wamid` (G12.5) — **hiçbiri henüz net değil.** | 🔴 **G12 HÂLÂ BLOCKER.** Tek bir **anonim örnek teslimat-webhook payload'ı** bunların tamamını anında çözer. |

### Onaylamanız için taslak webhook şeması

Aşağıdaki şekli bekliyoruz — **"evet böyle" / "şu alan farklı / yok / adı şu"** diyerek düzeltin:

```jsonc
// INMA → Invekto  (teslimat durumu push)
POST https://<invekto-webhook-url>/api/v1/webhook/delivery-status
{
  "requestID": "abc-123",            // /api/chatoperation yanıtındaki ile AYNI mı? (G12.1)
  "wamid": "wamid.HBgN...",          // WhatsApp kalıcı mesaj kimliği var mı? (G12.5)
  "instanceID": 15,                  // hangi kanal (G12.3)
  "companyCode": "dentadavista",     // hangi firma/tenant (G12.3) — VAR MI?
  "chatPhoneNumber": "905XXXXXXXXX",
  "status": "delivered",             // izin verilen değerler? sent|delivered|read|failed|...
  "statusCode": null,                // failed ise hata kodu (PDF §9 ile aynı mı?)
  "timestamp": "2026-06-08T12:00:00Z"
}
```

### Bu cevaplanınca (Invekto tarafı)

`external_message_id` üzerinde **`(tenant_id, external_message_id)` composite-UNIQUE** + **tenant-scoped lookup** + webhook'a **tenant_id routing**'i **tek atomik deploy** ile açarız → **canlı-açılım gate'i (P0-3) kalkar**, pilot müşteri cxapi'den gönderebilir.

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
| 🔴 **G12** | Korelasyon anahtarı (G12.1) + callback routing (G12.4) + firma kimliği (G12.3) + tam payload şeması (G12.2) + `wamid` (G12.5) — INMA 2026-06-08'de yalnızca *mekanizmayı* teyit etti (webhook push + read aktif) | **PR-3b**: ext_id composite-UNIQUE + tenant-scoped webhook + P0-3 gate kalkar |
| ✅ **G13** | ÇÖZÜLDÜ — `language` (G13.1/G13.2) = her dil ayrı şablon · dinamik buton (G13.3) **ERTELENDİ** · tek küçük teyit: send body `language` alanı gerekli mi? | **PR-4**: şablon picker (dil = slug seçimi) + per-recipient `parameters[]` + gönderim (statik-buton kapsamı) |

**En kritik:** Artık **yalnızca G12** — özellikle **G12.1 (korelasyon anahtarı)** ve **G12.4 (callback nereye gidiyor)**.
G13 çözüldü → **PR-4 yazılabilir** (dil = doğru slug seçimi); ancak **send go-live G12'ye bağlı** (P0-3 gate).

> Sorularınız olursa örnek gerçek bir teslimat-webhook payload'ı (anonim) paylaşmanız G12'yi **anında** çözer — şu an tek eksik bu.
