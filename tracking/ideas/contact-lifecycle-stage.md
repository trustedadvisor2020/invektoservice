<!-- Status: IDEA | 2026-04-24 -->
# Contact Lifecycle Stage — Otomatik İlerleyen Yaşam Döngüsü

> **Tarih:** 2026-04-24
> **Kaynak:** Q interview (sohbet bazlı etiket sisteminin üstüne lifecycle ekleme)
> **Durum:** FIKIR — tablo + geçiş matrisi onaylandı, plan JSON yok
> **Sektör:** TÜM

---

## Problem

Invekto şu anda **sohbet bazlı çoklu etiket** kullanıyor (tag = çoklu, manuel, konu sınıflandırması). Ama hiçbir yerde **"bu kişi satış yolculuğunun neresinde?"** sorusunun tek-değerli, sıralı, otomatik ilerleyen cevabı yok. Ajan her sohbette lead'in durumunu elle çıkarmak zorunda; raporlama yapılamıyor, segment bazlı kampanya kurulamıyor.

## Çözüm — Tag + Lifecycle Birlikte

İki sistem farklı soruları cevaplıyor, karışma riski düşük:

- **Tag** = "bu sohbet neyle ilgili" (çoklu, manuel, konu/ürün/sorun) — mevcut sistem, değişmiyor.
- **Lifecycle stage** = "bu kişi hangi aşamada" (tekil, sıralı, **otomatik**) — yeni.

Çift giriş yükünü önlemek için lifecycle **otomatik** (event-driven), tag **manuel** kalır.

---

## Lifecycle Stage Listesi

| # | Stage | Anlam |
|---|-------|-------|
| 1 | `new` | Lead oluştu, henüz hiç outbound mesaj gönderilmedi |
| 2 | `contacted` | Bizden ilk mesaj gitti (welcome flow veya ajan), müşteri henüz cevaplamadı |
| 3 | `engaged` | Müşteri en az bir cevap yazdı, diyalog akıyor |
| 4 | `opportunity` | Satın alma niyeti sinyali var (fiyat, teklif, demo vb.) |
| 5 | `customer` | Satış kapandı |
| 6 | `dormant` | Uzun süre sessizlik (terminal değil, geri dönebilir) |
| 7 | `lost` | Açıkça kaybedildi (red, kapsamsız, alternatif tedarikçi) |

## Geçiş Matrisi

| From → To | Event | Kaynak (mevcut/planlı kod) | Koşul | Mod |
|-----------|-------|---------------------------|-------|-----|
| — → `new` | Lead created | `MetaLeadgenIntakeJob` / WhatsApp inbound webhook | Yeni lead kaydı oluştu | Auto |
| `new` → `contacted` | First outbound delivered | `TriggerWelcomeFlowJob` / ChatService outbound | Bizden ilk mesaj teslim edildi | Auto |
| `contacted` → `engaged` | Customer inbound | Inbound webhook | Müşteriden ≥1 mesaj alındı | Auto |
| `engaged` → `opportunity` | Intent detected | ChatAnalysis (intent classifier) | Intent ∈ {price, quote, buy, demo} **veya** belirli manuel tag | Auto + manuel |
| `opportunity` → `customer` | Deal won | `ZohoLifecycleDispatcher` | Zoho Deal.Stage = Won | Auto |
| `engaged`/`opportunity` → `lost` | Lost signaled | Manuel ajan / Zoho | Ajan "kaybedildi" **veya** Zoho Deal.Lost | Manuel + auto |
| Any non-terminal → `dormant` | Idle timeout | Cron | Son müşteri mesajı > N gün (default 14), stage ∉ {customer, lost} | Auto |
| `dormant` → `engaged` | Reactivation | Inbound webhook | Müşteri yeniden yazdı | Auto |
| Any → Any | Manual override | Agent UI | Ajan zorla değiştirir, **sebep alanı zorunlu** | Manuel |

## Kurallar

- **İleri-akış varsayılan:** `new → contacted → engaged → opportunity → customer`. Atlamalar (örn. `new → customer`) izinli ama yalnızca manuel + sebep ile.
- **Geri-akış sınırlı:** Sadece `dormant → engaged` otomatik geri gider. Diğer geri dönüşler manuel.
- **Her geçiş `stage_history`'ye yazılır:** `{from, to, event_type, event_id, source_service, reason, actor (system|agent_id), at}` — denetim için.
- **Terminal durumlar:** `customer` ve `lost` terminal. `dormant` terminal DEĞİL (dönebilir).

---

## Açık Sorular (Plan JSON öncesi Q kararı gerekli)

1. **Scope:** Lifecycle **kişi/lead bazlı mı, sohbet bazlı mı?** Öneri: kişi bazlı (aynı müşterinin ikinci sohbeti "new" dönmez). Mevcut tag sohbet bazlı olduğu için iki farklı granülerlik olur, UI'da net ayrım gerek.
2. **Dormant eşiği:** 14 gün default mı, tenant başına config mi? Sektöre göre farklı olabilir (diş hekimi 6 ay, e-ticaret 30 gün).
3. **`new` vs `contacted`:** Welcome flow çoğu tenant'ta saniyeler içinde tetiklenir — `new` gerçekten ayrı stage olarak dursun mu, yoksa doğrudan `contacted` ile başlansın mı?
4. **Intent → opportunity eşleşmesi:** ChatAnalysis'in hangi intent çıktıları otomatik opportunity'ye taşısın? Liste sabit mi, tenant config mi?
5. **Mevcut kayıtlar:** Lifecycle tablosu devreye alınınca backfill yapılacak mı (örn. son 30 gün mesajlaşan = engaged)? `rollout` skill kullanılabilir.

---

## Mimari Etkisi (Taslak)

- **Yeni tablo:** `contact_lifecycle` (`tenant_id`, `contact_id`, `current_stage`, `updated_at`) + `contact_lifecycle_history`.
- **Yeni servis sorumluluğu:** `LifecycleBackendClient` (zaten ekleniyor — b-meta paketi) genişletilir, event evaluator eklenir.
- **Event kaynakları:** MetaLeadgenIntakeJob, TriggerWelcomeFlowJob, ChatAnalysis intent result, ZohoLifecycleDispatcher, cron (dormant timer).
- **UI:** Sohbet detay panelinde stage rozeti + geçmiş; raporlamada stage bazlı segmentasyon.

## Bağlantılı İşler

- **B-META** (Meta Leadgen Webhook — devam ediyor) — lead create event'i lifecycle'ı tetikleyecek.
- **EFS Drip Sequence** — dormant stage dormant drip tetikleyebilir.
- **ZohoLifecycleDispatcher** — mevcut dispatcher customer geçişini tetikler.

## Sonraki Adımlar

- [ ] Q: 5 açık sorunun cevapları
- [ ] Plan JSON yazımı (`arch/plans/YYYYMMDD-feat-contact-lifecycle.json`)
- [ ] Migration taslağı (`arch/db/migrations/`)
- [ ] Rollout planı (mevcut 11 tenant için backfill stratejisi)
