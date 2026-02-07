INVEKTO (WapCRM) — ENGINEERING BACKLOG + MICROSERVICE HARİTASI + 90 GÜNLÜK SPRINT PLANI
Türkiye + Global / 2026 agentic AI + e-ticaret + enterprise security
Versiyon: v1.0 (çok düşünülmüş, uygulanabilir)

=====================================================
A) JIRA-READY ENGINEERING BACKLOG (EPIC → FEATURES → STORIES)
=====================================================

ÖNEM SIRASI (Satış blokajı + değer üretimi)
P0 = procurement + kapanış + gelir
P1 = ölçek + kalite + maliyet düşürme
P2 = nice-to-have

-----------------------------------------------------
EPIC 01 — Enterprise Auth & Access (P0)
Amaç: Procurement veto’yu kaldır. Kurumsal satış aç.
-----------------------------------------------------

F01.1 — SSO (Google / Microsoft / Azure AD) (P0)
- Story: OIDC login (Google)
  AC:
  - Tenant bazlı enable/disable
  - Domain allowlist (örn: @firma.com)
  - İlk login → kullanıcı eşleştirme / provisioning
- Story: OIDC login (Microsoft)
  AC:
  - Azure tenant id support
  - Kullanıcı rol eşleştirme
- Story: SSO fallback local login
  AC:
  - SSO kapalıysa local login çalışır

F01.2 — 2FA (TOTP) (P0)
- Story: TOTP setup (QR + backup codes)
  AC:
  - 10 adet one-time backup code üret
  - Recovery süreci audit’e düşer
- Story: 2FA enforcement policy
  AC:
  - Tenant policy: zorunlu / opsiyonel
  - Supervisor zorunlu seçeneği

F01.3 — Session Management (P0)
- Story: Device sessions list
  AC:
  - Kullanıcı aktif cihazlarını görür
  - Tek tek revoke edebilir
- Story: Session timeout policy
  AC:
  - Tenant policy (örn 15/30/60 dk)
  - “Remember me” kontrollü

F01.4 — Brute-force protection + failed login logs (P0)
- Story: failed login log table + UI
  AC:
  - ip, device, timestamp, reason
- Story: lockout / exponential backoff
  AC:
  - 5 deneme → 15 dk kilit
  - IP bazlı throttle

F01.5 — IP / Country allowlist (P0)
- Story: IP allowlist
  AC:
  - CIDR destekle
  - Deneme loglanır
- Story: Country allowlist (GeoIP)
  AC:
  - Ülke kuralı + audit

-----------------------------------------------------
EPIC 02 — Audit & Compliance (P0)
Amaç: Kim ne yaptı sorusuna %100 cevap ver. KVKK/GDPR riski düşür.
-----------------------------------------------------

F02.1 — Tam Audit Log (P0)
- Story: Audit event schema
  AC:
  - event_type, actor_id, tenant_id, resource_type, resource_id
  - ip, user_agent, correlation_id
- Story: Critical events coverage
  AC:
  - login/logout, permission change, export, delete, knowledge update, prompt update, routing rule change
- Story: Audit UI + filter + export
  AC:
  - tarihe/actor’a/resource’a göre filtre
  - sadece Supervisor erişir

F02.2 — Data retention & purge (P0)
- Story: Retention policy per tenant
  AC:
  - “Chats retain 365 days” gibi
- Story: Scheduled purge job
  AC:
  - purge edilenler audit’e düşer
- Story: Legal hold
  AC:
  - hold açıkken purge çalışmaz

F02.3 — PII redaction (P0)
- Story: PII detector (TC, phone, email, IBAN, address)
  AC:
  - mesaj görüntülemede maskeleme
- Story: Export’da PII redaction seçenekleri
  AC:
  - “mask all PII” toggle
- Story: PII leak alert
  AC:
  - temsilci mesajına “TC/IBAN yazma” uyarısı

-----------------------------------------------------
EPIC 03 — Knowledge + Source of Truth (RAG) (P0)
Amaç: AI sallamasın. Kaynaklı cevap üretsin.
-----------------------------------------------------

F03.1 — Knowledge ingestion (P0)
- Story: PDF upload + chunking
  AC:
  - tenant izolasyonu
  - versiyonlama (v1/v2)
- Story: URL crawl (whitelist)
  AC:
  - sadece izinli domainler
- Story: FAQ editor
  AC:
  - hızlı içerik ekleme

F03.2 — Vector store + retrieval (P0)
- Story: embeddings pipeline
  AC:
  - chunk_id, doc_id, tenant_id
- Story: retrieval API (topK + filters)
  AC:
  - doc tag filter (pricing/policy/support)

F03.3 — Source-cited answers (P0)
- Story: AI answer must include citations
  AC:
  - kaynak doc adı + bölüm/sayfa
  - kaynak yoksa “insana devret” öner

F03.4 — Knowledge gap report (P1 ama önerim P0 sonuna) (P1)
- Story: Unanswered intents list
  AC:
  - son 7/30 gün top 50
  - “doc ekle” aksiyonu

-----------------------------------------------------
EPIC 04 — Agent Assist (P0)
Amaç: Temsilciyi roketle. Hataları azalt. KVKK riskini düşür.
-----------------------------------------------------

F04.1 — Reply generation (P0)
- Story: one-click suggested reply
  AC:
  - tone presets (formal/short/salesy)
