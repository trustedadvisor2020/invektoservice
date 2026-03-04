# Takim ve Rol Gecis Plani

> Son guncelleme: 2 Mart 2026
> Mevcut takim: 5 kisi + Q (toplam 6)
> Hedef: Q dev'den CEO'ya gecer, AI maksimum yuku tasir

---

## 1. Mevcut Takim

| Kisi | Mevcut Rol | Hedef Rol |
|------|-----------|-----------|
| **Q (Taner)** | Dev + CEO (karisik) | CEO / Product Owner |
| **Dev (gelecek)** | Henuz yok | Full-stack developer |
| **Teknik Destek 1** | Teknik destek | Teknik Destek + Dokumantasyon |
| **Teknik Destek 2** | Teknik destek | Teknik Destek + QA |
| **Satis/Marketing 1** | Satis | Satis + Musteri Basari |
| **Satis/Marketing 2** | Marketing/Fatura | Marketing + Icerik + Faturalama |

---

## 2. Sorumluluk Matrisi (RACI)

| Gorev | Q | Dev | T.Destek 1 | T.Destek 2 | Satis 1 | Satis 2 | AI |
|-------|---|-----|-----------|-----------|---------|---------|-----|
| **Mimari kararlar** | R | C | - | - | - | - | C |
| **Feature gelistirme** | A→R (gecis) | R (hedef) | - | - | - | - | C |
| **Bug fix** | A→R (gecis) | R (hedef) | I | I | - | - | C |
| **Code review** | R | - | - | - | - | - | C |
| **Deploy** | A→R (gecis) | R (hedef) | - | - | - | - | - |
| **Musteri onboarding** | A | - | R | R | C | - | C |
| **Musteri destek (1. seviye)** | - | - | R | R | - | - | R |
| **Musteri destek (2. seviye)** | C | R | R | - | - | - | - |
| **Dokumantasyon yazimi** | A | - | R | C | - | - | R |
| **Blog yazimi** | - | - | - | - | C | R | R |
| **SEO / icerik** | - | - | - | - | - | R | R |
| **Sosyal medya** | - | - | - | - | - | R | R |
| **Satis / demo** | I | - | C | - | R | C | - |
| **Faturalama** | I | - | - | - | - | R | C |
| **Musteri feedback toplama** | I | - | C | C | R | - | - |
| **Product roadmap** | R | C | I | I | C | C | C |
| **Test yazimi** | A→R (gecis) | R (hedef) | - | C | - | - | R |

**R** = Responsible (yapan), **A** = Accountable (sorumlu), **C** = Consulted, **I** = Informed

---

## 3. Q'nun Dev → CEO Gecis Plani

### Faz 1-2 (Hafta 1-6): Q Hala Dev

```
Q'nun zamani:
  ├── %60 Development (permission, billing, onboarding)
  ├── %20 Product kararlar (plan tierlari, fiyatlandirma)
  ├── %10 Code review + mimari
  └── %10 Takim yonetimi
```

### Faz 3 (Hafta 7-8): Gecis Baslangici

```
Q'nun zamani:
  ├── %40 Development (onboarding, son polish)
  ├── %20 Yeni dev'e devir (pair programming, dokumantasyon)
  ├── %20 Product + musteri
  └── %20 Takim yonetimi
```

### Faz 4+ (Hafta 9+): Q = CEO

```
Q'nun zamani:
  ├── %10 Development (kritik bug'lar, mimari)
  ├── %30 Product (roadmap, feature kararlar)
  ├── %30 Musteri + satis (demo, feedback)
  ├── %20 Takim yonetimi
  └── %10 Code review
```

### Dev Devir Checklist

Yeni developer geldiginde Q'nun devredecegi seyler:

