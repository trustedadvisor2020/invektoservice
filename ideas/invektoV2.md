INVEKTO (WapCRM) — 3 SEKTÖR / 75 GERÇEK SENARYO + CAPABILITY MAPPING + PROS/CONS
Tarih: 2026-02-07 (Türkiye %60 ağırlık; Avrupa %20; Global %20)

KAYNAKLI GERÇEKLER (sallama yok) — BU ÇALIŞMANIN DAYANDIĞI KURALLAR
- WhatsApp: Müşteri son mesaj attıktan sonraki 24 saat içinde serbest yanıt; 24 saat dışında iş başlatmak için ön onaylı şablon (template) gerekir. (Meta docs / Twilio docs) [WA]
- WhatsApp: Template mesajlar, 24 saat pencere dışında kullanıcıya mesaj atmanın tek yoludur. (Meta docs) [WA]
- WhatsApp: Template kategorileri (Utility/Marketing/Authentication) ve yanlış kategorilendirme riskleri/kuralları (Meta docs, 2025 güncellemesi). [WA]
- Instagram DM API: Kullanıcı mesajıyla başlayan 24 saatlik yanıt penceresi (Messenger/IG policy; respond.io blog). [IG]
- Türkiye Trendyol: İade hakkı 15 gün; iade kontrolü, uygun ise 2–10 iş günü içinde tamamlanabilir; paketleme/iade süreçleri ve sık yaşanan iade/kargo şikayetleri var. (Trendyol akademi/yardım + Şikayetvar örnekleri). [TR]
- Sağlık: WhatsApp üzerinden hasta verisi paylaşımı GDPR/KVKK riskleri; özel nitelikli veri (sağlık) için hukuki dayanak/açık rıza ve süreç gerekir. (KVKK özel nitelikli veri rehberi; literatür). [HEALTH]

Not: Aşağıdaki senaryolarda 'maliyet' ve 'hacim' gibi sayılar şirketten şirkete değişir. Kaynaklı olmayan sayılar 'tahmini' diye işaretlenmiştir.

============================================================
A) AVATAR SETİ (6 PERSONA) — WhatsApp + Instagram DM gerçeğine göre
============================================================

[E1] Mehmet | Bölge: Türkiye | Dikey: E-ticaret / Marketplace
- Rol: Trendyol+Hepsiburada satıcısı (2-5 kişi)
- Kanal: Mesaj kanalı: WhatsApp ağırlıklı, IG DM ikincil
- Temel ağrı: Pain: kargo/iade soruları, iade reddi kavgaları, aynı hattan çok kişi yazması, yanıt gecikmesi
- Uyumluluk/operasyon riski: Risk: template penceresi dışında 'proaktif' mesaj atamama; spam/şikayet; operasyon kaosu

[E2] Ayşe | Bölge: Türkiye/Global | Dikey: E-ticaret / D2C
- Rol: Shopify/WooCommerce growth store (5-20 kişi)
- Kanal: Mesaj kanalı: IG DM lead, WhatsApp support/sales
- Temel ağrı: Pain: DM’den WhatsApp’a geçiş, ödeme linki, sepet terk, stok/beden soruları, SLA
- Uyumluluk/operasyon riski: Risk: IG otomasyon tetik zorunluluğu ve 24 saat penceresi; rate limit; attribution kaybı

[D1] Dr. Burak | Bölge: Türkiye | Dikey: Diş
- Rol: Klinik sahibi (gelir/itibar sorumlusu)
- Kanal: Mesaj kanalı: WhatsApp randevu/plan, IG DM lead
- Temel ağrı: Pain: fiyat sorusu + fotoğrafla 'teşhis' talebi, no-show, tedavi planı takibi, ekip içi karmaşa
- Uyumluluk/operasyon riski: Risk: sağlık verisi KVKK özel nitelikli; hasta foto/rapor paylaşımı; kayıt saklama

[D2] Elif | Bölge: Türkiye | Dikey: Diş
- Rol: Ön büro / hasta koordinatörü
- Kanal: Mesaj kanalı: WhatsApp yoğun, IG DM de var
- Temel ağrı: Pain: aynı anda 30 sohbet, randevu teyidi, iptal/erteleme, yanlış bilgi verme korkusu
- Uyumluluk/operasyon riski: Risk: KVKK onam/aydınlatma; cihaz kaybı; kimin ne dediği kaydı

[A1] Dr. Selin | Bölge: Türkiye/Avrupa | Dikey: Klinik+Estetik
- Rol: Estetik klinik sahibi
- Kanal: Mesaj kanalı: IG DM lead motoru, WhatsApp kapanış
- Temel ağrı: Pain: DM’den randevuya dönüşmüyor, güven sorunu, before/after istekleri, paket satışı
- Uyumluluk/operasyon riski: Risk: sağlık/estetik verisi; yanlış vaat; reklam mevzuatı; gecikme → lead kaçar

[A2] Zeynep | Bölge: Türkiye/Global | Dikey: Klinik+Estetik
- Rol: Operasyon + satış sorumlusu
- Kanal: Mesaj kanalı: IG DM inbound, WhatsApp follow-up
- Temel ağrı: Pain: lead scoring yok, takip kaçıyor, ödeme/kapora, no-show
- Uyumluluk/operasyon riski: Risk: otomasyon spam sayılır; IG 24 saat; KVKK kayıt/erişim kontrolü

