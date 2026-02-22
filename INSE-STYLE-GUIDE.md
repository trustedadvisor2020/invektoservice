# Invekto UI Stil Rehberi

> Bu dokuman InvektoServices projesindeki tum UI stillerinin kapsamli referansidir.
> Baska projede kullanmak icin tasarlandi. Stripe-inspired, minimal, modern.

---

## 1. Temel Yapilandirma

### Font

```
Font: Inter (Google Fonts)
Fallback: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif
Base Size: 15px (html root)
Letter Spacing: -0.011em
Rendering: antialiased
```

**Import:**
```css
@import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');
```

### Font Agirliklari

| Agirlik | Kullanim |
|---------|----------|
| 400 (normal) | Body text, paragraflar |
| 500 (medium) | Navigasyon linkleri, buton text, form label |
| 600 (semibold) | Basliklar, card title, sidebar logo |
| 700 (bold) | Metrik rakamlari, vurgulu basliklar |

### Font Boyutlari

| Token | Boyut | Line Height | Kullanim |
|-------|-------|-------------|----------|
| `text-2xs` | 0.6875rem (11px) | 1rem | Version text, mikro etiketler, badge icerisindeki auth tag |
| `text-xs` | 0.75rem (12px) | 1rem | Badge, buton sm, filtre etiketleri, zaman damgasi |
| `text-sm` | 0.875rem (14px) | 1.25rem | Body text, form input, tablo icerigi |
| `text-base` | 1rem (16px) | 1.5rem | Card title, node basligi |
| `text-lg` | 1.125rem (18px) | 1.75rem | Bolum basligi |
| `text-xl` | 1.25rem (20px) | 1.75rem | Sayfa basligi (h1) |
| `text-2xl` | 1.5rem (24px) | 2rem | Metrik sayilari |
| `text-[10px]` | 10px | - | Port numaralari, uyari etiketleri, tooltip metni |
| `text-[11px]` | 11px | relaxed | Tooltip body metni |
| `text-[13px]` | 13px | - | Sidebar navigasyon linkleri |
| `text-[9px]` | 9px | - | Help tooltip soru isareti (?) |

---

## 2. Renk Sistemi

### Brand (Ana Renk - Mor/Indigo)

```
brand-50:  #F0EEFF   — Aktif navigasyon bg, bilgi badge bg
brand-100: #DBD8FF   — Bilgi badge border
brand-200: #B8B3FF
brand-300: #948EFF
brand-400: #7A74FF
brand-500: #635BFF   — Primary buton bg, logo bg, focus ring, selected edge (ANA RENK)
brand-600: #5046E5   — Primary buton hover
brand-700: #3D35CC   — Primary buton active, bilgi badge text
brand-800: #2E28A3
brand-900: #1F1B7A
```

### Navy (Notr Tonlar - Ana Text/BG)

```
navy-50:  #F7F9FC   — Sayfa bg, ghost hover bg, canvas bg
navy-100: #E3E8EE   — Border, ayirici, sidebar border
navy-200: #C1C9D2   — Scrollbar thumb, disabled text
navy-300: #8898AA   — Placeholder text, ikincil text, sidebar ikon inactive
navy-400: #6B7C93   — Ghost buton text, navigasyon text, aciklama metni
navy-500: #425466   — Yari kalin text
navy-600: #30425A
navy-700: #1A2B3D   — Label text, secondary buton text
navy-800: #0D1F30
navy-900: #0A2540   — Ana baslik text, body text
```

### Durum Renkleri

#### Yesil (Basari / OK)

```
emerald-50:  bg    — Basari badge bg, pozitif metrik bg
emerald-100: border — Basari badge border
emerald-200:       — Popup border
emerald-300:       — Simulation buton border
emerald-400:       — SimPanel avatar bg, sim focus ring
emerald-500: #10b981 — Durum noktasi OK, input handle bg, toggle checkbox
emerald-600:       — SimPanel header bg, basari ikon
emerald-700:       — Basari badge text, pozitif deger text
```

#### Kirmizi (Hata / Tehlike)

