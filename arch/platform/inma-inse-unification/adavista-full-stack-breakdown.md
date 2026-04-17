# Dent Adavista Pilotu — Tüm Yapı: INSE ve INMA Dağılımı

> **Müşteri:** Dent Adavista Dental Clinic (Kuşadası)
> **Kampanya:** Çok şehirli yurt dışı etkinlik (2 gün, 2 lokasyon)
> **Kanal:** WhatsApp + Instagram + Telegram (email yok)
> **Hedef:** Gerçek saha pilotu — çoğu firmanın yapmak istediği ama nasıl yapacağını bilmediği yapı. Bu pilot referans implementasyon.

---

## Bir Satırda INMA'dan İstenenler

1. Mesaj gönderirken şablon içindeki yer tutucuları (`{{name}}`, `{{city}}`) değerle değiştirin — **J1**
2. Contact'a "opt-out" alanı ekleyin + 2 endpoint + gönderim filtresi — **J2**
3. Business-initiated ilk-temas template'leri Meta'ya doğru kategoride (marketing/utility) submit edilebilsin, onay durumu INSE'den görünsün — **J-HSM** (mevcut template onay akışının INSE'ye açılması)
4. Contact bazında 24-saat window state'i (son gelen mesaj zamanı) INSE'den okunabilsin — **J-WND**
5. *(Opsiyonel, pilot sonrası)* Toplu gönderim endpoint'i — **J4**

**Kritik olan bu dört madde.** 5. pilot sonrası scale için. INMA ekibi 5 yıldır bu sistemi yazıyor; J-HSM ve J-WND zaten mevcut INMA iç altyapısından geliyor, sadece INSE'nin okuyabileceği şekilde "açmak" yeterli.

> **Tenant yönetimi INMA'dan bir şey istemiyor** — INSE tarafı lazy provisioning ile çözüldü (migration `016-tenant-registry-auto-gen-seq.sql`, 2026-04-17). Kullanıcı ilk kez INSE endpoint'ine eriştiğinde tenant otomatik yaratılır. `tenant.created` event'i, tenant list export, bulk SQL backfill — **üçü de iptal**.

---

## Adavista Ne İstiyor? (Business Resim)

Dent Adavista yurt dışında 2 gün sürecek, 2 farklı şehirde gerçekleşecek bir diş kliniği etkinliği yapacak. Reklam verip landing page üzerinden lead toplayacaklar. Sonra:

1. Lead geldiği anda bir AI **WhatsApp'tan karşılayacak** — robot değil, insan gibi hissettirecek
2. Kullanıcı sorular sorarsa AI **FAQ cevaplayacak** (fiyat, konum, güvenlik, xray vs.) — EN dilde öncelik, gerekirse 9 dil fallback
3. Randevu isteyenlere **Google Meet ile online consultation** teklifi
4. Etkinlik sonrası 3 aşamada **follow-up mesajları** (3. gün, 7. gün, 14. gün)
5. "STOP" yazan kişi bir daha mesaj almayacak
6. Tüm bu akışlar etkinlik tarihleri arasında (1-20 Mart penceresi) otomatik çalışacak

**Pilot volümü küçük (~200 lead)** ama yapıyı kurmuş olacağız — sonraki klinikler, hatta farklı sektörler (e-ticaret, eğitim) aynı iskeleti kullanacak.

---

## ⚠️ Kritik Akış Modeli — "Business-Initiated" (Biz Başlatıyoruz)

**Bu pilot'un omurgası, standart WhatsApp DM senaryosundan farklı.** Çoğu WA örneği "müşteri yazar, biz cevaplarız" (customer-initiated) mantığı. Bizde akış **tersi:**

```
[Müşteri] → Meta Lead Ads formu veya landing page formu doldurur
           → Ad + telefon + izin kutusu işaretli (opt-in kanıtı)
    ↓
[INSE] → Form verisi webhook'tan gelir → lead oluşturulur
    ↓
[BİZ BAŞLATIYORUZ] → WhatsApp'tan KARŞIMIZA İLK BİZ geliyoruz
    ↓ (müşteri henüz bize yazmadı — 24h window YOK)
    ↓
[WA HSM Template] → İlk mesaj Meta-onaylı "template message" olmak ZORUNDA
    ↓
[Müşteri cevap verirse] → 24 saatlik window açılır → freeform mesaj serbest
[Müşteri cevap vermezse] → Sonraki mesaj yine template olmalı
```

