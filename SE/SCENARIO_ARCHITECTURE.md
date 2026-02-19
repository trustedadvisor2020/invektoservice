# Invekto Senaryo Mimari ve Tasarim Kilavuzu

Bu dokuman, Invekto hizmet senaryolarinin (S1-S12 gelir, E01-M07 saha) dokumantasyon sayfalarini olustururken uyulmasi gereken mimari, icerik, tasarim ve CSS kurallarini belirler.

---

## 1. Sayfa Yapisi ve Organizasyon

### 1.1 Gelir Senaryolari (Revenue — S1-S12)

Her gelir senaryosu 3 tab + sabit footer yapisindan olusur. `ScenarioPage.jsx` bilesen tarafindan JSON'dan render edilir.

#### Sayfa Container

```
max-w-[1700px] mx-auto p-10 font-sans bg-gray-50/50 min-h-screen
```

- Max genislik: 1700px, ortali
- Padding: 40px (p-10)
- Font: Outfit (font-sans)
- Arkaplan: Hafif gri (`bg-gray-50/50`)

#### Header

```
mb-12 (48px alt bosluk)

Badge satiri: flex items-center gap-3 mb-5
  - Phase Badge: Badge color="blue" → bg-brand-100 text-brand-700
  - Category Badge: Badge color="green" → bg-emerald-100 text-emerald-700

Baslik: text-5xl font-extrabold text-t-primary mb-6 tracking-tight
  Format: "{ID.toUpperCase()}: {title}"
  Ornek: "S1: Negatif Yorum Kurtarma"

Subtitle: text-sm text-t-muted font-mono mb-2
  Ornek: "Review Recovery"

Aciklama: text-2xl text-t-secondary max-w-5xl font-light leading-relaxed
```

#### Tab Bar

```
flex gap-2 mb-10 border-b border-gray-200

Her tab butonu:
  px-8 py-4 font-bold text-base transition-colors border-b-4

  Aktif:   border-brand-600 text-brand-700
  Pasif:   border-transparent text-t-muted hover:text-t-primary

Tab isimleri:
  1. "GENEL BAKIS"
  2. "SENARYO AKISLARI ({flows.length})"
  3. "TEKNIK DETAYLAR"
```

---

### Tab 1: Genel Bakis (Overview)

**Amaci:** Is mantigi, hedef kitle, "Neden?" sorusu.

```
space-y-10 animate-fade-in
```

#### 1a. Ust Grid: 2 kolon

```
grid grid-cols-1 md:grid-cols-2 gap-10
```

**Sol — Hedef Kitle & Sektor (FlatCard)**
- `icon={User}` `title="Hedef Kitle & Sektor"`
- Icerik: `<ul className="space-y-4">`
- Her satir: `flex items-start gap-4 text-t-secondary text-lg`
  - Ikon: `CheckCircle size={24} text-emerald-500 mt-0.5`
  - Metin: `<strong>{label}:</strong> {value}`

**Sag — Entegre Servisler (FlatCard)**
- `icon={Database}` `title="Entegre Servisler"`
- Icerik: `flex flex-wrap gap-3` icinde Badge'ler
- Opsiyonel: `servicesNote` — `mt-6 text-base text-t-muted`

#### 1b. Sistem Calisma Mantigi (FlatCard)

```
FlatCard: title="Sistem Calisma Mantigi" icon={Zap}
  Ekstra sinif: border-l-4 border-brand-500
```

- Icerik: `mt-4 space-y-4`
- Her adim: `<Step number={i+1} title={...} goal={...}>`
  - Badge'ler: `flex gap-2 mb-2` icinde
  - Metin: string content

---

### Tab 2: Senaryo Akislari (Scenarios)

**Amaci:** Farkli use-case'leri ve AI davranislarini canli chat ile gostermek.

```
animate-fade-in
```

#### 2a. Akis Secici