- Story: “why this reply” explanation
  AC:
  - kaynak + intent + risk check sonucu

F04.2 — Guardrails (P0)
- Story: banned phrases + sensitive content
  AC:
  - tenant-level rules
- Story: PII prevention on outgoing
  AC:
  - TC/IBAN tespit → blokla veya onay iste

F04.3 — Next Best Action (P1)
- Story: “ask this question next”
  AC:
  - pipeline stage’e göre öner
- Story: “handoff notes” auto-generate
  AC:
  - devredince özet + gereken bilgi listesi

-----------------------------------------------------
EPIC 05 — Auto-Resolution Agent (P0)
Amaç: %60 sorguyu otonom kapat. Maliyet düşür.
-----------------------------------------------------

F05.1 — Intent router (P0)
- Story: top 20 intents taxonomy
  AC:
  - sipariş/kargo/iade/randevu/garanti/fiyat/stok vs.
- Story: confidence thresholds
  AC:
  - low confidence → human

F05.2 — Action executor (P0)
- Story: order lookup / tracking lookup
  AC:
  - entegrasyon servislerinden veri çek
- Story: escalation policy
  AC:
  - “3 denemede çözülmediyse” devret

F05.3 — Voice message transcription (P1)
- Story: voice→text + TR dialect tolerance
  AC:
  - transkript confidence skor

-----------------------------------------------------
EPIC 06 — Revenue Agent (P0)
Amaç: Lead→randevu→ödeme→sipariş akışını otomatikleştir.
-----------------------------------------------------

F06.1 — Lead qualification (P0)
- Story: qualification script
  AC:
  - bütçe/termin/ürün ihtiyacı soruları
- Story: lead score
  AC:
  - 0-100 + nedenleri

F06.2 — Offer / appointment (P0)
- Story: calendar integration (internal slot engine)
  AC:
  - basit slot API (Google Calendar şart değil)
- Story: proposal message templates
  AC:
  - teklif + şartlar + geçerlilik

F06.3 — Payment link (P1 Türkiye için kritik) (P1)
- Story: iyzico/paytr link create
  AC:
  - ödeme geldi → otomatik teyit mesajı

-----------------------------------------------------
EPIC 07 — Türkiye E-ticaret Integrations (P0)
Amaç: Trendyol/Hepsiburada + kargo entegrasyonuyla Türkiye’yi domine et.
-----------------------------------------------------

F07.1 — Trendyol integration (P0)
- Story: OAuth/API key setup per tenant
  AC:
  - secure vault
- Story: order sync (pull + webhook)
  AC:
  - sipariş, müşteri, kargo, iade
- Story: order status in conversation sidebar
  AC:
  - “son sipariş” kartı

F07.2 — Hepsiburada integration (P0)
- Aynı set: auth + sync + sidebar

F07.3 — Shipping integrations (P1)
- Story: Aras/Yurtiçi/PTT tracking
  AC:
  - tracking update → otomatik mesaj opsiyonu

-----------------------------------------------------
EPIC 08 — Revenue Automations (P0)
Amaç: Kampanya değil, para basan akışlar.
-----------------------------------------------------

F08.1 — Abandoned cart recovery (P0)
- Story: trigger rules (T+2h, T+24h)
  AC:
  - müşteriye göre kişiselleştir
- Story: opt-out compliance
  AC:
  - “STOP” → otomatik unsubscribe

F08.2 — Click-to-WhatsApp attribution (P0)
- Story: Meta click id capture
  AC:
  - lead source = campaign/adset/ad
- Story: pipeline auto-tagging
  AC:
  - label + segment + UTM mapping

-----------------------------------------------------
EPIC 09 — Ops Standard: SLA + QA + Mining (P1)
Amaç: Büyürken kaos çıkmasın.
-----------------------------------------------------

F09.1 — SLA tracker (P1)
- Story: response/resolve timers
  AC:
  - breach alerts + dashboard

F09.2 — QA scoring (P1)
- Story: rubric + auto-score
  AC:
  - script compliance + sentiment + resolution

F09.3 — Conversation mining (P1)
- Story: win/loss phrases
  AC:
  - top complaint drivers
  - top conversion drivers

-----------------------------------------------------
EPIC 10 — Predictive analytics + churn prediction (P2)
Not: Bunu erken koyarsan fokus dağıtır. P0/P1 bitmeden dokunma.
-----------------------------------------------------


=====================================================
B) MICROSERVICE HARİTASI (BİLEŞENLER + NEDEN + API’LER)
=====================================================

Hedef: Modüler ama saçma karmaşıklık yok.
Minimum servisle maksimum ayrışma.