============================================================
B) CAPABILITY LİSTESİ (Invekto'nun değerlendirme sözlüğü)
============================================================

- C1: Unified Inbox — WA+IG tek inbox, konuşma listesi, arama, filtre, etiket, açık/kapalı, transfer
- C2: Routing & Workload — sırayla/tesadüfi/en az sohbet/en çok bekleyen; yeni vs eski müşteri yönlendirme
- C3: Templates & Snippets — şablon mesajlar, hazır yanıt kütüphanesi, dosya/görsel/video şablonları
- C4: Reporting Core — yanıt süresi, hacim, agent performansı, etiket bazlı raporlar
- C5: Security Baseline — rol bazlı + page bazlı izin, maskeleme, KVKK/GDPR pratikleri
- C6: Enterprise Security (hedef) — SSO, 2FA, audit log, session timeout, failed login, IP/country allowlist, retention/purge, legal hold
- C7: Knowledge (RAG) (hedef) — PDF/URL/SSS bağla; kaynaklı cevap; gap raporu
- C8: Agent Assist (hedef) — cevap önerisi + kaynak + risk uyarısı + next-best-action
- C9: Auto-Resolution Agent (hedef) — top intents otonom çözüm; düşük güven → insan; not/özet bırakma
- C10: Revenue Agent (hedef) — lead qualify/score; randevu/teklif/ödeme linki; takip akışı
- C11: E-commerce Integrations (hedef) — Trendyol/HB/Shopify/Woo sipariş-iade-kargo senk
- C12: Ads Attribution (hedef) — Click-to-WA/IG kampanya kaynağı; etiket + pipeline
- C13: QA & Mining (hedef) — kalite skor, script uyumu, şikayet temaları, win/loss ifadeleri

============================================================
C) 'BUGÜN' vs 'HEDEF' — Invekto'nun gerçek durumu (sadece konuştuğumuz kadarı)
============================================================

BUGÜN (mevcut): Unified Inbox, routing, etiket, raporlama, şablon kütüphanesi, dosya/görsel/video, maskeleme (bazı), çoklu kanal (WA/IG/FB/Telegram/VOIP/SMS entegrasyonları).
BUGÜN (eksik): SSO/2FA, session timeout, failed login logs (yok), audit log (kısıtlı/varsayım), keyword routing (yok), test routing rules (yok).
HEDEF: Knowledge RAG + Agent Assist + Auto-Resolution + Revenue + yerel e-ticaret entegrasyonları + enterprise security tamamlanması.

============================================================
D) 75 SENARYO (25 e-ticaret + 25 diş + 25 klinik/estetik) — DERİN MOD
============================================================

Legend:
- Kanıt seviyesi: A=Kaynaklı/yaygın doğrulanmış, B=Yaygın saha pratiği (tekil kanıt zayıf), C=Doğrulanamadı (kullanmamaya çalıştım)
- WA/IG kuralları: 24 saat penceresi + template/opt-in kısıtları senaryolarda dikkate alındı. [WA][IG]

---
SENARYO 01 — Kargom nerede? takip linki istiyor
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Kargom nerede? Takip linki atar mısın?'
   - Satıcı: (gecikiyor)
   - Müşteri: '2 gündür yazıyorum cevap yok.'

2) Bugün işletme bunu nasıl yönetiyor?
   - WhatsApp'tan sipariş no sorulur
   - Trendyol/Shopify panelinden takip bulunur
   - Link kopyalanıp yapıştırılır

3) Nerede batıyor?
   - Yoğunlukta geç yanıt → müşteri sinirlenir
   - Yanlış siparişe link atılır
   - 24 saat penceresi kapanırsa template gerektirir (WhatsApp kuralı)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) Günlük 30-200 'kargo nerede' mesajı (satıcı ölçeğine göre)
   - (Tahmini) 1 mesaj 2-4 dk → günde 1-8 saat operatör zamanı
   - Kötü yorum/iptal riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + C2 routing + C3 hazır yanıtlarla temsilci hızlanır
   - Sipariş verisi otomatik çekilmediği için temsilci manuel bakar (entegrasyon yoksa)
B) Phase-1 ile ne olur?
   - C11 (Trendyol/Shopify) sipariş+takip sidebar → 1 tık takip linki
   - C8 Agent Assist: 'takip + gecikme özrü' hazır metin + doğru sipariş çekimi
C) Phase-2/3 gerektiren
   - C9 Auto-Resolution: 'kargo nerede' intentini otonom yanıtla
   - C12 attribution: kampanya kaynağına göre mesaj tonu/kupon
   - C6 enterprise güvenlik (kurumsal satıcı için)

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C11, C8

Öneri
   - Takip linkini ve kargo SLA bilgisini 'utility' içerikte tut (spam/marketing değil)
   - Sipariş no istemeden müşteriyi telefon/isim ile eşleştiren match kuralları ekle (hata azaltır)

