# Dent Adavista Pilot — Stage 1 Hazırlık Dokümanı

Hazırlık tarihi: 2026-04-24
Pilot tenant: 18173130 (Dent Adavista)
Kapsam: Meta Lead Ads → Invekto → WhatsApp welcome + Zoho stage ilerletme
Zapier/Make.com KULLANILMIYOR — Meta webhook direkt Invekto'ya gelecek.

Bu doküman 4 tarafa iş veriyor:
A) Invekto tarafı (biz — kod micro-paket + Dashboard config)
B) Müşteri Meta Business Manager tarafı
C) Müşteri Zoho CRM Console tarafı
D) Müşteri WhatsApp Business numarası tarafı (INMA allowlist)

Her adım numaralı. Her UI adımında tıklanacak yer `[ ... ]` ile belirtildi.


## BÖLÜM A — Invekto Kod Micro-Paketi (biz yapıyoruz)

Zapier olmayacağı için Meta Lead Ads webhook'unu Invekto içinde native karşılayan bir endpoint + verify + payload parse eklememiz gerekiyor. Bu kodun SCOPE'u küçük ama kaçınılmaz.

### A.1 — Yeni endpoint: POST /api/inbound/meta/leadgen/{tenant_slug}

Dosya: src/Invekto.Backend/Program.cs (veya ayrı MetaLeadgenEndpoints.cs)

İçerik:
1. GET /api/inbound/meta/leadgen/{tenant_slug} — Meta webhook verify handshake. `hub.mode=subscribe` + `hub.verify_token` check → `hub.challenge` döner. Verify token tenant-bazlı (tenant_settings.meta_leadgen_verify_token, her tenant rotate eder).
2. POST /api/inbound/meta/leadgen/{tenant_slug} — Meta leadgen event geldiğinde:
   - X-Hub-Signature-256 header ile payload signature validate (Meta App Secret ile HMAC-SHA256)
   - Payload'dan leadgen_id + form_id + page_id çek
   - Meta Graph API call: GET /{leadgen_id}?access_token=<page_access_token>&fields=field_data,created_time → form cevapları gelir
   - field_data'dan name/phone/email/city_preference gibi alanları tenant field_map ile canonical şekle çevir
   - LeadIntakeService.ProcessIntakeAsync(tenantId, source_slug="meta-leadgen", payload) çağır
   - 200 OK (Meta 10 saniye içinde 200 bekler, gecikme olursa Meta retry eder — job async hop yapılmalı, Hangfire enqueue + hemen 200 dön)

Error codes (yeni):
- INV-META-001 → signature invalid
- INV-META-002 → verify token mismatch
- INV-META-003 → Graph API lead fetch fail
- INV-META-004 → field_data parse fail
- INV-META-005 → tenant_slug unknown
- INV-META-006 → Meta access token missing/expired

### A.2 — Tenant config: meta_leadgen_settings tablosu (veya tenant_settings JSONB key)

