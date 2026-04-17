# INMA — J2: "STOP Diyen Kişi Listeden Çıksın"

> **Hedef:** INMA backend + Angular ekipleri
> **Pilot:** Dent Adavista çok şehirli yurt dışı etkinlik kampanyası
> **INSE karşı plan:** `arch/plans/20260417-j2-opt-out-inse-sync.json`
> **INMA tahmini efor:** 2-3 gün (1 backend dev + 0.5g Angular)

---

## Bir Satırda INMA'dan İstenenler

1. Contact kaydına "bu kişi opt-out" bilgisi tutan bir alan ekleyin
2. INSE'nin "şu kişiyi opt-out yap" diyebileceği bir endpoint açın
3. INSE'nin "şu kişiyi tekrar opt-in yap" diyebileceği bir endpoint açın
4. Opt-out olan kişiye pazarlama mesajı göndermeyi engelleyin (randevu onayı gibi servis mesajları geçsin)
5. Contact detay ekranında opt-out rozeti ve "tekrar izin ver" butonu gösterin

Gerisini INMA tarafı bilir — ekip 5 yıldır bu sistemi yazıyor.

---

## 1. "Opt-Out Bilgisi" Alanı

**Senaryo:**
Dent Adavista müşterisi Sarah, kampanya davetini aldı ama katılmak istemediği için WhatsApp'tan "STOP" yazdı. Bu andan itibaren INMA'daki Sarah kaydında bir işaret olmalı: "Bu kişi artık mesaj almak istemiyor." INMA admin paneli bu kişiyi açtığında kırmızı bir rozet görsün.

**Neden?**
Bugün INMA'da bu bilgi yok. Sarah STOP yazsa bile başka bir kampanya listesine eklenirse mesaj yine gider. Compliance açısından (GDPR, KVKK) kişinin "listede değilim" hakkı tek yerde — contact kaydında — saklanmalı.

**Nasıl Olmalı?**
Contact kaydının yanında: "opt-out mu?", "neden?", "hangi kanaldan?", "ne zaman?" bilgileri. Sarah opt-in olursa bu kayıt silinmez — "opt-out geçmişi" kalır (audit için). Tekrar opt-out olursa zaman damgası güncellenir.

**INSE'den ne gerekir?**
Hiçbir şey. Sadece data modelinin genişletilmesi.

---

## 2 & 3. "Opt-Out / Opt-In Yap" Endpoint'leri

**Senaryo:**
Sarah WhatsApp'tan STOP yazdı. INSE bu mesajı işliyor — INSE'nin AI agent'ı Sarah'ya "You've been unsubscribed" diyor, sonra INMA'ya bir bildirim atıyor: "Sarah'yı opt-out yap." INMA bu bildirimi alıp Sarah'nın kaydını işaretliyor. Tersi durumda Sarah bir hafta sonra "wait, I changed my mind" diye yazarsa INSE bu sefer INMA'ya "Sarah'yı opt-in yap" diyor.

**Neden?**
INSE, WhatsApp'tan gelen mesajları AI ile dinliyor (STOP keyword'u, sohbet bağlamı). Bu dinleme INMA'da yok. INSE bildiği bilgiyi INMA'ya iletebilmeli ki contact state'i iki sistemde de senkron olsun. Bildirim mekanizması = basit bir HTTP çağrısı.

**Nasıl Olmalı?**
INSE → INMA yönünde iki basit endpoint: "bu kişiyi opt-out yap" ve "bu kişiyi opt-in yap." Body'de sadece iki bilgi yeter: **neden** (örn: "Keyword: STOP") ve **kanal** (örn: "whatsapp"). Auth mevcut `X-CIB-SecretKey` pattern'i. Idempotent olsun — INSE aynı çağrıyı iki kez yaparsa ikinci seferde de 200 dönsün, sessizce yut.

**INSE'den ne gerekir?**
- INSE bu endpoint'i zaten çağırabilecek altyapıyı kurdu (outbox pattern + Hangfire).
- INMA endpoint'i hazır olmadığı sürece INSE "NoOp" modda kalıyor — çağrılar sıraya giriyor, kaybolmuyor.
- INMA teslim ettikten sonra INSE config'te tek flag değişiyor ve biriken kayıtlar arka planda gönderiliyor.

---

## 4. Opt-Out Olan Kişiye Gönderimi Engelleme