```
red-50:   bg — Hata banner bg, hata badge bg, confirm dialog bg
red-100:  border — Hata badge border, hata banner border
red-200:  border — Confirm dialog border
red-400:  — Hata input border
red-500:  #ef4444 — Hata noktasi, danger buton bg, output handle bg, chart stroke
red-600:  — Hata badge text, hata mesaj text, confirm buton bg
red-700:  — Confirm text, hata buton hover
```

#### Sari/Turuncu (Uyari)

```
amber-50:   bg — Uyari badge bg, impersonation satiri
amber-100:  border — Uyari badge border
amber-200:  — Uyari border
amber-400:  — Dirty indicator noktasi
amber-500:  #f59e0b — Degraded durum noktasi, impersonation bg
amber-600:  — Uyari buton
amber-700:  — Uyari badge text, orta sure text
```

#### Mavi (Bilgi / Focus)

```
blue-100: — Expanded log border
blue-200: — Expanded bg
blue-400: — Selected node ring
blue-500: — Focus border, tab aktif border
blue-600: — Validate buton bg, link text
blue-700: — ViewMode aktif text
```

#### Mor (AI / Ozel)

```
purple-50:   bg — Ort. sure metrik bg
purple-100:  — Note rengi (ede9fe)
purple-500:  — Ghost path buton aktif bg, AI buton aktif bg
purple-600:  — Mor metrik ikon, accent-purple input, degisken text
```

#### Turuncu

```
orange-50:  bg — Handoff rate metrik bg
orange-600: — Handoff ikon + text
```

### FlowBuilder Node Renkleri

```
trigger:  #10b981 (Yesil)   — Tetikleyici nodlari
message:  #635BFF (Mor)     — Mesaj nodlari
logic:    #f59e0b (Sari)    — Mantik nodlari
ai:       #8b5cf6 (Lavanta) — Yapay Zeka nodlari
action:   #ef4444 (Kirmizi) — Aksiyon nodlari
utility:  #6b7280 (Gri)     — Arac nodlari
```

### Ozel Renkler

```
Selection highlight:   rgba(99, 91, 255, 0.12)  — ::selection
Focus ring:           rgba(99, 91, 255, 0.15)   — shadow-focus
Sim glow yesil:       rgba(16, 185, 129, 0.3)   — Simülasyon aktif node
Hata glow:            rgba(239, 68, 68, 0.3)    — Hata durum node
Uyari glow:           rgba(245, 158, 11, 0.3)   — Degraded durum node
Node header alpha:    {color}20                  — Node header bg (renk %12 opacity)
Backdrop:             bg-navy-900/40             — Modal backdrop + backdrop-blur-sm
Backdrop siyah:       bg-black/30                — Alternatif backdrop
```

### HTTP Metot Renkleri

```
GET:     bg-emerald-50  text-emerald-700
POST:    bg-brand-50    text-brand-700
PUT:     bg-amber-50    text-amber-700
DELETE:  bg-red-50      text-red-700
DEFAULT: bg-navy-50     text-navy-700
```

### Note Renkleri (Utility Node)

```
Sari:    #fef3c7
Mavi:    #dbeafe
Yesil:   #dcfce7
Kirmizi: #fee2e2
Mor:     #ede9fe
```

---

## 3. Spacing ve Layout

### Sayfa Layout

```
min-h-screen flex           — Ana konteyner
Sidebar: w-56 (224px)       — Sol sidebar genisligi
Main content: flex-1        — Dinamik genislik
Content padding: p-8        — Icerik alani padding
max-w-7xl mx-auto           — Icerik maks genislik (1280px)
```

### Sidebar

```
Genislik: w-56 (224px)
Bg: bg-white
Border: border-r border-navy-100
Position: h-screen sticky top-0
Logo alani: h-14, px-4
Nav padding: px-2 py-3
Nav item gap: space-y-0.5
Nav item: h-9, px-3, rounded-lg, gap-2.5
Nav ikon: w-4 h-4
Logo ikon konteyner: w-7 h-7, rounded-lg
Version: px-4 py-1.5
Logout: px-2 py-2, border-t
```

### Grid Sistemleri

```
Dashboard kartlari:    grid-cols-2 md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-3
Metrik kartlari:       grid-cols-2 md:grid-cols-4 gap-4
Settings grid:         grid-cols-1 sm:grid-cols-2 gap-4
Flow settings:         grid-cols-3 gap-3
```