Pros
   - Cevap süresi düşer → yorum/iptal riski azalır
   - Yanlış link verme azalır
   - Operatör başına kapasite artar
Cons
   - Entegrasyon hatası yanlış bilgi üretirse müşteri daha çok sinirlenir → fallback/human şart
   - WhatsApp 24h pencere dışı proaktif bilgilendirme template maliyeti doğurur

---
SENARYO 02 — İade kodu aldım, hangi kargoya vereceğim?
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'İade kodu aldım, hangi kargoya vereceğim? ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 03 — İadem reddedildi / açılmadı deniyor
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'İadem reddedildi, ürünü hiç açmadım!'
   - Müşteri: 'Paramı vermiyorsunuz.'
   - Müşteri: 'Şikayetvar'a yazacağım.'

2) Bugün işletme bunu nasıl yönetiyor?
   - Temsilci sipariş/iade kaydını arar
   - Satıcı/marketplace süreçleri açıklanmaya çalışılır
   - Bazı durumlarda platforma yönlendirilir

3) Nerede batıyor?
   - Duygusal kriz yönetimi zayıfsa alevlenir
   - Kanıt/foto istenir, dosyalar dağılır
   - Yanlış söz verilirse hukuki risk

4) Gerçek maliyet (tahmini ise belirtildi)
   - İtibar kaybı: şikayet siteleri/yorumlar
   - Operatör zamanı + iade lojistik maliyeti

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + dosya paylaşımıyla kanıt toplanır
   - C2 routing ile 'şikayet' etiketiyle uzman agent'e gider
B) Phase-1 ile ne olur?
   - C8 'de-eskalasyon' cevap önerisi + doğru süreç metni (platforma göre)
   - C7 policy+SOP kaynaklı cevap; temsilci sallamaz
C) Phase-2/3 gerektiren
   - C13 mining: en çok kriz çıkaran kalıplar + QA
   - C11 entegrasyonla iade state ve gerekçe otomatik çekilir

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8, C13, C11

Öneri
   - Kriz akışı: 1) empati 2) süreç 3) istenen kanıt 4) SLA 5) platform eskalasyonu
   - Özel durumlar için 'insan devri' zorunlu; auto-resolution bu senaryoda riskli

Pros
   - Kriz dili standardize olur → şikayet azalır
   - Yetkin agent'e hızlı yönlendirme
Cons
   - Tam otomasyon yanlış karar verirse daha büyük kriz; bu senaryoda human-in-the-loop şart

---
SENARYO 04 — Kusurlu ürün geldi, değişim mi iade mi?
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Kusurlu ürün geldi, değişim mi iade mi? ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 05 — Yanlış ürün geldi / eksik parça
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Yanlış ürün geldi / eksik parça ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 06 — Teslim edildi görünüyor ama gelmedi
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Teslim edildi görünüyor ama gelmedi ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 07 — Kargo paketi hasarlı, tutanak istiyor
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Kargo paketi hasarlı, tutanak istiyor ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 08 — Fatura/kurumsal fatura talebi
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Fatura/kurumsal fatura talebi ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 09 — Adres değişikliği / teslimat saatini değiştir
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Adres değişikliği / teslimat saatini değiştir ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 10 — Kampanyalı aldım, fiyat farkı/kupon sorunu
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Kampanyalı aldım, fiyat farkı/kupon sorunu ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 11 — Ürün bedeni/uyumu — Instagram DM’den soru
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Ürün bedeni/uyumu — Instagram DM’den soru ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 12 — Stok var mı? Ne zaman gelir? (D2C)
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Stok var mı? Ne zaman gelir? (D2C) ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 13 — Kapıda ödeme var mı? Havale/IBAN at
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Kapıda ödeme var mı? Havale/IBAN at ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 14 — Sepeti terk etti — DM/WA follow-up
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E2 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Sepeti terk etti — DM/WA follow-up ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 15 — Ürün açıklamasıyla gelen farklı (görsel uyumsuz)
Bölge: Türkiye | Dikey: E-ticaret | Avatar: E2 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Ürün açıklamasıyla gelen farklı (görsel uyumsuz) ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 16 — İade süresi kaç gün? 15 gün kuralı
Bölge: Avrupa | Dikey: E-ticaret | Avatar: E1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'İade süresi kaç gün?'
   - Müşteri: '15 gün müydü?'
   - Müşteri: 'İadem ne zaman yatar?'

2) Bugün işletme bunu nasıl yönetiyor?
   - Temsilci Trendyol/mağaza politikasını hatırlamaya çalışır
   - Kopyala-yapıştır metin atar
   - İade durumunu panelden kontrol eder

3) Nerede batıyor?
   - Standart bilgi tutarsız verilir
   - İade süreci (2-10 iş günü) gibi detaylar atlanır
   - Müşteri tekrar tekrar yazar

4) Gerçek maliyet (tahmini ise belirtildi)
   - Tekrarlı soru yükü (tahmini): toplam mesajların %10-20'si
   - Yanlış bilgi → şikayet açılır

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C3 şablon ile standart yanıt mümkün
   - C4 raporla bu soruların hacmi görülür
