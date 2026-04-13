# Gap Matrix — Kim Ne Yapacak

**Lejant:**
- 🟦 **INMA-only** — INMA'da var/yapılacak, INSE kullanır
- 🟩 **INSE-only** — INSE'de yapılacak, INMA bilmesin
- 🟨 **Joint** — İki tarafta koordineli iş
- ⚪ **Skip** — Pilot için gerekmez

## Mesajlaşma & Template

| Feature | Owner | Nasıl |
|---------|-------|-------|
| Template storage (text/image/file library) | 🟦 INMA | Mevcut, dokunma |
| Variable substitution `{{name}}` | 🟨 Joint | INSE context sağlar (REST), INMA render eder. Ortak `{{key}}` syntax kontratı |
| Template A/B rotation | 🟩 INSE | INSE seçer hangi template_id'yi kullanacak, INMA'ya send API'den o id'yi geçer |
| Bulk messaging / broadcast | 🟨 Joint | INSE orchestrator (hedef liste + schedule), INMA gönderir |
| Opt-out keyword handler | 🟨 Joint | INSE STOP detect → INMA contact flag update (yeni API gerekli) |
| Auto-reply / office hours | 🟨 Joint | INMA mevcut "logout auto-reply", INSE saat bazlı + AI auto-reply ekler |

## Zamanlama & Otomasyon

| Feature | Owner | Nasıl |
|---------|-------|-------|
| Delayed / scheduled send | 🟩 INSE | Hangfire (G7) + INMA send API çağrı |
| Drip campaign (3/7/14g) | 🟩 INSE | Flow builder node'u + scheduler |
| Visual flow builder | 🟩 INSE | Mevcut `FlowValidator.cs` + React UI |
| Keyword-based routing | 🟩 INSE | Intent detector içinde keyword rule'lar |

## Contact / Lead

| Feature | Owner | Nasıl |
|---------|-------|-------|
| Contact CRUD | 🟦 INMA | Mevcut |
| Dedup | 🟦 INMA | Mevcut |
| Tags | 🟦 INMA | Mevcut — INSE flow tag okur/yazar |
| Notes | 🟦 INMA | Mevcut |
| **10 custom fields** | 🟦 INMA | Mevcut — Dent'in 10 field'ı buraya map |
| Pipeline stages | 🟩 INSE | Yeni tablo, contact FK INMA'ya |
| Contact scoring | 🟩 INSE | AI-driven, INSE'de hesaplanır, özet INMA'ya custom field olarak geri yazılabilir |

## AI

| Feature | Owner | Nasıl |
|---------|-------|-------|
| Intent detection | 🟩 INSE | Mevcut (Claude Haiku) |
| AI auto-reply / chatbot | 🟩 INSE | Intent → template → send |
| Sentiment | 🟩 INSE | Flow enrichment |
| Suggested reply (agent UI) | 🟨 Joint | INSE generate (API endpoint), INMA sohbet ekranında chip/button göster |

## Dosya

| Feature | Owner | Nasıl |
|---------|-------|-------|
| Media upload | 🟦 INMA | Mevcut |
| Media storage | 🟦 INMA | Mevcut — INSE upload için INMA API kullanır |
| Template media library | 🟨 Joint | INMA'nın library'si + INSE template ref |

## Kullanıcı

| Feature | Owner | Nasıl |
|---------|-------|-------|
| Users & roles | 🟦 INMA | Mevcut — INSE SSO, ayrı user tablosu YOK |
| Team assignment | 🟦 INMA | Mevcut — INSE flow "handoff" action'ı INMA API çağırır |
| Chat transfer | 🟦 INMA | Mevcut |
| SLA tracking | 🟩 INSE | Metrikler INSE, UI widget'ı INMA'da |

## Entegrasyon

| Feature | Owner | Nasıl |
|---------|-------|-------|
| Inbound webhook (3rd party → landing form) | 🟩 INSE | INSE `POST /api/inbound/form/{tenantId}` endpoint, contact INMA'ya push |
| Outbound webhook (INMA → INSE) | 🟦 INMA | ✅ Mevcut, Dent için URL set edilecek |
| API key mgmt | 🟦 INMA | Mevcut |
| REST API | 🟦 INMA | Mevcut |

## Randevu

| Feature | Owner | Nasıl |
|---------|-------|-------|
| Appointment / slot booking | 🟩 INSE | Yeni tablo + WA interactive list message |
| Calendar view | 🟩 INSE | INMA sidebar'dan link, iframe/embed |
| Meeting link (Google Meet) | 🟩 INSE | `GoogleMeetService` |

## Rapor

| Feature | Owner | Nasıl |
|---------|-------|-------|
| Volume / agent reports | 🟦 INMA | Mevcut |
| Conversion funnel | 🟩 INSE | Flow execution log'dan türetme |
| Custom dashboard | 🟩 INSE | INMA iframe |

## Altyapı

| Feature | Owner | Nasıl |
|---------|-------|-------|
| WebSocket real-time | 🟨 Joint | V1: ayrı WS'ler. V2 (Madde 11): tek bağlantı (P2) |
| Audit log | 🟩 INSE | Full audit INSE'de inşa edilir. INMA login log mevcut, INSE'ye stream'lenir |
| Multi-lang UI | 🟨 Joint | Ortak i18n key convention + paralel çeviri |
| Notification center | 🟨 Joint | **INSE inşa eder** (tablo + WS + API + React widget). INMA UI header'a bell widget slot açar (J9) |

## Özet Sayım

- 🟦 INMA-only: **12 feature** (mevcut, değişmez)
- 🟩 INSE-only: **18 feature**
- 🟨 Joint: **8 feature** (en kritik — koordinasyon)
- ❓ Belirsiz: **1** (notification center)

## Kritik Joint İşler (INMA Ekibiyle Konuşulacak)

| # | Konu | INMA Tarafı İsteği |
|---|------|--------------------|
| J1 | Template variable render | `send` API'ye `variables: {name, city}` param + template içinde `{{key}}` syntax render |
| J2 | Opt-out flag API | Contact'ta `opted_out bool` field + set/unset endpoint |
| J3 | Suggested reply UI | Sohbet ekranında INSE API'den öneri çekecek widget slot |
| J4 | Bulk send endpoint | Batch send API (list of phones + template_id + variables) |
| J5 | SSO JWT kabul | INSE'ye JWT validation için public key paylaşımı |
| J6 | Template media library sharing | INSE'den media id referansıyla template oluşturma izni |
| J7 | WebSocket unification (v2) | Ortak WS gateway tasarımı |
| J8 | Full audit hook | Kritik aksiyonlarda INMA INSE'ye event yayımlaması |
| J9 | Notification bell widget slot | INMA header'da INSE notification widget için DOM slot + iframe/Web Component embed |