### Ortak Spacing Degerleri

```
gap-0.5  (2px)   — Nav item arasi, tablo row arasi
gap-1    (4px)   — Kucuk buton grubu arasi
gap-1.5  (6px)   — Ikon + text arasi, badge grubu arasi
gap-2    (8px)   — Form input grubu arasi, buton md ikon gap
gap-2.5  (10px)  — Nav ikon + text arasi, logo ikon + text arasi
gap-3    (12px)  — Kart arasi, filtre elemanları arasi
gap-4    (16px)  — Form alanlari arasi, bolumler arasi
gap-6    (24px)  — Buyuk bölümler arasi
```

---

## 4. Bilesenler

### 4.1 Button

**Varyantlar:**

| Varyant | Normal | Hover | Active |
|---------|--------|-------|--------|
| **primary** | `bg-brand-500 text-white shadow-soft` | `bg-brand-600` | `bg-brand-700` |
| **secondary** | `bg-white text-navy-700 border border-navy-100 shadow-soft` | `bg-navy-50` | `bg-navy-100` |
| **danger** | `bg-red-500 text-white shadow-soft` | `bg-red-600` | `bg-red-700` |
| **ghost** | `bg-transparent text-navy-500` | `bg-navy-50` | `bg-navy-100` |

**Boyutlar:**

| Boyut | Yukseklik | Padding | Font |
|-------|-----------|---------|------|
| **sm** | h-8 (32px) | px-3 | text-xs, gap-1.5 |
| **md** | h-9 (36px) | px-4 | text-sm, gap-2 |
| **lg** | h-10 (40px) | px-5 | text-sm, gap-2 |

**Ortak Stiller:**
```
inline-flex items-center justify-center
rounded-lg
font-medium
transition-all duration-150 ease-out
focus:outline-none focus:ring-2 focus:ring-brand-500/20 focus:ring-offset-1
```

**Disabled:**
```
opacity-40 cursor-not-allowed pointer-events-none
```

### 4.2 Card

```
Bg: bg-white
Border: border border-navy-100
Radius: rounded-xl (12px)
Padding: p-5 (20px)
Shadow: shadow-card (0 1px 3px rgb(0 0 0 / 0.04), 0 1px 2px -1px rgb(0 0 0 / 0.04))
```

**Card Header:**
```
mb-4
```

**Card Title:**
```
text-base font-semibold text-navy-900
```

### 4.3 Badge

**Ortak Stiller:**
```
inline-flex items-center justify-center
px-2 py-0.5
rounded-full
text-xs font-medium
border
```

**Varyantlar:**

| Varyant | Bg | Text | Border |
|---------|-----|------|--------|
| **default** | `navy-50` | `navy-500` | `navy-100` |
| **success** | `emerald-50` | `emerald-700` | `emerald-100` |
| **warning** | `amber-50` | `amber-700` | `amber-100` |
| **error** | `red-50` | `red-600` | `red-100` |
| **info** | `brand-50` | `brand-700` | `brand-100` |

### 4.4 Input

```
Genislik: w-full
Yukseklik: h-10 (40px)
Padding: px-3
Bg: bg-white
Border: border border-navy-100
Radius: rounded-lg
Text: text-navy-900 text-sm
Placeholder: text-navy-300
```

**States:**
```
Hover:   border-navy-200
Focus:   border-brand-500 shadow-focus (0 0 0 3px rgba(99, 91, 255, 0.15))
Error:   border-red-400 → focus: border-red-500 shadow-[0_0_0_3px_rgba(237,95,116,0.15)]
```

**Label:**
```
text-sm font-medium text-navy-700 mb-1.5
```

**Error mesaji:**
```
mt-1.5 text-xs text-red-500
```

### 4.5 Select

Input ile ayni temel stiller + ozel ok isareti:
```
cursor-pointer appearance-none
Custom dropdown arrow: navy-300 (#8898AA) SVG bg-image, bg-no-repeat, bg-[right_12px_center]
```

### 4.6 Toolbar Butonlari (FlowBuilder)

```
flex items-center gap-1.5
px-3 py-1.5
rounded-md
text-xs font-medium
transition-colors
```