```
flex flex-wrap gap-4 mb-10

Her buton:
  px-5 py-3 rounded-xl text-base font-bold transition-all shadow-sm border

  Aktif:   bg-brand-600 text-white border-brand-600 shadow-lg ring-4 ring-brand-100
  Pasif:   bg-surface text-t-secondary border-brand-100 hover:bg-brand-50 hover:border-brand-300

  Format: "{idx+1}. {flow.title}"
```

#### 2b. Senaryo Paneli

```
grid grid-cols-1 lg:grid-cols-3 gap-10
```

**Sol Kolon (1/3) — Senaryo Detayi + Gereksinimler**

```
lg:col-span-1 space-y-8
```

- **Senaryo Detayi (FlatCard):**
  - `icon={Smartphone}` `className="bg-brand-50/50 border-brand-100"`
  - Baslik: `text-2xl font-bold text-t-primary mb-3`
  - Aciklama: `text-t-secondary text-base leading-relaxed mb-6`
  - Tag'ler: `flex gap-3 flex-wrap` icinde Badge'ler

- **Gereksinimler (FlatCard):**
  - `icon={Info}` `title="Gereksinimler"`
  - `RequirementItem` component ile render edilir (geriye uyumlu: string veya object)
  - Her gereksinim item'i su alanlari icerebilir:

    | Alan | Tip | Zorunlu | Aciklama |
    |------|-----|---------|----------|
    | `text` | string | Evet | Gereksinim metni |
    | `service` | string | Hayir | Kaynak servis (Backend, AgentAI, Knowledge, Integrations, ChatAnalysis) |
    | `page` | string | Hayir | Servis icindeki sayfa yolu (`Urun Katalogu > Stok Yonetimi`) |
    | `status` | enum | Hayir | `ready` (Hazir/yesil), `setup` (Kurulum/amber), `optional` (Opsiyonel/gri) |
    | `priority` | enum | Hayir | `required` (Zorunlu/rose), `recommended` (Onerilen/mavi), `optional` (Opsiyonel/gri) |
    | `effort` | enum | Hayir | `easy` (Kolay/yesil), `medium` (Orta/amber), `technical` (Teknik/mor) |
    | `capability` | string | Hayir | Ilgili yetenek kodu (C1-C13) — Badge olarak gosterilir |
    | `hint` | string | Hayir | Kurulum/yapilacaklar hakkinda kisa aciklama (italik) |

  - **Gorsel Layout (her item):**
    1. Metin: `font-medium text-t-primary` (kalin)
    2. Badge satiri: `flex flex-wrap gap-1.5` — Status + Priority + Effort + Capability Badge'leri
    3. Kaynak: `flex items-center gap-1 text-sm` + `ArrowUpRight size={14}` — `{service} · {page}`
    4. Ipucu: `text-sm text-t-muted italic`

  - "Satici Tarafi" bolumu:
    - Etiket: `text-sm font-bold text-t-muted uppercase tracking-wider block mb-3`
    - Liste: `space-y-4`
    - Bullet: `w-2 h-2 rounded-full bg-brand-400 mt-2`
    - Kaynak renk: `text-brand-500`
  - "Sistem Tarafi" bolumu:
    - Ayirici: `pt-6 border-t border-brand-50`
    - Bullet: `w-2 h-2 rounded-full bg-emerald-400 mt-2`
    - Kaynak renk: `text-emerald-600`

**Sag Kolon (2/3) — Chat Preview**

```
lg:col-span-2
```

ChatPreview bilesen detaylari: Bolum 4'e bak.

---

### Tab 3: Teknik Detaylar (Tech)

**Amaci:** Gelistirici ve mimarlar icin altyapi.

```
animate-fade-in space-y-10
```

#### 3a. Teknik Not (Callout)

```
Callout type="info" title="Teknik Not"
  → border-l-4 border-brand-500 bg-brand-50 text-brand-900
  → p-5 rounded-r-lg shadow-sm mb-6
```

#### 3b. Servis Kartlari

```
grid grid-cols-1 md:grid-cols-2 gap-10
```

