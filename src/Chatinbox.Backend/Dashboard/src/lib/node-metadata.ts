/** Node metadata: help guides, output variables, edge labels, cron descriptions.
 *  Used by NodePropertyPanel and DeleteEdge for enriched UX. */

import type { FlowNodeType } from '../types/flow';

// ============================================================
// Node Help Guides (Turkish, non-technical)
// ============================================================

export interface NodeGuide {
  summary: string;
  detail: string;
  scenarios: string;
  antiPatterns: string;
}

export const NODE_GUIDES: Record<FlowNodeType, NodeGuide> = {
  trigger_start: {
    summary: 'Her akışın tek ve zorunlu giriş noktasıdır. Müşteri mesaj gönderdiğinde akış buradan başlar.',
    detail:
      'Başlangıç node\'u akışınızın ilk adımını belirler. Bir müşteriden mesaj geldiğinde sistem bu node\'u bulur ve akışı başlatır.\n\n' +
      'Her akışta yalnızca bir tane Başlangıç node\'u olabilir ve silinemez. Hangi WhatsApp hatlarından gelen mesajların bu akışı tetikleyeceğini seçebilirsiniz. Hat seçimi yapmazsanız tüm hatlar bu akışı kullanır.\n\n' +
      'Başlangıç node\'u doğrudan bir sonraki adıma bağlanır. Genellikle bir karşılama mesajı veya intent algılama adımına bağlanır.',
    scenarios:
      '- Müşteri destek akışı: Başlangıç → Karşılama mesajı → Intent Algılama\n' +
      '- Tek hat satış botu: Başlangıç (sadece satış hattı seçili) → Menü\n' +
      '- Çoklu hat: Başlangıç (tüm hatlar) → Mesai Saati kontrolü',
    antiPatterns:
      '- Birden fazla Başlangıç node\'u eklenemez (sistem otomatik engeller)\n' +
      '- Başlangıç\'tan sonra doğrudan Temsilciye Aktar koymak gereksiz — müşteri bot ile hiç konuşamaz\n' +
      '- Hat seçimi yapmadan bırakma: Tüm hatlar aynı akışı kullanır, bu genelde istenmez',
  },
  webhook_trigger: {
    summary: 'Dış sistemlerden gelen HTTP istekleriyle akışı başlatır. CRM, e-ticaret veya başka bir yazılım entegrasyonu için kullanılır.',
    detail:
      'Webhook tetikleyici, dış bir sistem (örneğin e-ticaret sitesi, CRM veya otomasyon aracı) belirli bir URL\'ye HTTP POST isteği gönderdiğinde akışı başlatır.\n\n' +
      'Gelen veri (payload) bir değişkene atanır ve akış boyunca kullanılabilir. Örneğin bir sipariş tamamlandığında e-ticaret sistemi bu webhook\'u tetikler ve sipariş bilgilerini gönderir.\n\n' +
      'Secret Key (gizli anahtar) ayarlayarak sadece yetkili sistemlerin tetikleme yapmasını sağlayabilirsiniz.',
    scenarios:
      '- E-ticaret sipariş bildirimi: Sipariş tamamlanınca webhook → Müşteriye "Siparişiniz alındı" mesajı\n' +
      '- CRM entegrasyonu: Yeni müşteri kaydı → Hoşgeldiniz mesajı\n' +
      '- Otomasyon aracı (Zapier/n8n): Dış event → Akış tetikleme',
    antiPatterns:
      '- Normal müşteri mesajları için webhook kullanmayın — bunun için Başlangıç node\'u var\n' +
      '- Secret Key\'siz bırakmayın — herkes tetikleyebilir\n' +
      '- Çok fazla veri göndermekten kaçının — sadece gerekli alanları gönderin',
  },
  outbound_trigger: {
    summary: 'Toplu mesaj kampanyaları için tetikleyicidir. Outbound modülü bir kampanya başlattığında bu akış devreye girer.',
    detail:
      'Outbound tetikleyici, toplu mesaj kampanyalarında kullanılır. Siz bir kampanya oluşturduğunuzda ve başlattığınızda, her bir müşteriye bu akış uygulanır.\n\n' +
      'Kampanya bilgileri (kampanya ID, müşteri verileri) otomatik olarak değişkenlere atanır. Bu sayede kişiselleştirilmiş mesajlar gönderebilirsiniz.\n\n' +
      'Örneğin "İndirim kampanyası" adında bir outbound kampanya oluşturdunuz — tetiklendiğinde her müşteriye özel indirim mesajı gönderilir.',
    scenarios:
      '- Toplu indirim bildirimi: Kampanya tetiklenir → Kişisel mesaj → Menü (ilgileniyor musunuz?)\n' +
      '- Hatırlatma kampanyası: Randevu hatırlatma → Onay/iptal seçeneği\n' +
      '- Yeniden pazarlama: Eski müşterilere tekrar ulaşma',
    antiPatterns:
      '- Tek tek müşteri mesajları için outbound kullanmayın — Başlangıç node\'u yeterli\n' +
      '- Kampanya içeriği akışa gömülüyorsa değişken kullanmayı unutmayın\n' +
      '- Çok sık kampanya gönderimi müşteri şikayet riski oluşturur',
  },
  schedule_trigger: {
    summary: 'Belirli zamanlarda otomatik olarak tetiklenir. Günlük, haftalık veya özel bir zaman diliminde akış başlatır.',
    detail:
      'Zamanlayıcı, cron ifadesi kullanarak belirli zamanlarda otomatik tetiklenen bir akış başlangıcıdır. Örneğin "Her gün sabah 9\'da" veya "Her Pazartesi saat 10\'da" gibi zamanlamalar yapabilirsiniz.\n\n' +
      'Cron ifadesi 5 alandan oluşur: dakika, saat, ayın günü, ay, haftanın günü. En yaygın kullanımlar için örnekler aşağıdadır.\n\n' +
      'Saat dilimini Türkiye olarak seçmeniz önemlidir, aksi halde farklı saatlerde tetiklenebilir.',
    scenarios:
      '- Günlük rapor: Her sabah 09:00 → Dünkü özet mesajı gönder\n' +
      '- Haftalık hatırlatma: Her Pazartesi 10:00 → Haftalık görev listesi\n' +
      '- Aylık fatura: Her ayın 1\'inde → Fatura hatırlatma mesajı',
    antiPatterns:
      '- Müşteri mesajlarına cevap vermek için zamanlayıcı kullanmayın — Başlangıç node\'u kullanın\n' +
      '- Çok sık tetikleme (her dakika gibi) sistem yükünü artırır\n' +
      '- Saat dilimini UTC bırakmayın — Türkiye saatini seçin',
  },
  message_text: {
    summary: 'Müşteriye düz metin mesajı gönderir. En temel ve en çok kullanılan adımdır.',
    detail:
      'Mesaj node\'u müşteriye yazıyla bir mesaj gönderir. Mesaj içinde değişkenler kullanabilirsiniz: örneğin {{musteri_adi}} yazarsanız müşterinin adı otomatik yerleştirilir.\n\n' +
      '"Kullanıcı yanıtını bekle" seçeneği açıkken, mesaj gönderildikten sonra akış durur ve müşterinin cevabını bekler. Müşterinin gönderdiği cevap {{user_input}} değişkenine atanır ve sonraki adımlarda kullanılabilir.\n\n' +
      'WhatsApp mesaj limiti 4096 karakterdir. Bu limiti aşmamaya dikkat edin.',
    scenarios:
      '- Karşılama: "Merhaba {{musteri_adi}}, size nasıl yardımcı olabilirim?"\n' +
      '- Bilgilendirme: "Siparişiniz kargoya verildi. Takip no: {{kargo_no}}"\n' +
      '- Soru sorma (yanıt bekle açık): "Randevu için hangi tarih uygun?" → Cevabı al → Devam et',
    antiPatterns:
      '- Çok uzun mesajlar göndermeyin — müşteriler okumaz (maks 4096 karakter)\n' +
      '- Art arda 3\'ten fazla mesaj node\'u koymayın — müşteri spam gibi algılar\n' +
      '- Yanıt beklemeniz gerekmiyorsa "Kullanıcı yanıtını bekle"yi açık bırakmayın — akışı gereksiz durdurur',
  },
  message_menu: {
    summary: 'Müşteriye seçenekli bir menü gösterir. Müşteri bir seçenek seçtikten sonra akış o dala devam eder.',
    detail:
      'Menü node\'u müşteriye bir mesaj ve altında seçenekler gönderir. Müşteri numarayı veya seçeneği yazdığında, akış o seçeneğin dalına yönlenir.\n\n' +
      'Her seçenek için bir "anahtar" (müşteri ne yazar) ve bir "etiket" (müşteri ne görür) tanımlanır. Örneğin anahtar: "1", etiket: "Satış". Müşteri "1" yazdığında Satış dalına gider.\n\n' +
      'Her seçeneğin kendine ait bir çıkış bağlantısı vardır. Böylece farklı seçenekler farklı adımlara yönlendirilir.',
    scenarios:
      '- Ana menü: "1-Satış, 2-Destek, 3-Bilgi" → Her biri farklı akışa gider\n' +
      '- Onay: "1-Evet, devam et / 2-Hayır, iptal" → İki farklı dal\n' +
      '- Ürün seçimi: "Hangi ürünle ilgileniyorsunuz?" → Ürün seçenekleri',
    antiPatterns:
      '- 10\'dan fazla seçenek koymayın — müşteri karar veremez\n' +
      '- Her seçeneğin bağlantısını yapın — bağlantısız seçenek akışı kırar\n' +
      '- Menü metnini boş bırakmayın — müşteri ne seçeceğini anlamaz',
  },
  logic_condition: {
    summary: 'Bir koşulu kontrol eder ve sonuca göre akışı iki dala ayırır: Doğru veya Yanlış.',
    detail:
      'Koşul node\'u bir değişkenin değerini kontrol eder. Örneğin "musteri_tipi eşittir VIP" koşulunu tanımlarsanız, VIP müşteriler Doğru dalına, diğerleri Yanlış dalına yönlenir.\n\n' +
      'Kullanabileceğiniz operatörler: Eşittir, İçerir, Başlar, Büyüktür, Küçüktür, Boş mu ve Regex. Her biri farklı karşılaştırma türü yapar.\n\n' +
      'Değişken olarak önceki adımlardan gelen herhangi bir değeri kullanabilirsiniz. Örneğin {{__last_input}} müşterinin son mesajını içerir.',
    scenarios:
      '- VIP kontrolü: musteri_tipi = "VIP" → Özel karşılama / Normal karşılama\n' +
      '- Mesaj içerik kontrolü: __last_input içerir "iptal" → İptal akışı\n' +
      '- Boş kontrol: telefon boş mu → Telefon sor / Devam et',
    antiPatterns:
      '- Karmaşık mantık için iç içe koşul koymak yerine Switch kullanın\n' +
      '- Değişken adını yanlış yazmamaya dikkat edin — eşleşme olmaz\n' +
      '- Her iki dalı da (Doğru/Yanlış) bağlayın — bağlantısız dal akışı kırar',
  },
  logic_switch: {
    summary: 'Bir değişkenin değerine göre akışı birden fazla dala ayırır. Çoklu seçenek için idealdir.',
    detail:
      'Switch node\'u bir değişkeni birden fazla değerle karşılaştırır. Hangi değer eşleşirse o daldan devam eder. Hiçbiri eşleşmezse Varsayılan dalına gider.\n\n' +
      'Örneğin "departman" değişkeni "satis", "destek" veya "muhasebe" olabilir. Her biri farklı bir dala yönlendirilir. Tanımadığı bir değer gelirse Varsayılan dalı devreye girer.\n\n' +
      'Koşul node\'undan farkı: Koşul sadece Doğru/Yanlış (2 dal) verir, Switch ise istediğiniz kadar dal oluşturabilir.',
    scenarios:
      '- Departman yönlendirme: satis/destek/muhasebe/varsayilan\n' +
      '- Dil seçimi: tr/en/de → Farklı dilde mesajlar\n' +
      '- Sipariş durumu: yeni/hazir/kargoda/teslim → Farklı bilgilendirmeler',
    antiPatterns:
      '- Sadece 2 durum varsa Switch yerine Koşul kullanın — daha basit\n' +
      '- Varsayılan dalını bağlantısız bırakmayın — bilinmeyen değerler kaybolur\n' +
      '- Maks 10 durum sınırını aşmayın — çok fazla dal yönetimi zorlaştırır',
  },
  logic_working_hours: {
    summary: 'Mesai saati içi mi dışı mı kontrol eder ve buna göre dallanır.',
    detail:
      'Mesai Saati node\'u, tenant (işletme) ayarlarında tanımlı olan çalışma saatlerini kullanır. Müşteri mesajı mesai saatleri içinde geldiyse "Mesai İçi" dalına, dışında geldiyse "Mesai Dışı" dalına yönlenir.\n\n' +
      'Bu node\'un kendisinde ayar yoktur — çalışma saatleri işletme ayarlarından otomatik alınır. Bu sayede merkezi bir yerden yönetim sağlanır.\n\n' +
      'Mesai dışı dalında genellikle "Şu anda mesai saatleri dışındayız, en kısa sürede döneceğiz" gibi bir mesaj gönderilir.',
    scenarios:
      '- Mesai içi: Normal akış (bot + temsilci) / Mesai dışı: Otomatik mesaj\n' +
      '- Acil durumlar: Mesai dışı → Acil mi? → Evet → Nöbetçi temsilciye aktar\n' +
      '- Farklı hizmet: Mesai içi → Canlı destek / Mesai dışı → FAQ botu',
    antiPatterns:
      '- İşletme ayarlarında çalışma saatleri tanımlanmadan bu node\'u kullanmayın\n' +
      '- Mesai dışı dalını boş bırakmayın — müşteri cevapsız kalır\n' +
      '- 7/24 hizmet veren işletmelerde gereksiz — akışı karmaşıklaştırır',
  },
  ai_intent: {
    summary: 'Müşterinin ne istediğini yapay zeka ile otomatik anlar ve doğru dala yönlendirir.',
    detail:
      'Intent Algılama, müşterinin mesajını yapay zeka (Claude AI) ile analiz eder ve hangi konuyla ilgilendiğini (intent) tespit eder. Örneğin müşteri "fiyat ne kadar?" yazdığı zaman AI bunu "fiyat_bilgisi" intent\'i olarak algılar.\n\n' +
      'Tanımlayacağınız her intent için bir çıkış dalı oluşur. AI müşterinin mesajını analiz eder ve en uygun intent\'e yönlendirir. Güven eşiği altında kalan mesajlar "Diğer" dalına gider.\n\n' +
      '"Müşteri ismini sor" seçeneği açıkken, AI önce müşterinin adını sorar, doğrular ve sohbet boyunca ismiyle hitap eder. Bu daha samimi bir deneyim sağlar.',
    scenarios:
      '- Satış botu: satin_alma / fiyat_sorgulama / iade / diger\n' +
      '- Destek botu: teknik_sorun / fatura / sikayet / diger\n' +
      '- Genel bot: randevu / bilgi / sikayet / insan_ile_gorusme / diger',
    antiPatterns:
      '- Çok fazla intent (10+) tanımlamayın — AI\'nin doğru seçme oranı düşer\n' +
      '- Birbirine çok benzer intent\'ler tanımlamayın (örnek: "satin_alma" ve "alis") — karışıklık yaratır\n' +
      '- Güven eşiğini çok yüksek tutmayın (90%+) — çoğu mesaj "Diğer"e düşer\n' +
      '- Intent isimlerini Türkçe ve anlaşılır yazın',
  },
  ai_faq: {
    summary: 'Bilgi bankasında müşteri sorusuna cevap arar. Eşleşen cevabı otomatik gönderir.',
    detail:
      'FAQ Arama, müşterinin sorusunu bilgi bankasındaki (FAQ ve dökümanlar) kayıtlarla karşılaştırır. Semantik arama kullanır — yani kelime kelime değil, anlam bazlı eşleştirir.\n\n' +
      'Örneğin müşteriniz "iade süresi ne kadar?" diye sorduysa ve bilgi bankanızda "İade Politikası: Ürünler 14 gün içinde iade edilebilir" kaydı varsa, bu eşleşti olarak bulunur ve cevap gönderilir.\n\n' +
      'FAQ eşleşirse cevap doğrudan gönderilir. Döküman eşleşirse AI ile özetlenip gönderilir. Minimum güven eşiği altındaki sonuçlar "Eşleşmedi" dalına yönlenir ve başka bir adım devreye girer.',
    scenarios:
      '- Müşteri destek: Soru → FAQ\'da ara → Bulursa cevapla / Bulamazsa temsilciye aktar\n' +
      '- Bilgi botu: Soru → FAQ + Döküman ara → Cevapla / "Bu konuda bilgim yok"\n' +
      '- Hibrit: Intent Algılama → "bilgi" intent\'i → FAQ Arama → Temsilci',
    antiPatterns:
      '- Bilgi bankası boşsa bu node işe yaramaz — önce içerik ekleyin\n' +
      '- Minimum güveni çok düşük tutmayın (20% altı) — alakasız cevaplar gider\n' +
      '- Minimum güveni çok yüksek tutmayın (90%+) — çoğu soru eşleşmez\n' +
      '- FAQ yerine genel sohbet botu olarak kullanmayın — bunun için Intent Algılama var',
  },
  ai_sentiment: {
    summary: 'Müşterinin duygusal durumunu yapay zeka ile analiz eder: olumlu mu olumsuz mu?',
    detail:
      'Duygu Analizi, müşterinin mesajındaki duygu tonunu AI ile tespit eder. Skor eşik değerinin üzerindeyse "Pozitif" dalına, altındaysa "Negatif" dalına yönlenir.\n\n' +
      'Bu sayede kızgın veya mutsuz müşterileri erken tespit edip özel ilgi gösterebilirsiniz. Örneğin negatif duygu tespit edildiğinde doğrudan temsilciye aktarma yapılabilir.\n\n' +
      'Eşik değeri 0-100% arasında ayarlanır. %50 varsayılan değerdir. Daha hassas tespit için eşiği düşük tutabilirsiniz.',
    scenarios:
      '- Şikayet yönetimi: Negatif → Temsilciye hemen aktar / Pozitif → Normal akış\n' +
      '- Memnuniyet ölçümü: Hizmet sonrası → Duygu analizi → Negatif → Özür + indirim teklifi\n' +
      '- Escalation: Negatif → "Sizi anlıyoruz, hemen bir yetkiliye bağlıyorum"',
    antiPatterns:
      '- Her mesajda duygu analizi yapmayın — gereksiz yavaştır\n' +
      '- Tek kelimelik cevaplarda ("evet", "hayır") doğru sonuç vermeyebilir\n' +
      '- Sadece duygu analizine dayanarak karar vermeyin — başka verilerle birleştirin',
  },
  action_handoff: {
    summary: 'Sohbeti canlı bir temsilciye yönlendirir. Akış burada sona erer.',
    detail:
      'Temsilciye Aktar, bot ile müşteri arasındaki sohbeti sonlandırıp canlı bir temsilciye devreder. Bu bir "terminal" node\'dur — yani bu adımdan sonra akış devam etmez.\n\n' +
      'Özet şablonu alanına, temsilcinin göreceği özet bilgiyi yazabilirsiniz. Değişkenler kullanarak müşterinin adı, sorunu gibi bilgileri özetleyebilirsiniz. Örneğin: "{{musteri_adi}} - {{__detected_intent}} hakkında destek istiyor".\n\n' +
      'Temsilci sohbete bağlandığında müşterinin önceki mesajlarını ve bu özeti görebilir.',
    scenarios:
      '- Karmaşık sorunlar: Bot cevaplayamadı → Temsilciye aktar\n' +
      '- Müşteri isteği: "İnsanla konuşmak istiyorum" → Doğrudan aktar\n' +
      '- Kızgın müşteri: Duygu analizi negatif → Hemen temsilciye aktar',
    antiPatterns:
      '- Her akışın sonuna koymak zorunda değilsiniz — FAQ ile çözülen sorular için gereksiz\n' +
      '- Bu node\'dan sonra başka bir adım bağlamayın — çalıştırılmaz\n' +
      '- Özet boş bırakmayın — temsilci müşterinin neyle ilgilendiğini anlamaz',
  },
  action_assign_group: {
    summary: 'Sohbeti belirli bir temsilci grubuna yönlendirir. Departman bazlı dağıtım için idealdir.',
    detail:
      'Gruba Ata, sohbeti belirli bir INMA temsilci grubuna yönlendirir. Temsilciye Aktar\'dan farkı, rastgele bir temsilci yerine belirli bir gruba (örneğin "Satış Ekibi" veya "Teknik Destek") yönlendirmesidir.\n\n' +
      'Grup ID\'si INMA\'dan alınır. Grup adı sadece görsel amaçlıdır — siz ve ekibiniz için okunabilirlik sağlar.\n\n' +
      'Özet şablonu ile temsilciye aktarılacak bilgileri özelleştirebilirsiniz.',
    scenarios:
      '- Departman yönlendirme: Satış intent\'i → Satış grubuna ata\n' +
      '- VIP müşteri: VIP kontrolü → Özel destek grubuna ata\n' +
      '- Teknik sorun: Teknik intent → Teknik destek grubuna ata',
    antiPatterns:
      '- Grup ID\'sini boş bırakmayın — yönlendirme çalışmaz\n' +
      '- Tek temsilci grubunuz varsa Temsilciye Aktar yeterli — Gruba Ata gereksiz\n' +
      '- Bu node\'dan sonra başka adım bağlamayın — terminal node\'dur',
  },
  action_api_call: {
    summary: 'Dış bir web servisine HTTP isteği gönderir ve cevabını değişkene atar.',
    detail:
      'API Çağrısı, akış sırasında dış bir web servisine (API) istek gönderir. Örneğin bir CRM\'den müşteri bilgisi çekme, sipariş sisteminize durum sorgulama veya başka bir servise veri gönderme için kullanılır.\n\n' +
      'HTTP metodu (GET/POST/PUT/DELETE), URL, başlıklar ve gönderilecek veri (body) tanımlanır. Cevap belirtilen değişkene atanır ve sonraki adımlarda kullanılabilir.\n\n' +
      'Zaman aşımı ayarı önemlidir — çok kısa tutarsanız yavaş API\'ler zaman aşımına uğrar, çok uzun tutarsanız müşteri bekler.',
    scenarios:
      '- Müşteri bilgisi: GET /api/customers/{{phone}} → Bilgiyi mesajda kullan\n' +
      '- Sipariş oluşturma: POST /api/orders → Sipariş numarasını müşteriye gönder\n' +
      '- Stok kontrolü: GET /api/stock/{{urun_id}} → Stok durumunu göster',
    antiPatterns:
      '- URL\'yi boş bırakmayın — istek gönderilemez\n' +
      '- Zaman aşımını 30 saniyeye çıkartmayın — müşteri bu kadar beklemez\n' +
      '- Hassas bilgileri (şifre, API key) doğrudan URL\'ye yazmayın — başlık (header) kullanın\n' +
      '- Hata dalını bağlantısız bırakmamaya dikkat edin — API hatası akışı kırar',
  },
  action_delay: {
    summary: 'Akışı belirli bir süre duraklatır. Doğal bir konuşma temposu için kullanılır.',
    detail:
      'Bekle node\'u akışı tanımladığınız süre kadar duraklatır. Bu süre boyunca müşteriye bir şey gönderilmez.\n\n' +
      'En yaygın kullanım: Art arda mesaj göndermek yerine araya küçük beklemeler koyarak daha doğal bir sohbet temposu oluşturmak.\n\n' +
      'Minimum 1 saniye, maksimum 300 saniye (5 dakika) bekleyebilir. Genellikle 1-3 saniye yeterlidir.',
    scenarios:
      '- Doğal tempo: Mesaj → 2sn bekle → Mesaj (spam gibi görünmez)\n' +
      '- İşlem süresi simülasyonu: "Siparişinizi kontrol ediyorum..." → 3sn bekle → Sonuç\n' +
      '- Ardışık mesaj: Mesaj 1 → 1sn → Mesaj 2 → 1sn → Menü',
    antiPatterns:
      '- 10 saniyeden uzun beklemeler koymayın — müşteri botu bozuldu sanır\n' +
      '- Her mesaj arasına bekle koymak zorunda değilsiniz\n' +
      '- Kritik işlemlerde (temsilciye aktarma gibi) gecikme eklemeyin',
  },
  action_call_flow: {
    summary: 'Başka bir akışı alt program gibi çağırıp sonucunu bekler. Tekrar kullanılabilir akışlar için idealdir.',
    detail:
      'Alt Flow, mevcut akışın içinden başka bir akışı çağırır. Alt akış tamamlandığında ana akış kaldığı yerden devam eder.\n\n' +
      'Girdi eşleme ile ana akıştan alt akışa değişken aktarabilirsiniz. Çıktı eşleme ile alt akıştan ana akışa sonuç alabilirsiniz. Örneğin müşteri bilgilerini alt akışa gönderip, alt akışın topladığı verileri geri alabilirsiniz.\n\n' +
      'Alt akış başarıyla tamamlanırsa "Tamamlandı" dalına, hata olursa "Hata" dalına yönlenir.',
    scenarios:
      '- Müşteri doğrulama alt akışı: Ana akış → Doğrulama akışı çağır → Doğrulandı/Başarısız\n' +
      '- Ortak FAQ: Birden fazla akış aynı FAQ alt akışını çağırabilir\n' +
      '- Sipariş süreci: Ana akış → Sipariş alt akışı → Sonuca göre devam',
    antiPatterns:
      '- Kendini çağıran döngüsel akış oluşturamazsınız — sonsuz döngü oluşur\n' +
      '- Çok derin zincirleme (akış → alt akış → alt alt akış) kaçınılmalı — debug zorlaşır\n' +
      '- Alt akışın hata dalını bağlamayı unutmayın\n' +
      '- Basit işler için alt akış kullanmayın — doğrudan ana akışta yapın',
  },
  utility_set_variable: {
    summary: 'Bir değişkene değer atar veya mevcut değeri değiştirir. Veri taşıma ve hesaplama için kullanılır.',
    detail:
      'Değişken Ata, akış içinde kullanılacak bir değişkene değer verir. Bu değer sabit bir metin, başka bir değişkenin değeri veya bir ifade olabilir.\n\n' +
      'Örneğin müşteri türünü belirleyip "musteri_tipi" değişkenine "VIP" atayabilir, sonraki adımlarda bu değişkeni kullanabilirsiniz.\n\n' +
      'Değer ifadesinde {{degisken}} yazarak mevcut değişkenlere referans verebilirsiniz. Örneğin "Merhaba {{musteri_adi}}" gibi birleşik değerler oluşturabilirsiniz.',
    scenarios:
      '- Bayrak atama: musteri_tipi = "VIP" → Sonraki koşulda kullan\n' +
      '- Birleşik değer: tam_ad = "{{ad}} {{soyad}}"\n' +
      '- Sayaç: deneme_sayisi = "{{deneme_sayisi + 1}}"',
    antiPatterns:
      '- Değişken adını boş bırakmayın — atama yapılmaz\n' +
      '- Aynı değişkeni çok fazla yerde değiştirmeyin — takip zorlaşır\n' +
      '- Değişken isimlerini anlaşılır ve tutarlı yazın (örnek: musteri_adi, siparis_no)',
  },
  utility_note: {
    summary: 'Akışa görsel yorum ekler. Çalıştırılmaz, sadece siz ve ekibiniz için açıklama amaçlıdır.',
    detail:
      'Not node\'u akış içinde çalıştırılmayan, sadece görsel bir açıklama kutusudur. Karmaşık akışlarda belirli bölgelerin ne yaptığını açıklamak için kullanılır.\n\n' +
      'Renk seçenekleri ile farklı konuları (uyarı, bilgi, önemli not) görsel olarak ayırt edebilirsiniz. Sarı: genel not, Kırmızı: uyarı, Mavi: bilgi, Yeşil: onay, Mor: özel not.\n\n' +
      'Not node\'unu herhangi bir yere koyabilirsiniz — akışı etkilemez, bağlantı yapmanız gerekmez.',
    scenarios:
      '- Açıklama: "Bu bölüm VIP müşteriler için özel akış"\n' +
      '- Uyarı (kırmızı): "DİKKAT: Bu API çağrısında hata olursa temsilciye aktarılır"\n' +
      '- Hatırlatma (sarı): "Bu kısımdaki mesaj metnini güncellemeyi unutma"',
    antiPatterns:
      '- Not node\'unu akışa bağlamayın — gereksiz bağlantı oluşturur\n' +
      '- Her adıma not eklemek akışı karmaşık gösterir — sadece gerekli yerlere ekleyin\n' +
      '- Not içine teknik bilgi (değişken adları vs.) yazmak yerine anlaşılır açıklama yazın',
  },
  action_ecommerce: {
    summary: 'E-ticaret platformunda sipariş, ürün ve müşteri işlemleri yapar.',
    detail:
      'E-Ticaret node\'u, entegre edilmiş e-ticaret platformunda (ikas vb.) işlem yapmanızı sağlar. Sipariş listeleme, ürün sorgulama, kargolama, durum güncelleme ve iade gibi işlemleri akış içinden otomatik olarak gerçekleştirir.\n\n' +
      'Her işlem için gerekli alanlar değişir. Örneğin sipariş detayı için sipariş ID gerekli, kargolama için sipariş ID + takip kodu + kargo firması gereklidir.\n\n' +
      'Sonuç belirtilen değişkene atanır. Başarılı işlemler "success" dalına, hatalar "error" dalına yönlenir.',
    scenarios:
      '- Sipariş sorgulama: Müşteri telefonu ile sipariş ara → Sonucu mesajla gönder\n' +
      '- Kargo bildirimi: Sipariş ID ile kargola → Takip numarasını müşteriye gönder\n' +
      '- Ürün bilgisi: Ürün ID ile detay al → Fiyat ve stok bilgisini göster\n' +
      '- İade işlemi: Sipariş + kalem ID ile iade → Sonucu bildir',
    antiPatterns:
      '- Sipariş ID\'sini boş bırakmayın — detay/kargolama/iade çalışmaz\n' +
      '- Kargolama için takip kodu ve kargo firması zorunlu\n' +
      '- Hata dalını bağlantısız bırakmayın — API hatası akışı kırar\n' +
      '- Çok fazla e-ticaret node\'u art arda koymayın — her biri API çağrısı yapar',
  },
  customer_status_changed: {
    summary: 'Bir müşterinin INMA durumu (feature grubu) değiştiğinde akışı başlatır. Örneğin müşteri "Teklif Verildi" olduğunda otomatik WhatsApp gönderebilirsiniz.',
    detail:
      'Müşteri Durumu Değişti tetikleyicisi, INMA panelinde bir müşterinin durumu (feature grubu seçimi) değiştiğinde akışı başlatır. Bir feature grubu seçerseniz, akış SADECE o gruptaki herhangi bir durum değiştiğinde tetiklenir.\n\n' +
      'Grup seçmezseniz (varsayılan), akış HERHANGİ bir durum değişikliğinde tetiklenir. Önemli: tetikleme grup seviyesindedir — gruptaki tek bir duruma (örneğin sadece "Teklif Verildi" olduğunda) göre filtreleme yapılamaz; gruptaki her değişiklikte çalışır.\n\n' +
      'Tetiklendiğinde eski durum, yeni durum, grup adı ve değişikliği yapan kişi otomatik olarak değişkenlere atanır ve sonraki adımlarda kullanılabilir. Yalnızca panel kullanıcılarının (operatör) yaptığı değişiklikler tetikler; sistemin/API\'nin kendi yazdığı değişiklikler sonsuz döngüyü önlemek için tetiklemez.',
    scenarios:
      '- Satış takibi: Müşteri "Teklif Verildi" olunca → "Teklifinizle ilgili sorularınız mı var?" mesajı\n' +
      '- Onboarding: Müşteri "Yeni Müşteri" olunca → Karşılama + sonraki adımlar\n' +
      '- Kaybedilen fırsat: Durum "Kaybedildi" grubuna geçince → Geri kazanım kampanyası',
    antiPatterns:
      '- Tek bir duruma göre filtrelemek istemeyin — tetikleme grup seviyesindedir, sonraki adımda {{new_customer_status}} ile dallanın\n' +
      '- Metin tipli grupları seçmeyin — onlar otomasyon tetikleyemez (bilgi notu gösterilir)\n' +
      '- WapCRM bağlantısı yoksa grup listesi gelmez; akış yine de "her değişiklikte" çalışır',
  },
  action_set_customer_status: {
    summary: 'Bir müşterinin INMA durumunu (feature grubu seçimini) akış içinden günceller. Örneğin müşteri randevu alınca durumunu otomatik "Randevu" yapabilirsiniz.',
    detail:
      'Müşteri Durumu Ata node\'u, seçtiğiniz INMA feature grubunun seçimini cxapi üzerinden günceller (audit kaydında Source="api").\n\n' +
      'DİKKAT — TAM LİSTE mantığı: gönderdiğiniz özellikler o grubun YENİ TAM seçimidir. Çoklu-seçim gruplarında seçmediğiniz özellikler grupta KALDIRILIR (üzerine yazma). Tek-seçim gruplarında tek değer atanır. Hiç özellik seçmezseniz grup seçimi TEMİZLENİR.\n\n' +
      'Müşteri kimliği akış bağlamından gelir: varsa INMA müşteri kimliği (öncelikli), yoksa telefon. Bağlam kimlik taşımıyorsa node "error" dalına düşer (yazma yapılmaz). Başarılı güncelleme "success", hata "error" dalına yönlenir. Metin tipli (selectionMode=3) gruplar desteklenmez (sağlayıcı yazma API\'si metin almıyor) — picker\'da pasiftir.',
    scenarios:
      '- Pipeline ilerletme: Müşteri randevu onaylayınca → durumu "Randevu" yap\n' +
      '- Otomatik etiketleme: Belirli bir cevaptan sonra → "İlgileniyor" durumunu ata\n' +
      '- Temizleme: Süreç bitince → grubun seçimini temizle (özellik seçmeyin)',
    antiPatterns:
      '- Çoklu-seçim grubunda "tek durum ekle" sanmayın — TÜM grup seçimi değiştirilir, seçmedikleriniz silinir\n' +
      '- "error" dalını bağlantısız bırakmayın — yazma başarısız olursa akış sessiz kalmasın\n' +
      '- Metin tipli grupları kullanmaya çalışmayın — sağlayıcı desteklemiyor\n' +
      '- Karşılama/cron gibi telefon taşımayan bağlamlarda beklemeyin — kimlik yoksa "error" dalına düşer',
  },
};