**Normal:** `bg-navy-50 hover:bg-navy-100 text-navy-500`
**Aktif:** Butona gore degisir:
- Preview aktif: `bg-sky-500 text-white`
- AI aktif: `bg-purple-500 text-white`
- Ghost Path aktif: `bg-purple-500 text-white`
- Test aktif: `bg-emerald-100 text-emerald-700 border border-emerald-300`
- Test normal: `bg-emerald-600 hover:bg-emerald-500 text-white`
- Kaydet (dirty): `bg-brand-500 hover:bg-brand-600 text-white`
- Kaydet (clean): `bg-navy-50 text-navy-300 cursor-not-allowed`
- Disabled: `bg-navy-50 text-navy-200 cursor-not-allowed`

### 4.7 Form Alanlari (FlowBuilder Panels)

**Input (panel icerisi):**
```
w-full bg-navy-50 border border-navy-200
rounded px-2 py-1.5 text-sm text-navy-700
outline-none focus:border-brand-500
```

**Textarea:**
```
Ayni stiller + resize-none
Font-mono eklemeli alanlar icin: font-mono text-xs
```

**Select (panel icerisi):**
```
w-full bg-navy-50 border border-navy-200
rounded px-2 py-1.5 text-sm text-navy-700
outline-none focus:border-brand-500
```

**Checkbox:**
```
w-3.5 h-3.5 (kucuk) veya w-4 h-4 (normal)
rounded border-navy-200
text-emerald-600 focus:ring-emerald-500  (veya purple, brand rengine gore)
```

**Range slider:**
```
w-full accent-purple-500 (AI alanlar icin)
w-full accent-red-500 (timeout icin)
```

**Field Group Label:**
```
text-xs font-medium text-navy-400
uppercase tracking-wider
mb-1 (veya mb-1.5)
```

### 4.8 Ekle/Cikar Butonlari

**Ekle butonu:**
```
w-full px-2 py-1
rounded border border-dashed border-navy-200
text-sm text-navy-400
hover:border-brand-500 hover:text-brand-600 (veya purple/amber)
transition-colors
```

**Cikar butonu:**
```
p-0.5
text-navy-300 hover:text-red-500
transition-colors
Ikon: w-3.5 h-3.5 X ikonu
```

**Sil butonu (tehlike):**
```
w-full px-3 py-1.5 rounded-md
text-sm font-medium
bg-red-50 text-red-600 hover:bg-red-100
transition-colors
```

---

## 5. Navigasyon

### Sidebar Navigasyon

**Normal link:**
```
flex items-center gap-2.5 h-9 px-3 rounded-lg
text-[13px] font-medium
text-navy-400
hover:bg-navy-50 hover:text-navy-700
transition-colors duration-150
```

**Aktif link:**
```
bg-brand-50 text-brand-600
Ikon: text-brand-500
```

**Normal ikon:**
```
w-4 h-4 flex-shrink-0 text-navy-300
```

### Impersonation Banner

```
fixed top-0 left-0 right-0
bg-amber-500 text-white
px-4 py-2
flex items-center justify-between
text-sm font-medium
z-50
```

### Sekme (Tab) Degistirici

**Login mode toggle:**
```
Konteyner: flex rounded-lg bg-navy-50 p-0.5
Aktif: bg-white text-navy-900 shadow-soft
Pasif: text-navy-400 hover:text-navy-600
transition-all duration-200
```

**ViewMode toggle (LogStream):**
```
Konteyner: flex bg-navy-100 rounded-lg p-0.5
Aktif: bg-white text-brand-700 shadow-sm
Pasif: text-navy-400 hover:text-navy-700
```

**Simulation tab:**
```
flex-1 px-3 py-1.5 text-xs font-medium
Aktif: text-emerald-700 border-b-2 border-emerald-500
Pasif: text-navy-400 hover:text-navy-700
```

**Settings modal tab:**
```
px-2 pb-1 text-xs font-medium border-b-2
Aktif: border-brand-500 text-brand-600 (veya border-purple-500 text-purple-600)
Pasif: border-transparent text-navy-300 hover:text-navy-600
```

---

## 6. Modal ve Popup

### Modal (Genel)