- **Backend Servisleri (FlatCard):** `icon={Server}` `title="Backend Servisleri"`
  - Her madde: `<Step number={A/B/C...} title={...} goal={...}>{content}</Step>`
  - Numaralama: `String.fromCharCode(65 + i)` → A, B, C...

- **API Entegrasyonlari (FlatCard):** `icon={Database}` `title="API Entegrasyonlari"`
  - Ayni Step yapisi

#### 3c. Konfigurasyon (FlatCard)

```
FlatCard: icon={BookOpen} title="Ornek Konfigurasyon"

<pre className="bg-gray-50 border border-gray-200 rounded-lg p-6 text-sm font-mono overflow-x-auto text-t-primary">
  {tech.config}
</pre>
```

---

### Footer: Interaktif ROI Hesaplayici

**Amaci:** Yatirim getirisini (ROI) canli hesaplatmak. Tum tab'lardan bagimsiz, sayfanin en altinda sabit durur.

```
mt-16

FlatCard: className="bg-emerald-50/80 border-emerald-100"
  (title prop yok — ozel header)
```

#### ROI Header

```
mb-8

Baslik satiri: flex items-center gap-3 mb-2
  h2: text-3xl font-bold text-emerald-900
    Format: "{title}: ~{result.toLocaleString('tr-TR')} TL"
  Etiket: text-xs font-mono bg-emerald-200 text-emerald-800 px-2 py-1 rounded
    Metin: "CANLI HESAPLAMA"

Aciklama: text-emerald-700 text-lg
```

#### ROI Input Grid

```
grid grid-cols-1 md:grid-cols-2 lg:grid-cols-{min(inputs.length+1, 5)} gap-8 pt-8 border-t border-emerald-200
```

Her input:
```
Etiket: text-emerald-600 text-sm font-bold uppercase tracking-wide mb-1
Deger:  text-3xl font-bold text-emerald-900

Duzenlenebilir input:
  bg-transparent border-b-2 border-dashed border-emerald-400 w-24 text-center
  focus:border-emerald-600

Ipucu: text-emerald-700/80 text-sm mt-1
```

Sonuc kutusu:
```
bg-emerald-100/50 rounded-lg p-4 -m-2

Etiket: text-emerald-600 text-sm font-bold uppercase tracking-wide mb-1
Deger:  text-3xl font-bold text-emerald-900 → "{(result/1000).toFixed(1)}k TL"
ROI:    text-emerald-700/80 text-sm → "{roi}x ROI ({cost} TL abonelik)"
```

#### ROI Formul Gosterimi (Opsiyonel)

```
mt-6 pt-4 border-t border-emerald-200
text-emerald-600 text-sm font-mono
```

---

### 1.2 Saha Senaryolari (Field — E01, D27, H01, vb.)

Saha senaryolari daha basit bir yapiya sahiptir. `FieldScenarioDetail.jsx` bilesen tarafindan render edilir.

#### Sayfa Container

```
max-w-[1200px] mx-auto p-10 font-sans min-h-screen
```

#### Geri Butonu

```
inline-flex items-center gap-2 text-brand-600 hover:text-brand-800 font-medium mb-8 transition-colors
Ikon: ArrowLeft size={18}
Metin: "Tum Senaryolar"
```

#### Header

```
mb-10

Kod + Badge satiri: flex items-center gap-3 mb-4
  Kod: text-sm font-mono font-bold text-brand-600 bg-brand-50 px-3 py-1.5 rounded-lg
  Phase Badge: Badge color="blue"
  Tip Badge: Badge color="gray" → "Saha Senaryosu"

Baslik: text-4xl font-extrabold text-t-primary mb-4 tracking-tight
Aciklama: text-xl text-t-secondary max-w-3xl font-light leading-relaxed
```

#### Icerik Grid

```
grid grid-cols-1 md:grid-cols-2 gap-8
```