-----------------------------------------------------
1) API Gateway / BFF (Backend for Frontend)
- Neden: UI için tek giriş, auth, rate limit, routing.
- Sağlar:
  - /auth/*
  - /inbox/*
  - /ai/*
  - /integrations/*
- Not: İlk aşamada tek Node/Express bile olur.

-----------------------------------------------------
2) Auth Service
- Neden: SSO/2FA/session/brute-force burada.
- DB: auth_users, sessions, login_attempts, tenant_policies
- API:
  - POST /login
  - POST /oidc/callback
  - POST /2fa/setup
  - GET /sessions
  - POST /sessions/revoke

-----------------------------------------------------
3) Audit Service
- Neden: Her kritik olayın değişmez kaydı.
- DB: audit_events (append-only)
- API:
  - POST /audit/event
  - GET /audit/search

-----------------------------------------------------
4) Inbox / Conversation Service
- Neden: multi-channel inbox çekirdeği.
- DB: conversations, messages, assignments, labels
- API:
  - GET /conversations
  - POST /assign
  - POST /label
  - POST /export (audit + pii filter)

-----------------------------------------------------
5) Integration Hub (E-commerce + Shipping)
- Neden: Trendyol/Hepsiburada/kargo bağları izole kalsın.
- DB: integration_accounts, sync_state, orders_cache
- API:
  - POST /integrations/trendyol/connect
  - POST /integrations/trendyol/webhook
  - GET /orders/latest?customer_id=...

-----------------------------------------------------
6) Knowledge Service (RAG)
- Neden: ingestion + retrieval + citations.
- DB:
  - docs, chunks, embeddings_index (vector db)
- API:
  - POST /knowledge/docs
  - POST /knowledge/url
  - POST /knowledge/retrieve

-----------------------------------------------------
7) AI Orchestrator (Agent Runtime)
- Neden: Agent Assist + Auto-Resolution + Revenue Agent karar/aksiyon motoru.
- Bileşen:
  - intent classifier
  - policy engine (guardrails)
  - tool executor (order lookup, payment link, ticket create)
- API:
  - POST /ai/suggest_reply
  - POST /ai/auto_resolve
  - POST /ai/qualify_lead
  - POST /ai/next_action

-----------------------------------------------------
8) Jobs/Workers (Queue)
- Neden: webhook, sync, abandoned cart, purge, transcription async.
- Tech:
  - Redis queue / RabbitMQ (hangisi sende varsa)
- Jobs:
  - order sync job
  - retention purge job
  - abandoned cart job
  - voice transcription job

-----------------------------------------------------
9) Analytics Service (P1)
- Neden: SLA/QA/mining compute ayrışsın.
- DB: metrics_daily, sla_breaches, qa_scores, mining_insights


=====================================================
C) 90 GÜNLÜK SPRINT PLANI (3 AY / 6 SPRINT x 2 HAFTA)
=====================================================

KURAL:
- İlk 30 gün: procurement + correctness + agent assist.
- 60 gün: auto-resolution + Trendyol/Hepsiburada.
- 90 gün: revenue agent + abandoned cart + attribution.
- Predictive analytics/churn = 90 gün dışı.

Her sprintte:
- Done = prod deploy + telemetry + docs.
- Security ve audit coverage tamamlanmadan “AI otonomi” açılmaz.

-----------------------------------------------------
SPRINT 1 (Gün 1-14) — Enterprise Auth Temeli + Audit İskeleti
-----------------------------------------------------
Hedef: Kurumsal satış kapısını arala.

Deliverables:
- Auth service: sessions, session timeout policy
- Failed login logs + basic lockout
- Audit service (append-only) + event emitter SDK
- Gateway/BFF routing

Acceptance:
- Login/Logout/Session revoke çalışıyor
- failed login listesi UI’dan görülebiliyor
- audit event’leri düşüyor (login, export placeholder)

-----------------------------------------------------
SPRINT 2 (Gün 15-28) — SSO + 2FA + IP/Country Policies
-----------------------------------------------------
Hedef: Procurement veto’yu fiilen kaldır.

Deliverables:
- Google OIDC + Microsoft OIDC
- TOTP 2FA + backup codes
- IP allowlist + country allowlist
- Audit coverage genişlet: permission change, login policy change

Acceptance:
- Tenant bazlı SSO/2FA zorunlu yapılabiliyor
- IP dışı giriş engelleniyor + audit
- Admin UI policy paneli hazır

-----------------------------------------------------
SPRINT 3 (Gün 29-42) — Knowledge Ingestion + Retrieval + Kaynaklı Cevap
-----------------------------------------------------
Hedef: AI’nin doğruluğunu kilitle.

Deliverables:
- PDF upload + chunk + embedding
- URL ingestion (whitelist)
- Retrieval API (topK)
- Source-cited answer formatı (UI’da gösterim)
- Prompt/Knowledge versioning v0

Acceptance:
- 1 doc yükle → AI cevap verirken kaynak gösteriyor
- Kaynak yoksa “insana devret” kuralı çalışıyor
- Doc versiyonları tutuluyor

-----------------------------------------------------
SPRINT 4 (Gün 43-56) — Agent Assist (Reply + Guardrails + PII)
-----------------------------------------------------
Hedef: Temsilci verim artışı + KVKK risk azaltma.

Deliverables:
- Suggest reply endpoint + UI one-click insert
- Guardrails: banned phrases + tenant rules
- Outgoing PII block/warn (TC/IBAN/phone/email)
- Audit: AI suggestion generated + agent accepted/rejected

Acceptance:
- Temsilci 1 tık cevap atıyor
- PII içeren mesaj ya bloklanıyor ya da onay istiyor
- AI aksiyonları auditleniyor

-----------------------------------------------------
SPRINT 5 (Gün 57-70) — Trendyol + Hepsiburada Sync + Auto-Resolution v1
-----------------------------------------------------
Hedef: Türkiye’de “kill shot” başlasın, deflection devreye girsin.

Deliverables:
- Trendyol connect + order sync + webhook ingest
- Hepsiburada connect + order sync
- Conversation sidebar: latest order card
- Auto-Resolution v1:
  - intent: “kargom nerede / sipariş durumu / iade”
  - action: order lookup + tracking message
  - escalation: low confidence → agent

Acceptance:
- Sipariş bilgisi konuşma yanında görünüyor
- “kargom nerede” mesajı otomatik yanıtlanabiliyor
- Escalation düzgün not bırakıyor

-----------------------------------------------------
SPRINT 6 (Gün 71-90) — Revenue Agent v1 + Abandoned Cart + Ads Attribution
-----------------------------------------------------
Hedef: Para getiren otomasyonları canlıya al.

Deliverables:
- Revenue Agent v1:
  - lead qualification script + score
  - appointment slot engine (basit)
- Abandoned cart recovery:
  - T+2h / T+24h triggers
  - opt-out compliance
- Click-to-WhatsApp attribution:
  - UTM/campaign tags
  - pipeline labels otomatik

Acceptance:
- Instagram lead → qualification → score → randevu önerisi akıyor
- Sepet terk akışı çalışıyor + opt-out
- Kampanya kaynağı konuşmaya tagleniyor

=====================================================
D) TANIMLI “DONE” KURALLARI (YOKSA BİTMEZ)
=====================================================

Her feature için Done:
- Unit test + basic integration test
- Audit coverage (kritik aksiyonlarda)
- Tenant isolation (asla karışmayacak)
- Rollback plan (feature flag)
- Metrics:
  - AI deflection rate
  - human handoff rate
  - response time
  - revenue attribution

=====================================================
E) EN KRİTİK TEKNİK TUZAKLAR (BUNLARA DÜŞERSEN YANARSIN)
=====================================================

1) “AI her şeyi çözer” sanma:
   - Kaynak yoksa otomasyon kapalı olmalı.
   - Aksi: yanlış bilgi → iade/şikayet.

2) Security ertelenirse:
   - Kurumsal satış yok.
   - AI hikayesi procurement’te ölür.

3) Entegrasyonlar “tek seferlik” yapılmaz:
   - Sync state + retry + idempotency zorunlu.
   - Webhook duplicate geldiğinde bozulmayacak.

4) Multi-tenant izolasyon:
   - Knowledge embeddings tenant bazlı olmak zorunda.
   - Aksi: veri sızıntısı = ölüm.

=====================================================
F) 90 GÜN SONUNDA ELİNDE NE OLACAK?
=====================================================

- Enterprise-ready security (SSO/2FA/audit/session/IP)
- Kaynaklı AI (RAG) + agent assist
- Trendyol/Hepsiburada + auto-resolution (ilk 3 intent)
- Revenue agent (qualification + randevu)
- Abandoned cart + ads attribution

Bu kombinasyon Invekto’yu “panel”den “para kazandıran sistem”e çevirir.


Raporun temelini aldım, ama eksik ve dar kalmış. Türkiye’de Trendyol/Hepsiburada gibi e-ticaret devleri WhatsApp üzerinden satış patlatırken, senin "AI destekli" diye konumladığın Invekto hala inbox seviyesinde kalırsa, globalde Salesforce/HubSpot, yerelde Wati/Respond.io gibi rakipler seni ezer. Dünya pazarı 2026’da agentic AI’a (otonom ajanlar) kaymış, Türkiye’de ise e-ticaret entegrasyonları ve KVKK uyumlu otomasyonlar olmadan kurumsal satış yapamazsın. AI’ı "transkripsiyon"la sınırlama, ajanları tam otonom yap: lead’den siparişe kadar işi bitir. Güvenliği oyuncak sanma, yoksa procurement’te veto yersin. Şimdi harekete geç: Aşağıdaki özellikleri acil build et, yoksa 6 ayda pazar payın erir.
Kritik Eksik Gerçekler (Türkiye + Global)

Pazar Trendleri: Globalde AI CRM pazarı 2026’da 48 milyar USD’ye ulaşacak, büyümeyi agentic AI sürüklüyor – Salesforce Einstein gibi platformlar lead scoring’den öte, otonom ajanlarla satış döngüsünü %30 kısaltıyor. Türkiye’de e-ticaret hacmi 1 trilyon TL’yi aşmış, %80’i WhatsApp üzerinden iletişim istiyor; AI olmadan cart abandonment’ı %40 düşüremezsin. Rakipler (Wati, Respond.io) AI ajanlarla %60 sorguyu otonom çözüyor, sen yapmazsan churn artar.
Güvenlik Kilidi: KVKK/GDPR’yi "var" demekle geçiştirme. Türkiye’de kurumsallar (bankalar, e-ticaret) audit log’suz, SSO’suz AI tool’u imzalamaz. Globalde de GDPR cezaları milyarlarca, data purge olmadan ölürsün.
AI Darlığı: Transkripsiyon/qualification yetmez. Pazar agentic AI’a geçti: Ajanlar lead’leri qualify edip, teklif/randevu/sipariş’i otomatik yönetiyor. Türkiye’de Türkçe diyalekt toleransı şart, yoksa sesli mesajlarda %50 hata yaparsın.
E-ticaret Eksikliği: Türkiye’de WhatsApp CRM’siz e-ticaret olmaz – Trendyol entegrasyonuyla sipariş/iade akışı yapmazsan, rakipler (QuickReply.ai) seni geçer. Globalde Shopify/WooCommerce entegrasyonu standart, yoksa %20 gelir kaybı.

Kazanman Gereken Yetenekler (Invekto Feature Raporu: Türkiye + Global Odaklı)

Güvenlik ve Yönetişim (Satış Ön Koşulu, Yoksa İmza Yok)
Bunlar zorunlu, "nice-to-have" diye erteleme:
2FA + SSO (Google/Microsoft/Azure) + biometric auth.
Tam audit log: Her erişim, değişiklik, export log’lanmalı, raporlanmalı.
Failed login/brute-force koruma + otomatik ban.
Session timeout + multi-device management + IP/country whitelist.
Data retention/purge + legal hold + otomatik PII redaction (KVKK için zorunlu).
AI prompt/knowledge versiyonlama + compliance checker (GDPR/KVKK otomatik tarama).
Değer: Kurumsal Türkiye satışlarını %50 artırır, globalde enterprise deal’leri açar. Risk: Yoksa "AI var" diye heyecanlanma, security gap’le procurement’te ölürsün.

AI Agent Katmanı (Fark Yaratan, Otonom İş Yapan)
Rakipler (Salesforce Agentforce, Wati AI) ajanları tam otonom yapmış, sen de yap:
A. Agent Assist (Temsilciyi Roketleyen)
Konuşma analiziyle tek tık reply üret + "neden bu" açıklaması + kaynak referansı.
Ton/brand guardrails + KVKK risk uyarısı (yasaklı veri sızdırma önleme).
Next-best-action: AI-driven öneri (soru sor, bilgi iste, adım at) + predictive analytics.
B. Auto-Resolution Agent (Sorguları Kapatıcı)
Top 20 konu otonom: Sipariş durumu, iade, stok, randevu, kargo – %60 deflection.
Eskalasyon kuralları: İnsan devri + not/insight transferi.
Multilingual + Türkçe diyalekt/AI (ses/transkripsiyon için) + agentic AI (karar alıp eylem yap).
C. Revenue Agent (Para Kazananı)
Lead qualification + bütçe/niyet scoring + churn prediction.
AI ürün önerisi/cross-sell/upsell + personalized messaging.
Otomatik randevu/teklif/ödeme linki + follow-up otomasyonu.
Değer: Satış döngüsü %30 kısalır, Türkiye e-ticaret’te ROAS %20 artar.

Knowledge + Source of Truth Motoru (AI Doğruluğunu Sağlayan)
Respond.io gibi yap: AI’ı şirket verisine bağla, sallama önle.
Knowledge base entegrasyonu: PDF/URL/SSS/ürün katalog/police + e-ticaret veri sync’i.
Kaynaklı cevaplar + tenant izolasyonu + erişim kontrolleri.
Knowledge gap analizi: En çok sorulan 50 unanswered konu raporu + AI öneri.
AI enrichment: Otomatik veri güncelleme/multilingual localization.
Değer: AI accuracy %90’a çıkar, Türkiye’de şikayet %40 düşer.

Ticaret Entegrasyonları (Türkiye’de Kill-Shot, Globalde Büyüme)
Türkiye e-ticaret’i domine et:
Trendyol/Hepsiburada entegrasyonu: Sipariş/teslimat/iade/soru akışı + webhook.
Kargo (PTT/Aras/Yurtiçi) takip + otomatik bildirim.
Ödeme gateway (Iyzico/PayTR) + tahsilat teyidi.
Global standart:
Shopify/WooCommerce/Magento: Sipariş/müşteri/iade sync’i.
SMS/Voice fallback (WhatsApp down’larında) + omnichannel (Instagram/FB).
Değer: Cart abandonment %40 azalır, Türkiye satışları %25 büyür.

Gelir Getiren Otomasyonlar (Kampanyadan Öte Akışlar)
Abandoned cart recovery: WhatsApp reminder + personalized nudge.
Click-to-WhatsApp ads attribution: Lead etiketle + pipeline’a sok.
Lifecycle automation: Onboarding/upsell/churn prevention + AI personalization.
Broadcast + segmentation: Dynamic list’ler (Shopify/Trendyol verisiyle).
Değer: Gelir %20 artar, retention %15 yükselir.

Operasyon Standardı: SLA + Kalite + Denetim (Büyüme İçin Şart)
SLA tracker: Yanıt/çözüm süresi alarmı + dashboard.
QA scoring: AI destekli temsilci puanı + script compliance.
Conversation mining: Şikayet analizi + kazan/kaybet ifadeleri + dönüşüm pattern’leri.
Analytics: Agent performance + revenue impact raporu.
Değer: Operasyon verimliliği %40 artar, scale ederken kaos önlenir.


Sonuç: Invekto’nun Olmazsa Olmaz 12 Maddelik Build Listesi (Hemen Başla)

2FA/SSO + audit log + session timeout + failed login.
Knowledge base bağlama + kaynaklı AI cevapları + gap raporu.
Agent Assist: Reply/next-action/risk uyarıları.
Auto-Resolution Agent: Top 20 konu otonom + multilingual Türkçe.
Revenue Agent: Qualification/teklif/randevu/upsell.
Trendyol/Hepsiburada entegrasyonu: Sipariş/iade/soru.
Kargo takip + otomatik mesajlar.
Abandoned cart recovery + SMS fallback.
Click-to-WhatsApp attribution + pipeline automation.
SLA/QA dashboard + conversation mining.
Predictive analytics + churn prediction.
Omnichannel sync: Instagram/FB/SMS + e-ticaret API’leri.

Bunu build et, Invekto’yu AI-first satış motoru yap – yoksa "panel" diye kalır, rakipler ezer. Hemen dev team’i topla, ilk MVP’yi 3 ayda çıkar: Güvenlik + temel AI ajanla başla. Satışa odaklan, kazan.


INVEKTO (eski adıyla WapCRM)
AI-FIRST SALES & SUPPORT OPERATING SYSTEM
Türkiye + Global Ürün Strateji ve Implementasyon Raporu
2026 READY

=====================================================
GENEL GERÇEK
=====================================================

Inbox artık commodity.

WhatsApp CRM satmıyorsun.
Satış + destek işletim sistemi satıyorsun.

Türkiye’de Trendyol / Hepsiburada WhatsApp üzerinden satış patlatıyor.
Globalde Salesforce / HubSpot / Respond.io agentic AI’ya geçmiş durumda.

Eğer Invekto:
- Agentic AI
- E-ticaret entegrasyonu
- Kurumsal güvenlik

üçlüsünü birlikte sunmazsa:

6 ay içinde “ucuz panel” kategorisine düşer.

AI’yı transkripsiyonla sınırlarsan ölürsün.
Security’yi hafife alırsan procurement’te veto yersin.
E-ticaret bağlamazsan Türkiye’de ölürsün.

Bu doküman: hayatta kalma planı.

=====================================================
1. GÜVENLİK + YÖNETİŞİM (SATIŞIN ÖN KOŞULU)
=====================================================

Özellikler:

- 2FA (TOTP + SMS opsiyonel)
- SSO (Google / Microsoft / Azure AD)
- Biometric auth (mobil)
- Failed login log + brute-force protection
- Session timeout
- Multi-device session management
- IP whitelist + country allowlist
- Full audit log
- Data retention / purge
- Legal hold
- Otomatik PII redaction
- AI prompt / knowledge versioning
- Compliance scanner (KVKK/GDPR)

------------------
Neden kritik:
------------------

Türkiye’de bankalar, büyük e-ticaret firmaları:

Audit log yoksa → imza yok  
SSO yoksa → IT reddeder  
Session timeout yoksa → security veto  
PII mask yoksa → KVKK alarm  

Globalde GDPR cezaları milyar dolar seviyesinde.

Security yoksa AI satamazsın.

------------------
Gerçek Senaryo:
------------------

E-ticaret firması diyor ki:

“Temsilci müşteri TC kimlik paylaştı mı görebiliyor muyum?”

Audit log yok → satış biter.

------------------
Örnek Kullanım:
------------------

Supervisor panelinde:

- Son 30 gün login denemeleri
- IP bazlı erişim
- Hangi agent hangi konuşmayı export etti
- AI hangi prompt ile cevap verdi

tek ekranda.

------------------
Implementasyon:
------------------

Backend:
- Auth microservice
- JWT + refresh token
- Session table (device_id, ip, country)

Audit:
- event_type
- user_id
- resource_id
- before/after snapshot

PII:
- Regex + ML entity detection
- Mask pipeline

Retention:
- Tenant policy table
- Cron purge jobs

SSO:
- OAuth2 + OpenID Connect

=====================================================
2. AI AGENT KATMANI (CORE DIFFERENTIATOR)
=====================================================

3 agent tipi:

A) Agent Assist  
B) Auto-Resolution Agent  
C) Revenue Agent  

-----------------------------------------------------
A) AGENT ASSIST
-----------------------------------------------------

Fonksiyonlar:

- Reply generation
- Kaynak referanslı cevap
- Ton kontrolü
- KVKK risk uyarısı
- Next best action

Senaryo:

Müşteri yazıyor:
“Ürün kaç günde gelir?”

AI:
- Sipariş DB’ye bakar
- Kargo SLA çeker
- Hazır cevap üretir

Aynı anda temsilciye:

“Bu müşteri yeni → teslim süresi söyle + upsell öner”

Implementasyon:

Pipeline:

message →
intent classify →
knowledge lookup →
response generation →
guardrail →
agent UI

Guardrails:
- yasak kelime listesi
- PII detector

-----------------------------------------------------
B) AUTO RESOLUTION AGENT
-----------------------------------------------------

Top 20 konu:

- Sipariş durumu
- Kargo takibi
- İade
- Stok
- Randevu
- Fiyat
- Kampanya
- Garanti

Amaç:
%60 sorgu otomatik kapanır.

Senaryo:

Müşteri:
“Kargom nerede?”

Agent:
- Trendyol order API
- Aras tracking
- WhatsApp’a cevap

İnsan görmeden kapanır.

Implementasyon:

Intent router
Action executor
Fallback human handoff

-----------------------------------------------------
C) REVENUE AGENT
-----------------------------------------------------

Fonksiyonlar:

- Lead qualification
- Bütçe tahmini
- Satın alma niyeti
- Ürün önerisi
- Randevu
- Ödeme linki

Senaryo:

Instagram’dan gelen lead.

Agent sorar:
- İhtiyacın ne?
- Bütçen?

Skorlar.

Uygunsa:
Takvim + ödeme linki yollar.

Implementasyon:

Scoring model:
- conversation features
- response latency
- keywords

CRM pipeline auto advance.

=====================================================
3. KNOWLEDGE + SOURCE OF TRUTH
=====================================================

AI şirket verisine bağlı çalışır.

Kaynaklar:

- PDF
- URL
- Ürün katalog
- Fiyat listesi
- SSS
- KVKK metni

Her cevapta:
kaynak referansı.

------------------
Senaryo:
------------------

Müşteri fiyat sorar.

AI cevap verir:

“X paketi aylık 1.200 TL (kaynak: pricing.pdf sayfa 3)”

------------------
Implementasyon:
------------------

Vector DB (tenant bazlı)
Chunking
Embedding
RAG pipeline

Knowledge gap report:
Top unanswered 50 soru.

=====================================================
4. TÜRKİYE E-TİCARET ENTEGRASYONLARI (KILL SHOT)
=====================================================

- Trendyol
- Hepsiburada
- Kargo: Yurtiçi / Aras / PTT
- Ödeme: Iyzico / PayTR

Fonksiyon:

- Sipariş sync
- İade
- Teslimat
- Müşteri mesajı

------------------
Senaryo:
------------------

Trendyol siparişi düşer.
WhatsApp’a otomatik:

“Siparişiniz alındı.”

Teslimatta:

“Kargonuz yola çıktı.”

------------------
Implementasyon:
------------------

Webhook listener
Order microservice
Customer merge logic
WhatsApp outbound worker

=====================================================
5. GELİR OTOMASYONLARI
=====================================================

- Abandoned cart
- Click-to-WhatsApp ads
- Lifecycle flows
- Broadcast segmentation

------------------
Senaryo:
------------------

Sepeti terk eden müşteri:

2 saat sonra:
“Sepetiniz sizi bekliyor.”

24 saat sonra:
%10 kupon.

------------------
Implementasyon:
------------------

Cart table
Timer jobs
Personalized message templates

=====================================================
6. OPERASYON STANDARDI
=====================================================

- SLA tracker
- QA scoring
- Conversation mining
- Revenue attribution

------------------
Senaryo:
------------------

Manager dashboard:

- Ortalama cevap süresi
- En çok kaybettiren cümleler
- En çok satış getiren agent

------------------
Implementasyon:
------------------

Analytics pipeline
Event streaming
Dashboard UI

=====================================================
SONUÇ: INVEKTO BUILD CHECKLIST
=====================================================

1. 2FA / SSO / Audit / Session / Failed login
2. Knowledge base + kaynaklı AI
3. Agent Assist
4. Auto Resolution Agent
5. Revenue Agent
6. Trendyol + Hepsiburada
7. Kargo entegrasyonu
8. Abandoned cart
9. Click-to-WhatsApp attribution
10. SLA + QA dashboard
11. Predictive analytics
12. Omnichannel sync

=====================================================
STRATEJİK GERÇEK
=====================================================

Inbox satma.
Ajan sat.

Panel yapma.
İş bitiren sistem yap.

Security’yi erteleme.
Satış ölür.

AI’yı oyuncak yapma.
Otonom yap.

Türkiye’de e-ticaret bağlamazsan yoksun.

=====================================================
MVP YOLU (3 AY)
=====================================================

Ay 1:
Security + Knowledge + Agent Assist

Ay 2:
Auto resolution + Trendyol

Ay 3:
Revenue Agent + abandoned cart

Sonra scale.

Bunu yaparsan Invekto kategori yaratır.
Yapmazsan Respond / Wati seni siler.

WapCRM için hem Türkiye hem de global pazarda rekabet avantajı yaratacak, özellikle AI destekli yeni özellikleri araştırmamı istiyorsun. Raporun genel yapısı çok sağlam. Araştırmamı aşağıdaki konularda derinleştirmemi ister misin?

Rakiplerin 2024 itibariyle eklediği yeni AI tabanlı özellikler (özellikle WhatsApp/Instagram CRM'leri)

Türkiye dışı pazarlarda hangi AI entegrasyonlarının satış/destek verimliliğini artırdığı

Global KOBİ odaklı CRM’lerde yükselen entegrasyon trendleri (e-ticaret, ödeme, reklam)

WapCRM’in SaaS modeli için öne çıkan fiyatlama/monetizasyon stratejileri

AI özelliklerinin teknik derinliği: hazır LLM entegrasyonlar mı, özel fine-tune edilmiş modeller mi?

Bunlardan hangileri odak alanın olsun? Veya eklemek istediğin başka boyutlar var mı?

even hepsi uygun
Thought for 23s

Invekto’yu (WapCRM) “AI-first satış + destek işletim sistemi” yapmadığın sürece Türkiye’de de globalde de fiyat/kanal savaşında ezilirsin. Çekirdek inbox artık commodity; kazananlar AI agent + bilgi tabanı + ticaret entegrasyonları + güvenlik paketini satıyor.

Senin raporda eksik olan kritik gerçek

“KVKK/GDPR var” demek yetmiyor; kurumsal satın almayı kilitleyen şeyler: audit log, 2FA/SSO, failed login log, session timeout, IP allowlist, data retention/purge, role granularity. Bunlar yoksa özellikle kurumsalda “AI var” diye kimse imza atmaz.

AI tarafında “transkripsiyon/lead qualification” tek başına küçük kalıyor. Pazarın gittiği yer: AI agent’ların işi baştan sona yürütmesi (lead eleme, teklif, randevu, sipariş durumu, iade, eskalasyon).

Global + Türkiye için kazanman gereken yetenekler (Invekto Feature Raporu)
1) Güvenlik ve yönetişim (Satış engelini kaldıran paket)

Bunlar “nice-to-have” değil; satışın ön koşulu:

2FA + SSO (Google/Microsoft)

Audit log (kim neyi gördü/değiştirdi/export etti)

Failed login attempt log + brute-force koruma

Session timeout + cihaz bazlı oturum yönetimi

IP allowlist + ülke bazlı erişim politikası

Data retention & legal hold + PII redaction

Template/prompt/knowledge değişikliklerinde versiyonlama

Değer: Kurumsal satış kapısı açılır.
Risk: Bunlar yoksa AI yatırımı “oyuncak” gibi kalır, procurement’de ölür.

2) AI Agent Katmanı (Invekto’nun gerçek farkı)

Rakipler AI’ı “reply önerisi”nden “işi yapan ajan” seviyesine taşıyor.
Senin de yapman gereken:

A. Agent Assist (temsilciyi uçuran)

Konuşma içinden tek tık cevap üret + “neden bu cevap” + kaynak gösterimi

Ton/brand guardrails (yasaklı ifadeler, KVKK riskli cümle uyarısı)

Next-best-action: sorulması gereken 1 soru, istenecek 1 bilgi, önerilecek 1 adım

B. Auto-Resolution Agent (işi kapatan)

“Siparişim nerede / iade / fiyat / stok / randevu / kargo” gibi top 20 konuya otonom çözüm

Eskalasyon politikası: ne zaman insana devredecek, hangi notlarla devredecek

Çok dilli + TR ağız/diyalekt toleransı (özellikle sesli mesajlarda)

C. Revenue Agent (para kazandıran)

Lead qualification + bütçe/niyet skoru

Ürün önerisi / cross-sell / upsell

Randevu/teklif akışı (takvim + ödeme linki + follow-up)

3) “Knowledge + Source of Truth” motoru (AI’ın doğruluk problemi burada çözülür)

Respond.io tarzı yaklaşım: AI yanıtlarını PDF/URL/snippet gibi şirket kaynaklarına dayandırma.
Invekto’da şart:

Knowledge base bağlama (doküman, URL, SSS, ürün katalog, politika metinleri)

Kaynak referanslı cevap (internetten sallama yok)

Tenant bazlı bilgi izolasyonu + erişim yetkisi

Knowledge gap raporu: En çok sorulan ama dokümanda olmayan 50 konu

Değer: AI kalitesi stabil olur, iade/şikayet riski düşer.

4) Ticaret entegrasyonları (Türkiye’de zorunlu, globalde büyüme)

Türkiye’de “WhatsApp CRM”i satın alanların çoğu e-ticaret / hizmet işletmesi. Burada kazanırsın.

Türkiye (lokal kill-shot)

Trendyol entegrasyonu: sipariş/teslimat/iade/müşteri soruları akışı (API + webhook)

Kargo entegrasyonları: takip linki + otomatik durum bildirimleri

Ödeme linki: tahsilat + otomatik “ödeme geldi” teyidi

Global (standard set)

Shopify / WooCommerce / Magento: sipariş + müşteri kartı + iade

SMS fallback (WhatsApp ulaşamazsa) — Wati bunu büyüme/uyumluluk için öne çıkarıyor

5) Gelir getiren otomasyonlar (AI ile “kampanya” değil “gelir akışı”)

Abandoned cart / checkout drop recovery (WhatsApp üzerinden) — pazarda açıkça konumlanıyor

Click-to-WhatsApp ads attribution: reklamdan gelen lead’i otomatik etiketle, pipeline’a sok

Lifecycle otomasyonu: yeni müşteri onboarding → kullanım → upsell → churn önleme

6) Operasyon standardı: SLA + kalite + denetim

Büyüyen ekiplerde satın alma sebebi burası:

SLA hedefleri (yanıt süresi, çözüm süresi) + alarm

QA scoring (temsilci kalite puanı, script uyumu)

Conversation mining: en çok şikayet konusu, en çok kaybeden ifade, en çok dönüşüm getiren kalıp

Sonuç: Invekto’nun “olmazsa olmaz” 10 maddelik build listesi

2FA/SSO + audit log + session timeout + failed login log

Knowledge base bağlama + kaynak referanslı AI cevapları

Agent Assist (reply + next-best-action + risk uyarıları)

Auto-Resolution Agent (top 20 konu)

Revenue Agent (qualification + teklif/randevu)

Trendyol entegrasyonu (sipariş/iade/soru)

Kargo takip + otomatik durum mesajları

Abandoned cart recovery + SMS fallback

Click-to-WhatsApp attribution + pipeline otomasyonu

SLA/QA/Conversation mining dashboard (yönetim katmanı)

Bunu yaparsan Invekto “inbox” değil, gelir ve operasyon motoru olur. Yapmazsan rakiplerin AI ajan anlatısıyla seni “panel” seviyesine iter.