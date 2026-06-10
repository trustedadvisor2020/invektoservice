# INMA / WapCRM Bug Raporu — cxapi HSM HEADER Text Değişkenleri (2026-06-10)

> **Durum:** INMA'YA İLETİLDİ ✅ (2026-06-10, Q gönderdi). Yanıt/fix bekleniyor.
> **İlgili paket:** FEAT-PROJELER PKT-14 / HSM header-param vendor gap (`bc4b705a`, plan `arch/plans/20260610-hsm-header-param-vendor-gap.json`)
> **Bizim taraf mitigasyonu:** Dashboard param eşleme bölümünde amber uyarı CANLI; wire davranışı değişmedi (forward-compat — fix gelince gönderdiğimiz değer otomatik uygulanır).

---

## ⬇️ INMA'ya gönderilecek metin (kopyala-gönder)

---

**Konu: cxapi — HSM şablon HEADER text değişkenleri requiredInputs'ta eksik + chatoperation'da uygulanmıyor (2 ilişkili bug)**

Merhaba,

cxapi üzerinden onaylı şablon (HSM) gönderiminde HEADER bölümündeki text değişkenleriyle ilgili iki ilişkili sorun tespit ettik. Kanıtlarıyla birlikte aktarıyoruz.

### Ortam

- **Instance:** 6570 ("WapCRM Official", Cloud API / InstanceType=1, aktif)
- **Company Code:** 5050 (Regex Danışmanlık15 — sandbox)
- **Örnek şablon:** `1664033448202974` — header: `Test {{hname}}`, body: `{{name}}` içeriyor
- **Tarih:** 2026-06-10

### Bug 1 — `POST /api/templates` (template-list): HEADER text değişkenleri `requiredInputs`'ta listelenmiyor

Şablon listesini çektiğimizde, HEADER'da text değişkeni olan şablonlarda bu değişkenler `requiredInputs` dizisinde **hiç dönmüyor**. BODY değişkenleri (örn. `name`) ve BUTTON değişkenleri (örn. `btn1_url`) doğru dönüyor.

- Örnek: şablon `1664033448202974` → `preview.header = "Test {{hname}}"` ama `requiredInputs = [ { kind:"text", paramKey:"name" } ]` — `hname` yok.
- Bu durum tekil değil: instance 6570'teki **9/9 şablonda sistematik** olarak aynı (header'ında text değişkeni olan tüm şablonlarda HEADER anahtarı eksik, BODY+BUTTON anahtarları mevcut).

### Bug 2 — `POST /api/chatoperation` (template gönderimi): HEADER paramKey'i gönderilse bile UYGULANMIYOR

Entegrasyon rehberindeki (testapi.wapcrm.net) wire formatına uygun şekilde header değişkenini `template.parameters` içinde gönderiyoruz:

```json
{
  "instanceID": 6570,
  "userID": 12,
  "chatPhoneNumber": "905XXXXXXXXX",
  "template": {
    "templateId": "1664033448202974",
    "parameters": [
      { "paramKey": "name",  "value": "taner" },
      { "paramKey": "hname", "value": "taner" }
    ]
  }
}
```

**Beklenen:** Alıcıya header `Test taner`, body'de `taner`.
**Gerçekleşen:** Body değişkeni doğru uygulandı (`taner`), ancak header'a gönderdiğimiz değer **yok sayılıp** şablonun Meta onayında kullanılan örnek değer basıldı → alıcıya `Test Müşterimiz` gitti.

Kendi tarafımızda gönderim anındaki parametre snapshot'ını doğruladık: `template_params = { "name": "taner", "hname": "taner" }` şeklinde **ikisi de wire'a çıktı** (DB kaydı mevcut) — yani sorun bizim gönderimimizde değil, cxapi'nin header parametresini işlememesinde.

### Talebimiz (2 madde)

1. **`/api/templates` requiredInputs:** HEADER'daki text değişkenleri de listelensin — örn. `{ "kind": "text", "location": "HEADER", "paramKey": "hname" }` (mevcut BODY/BUTTON davranışıyla simetrik).
2. **`/api/chatoperation`:** `template.parameters` içinde gönderilen HEADER paramKey'leri gönderime uygulansın (şu an Meta-onaylı örnek değer basılıyor).

### Etki

Header'ında değişken olan tüm HSM şablonlarında alıcıya kişiselleştirilmiş değer yerine onaylı örnek değer gidiyor. Müşteri-görünür bir sorun olduğu için öncelikli değerlendirmenizi rica ederiz. Biz kendi tarafımızda parametreyi göndermeye devam ediyoruz; düzeltme yayınlandığında ek bir değişiklik gerekmeden çalışacak.

Teşekkürler.

---

## Dahili not (rapora dahil DEĞİL)

- Doğrulama planı (fix geldiğinde): hname'li şablonla test-send → header substitution gerçekleşmeli + Dashboard'daki amber uyarı kendiliğinden kaybolmalı (uyarı `requiredInputs` üyeliğine bağlı, kod değişikliği gerekmez).
- Q kararı 2026-06-10: **gerçek müşteri pilot tenant'ı bu fix'i bekliyor** (ön koşul).