- **Musteri Mesaji (FlatCard):** `icon={MessageCircle}` `title="Musteri Mesaji"`
  - Balon: `bg-[#d9fdd3] rounded-xl p-5 flex items-start gap-3`
  - Ikon: `MessageCircle size={20} text-emerald-600 mt-0.5`
  - Metin: `text-base text-gray-800 italic` → `"{customerMessage}"`

- **Invekto Cozumu (FlatCard):** `icon={Zap}` `title="Invekto Cozumu"`
  - Balon: `bg-brand-50 rounded-xl p-5 flex items-start gap-3`
  - Ikon: `ArrowRight size={20} text-brand-600 mt-0.5`
  - Metin: `text-base text-brand-900`

#### Yetenekler (Capabilities)

```
FlatCard: title="Gereken Yetenekler (Capabilities)" icon={Zap} className="mt-8"

Grid: grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4

Her yetenek:
  flex items-center gap-3 p-3 rounded-lg bg-gray-50 border border-gray-100
  Badge: color={capabilityColors[cap]}
  Etiket: text-sm text-t-secondary
```

**Capability Renk Haritasi:**

| Kod | Isim | Renk |
|-----|------|------|
| C1 | Unified Inbox | blue |
| C2 | Routing | blue |
| C3 | Templates | blue |
| C4 | Reporting | purple |
| C5 | Security Baseline | rose |
| C6 | Enterprise Security | rose |
| C7 | Knowledge/RAG | green |
| C8 | Agent Assist | amber |
| C9 | Auto-Resolution | gray |
| C10 | Revenue Agent | purple |
| C11 | E-com Integrations | amber |
| C12 | Ads Attribution | purple |
| C13 | QA Mining | gray |

---

## 2. Tasarim Sistemi (Design System)

Proje **"WapCRM Docs Flat Design"** sistemini takip eder.

### Renk Paleti (Token Bazli)

| Token | Kullanim | Hex |
|:---|:---|:---|
| `brand-500` | Ana Eylem / Vurgu | `#6366f1` (Indigo) |
| `brand-600` | Tab aktif, buton hover | Indigo 600 |
| `brand-700` | Baslik hover, aktif link | Indigo 700 |
| `brand-100` | Badge arkaplan, kart kenarligi | Indigo 100 |
| `brand-50` | Hafif vurgu arkaplan | Indigo 50 |
| `surface` | Kart Arkaplanlari | `#ffffff` |
| `bg-gray-50` | Sayfa & Sohbet Arkaplan | `#f9fafb` |
| `t-primary` | Ana Metin | `#111827` |
| `t-secondary` | Aciklama Metni | `#6b7280` |
| `t-muted` | Deaktif / Ipucu | Gray 400 |
| `emerald-500` | Basari, Onay ikonu | `#10b981` |
| `emerald-600` | Chat cevrimici, ROI | Emerald 600 |
| `amber-500` | Uyari callout | Amber 500 |
| `rose-500` | Hata / Tehlike | Rose 500 |
| `#d9fdd3` | WhatsApp kullanici balonu | Yesil |

### Tipografi

| Kullanim | Sinif | Boyut |
|:---|:---|:---|
| Sayfa basligi (Revenue) | `text-5xl font-extrabold tracking-tight` | ~48px |
| Sayfa basligi (Field) | `text-4xl font-extrabold tracking-tight` | ~36px |
| Kart basligi | `text-xl font-bold` | 20px |
| Senaryo basligi (flow) | `text-2xl font-bold` | 24px |
| Aciklama | `text-2xl font-light leading-relaxed` | 24px |
| Govde metni | `text-base leading-relaxed` | 16px |
| Tab etiketi | `text-base font-bold` | 16px |
| Badge | `text-sm font-medium font-mono` | 14px |
| Kod/subtitle | `text-sm font-mono` | 14px |
| Kucuk etiket | `text-xs font-mono` | 12px |
| ROI degeri | `text-3xl font-bold` | 30px |

