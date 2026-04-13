# Faz 7 — Google Meet Online Consultation

**Süre:** 0.5 gün | **Bağımlılık:** Faz 6

## Hedef
Randevu onaylanınca otomatik Google Meet link + calendar invite üret. Lead'e WA üzerinden gönder.

## Adımlar

### 7.1 Google Workspace OAuth Setup
- [ ] Müşteri Google Workspace hesabı (Faz 0'dan)
- [ ] Google Cloud Console → yeni OAuth 2.0 client (web app)
- [ ] Scopes: `calendar.events`, `calendar.readonly`
- [ ] Refresh token al, tenant config'e şifreli kaydet

### 7.2 Meet Link Generator Service
- [ ] Invekto Backend'de yeni küçük modül: `GoogleMeetService` (Shared via DI)
- [ ] Metod: `CreateMeeting(title, startTime, duration, attendees[]) → { meetLink, calendarEventId, icsFile }`
- [ ] Calendar event host: kliniğin Workspace kullanıcısı
- [ ] Attendee'ler: lead email (landing'den) + Dr. Özge + coordinator

### 7.3 Randevu Onay Akışı
Faz 6'dan `offer_status=accepted` gelince:
- [ ] `GoogleMeetService.CreateMeeting()` çağır
- [ ] `appointment_confirmed_en` HSM template gönder:
  - `{{1}}=name`, `{{2}}=city`, `{{3}}=slot_time`, `{{4}}=meet_link`
- [ ] ICS dosyası attachment olarak da gönder (iPhone/Android calendar ekle)

### 7.4 Reminder
- [ ] Randevudan 24 saat önce → WA reminder + Meet link tekrar
- [ ] Randevudan 1 saat önce → kısa reminder ("Your Meet starts in 1 hour")

### 7.5 Post-Meeting Hook
- [ ] Meet bittikten 30dk sonra → otomatik mesaj: "How was your consultation? Any follow-up questions?"
- [ ] Intent: `satisfied` / `needs_more_info` / `ready_to_book_treatment`

## Deliverable
- GoogleMeetService modülü (unit test ile)
- Appointment confirmation flow canlı
- Test: mock lead accept → Meet link 60sn içinde WA'da

## Çıkış Kriteri
End-to-end: offer accept → Meet link otomatik + calendar invite → lead telefonundan katılabiliyor.

## Riskler
- **OAuth refresh token expire:** 6 ayda bir Google'dan token yenileme uyarısı (monitoring alert ekle)
- **Timezone mismatch:** Event timezone Europe/Dublin olmalı, dentist Istanbul'dan katılacak (Google otomatik çözer)
- **Yüz yüze roadshow vs online:** Flowchart'ta "Online Consultation" ayrı branch — yüz yüze Dublin/Cork ile karışmasın. State ayrımı: `meeting_type = in_person | online`
