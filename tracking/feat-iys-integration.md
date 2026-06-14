# FEAT-IYS-INTEGRATION — Tracking

> **Slug:** `feat-iys-integration` (plan JSON henüz yok) | **Risk:** HIGH (regülasyon + multi-tenant yetki + PII + 3-iş-günü yasal SLA)
> **Spec:** (yazılacak — `arch/features/iys-integration.md`)
> **Status:** **DRAFT-RESEARCH** — internet araştırması + resmî alan doğrulaması tamam (2026-06-13). **Build BLOCKED** → Faz 0 statü cevabı (İYS A.Ş.) gelmeden kod yok.
> **Tercih:** Q → **direkt bağlanma** (Invekto = yetkili AHS/Entegratör). Aracı entegratör = fallback.

## Özet

Invekto'nun kendi tenant'ları (her biri ayrı **Hizmet Sağlayıcı / marka**) adına İYS'de ticari ileti izinlerini **ekleme / ret / sorgulama + gönderim öncesi kontrol** yapması. İYS = 6563 sayılı Kanun kapsamı: ticari elektronik ileti (ARAMA / MESAJ / EPOSTA) izin yönetimi. WhatsApp ticari mesaj pratikte **MESAJ** izni altında değerlendiriliyor (resmî kanal değil — teyit gerekli).

## ⚠️ #1 KARAR NOKTASI — Statü (build'den ÖNCE)

İYS'de iki ayrı statü var; Invekto'nun hangisine düştüğü **maliyeti + mimariyi + süreyi** belirler:

| Statü | Tanım | Invekto için |
|-------|-------|--------------|
| **AHS (Aracı Hizmet Sağlayıcı)** | Başkası adına işlem yapan iletim aracısı (daha hafif) | API sözleşmesi + API satın alma yeterli olabilir |
| **İYS İş Ortağı / Entegratör** | 3. taraflara İYS-entegrasyonu-as-a-service satan; **yetkilendirme Tebliğine tabi** | Teknik/idari yeterlilik + KVKK/güvenlik dokümantasyonu + muhtemel denetim |

> **Invekto 35+ tenant'a ürün özelliği olarak sunacağı için büyük ihtimalle Entegratör yetkilendirmesi kategorisine düşer** — ama bu İYS A.Ş.'ye **yazılı sorulup teyit edilmeli**. Overclaim yok: araştırmada (dev.iys.org.tr bot-koruması fetch reddetti) net rakam/şart doğrulanamadı.

## Faz Yol Haritası (direkt bağlanma)

| Faz | Kapsam | Çıktı | Blocker |
|-----|--------|-------|---------|
| **0** | İYS A.Ş. statü sorusu (`kurumsalhizmetler@iys.org.tr` + `ahs@iys.org.tr`) | AHS mi / Entegratör yetkisi mi netleşir + belge listesi | — (İLK İŞ) |
| **1** | Başvuru + İYS API sözleşmesi + API satın alma + sandbox erişimi | Test ortamı credential'ları | Faz 0 |
| **2** | Tenant onboarding modeli: her tenant kendi HS kaydını yapar + Invekto'yu AHS olarak yetkilendirir | Panel "İYS Ayarları" ekranı | Faz 1 |
| **3** | Teknik entegrasyon: OAuth2 client_credentials + izin ekleme/sorgulama metotları + 3-iş-günü job | `InvektoIys` servis/modül + DB + cxapi gate | Faz 2 |
| **4** | Mikroservis + DB + gönderim öncesi gate (cxapi/MessageSender) | Multi-tenant izin katmanı | Faz 3 |
| **5** | Sandbox 2-tenant smoke → canlı onay → MVP rollout | Canlı | Faz 4 |

## MVP Sıralaması (Q tercihi: önce sorgu)

1. **MVP = SADECE SORGULAMA** — gönderim öncesi filtre + kişi kartında durum + sorgu logu.
2. Sonraki faz: izin ekleme + ret + unsubscribe + toplu yükleme + İYS→Invekto senkronizasyon.
3. **Kural:** gönderim yapılıyorsa sorgu sonucu **loglanmadan** geçilmez (denetim kanıtı).

## Doğrulanmış Resmî İYS API Alan Referansı

> Kaynak: dev.iys.org.tr metot dokümanı + Telsam çoklu izin ekleme (2026-06-13 doğrulama).

