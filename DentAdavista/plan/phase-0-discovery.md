# Faz 0 — Discovery & Audit

**Süre:** 0.5 gün | **Bağımlılık:** yok

## Hedef
Pilot'a başlamadan önce Invekto'da hangi komponentin hazır, hangisinin eksik olduğunu netleştir. Gap varsa Faz 1-9'a ek task olarak ekle.

## Adımlar

### 0.1 Invekto Capability Audit
- [ ] WhatsApp connector (WAA servisi) production-ready mi? Mevcut tenant'larda canlı mı kontrol et
- [ ] ChatAnalysis AI agent EN dil desteği — `MockIntentDetector` en-US culture test et
- [ ] Template modal EN şablon kaydı mevcut mu, yoksa ekle
- [ ] Flow builder'da "wait N hours/days" node'u var mı
- [ ] Scheduled reminder scheduler çalışıyor mu (cron / background job)
- [ ] File upload (X-ray image) WA üzerinden → lead record attachment var mı
- [ ] Custom field (per-tenant) sistemi var mı (teklif durumu, şehir seçimi için)

### 0.2 Müşteri Tarafı Bilgi Toplama
- [ ] Dent Adavista WhatsApp Business numarası (ya da Meta Business Manager access)
- [ ] Landing page URL + form alanları (field isimleri)
- [ ] Logo + marka renkleri (branding için)
- [ ] Güneş'in gerçek numarası mı, ortak clinic numarası mı
- [ ] Google Meet için hangi Google Workspace hesabı kullanılacak
- [ ] Fiyat listesi PDF/link (Güneş'in göndereceği)
- [ ] Instagram/FB URL'leri (sosyal proof için — Faz 3)

### 0.3 Deliverable
`DentAdavista/plan/phase-0-audit-report.md` — capability matrisi (YES/NO/PARTIAL) + gap list.

## Çıkış Kriteri
- Tüm capability'ler ✅ veya ⚠️ (gap + workaround) işaretli
- Müşteri bilgileri klasörde `customer-info.md` altında
- Gap'ler varsa ilgili faz dosyalarına task olarak eklendi

## Riskler
- **WAA production stability:** Son commit "WAA DI fix" — DI injection sorunu çözüldü mü doğrulanmalı
- **EN dil:** Intent detector TR baskın, EN için template coverage eksik olabilir (Faz 3'te ele alınacak)