Kolonlar (tenant_settings.meta_leadgen_config JSONB):
- verify_token (string, random 32-char — tenant rotate eder)
- app_secret (string, Meta App → Settings → Basic → App Secret)
- page_access_token (string, Meta Graph API Explorer'dan veya long-lived token)
- page_id (string, Dent Adavista Facebook Page ID)
- field_id_map (object, Meta form question_id → canonical field adı). Örn:
  ```
  {
    "FULL_NAME": "name",
    "PHONE": "phone",
    "EMAIL": "email",
    "custom_q_1234": "city_preference"
  }
  ```

Migration: 033-meta-leadgen-config.sql

### A.3 — ZohoLifecycleDispatcher welcome_sent hook

Şu an LeadStatusEventMap.cs:13-22 welcome_sent event'ini map etmiyor. Müşteri "1. mesaj atıldı" Zoho'da görmek istiyorsa welcome flow completion point'inden dispatcher'a call eklenmeli.

Scope:
1. ZohoLifecycleDispatcher → yeni metot DispatchEvent(tenantId, leadId, zohoEvent)
2. TriggerWelcomeFlowJob.ExecuteAsync → terminal status success olduktan sonra dispatcher.DispatchEvent(tenantId, leadId, "welcome_sent") fire-and-forget
3. AllowedEvents whitelist zaten mevcut (7 event), yeni whitelist entry GEREKMİYOR
4. Test: prod'da Dent'te test lead → welcome flow success → zoho_sync_log yeni row welcome_sent success + Zoho Console Leads history'de transition görünür

### A.4 — Dashboard /settings/meta-leadgen ekranı

İçerik:
1. Webhook URL kutusu (salt-okunur): `https://app.invekto.com/api/inbound/meta/leadgen/dent-adavista`
2. Verify Token kutusu + [Rotate] butonu (random 32-char uret)
3. App Secret kutusu (password field)
4. Page Access Token kutusu (password field, long-lived preferred)
5. Page ID kutusu
6. Field ID Mapping tablosu (Meta form question_id ↔ canonical):
   - [+ Yeni Satır] butonu
   - Satır başı: question_id (metin) + canonical (dropdown: name, phone, email, custom_1..5, consent_marketing)
7. [Keşfet: Form Fields] butonu — Meta Graph API call `/page/leadgen_forms?access_token=...` → form listesi dropdown → seçilen form'un field listesi otomatik doldurulur
8. [Test Webhook] butonu — son 5 leadgen event history + status (200/401/500) + error code
9. [Kaydet] butonu

### A.5 — Paket tahmini süre: ~4-6 saat

- A.1 endpoint (1.5h)
- A.2 config tablosu + migration (0.5h)
- A.3 welcome_sent hook (0.5h)
- A.4 Dashboard sayfası (1.5h)
- Unit test + /rev Codex PASS + deploy (1h)

Plan JSON: arch/plans/20260425-feat-meta-leadgen-webhook.json (hazırlanacak)


## BÖLÜM B — Müşteri Meta Business Manager Hazırlığı

Müşteri (Dent Adavista ops) aşağıdaki adımları kendi Meta Business Manager hesabında yapacak. İlk kez yapıyorlarsa ~2 saat, tecrübeliyse 30dk.

### B.1 — Meta App oluştur

1. https://developers.facebook.com/apps/ → [Create App]
2. App Type: `Business`
3. App Name: `Dent Adavista Invekto Bridge`
4. Business Manager seç: `Dent Adavista Dental Clinic`
5. Create

### B.2 — App ayarları

1. Sol menü: [Settings] → [Basic]
2. Not al: `App ID` + `App Secret` (App Secret'ı Invekto'ya vereceğiz)
3. [Add Platform] → Website → URL: `https://app.invekto.com`
4. App Domains: `invekto.com`
5. Privacy Policy URL, Terms URL doldur
6. Kaydet

### B.3 — Leadgen webhook ekle

1. Sol menü: [Add Products] → "Webhooks" → Set Up
2. [Add Subscription] → Object: `Page`
3. Callback URL: `https://app.invekto.com/api/inbound/meta/leadgen/dent-adavista`
4. Verify Token: Invekto Dashboard'dan [Rotate] ile aldıkları 32-char token (Bölüm A.4 adım 2)
5. [Verify and Save] tıkla — Invekto 200 + challenge return ile doğrular
6. Subscription fields: sadece `leadgen` tikle
7. [Subscribe] tıkla

### B.4 — Page access token al

1. https://developers.facebook.com/tools/explorer/
2. App Dropdown → `Dent Adavista Invekto Bridge` seç
3. User Token → [Get User Access Token]
4. Permissions: `pages_show_list`, `pages_read_engagement`, `leads_retrieval`, `pages_manage_metadata`
5. [Generate Access Token] → Facebook login + onay
6. Dropdown → Page Access Tokens → Dent Adavista page seç → page token görünür
7. Bu token kısa ömürlü — long-lived token'a çevir:
   - https://developers.facebook.com/tools/debug/accesstoken/ → tokenı yapıştır → [Debug]
   - Expiration: "Never" görünürse zaten long-lived. Değilse:
   - Graph API Explorer → `GET /oauth/access_token?grant_type=fb_exchange_token&client_id={APP_ID}&client_secret={APP_SECRET}&fb_exchange_token={short_token}` → long-lived token
8. Long-lived token'ı Invekto Dashboard [Page Access Token] kutusuna yapıştır (Bölüm A.4 adım 4)

### B.5 — Page ID'yi bul

1. Dent Adavista Facebook sayfası → [About] → altta "Page ID" görünür (sayı)
2. Bu sayıyı Invekto Dashboard [Page ID] kutusuna yapıştır (Bölüm A.4 adım 5)

### B.6 — Lead Form oluştur (veya var olanı bağla)

1. Facebook Business Suite → [All Tools] → [Instant Forms] → [Create Form]
2. Form adı: `Roadshow Ireland 2026 Signup`
3. Intro: "Free 1-to-1 Dental Roadshow with Dr. Özge — Dublin 14 March / Cork 15 March"
4. Questions (sırayla ekle):
   - Full Name (short answer)
   - Phone Number (phone, required)
   - Email (email, required)
   - Which city? (multiple choice: Dublin / Cork / Both)
   - Have you had previous dental treatment abroad? (yes/no)
5. Privacy Policy URL: Dent Adavista'nın privacy policy sayfası
6. Thank you screen: "Thanks! Our team will message you on WhatsApp shortly."
7. [Publish]
8. Form yayınlandıktan sonra "Form Settings" → Question ID'leri not al (Invekto field mapping için gerekli)

### B.7 — Invekto field mapping gir

1. Invekto Dashboard → [Settings] → [Meta Leadgen]
2. [Keşfet: Form Fields] tıkla → Roadshow Ireland 2026 Signup formunu seç
3. Otomatik dolan mapping tablosunda her satırı doğrula:
   - FULL_NAME → name
   - PHONE → phone
   - EMAIL → email
   - custom_q_<id> (city) → custom_1 (city_preference)
   - custom_q_<id> (previous) → custom_2
4. [Kaydet]

### B.8 — HSM Welcome Template submit (En Kritik, 24-48h onay bekler)

1. Meta Business Manager → [WhatsApp Manager] → [Message Templates] → [Create Template]
2. Category: `Utility` (MARKETING değil — Utility approval daha hızlı + daha ucuz)
3. Language: English (US)
4. Template Name: `dent_welcome_roadshow_v1`
5. Header: (boş)
6. Body:
   ```
   Hi {{1}} 😊 How are you?
   I'm Güneş from Dent Adavista Dental Clinic (Kuşadası). We're hosting a free 1-to-1 dental Roadshow with our founder dentist Dr. Özge Yılmazoğlu — Dublin (14 March) and Cork (15 March).
   Would you like me to save you a free spot? Dublin or Cork?
   ```
7. Footer: `Reply STOP to opt out`
8. Buttons: (Quick Reply) "Dublin" + "Cork"
9. Sample values: {{1}} = "Sarah"
10. [Submit]
11. Pilot için SADECE v1 yeterli. Stage 3'te v2-v10 eklenir (rotation pool için).

FAQ template'leri pilot için zorunlu DEĞİL — ai_faq node semantic search ile 24h window içinde free-form cevap veriyor (template gerekmez).


## BÖLÜM C — Müşteri Zoho CRM Hazırlığı

Müşteri Zoho Admin'i (veya biz onunla screenshare) aşağıdakileri yapar. ~45dk.

### C.1 — Leads modülü için Blueprint oluştur

1. Zoho CRM → [Setup] (sağ üst dişli)
2. [Automation] → [Blueprint]
3. [Create Blueprint] → Module: `Leads`
4. Field: `Lead Status` seç
5. Blueprint Name: `Dent Roadshow Pipeline`
6. Description: "Invekto AI agent lifecycle stages"
7. [Next]

### C.2 — State + Transition ekle

State list (soldan sağa pipeline):
- New (start) → Contacted → Engaged → Qualified → Offer Sent → Deposit Paid → Won
- (branch) Lost

Transition list (aşağıdaki 7 transition MUTLAKA olmalı, isim serbest ama aşağıdaki mapping'te Invekto event ile eşleşecek):

| Transition Adı (Zoho) | From | To | Invekto Event |
|---|---|---|---|
| 1. Mesaj Atıldı | New | Contacted | welcome_sent |
| Konuşma Başladı | Contacted | Engaged | engaged |
| Nitelikli Aday | Engaged | Qualified | qualified |
| Teklif Gönderildi | Qualified | Offer Sent | offer_sent |
| Kapora Alındı | Offer Sent | Deposit Paid | deposit_paid |
| Tedavi Tamamlandı | Deposit Paid | Won | closed_won |
| İptal | * (any) | Lost | closed_lost |

Her transition için:
1. Blueprint tasarlama alanında [+ Transition]
2. Name: yukarıdaki tablo
3. Before Transition: (boş — Invekto otomatik tetikleyecek)
4. During Transition: (opsiyonel — örn. offer_sent'te Notes zorunlu yapılabilir)
5. After Transition: (opsiyonel — örn. deposit_paid'e geçince owner'a notification)
6. Save

### C.3 — Blueprint'i aktive et

1. Tüm state + transition tanımlandıktan sonra üst sağda [Save & Publish]
2. Status: `Active` görünmeli

### C.4 — Zoho API Console'dan scope ayarla

1. https://api-console.zoho.com/ → Self Client (veya Server-based Application)
2. Client ID + Client Secret not al (Invekto'ya vereceğiz)
3. Scopes (INMA sonrası Invekto'ya verilir): `ZohoCRM.modules.Leads.ALL`, `ZohoCRM.settings.ALL`, `ZohoCRM.users.READ`
4. Redirect URL: `https://app.invekto.com/api/integrations/zoho/oauth/callback`

### C.5 — Invekto Zoho bağlantısını kur

1. Invekto Dashboard → [Settings] → [Entegrasyonlar] → [Zoho CRM]
2. [Bağlan] butonu → Zoho OAuth popup → onay → Invekto'ya token kaydedilir
3. [Bağlantıyı Test Et] butonu → 200 OK + org info görünmeli

### C.6 — Stage Mapping'i Invekto'ya taşı

1. Invekto Dashboard → [Settings] → [Zoho Stage Mapping]
2. [Discover Transitions] butonu → Zoho'daki 7 transition listelenir
3. 7 satır için dropdown'dan transition seç:
   - welcome_sent → "1. Mesaj Atıldı"
   - engaged → "Konuşma Başladı"
   - qualified → "Nitelikli Aday"
   - offer_sent → "Teklif Gönderildi"
   - deposit_paid → "Kapora Alındı"
   - closed_won → "Tedavi Tamamlandı"
   - closed_lost → "İptal"
4. [Kaydet]
5. [Dry Run Test] butonu → Invekto bir test lead için transition deneme çalıştırır (Zoho'da gerçek kayıt oluşmaz, sadece API validate)


## BÖLÜM D — Müşteri WhatsApp Business & INMA Hazırlığı

WhatsApp Business numarası INMA (WaClient.Management) üzerinden outbound gönderiyor. Bu yüzden INMA tarafı config'i de gerekli.

### D.1 — WhatsApp Business numarası

Müşteri sağlar:
- WABA (WhatsApp Business Account) ID
- Phone Number ID (Meta Business Manager → WhatsApp Manager → Phone Numbers)
- Display Name (Dent Adavista)
- Profile picture + description

Eğer müşterinin henüz WABA numarası yoksa:
1. Meta Business Manager → [WhatsApp Accounts] → [Add WhatsApp Account]
2. BSP (Business Solution Provider) seçmez — Cloud API için direct
3. Phone number verify (SMS veya voice call)
4. Display name onay bekler (~24h)

### D.2 — INMA (WaClient.Management) allowlist

Invekto → INMA bridge (`/api/v1/callback/wapcrm`) Dent Adavista'nın WhatsApp numarasını allowlist'e almalı.

Biz (Invekto ops) yapacak:
1. INMA Admin panel → Dent Adavista firma kaydı
2. InvektoCompanyCode = 18173130 (Invekto tenant_id eşleşmesi)
3. WABA phone_number_id gir
4. X-CIB-SecretKey oluştur → Invekto tenant_settings.inma_secret_key kolonuna yaz
5. Test: Invekto'dan outbound test template → INMA'dan WA'ya → müşterinin test numarasına ulaşıyor mu?


## BÖLÜM E — Invekto Dashboard Konfigürasyon (biz yapıyoruz)

Tüm yukarıdaki config'ler hazır olduktan sonra Invekto Dashboard'da 7 sayfayı sırayla tik atarak verify. Her sayfa ~2-5dk.

### E.1 — Tenant ayarları

1. Dashboard → [Settings] → [Tenant]
2. Display Name: `Dent Adavista Dental Clinic`
3. Locale Default: `en-US`
4. Locale Fallback: `tr-TR`
5. Timezone Primary: `Europe/Dublin` (operasyon)
6. Timezone Clinic: `Europe/Istanbul`
7. Feature Flags (toggle):
   - ai_agent_enabled: ON
   - flow_builder: ON
   - multi_city_campaign: ON
   - followup_sequence: ON
   - appointments: ON (plan tier = kurumsal olmalı, zaten Paket A'da upgrade edildi)
8. Branding → Logo upload + primary renk + secondary renk
9. Agent Display Name: `Gunes`
10. Agent Signature: `Gunes - Dent Adavista Dental Clinic - Kusadasi`
11. [Kaydet]

### E.2 — Lead Intake (LIW) ayarları

1. Dashboard → [Settings] → [Lead Intake]
2. Source Slug: `meta-leadgen` (yeni endpoint için; eski `roadshow-landing` zaten var)
3. API Key: [Rotate] → kopyala (Meta'ya vermeyeceğiz, sadece Invekto internal kullanım — Meta webhook signature ile validate ediyor, api_key bu path'te kullanılmaz; landing path'i için kalmaya devam)
4. Field Map (canonical → source):
   - name → name
   - phone → phone
   - email → email
   - custom_1 → city_preference
   - custom_2 → country_code
   - consent_marketing → consent_marketing
5. Phone Country Hint: `IE`
6. Consent required: ON
7. [Kaydet]

### E.3 — Meta Leadgen ayarları (YENI — Bölüm A.4 endpoint'i geldikten sonra)

1. Dashboard → [Settings] → [Meta Leadgen]
2. Bölüm B.2, B.4, B.5 adımlarındaki değerleri gir:
   - App Secret
   - Page Access Token (long-lived)
   - Page ID
3. [Rotate] ile verify token oluştur + müşteriye kopyala (Bölüm B.3 adım 4 için)
4. [Keşfet: Form Fields] → form seç → mapping otomatik dolsun
5. [Kaydet]
6. [Test Webhook] → son 5 event history

### E.4 — Field Mapping (TFM)

1. Dashboard → [Settings] → [Field Mapping]
2. 5 field aktif:
   - roadshow_city → cf1 (text)
   - appointment_slot → cf2 (date)
   - offer_status → cf3 (enum: none, preparing, sent, accepted, declined, on_hold)
   - deposit_status → cf4 (enum: pending, paid, refunded)
   - flight_booked → cf5 (bool)
3. 5 reserve slot cf6-cf10 boş kalsın
4. [Kaydet]

### E.5 — Campaign Config (MCC)

1. Dashboard → [Settings] → [Campaigns]
2. Zaten seed: `roadshow_ireland_2026` — sadece verify
3. Active window: 2026-03-01 → 2026-03-20 (Stage 1 test için tarihleri bugünden sonraya revize etmek gerekebilir — Paket 11 FlowBuilder wiring'te 2026-05-20 → 2026-06-15'e çekildi, Stage 1'i buna göre ayarla)
4. Cities + Dates verify
5. [Kaydet] (değişiklik varsa)

### E.6 — Followup Sequence (EFS)

1. Dashboard → [Settings] → [Followup Sequence]
2. Test Mode: ON (pilot Stage 1 için delay_days dk'ya dönüşür)
3. Enabled: ON
4. Threshold: 3 days no reply
5. Stages:
   - Day 3 → template_slug: `post_event_day3_en`, group: `followup_soft`
   - Day 7 → template_slug: `post_event_day7_en`, group: `followup_nudge`
   - Day 14 → template_slug: `post_event_day14_en`, group: `followup_final`
6. Triggers (tümünü tikle):
   - welcome_chain_no_reply
   - offer_declined
   - offer_timeout
   - offer_on_hold_7d
7. A/B Control %: 50
8. [Kaydet]
9. [Test Mode Banner] Dashboard üstünde görünmeli

### E.7 — Flow Builder wiring (welcome flow verify)

1. Dashboard → [Flows] → `dent_welcome_roadshow` (flow_id=29) → [Edit]
2. Welcome node:
   - data.group_tag: `welcome_with_date`
   - data.message_text_welcome_1: Paket C1'de DocX content'e bind edildi — verify
3. ai_faq nodes (12 intent için) — Stage 1'de sadece verify, rotation Stage 3'te B-C2 paketinde aktivate
4. [Validate Flow] → 0 error
5. [Kaydet]

### E.8 — Zoho Stage Mapping (Bölüm C.6'da yapıldı, burada sadece tik)

1. Dashboard → [Settings] → [Zoho Stage Mapping]
2. 7 satır görünüyor + transition ID dolu mu kontrol
3. [Dry Run All] → 7 transition test PASS


## BÖLÜM F — Stage 1 Smoke Test (2 gün, 5 test numarası)

Stage 1 launch gününde:

### F.1 — Gün 1 — End-to-end happy path

Test numaraları:
- +90 5XX XXX XX 01 (Q kendi — Taner)
- +90 5XX XXX XX 02 (Dent Adavista ops-1)
- +90 5XX XXX XX 03 (Dent Adavista ops-2)
- +44 XX XXXX XX 04 (UK test — IE region için)
- +44 XX XXXX XX 05 (UK test-2)

Test senaryoları (her numara için):

Senaryo 1 — Meta Form'dan intake:
1. Test numarasını sahibi Facebook'a login
2. "Roadshow Ireland 2026 Signup" formunu aç (müşteri paid ad üzerinden göndersin veya direct URL)
3. Formu doldur + submit
4. Beklenen (Invekto tarafı):
   - Invekto webhook 200 OK (Meta dashboard webhook history)
   - Invekto Backend log: [INV-META-000] leadgen received + [INV-AT-069] welcome flow triggered
   - Dashboard [Leads] sayfasında yeni satır
   - WhatsApp'a welcome HSM template ulaştı (delivered tick)
   - Zoho Console → Leads → yeni kayıt + status "Contacted" (welcome_sent transition executed)
   - Invekto [Zoho Sync Log] sayfasında welcome_sent success

Senaryo 2 — Reply "Dublin":
1. Test numarasından WhatsApp'a "Dublin" yaz
2. Beklenen:
   - FlowEngineV2 welcome node → city detection → rotation_group city=dublin
   - Bir sonraki mesaj: Dublin için slot list (interactive list)
   - custom_1 = "dublin" DB'de güncellendi
   - pipeline_status → `contacted` → Zoho engaged transition
   - Invekto [Zoho Sync Log] engaged success

Senaryo 3 — FAQ sor:
1. Test numarasından "Is it really free?" yaz
2. Beklenen:
   - ai_faq node semantic search top hit score >0.6 (is_it_free intent)
   - Response: variant A/B/C'den biri (Paket C1 36 FAQ pool)
   - chat_sessions tablosunda yeni row

Senaryo 4 — Appointment booking:
1. Test numarasından slot list'ten birini seç
2. Beklenen:
   - appointments INSERT
   - VideoMeetingCreationJob Hangfire → Succeeded (~500ms)
   - meeting_link non-null (mock provider)
   - reminder jobs (24h + 1h before) scheduled
   - pipeline_status → `consultation` → Zoho offer_sent transition

Senaryo 5 — Followup sequence (test_mode=ON dk bazlı):
1. Test numarasından welcome mesajına REPLY GÖNDERME
2. 3 dakika bekle (test_mode delay_days → dk çevirme)
3. Beklenen:
   - FollowupStageJob queue=marketing-followup pickup
   - stage[0] Succeeded ~16s
   - Day 3 template gönderildi
   - event_followup_runs tabloda success row

Senaryo 6 — Opt-out:
1. Test numarasından "STOP" yaz
2. Beklenen:
   - inma_optout_outbox event_type=optout_stop
   - outbound_messages status=blocked (bundan sonraki tüm outbound)
   - Invekto Dashboard [Leads] satırında opt-out icon

### F.2 — Gün 2 — Edge case + monitoring

1. Geçersiz telefon formatı (ör. "abc") → Meta form'dan submit → INV-BE-105 reject verify
2. Consent false → LIW reject verify
3. Aynı telefonla 2 kez submit → dup merge (tek lead, 2 audit log entry)
4. Zoho token expire simulate → Invekto retry worker verify + sync_log failed/retry pattern
5. INMA bridge 500 simulate → outbound retry worker verify
6. Monitoring dashboard:
   - WAA webhook error count: 0
   - Flow stuck lead: 0
   - LLM cost: <$5/gün
   - Intent confidence avg: >0.7

### F.3 — Go/No-Go kriterleri

Stage 2'ye (3 gün, ilk 20 gerçek lead) geçiş için:
- 5/5 test numarası için 6/6 senaryo PASS
- 0 blocker bug
- Zoho sync log success rate ≥ %95
- Müşteri sign-off email (ekran görüntüsü + onay)

Başarısızsa:
- Her failure için INV-xxx error code + root cause
- Müşteri mutabık + fix paketi açılır
- Fix'ten sonra tekrar Stage 1 smoke (tam döngü)


## BÖLÜM G — Sıralı İş Akışı (timeline)

Gün 0 (bugün 2026-04-24):
- Biz: Bölüm A kod micro-paketi /auto başlat (Meta Leadgen endpoint + welcome_sent hook)
- Müşteri: Bölüm B.1-B.7 Meta App + Lead Form kurulum başlasın
- Müşteri: Bölüm B.8 HSM template v1 submit (24-48h onay bekler)
- Müşteri: Bölüm C.1-C.5 Zoho Blueprint setup

Gün 1 (2026-04-25):
- Biz: Bölüm A deploy + Codex PASS
- Biz: Bölüm E Dashboard config (7 sayfa)
- Müşteri: HSM onay bekleniyor
- Müşteri: Bölüm C.6 Invekto Zoho stage mapping

Gün 2 (2026-04-26):
- HSM template approved (umut)
- Biz: Bölüm F.1 Stage 1 smoke başlat
- 5 test numarası + 6 senaryo

Gün 3 (2026-04-27):
- Biz: Bölüm F.2 edge case + monitoring
- Stage 1 sign-off
- Stage 2 (ilk 20 gerçek lead) başlar

Gün 4-6 (2026-04-28 → 2026-04-30):
- Stage 2: 20 lead manuel inceleme + coordinator dashboard
- Ops ekibi her saat review

Gün 7+ (2026-05-01):
- Stage 3 full trafik
- Paralel: B0 (Google Meet real OAuth) + B-C2 (welcome rotation activation) paketleri


## KONTROL LİSTESİ (müşteriye gönderilebilir özet)

Müşteri yapacaklar:
- [ ] Meta App oluştur + App Secret kopyala (Bölüm B.1-B.2)
- [ ] Leadgen webhook subscribe (Bölüm B.3)
- [ ] Page Access Token + Page ID al (Bölüm B.4-B.5)
- [ ] Lead Form yayınla (Bölüm B.6)
- [ ] HSM welcome template v1 submit (Bölüm B.8) — KRİTİK, 48h onay
- [ ] Zoho Blueprint oluştur + 7 transition (Bölüm C.1-C.3)
- [ ] Zoho API Client ID + Secret + scope (Bölüm C.4)
- [ ] WABA phone_number_id + display name (Bölüm D.1)
- [ ] 5 test numarası Invekto'ya ver (Bölüm F.1)

Biz (Invekto) yapacaklar:
- [ ] Meta Leadgen endpoint kod paketi (Bölüm A.1-A.4, ~5h)
- [ ] welcome_sent Zoho dispatch hook (Bölüm A.3)
- [ ] INMA allowlist Dent WABA (Bölüm D.2)
- [ ] Dashboard 7 config sayfası tik (Bölüm E.1-E.8)
- [ ] Stage 1 smoke 5 numara × 6 senaryo (Bölüm F.1)
- [ ] Edge case + monitoring (Bölüm F.2)
- [ ] Stage 2 + 3 koordinasyon


## NOTLAR

1. Bu doküman `pilot-checklist.md` dosyasının tamamlayıcısı. Checklist canonical kurulum maddelerini, bu doküman Stage 1 launch için tıkla-tıkla akışı gösteriyor.
2. Zapier olmayacağı için Meta Leadgen endpoint micro-paketi KRİTİK — bu kod olmadan Meta form → Invekto bağlantısı kurulamaz.
3. HSM template onayı 24-48h alıyor — müşteri ilk gün submit etmezse pilot ertelenir. Gün 0'da submit zorunlu.
4. Zoho Blueprint yoksa `welcome_sent` dispatch INV-INT-121 ile log'a düşer + sync_log failed (terminal). Müşteri Blueprint'i yayınlamadan test başlanamaz.
5. Google Meet real OAuth (B0 paketi) Stage 3 öncesi. Stage 1-2'de mock provider kullanılıyor (meeting_link fake URL ama flow ve reminder job'lar gerçek).
6. Welcome rotation (B-C2) Stage 3'te. Stage 1'de tek varyant kullanılıyor (DocX with-date variant 1).