// ============================================================
// Node Output Variables
// ============================================================

export interface NodeOutputVar {
  name: string;
  description: string;
}

export const NODE_OUTPUT_VARS: Partial<Record<FlowNodeType, NodeOutputVar[]>> = {
  trigger_start: [
    { name: '__chat_id', description: 'Sohbet kimlik numarası' },
    { name: '__phone', description: 'Müşteri telefon numarası' },
    { name: '__last_input', description: 'Müşterinin son mesajı' },
    { name: '__instance_id', description: 'Mesajın geldiği hat ID' },
  ],
  webhook_trigger: [
    { name: 'webhook_payload', description: 'Gelen webhook verisi (JSON)' },
  ],
  outbound_trigger: [
    { name: 'campaign_id', description: 'Kampanya kimlik numarası' },
  ],
  schedule_trigger: [
    { name: '__trigger_time', description: 'Tetiklenme zamanı' },
  ],
  message_text: [
    { name: 'user_input', description: 'Müşterinin cevabı (yanıt bekle açıksa)' },
  ],
  message_menu: [
    { name: '__selected_option', description: 'Seçilen seçenek etiketi' },
    { name: '__selected_key', description: 'Seçilen seçenek anahtarı' },
  ],
  ai_intent: [
    { name: '__detected_intent', description: 'Tespit edilen intent adı' },
    { name: '__intent_confidence', description: 'Güven skoru (0-1)' },
    { name: '__customer_name', description: 'Müşteri adı (isim sorma açıksa)' },
  ],
  ai_faq: [
    { name: '__faq_answer', description: 'Bulunan cevap metni' },
    { name: '__faq_confidence', description: 'Eşleşme güven skoru' },
  ],
  ai_sentiment: [
    { name: '__sentiment_score', description: 'Duygu skoru (0-1)' },
    { name: '__sentiment_label', description: 'Duygu etiketi (positive/negative)' },
  ],
  action_api_call: [
    { name: 'api_response', description: 'API cevap verisi' },
    { name: '__api_status_code', description: 'HTTP durum kodu' },
  ],
  action_ecommerce: [
    { name: 'ecom_result', description: 'E-ticaret işlem sonucu (JSON)' },
  ],
  utility_set_variable: [
    { name: '(kullanıcı tanımlı)', description: 'Atanan değişken adı ve değeri' },
  ],
  customer_status_changed: [
    { name: 'customer_status_group', description: 'Değişen feature grubu adı' },
    { name: 'old_customer_status', description: 'Önceki durum' },
    { name: 'new_customer_status', description: 'Yeni durum' },
    { name: 'customer_status_changed_by', description: 'Değişikliği yapan kişi' },
  ],
  action_set_customer_status: [
    { name: 'set_status_code', description: 'Sonuç kodu (başarıda 200)' },
    { name: 'set_status_error', description: 'Hata mesajı (başarıda boş)' },
  ],
};

