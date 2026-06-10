# INMA Contract Teklifi — Customer Status Change Webhook (2026-06-10)

> **Durum:** DRAFT v1.1 (Codex consult critique uygulandı) — Q'ya teslim; Q INMA'ya iletecek.
> **İlgili paket:** FEAT-INMA-PIPELINE-V2 C2/C3/C4 (KARAR-INMA-PIPELINE-CONTRACT blocker'ını açar)
> **Makine-okunur contract:** `arch/contracts/inma-customer-status-webhook.json` (v1.1)
> **Q kararları (2026-06-10):** (1) mevcut webhook reuse (dedike endpoint YOK — cxapi ack pattern'i), (2) mevcut auth modeli aynen (HMAC yok), (3) TÜM FeatureGroup değişiklikleri push edilir, INSE filtreler.
> **Kilit keşif:** INMA'nın mevcut `customer-feature-groups` API'si (PDF §6: catalog / customer-selections / update) "Set Customer Status" action'ı ve dropdown'ı TAMAMEN karşılıyor → INMA'dan istenen TEK yeni iş bu webhook event'i (+ opsiyonel 2 küçük iyileştirme: ChangeVersion, ClientRequestID echo).
> **Codex critique'ten gelen revizyonlar:** required/recommended/optional katmanlaması · ChangeVersion ordering primitifi · OriginRequestID/ApiClient loop koruması · emit kuralları · free-text ayrı alanlar · telefon normalizasyon sözleşmesi · dedupe 30 gün · durable-accept-then-202.

---

## ⬇️ INMA'ya gönderilecek metin (kopyala-gönder)

---

**Konu: Müşteri Durumu (FeatureGroup) değişiklik webhook'u — entegrasyon teklifi**

Merhaba,

Müşteri durumu (Lead Aşaması vb. FeatureGroup seçimleri) değiştiğinde Invekto tarafında otomasyon tetikleyebilmek istiyoruz (örn. "Teklif Verildi"ye geçen müşteriye otomatik akış başlatma). Mevcut API'nizi inceledik; ihtiyacın büyük kısmı zaten karşılanıyor, **tek eksik bir webhook event'i**. Teklifimiz aşağıda — alan adları/şekli üzerinde tamamen esneğiz, push altyapınıza en az iş çıkaracak biçime uyarlarız.

### Zaten kullanacağımız mevcut API'leriniz (değişiklik istemiyoruz)

- `GET/POST /api/customer-feature-groups/catalog` — grup + seçenek listesi (akış editörümüzdeki durum dropdown'ı için)
- `POST /api/customer-feature-groups/customer-selections` — müşterinin güncel seçimleri
- `POST /api/customer-feature-groups/update` — akış aksiyonumuz müşteri durumunu buradan set edecek (Source="api" audit'iniz bizim için yeterli)

### Talep: müşteri durumu değişince webhook push'u

Bir müşterinin herhangi bir FeatureGroup seçimi **etkin olarak değiştiğinde** (panel, API veya import kaynaklı; aynı nihai kümeyle no-op update hariç), firmanın **mevcut webhook URL'ine** (bizim entegrasyonda `https://services.invekto.com/api/v1/webhook/event?companyId=X` — şu an mesaj ve teslimat-ack push'larınızın gittiği adres) yeni bir event push edilmesini rica ediyoruz. **Yeni bir push hedefi/konfigürasyon gerekmiyor** — mevcut boruya yeni bir gövde şekli.

**Zorunlu alanlar (minimum iş):**

```jsonc
{
  "EventType": "customer_status_changed",   // sabit ayırt edici alan
  "EventID": "9f8e7d6c-...",                // UUID; aynı değişikliğin retry'larında AYNI kalmalı
  "CustomerID": 17,
  "Phone": "905551112233",                  // yalnız rakam, ülke kodlu, '+'/'00'/boşluk yok —
                                            // inbound mesaj push'larınızla aynı normalizasyon
  "FeatureGroupID": 1,
  "SelectionMode": 2,                       // 1=Çoklu, 2=Tek, 3=Metin
  "NewSelections": [                        // değişiklik sonrası TAM liste (küme — sıra önemsiz);
    { "FeatureID": 19, "FeatureName": "Teklif Verildi" }
  ],                                        // [] = seçim temizlendi
  "ChangeSource": "panel",                  // "panel" | "api" | "import" | "system"
  "ChangedAt": "2026-06-10T14:30:00Z"       // ISO8601 UTC
}
```

**Çok değerli olur (yapılabilirse):**

```jsonc
{
  "ChangeVersion": 8,                       // aynı CustomerID+FeatureGroupID için monotonik artan
                                            // sayaç — sıra dışı teslimde eski event'i atmamızı sağlar
  "OriginRequestID": "invekto-...",         // update çağrımıza ekleyeceğimiz ClientRequestID'nin
                                            // echo'su — kendi tetiklediğimiz değişikliği tanırız
  "ApiClient": "invekto"                    // ChangeSource="api" iken hangi istemci (echo olmazsa bu)
}
```

**Opsiyonel (varsa güzel, yoksa sorun değil):**

```jsonc
{
  "FeatureGroupSystemKey": "customer_stage", // varsa eşleşmeyi bununla yaparız (rename'e dayanıklı)
  "FeatureGroupName": "Lead Aşaması",
  "OldSelections": [ ... ],                  // null = bilinmiyor; [] = önceki durum bilinen-boş
  "ChangedBy": "agent@firma.com",            // sadece gösterim/audit
  "InstanceID": 6570,
  "NewTextValue": "...", "OldTextValue": "..." // SelectionMode=3 (Metin) gruplarında değer burada;
                                               // NewSelections o durumda boş kalır
}
```

Bizim tarafımızın davranışı:

- Gövdede `messages` yok + `EventType` var → mesaj/ack işlemenizden bağımsız yeni dalda işlenir; **mevcut mesaj ve ack push'larınız hiçbir şekilde etkilenmez**.
- **202 = event'i kalıcı kabul ettik.** Kalıcı kaydedemezsek 5xx döneriz — bu durumda retry etmenizi rica ederiz. `EventID` ile dedupe yaptığımız için at-least-once retry güvenlidir.
- Auth/whitelist mevcut webhook kanalıyla aynı — ek bir şey kurmanız gerekmez.
- Tüm grupların değişikliklerini gönderebilirsiniz; filtrelemeyi biz yaparız (sizin tarafta filtre kodu gerekmez).

### Sorularımız

1. **Retry politikası:** Webhook push'larınız at-least-once mı; başarısız push kaç kez/hangi aralıklarla denenir? 5xx aldığınızda retry var mı? `EventID` retry'da sabit kalır mı?
2. **ChangeVersion:** Aynı müşteri+grup için monotonik artan bir sayaç sağlanabilir mi? (Yoksa her event'te güncel durumu `customer-selections`'tan doğrularız — sizin API yükünüz artar.)
3. **ClientRequestID echo:** `update` çağrımıza opsiyonel `ClientRequestID` ekleyip webhook'ta `OriginRequestID` olarak geri alabilir miyiz? Olamıyorsa API-kaynaklı değişikliklerde `ApiClient` (hangi istemci) verilebilir mi? (Kendi set ettiğimiz durumun geri dönüp tekrar otomasyon tetiklememesi için.)
4. **Kapsam:** API/import/system kaynaklı değişiklikler de push edilir mi ve `ChangeSource` doğru işaretlenir mi? No-op update'lerde (aynı nihai küme) push atlanır mı?
5. **Metin modu:** `SelectionMode=3` gruplarında önerdiğimiz ayrı alanlar (`NewTextValue`/`OldTextValue`) uygun mu?
6. **Merge/delete:** Müşteri birleştirme/silme senaryolarında seçim-değişikliği event'i üretilir mi? (Üretilmiyorsa sorun değil — bilmemiz yeterli.)
7. **OldSelections:** Eski seçim listesi sağlanabilir mi? (Maliyetliyse `null` gönderin, kendi kaydımıza dayanırız.)
8. **Telefon:** `Phone` inbound mesaj push'larınızla aynı normalizasyonda mı? Çoklu telefonlu müşteride hangi numara gönderilir?
9. **Catalog değişiklikleri:** Grup/seçenek ekleme-silme-yeniden adlandırma için ayrıca bir event var mı, yoksa catalog'u periyodik yenilememiz mi gerekir?

Uygun bulursanız kısa bir teknik görüşmede netleştirebiliriz.

Teşekkürler.

---

## Dahili notlar (rapora dahil DEĞİL)

- **Supersede:** 2026-05-12 memory'deki dedike `POST /api/v1/inbound/inma/customer-status-change` fikri → Q kararı 2026-06-10 ile mevcut webhook reuse'a evrildi (cxapi ack ingress pattern'i, memory `project_cxapi_ack_ingress_design` ile tutarlı).
- **Loop koruması (3 katman, C3 kuralı):** (1) `OriginRequestID` bizim flow-run kaydımızla eşleşiyorsa SUPPRESS; (2) yoksa `ApiClient=="invekto"` SUPPRESS; (3) o da yoksa `ChangeSource=="api"` varsayılan SUPPRESS (tenant-config ile açılabilir — diğer entegrasyonların meşru API değişikliklerini kaçırma riski bilinçli). Opaque store her kaynağı kaydeder; bastırma yalnız flow-trigger katmanında.
- **Ordering/stale koruması (C2 kuralı):** `ChangeVersion` varsa yerel son version karşılaştırması; yoksa flow tetiklemeden önce `customer-selections` fetch-verify. Hızlı ardışık değişikliklerde ara durumlar collapse olabilir — kabul edilen trade-off (C2 interview'da Q onayına gelir).
- **202 semantiği ack'ten FARKLI:** durable-write-then-202; persist edilemezse 5xx (INMA retry'ına yaslan). Delivery-ack koşulsuz-202 kalır.
- **customer_status derivasyonu:** mode 2 → `NewSelections[0].FeatureName`; `[]` → NULL; mode 1 → alfabetik CSV; mode 3 → `NewTextValue`. Ham event audit'e FeatureID'lerle yazılır (rename güvenliği).
- **Kimlik:** Phone = bizim anahtar; CustomerID-Phone çelişkisinde Phone esas; eşleşmeyen telefon → drop-with-audit + WARN, lead-create YOK.
- **INSE tarafı iş listesi** (INMA onayı sonrası C2/C3/C4 paketleri): webhook handler 3. dal + durable raw-event tablosu + dedupe (30 gün) + `leads.customer_status TEXT` migration + FlowGraphV2 trigger + INodeHandler action + Backend catalog proxy/cache + suppress-tablosu (flow-run-uuid, TTL ~1h). Detay: `arch/contracts/inma-customer-status-webhook.json` → `inse_side_implementation_notes`.
- **Eski kolon:** `leads.pipeline_status VARCHAR(30)` Zoho-out sonrası sahipsiz — C2 migration'ında ele alınacak.
- **Dedupe 30 gün** (Codex: 24h kısa — geç retry/manuel replay riski).
