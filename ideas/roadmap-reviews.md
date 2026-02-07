# Invekto — Uzman Review'ları (Roadmap v3 Üzerine)

> Ana dosya: [roadmap.md](roadmap.md)
> Bu dosya: 3 uzman perspektifinden review + aksiyonlar

**Genel hüküm:** Bu roadmap mühendislik olarak güçlü, senaryo olarak zengin —
ama positioning bulanık, SaaS metrikleri eksik, ürün akışı tanımsız.
Şu haliyle: **iç ekip için harita, pazar için ham.**

---

## 1. April Dunford (Positioning Gözüyle)

**Ne doğru:**
- Niche seçimi net: Trendyol/HB satıcıları → sonra sağlık
- Avatar (Mehmet / Dr. Ayşe) iyi yazılmış
- ROI hesapları somut

**Kritik problem — Positioning bulanık:**

Roadmap'te Invekto aynı anda 4 kategori:

| # | Kategori | Nerede geçiyor |
|---|----------|----------------|
| 1 | WhatsApp CRM | Product Story |
| 2 | AI Agent platformu | Phase 3 AgentAI |
| 3 | Revenue OS | Phase 5 Revenue Agent |
| 4 | E-ticaret otomasyon aracı | Trendyol entegrasyonları |

**"Sen kategori yaratmıyorsun, 4 kategoriye aynı anda girmeye çalışıyorsun."**

Dunford'un zorlayacağı tek doğru konum:

> *"Trendyol ve Hepsiburada satıcıları için WhatsApp üzerinden sipariş sonrası yükü otomatik kapatan AI."*

Hepsi bu. Revenue Agent, sağlık, outbound = sonra.

**Aksiyon durumu:** ✅ Positioning bölümü eklendi ([roadmap.md](roadmap.md) başına). Landing page'e yazılacak tek cümle sabitlendi.

---

## 2. Jason Lemkin (SaaS Ölçek Gözüyle)

**Ne doğru:**
- Phase 0'da satışa çıkma fikri çok iyi
- Revenue-first yaklaşım mantıklı
- Phase bazlı genişleme gerçekçi

**Kritik problem 1 — Auth Phase 4 = çok geç:**

```
Kurumsal müşteri:
  → SSO sorar         ← Phase 4'te
  → Audit sorar       ← Phase 4'te
  → KVKK sorar        ← Phase 4'te

Bunlar olmadan pipeline bile açılmaz.

"Kurumsal gelince yaparız" = "Kurumsal gelmeyecek çünkü bunlar yok."
```

> **Çözüm:** Phase 3 sonunda kurumsal talep sayısını ölç. ≥3 "SSO var mı?" sorusu → Auth'u Phase 3.5'e çek.

**Kritik problem 2 — Core retention metric yok:**

MRR yazıyor ama:
- Net logo churn hedefi yok
- Activation tanımı yok
- "Customer is live" ne demek belli değil

**Aksiyon durumu:** ✅ Core SaaS Metrics kutusu eklendi ([roadmap-phases.md](roadmap-phases.md) Phase 1-2 arasına).
5 zorunlu metrik tanımlandı: TTFAR, Weekly Deflection %, 30-Day Logo Retention, Activation tanımı, Net Logo Churn.
Auth zamanlama uyarısı eklendi.

---

## 3. Lenny Rachitsky (Product Gözüyle)

**Ne doğru:**
- Senaryolar (S1–S10) çok güçlü
- Pain → feature eşleşmesi iyi
- Mehmet avatarı gerçek

**Kritik problem — Feature dolu, user journey yok:**

```
Roadmap'te cevabı olmayan sorular:
  → İlk kullanıcı 1. gün ne yapıyor?
  → İlk değer anı (aha moment) neresi?
  → 7 gün sonra ne görüyor?
  → 30 gün sonra neden kalıyor?

Her şey backend, phase, servis.
Kullanıcı akışı = YOK.
```

Lenny'nin zorlayacağı minimum flow:

```
1. Connect Trendyol
2. Seç: "Kargo Soruları"
3. Turn ON
4. İlk otomatik cevap
5. Dashboard'da: "% saved time"
```

Bu akış tanımlı değilse ürün öğrenmez.

**Aksiyon durumu:** ✅ User First-Value Flow eklendi ([roadmap-phases.md](roadmap-phases.md) Phase 1 sonuna).
Day 1 → Day 7 → Day 30 akışı tanımlandı.
Aha moment: "Gerçekten otomatik cevapladı!" olarak sabitlendi.

---

## Özet: 3 Uzmanın Verdikleri

| Uzman | Teşhis | Aksiyon | Durum |
|-------|--------|---------|-------|
| **Dunford** | Positioning bulanık (4 kategori) | Tek cümle positioning eklendi | ✅ |
| **Lemkin** | SaaS metrikleri eksik, Auth geç | Core metrics + Auth uyarısı eklendi | ✅ |
| **Lenny** | User journey tanımsız | First-Value Flow eklendi | ✅ |

**Genel eylem planı:**
1. ~~Positioning düzelt~~ → ✅ Tek cümle sabitlendi
2. ~~SaaS metrikleri ekle~~ → ✅ 5 zorunlu metrik tanımlandı
3. ~~User flow tanımla~~ → ✅ Day 1-7-30 akışı eklendi
4. **Auth zamanlamasını izle** → Phase 3'te kurumsal talep ≥3 ise hızlandır
5. **Landing page yazıldığında** → Dunford positioning'i test et, başka bir şey ekleme