// ============================================================
// System Variables (always available)
// ============================================================

export interface SystemVariable {
  name: string;
  description: string;
}

export const SYSTEM_VARIABLES: SystemVariable[] = [
  { name: '__last_input', description: 'Müşterinin son mesajı' },
  { name: '__chat_id', description: 'Sohbet kimlik numarası' },
  { name: '__phone', description: 'Müşteri telefon numarası' },
  { name: '__instance_id', description: 'Mesaj gelen hat ID' },
  { name: '__customer_name', description: 'Müşteri adı (varsa)' },
  { name: '__timestamp', description: 'İşlem zamanı' },
];

// ============================================================
// Edge Label Mapping
// ============================================================

/** Static handle → label mapping for known node types */
export const EDGE_HANDLE_LABELS: Partial<Record<FlowNodeType, Record<string, string>>> = {
  logic_condition: {
    true_handle: 'DOĞRU',
    false_handle: 'YANLIŞ',
  },
  logic_working_hours: {
    within_hours: 'MESAİ İÇİ',
    outside_hours: 'MESAİ DIŞI',
  },
  ai_intent: {
    high_confidence: 'ALGILANDI',
    low_confidence: 'DİĞER',
  },
  ai_faq: {
    matched: 'EŞLEŞTİ',
    no_match: 'EŞLEŞMEDİ',
  },
  ai_sentiment: {
    positive: 'POZİTİF',
    negative: 'NEGATİF',
  },
  action_api_call: {
    success: 'BAŞARILI',
    error: 'HATA',
  },
  action_call_flow: {
    completed: 'TAMAMLANDI',
    error: 'HATA',
  },
  action_ecommerce: {
    success: 'BAŞARILI',
    error: 'HATA',
  },
};