### Bunun Sonuçları

| Konu | Sonuç |
|------|-------|
| **İlk mesaj formatı** | Meta'ya önceden onaylatılmış WA HSM template'i. Freeform mesaj = Meta ban riski. |
| **Template kategorileri** | Marketing (kampanya, promo) / Utility (randevu, fatura) / Authentication. Yanlış kategori = reject/bloke. |
| **Opt-in kanıtı** | Form submission zamanı + IP + "WhatsApp ile iletişim kabul ediyorum" checkbox → her lead kaydıyla birlikte saklanır. Meta audit'inde kanıt. |
| **24h window mantığı** | Müşteri cevap verene kadar freeform yok. Cevap geldikten sonra 24 saat freeform serbest; sonra tekrar template. |
| **FAQ cevapları** | Müşteri soru sorarsa = 24h window açık = freeform AI cevabı serbest. İlk temas değilken FAQ freeform OK. |
| **Follow-up (Day 3/7/14)** | Her follow-up mesajı YİNE template (window kapanmış olur). Utility kategorisi tercih — marketing daha kısıtlı. |
| **Opt-out bypass** | Transactional template'ler (appointment_confirmed, meeting_link) utility kategori + opt-out bypass ile. |

### INMA ↔ INSE Sorumluluk

- **INMA:** Template onay workflow'u (Meta submission, kategori yönetimi, reject nedenlerini UI'a göstermek), gönderim anında doğru template'i yollamak, 24h window state'ini contact düzeyinde bilmek
- **INSE:** Hangi lead'e ne zaman hangi template gidecek kararını vermek, freeform mesajları sadece window açıkken göndermek, template fallback chain (onaylanmamış template → utility fallback)

### Dent'e Özel Kullanım

- **46 template** hazır: 10 welcome (ilk temas = business-initiated, MUTLAKA HSM) + 36 FAQ cevabı (24h window açıkken freeform veya template mix)
- **Meta'ya submit edilecek en az 6 template:** 2 welcome (date-variant + no-date), 3 follow-up (day 3/7/14), 1 opt-out confirmation
- **FAQ cevapları** serbestçe gönderilebilir çünkü müşteri soru soruyor (window açık)
- **Form submission** consent proof olarak leads tablosunda saklı (`consent_marketing=true`, `consent_timestamp`, `consent_source=landing_form`)

---

## Yapı Taş Taş — Her Parça Kimde?

| # | Yetenek | INSE Tarafı | INMA Tarafı |
|---|---------|-------------|-------------|
| 1 | AI karşılama agent (46 template) | Template rotation + FAQ intent detection + A/B | Template onayı (WA HSM) + mesajı göndermek |
| 2 | Çok dilli (9 dil) | Lang detection + translation layer + locale fallback | — (INSE çözer) |
| 3 | Çok şehirli kampanya yönetimi | Campaign config + flow variable (`{{city}}`, `{{date}}`) | — (INSE lokal) |
| 4 | Landing page'den lead geldiğinde | Webhook endpoint + field mapping | Contact upsert (phone ile) |
| 5 | Google Meet consultation | Meeting link oluşturma + ICS + reminder job | Mesajı göndermek (servis mesajı) |
| 6 | 3 aşama follow-up drip | Scheduled job + stage templates + exit conditions | Mesajı göndermek |
| 7 | STOP keyword opt-out | AI STOP algıla + outbox + transactional bypass | **J2** — contact flag + gönderim filtresi |
| 8 | Template değişken render | Template catalog + variable substitution (preview) | **J1** — gönderim anında `{{name}}` değerle değişsin |
| 9 | Tek login (SSO) | ✅ DONE (welcome introspection, 2026-04-16) | — (mevcut welcome endpoint yeter) |
| 10 | Yeni tenant açılışı | ✅ Lazy provisioning IN-PROGRESS (migration 016) | — (hiçbir şey istenmiyor) |
| 11 | Business-initiated ilk mesaj (HSM) | Template fallback chain + window guard | **J-HSM** — template onay durumu API'de görünsün |
| 12 | 24h window state takibi | Gönderim öncesi window check | **J-WND** — contact.last_inbound_at API'de görünsün |

