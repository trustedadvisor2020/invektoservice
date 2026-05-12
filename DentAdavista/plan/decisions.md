# Kararlar

> **Ilk karar seti:** 2026-04-13 Q-onayli. Kapsam pilot-bagimsiz platform feature'lari + Dent pilot konfigurasyonu ikiye ayrildi: 2026-04-16 refactor (B paketi).

## Ana Kararlar (kilitli)

| # | Konu | Karar | Sonuc |
|---|------|-------|-------|
| 1 | Custom Fields | **INMA'nin mevcut 10 tenant custom field'ini kullan** | G4 iptal; generic semantic overlay feature'a donusturuldu ([`arch/features/tenant-field-mapping.md`](../../arch/features/tenant-field-mapping.md)) |
| 2 | Flow Wait State | `flow_execution_state` persistent tablosu | G6 DONE 2026-04-13 |
| 3 | Scheduler | Hangfire migration | G7 Faz 1-5 DONE 2026-04-13/14 |
| 4 | Multi-channel | INMA uzerinden WA + IG + Telegram (Email YOK) | INMA adapter pattern |
| 5 | INMA Send | Swagger dogrulandi, endpoint'ler hazir (tenant 5050 canli) | Outbound WA write YASAK DEGIL (mesajlasma); INMA lisans okumasi hala READONLY |
| 6 | Unified Platform | INMA-INSE tek native platform (SSO + tenant sync + shared data + feature flags) | UP0 paketleri 2026-04-13'ten baslayarak devam |
| 7 | Feature-Pilot ayrimi | 2026-04-16 B paketi: 6 generic feature spec ([`arch/features/`](../../arch/features/)) + Dent pilot checklist | Pilot tenant-bagimsiz feature'lari tuketecek; "Gunes" pilot kod adi |

## INMA Endpoint Haritasi

| Ihtiyac | INMA Endpoint |
|---------|--------------|
| Outbound mesaj baslat | `POST /api/chatsv3/start-chat` (ChatV3VM) |
| Outbound v3 alternatif | `POST /api/chatsv3/start-chat-v3` (ChartStartControlVM) |
| Kanal listesi (WA/IG/Telegram) | `GET /api/chatsv3/getcompanychannels` |
| Telefondan chat bul | `GET /api/chatsv3/find-chat-by-phonenumber/{id}` |
| Media upload | `POST /api/chats/upload-file` |
| Image retrieve | `GET /api/chats/getimage` |
| Block/unblock | `POST /api/chatsv3/block-phone-number` |
| Performance raporu | `GET /api/chats/performance-report` |

**Auth:** `X-CIB-SecretKey` (mevcut `WapCrmClient.cs` pattern).

**Acik soru:** Inbound webhook? Swagger'da webhook endpoint yok — INMA polling mi, ayri dokumantasyon mi? Mevcut `WapCrmClient` inbound akisi verify edilecek.

## Unified Platform Kararlari (UP0/UP1/UP2)

| Faz | Item | Durum |
|-----|------|-------|
| UP0.1 | INMA DTO contract reorg (Shared) | DONE 2026-04-13 |
| UP0.1b | DTO consolidation (webhook types) | DONE 2026-04-15 |
| UP0.2 | SSO dogrulama + role map | **BLOCKED** — INMA JWT public key bekliyor (asymmetric RS256 verify) |
| UP0.3 | Tenant lifecycle handler (event-driven) | BLOCKED — INMA `tenant.created` event bekliyor |
| UP0.4 | Bidirectional sync (shared bus) | PENDING |
| UP0.5 | `IInmaSendClient` outbound | PARTIAL — J1/J4 bekliyor |
| UP0.6 | Feature flag service | DONE 2026-04-13 |
| UP1 | Embedded UI / feature surfacing | BACKLOG |
| UP2 | Audit/notification / unified WebSocket | BACKLOG |

**Detay:** [`arch/platform/inma-inse-unification/`](../../arch/platform/inma-inse-unification/)

## ~~Blueprint Sync (Zoho)~~ → V2 Customer Status (INMA-otorite, 2026-05-12)

> **🔄 V2 Mimari Pivot (2026-05-12, FEAT-INMA-PIPELINE-V2 C1 Zoho-out, commit `0c0733b`):**
> Eski "Blueprint Sync (Zoho)" karari kaldirildi. Zoho INSE'den TAMAMEN cikti — lead CRUD + stage sync + Blueprint + OAuth + zoho_* tablolar HEPSI silindi (Migration 048). LeadStatusEventMap class'i + ZohoLifecycleDispatcher silindi.
>
> **V2 Karar:**
> - **Pipeline status alani:** `leads.pipeline_status` VARCHAR(30) — **INSE'de**, INMA'da degil (degismedi, INSE-native).
> - **customer_status alani:** YENI — INSE'de opaque TEXT olarak saklanir, INMA-otorite (INMA agent UI dropdown manuel set), INSE validate ETMEZ.
> - **Sync yonu:** ONE-WAY INMA→INSE webhook (`POST /api/v1/inbound/inma/customer-status-change` C2 BLOCKED INMA contract). Eski INSE→Zoho push iptal.
> - **Loop prevention:** INSE customer_status'u INMA'ya forward ETMEZ; INMA dropdown sahipligi.
> - **Field mapping vs pipeline:** Farkli kavramlar. `pipeline_status` = INSE-native CRM lifecycle; `customer_status` = INMA-otorite (V2 yeni); field mapping (INMA custom_1..10) = tenant domain vocabulary.

## Gap Fix Durumu

| Gap | Durum | Detay |
|-----|-------|-------|
| G3 (Template rotation) | DONE 2026-04-14 | `ITemplateRotationService` Shared'da |
| G6 (Flow state persistence) | DONE 2026-04-13 | `flow_execution_state` tablosu |
| G7 (Hangfire) | DONE 2026-04-13/14 | Faz 1-5 + deploy HEALTHY |
| G4 (Custom fields) | IPTAL | INMA 10 field kullanilacak (feature spec ile overlay) |
| G2 (Multi-channel) | PENDING | INMA adapter, pilot oncesi |
| G8 (EN locale test) | PENDING | Feature spec AC-4 icinde |

## Paralel Yapilabilir

- 6 generic feature spec'in implement'i bagimsiz paketler — birbirine zincirli degil
- Pilot checklist ilerleyisi, feature implement'i tamamlandigi kadar aktivite kazaninr
- UP0.2/0.3/0.5 INMA-ekibi bagimlili — Dent pilot critical path