B) Phase-1 ile ne olur?
   - C7 knowledge ile 'iade politikası' tek kaynak; cevap kaynaklı gider
   - C8 agent assist, müşteri mesajından intent→ doğru politika paragrafını önerir
C) Phase-2/3 gerektiren
   - C9 auto-resolution iade sorularını otonom çözer
   - C11 iade durumu canlı çekilir (platform entegrasyonlarına bağlı)

Gerekli yetenekler (capability mapping)
   - C3, C7, C8, C4

Öneri
   - Trendyol iade hakkı (15 gün) ve iade tamamlanma (2-10 iş günü) gibi sabit bilgileri knowledge'a koy; kaynağı linkle. [TR]
   - İade durumu çekilemiyorsa net 'panelden bakıp döneceğim' yerine SLA ver

Pros
   - Tutarlı cevap, daha az ping-pong
   - Yeni personel hızlı adapte olur
Cons
   - Politika değişirse knowledge güncellenmezse yanlış bilgi yayılır → versioning şart

---
SENARYO 17 — İade süreci kaç günde biter? 2-10 iş günü
Bölge: Avrupa | Dikey: E-ticaret | Avatar: E1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'İade süreci kaç günde biter? 2-10 iş günü ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 18 — Gel Al noktası kapalı / iade teslim edemiyorum
Bölge: Avrupa | Dikey: E-ticaret | Avatar: E1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Gel Al noktası kapalı / iade teslim edemiyorum ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 19 — Hepsiburada siparişinde kargo gecikti
Bölge: Avrupa | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Hepsiburada siparişinde kargo gecikti ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 20 — Trendyol Express araması vs WhatsApp
Bölge: Avrupa | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Trendyol Express araması vs WhatsApp ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 21 — Shopify: ödeme başarısız, link tekrar
Bölge: Global | Dikey: E-ticaret | Avatar: E2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Shopify: ödeme başarısız, link tekrar ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 22 — WooCommerce: kargo ücretini iade eder misiniz?
Bölge: Global | Dikey: E-ticaret | Avatar: E2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'WooCommerce: kargo ücretini iade eder misiniz? ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 23 — Çoklu müşteri aynı hattan yazıyor — ekip çakışması
Bölge: Global | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Çoklu müşteri aynı hattan yazıyor — ekip çakışması ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 24 — İade paketi kayboldu / kargo kodu okunmadı
Bölge: Global | Dikey: E-ticaret | Avatar: E1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'İade paketi kayboldu / kargo kodu okunmadı ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 25 — Mesaj penceresi kapandı — template ile bilgi verme
Bölge: Global | Dikey: E-ticaret | Avatar: E1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Müşteri: 'Mesaj penceresi kapandı — template ile bilgi verme ile ilgili yardım lazım.'
   - Müşteri: 'Hızlı döner misiniz?'
   - Satıcı: (yoğun)

2) Bugün işletme bunu nasıl yönetiyor?
   - Mesajlar farklı kişilerde kalır
   - Panelden bilgi aranır
   - Cevap geç gider

3) Nerede batıyor?
   - Ekip çakışması / duplicate yanıt
   - Yanlış bilgi
   - 24 saat penceresi riski (WA/IG)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı / iade artışı / yorum riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart yanıt
B) Phase-1 ile ne olur?
   - C7 knowledge ile tutarlılık
   - C11 entegrasyon varsa veri çekimi
   - C8 cevap önerisi
C) Phase-2/3 gerektiren
   - C9 otonom çözüm (uygunsa)
   - C13 kalite/tema analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Tekrarlı sorular için intent→template eşlemesi yap
   - IG DM için tetik/24h penceresini gözet; kullanıcı başlatmadan outbound yapma. [IG]

Pros
   - Cevap süresi düşer
   - Ekip verimi artar
Cons
   - Yanlış otomasyon müşteri deneyimini bozar; fallback şart

---
SENARYO 26 — Fiyat sorusu: implant kaç TL? 'tek çene' vs
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Fiyat sorusu: implant kaç TL? 'tek çene' vs'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 27 — Instagram DM: 'foto atsam fiyat verir misiniz?'
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Instagram DM: 'foto atsam fiyat verir misiniz?''
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 28 — Randevu alma: uygun saat ve doktor seçimi
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Yarın akşam müsait misiniz?'
   - Klinik: 'Hangi işlem?'
   - Hasta: 'İmplant kontrol'

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro boşluklara bakar
   - Elle saat önerir
   - Teyit mesajı atar

3) Nerede batıyor?
   - Çakışan randevu riski
   - Yanıt gecikirse hasta başka yere gider
   - IG/WA 24h pencere içinde hızlı yanıt şart

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) lead kaçışı + boş slot maliyeti

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + yönlendirme ile hızlı dönüş
   - Şablonlarla standart soru seti
B) Phase-1 ile ne olur?
   - C10 revenue agent: slot önerisi + teyit + kapora linki (hedef)
   - C8: next-best-question (işlem, süre, lokasyon)
C) Phase-2/3 gerektiren
   - Takvim/klinik yazılımı entegrasyonu
   - C9: basit randevu niyetli mesajları otonom yönetme (riskli, kontrollü)

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C8, C10