```
Wrapper: fixed inset-0 z-50 flex items-center justify-center
Backdrop: absolute inset-0 bg-black/30 (veya bg-navy-900/40 backdrop-blur-sm)
Panel: bg-white rounded-xl (veya rounded-2xl) shadow-xl
Max height: max-h-[80vh] flex flex-col
Tipik genislik: w-[420px] veya max-w-lg
```

**Modal Header:**
```
px-5 py-4 border-b border-navy-100
flex items-center justify-between
Baslik: text-sm font-semibold text-navy-900
```

**Modal Body:**
```
p-5 space-y-4 overflow-y-auto
```

**Kapat butonu:**
```
p-1 rounded hover:bg-navy-100
text-navy-300 hover:text-navy-600
Ikon: w-4 h-4 X ikonu
```

### Endpoint Popup

```
Panel: bg-white rounded-2xl shadow-elevated max-w-lg w-full
Max height: max-h-[80vh]
Border: border border-navy-100
```

**Popup Header:**
```
p-4 border-b border-navy-100
Ikon konteyner: w-8 h-8 rounded-lg bg-navy-50
Baslik: font-semibold text-navy-900
Alt text: text-xs text-navy-300
```

**Kapat butonu:**
```
w-8 h-8 rounded-lg hover:bg-navy-50
text-navy-300 hover:text-navy-600
```

---

## 7. Tablo

### Genel Tablo Stili

```
Wrapper: bg-white rounded-lg border border-navy-100 overflow-hidden
Table: w-full text-sm
```

**Thead:**
```
bg-navy-50 border-b border-navy-100
th: text-left px-4 py-2.5 font-medium text-navy-500
```

**Tbody:**
```
tr: border-b border-navy-100 hover:bg-navy-50/50
td: px-4 py-2.5
```

### Zaman Cizelgesi Tablosu (LogStream Expanded)

```
bg-white rounded-lg border border-navy-100 overflow-hidden
text-xs font-mono

thead: bg-navy-50 text-navy-400
th: text-left px-3 py-1.5

tbody tr: border-t border-navy-100
Hata satiri: bg-red-50/50
Uyari satiri: bg-amber-50/50
```

### Sure Renk Kodlamasi

```
> 5000ms: bg-red-100 text-red-700
> 1000ms: bg-amber-100 text-amber-700
<= 1000ms: bg-green-100 text-green-700
```

---

## 8. FlowBuilder Ozel Stiller

### Canvas

```
Bg: #F7F9FC
Grid: (React Flow default)
```

### Node (BaseNode)

```
Konteyner:
  min-w-[180px] max-w-[260px]
  rounded-lg border-2 shadow-lg
  bg: #ffffff (beyaz)
  transition-all

Secili: shadow-xl ring-2 ring-blue-400/50, borderColor: #60a5fa
Simülasyon aktif: ring-2 ring-emerald-400/60, borderColor: #10b981
Ghost path dimmed: opacity: 0.3

Header:
  flex items-center gap-2 px-3 py-2 rounded-t-md
  bg: {nodeColor}20 (renk %12 alpha)
  Ikon: w-5 h-5, color: nodeColor
  Text: text-base font-medium text-navy-700

Body:
  px-3 py-2 text-sm text-navy-500
```

### Handle (Baglanti Noktasi)

```
Input (Yesil - Ust):
  w-5 h-5 border-2
  bg-emerald-500 border-emerald-300
  hover: bg-emerald-400 border-emerald-200

Output (Kirmizi - Alt):
  w-5 h-5 border-2
  bg-red-500 border-red-300
  hover: bg-red-400 border-red-200
```

### Edge (Baglanti Cizgisi)

```
Normal: stroke: #8898AA, strokeWidth: 2
Secili: stroke: #635BFF, strokeWidth: 3
Baglanti cizgisi: stroke: #635BFF, strokeWidth: 2, strokeDasharray: 5
```

### Node Palette (Sol Panel)

```
Konteyner: w-52 bg-white border-r border-navy-100
Baslik: text-xs font-semibold text-navy-400 uppercase tracking-wider
Alt text: text-[10px] text-navy-300

Kategori basligi:
  text-[10px] font-semibold text-navy-300 uppercase tracking-wider

Palette item:
  flex items-center gap-2 px-3 py-2 rounded-md
  cursor-grab active:cursor-grabbing
  border border-transparent hover:border-navy-200
  bg-navy-50/60 hover:bg-navy-50

Item baslik: text-xs font-medium text-navy-700
Item aciklama: text-[10px] text-navy-300
```

