# PKT-2: Saglik Core

> **Durum:** DONE | **Tarih:** 2026-02-16 | **Codex:** iter 1, FORCE PASS
> **Commit:** e994e29

## GR Listesi

- **GR-2.4 Randevu Motoru:** Haftalik slot CRUD, booking/cancel, T-48h/T-2h hatirlama, Dashboard slot yonetimi
- **GR-2.6 KVKK Minimum:** AI disclaimer, acik riza, veri minimizasyonu, foto politikasi (5 servis)

## GR Detail

### GR-2.4: Randevu Motoru (Core)
- 2.4.1 Basit haftalık slot tanımı (gün + saat aralıkları)
- 2.4.2 Randevu al → WhatsApp teyit mesajı
- 2.4.3 T-48h / T-2h hatırlatma (Outbound Engine ile)
- 2.4.4 İptal → slot boşalt
- 2.4.5 Self-service slot tanımı (Dashboard)
- DB: appointment_slots, appointments

**Phase 3A'ya taşınan (GR-3.19):** Google Calendar sync, doktor bazlı slot, bekleme listesi, no-show prediction, fiyat editor

### GR-2.6: KVKK Minimum Koruma
- 2.6.1 Disclaimer: AI sağlık tavsiyesi vermez
- 2.6.2 Açık rıza: hasta onayı (opt-in mesajı)
- 2.6.3 Veri minimizasyonu: sadece isim, telefon, randevu
- 2.6.4 Erişim kontrolü: multi-tenant izolasyon
- 2.6.5 Fotoğraf politikası: hasta fotoğrafı yüklenmez (Phase 4'e kadar)

## Deliverables

- Yeni **Invekto.Appointments** servisi (port 7102)
- ReminderSchedulerService (IHostedService)
- KvkkHelper (Shared): 5 serviste KVKK uygulama
- 31 dosya +9006/-15
- DB: appointment_slots, appointments

## Plan

`arch/plans/20260215-pkt2-saglik-core.json`