Öneri
   - Ön büro için 'mini script': 3 soru ile slot öner; sonra teyit ve hatırlatma planla
   - IG DM lead’leri hızlıca WhatsApp’a taşı (tek tık link) ama kullanıcı başlatmalı. [IG]

Pros
   - Yanıt hızı artar → dönüşüm artar
   - Çakışma azalır
Cons
   - Tam otomasyon yanlış slot verirse kriz; insan onayı iyi

---
SENARYO 29 — No-show: hasta gelmedi, hatırlatma yok
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'No-show: hasta gelmedi, hatırlatma yok'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 30 — Acil ağrı: gece mesajı, triage gerekiyor
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Acil ağrı: gece mesajı, triage gerekiyor'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 31 — Tedavi planı: 2 seans, süreç anlatımı
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Tedavi planı: 2 seans, süreç anlatımı'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 32 — Öncesi/sonrası bakım talimatı (çekim sonrası)
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Öncesi/sonrası bakım talimatı (çekim sonrası)'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 33 — Hasta kimlik/rapor gönderdi: KVKK riski
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Kimliğimi ve röntgenimi atıyorum.'
   - Hasta: (fotoğraf/dosya)
   - Klinik: 'Tamam'

2) Bugün işletme bunu nasıl yönetiyor?
   - Hasta dosyayı WhatsApp'tan yollar
   - Telefon galeride kalır
   - Kim gördü belli olmaz

3) Nerede batıyor?
   - Özel nitelikli sağlık verisi + kimlik → KVKK riski
   - Erişim kontrolü/audit yoksa iç sızıntı riski
   - Cihaz kaybı → veri kaybı

4) Gerçek maliyet (tahmini ise belirtildi)
   - Regülasyon riski (KVKK) + itibar riski
   - Operasyon: dosya aramak/iletmek zaman

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + dosya yönetimi var ama enterprise kontrol eksik kalabilir
   - Maskeleme var ama sağlık verisi için süreç/aydınlatma gerektirir
B) Phase-1 ile ne olur?
   - C6: audit log + erişim kontrol + retention policy (hedef)
   - C7: onam/aydınlatma metinlerini knowledge'dan kaynaklı gönderme
   - C8: temsilciye 'hasta verisi isteme/almada uyarı'
C) Phase-2/3 gerektiren
   - Hasta verisini CRM/EMR ile entegre güvenli arşivleme
   - C13: compliance QA kontrolleri

Gerekli yetenekler (capability mapping)
   - C1, C5, C6, C7, C8, C13

Öneri
   - WhatsApp üzerinden sağlık verisi geliyorsa: 1) aydınlatma + açık rıza metni 2) erişim sınırı 3) otomatik maskeleme/etiketleme 4) retention
   - Hasta dosyasını 'gerektiği kadar' iste; gereksiz veri isteme

Pros
   - Kurumsal/hukuki risk azalır
   - Kayıt tutarlılığı artar
Cons
   - Aşırı sıkı politika kullanıcı deneyimini yavaşlatır; kritik: doğru denge

---
SENARYO 34 — Yabancı hasta (EU): fiyat + otel/transfer sorusu
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Yabancı hasta (EU): fiyat + otel/transfer sorusu'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 35 — Şikayet: 'dolgu düştü' tekrar randevu
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Şikayet: 'dolgu düştü' tekrar randevu'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 36 — Ödeme/kapora: rezervasyon için ödeme linki
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Ödeme/kapora: rezervasyon için ödeme linki'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 37 — Sigorta/özel sağlık anlaşması sorusu
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Sigorta/özel sağlık anlaşması sorusu'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 38 — Çocuk hasta: korku yönetimi + randevu
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Çocuk hasta: korku yönetimi + randevu'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 39 — WhatsApp mesajları kayıt altına alma/raporlama ihtiyacı
Bölge: Türkiye | Dikey: Diş | Avatar: D2 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'WhatsApp mesajları kayıt altına alma/raporlama ihtiyacı'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 40 — Randevu iptal/erteleme yoğunluğu
Bölge: Türkiye | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Yarın akşam müsait misiniz?'
   - Klinik: 'Hangi işlem?'
   - Hasta: 'İmplant kontrol'

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro boşluklara bakar
   - Elle saat önerir
   - Teyit mesajı atar

3) Nerede batıyor?
   - Çakışan randevu riski
   - Yanıt gecikirse hasta başka yere gider
   - IG/WA 24h pencere içinde hızlı yanıt şart

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) lead kaçışı + boş slot maliyeti

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + yönlendirme ile hızlı dönüş
   - Şablonlarla standart soru seti
B) Phase-1 ile ne olur?
   - C10 revenue agent: slot önerisi + teyit + kapora linki (hedef)
   - C8: next-best-question (işlem, süre, lokasyon)
C) Phase-2/3 gerektiren
   - Takvim/klinik yazılımı entegrasyonu
   - C9: basit randevu niyetli mesajları otonom yönetme (riskli, kontrollü)

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C8, C10

