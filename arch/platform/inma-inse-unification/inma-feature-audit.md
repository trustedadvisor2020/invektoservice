# INMA Feature Audit (Q, 2026-04-13)

**Lejant:** ✅ Var · ❌ Yok · 🟡 Kısmen

## A. Mesajlaşma Temeli
| # | Feature | Durum | Not |
|---|---------|-------|-----|
| 1 | Message templates (quick reply) | ✅ | Text + resim + dosya için ayrı library'ler tanımlanabiliyor |
| 2 | Template variable substitution (`{{name}}`) | ❌ | **Joint:** INMA render, INSE değişken sağlayacak |
| 3 | Template A/B rotation | ❌ | INSE yapacak |
| 4 | Bulk messaging / broadcast | ❌ | INSE yapacak |
| 5 | Opt-out keyword handler ("STOP") | ❌ | INSE detect → INMA flag |
| 6 | Auto-reply / office hours | 🟡 | Var ama saate bağlı değil — kullanıcı logout'sa çalışıyor |

## B. Zamanlama & Otomasyon
| # | Feature | Durum | Not |
|---|---------|-------|-----|
| 7 | Delayed send / scheduled message | ❌ | INSE yapacak |
| 8 | Recurring campaign (drip 3/7/14g) | ❌ | **ÖNEMLİ** — INSE yapacak |
| 9 | Visual flow builder | ❌ | INSE yapacak |
| 10 | Keyword-based routing | ❌ | INSE yapacak |

## C. Contact / Lead
| # | Feature | Durum | Not |
|---|---------|-------|-----|
| 11 | Contact CRUD + listing | ✅ | INMA sahibi |
| 12 | Contact deduplication | ✅ | |
| 13 | Tags / labels | ✅ | INSE bunu kullanacak |
| 14 | Pipeline stages | ❌ | INSE yapacak |
| 15 | Internal notes | ✅ | |
| 16 | Contact scoring | ❌ | INSE yapacak |

## D. AI / Intent
| # | Feature | Durum | Not |
|---|---------|-------|-----|
| 17 | Intent detection | ❌ | INSE zaten yapıyor |
| 18 | AI auto-reply / chatbot | ❌ | INSE yapacak |
| 19 | Sentiment analysis | ❌ | INSE yapacak |
| 20 | Suggested reply | ❌ | **Joint:** INSE üret, INMA göster |

## E. Dosya & Medya
| # | Feature | Durum | Not |
|---|---------|-------|-----|
| 21 | File/media upload | ✅ | |
| 22 | Persistent media storage | ✅ | INMA sahibi |
| 23 | Media library (template'e ekleme) | ❌ | INSE + INMA birlikte |

## F. Kullanıcı & Ekip
| # | Feature | Durum | Not |
|---|---------|-------|-----|
| 24 | User roles & permissions | ✅ | INMA sahibi — INSE SSO |
| 25 | Team inbox / assignment | ✅ | |
| 26 | Chat transfer | ✅ | |
| 27 | SLA / response time tracking | ❌ | INSE yapacak |

## G. Entegrasyon & API
| # | Feature | Durum | Not |
|---|---------|-------|-----|
| 28 | Inbound webhook (3rd party → INMA) | ❌ | **INSE karşılar** — landing form → INSE webhook → INMA API'ye contact create |
| 29 | Outbound webhook | ✅ | Per-tenant, ekrandan config |
| 30 | External API key management | ✅ | |
| 31 | REST API | ✅ | Swagger'da |

## H. Randevu / Takvim
| # | Feature | Durum | Not |
|---|---------|-------|-----|
| 32 | Appointment / slot booking | ❌ | INSE yapacak |
| 33 | Calendar view | ❌ | INSE yapacak |
| 34 | Meeting link generation | ❌ | INSE yapacak (Google Meet) |

## I. Dashboard & Rapor
| # | Feature | Durum | Not |
|---|---------|-------|-----|
| 35 | Conversation volume reports | ✅ | |
| 36 | Agent performance reports | ✅ | `/performance-report` endpoint |
| 37 | Conversion funnel analytics | ❌ | INSE yapacak |
| 38 | Custom dashboard widgets | ❌ | INSE yapacak |

## J. Altyapı
| # | Feature | Durum | Not |
|---|---------|-------|-----|
| 39 | Real-time WebSocket | ✅ | INSE'nin WS'i ile birleştirilmeli (Madde 11) |
| 40 | Audit log | 🟡 | Sadece login log var. INSE full audit inşa edecek |
| 41 | Multi-language UI | ✅ | INSE de i18n yapmalı, ortak key set |
| 42 | Notification center | ❌ | **YOK, eklemek önemli** — INSE yapacak + INMA UI entegrasyon |

## K. Bilinenler
- ✅ **10 tenant-based custom field**
- ✅ **Webhook outbound** (per-tenant config UI mevcut)
- ✅ **Channels:** WhatsApp + Instagram + Telegram (INMA API üzerinden canlı)

## Notifikasyon Merkezi Kararı
INMA'da yok. INSE kendi notification service'ini inşa eder:
- Backend: `notifications` tablosu (tenant_id, user_id, type, payload, read, created_at)
- SSE/WebSocket push
- INMA UI üst sağ'a **bell icon + dropdown** widget slot (J3 ile birlikte)
- Event tipleri: `flow.completed`, `offer.sent`, `offer.accepted`, `appointment.booked`, `xray.uploaded`, `sla.breach`, `nurture.exit`, `lead.handoff_required`

**Yeni Joint iş: J9 — Notification widget slot** (INMA UI header'a embed).
Efor: INSE M (3-4g) + INMA S (1g) = P1'e eklendi.