- [ ] InvektoServices codebase walkthrough (arch/ klasoru + CLAUDE.md)
- [ ] Deploy sureci (MCP, PM2, NSSM)
- [ ] Test calistirma (dotnet test, simulator, UI tester)
- [ ] Git workflow (branch, commit, push)
- [ ] Mikroservis mimarisi ve port haritasi
- [ ] INMA entegrasyonu (auth flow)
- [ ] Claude Code kullanimi (auto workflow, skill'ler)
- [ ] PostgreSQL erisim ve migration sureci

**Hedef:** Yeni dev 2 hafta icinde bagimsiz calisabilir hale gelir.

---

## 4. AI'nin Rolu (7. Takim Uyesi)

AI, takim icinde gorunmez ama en uretken uye:

| Alan | AI Ne Yapar | Insan Ne Yapar |
|------|-------------|---------------|
| **Kod yazma** | Feature kodu, test kodu, migration SQL | Review + merge |
| **Dokumantasyon** | MDX makale yazar, SEO optimize eder | Review + ekran goruntuleri |
| **Blog** | Makale yazar, anahtar kelime arastirir | Son okuma + yayinlama |
| **Musteri destek (1. seviye)** | WebChat AI asistan + Knowledge base | Escalation'lara insan bakar |
| **Email** | Kampanya icerigi yazar | Gonderme karari + liste yonetimi |
| **Sosyal medya** | Post icerigi uretir | Paylasma + yorum yonetimi |
| **Analiz** | Data analiz, rapor olusturma | Karar verme |
| **Test** | Test kodu yazar, senaryo tasarlar | Calistirma + debug |

### AI Kazanci Tahmini

| Gorev | AI olmadan (saat/hafta) | AI ile (saat/hafta) | Tasarruf |
|-------|------------------------|---------------------|----------|
| Feature development | 40 saat | 15 saat | %62 |
| Dokumantasyon (4 makale) | 16 saat | 4 saat (review) | %75 |
| Blog (2 makale) | 8 saat | 2 saat (review) | %75 |
| Test yazimi | 10 saat | 3 saat (review) | %70 |
| Email kampanya | 4 saat | 1 saat | %75 |
| Sosyal medya | 6 saat | 1.5 saat | %75 |
| **TOPLAM** | **84 saat** | **26.5 saat** | **%68** |

> Bu 6 kisilik takimin 15 kisilik bir takima esit uretkenlikte olmasini saglar.

---

## 5. Haftalik Ritim

### Pazartesi: Planlama
- Q: Haftalik oncelikleri belirle, takim toplantisi (30 dk)
- Herkes: Haftanin gorevlerini al

### Sali-Persembe: Uretim
- Dev/Q: Kod yazma (AI ile)
- Teknik Destek: Musteri destek + haftada 2 dokumantasyon makalesi
- Satis: Demo + musteri iletisimi + haftada 1 blog review

### Cuma: Review + Deploy
- Q: Code review, deploy (gerekiyorsa)
- Teknik Destek: Haftalik destek raporu
- Satis: Haftalik satis raporu
- Herkes: "Gelecek hafta ne yapacagiz?" (15 dk)

### Iletisim Araclari
- **Gunluk:** WhatsApp grubu (hizli iletisim)
- **Haftalik:** 30 dk toplanti (Pazartesi)
- **Gorev takibi:** Basit Trello/Notion board
- **Musteri feedback:** WhatsApp + email → Trello'ya not

---

## 6. Ise Alim Oncelikleri

| Oncelik | Rol | Ne Zaman | Neden |
|---------|-----|----------|-------|
| **P0** | Full-stack Developer | Faz 2-3 (Hafta 4-7) | Q'nun dev yukunu devralacak |
| **P1** | - | - | Mevcut takim yeterli (AI ile) |
| **P2** | Ek Teknik Destek | 30+ musteri olunca | Destek yuku artinca |
| **P3** | Product Manager | 50+ musteri olunca | Q'nun urun yukunu hafifletir |

### Developer Ise Alim Kriterleri

| Kriter | Onem |
|--------|------|
| .NET 8 / C# deneyimi | Zorunlu |
| React + TypeScript | Zorunlu |
| PostgreSQL | Zorunlu |
| Claude Code / AI-assisted development | Tercih |
| SaaS / multi-tenant deneyimi | Tercih |
| Turkce iletisim | Zorunlu |

**Not:** AI-assisted development bilmesi buyuk avantaj. Claude Code ile calismaya alisik biri, 2x daha verimli olur.

---

## 7. Risk ve Azaltma

| Risk | Olasilik | Etki | Azaltma |
|------|----------|------|---------|
| Q hasta olursa / musait degilse | Orta | Yuksek | CLAUDE.md + arch/ dosyalari ile dev bagimsiz calisabilir |
| Yeni dev bulunamazsa | Orta | Yuksek | Q dev'e devam eder, CEO rolu yavaslar. AI yuku artirir |
| Teknik destek ekibi yetmezse | Dusuk | Orta | AI chatbot 1. seviye destegi ustlenir |
| Musteri 0 kalirsa (satis basarisiz) | Orta | Yuksek | Ilk 5 firma icin Q bizzat demo yapar |
| AI araclar bozulursa (API down) | Dusuk | Orta | Manuel calisma moduna gec, kritik degil |
