# Phase 4 — Enterprise Altyapı: SSO + Audit + Güvenlik

> **Hafta:** 25-32
> **MRR Hedefi:** 800K-1.2M TL
> **Müşteri Hedefi:** 130+
> **Bağımlılık:** Phase 3 tamamlanmış olmalı
> **Durum:** ⬜ Başlamadı

---

## Durum Takibi

| Alt Gereksinim | Durum | Tamamlanma Tarihi | Notlar |
|----------------|-------|-------------------|--------|
| GR-4.1 SSO / 2FA Genişletme | ⬜ Başlamadı | — | — |
| GR-4.2 Audit Service | ⬜ Başlamadı | — | — |
| GR-4.3 PII Koruma | ⬜ Başlamadı | — | — |
| GR-4.4 Guardrails | ⬜ Başlamadı | — | — |
| GR-4.5 Admin Panel | ⬜ Başlamadı | — | — |
| GR-4.6 QA & Mining Hazırlık | ⬜ Başlamadı | — | — |
| GR-4.7 Sağlık Niche Enterprise | ⬜ Başlamadı | — | — |

---

## Özet

Kurumsal müşteriler kapıyı çalıyor. "Demo güzel ama SSO yok, audit yok — procurement veto" deniyor. İhtiyaç kanıtlanmış, yatırım yapılabilir.

**Satış dili:** "Bankalar bile onaylıyor: SSO, 2FA, audit log, KVKK uyumlu"

**Yeni Mikro Servis:**

| Servis | Port | Sorumluluk |
|--------|------|------------|
| `Invekto.Audit` | 7103 | Append-only event store, işlem logları |

---

## Gereksinimler

### GR-4.1: SSO / 2FA Genişletme

> **Servis:** Ana Uygulama (.NET) — ayrı mikroservis DEĞİL
> **Not:** Temel auth (email/şifre, JWT, multi-tenant, role-based) zaten mevcut. Bu adım enterprise seviyeye yükseltiyor.

- [ ] **4.1.1** Google OIDC (SSO) desteği ekle
- [ ] **4.1.2** Microsoft OIDC (SSO) + Azure tenant id
- [ ] **4.1.3** TOTP 2FA (QR + 10 backup code)
- [ ] **4.1.4** 2FA enforcement policy (tenant bazlı)
- [ ] **4.1.5** Session management genişletme
  - Device list, revoke, timeout policy
- [ ] **4.1.6** Failed login log + brute-force protection (5 deneme → 15dk kilit)
- [ ] **4.1.7** IP allowlist (CIDR destekli)
- [ ] **4.1.8** Country allowlist (GeoIP)
- [ ] **4.1.9** Tenant bazlı policy engine
- [ ] **4.1.10** DB (SQL Server — ana app):
  ```sql
  -- Mevcut users tablosuna SSO/2FA kolonları eklenir:
  -- sso_provider, sso_subject, totp_secret, backup_codes → ALTER

  sessions (id, user_id, device_id, ip, country, user_agent, expires_at, created_at)
  login_attempts (id, email, ip, device_info, reason, created_at)
  tenant_policies (id, tenant_id, session_timeout_min, max_failed_attempts, ip_allowlist_json, country_allowlist_json, sso_required, tfa_required, created_at, updated_at)
  ```

---

### GR-4.2: Audit Service

> **Servis:** `Invekto.Audit` (port 7103) — YENİ

- [ ] **4.2.1** Audit servis iskeletini oluştur (port 7103, health check)
- [ ] **4.2.2** Append-only event store
- [ ] **4.2.3** Event schema tanımla:
  - event_type, actor_id, tenant_id, resource_type, resource_id
  - ip, user_agent, correlation_id
  - payload_before_json, payload_after_json
- [ ] **4.2.4** Kritik event coverage:
  - [ ] Login/logout
  - [ ] Permission change
  - [ ] Data export
  - [ ] Conversation view/delete
  - [ ] Knowledge update/delete
  - [ ] AI prompt change
  - [ ] Configuration change
- [ ] **4.2.5** Search API (tarih/actor/resource/event_type filtre)
- [ ] **4.2.6** Audit UI (Supervisor erişimli)
- [ ] **4.2.7** Retention policy per tenant + scheduled purge job
- [ ] **4.2.8** DB (PostgreSQL):
  ```sql
  audit_events (id, tenant_id, event_type, actor_id, resource_type, resource_id, ip, user_agent, correlation_id, payload_before_json, payload_after_json, created_at)
  retention_policies (id, tenant_id, resource_type, retain_days, legal_hold, created_at, updated_at)
  ```