**Sonuç:** INMA'dan pilot için **4 madde (J1 + J2 + J-HSM + J-WND)** kritik. J-HSM ve J-WND zaten INMA iç altyapısında mevcut — sadece mevcut endpoint'lere alan genişletmesi.

---

## INMA'dan İstenenler — Her Biri Ayrıntılı

### J1 — "Gönderirken Mesajın İçinde Değişken Olsun"

**Senaryo:**
Dent'in 46 template'i var. Welcome mesajı şu şekilde:

> *"Hi {{name}}! Dent Adavista'ya ilginiz için teşekkürler. Etkinliğimize {{city}} ({{date}}) için kayıt olmak ister misiniz?"*

Bu mesaj Sarah'ya gittiğinde şöyle görünmeli:

> *"Hi Sarah! Dent Adavista'ya ilginiz için teşekkürler. Etkinliğimize <Şehir A> (<tarih>) için kayıt olmak ister misiniz?"*

INSE karar verir kime ne gönderileceğine, değerleri bilir. INMA'dan isteği "al bu template'i, bu değerlerle gönder" şeklinde atacak.

**Neden?**
Şu an INMA gönderim API'si template id'sini alıyor ve aynen gönderiyor — `{{name}}` yazısı olduğu gibi gidiyor. Kullanıcı "Hi {{name}}" mesajı alıyor ki bu utanç verici. Template değerlerinin gönderim anında değiştirilmesi evrensel bir ihtiyaç, INMA altyapısının doğal bir parçası olmalı.

**Nasıl olmalı?**
Gönderim endpoint'i (`start-chat`) body'sine `variables` alanı eklensin. Örn:
```json
{
  "templateId": 1042,
  "phone": "+353...",
  "variables": { "name": "Sarah", "city": "<Şehir A>", "date": "<tarih>" }
}
```
INMA template'i alır, `{{name}}` → "Sarah" yapar, WhatsApp'a öyle gönderir. Eksik değişken varsa (template `{{x}}` bekliyor ama INSE göndermedi) hata dönsün.

