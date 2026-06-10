# INMA → Müşteri Durumu Webhook Teklifi — Cevabımız

> **Kimden:** Invekto Messaging API ekibi · **Kime:** INMA entegrasyon ekibi · **Tarih:** 2026-06-10
> **Özet:** İstediğiniz webhook **zaten canlı** (`customer.selection_changed`). Teklifinizdeki eksik
> alanlardan **phone / originRequestId echo / text modu bugün eklendi**; `ChangeVersion` Faz 2
> (aşağıda güvenli fallback reçetesi var). Yeni bir event shape'i geliştirmemize gerek yok —
> tek yapmanız gereken **bir kerelik abonelik kurulumu + alan adlarını bizim şemaya map etmek.**

---

## 0. En önemli düzeltme: ayrı (ve daha sağlam) bir push kanalı

Teklifiniz "mevcut mesaj/ack borusuna yeni gövde" varsayıyordu. Gerçek durum daha iyi:
müşteri-durumu event'leri **ayrı bir sistem-olayları webhook altyapısından** gider:

- **Ayrı endpoint kaydı** — mesaj/ack push URL'inizden bağımsız; panelden bir kerelik tanımlanır
  (URL + secret + event aboneliği). `…/api/v1/webhook/event` adresinizi buraya da verebilirsiniz
  ya da ayrı bir route açarsınız — size kalmış.