---

### GR-4.3: PII Koruma

> **Servis:** `Audit` + `AgentAI` entegrasyonu

- [ ] **4.3.1** PII detector (TC, telefon, email, IBAN, adres)
- [ ] **4.3.2** Mesaj görüntülemede maskeleme
- [ ] **4.3.3** Temsilci outgoing mesajında PII uyarısı (TC/IBAN tespit → uyar)
- [ ] **4.3.4** Export'da PII redaction toggle
- [ ] **4.3.5** Legal hold (açıkken purge çalışmaz)

---

### GR-4.4: Guardrails (AgentAI'a eklenir)

> **Servis:** `AgentAI` genişleme

- [ ] **4.4.1** Banned phrases / sensitive content (tenant-level kurallar)
- [ ] **4.4.2** PII prevention on outgoing (TC/IBAN tespit → blokla veya onay)
- [ ] **4.4.3** AI aksiyonlarını audit'e logla
- [ ] **4.4.4** Escalation auto-notes (devredilince AI özet)

---

### GR-4.5: Admin Panel

> **Servis:** Dashboard genişleme

- [ ] **4.5.1** Tenant yönetimi (kullanıcı davet, rol atama)
- [ ] **4.5.2** Security policy paneli (SSO/2FA/IP/timeout ayarları)
- [ ] **4.5.3** Audit log viewer (filtre + export)
- [ ] **4.5.4** PII redaction ayarları

---

### GR-4.6: QA & Mining Derinleştirme (Phase 6 prep)

> **Servis:** `Audit` + Dashboard
> **Not:** Tam QA Scoring ve Conversation Mining Phase 6'da. Temel metadata logging Phase 2'de başlatıldı (GR-2.5).
> Bu adım daha derin veri toplama ve script compliance ekliyor.
>
> **v4.2 (2026-02-15):** Basit metadata log ve FRT Phase 2 Dashboard'una taşındı (GR-2.5.9/10).
> Phase 4'te kalan: script compliance, agent performans derinleştirme, haftalık rapor.

- [ ] **4.6.1** Conversation metadata log **genişletme** (Phase 2'deki basit log'a ek: escalation chain, multi-turn count, knowledge hits)
- [ ] **4.6.2** Basit script compliance check (banned phrases tetiklenme raporu)
- [ ] **4.6.3** Agent bazlı ortalama yanıt süresi + çözüm oranı (Phase 2 FRT'nin üstüne detay)
- [ ] **4.6.4** "Top unanswered intents" haftalık rapor (Knowledge gap tespiti)

---

### GR-4.7: Sağlık Niche — Enterprise Özellikler

> **Servis:** `Audit` + `AgentAI` + Dashboard
> **Not:** Randevu motoru, no-show önleme, intent seti Phase 2'de zaten kuruldu. Bu adım enterprise derinleştirme.

- [ ] **4.7.1** SLA tracking (klinik bazlı yanıt süresi hedefleri)
- [ ] **4.7.2** Audit log (hasta verileri erişim geçmişi — KVKK uyumu)
- [ ] **4.7.3** Advanced analytics (tedavi bazlı dönüşüm, doktor performansı)
- [ ] **4.7.4** PII koruma (TC kimlik, sağlık verisi maskeleme)
- [ ] **4.7.5** Multi-şube yönetimi (merkezi dashboard + şube bazlı izolasyon)

**Yapılmayacak:**
- ❌ Tedavi sonrası takip otomasyonu (Phase 5)
- ❌ Google yorum motoru (Phase 5)
- ❌ HBYS entegrasyonu (çok erken)

---

## Çıkış Kriterleri (Phase 5'e Geçiş Şartı)

- [ ] En az 1 kurumsal müşteri SSO ile bağlandı
- [ ] Audit log çalışıyor, Supervisor erişiyor
- [ ] PII maskeleme aktif
- [ ] Kurumsal satış pipeline'ı açıldı
- [ ] Diş: 10+ klinik aktif, no-show %10 altında
- [ ] Diş: fiyat→randevu dönüşüm oranı %35+
- [ ] Estetik: 10+ klinik aktif, lead→randevu dönüşüm %35+
- [ ] Estetik: yabancı hasta oranı ölçülüyor