**Senaryo:**
Ertesi hafta Dent Adavista başka bir kampanya başlatıyor — 300 kişilik listeye "Bu hafta %20 indirim" mesajı gidecek. Listede Sarah da var (eski bir müşteri). INMA bu mesajı göndermeden önce Sarah'nın opt-out olduğunu görüp onu atlamalı. Ama aynı gün Sarah'nın daha önce aldığı randevu için bir hatırlatma gitmesi gerekiyor — **bu geçmeli** (randevu onayı kampanya değil, servis mesajı).

**Neden?**
Opt-out "tüm mesajları sustur" değil — "pazarlama mesajlarını sustur" demek. Müşterinin hayatını etkileyen bildirimler (randevu, fatura, ödeme makbuzu) geçmeli. Bu ayrım olmazsa ya müşteri randevusuna gelmez, ya da opt-out sistemi güvenilmez olur ve kimse STOP yazmaya cesaret edemez.

**Nasıl Olmalı?**
Mesaj gönderim akışına iki kontrol eklensin:
- Contact opt-out mu? (evet ise devam et alttakine)
- Mesaj "servis/transactional" mı yoksa "pazarlama" mı?
  - Pazarlama → bloke (skip) + yanıtta "bu kişi atlandı" bilgisi
  - Servis (randevu onayı, Meet linki, ödeme alındı vb.) → gönder + audit log'da "opt-out bypassed for transactional"

INSE gönderim API'ye çağrı atarken body'de `transactional: true/false` gönderecek. INMA'nın işi sadece bu flag'e saygı göstermek.

**INSE'den ne gerekir?**
INSE her outbound çağrısında template kategorisine göre flag'i set ediyor:
- `appointment_confirmed_*`, `meeting_link_sent_*`, `payment_receipt_*` → `transactional: true`
- Kampanya template'leri, drip mesajları, welcome → `transactional: false`

INMA bu mantığı yeniden kurmak zorunda değil. INSE söyler, INMA uygular.

---

## 5. Contact Detay Ekranında Rozet + Buton

**Senaryo:**
Dent koordinatörü INMA admin panelinden Sarah'nın kaydını açıyor. Üstte kırmızı bir rozet görüyor: "⚠️ Opt-out (Keyword: STOP · 17 Nisan)". Sarah telefon etti, "tamam bilgi alayım" dedi — koordinatör "Tekrar izin ver" butonuna basıyor. Rozet kalkıyor, Sarah tekrar kampanyalara girebiliyor. Audit log geçmişi kayıt ediyor.

**Neden?**
Koordinatörün doğrudan INMA UI'dan bu işlemi yapabilmesi lazım — INSE dashboard'a gitmek zorunda olmasın. Opt-out bilgisi görünür olmazsa koordinatör yanlışlıkla yine kampanyaya dahil eder.

**Nasıl Olmalı?**
Contact detay component'inde mevcut header'a rozet + button eklensin. Kickoff brief §4'teki Angular örneği zaten pattern'i gösteriyor — ekip o komponenti biliyor, aynı stil yeter.

**INSE'den ne gerekir?**
Hiçbir şey. Pure INMA UI + INMA endpoint çağrısı.

---

## INMA ↔ INSE Sıralama

| Kim | Ne Yapar | Ne Zaman |
|-----|----------|----------|
| INMA | Data model + 3 endpoint + outbound filter + Angular rozet | Pilot go-live -7g |
| INMA QA | 7 test senaryosu (happy path, idempotency, opt-in recovery, marketing block, transactional pass, validation, auth) | Pilot go-live -3g |
| INSE (Q) | Config flip `NoOp → Http`, Hangfire outbox drain | Pilot go-live -2g |
| Pilot başlar | — | T0 |

**Risk yönetimi:** INMA gecikirse pilot yine başlar. INSE tarafı `NoOp` modda çalışır — STOP yazan lead'e INSE bloke eder, INMA'ya da "biriktireyim" der. INMA hazır olduğunda biriken tüm kayıtlar arka planda INMA'ya akar. **Pilot downtime sıfır.**

---

## Sorular

INMA ekibinden beklenen:
1. **Efor onayı:** 2-3g gerçekçi mi?
2. **Endpoint naming:** `/opt-out` vs `/opted-out` vs `/unsubscribe` — INMA konvansiyonu ne diyor?
3. **Transactional flag zaten var mı?** Yoksa start-chat body'de yeni alan mı (`transactional: true/false`)?
4. **Idempotency:** Aynı contact'a iki PATCH arka arkaya — 200 + silent mi yoksa 409 mu tercih?

INSE tarafı sahibi: **Q (Taner)**. Teknik detaylar için `arch/plans/20260417-j2-opt-out-inse-sync.json` referans.