### Property Panel (Sag Panel)

```
Konteyner: w-64 bg-white border-l border-navy-100

Header:
  p-3 border-b border-navy-100
  Renk noktasi: w-3 h-3 rounded-sm
  Baslik: text-base font-medium text-navy-700
  Aciklama: text-xs text-navy-300

Body: p-3 space-y-3
```

### Simulation Panel

```
Konteyner: w-[280px] border-l border-navy-100 bg-navy-50

Header: h-10 bg-emerald-600
  Avatar: w-6 h-6 rounded-full bg-emerald-400
  Text: text-white text-xs font-medium

Loading dots: w-1.5 h-1.5 bg-navy-200 rounded-full animate-bounce

Chat input:
  text-xs border border-navy-100 rounded-full
  px-3 py-1.5
  focus:border-emerald-400 focus:ring-1 focus:ring-emerald-400/30

Send butonu:
  w-7 h-7 rounded-full
  Aktif: bg-emerald-500 hover:bg-emerald-400 text-white
  Pasif: bg-navy-100 text-navy-200

Menu secenekleri:
  px-2.5 py-1 text-xs rounded-full border
  border-emerald-300 text-emerald-700 bg-emerald-50 hover:bg-emerald-100
```

### Flow Summary Bar (Onizleme Paneli)

```
Konteyner: w-60 bg-white border-l border-navy-100

Header: px-3 py-2.5 border-b border-navy-100
  Baslik: text-xs font-semibold text-navy-700
  Adim sayaci: text-[10px] text-navy-300 bg-navy-100 px-1.5 py-0.5 rounded-full

Summary item:
  text-xs leading-5
  Ikon: text-[10px] opacity-60
  Indent: paddingLeft = indent * 14px
```

### Toolbar

```
Konteyner: h-12 bg-white border-b border-navy-100 shadow-sm
flex items-center px-4 gap-3

Flow adi input:
  bg-transparent text-sm font-medium text-navy-900
  border-none outline-none w-40
  focus:ring-1 focus:ring-brand-500/30 rounded px-2 py-1

Divider: w-px h-6 bg-navy-100
Dirty indicator: w-2 h-2 rounded-full bg-amber-400
```

---

## 9. Golge Sistemi

```
shadow-soft:     0 1px 2px 0 rgb(0 0 0 / 0.04)          — Hafif golge (butonlar)
shadow-card:     0 1px 3px 0 rgb(0 0 0 / 0.04),          — Kart golgesi
                 0 1px 2px -1px rgb(0 0 0 / 0.04)
shadow-elevated: 0 4px 12px 0 rgb(0 0 0 / 0.06)          — Yukseltiimis golge (popup)
shadow-focus:    0 0 0 3px rgba(99, 91, 255, 0.15)        — Focus ring
shadow-xl:       (Tailwind default)                        — Secili node
shadow-lg:       (Tailwind default)                        — Node normal
shadow-sm:       (Tailwind default)                        — Toolbar, toggle aktif
```

---

## 10. Border Radius

```
rounded:      0.25rem (4px)   — Inline badge, kucuk eleman
rounded-md:   0.375rem (6px)  — Toolbar butonlari, toggle
rounded-lg:   0.5rem (8px)    — Butonlar, input, card kucuk, node
rounded-xl:   0.75rem (12px)  — Card, alert banner
rounded-2xl:  1rem (16px)     — Login card, modal, logo ikon
rounded-full: 9999px          — Badge, avatar, durum noktasi, send butonu
rounded-sm:   0.125rem (2px)  — Node tipi renk noktasi
```

---

## 11. Transition ve Animasyon

```
Renkler:          transition-colors duration-150
Tumu:             transition-all duration-150 ease-out (butonlar)
Golge/transform:  transition-all (node)
Login toggle:     transition-all duration-200

Animasyon:
  animate-spin:   Refresh ikonu donuyor
  animate-bounce: Loading dots (0ms, 150ms, 300ms delay)
  hover:scale-110: Renk secici butonlari (Note renkleri)
```

---

## 12. Scrollbar

