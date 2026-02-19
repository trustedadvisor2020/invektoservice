# Invekto Senaryo Mimari ve Tasarım Kılavuzu

Bu doküman, Invekto hizmet senaryolarının (Örn: S1, S2) dokümantasyon sayfalarını oluştururken uyulması gereken mimari, içerik ve tasarım kurallarını belirler.

---

## 1. Sayfa Yapısı ve Organizasyon

Her senaryo sayfası, kullanıcıya (hem teknik hem de iş birimi) konuyu 3 farklı katmanda anlatmak üzere tasarlanmıştır. Bu katmanlar sekme (Tab) yapısı ile ayrılmalıdır:

### Tab 1: Genel Bakış (Overview)
**Amacı:** İş mantığını, hedef kitleyi ve "Neden?" sorusunu yanıtlamak.
**Zorunlu İçerik:**
1.  **Hedef Kitle & Sektör Kartı:** Kimin için? (Persona), Hangi Platform? (Pazaryeri, Web), Hacim Kriteri?
2.  **Entegre Servisler Kartı:** Kullanılan teknolojilerin logoları/isimleri (Badge olarak).
3.  **Sistem Çalışma Mantığı (Adımlar):** Sürecin kuş bakışı özeti.
    *   *Kural:* Her adımda **Sistem** ve **Tenant** sorumluluklarını gösteren Badge'ler kullanılmalıdır.
    *   *Örn:* `Sistem (Otomatik)` vs `Tenant (API Key)`

### Tab 2: Senaryo Akışları (Scenarios)
**Amacı:** Farklı kullanıcı durumlarını (Use-Cases) ve yapay zeka davranışlarını örneklendirmek.
**Zorunlu İçerik:**
1.  **Akış Menüsü:** En az 3-5 farklı senaryo (Örn: Geç Teslimat, Yanlış Ürün, Sahte Ürün).
2.  **Sol Panel (Gereksinimler):**
    *   **Senaryo Tanımı:** Durum nedir?
    *   **Gereksinim Listesi (Checklist):** Bu senaryonun çalışması için Satıcıdan (Client) ve Sistemden (Provider) ne bekleniyor?
3.  **Sağ Panel (Canlı Önizleme - Chat UI):**
    *   Gerçekçi bir WhatsApp/Chat arayüzü.
    *   **Sistem/AI Mesajları:** Araya giren lojik adımlar (sarı/mavi kutucuklar).
    *   **Diyalog:** Doğal, insansı ve çözüm odaklı konuşma balonları.

### Tab 3: Teknik Detaylar (Tech Specs)
**Amacı:** Geliştirici ve mimarlar için altyapıyı açıklamak.
**Zorunlu İçerik:**
1.  **Teknik Not (Callout):** Senkron/Asenkron yapı, kullanılan protokoller.
2.  **Backend Servisleri Kartı:** Cron, Queue, Worker yapısı.
3.  **API Entegrasyon Kartı:** Kullanılan endpointler ve yetkilendirme (OAuth2 vb.).
4.  **Konfigürasyon Örneği (JSON):** Sistemin parametrik yapısını gösteren kod bloğu (Opsiyonel ama önerilir).

### Footer: Etki Analizi (Impact Analysis)
**Amacı:** Yatırım getirisini (ROI) somutlaştırmak.
**Kural:** Sayfanın en altında, tüm sekmelerden bağımsız sabit durmalı.
**İçerik:**
*   Potansiyel Kazanç (TL/USD).
*   Hesaplama Mantığı (Matematiksel formül).
*   Kırılım: Günlük Sipariş x Hata Oranı x Sepet Ortalaması.

---

## 2. Tasarım Sistemi (Design System)

Proje **"WapCRM Docs Flat Design"** sistemini takip eder.

### Renk Paleti (Token Bazlı)
| Token | Kullanım | Hex |
| :--- | :--- | :--- |
| `brand-500` | Ana Eylem / Vurgu | `#6366f1` (Indigo) |
| `surface` | Kart Arkaplanları | `#ffffff` |
| `bg-gray-50` | Sayfa & Sohbet Arkaplanı | `#f9fafb` |
| `t-primary` | Ana Metin | `#111827` |
| `t-secondary` | Açıklama Metni | `#6b7280` |

### Tipografi
*   **Genel:** `Outfit` (Modern, Sans-serif).
*   **Kod/Teknik:** `JetBrains Mono` (Badge, JSON, Steps).
*   **Boyutlar:** "Airy" (Havadar) prensibi.
    *   `text-base`: Standart metin (16px+).
    *   `text-lg`: Vurgulu açıklamalar.
    *   `text-4xl/5xl`: Sayfa başlıkları.

### Bileşen Kuralları

#### FlatCard
*   Gölge yok veya çok hafif (`shadow-sm`).
*   Kenarlık: `border border-brand-100`.
*   Padding: Geniş (`p-8`).

#### Step (Numaralı Liste)
*   Dikey çizgi ile birbirine bağlı.
*   Numara: Daire içinde, `font-mono`.
*   Başlık yanında `Hedef` etiketi.

#### Chat Interface
*   **Arkaplan:** `bg-gray-50` (Düz renk, desen yok).
*   **Kullanıcı:** `bg-[#d9fdd3]` (WhatsApp Yeşili).
*   **Bot:** Beyaz.
*   **Sistem Logları:** Sohbetin ortasında, küçük kutucuklar (AI Analizi, API Çağrısı).

---

## 3. Kodlama Standartları

1.  **Atomik Bileşenler:** Sayfa içinde `Callout`, `Badge`, `Step` gibi bileşenler yerel (Local) veya `components/` altında tanımlı olmalı.
2.  **Tailwind Config:** Renkler ve fontlar `tailwind.config.js` üzerinden `brand-*` ve `font-*` olarak çekilmeli. Hardcoded hex yasak.
3.  **Lucide React:** İkon seti olarak sadece `lucide-react` kullanılmalı.

---

*Bu doküman, tüm senaryo sayfalarında tutarlılığı (Consistency) sağlamak amacıyla oluşturulmuştur.*
