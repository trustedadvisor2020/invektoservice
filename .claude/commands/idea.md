# /idea - SaaS Fikir Danismanligi (Gemini)

Iteratif SaaS fikir danismanligi. Claude (Opus) + Google Gemini 3.1 Pro birlikte analiz yapar.

## Tetikleme

`/idea` veya kullanici bir fikir/ozellik teklifi yaptiginda.

## Workflow

### Faz 1: Fikri Anlama (Interview)

Q bir fikir verdiginde, ONCE AskUserQuestion ile detaylari topla. Asagidaki bilgileri toplamadan ASLA analize baslama:

**Zorunlu Sorular:**
1. **Hedef kullanici:** Bu ozellik kimin icin? (mevcut musteriler, yeni segment, internal?)
2. **Problem:** Hangi problemi cozuyor? Simdi nasil cozuluyor?
3. **Basari metrigi:** Bu basarili olursa neyi olcecegiz? (gelir, retention, activation, NPS?)
4. **Oncelik:** Bu ne kadar acil? (roadmap'te nerede?)

**Opsiyonel ama degerli:**
5. **Kisitlamalar:** Bilinen teknik/is kisitlamalari var mi?
6. **Rakipler:** Rakipler benzer bir sey yapiyor mu?
7. **Kapsam:** MVP mi yoksa full vision mi isteniyor?

Tum sorulari TEK bir AskUserQuestion ile sor (multiSelect degil, acik uclu). Q'nun cevaplarini bekle.

### Faz 2: Gemini Analiz

Q'nun cevaplari ile birlikte Gemini'yi cagir:

```
Tool: mcp__idea-consultant-gemini__idea_consult_gemini
Parameters:
  idea: [Q'nun fikri + interview cevaplari ile zenginlestirilmis]
  context: [InvektoServices konteksti + hedef servis + mevcut durum]
  focus_areas: [Q'nun cevaplarina gore 3-5 alan sec]
  iteration: 0
  depth: "standard" (default = medium thinking level)
  constraints: [Q'nun belirttigi kisitlamalar]
```

**Focus area secim rehberi:**
- Yeni ozellik -> feasibility, architecture, mvp_definition
- Gelir artisi -> monetization, market_fit, competitive_analysis
- Teknik iyilestirme -> architecture, scalability, implementation_plan
- UX degisikligi -> user_experience, feasibility, risk_assessment

### Faz 3: Sentez ve Sunum

Gemini'nin cevabini al ve Claude'un kendi analizini ekleyerek zenginlestirilmis formatta Q'ya sun:

**Sunum Formati:**

```markdown
## Fikir Analizi: [Fikir Basligi]

### Skor
| Metrik | Deger |
|--------|-------|
| Feasibility Score | X/10 |
| Complexity | [level] |

### Uzman Gorusleri
[Gemini + Claude sentezi - en degerli perspektifler]

### Onerilen Aksiyon Plani
| # | Aksiyon | Oncelik | Efor |
|---|---------|---------|------|
| 1 | ... | P0 | ... |
| 2 | ... | P1 | ... |

### Riskler
[Top 5 risk]

### Acik Sorular
[Cozulmesi gereken noktalar]
```

Sunumdan SONRA, AskUserQuestion ile Q'ya sor:

**Sorulacaklar (her iterasyonda):**
1. Hangi alanlar daha derinlestirilmeli?
2. Eksik veya yanlis bulunan noktalar var mi?
3. Devam mi, yoksa baska bir acidan bakmali miyiz?

### Faz 4: Iterasyon

Q geri bildirim verdikce, Gemini'yi tekrar cagir:

```
Tool: mcp__idea-consultant-gemini__idea_consult_gemini
Parameters:
  iteration: [artan sayi]
  previous_feedback: [Q'nun geri bildirimi]
  focus_areas: [guncellenmis odak alanlari]
  depth: [Q isterse "deep" yap, default "standard"]
```

Her iterasyonda:
1. Gemini'yi cagir
2. Sonuclari Claude ile sentezle
3. Q'ya sun + yeni sorular sor
4. Q "tamam" deyince veya aksiyon planina gececek kadar netlesince dur

### Faz 5: Final Cikti

Q hazir oldugunda, tum iterasyonlarin birlesik sentezini yap:

```markdown
## Final: [Fikir Basligi]

### Karar Ozeti
- Feasibility: X/10
- Onerilen yaklasim: [1-2 cumle]
- Tahmini efor: [sprint/gun bazli]
- Oncelik: P0/P1/P2/P3

### MVP Tanimı
[Minimum deger ureten kapsam]

### Uygulama Yol Haritasi
1. [Adim 1 - efor]
2. [Adim 2 - efor]
...

### Risk Azaltma Plani
[Top riskler + somut aksiyonlar]

### Basari Kriterleri
[Olculebilir metrikler]
```

## Onemli Kurallar

1. **Her iterasyonda AskUserQuestion kullan** - Q'yu yonlendirme noktasinda tut
2. **Somut ol** - "Yapilabilir" degil "su servis su endpoint ile 3 gunde yapilir"
3. **InvektoServices kontekstinde kal** - Genel SaaS tavsiyesi degil, bu platforma ozel
4. **Depth default = standard** (Gemini: medium thinking)
5. **Turkce** - Teknik terimler Ingilizce kalabilir
6. **Token maliyeti uyarisi** - Deep modda "gemini high = pahali, emin misin?" sor
7. **Soru sormaktan cekinme** - Ne kadar detayli ve cok soru o kadar iyi

## Hata Durumu

Eger Gemini hata verirse (API key, rate limit, model unavailable):
1. Hatayi Q'ya bildir
2. Claude kendi analiziyle devam et
3. Gemini tekrar kullanilabilir oldugunda bildir

## MCP Tool Referansi

### Google Gemini
- **Tool:** `mcp__idea-consultant-gemini__idea_consult_gemini`
- **Model:** gemini-3.1-pro-preview (1M context, 64K output)
- **Thinking:** low/medium/high (depth'e gore)
- **Maliyet:** Gemini AI Studio ucretsiz tier mevcut, paid tier icin pricing farkli

## Ornek Kullanim

```
Q: "Musterilerimize AI ile otomatik rapor olusturma ozelligi eklesek nasil olur?"

Claude: AskUserQuestion ->
  - Bu raporlar ne tur veriler icersin?
  - Hedef kullanici kim?
  - Raporlar ne siklikta olusturulacak?
  - Mevcut bir raporlama var mi?

Q cevaplar ->

Claude: Gemini MCP call ->
  mcp__idea-consultant-gemini__idea_consult_gemini

Claude: Sentez + yeni AskUserQuestion sor
  - Feasibility: 8/10
  - "Haftalik rapor MVP icin yeterli"
  - Hangi yaklasim daha mantikli?

[Iterate until Q says "tamam"]
```