```css
/* Dashboard */
::-webkit-scrollbar { width: 6px; height: 6px; }  /* w-1.5 h-1.5 */
::-webkit-scrollbar-track { background: transparent; }
::-webkit-scrollbar-thumb { background: #C1C9D2; border-radius: 9999px; }  /* navy-200, rounded-full */
::-webkit-scrollbar-thumb:hover { background: #8898AA; }  /* navy-300 */

/* FlowBuilder */
::-webkit-scrollbar { width: 5px; }
::-webkit-scrollbar-track { background: transparent; }
::-webkit-scrollbar-thumb { background: #C1C9D2; border-radius: 3px; }
::-webkit-scrollbar-thumb:hover { background: #8898AA; }
```

---

## 13. Selection

```css
::selection {
  background: rgba(99, 91, 255, 0.12);  /* brand-500 %12 alpha */
}
```

---

## 14. Z-Index Katmanlari

```
z-50: Impersonation banner, Modal/Popup backdrop + panel, Tooltip
```

---

## 15. Ikon Sistemi

**Kutuphane:** Lucide React

**Boyutlar:**
```
w-3 h-3     — Inline kucuk ikonlar (filtre, badge ici)
w-3.5 h-3.5 — Toolbar buton ikonlari, panel ikonlari
w-4 h-4     — Navigasyon ikonlari, aksiyon buton ikonlari
w-5 h-5     — Baslik ikonlari, metrik ikonlari, node header ikon
w-6 h-6     — Login logo ikonu
```

**flex-shrink-0** her zaman ikon'a eklenir (tasma onleme).

---

## 16. Responsive Kurallar

```
sm:   640px  — Settings grid 2 sutun
md:   768px  — Dashboard 3 sutun, Metrik 4 sutun
lg:   1024px — Dashboard 4 sutun
xl:   1280px — Dashboard 5 sutun
```

---

## 17. Alert / Banner / Toast Kaliplari

### Hata Banner

```
p-4 bg-red-50 border border-red-100 rounded-xl
flex items-center gap-3
Ikon konteyner: w-8 h-8 bg-red-100 rounded-lg
Text: text-sm text-red-600 font-medium
```

### Hata Mesaji (Inline)

```
p-3 bg-red-50 border border-red-100 rounded-lg
text-sm text-red-600
```

### Basari Mesaji

```
p-3 bg-emerald-50 border border-emerald-100 rounded-xl
text-sm text-emerald-700
flex items-center gap-2
```

### Uyari Mesaji

```
p-3 bg-amber-50 border border-amber-100 rounded-xl
text-sm text-amber-700
```

### Confirm Dialog (Inline)

```
flex items-center gap-2 px-3 py-2
bg-red-50 border border-red-200 rounded-lg text-sm
Ikon: w-4 h-4 text-red-500
Text: text-red-700
```

### Validation Result Box

```
rounded-lg border p-3 text-xs space-y-1
Gecerli:  bg-green-50 border-green-200
Hata:     bg-red-50 border-red-200
Uyari:    bg-amber-50 border-amber-200
```

---

## 18. SVG / Dependency Map Renkleri

```
OK node:       fill: #10b981, stroke: #059669, glow: rgba(16, 185, 129, 0.3)
Degraded node: fill: #f59e0b, stroke: #d97706, glow: rgba(245, 158, 11, 0.3)
Error node:    fill: #ef4444, stroke: #dc2626, glow: rgba(239, 68, 68, 0.3)

Backend node: rect 170x60 rx=10, border 2.5px
Dep node: rect 140x56 rx=8, border 2px
Arrow: stroke #e5e7eb, width 1.5, dasharray 6,4
Arrow head: fill #d1d5db

Legend: fontSize 11, fill #6b7280
Labels: fill #111827, fontSize 12/14, fontWeight 600
Port labels: fill #6b7280, fontSize 10/11
```

---

## 19. Chart / Grafik Stilleri (Recharts)