/** Resolve an edge label from source node type, handle, and node data.
 *  For dynamic handles (menu options, switch cases) we look into node data. */
export function resolveEdgeLabel(
  sourceNodeType: FlowNodeType | undefined,
  sourceHandle: string | null | undefined,
  sourceNodeData: Record<string, unknown> | undefined,
): string | null {
  if (!sourceNodeType || !sourceHandle) return null;

  // Static mapping
  const staticMap = EDGE_HANDLE_LABELS[sourceNodeType];
  if (staticMap && staticMap[sourceHandle]) return staticMap[sourceHandle];

  // Dynamic: message_menu options
  if (sourceNodeType === 'message_menu' && sourceNodeData) {
    const options = sourceNodeData.options as Array<{ handle_id: string; label: string }> | undefined;
    const opt = options?.find(o => o.handle_id === sourceHandle);
    if (opt) return opt.label;
  }

  // Dynamic: logic_switch cases
  if (sourceNodeType === 'logic_switch' && sourceNodeData) {
    const cases = sourceNodeData.cases as Array<{ handle_id: string; value: string }> | undefined;
    const c = cases?.find(cs => cs.handle_id === sourceHandle);
    if (c) return c.value || '(bos)';
    const defaultId = (sourceNodeData.default_handle_id as string) || 'default';
    if (sourceHandle === defaultId) return 'VARSAYILAN';
  }

  return null;
}