Öneri
   - Ön büro için 'mini script': 3 soru ile slot öner; sonra teyit ve hatırlatma planla
   - IG DM lead’leri hızlıca WhatsApp’a taşı (tek tık link) ama kullanıcı başlatmalı. [IG]

Pros
   - Yanıt hızı artar → dönüşüm artar
   - Çakışma azalır
Cons
   - Tam otomasyon yanlış slot verirse kriz; insan onayı iyi

---
SENARYO 41 — Diş beyazlatma kampanyası: IG DM lead
Bölge: Avrupa | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Diş beyazlatma kampanyası: IG DM lead'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 42 — Doktor meşgul: ön büro cevap veremiyor
Bölge: Avrupa | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Doktor meşgul: ön büro cevap veremiyor'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 43 — Yanlış bilgi: fiyat/plan tutarsızlığı
Bölge: Avrupa | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Yanlış bilgi: fiyat/plan tutarsızlığı'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 44 — Hasta fotoğrafları cihazda kalıyor (data loss)
Bölge: Avrupa | Dikey: Diş | Avatar: D1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Hasta fotoğrafları cihazda kalıyor (data loss)'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 45 — Konsültasyon sonrası takip: 'nasılsınız' mesajı (template/opt-in)
Bölge: Avrupa | Dikey: Diş | Avatar: D2 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Konsültasyon sonrası takip: 'nasılsınız' mesajı (template/opt-in)'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 46 — Gece/hafta sonu otomatik cevap (IG/WA 24h)
Bölge: Global | Dikey: Diş | Avatar: D1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Gece/hafta sonu otomatik cevap (IG/WA 24h)'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 47 — İkinci görüş: önceki tetkik dosyaları
Bölge: Global | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'İkinci görüş: önceki tetkik dosyaları'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 48 — İade/iptal: kapora geri ister
Bölge: Global | Dikey: Diş | Avatar: D1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'İade/iptal: kapora geri ister'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 49 — Hekim notları + etiketli raporlama
Bölge: Global | Dikey: Diş | Avatar: D2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Hekim notları + etiketli raporlama'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 50 — Hasta memnuniyet anketi (opt-in)
Bölge: Global | Dikey: Diş | Avatar: D1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Hasta: 'Hasta memnuniyet anketi (opt-in)'
   - Hasta: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - Ön büro cevaplar, doktor araya girer
   - Fiyatlar kişiden kişiye değiştiği için uzar
   - Takip unutulur

3) Nerede batıyor?
   - Tutarsız fiyat/vaat riski
   - Lead kaybı (gecikme)
   - Hasta verisi/mahremiyet riski (foto/rapor)

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı; no-show; ekip zamanı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1+C2 ile ekip paylaşımı
   - C3 ile standart bilgi (genel fiyat aralığı)
B) Phase-1 ile ne olur?
   - C7: SSS + prosedür açıklamaları
   - C8: doğru soruları öner (yaş, şikayet, randevu niyeti)
C) Phase-2/3 gerektiren
   - C10: ödeme/kapora + follow-up
   - C13: kalite skoru + kaçan lead analizi

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C7, C8

Öneri
   - Fiyat sorularını 'aralık + muayene şart' çerçevesine oturt; yanlış vaatten kaçın
   - Sağlık verisi gelirse onam metni ve veri minimizasyonu uygula. [HEALTH]

Pros
   - Lead hızlanır, tutarlılık artar
Cons
   - Aşırı otomatik cevap 'robot' hissi yaratır; estetik/dişte güven önemli

---
SENARYO 51 — Instagram DM: 'fiyat nedir?' botox/dolgu
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Instagram DM: 'fiyat nedir?' botox/dolgu'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 52 — Before/after fotoğraf isteği + güven sorusu
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Before/after fotoğraf isteği + güven sorusu'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 53 — DM’den WhatsApp’a geçiş: 'numaranızı atın' kayıp
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'DM’den WhatsApp’a geçiş: 'numaranızı atın' kayıp'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 54 — Randevu planlama + kapora
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Randevu planlama + kapora'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 55 — No-show: hatırlatma ve yeniden kazanım
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'No-show: hatırlatma ve yeniden kazanım'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 56 — Uygunluk/kontrendikasyon soruları
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Uygunluk/kontrendikasyon soruları'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 57 — İşlem sonrası şikayet: şişlik/morarma panik
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'İşlem sonrası şikayet: şişlik/morarma panik'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 58 — Paket satış: lazer 6 seans
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Paket satış: lazer 6 seans'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 59 — Fiyat pazarlığı + kampanya
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Fiyat pazarlığı + kampanya'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 60 — Yorum/şikayet yönetimi (sosyal kanıt)
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Yorum/şikayet yönetimi (sosyal kanıt)'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 61 — Yabancı hasta (EU): transfer/otel + fiyat
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Yabancı hasta (EU): transfer/otel + fiyat'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 62 — KVKK: foto/video sağlık verisi
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Fotoğraf atıyorum, hangi işlem olur?'
   - Lead: (yüz foto)
   - Klinik: 'Tamam'

2) Bugün işletme bunu nasıl yönetiyor?
   - Foto telefonlarda kalır
   - Farklı personel görür
   - Silme/erişim takibi yok