**INSE'den ne gerekir?**
INSE zaten her gönderim çağrısında doğru değerleri hazırlıyor — sadece INMA'nın bu değerleri kabul etmesi yeterli. INSE template'leri önceden "preview rendered" göndermek yerine placeholder'larla gönderip INMA'nın render etmesini tercih ediyor (Meta/WhatsApp template parametrelerini INMA'nın denetimi altında tutmak için).

---

### J2 — "STOP Diyen Kişi Listeden Çıksın"

**Senaryo:**
Sarah kampanya davetini aldı, katılmak istemedi, WhatsApp'tan "STOP" yazdı. INSE bunu algıladı, AI agent "You've been unsubscribed" dedi ve INMA'ya bildirdi. Ertesi hafta Dent başka bir kampanya başlatıyor — 300 kişilik listede Sarah da var ama INMA otomatik atlıyor. Ama aynı gün Sarah'nın önceki randevusu için hatırlatma mesajı gidiyor — **o geçiyor** (servis mesajı, pazarlama değil).

**Neden?**
Opt-out tek yerde (contact kaydında) tutulmalı ki başka kampanyalarda yeniden yakalanmasın. "Servis" vs "pazarlama" ayrımı olmazsa ya müşteri randevusuna gelmez ya da kimse STOP yazmaya cesaret edemez.

**Nasıl olmalı?**
- Contact kaydında 4 yeni alan: `opted_out`, `reason`, `source`, `opted_out_at`
- 2 endpoint: "opt-out yap" ve "opt-in yap" (INSE çağırır)
- Gönderim akışında kontrol: opt-out ise ve mesaj `transactional=false` ise bloke et
- Contact detay ekranında rozet: "⚠️ Opt-out"

**INSE'den ne gerekir?**
- STOP keyword algılama (INSE'de zaten var: `OptOutManager`)
- Her gönderim çağrısında `transactional: true/false` flag set etme (INSE appointment/meeting/payment template'lerini otomatik transactional yapacak)
- INMA endpoint'i hazır olana kadar INSE "NoOp" modda — çağrılar kaybolmuyor, sıraya giriyor, INMA teslim edince otomatik drain oluyor

**Detaylı plan:** `inma-plan-j2-opted-out.md`

---

### J-HSM — Template Onay Durumu INSE'den Görünsün

**Senaryo:**
INSE Dent için 10 welcome + 3 follow-up (day 3/7/14) + 1 opt-out confirmation template'i Meta'ya submit etti. 2 tanesi approved, 1 tanesi reject ("Tarih yer tutucusu belirsiz"), kalanları pending. INSE flow engine bir lead'e welcome göndermek üzere — template henüz approved değilse ya onaylı fallback'e geçmeli ya bekletmeli. Bu bilgi INSE'de olmazsa flow pending template'i gönderir, Meta 2020 error code (template not approved) döner, lead için hiçbir mesaj gitmez ve sessizce kaybedilir.

**Neden?**
Business-initiated akışın omurgası Meta-onaylı template'lerden geçiyor. INSE karar mekanizması "hangi template'i göndereyim?" sorusunu sorduğunda cevap "onaylı olan" olmalı. INMA zaten bu bilgiyi kendi admin panelinde gösteriyor (Meta callback'ten geliyor). Sadece INSE'nin de okuyabilmesi lazım.

**Nasıl olmalı?**
Mevcut template listesi endpoint'ine bir alan eklensin: `meta_approval_status` (`approved` | `pending` | `rejected`), `meta_category` (`marketing` | `utility` | `authentication`), `rejection_reason` (varsa). Yeni endpoint gerekmez — mevcut `/api/templates` response'una alan genişletmesi yeterli. INSE 5dk cache'le okur.

**INSE'den ne gerekir?**
INSE flow engine her gönderim öncesi "bu template approved mı?" check eder. Approved değilse:
- Fallback template varsa onu kullan (utility kategorisi default fallback)
- Yoksa gönderimi bekletme kuyruğuna al + admin dashboard'da "Template X Meta reject, manuel aksiyon gerek" uyarısı

---

### J-WND — 24 Saat Window State'i Okuma

**Senaryo:**
Lead form doldurdu → INSE welcome HSM gönderdi → lead 3 saat sonra "Hi, what's the price?" diye cevap yazdı. Bu andan itibaren 24 saatlik window açık. INSE AI'ı serbestçe fiyat cevabı yazabilir (freeform). Ama 24 saat sonra INSE yine mesaj göndermek isterse (day-3 follow-up henüz erken, gün içinde fiyat sorusu gelse) window kapalı olabilir — o zaman freeform değil template zorunlu. INSE bu state'i bilmiyorsa yanlış kategori mesaj atar → Meta policy violation → numara ban riski.

**Neden?**
Meta kuralı: son gelen inbound mesaj zamanından +24h içinde freeform serbest. Sonra sadece template. Bu state INMA'da zaten var (inbound mesaj akışı INMA üzerinden geçer) ama INSE bunu okuyamıyor.

**Nasıl olmalı?**
Contact endpoint'ine bir alan: `last_inbound_at` (son müşteri mesajı zamanı, ISO 8601). INSE bunu her gönderim öncesi okur. `NOW() - last_inbound_at < 24h` → freeform serbest. Aksi takdirde INSE sadece approved template gönderir.

Alternatif: `GET /api/contacts/{phone}/window-status` → `{ windowOpen: true, expiresAt: "..." }`.

**INSE'den ne gerekir?**
INSE outbound helper'ı bu check'i her gönderim öncesi otomatik yapar. Window kapalıysa freeform → template fallback.

---

### (Opsiyonel, Pilot Sonrası) J4 — Toplu Gönderim Endpoint'i

**Senaryo:**
Pilot sonrası Dent 2000 kişilik bir liste üzerine tek seferde "indirim" kampanyası yapmak istiyor. Tek tek API çağırmak mantıksız — INMA'nın rate limiter'ı zaten gönderimi sıraya alacak.

**Neden P0 değil?**
Dent pilot volümü küçük (~200 lead, event-bazlı dağılır). INSE kendi tarafında Hangfire + throttle queue ile 2-3 dakikada halleder. Bulk endpoint 1000+ hedef için optimize.

**Pilot sonrası eklenir** — kickoff brief §11.

---

## INSE Tarafında Yapılacaklar (Referans — INMA'yı ilgilendirmez)

Sadece dağılım görünümü için; INSE tarafı sahibi Q:

| Paket | Durum |
|-------|-------|
| UP0.2 Welcome introspection (SSO) | ✅ DONE 2026-04-16 |
| UP0.3 Lazy tenant provisioning | ⏳ IN_PROGRESS (INMA-bağımsız) |
| UP0.5 `IInmaSendClient` (outbound wrapper) | ⏳ J1 bekliyor |
| UP0.5 `IInmaContactOptOutClient` (outbox + drain) | ⏳ PLAN hazır, J2 teslim olunca config flip |
| UP0.6 Feature flags | ✅ DONE 2026-04-13 |
| UP0.7 Inbound webhook (message.received, contact.updated) | PARTIAL |
| FEAT-WTP Welcome template pack (46 template) | DRAFT (6 spec içinde) |
| FEAT-MCC Multi-city campaign | DRAFT |
| FEAT-LIW Lead intake webhook | DRAFT |
| FEAT-VCP Video consultation | DRAFT |
| FEAT-EFS Event follow-up sequence | DRAFT |
| FEAT-TFM Tenant field mapping | DRAFT |

INSE her paketin kendi plan JSON'u var. INMA ekibi bunları bilmesine gerek yok.

---

## Go-Live Koordinasyon Takvimi

| Gün | Olay | Kim |
|-----|------|-----|
| T-14 | INMA J1 + J2 + J-HSM + J-WND ticket açıldı | INMA Backend |
| T-12 | J-HSM + J-WND field genişletmesi deploy (mevcut endpoint'lere) | INMA Backend |
| T-10 | J1 endpoint deploy test | INMA QA |
| T-8 | Template'ler Meta'ya submit + approved beklemeye başlar | Q + Dent |
| T-7 | J2 endpoint + migration deploy test | INMA QA |
| T-5 | J2 Angular rozet deploy | INMA Angular |
| T-3 | J1 + J2 + J-HSM + J-WND joint test (INSE staging) | Q + INMA QA |
| T-2 | INSE prod config flip (`NoOp → Http`) | Q |
| T-1 | Smoke test (Dent test tenant, business-initiated akış ilk mesaj) | Q |
| T0 | Pilot başlar | — |

**Risk absorpsiyonu:** INMA gecikirse pilot yine başlar — INSE NoOp modunda contact opt-out lokal bloke eder, outbox'ta birikir. INMA teslim ettiği anda drain olur. Pilot downtime sıfır.

---

## Sorular (INMA Ekibi)

1. **J1 efor onayı:** Mesaj gönderirken template render — INMA tarafında ne kadar iş? 1g mi, 2g mi?
2. **J2 efor onayı:** Data model + 3 endpoint + outbound filter + Angular — 2-3g gerçekçi mi?
3. **Transactional flag start-chat body'sinde zaten var mı, yoksa yeni mi?**
4. **Template `{{variable}}` syntax kabul edilir mi, yoksa Meta pattern (`{{1}}`, `{{2}}`) mi tercih?**
5. **Tenant yönetimi INMA'dan istenmiyor (lazy provisioning çözdü) — onay mı, itiraz mı?**

INSE tarafı sahibi: **Q (Taner)**.
Teknik detaylar:
- J2 INSE plan: `arch/plans/20260417-j2-opt-out-inse-sync.json`
- J2 INMA plan: `inma-plan-j2-opted-out.md`
- Kickoff brief: `inma-team-kickoff-brief.md` (§1–§4, §11 opsiyonel)