```
Axis: stroke #8898AA (navy-300), fontSize 10, tickLine false, axisLine false
Y-axis width: 30px

Grid: stroke #E3E8EE (navy-100)
Bar (conversations): fill #635BFF (brand-500)

Tooltip:
  bg: white
  border: 1px solid #E3E8EE (navy-100)
  borderRadius: 0.5rem
  boxShadow: 0 4px 6px -1px rgb(0 0 0 / 0.1)
  color: #0A2540 (navy-900)
  fontSize: 12px
  labelStyle color: #6B7C93 (navy-400)

Error gradient:
  5%:  #ef4444, opacity 0.3
  95%: #ef4444, opacity 0.05

Area: stroke #ef4444, strokeWidth 2, cursor pointer
```

---

## 20. Yardimci Siniflar (Utility)

### cn() Fonksiyonu (clsx + tailwind-merge)

```typescript
import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}
```

### Ortak Kaliplar

**Truncate:** `truncate` (single line ellipsis)
**Font mono:** `font-mono` (code, port, zaman, API path, degisken adi)
**Select all:** `select-all` (kopyalanabilir ID)
**Break words:** `break-words min-w-0` (tasmayan text)
**Uppercase tracking:** `uppercase tracking-wider` (section label)
**Leading tight:** `leading-tight` (sidebar logo text)

---

## 21. Tailwind Config Tam Referans

```javascript
/** @type {import('tailwindcss').Config} */
export default {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  theme: {
    extend: {
      colors: {
        brand: {
          50: '#F0EEFF', 100: '#DBD8FF', 200: '#B8B3FF',
          300: '#948EFF', 400: '#7A74FF', 500: '#635BFF',
          600: '#5046E5', 700: '#3D35CC', 800: '#2E28A3', 900: '#1F1B7A',
        },
        navy: {
          50: '#F7F9FC', 100: '#E3E8EE', 200: '#C1C9D2',
          300: '#8898AA', 400: '#6B7C93', 500: '#425466',
          600: '#30425A', 700: '#1A2B3D', 800: '#0D1F30', 900: '#0A2540',
        },
        // FlowBuilder ek renkler:
        // canvas: { bg: '#1a1a2e', grid: '#252542' },
        // node: { trigger: '#10b981', message: '#635BFF', logic: '#f59e0b',
        //         ai: '#8b5cf6', action: '#ef4444', utility: '#6b7280' },
      },
      fontFamily: {
        sans: ['Inter', '-apple-system', 'BlinkMacSystemFont', 'Segoe UI', 'Roboto', 'sans-serif'],
      },
      fontSize: {
        '2xs': ['0.6875rem', { lineHeight: '1rem' }],
      },
      borderRadius: {
        'xl': '0.75rem',
        '2xl': '1rem',
      },
      boxShadow: {
        'soft': '0 1px 2px 0 rgb(0 0 0 / 0.04)',
        'card': '0 1px 3px 0 rgb(0 0 0 / 0.04), 0 1px 2px -1px rgb(0 0 0 / 0.04)',
        'elevated': '0 4px 12px 0 rgb(0 0 0 / 0.06)',
        'focus': '0 0 0 3px rgba(99, 91, 255, 0.15)',
      },
    },
  },
  plugins: [],
}
```

---

## 22. Bagimliliklar

```json
{
  "clsx": "class birlestirme",
  "tailwind-merge": "tailwind class conflict cozmesi",
  "tailwindcss": "utility-first CSS framework",
  "postcss": "CSS islemci",
  "autoprefixer": "vendor prefix otomasyonu",
  "@xyflow/react": "Flow/graph gorselleştirme (FlowBuilder)",
  "recharts": "Chart/grafik kutuphanesi (Dashboard)",
  "lucide-react": "Ikon kutuphanesi",
  "react-router-dom": "Routing"
}
```

---

## 23. Hizli Baslangic

Baska projede bu stil sistemini kullanmak icin:

1. `tailwind.config.js`'den `colors`, `fontFamily`, `fontSize`, `boxShadow` ayarlarini kopyala
2. `index.css`'den font import ve base stilleri kopyala
3. `clsx` + `tailwind-merge` yukle, `cn()` fonksiyonunu olustur
4. `lucide-react` yukle (ikonlar icin)
5. UI bilesenlerini (Button, Card, Badge, Input, Select) kopyala
6. Brand rengini projeye gore degistir (`brand-500: #635BFF` -> yeni renk)

---

*Olusturulma: 2026-02-22 | Kaynak: InvektoServices Dashboard + FlowBuilder*