3) Nerede batıyor?
   - Sağlık verisi özel nitelikli; aydınlatma/onam/saklama-silme süreçleri şart
   - Kayıp cihaz riski
   - Gereksiz veri toplama riski

4) Gerçek maliyet (tahmini ise belirtildi)
   - KVKK riski + itibar riski
   - Operasyon: dosya arama/iletme

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + rol bazlı erişim var ama enterprise seviyede audit/retention eksik olabilir
   - Maskeleme mekanizmaları sınırlı kalabilir
B) Phase-1 ile ne olur?
   - C6 güvenlik seti (audit/retention) planlanmalı
   - C7 knowledge ile aydınlatma metni + rıza akışı
   - C8 uyarı: 'foto istemeden önce onam'
C) Phase-2/3 gerektiren
   - Güvenli arşiv + silme talepleri (DSAR) yönetimi
   - C13 compliance QA

Gerekli yetenekler (capability mapping)
   - C5, C6, C7, C8, C13

Öneri
   - Foto/sağlık verisini istemeden önce kısa onam + amaç + saklama süresi mesajı gönder
   - Mümkünse ön değerlendirmeyi yapılandırılmış form ile al; DM'de dağılmasın

Pros
   - Regülasyon ve iç sızıntı riski düşer
   - Profesyonel algı artar
Cons
   - Sürtünme artar; conversion düşmemesi için akış çok kısa olmalı

---
SENARYO 63 — Reklam kaynağı: click-to-whatsapp lead
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Reklam kaynağı: click-to-whatsapp lead'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 64 — Mesaj penceresi kapandı: follow-up template
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Merhaba, fiyat?' (dün yazmış)
   - Klinik: (bugün dönüyor)
   - WhatsApp 24h penceresi kapanmış olabilir

2) Bugün işletme bunu nasıl yönetiyor?
   - Geç dönülür
   - Bazı klinikler yine mesaj atar (risk)
   - Bazıları hiç dönmez

3) Nerede batıyor?
   - WhatsApp 24h pencere dışı serbest mesaj atılamaz; template gerekir (kural)
   - Geç yanıt → lead kaçar
   - Yanlış template kategorisi maliyet/ret riski

4) Gerçek maliyet (tahmini ise belirtildi)
   - Lead kaybı
   - Template maliyeti + hesap sağlık riski

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C3 template desteği ile pencere dışı iletişim yönetilebilir (doğru kategori)
   - C4 ile yanıt gecikmesi raporlanır
B) Phase-1 ile ne olur?
   - C12 attribution ile geciken lead'leri önceliklendir
   - C8 agent assist follow-up metni + compliance uyarısı
C) Phase-2/3 gerektiren
   - C10 revenue agent: otomatik follow-up + randevu teklif
   - C6 enterprise security + audit (özellikle sağlık verisi)

Gerekli yetenekler (capability mapping)
   - C3, C4, C8, C12

Öneri
   - Follow-up mesajlarını 'utility' (randevu teyit/ bilgi) çerçevesinde tut; marketing spam yapma
   - IG tarafında kullanıcı tetiklemeden outbound DM atma. [IG]

Pros
   - Kaçan lead'leri geri kazanma şansı
   - Uyumluluk kontrolü ile hesap riski azalır
Cons
   - Yanlış template/yanlış içerik: maliyet + blok riski
   - Otomatik follow-up aşırı olursa spam algısı

---
SENARYO 65 — Çoklu personel: DM/WA'da çakışan cevaplar
Bölge: Türkiye | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Çoklu personel: DM/WA'da çakışan cevaplar'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 66 — Doktor onayı gereken lead'ler
Bölge: Avrupa | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Doktor onayı gereken lead'ler'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 67 — İade/iptal: kapora geri ister
Bölge: Avrupa | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'İade/iptal: kapora geri ister'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 68 — Ödeme linki + taksit sorusu
Bölge: Avrupa | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Ödeme linki + taksit sorusu'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 69 — İşlem takvimi: yoğun günlerde slot yönetimi
Bölge: Avrupa | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'İşlem takvimi: yoğun günlerde slot yönetimi'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 70 — Müşteri 'doktorla konuşmak istiyorum' baskısı
Bölge: Avrupa | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Müşteri 'doktorla konuşmak istiyorum' baskısı'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 71 — Ön değerlendirme formu ihtiyacı
Bölge: Global | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Ön değerlendirme formu ihtiyacı'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 72 — Post-op bakım talimatı standardizasyonu
Bölge: Global | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: B

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Post-op bakım talimatı standardizasyonu'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 73 — Memnuniyet anketi + referral isteme (opt-in)
Bölge: Global | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Memnuniyet anketi + referral isteme (opt-in)'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 74 — Spam/yanlış tetik: IG otomasyon limitleri
Bölge: Global | Dikey: Klinik+Estetik | Avatar: A2 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Spam/yanlış tetik: IG otomasyon limitleri'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

---
SENARYO 75 — Hasta verisini saklama: kayıt, erişim, silme
Bölge: Global | Dikey: Klinik+Estetik | Avatar: A1 | Kanıt: A

