# Faz 3 — AI Agent "Güneş" Konfigürasyonu

**Süre:** 1.5 gün | **Bağımlılık:** Faz 1

## Hedef
10 karşılama varyantı + 12 FAQ × 3 cevap varyantı = **46 şablon** Invekto'ya yüklenecek, intent detector EN diline göre eğitilecek, varyant rotation mantığı çalışacak.

## Adımlar

### 3.1 Persona Tanımı
- [ ] Agent name: **Güneş**
- [ ] Tone: warm, professional, low-pressure, emoji-friendly
- [ ] Signature: `Güneş · Dent Adavista Dental Clinic · Kuşadası 🇹🇷`
- [ ] Fallback human handoff threshold: intent confidence < 0.6 → coordinator'a escalate

### 3.2 Karşılama Şablonları (10 varyant, 2 grup)

**Grup A — Tarihli (7 varyant):** ilk mesaj, Dublin/Cork tarih içeriyor
**Grup B — Tarihsiz (5 varyant):** registration sonrası follow-up

- [ ] `welcome_v1` … `welcome_v10` template'leri oluştur
- [ ] Her biri için variable slot: `{{name}}`, `{{city_options}}`
- [ ] A/B rotation: lead'in `welcome_variant_seed = hash(phone) % 10` ile deterministik seçim

### 3.3 FAQ Intent Map (12 intent)

| Intent | Keyword trigger (EN) | Cevap sayısı |
|--------|---------------------|--------------|
| `is_it_free` | free, cost, pay, charge | 3 varyant |
| `location_where` | where, address, hotel | 3 |
| `what_happens` | what happens, agenda | 3 |
| `any_treatment` | treatment there, procedure | 3 |
| `payment_after` | obligation, commit, after | 3 |
| `bring_xray` | xray, x-ray, scan | 3 |
| `bring_companion` | friend, family, bring | 3 |
| `duration` | how long, time, minutes | 3 |
| `why_ireland` | why ireland, why here | 3 |
| `price_quote` | price, how much, cost of | 3 + fallback (liste linki) |
| `safety_concern` | safe, trust, legit | 3 + social links |
| `hotel_transfers` | hotel, transfer, flight | 3 |

- [ ] Intent training data: docx'ten çıkarılan soru örnekleri + 5-10 paraphrase per intent
- [ ] `MockIntentDetector` → `OpenAiIntentDetector` geçişi (production'da LLM-based)
- [ ] Her intent için 3 cevap varyantı → round-robin rotation (lead bazında)

### 3.4 Özel Intent Davranışları
- [ ] `price_quote` → 3. tetiklemede otomatik fiyat listesi linki + "dentist call-back?" teklif
- [ ] `safety_concern` → otomatik inject: website + Instagram son 3 before/after + FB + telefon
- [ ] `location_where` → şehir seçilmeden önce "Dublin mı Cork mu?" sor; seçildikten sonra spesifik hotel
- [ ] Bilinmeyen intent → "Let me connect you with our coordinator" → human handoff

### 3.5 Dil Detection
- [ ] Inbound mesaj Türkçe gelirse → TR template fallback (az da olsa ihtimal)
- [ ] Diğer diller → EN fallback + "I'll respond in English, hope that's okay 🙂"

### 3.6 Translation Layer (son commit: "translation AI detect")
- [ ] Müşteri TR yazarsa Güneş EN template'i otomatik TR'ye çevirip gönder
- [ ] Outgoing EN, incoming normalize EN (AI detection)

## Deliverable
- 10 welcome + 36 FAQ = 46 template DB'de
- Intent detector EN için 12 intent'te ≥85% accuracy (sim test)
- Rotation logic unit test geçiyor

## Çıkış Kriteri
Simülasyon: 20 farklı "lead" senaryosu → Güneş beklenen intent'i seçiyor, varyant rotation çalışıyor, human handoff tetikleniyor.

## Riskler
- **LLM cost:** Her mesaj için intent detection çağrısı — caching + rule-based pre-filter ekle
- **Template approval:** Meta outbound template'ler için Faz 2 ile koordine