- **HMAC-SHA256 imza** — her istekte `X-Invekto-Signature` (+ `X-Invekto-Event-Id/-Event-Type/-Timestamp`
  header'ları). Mesaj webhook'unun aksine bu kanal imzalıdır; doğrulama kod örnekleri ekteki
  **"Invekto Webhook Müşteri Rehberi" PDF**'inde (C#/PHP/Python/Node/Java).
- **Transactional outbox + retry** — durum değişikliğiyle aynı veritabanı transaction'ında kuyruğa yazılır;
  "değişiklik oldu ama event üretilmedi" durumu yoktur.

> Yani: "INMA'dan istenen tek yeni iş webhook'a yeni event shape'i" beklentiniz yerine,
> **hiçbir backend geliştirmesi gerekmiyor** — event zaten üretiliyor. İş: abonelik + consumer kodu.

---

## 1. Gerçek event şeması (alan eşleme tablosu)

```jsonc
{
  "id":         "f47ac10b-...",                  // sizin EventID — retry'da SABİT, dedupe anahtarı
  "type":       "customer.selection_changed",    // sizin EventType (sabit ayırt edici)
  "version":    1,                               // şema versiyonu (additive eklemelerde artmaz)
  "occurredAt": "2026-06-10T14:30:00Z",          // sizin ChangedAt (ISO8601 UTC)
  "companyId":  11,
  "actor":      { "type": "api", "id": 42, "name": "..." },   // sizin ChangeSource + ChangedBy
  "data": {
    "customerId":       134162,                  // sizin CustomerID
    "phone":            "905551112233",          // sizin Phone — YENİ (2026-06-10), aşağıya bakın
    "phones":           ["905551112233"],        // tüm aktif telefonlar — YENİ
    "featureGroupId":   1,                       // sizin FeatureGroupID
    "featureGroupName": "Lead Aşaması",          // sizin FeatureGroupName
    "selectionMode":    "single",                // sizin SelectionMode (string: single|multi|text)
    "originRequestId":  "invekto-...",           // sizin OriginRequestID — YENİ, aşağıya bakın
    "before": { "featureIds": [5], "featureNames": ["Görüşme"] },        // sizin OldSelections
    "after":  { "featureIds": [7], "featureNames": ["Teklif Verildi"] }  // sizin NewSelections
  }
}
```

| Sizin taslak alanınız | Bizim alan | Not |
|---|---|---|
| `EventType: "customer_status_changed"` | `type: "customer.selection_changed"` | ✅ Sabit ayırt edici. Gövdede `type` varsa sistem olayı, `messages`/`Status` varsa mesaj/ack. |
| `EventID` (UUID, retry'da sabit) | `id` | ✅ Retry'da byte'ı byte'ına aynı gövde gönderilir; ayrıca `X-Invekto-Event-Id` header'ı. |
| `CustomerID` | `data.customerId` | ✅ |
| `Phone` | `data.phone` + `data.phones` | ✅ **Bugün eklendi.** Detay §2. |
| `FeatureGroupID` | `data.featureGroupId` | ✅ |
| `SelectionMode` (1/2/3) | `data.selectionMode` | ✅ String: `"single"`(2) / `"multi"`(1) / `"text"`(3). |
| `NewSelections` (tam küme) | `data.after.featureIds/featureNames` | ✅ Değişiklik sonrası TAM liste; `[]` = temizlendi. |
| `OldSelections` | `data.before.…` | ✅ Her zaman dolu (opsiyonel değil) — null/bilinmiyor durumu yok. |
| `ChangeSource` | `actor.type` | ✅ `user`(panel) / `api`(dış API) / `system` / `zoho-webhook`. `import` yok — toplu import bu seçim tablosuna yazamaz, o kaynaklı event olamaz. |
| `ChangedAt` | `occurredAt` | ✅ ISO8601 UTC. |
| `ChangedBy` | `actor.id` / `actor.name` | ✅ Panel değişikliklerinde dolu. |
| `OriginRequestID` | `data.originRequestId` | ✅ **Bugün eklendi.** Detay §3. |
| `ApiClient` | — | Echo varken gereksiz (firma başına tek API anahtarı var; istemci ayrımı echo ile yapılır). |
| `NewTextValue/OldTextValue` | `after.textValue` / `before.textValue` | ✅ **Bugün eklendi.** Detay §4. |
| `ChangeVersion` | — | ❌ Faz 2. Fallback reçetesi §5 — taslağınızdaki yedek planınız yeterli. |
| `FeatureGroupSystemKey` | — | Yok; eşlemeyi `featureGroupId` ile yapın (ID rename'e zaten dayanıklı; isimler event üretim anında çözülür). |
| `InstanceID` | — | Yok; seçim kanal-bağımsız bir kavramdır. |

---

## 2. `phone` / `phones` (bugün eklendi)

- `phone` = müşterinin ilk aktif telefonu, **yalnız rakam** (`+`/boşluk/tire arındırılmış).
  Müşterinin telefonu yoksa alan **gelmez**.
- `phones` = müşterinin **tüm** aktif telefonları (yalnız rakam, tekrarsız). En sağlam eşleme:
  kendi numaranızı da rakama indirip `phones` içinde arayın (çoklu telefonlu müşteri sorunuzun cevabı).
- **Normalizasyon dürüstlüğü:** Ülke kodu **garanti edilmez** — numara sistemimize nasıl girildiyse o
  (rakam-dışı karakterler ayıklanmış hâlde). WhatsApp kaynaklı müşteri kayıtları pratikte ülke kodludur
  (`905...`) ve inbound mesaj push'larındaki formatla uyumludur; ama elle/CRM'den girilmiş kayıtlarda
  `0555...` gibi formatlar görülebilir. Eşleşmeyen numarada `customerId` → `customer-selections`
  endpoint'i ile doğrulamanızı öneririz.

## 3. `originRequestId` echo (bugün eklendi)

`POST /api/customer-feature-groups/update` çağrınıza opsiyonel **`ClientRequestID`** (string, max 128)
ekleyin; bu update'in tetiklediği event'te `data.originRequestId` olarak **aynen** döner:

```jsonc
// sizin update isteğiniz
{ "CustomerID": 17, "FeatureGroupID": 1, "FeatureIDs": [19], "ClientRequestID": "invekto-9f8e7d" }
// gelen event (data içinde)
"originRequestId": "invekto-9f8e7d"   // → kendi tetiklediğiniz değişiklik; otomasyonu atlayın
```

İkinci güvence katmanı (zaten canlıydı): **natural idempotency** — aynı nihai kümeyle tekrar update
no-op'tur, event üretmez. Yani kendi set ettiğiniz durumu yanlışlıkla tekrar set etseniz bile sonsuz
döngü oluşamaz.

## 4. Text modu (bugün eklendi)

`selectionMode="text"` (serbest metin grupları) artık event üretir. Önerdiğiniz ayrı alanlar yerine
mevcut `before`/`after` yapısının içinde:

```jsonc
{ "selectionMode": "text",
  "before": { "featureIds": [], "featureNames": [], "textValue": "eski not" },
  "after":  { "featureIds": [], "featureNames": [], "textValue": "yeni not" } }
```
`textValue` alanı yoksa o yönde değer yok demektir (`before`'da yok = ilk atama; `after`'da yok = temizlendi).

## 5. Sorularınızın cevapları

1. **Retry politikası:** At-least-once. Geçici hatalarda (5xx/408/429/timeout/DNS) exponential backoff:
   **anlık → +1dk → +5dk → +15dk → +1sa → +6sa** (toplam 6 deneme, ≈7,5 saatlik pencere; sonrası kalıcı
   "failed", admin müdahalesiyle yeniden kuyruklanabilir). 3xx ve diğer 4xx kalıcı fail (retry yok),
   401 en fazla 3 deneme. Event `id` retry'da **sabittir** (saklanan gövde byte'ı byte'ına yeniden gönderilir).
   Ek: URL bazlı circuit breaker (ardışık 10 fail → 5dk-1sa soğuma). **2xx dönün** = kabul; 5xx dönerseniz
   retry ederiz; `id` ile dedupe sizde.
2. **ChangeVersion:** Yok (Faz 2 adayı). Reçete: (a) `customerId+featureGroupId` başına son uyguladığınız
   `occurredAt`'ı saklayın, eski/eşit olanı atın; (b) **zincir kontrolü** — sıra doğruysa her event'in
   `before`'u elinizdeki güncel duruma eşittir, değilse sıra-dışılık/kaçak var demektir; (c) o durumda
   `customer-selections`'tan otoriter durumu çekin. `before` her event'te dolu geldiği için zincir kontrolü
   size ChangeVersion'ın vereceği sinyalin pratikte aynısını verir. Canlıda sıra-dışılık frekansı beklenenden
   yüksek çıkarsa sayacı ekleriz (additive alan — şema kırılmaz).
3. **ClientRequestID echo:** ✅ Var (bugün eklendi, §3). `ApiClient`'a gerek kalmadı.
4. **Kapsam:** Panel (`actor.type="user"`), dış API (`"api"`), Zoho senkronu (`"zoho-webhook"`) değişiklikleri
   push edilir ve doğru işaretlenir. **Import push edilmez** — toplu import bu seçim sistemine yazamıyor
   (eski etiket tablosuna yazar), dolayısıyla import kaynaklı seçim değişikliği diye bir durum yok.
   **No-op atlanır** ✅ (aynı nihai küme → event üretilmez). Zoho kaynaklı değişiklikler de gelir —
   istemiyorsanız `actor.type` ile filtreleyin. Beyanınıza teyit: **grup bazlı filtre yoktur** —
   abonelikte TÜM FeatureGroup'ların değişiklikleri gönderilir, `featureGroupId` ile filtrelemeyi siz yaparsınız
   (tam istediğiniz gibi; bizim tarafta filtre konfigürasyonu gerekmez).
5. **Metin modu:** ✅ Destekleniyor (bugün eklendi, §4) — alanlar `before/after.textValue`.
6. **Merge/delete:** Müşteri birleştirme/silme **event üretmez** (bilginize — "kaybolmuş" müşteriyi
   webhook'tan tespit edemezsiniz; gerekirse `customer-export` ile periyodik mutabakat).
7. **OldSelections:** ✅ `before` her zaman dolu — null/"bilinmiyor" durumu yoktur.
8. **Telefon:** §2'de — `phone`+`phones` eklendi; format dürüstlüğü orada.
9. **Catalog değişiklikleri:** Ayrı bir event yok — catalog'u periyodik yenileyin (örn. günlük + akış
   editörü açılışında). `featureGroupId`/`featureIds` stabil; isimler event üretim anında güncel hâliyle çözülür.

## 6. Yapmanız gerekenler (özet)

1. **Abonelik kurulumu (bir kez):** Invekto panelinden webhook endpoint'i tanımlanır
   (URL + secret + `customer.selection_changed` aboneliği). Secret'ı kurulumda bir kez görürsünüz.
2. **İmza doğrulama:** `X-Invekto-Signature` (HMAC-SHA256, `timestamp.body`) — kod örnekleri ekteki
   müşteri rehberi PDF'inde. İmzasız/doğrulanmamış isteği işlemeyin.
3. **Consumer:** `id` ile dedupe → `occurredAt` guard + `before` zincir kontrolü → durumu uygula /
   otomasyonu tetikle. Kendi update'lerinize `ClientRequestID` koyup `originRequestId` eşleşenleri atlayın.
4. **202/2xx dönün**; kalıcı kaydedemediyseniz 5xx (retry bizden).

> Ekler: **Invekto Webhook Müşteri Rehberi (PDF)** — kurulum + imza doğrulama + retry detayı;
> **Invekto Messaging API Dokümantasyonu (PDF) §6.5** — özet referans.