**Fontlar:**
- Genel: `Outfit` (Modern, Sans-serif) → Tailwind `font-sans`
- Kod/Teknik: `JetBrains Mono` → Tailwind `font-mono`

---

## 3. Atomik Bilesen Referansi

### 3.1 Badge

```
px-3 py-1 rounded-md text-sm font-medium font-mono
```

| Renk Prop | Siniflar |
|:---|:---|
| `gray` | `bg-gray-100 text-t-secondary` |
| `blue` / `indigo` | `bg-brand-100 text-brand-700` |
| `green` | `bg-emerald-100 text-emerald-700` |
| `amber` | `bg-amber-100 text-amber-700` |
| `purple` | `bg-purple-100 text-purple-700` |
| `rose` | `bg-rose-100 text-rose-700` |

### 3.2 FlatCard

```
bg-surface rounded-xl shadow-sm border border-brand-100 p-8

Baslik: flex items-center gap-3 text-xl font-bold text-t-primary mb-5 pb-3 border-b border-gray-50
  Ikon: text-brand-500 size={24}
```

### 3.3 Step

```
flex gap-5 relative pb-10 last:pb-0

Dikey cizgi: absolute left-[18px] top-10 bottom-0 w-0.5 bg-gray-200 (son adimda gizli)

Numara dairesi:
  w-10 h-10 rounded-full border-2 border-brand-200 bg-surface text-brand-600
  font-bold text-base font-mono shadow-sm z-10

Baslik: text-xl font-bold text-t-primary
Hedef etiketi: text-xs text-t-muted bg-gray-50 px-2 py-1 rounded border border-gray-100 font-mono
  → "Hedef: {goal}"

Icerik: text-t-secondary leading-relaxed text-base
```

### 3.4 Callout

```
p-5 rounded-r-lg shadow-sm mb-6 border-l-4
```

| Tip | Arkaplan | Kenarlik | Metin |
|-----|----------|----------|-------|
| info | bg-brand-50 | border-brand-500 | text-brand-900 |
| warning | bg-amber-50 | border-amber-500 | text-amber-900 |
| success | bg-emerald-50 | border-emerald-500 | text-emerald-900 |
| danger | bg-rose-50 | border-rose-500 | text-rose-900 |

```
Baslik: font-bold text-lg mb-2 flex items-center gap-2
  info → Info ikonu, warning → AlertTriangle ikonu

Icerik: text-base leading-relaxed
```

### 3.5 ScenarioCard (Landing kart)

```
bg-surface rounded-xl shadow-sm border border-brand-100 p-6
hover:shadow-md hover:border-brand-300 transition-all cursor-pointer

Header: flex items-start justify-between mb-4
  Kod: text-sm font-mono font-bold text-brand-600 bg-brand-50 px-2 py-1 rounded
  Baslik: text-lg font-bold text-t-primary → hover: text-brand-700

Aciklama: text-t-secondary text-base mb-4 leading-relaxed
Musteri mesaji: bg-[#d9fdd3] rounded-xl p-3 mb-4 → text-sm italic
Cozum: bg-brand-50 rounded-xl p-3 mb-4 → text-sm text-brand-900
Capabilities: flex flex-wrap gap-2 mt-4 pt-4 border-t border-gray-100
```

---

## 4. Chat Preview Bilesen Spesifikasyonu

### 4.1 Container

```
bg-gray-100 rounded-2xl overflow-hidden border border-brand-100 shadow-sm flex flex-col h-[800px]
```

Sabit yukseklik: 800px, scroll yapilabilir icerik alani.

### 4.2 Chat Header

```
bg-surface p-5 border-b border-brand-100 shadow-sm z-10 flex items-center justify-between

Avatar: w-12 h-12 rounded-full bg-emerald-500 → ikon: beyaz size={24}
Asistan adi: font-bold text-t-primary text-base
Durum: text-sm text-emerald-600 font-medium → "Cevrimici"
Sag: Badge color="gray" → "Canli Onizleme"
```

### 4.3 Mesaj Rolleri ve Stilleri