// ============================================================
// Cron Description (Turkish, human-readable)
// ============================================================

export function describeCron(expression: string): string | null {
  if (!expression || !expression.trim()) return null;
  const parts = expression.trim().split(/\s+/);
  if (parts.length !== 5) return 'Geçersiz cron ifadesi (5 alan olmalı)';

  const [minute, hour, dayOfMonth, month, dayOfWeek] = parts;

  // Common patterns
  if (minute === '*' && hour === '*') return 'Her dakika';
  if (minute.startsWith('*/')) {
    const n = parseInt(minute.slice(2), 10);
    if (!isNaN(n)) return `Her ${n} dakikada bir`;
  }
  if (hour.startsWith('*/')) {
    const n = parseInt(hour.slice(2), 10);
    if (!isNaN(n)) return `Her ${n} saatte bir`;
  }

  const hourNum = parseInt(hour, 10);
  const minuteNum = parseInt(minute, 10);
  if (isNaN(hourNum) || isNaN(minuteNum)) return formatRawCron(parts);

  const timeStr = `${String(hourNum).padStart(2, '0')}:${String(minuteNum).padStart(2, '0')}`;

  // Every day at specific time
  if (dayOfMonth === '*' && month === '*' && dayOfWeek === '*') {
    return `Her gün saat ${timeStr}`;
  }

  // Weekdays
  if (dayOfMonth === '*' && month === '*' && (dayOfWeek === '1-5' || dayOfWeek === 'MON-FRI')) {
    return `Hafta içi her gün saat ${timeStr}`;
  }

  // Specific day of week
  if (dayOfMonth === '*' && month === '*' && dayOfWeek !== '*') {
    const dayName = getDayName(dayOfWeek);
    if (dayName) return `Her ${dayName} saat ${timeStr}`;
  }

  // Specific day of month
  if (dayOfMonth !== '*' && month === '*' && dayOfWeek === '*') {
    const dom = parseInt(dayOfMonth, 10);
    if (!isNaN(dom)) return `Her ayın ${dom}. günü saat ${timeStr}`;
  }

  return formatRawCron(parts);
}

function getDayName(day: string): string | null {
  const map: Record<string, string> = {
    '0': 'Pazar', '7': 'Pazar', '1': 'Pazartesi', '2': 'Salı',
    '3': 'Çarşamba', '4': 'Perşembe', '5': 'Cuma', '6': 'Cumartesi',
    'SUN': 'Pazar', 'MON': 'Pazartesi', 'TUE': 'Salı',
    'WED': 'Çarşamba', 'THU': 'Perşembe', 'FRI': 'Cuma', 'SAT': 'Cumartesi',
  };
  return map[day.toUpperCase()] ?? null;
}

function formatRawCron(parts: string[]): string {
  return `Cron: ${parts.join(' ')} (özel zamanlama)`;
}