1) Müşteri mesajı (örnek konuşma)
   - Lead: 'Hasta verisini saklama: kayıt, erişim, silme'
   - Lead: 'Fiyat alabilir miyim?'
   - Klinik: (gecikiyor)

2) Bugün işletme bunu nasıl yönetiyor?
   - IG DM'de hızlı cevap baskısı
   - Cevap gecikirse lead kayar
   - WhatsApp'a geçişte numara kaybolur

3) Nerede batıyor?
   - Speed-to-lead kritik
   - Güven sorusu (before/after, yorum)
   - No-show + kapora yönetimi

4) Gerçek maliyet (tahmini ise belirtildi)
   - (Tahmini) dönüşüm kaybı + boş slot maliyeti
   - (Tahmini) no-show oranı artışı

Invekto burada:
A) Bugün ne kadarını yapıyor?
   - C1 inbox + çoklu ajan yönetimi
   - C3 şablonlar ile hızlı cevap
   - C4 raporla gecikme görünür
B) Phase-1 ile ne olur?
   - C8 agent assist: doğru soruları öner + güven metinleri
   - C10 revenue agent: kapora + randevu
   - C12 attribution: iyi lead kaynağını gör
C) Phase-2/3 gerektiren
   - C9 auto-resolution: basit FAQ'ları otonom yanıtla
   - C13 mining: en çok dönüşüm getiren script

Gerekli yetenekler (capability mapping)
   - C1, C2, C3, C4, C8, C10, C12

Öneri
   - IG DM için 3 adımlı script: 1) ihtiyacı netle 2) güven kanıtı 3) WhatsApp'a geç ve slot öner
   - No-show için otomatik hatırlatma (opt-in uyumlu) ve kapora politikası

Pros
   - Lead kaçışı azalır
   - Operasyon standardize olur
   - Gelir akışı netleşir
Cons
   - Aşırı otomasyon 'spam' hissi yaratır
   - Yanlış vaat/yanlış uygunluk: sağlık riski → human review şart

============================================================
E) CAPABILITY GAP ÖZETİ (75 senaryodan çıkarım)
============================================================

En çok tekrar eden yetenekler (senaryo sayısı):
- C8: 75/75
- C3: 73/75
- C1: 72/75
- C2: 71/75
- C7: 48/75
- C4: 25/75
- C10: 25/75
- C12: 24/75
- C8: doğru soruları öner (yaş: 22/75
- şikayet: 22/75
- randevu niyeti): 22/75
- C13: 3/75
- C11: 2/75
- C8: next-best-question (işlem: 2/75
- süre: 2/75

Phase-1 için 'matematiksel' zorunlular (en yüksek frekans + düşük risk):
- C1 Unified Inbox + C2 Routing + C3 Templates/Snippets (çekirdek) — zaten var.
- C7 Knowledge (RAG değilse bile 'tek kaynak' SSS/policy motoru) — tutarlılık için kritik.
- C8 Agent Assist (cevap önerisi + risk uyarısı) — hız + kalite için.
- C11 Entegrasyonlar: Türkiye e-ticarette Trendyol/HB; D2C’de Shopify/Woo (sipariş/kargo/iade çekimi).
- C4 Reporting Core: Speed-to-lead, SLA ve deflection ölçümü.

Phase-2/3 için yüksek değer ama daha riskli/karmaşık:
- C9 Auto-Resolution (otonom çözüm) — yanlış cevap riski; iyi guardrails/hand-off şart.
- C10 Revenue Agent — ödeme/kapora/teklif; entegrasyon ve süreç tasarımı ister.
- C6 Enterprise Security full paketi — enterprise satış için şart; sağlıkta erken gelir.
- C12 Ads attribution — growth için; önce temel akış.
- C13 QA & Mining — ölçeklemede kaliteyi tutmak için.

============================================================
F) KAYNAK NOTLARI (bu dokümandaki 'A' seviyeli gerçekler için)
============================================================

[WA] WhatsApp 24h pencere + template zorunluluğu: Twilio 'customer service window' açıklaması ve Meta WhatsApp template docs. (Turn0search4, Turn0search8)
[WA] Template kategorilendirme güncellemesi (2025): Meta developer docs template categorization. (Turn0search1)
[IG] 24h pencere: Messenger/IG policy overview + respond.io IG customer service yazısı. (Turn0search2, Turn0search18)
[TR] Trendyol iade hakkı 15 gün: Trendyol Satıcı Bilgi Merkezi iade süreçleri. (Turn0search11)
[TR] İade tamamlanma 2–10 iş günü ve paketleme standartları: Trendyol kolay iade paketleme standartları sayfası. (Turn0search15)
[TR] İade/kargo şikayet patternleri: Şikayetvar Trendyol iade/kargo şikayetleri örnekleri. (Turn0search7)
[HEALTH] Özel nitelikli veri rehberi: KVKK rehberi. (Turn1search5)
[HEALTH] Sağlıkta WhatsApp kullanımı riskleri ve kayıt tutma zorluğu: PMC makalesi. (Turn1search8)
[HEALTH] KVKK kamu duyurusu (WhatsApp gibi uygulamalarla ilgili): duyuru üzerine analiz. (Turn1search2)
