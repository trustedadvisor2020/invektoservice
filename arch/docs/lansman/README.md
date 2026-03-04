# INVEKTO — SaaS Lansman Plani

> Olusturma: 2 Mart 2026
> Durum: Faz 0 — Karar & Hazirlik

---

## Dosya Indeksi

| # | Dosya | Icerik |
|---|-------|--------|
| 1 | [01-EKOSISTEM-RAPORU.md](01-EKOSISTEM-RAPORU.md) | Mevcut durum, mimari, auth akisi, guclu/zayif yanlar |
| 2 | [02-LANSMAN-ROADMAP.md](02-LANSMAN-ROADMAP.md) | 5 fazli lansman yol haritasi (10 hafta) |
| 3 | [03-BILLING-VE-PERMISSION-PLANI.md](03-BILLING-VE-PERMISSION-PLANI.md) | QNB SanalPos, izin sistemi, kota, fatura |
| 4 | [04-ONBOARDING-PLANI.md](04-ONBOARDING-PLANI.md) | INMA'dan gelen firma icin wizard akisi |
| 5 | [05-DOKUMANTASYON-PLANI.md](05-DOKUMANTASYON-PLANI.md) | AI-driven icerik takvimi, 6 haftada %40→%85 |
| 6 | [06-TEST-PLANI.md](06-TEST-PLANI.md) | Pragmatik test stratejisi, risk bazli |
| 7 | [07-TAKIM-VE-ROL-PLANI.md](07-TAKIM-VE-ROL-PLANI.md) | 6 kisi + AI, Q'nun CEO gecisi, RACI matrisi |

---

## Ozet

**4 proje** (InvektoServices, InvektoWebsite, InvektoChat, InvektoHelp) ile SaaS lansmanı.

**Backend %95 hazir.** Eksikler belirli:
1. Permission enforcement (Faz 1, 2 hafta)
2. Billing — QNB SanalPos (Faz 2, 3 hafta)
3. Onboarding wizard (Faz 3, 2 hafta)
4. Dokumantasyon (paralel, 6 hafta)

**Hedef:** 10 hafta icinde ilk odeme yapan musteri.

**Prensip:** AI maksimum yuku tasir. Blog, dokumantasyon, kod, test, email, sosyal medya — hepsinde AI uretir, insan review eder.

---

## Hizli Baslangic

Faz 0'dan basla:
1. Bu dosyalari oku
2. Plan tierlarini onayla (02-LANSMAN-ROADMAP.md icinde)
3. QNB SanalPos basvurusunu baslat
4. Domain planini kesinlestir (super.invekto.com / crm.invekto.com)
5. Faz 1'e gec: permission middleware
