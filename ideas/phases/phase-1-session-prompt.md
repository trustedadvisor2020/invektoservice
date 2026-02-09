# Phase 1 Session Prompt

> Bu dosyayı yeni bir Claude Code session'ına yapıştır. Auto workflow otomatik başlayacaktır.
> **Kullanım:** Aşağıdaki `---PROMPT BAŞLANGIÇ---` ile `---PROMPT BİTİŞ---` arasını kopyala.

---PROMPT BAŞLANGIÇ---

## Phase 1: Core Otomasyon — Başlangıç

Phase 1'e başlıyoruz. Bu phase'in amacı mevcut 50+ müşteriye otomasyon, AI ve broadcast kazandırmak.

### Mevcut Durum

- **Çalışan servisler:** `Invekto.Backend` (:5000) + `Invekto.ChatAnalysis` (:7101) + `Invekto.Shared`
- **Tech stack:** .NET 8 Minimal API, Windows Service
- **Ana Invekto uygulaması:** .NET/Angular/SQL Server — 50+ müşteri, 7 kanal, CRM, routing, VOIP
- **Bu repo (InvektoServis):** Ana uygulamaya AI/otomasyon eklenti katmanı
- **Stage-0 tamamlandı:** Gateway, health endpoints, chat analysis (Claude Haiku), JSON logging

### Phase 1 Scope (9 alt gereksinim)

Phase 1'de 3 yeni mikro servis doğacak:

| Servis | Port | Sorumluluk |
|--------|------|------------|
| `Invekto.Automation` | 7108 | Chatbot, flow engine, trigger sistemi |
| `Invekto.AgentAI` | 7105 | Agent Assist, intent detection, reply suggestion |
| `Invekto.Outbound` | 7107 | Broadcast, toplu mesaj, zamanlama |

**Gereksinimler (öncelik sırasına göre):**

1. **GR-1.9: Invekto ↔ InvektoServis Entegrasyonu** — İki platform arası API köprüsü, auth token validation, tenant sync
2. **GR-1.1: Chatbot / Flow Builder** — Automation servisi, menü chatbot, FAQ, mesai dışı otocevap, human handoff
3. **GR-1.2: AI Agent Assist** — AgentAI servisi, suggested reply, intent detection, otomatik etiketleme
4. **GR-1.3: Broadcast / Toplu Mesaj + Trigger** — Outbound servisi, segment bazlı gönderim, rate limiting, opt-out
5. **GR-1.4: Otomasyon Dashboard** — React dashboard genişlemesi, deflection rate, trend
6. **GR-1.5: Diş Kliniği Pipeline** — Fiyat sorusu intent + otocevap (ChatAnalysis genişleme)
7. **GR-1.6: Basit Randevu Motoru** — Slot tanımı, randevu, T-48h/T-2h hatırlatma
8. **GR-1.7: Estetik Lead Pipeline** — Lead tracking, follow-up hatırlatma
9. **GR-1.8: KVKK Minimum Koruma** — Disclaimer, opt-in, veri minimizasyonu

### Referans Dosyalar

- Detaylı gereksinimler: `ideas/phases/phase-1.md`
- Platform dağılımı: `ideas/phases/platform-split.md`
- Ana roadmap: `ideas/roadmap.md`
- Mevcut mimari: `arch/session-memory.md`, `arch/active-work.md`
- Lessons learned: `arch/lessons-learned.md`
- Kontratlar: `arch/contracts/`
- Error codes: `arch/errors.md`

### DB Stratejisi

- Yeni servisler PostgreSQL kullanacak (ana app SQL Server)
- Phase 1'de tek PostgreSQL instance yeterli
- tenant_id eşleştirme KRİTİK (SQL Server ↔ PostgreSQL)

### Kısıtlar

- Solo founder — gerçekçi süre 10-15 hafta
- Mevcut müşterilerin akışını BOZMA
- WhatsApp Business API kurallarına %100 uyum (broadcast)
- AI sağlık tavsiyesi VERMEZ (sağlık niche'inde disclaimer zorunlu)

### Risk: HIGH

Yeni mikro servisler + veritabanı + entegrasyon = multi-file, multi-service değişiklik.

### İlk Adım

GR-1.9 (Invekto ↔ InvektoServis Entegrasyonu) ile başlayalım. Çünkü diğer tüm özellikler bu köprüye bağlı.

Interview ile başla — gri noktaları çözelim.

---PROMPT BİTİŞ---

---

## Notlar

### Bu prompt ne tetikler?

1. Auto workflow otomatik aktif olur (CLAUDE.md + INVEKTO_BASE.prompt.md)
2. Session bootstrap: session-memory, active-work, lessons-learned okunur
3. Interview başlar (AskUserQuestion ile)
4. Interview'dan sonra plan JSON oluşturulur → Q onayı → dev → build → /rev → codex → commit

### Her GR tamamlandıkça

1. `ideas/phases/phase-1.md` dosyasındaki durum takibi tablosunu güncelle
2. `arch/active-work.md` dosyasını güncelle
3. `arch/session-memory.md` dosyasını güncelle
4. Yeni session'a geçerken bir sonraki GR için prompt'u güncelle

### Sonraki GR için prompt güncelleme

Her GR tamamlandığında, yukarıdaki prompt'taki "İlk Adım" bölümünü bir sonraki GR ile değiştir:

| Tamamlanan | Sonraki | İlk Adım Güncelleme |
|------------|---------|---------------------|
| GR-1.9 | GR-1.1 | "GR-1.1 (Chatbot / Flow Builder) ile devam edelim. Automation servisi oluşturulacak." |
| GR-1.1 | GR-1.2 | "GR-1.2 (AI Agent Assist) ile devam edelim. AgentAI servisi oluşturulacak." |
| GR-1.2 | GR-1.3 | "GR-1.3 (Broadcast / Toplu Mesaj) ile devam edelim. Outbound servisi oluşturulacak." |
| GR-1.3 | GR-1.4 | "GR-1.4 (Otomasyon Dashboard) ile devam edelim. React dashboard genişletilecek." |
| GR-1.4 | GR-1.5 | "GR-1.5 (Diş Kliniği Pipeline) ile devam edelim. Fiyat sorusu intent'i eklenecek." |
| GR-1.5 | GR-1.6 | "GR-1.6 (Basit Randevu Motoru) ile devam edelim." |
| GR-1.6 | GR-1.7 | "GR-1.7 (Estetik Lead Pipeline) ile devam edelim." |
| GR-1.7 | GR-1.8 | "GR-1.8 (KVKK Minimum Koruma) ile devam edelim." |
| GR-1.8 | — | Phase 1 tamamlandı! Çıkış kriterlerini kontrol et. |