| Alan | Değerler / Format |
|------|-------------------|
| `type` (kanal) | `ARAMA` \| `MESAJ` \| `EPOSTA` (sadece 3 kanal; WhatsApp kanalı YOK) |
| `status` | `ONAY` \| `RET` |
| `recipient` | telefon (≤15 karakter) veya e-posta (≤265 karakter) |
| `recipientType` | `BIREYSEL` \| `TACIR` |
| `consentDate` | `YYYY-MM-DD HH:mm:ss` (TR saati) |
| `source` (enum) | `HS_WEB`, `HS_CAGRI_MERKEZI`, `HS_FIZIKSEL_ORTAM`, `HS_ISLAK_IMZA`, `HS_SOSYAL_MEDYA`, `HS_MESAJ`, `HS_MOBIL`, `HS_EORTAM`, `HS_ETKINLIK`, `HS_ATM`, `HS_2015`, `HS_KARAR` (serbest metin DEĞİL) |
| Auth | OAuth2 `client_credentials` → access token → `Authorization: Bearer` |
| Base URL | Resmî: `api.iys.org.tr` (kesin path'ler sözleşme sonrası API dokümanından) |

> **Karıştırma uyarısı:** `api.iyspanel.com` / `hermesiletisim` = özel entegratör (Hermes), resmî İYS DEĞİL.

## Multi-Tenant Mimari (mikroservis izolasyonu)

- Yeni servis önerisi: **`InvektoIys`** (veya mevcut Integrations :7106 altında modül) — Shared üzerinden DTO, doğrudan servis-servis referans YOK.
- İzin **marka bazlı** (`iysCode` / `brandCode`) → her tenant ayrı marka paketi öder (maliyet çarpanı tenant'a yansır).
- Gönderim öncesi gate: cxapi / `MessageSenderService` akışına "izin yoksa/RET ise gönderme + sonucu logla".
- 3-iş-günü kuralı: İYS dışında alınan onay **ve** ret bildirimleri 3 iş günü içinde İYS'ye işlenmeli → Hangfire scheduled job ile garanti.

## DB Tabloları (snake_case, tenant_id scoped)

| Tablo | Amaç | Ana kolonlar |
|-------|------|--------------|
| `iys_integrations` | tenant ↔ marka ↔ token | `tenant_id`, `iys_code`, `brand_code`, `channels`, `status`, `access_token_ref`, timestamps |
| `iys_consents` | kişi bazlı izin durumu | `tenant_id`, `contact_id`, `recipient`, `channel`, `recipient_type`, `status`, `source`, `consent_date`, `iys_ref_id`, `evidence_ref`, `last_checked_at` |
| `iys_events` | audit log | `tenant_id`, `action` (ADD/RET/CHECK/SYNC), `request_payload`, `response_payload`, `success`, `error_code`, `created_at` |

## Error Code Namespace (öneri)

`INV-IYS-001+` — `arch/errors.md`'ye eklenecek (Codex audit öncesi namespace netleştir). Örnek aday durumlar: auth fail, marka yetkisiz, kanal geçersiz, 3-gün SLA aşımı, İYS upstream hata, recipient format hata.

## Açık Sorular (Q + İYS A.Ş.)

| # | Soru | Etki |
|---|------|------|
| 1 | **AHS mi / Entegratör yetkilendirmesi mi gerekir?** | Maliyet + mimari + süre (en kritik) |
| 2 | WhatsApp ticari mesaj resmî olarak MESAJ izni altında mı? | Kanal eşleme + uyum riski |
| 3 | Güncel marka paketi bedelleri (İleti5/25/... 2026) + reseller/toplu fiyat var mı? | 35-tenant maliyet projeksiyonu |
| 4 | Direkt mi yoksa aracı entegratör (Verimor/İletiMerkezi/VatanSMS) fallback mı? | Q tercihi direkt; statü cevabına bağlı |
| 5 | MVP sadece sorgu mu, yoksa ekleme+ret de ilk fazda mı? | Scope (Q eğilimi: önce sorgu) |

## Bağımlılıklar

| # | Bağımlılık | Status |
|---|-----------|--------|
| 1 | İYS A.Ş. statü + belge cevabı | PENDING (Faz 0 maili) |
| 2 | İYS API sözleşmesi + API satın alma | PENDING (Faz 1) |
| 3 | Her tenant'ın İYS HS kaydı + Invekto'yu AHS yetkilendirmesi | PENDING (Faz 2, tenant-bazlı) |
| 4 | KVKK/güvenlik dokümantasyonu (Invekto veri işleme) | PENDING |
| 5 | Sabit IP (API allowlist) + webhook/callback adresi | mevcut altyapı audit gerek |

## ⚠️ İYS ≠ KVKK

İYS = ticari ileti **gönderme** izni. KVKK = veri **işleme** açık rızası. Biri diğerinin yerine geçmez; ayrı tutulur.

## Sonraki Adım

**Faz 0 maili at** (statü sorusu). Cevap gelmeden plan JSON / kod yok — çünkü AHS-vs-Entegratör cevabı hem maliyeti hem mimariyi belirliyor. Cevap sonrası: spec yaz (`arch/features/iys-integration.md`) → interview → plan JSON.

## Kaynaklar

- [İYS Geliştirici Merkezi](https://dev.iys.org.tr/) · [Tekil İzin Ekleme](https://dev.iys.org.tr/api-metotlar/izin-yonetimi/tekil-izin-ekleme/) · [API Başvuru Formu](https://iys.org.tr/api-basvuru-formu)
- [Kurumsal Hizmetler](https://iys.org.tr/hizmet-saglayici/kurumsal-hizmetler) · [İş Ortaklığı](https://iys.org.tr/is-ortaklari)
- [Entegratörlük Yetkilendirme Tebliği yorumu (Yüksel Attorneys)](https://medium.com/y%C3%BCksel-attorneys-at-law/i%CC%87ys-entegrat%C3%B6rl%C3%BC%C4%9F%C3%BC-i%CC%87%C5%9F-ortakl%C4%B1%C4%9F%C4%B1-i%C3%A7in-yetkilendirme-%C5%9Fart%C4%B1-getiren-tebli%C4%9F-neler-i%CC%87%C3%A7eriyor-6aed989f47e5)
- [Çoklu İzin Ekleme alanları (Telsam)](https://telsam.com.tr/bilgi-bankasi/iys-coklu-izin-ekleme/) · [WhatsApp/ticari ileti kapsam (Mysoft)](https://iletiyonetimi.com/iysde-izinsiz-gonderilebilen-iletiler-ve-yasal-duzenlemeler)