| Rol | Konum | Arkaplan | Metin | Ikon | Kenarlik |
|:----|:------|:---------|:------|:-----|:---------|
| `user` | Sag (`justify-end`) | `bg-[#d9fdd3]` | `text-gray-900` | — | `rounded-tr-none` |
| `bot` | Sol (`justify-start`) | `bg-white` | `text-gray-900` | — | `rounded-tl-none` |
| `system` | Orta (`justify-center`) | `bg-amber-50` | `text-amber-800` | Server | `border-amber-200` |
| `ai` | Orta (`justify-center`) | `bg-brand-100` | `text-brand-800` | Cpu | `border-brand-200` |
| `agent` | Orta (`justify-center`) | `bg-rose-50` | `text-rose-800` | Server | `border-rose-200` |

**user/bot balonu:**
```
max-w-[80%] p-4 rounded-xl text-base shadow-sm leading-relaxed

Zaman damgasi: text-xs text-gray-400 text-right mt-2
  User: "14:3X ✓✓"
  Bot:  "14:3X"

Ucgen (SVG tail):
  User: sag ust → fill-[#d9fdd3]
  Bot:  sol ust → fill-white
```

**system/ai/agent etiket:**
```
max-w-[90%] px-4 py-2 rounded-lg text-sm font-bold text-center border shadow-sm flex items-center gap-2
```

### 4.4 Chat Input (Placeholder)

```
p-4 bg-gray-50 border-t border-gray-200 flex items-center gap-3

Sol ikon: Zap size={24} text-gray-400
Input: bg-white border border-gray-200 rounded-full px-6 py-3 text-base text-gray-400 shadow-sm
  Placeholder: "Mesaj yazin..."
Sag ikon: Smartphone size={24} text-gray-400
```

---

## 5. JSON Veri Semasi

### 5.1 Gelir Senaryosu (Revenue — s1.json - s12.json)

```json
{
  "id": "s1",
  "title": "Negatif Yorum Kurtarma",
  "subtitle": "Review Recovery",
  "phase": "Phase 3",
  "category": "E-TICARET OTOMASYONU",
  "niche": "ecommerce",
  "description": "Uzun aciklama...",

  "overview": {
    "audience": [
      { "label": "Sektor", "value": "E-Ticaret" }
    ],
    "services": [
      { "name": "WhatsApp Business API", "color": "green" }
    ],
    "servicesNote": "Opsiyonel alt not",
    "steps": [
      {
        "title": "Tespit (Detection)",
        "goal": "Negatif yorumu yakala",
        "badges": [
          { "text": "Sistem (Otomatik)", "color": "purple" }
        ],
        "content": "Aciklama metni..."
      }
    ]
  },

  "flows": [
    {
      "id": "flow-1",
      "title": "Gec Teslimat / Hasarli Urun",
      "description": "Musteri kargodan sikayetci.",
      "assistantName": "Destek Asistani",
      "assistantIcon": "Smartphone",
      "tags": [
        { "text": "Otomatik Yanit", "color": "blue" }
      ],
      "requirements": {
        "client": [
          {
            "text": "Yedek urun stogu",
            "service": "Backend",
            "page": "Urun Katalogu > Stok Yonetimi",
            "status": "setup",
            "priority": "required",
            "effort": "easy",
            "capability": "C11",
            "hint": "Envanter modulunde yedek urun miktarlarini guncel tutun"
          }
        ],
        "provider": [
          {
            "text": "Hasar tanima promptu",
            "service": "AgentAI",
            "page": "Prompt Sablonlari > Gorsel Analiz",
            "status": "ready",
            "priority": "required",
            "effort": "easy",
            "capability": "C8",
            "hint": "Varsayilan prompt sistemde hazir, ozellestirilebilir"
          }
        ]
      },
      "steps": [
        { "role": "system", "content": "Trendyol API: 1 Yildiz Yorum Tespit" },
        { "role": "ai", "content": "Analiz: Negatif - Kargo Hasari." },
        { "role": "bot", "content": "Merhaba, hemen telafi edelim." },
        { "role": "user", "content": "[Fotograf gonderir]" },
        { "role": "agent", "content": "Manuel Kontrol: Anomali tespit." }
      ]
    }
  ],

  "tech": {
    "note": "Bu senaryo tamamen asenkron calisir.",
    "backend": [
      { "title": "Cron Job Service", "goal": "Zamanli Gorevler", "content": "Her 15 dk..." }
    ],
    "apis": [
      { "title": "Trendyol Seller API", "goal": "Veri Kaynagi", "content": "OAuth2..." }
    ],
    "config": "{ \"cronInterval\": \"15m\" }"
  },

  "impact": {
    "title": "Potansiyel Aylik Kayip",
    "description": "Asagidaki degerleri duzenleyin.",
    "resultLabel": "Kurtarilan (%30)",
    "subscriptionCost": 5000,
    "inputs": [
      {
        "key": "dailyOrders",
        "label": "Gunluk Siparis",
        "default": 200,
        "hint": "Ortalama kargo sayisi",
        "editable": true,
        "prefix": "",
        "suffix": "TL",
        "step": 1,
        "min": 0,
        "hidden": false,
        "constant": null
      }
    ],
    "formula": "dailyOrders * (errorRate / 100) * 4 * avgBasket * 30 * 0.3",
    "formulaDisplay": null
  }
}
```

