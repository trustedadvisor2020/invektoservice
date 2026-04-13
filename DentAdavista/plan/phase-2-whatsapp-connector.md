# Faz 2 — Multi-Channel Connector (INMA: WA + IG + Telegram)

> **Güncelleme 2026-04-13:** Kanal seti genişledi — INMA API üzerinden **WhatsApp + Instagram + Telegram** (Email YOK). Adapter pattern zorunlu oldu (G2 → **A tam versiyonu**).
> **INMA endpoint haritası:** bkz. [decisions.md](decisions.md).

**Süre:** 1 gün | **Bağımlılık:** Faz 1

> **PIVOT (2026-04-13):** Müşteri WhatsApp mesajlarını **kendi Meta WABA'sı ile değil**, **INMA (wapcrm) platformu** üzerinden alacak. Dolayısıyla Invekto **Meta Cloud API'ye DEĞİL, INMA API'ye** bağlanır.
>
> - **Numara:** `+44 7547 762090` (INMA'da register edilmiş)
> - **Mimari:** INMA → Invekto (webhook ya da polling) → ChatAnalysis → Güneş response → INMA → WhatsApp
> - **Avantaj:** Meta template approval süreci bypass (INMA kendi hesabından gönderiyor)
> - **Dezavantaj:** INMA rate limit + uptime'a bağımlıyız, WA feature setinin tamamı erişilebilir olmayabilir (interactive list/button, media vb. kontrol edilmeli)

## Hedef
INMA ↔ Invekto çift yönlü mesaj akışı kur. Inbound mesaj → flow trigger, outbound mesaj → INMA API çağrısı.

## Adımlar

### 2.1 INMA API Discovery
- [ ] INMA Swagger incele: https://testapi.wapcrm.net/index.html
- [ ] Dokümantasyonu çıkar:
  - Auth modeli (API key / OAuth / session token)
  - Send message endpoint (text, media, interactive?)
  - Inbound webhook formatı — INMA bize mesajı nasıl push ediyor
  - Rate limit, error codes
  - InvektoCompanyCode eşleşmesi (memory kuralı: INMA READONLY lisans için — ama mesajlaşma için YAZMA endpoint'i kullanılacak, bu yeni bir durum. Doğrulanacak!)
- [ ] Müşterinin INMA tenant/hesap bilgisi al (API key)

### 2.2 Invekto Connector Modülü — `IMessageChannel` Abstraction
- [ ] `Invekto.Shared/Channels/IMessageChannel.cs` interface:
  ```csharp
  interface IMessageChannel {
    ChannelType Type { get; } // Wa | Instagram | Telegram
    Task<SendResult> SendTextAsync(string phoneOrHandle, string text);
    Task<SendResult> SendMediaAsync(string phoneOrHandle, MediaPayload media);
    Task<SendResult> SendInteractiveAsync(string phoneOrHandle, InteractivePayload payload);
  }
  ```
- [ ] `InmaAdapter` implementasyonu — tek adapter, 3 kanal (channel param ile routing)
  - INMA `getcompanychannels` ile WA/IG/Telegram channelId map'lenir
  - `start-chat` / `start-chat-v3` endpoint'leri wrap edilir
  - `upload-file` media için
- [ ] Channel registry: `IChannelRegistry` — tenant bazlı aktif kanallar
- [ ] Inbound: `POST /waa/inma/webhook/{tenantId}` → channel detect → messages tablosu → flow dispatch
  - **ALTERNATIF:** Swagger'da webhook görünmüyor, **polling** olabilir. Verify edilecek (mevcut `WapCrmClient` nasıl inbound alıyor → inceleme)
- [ ] Config: `appsettings.Production.json` tenant bloğu:
  ```json
  "Dentadavista": {
    "InmaApiBase": "https://testapi.wapcrm.net",
    "InmaSecretKey": "<X-CIB-SecretKey>",
    "InmaCompanyCode": "<id>",
    "Channels": {
      "WhatsApp": { "Enabled": true, "PhoneNumber": "447547762090" },
      "Instagram": { "Enabled": true, "Handle": "dentadavistaclinic" },
      "Telegram": { "Enabled": true, "Username": "<tba>" }
    }
  }
  ```

### 2.3 Mesaj Tipleri — Feature Parity Check
INMA'nın desteklediğini doğrula:
- [ ] Text message ✅ (zorunlu)
- [ ] Media (image/PDF) — X-ray upload ve teklif PDF için
- [ ] Interactive list/button — slot picker için (Faz 6). DESTEKLEMİYORSA fallback: "Reply 1 for Dublin, 2 for Cork"
- [ ] Template message — INMA kendi template onayı yapıyorsa süreç öğrenilmeli
- [ ] Typing indicator, read receipts — nice-to-have

### 2.4 Template Yönetimi
- **Meta yerine INMA katmanında:** INMA template mi istiyor yoksa serbest text mi?
  - **Freeform çalışıyorsa:** HSM onay süreci YOK, 24h session kuralı INMA tarafında absorb edilmiş olabilir
  - **Template gerekirse:** INMA dashboard üzerinden template submit
- [ ] Test: 1 mesaj gönder, yanıt al, feature'ları verify et

### 2.5 Inbound Flow Trigger
- [ ] Inbound WA message → `leads` tablosuna kayıt (phone, name, raw_text)
- [ ] `flow_engine` dispatch: welcome flow tetikle (Faz 5)
- [ ] Media attachment (X-ray image) → S3/file storage + lead'e attach

### 2.6 Rate Limit & Compliance
- [ ] INMA rate limit öğren (API response header'ları)
- [ ] Opt-out keyword handler ("STOP", "UNSUBSCRIBE") → lead `opted_out=true`
- [ ] GDPR log: her inbound/outbound mesaj `message_audit` tablosunda

## Deliverable
- INMA adapter canlı, `+44 7547 762090` numarasına test mesajı atılıyor
- Webhook'tan inbound mesaj Invekto'ya düşüyor
- Tenant config dokümante edilmiş

## Çıkış Kriteri
Test cihazından Dent Adavista WA numarasına mesaj → Invekto inbox → Güneş otomatik yanıt → müşteri cihazında görünüyor.

## Riskler
- **INMA Swagger eksikliği:** Dokümantasyon zayıfsa reverse-engineer gerekebilir — Faz 2 süresi 1g → 2g'ye çıkabilir
- **INMA feature sınırı:** Interactive message desteklemiyorsa Faz 6 slot picker UX'i fallback'e düşer
- **INMA uptime:** SLA'sı bilinmiyor — monitoring + retry kuyruğu şart
- **Memory kuralı "INMA READONLY":** Lisans için READONLY, mesajlaşma için YAZMA olacak. Bu AYRI endpoint'ler. Q onayı ile kural genişletilecek.
