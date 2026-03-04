# Q Operasyonel Checklist

> Her faz bittiginde Q'nun sunucuda/DB'de yapmasi gereken isler.
> DevAgent kod yazar, Q deploy eder. Bu dosya Q'nun bekleyen islerini takip eder.
> Q onayladikca checkbox'lar isaretlenir.
> **Referans:** `OPS-{numara}` ile kisa referans ver (ornek: "OPS-3 tamam").

---

## Tamamlanan Isler

### GR-1.9 Integration Bridge (2026-02-08)
- [x] PostgreSQL kur (invekto DB, pgAdmin)
- [x] tenant-registry.sql calistir
- [x] JWT claims dogrula (Main App token yapisi)
- [x] Staging deploy testi (FTPES + health OK)
- [x] appsettings.Production.json doldur (Backend + ChatAnalysis)
- [x] Windows Service kurulumu (NSSM, auto-start, auto-restart)

### GR-1.1 Automation Service (2026-02-09)
- [x] automation.sql calistir (PostgreSQL)
- [x] automation.sql migration (chatbot_flows multi-flow PK degisikligi)
- [x] tenant_registry flow_builder_api_key ekle

### GR-1.2 AgentAI Service (2026-02-11)
- [x] agentai.sql calistir (PostgreSQL)
- [x] AgentAI appsettings.Production.json doldur
- [x] AgentAI deploy + NSSM servis kurulumu (InvektoAgentAI SERVICE_RUNNING)

### GR-1.3 Outbound Service (2026-02-12)
- [x] outbound.sql calistir (PostgreSQL)
- [x] Outbound appsettings.Production.json doldur
- [x] Outbound deploy + NSSM servis kurulumu

---

## Bekleyen Isler

### Knowledge Service (GR-2.1 Phase A+B) -- Commit: 385d3e0, 89bbe72

| # | Is | Detay | Durum |
|---|----|-------|-------|
| OPS-1 | `knowledge.sql` calistir | PostgreSQL -- `arch/db/knowledge.sql` | [ ] |
| OPS-2 | pgvector extension kur | `CREATE EXTENSION IF NOT EXISTS vector;` | [ ] |
| OPS-3 | Production config doldur | `appsettings.Production.Knowledge.json`: Jwt:SecretKey + PG password + OpenAI:ApiKey | [ ] |
| OPS-4 | Knowledge deploy + NSSM | InvektoKnowledge, port 7104, `C:\Invekto\Knowledge\current\` | [ ] |
| OPS-5 | E2E test calistir | `test-knowledge.bat` (sunucuda) | [ ] |
| OPS-6 | Firewall rule | port 7104 localhost-only (`firewall-rules.bat`'ta mevcut) | [ ] |

### WhatsApp Analytics (WA-5/6 Phase A) -- Commit: 18f387f

| # | Is | Detay | Durum |
|---|----|-------|-------|
| OPS-7 | `whatsapp-analytics.sql` calistir | PostgreSQL -- `arch/db/whatsapp-analytics.sql` | [ ] |
| OPS-8 | Production config olustur | `appsettings.Production.WhatsAppAnalytics.json`: Jwt:SecretKey + PG password + Storage:BasePath | [ ] |
| OPS-9 | WA Analytics deploy + NSSM | InvektoWhatsAppAnalytics, port 7109, `C:\Invekto\WhatsAppAnalytics\current\` | [ ] |
| OPS-10 | Firewall rule | port 7109 localhost-only | [ ] |
| OPS-11 | Upload dizini olustur | `C:\Invekto\WhatsAppAnalytics\uploads\` | [ ] |

---

## Notlar

- **Deploy script:** `dev-to-invekto-services.bat` tum servisleri FTPES ile gonderir
- **Sunucu path:** `C:\Invekto\Knowledge\current\`, `C:\Invekto\WhatsAppAnalytics\current\`
- **NSSM:** NSSM binary sunucudaki `C:\Invekto\` altinda
- **Restart:** `arch/deploy/restart-services.bat` tum servisleri yeniden baslatir
- **SQL dosyalari:** `arch/db/` altinda her servisin schema'si var
- **Upload path:** `C:\Invekto\WhatsAppAnalytics\uploads\`
- **Referans kullanimi:** "OPS-3 tamam" veya "OPS-7,8 yaptim" seklinde kisa bildir