### 5.2 Saha Senaryosu (Field — field-scenarios.json)

```json
{
  "sectorKey": {
    "label": "E-Ticaret",
    "scenarios": [
      {
        "id": "E01",
        "name": "Kargo Takip Sorusu",
        "description": "Musteri kargo durumunu soruyor.",
        "customerMessage": "Kargomu ne zaman alacagim?",
        "solution": "Otomatik kargo takip linki gonderilir.",
        "capabilities": ["C1", "C3"],
        "phase": "Phase 1"
      }
    ]
  }
}
```

### 5.3 Sektor Gruplari (Sidebar)

```
ecommerce      → E-Ticaret (E01-E25 + EB01-EB07)
dental         → Saglik / Dis Hekimi (D01-D19)
aesthetic      → Saglik / Estetik (A01-A20)
healthExtra    → Saglik / Ek (SB01-SB05)
crossSector    → Evrensel (CS01-CS08)
hotel          → Otel / Turizm (H01-H17)
beauty         → Guzellik Salonu (GS01-GS25)
education      → Egitim (ED01-ED25)
mobile         → Mobil Uygulama (M01-M07)
ecommerceExtra → E-Ticaret / Ek (EB01-EB07)
```

---

## 6. Kodlama Standartlari

1. **Atomik Bilesenler:** `Badge`, `FlatCard`, `Step`, `Callout`, `ChatPreview`, `InteractiveROI`, `ScenarioCard` — tumu `components/` altinda.
2. **Tailwind Config:** Renkler ve fontlar `tailwind.config.js` uzerinden `brand-*` ve `font-*` olarak cekilmeli. Hardcoded hex **YASAK** (tek istisna: WhatsApp `#d9fdd3`).
3. **Lucide React:** Ikon seti olarak sadece `lucide-react` kullanilmali. JSON'dan string ikon cozumlemesi `import * as LucideIcons` ile yapilir.
4. **JSON-Driven:** Tum icerik JSON dosyalarindan gelir. Bilesen icinde hardcoded metin yasak (UI etiketleri haric).
5. **Responsive:** `grid-cols-1 md:grid-cols-2 lg:grid-cols-3` patterni kullanilir. Mobile-first.
6. **Animasyon:** Tab gecislerinde `animate-fade-in` kullanilir (Tailwind custom animation).

---

*Bu dokuman, tum senaryo sayfalarinda tutarliligi (Consistency) saglamak amaciyla olusturulmustur.*
*Son guncelleme: 2026-02-19*
