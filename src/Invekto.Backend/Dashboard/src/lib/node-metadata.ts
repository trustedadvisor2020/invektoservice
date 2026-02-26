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
    summary: 'Her akisin tek ve zorunlu giris noktasidir. Musteri mesaj gonderdiginde akis buradan baslar.',
    detail:
      'Baslangic node\'u akisinizin ilk adimini belirler. Bir musteriden mesaj geldiginde sistem bu node\'u bulur ve akisi baslatir.\n\n' +
      'Her akista yalnizca bir tane Baslangic node\'u olabilir ve silinemez. Hangi WhatsApp hatlarindan gelen mesajlarin bu akisi tetikleyecegini secebilirsiniz. Hat secimi yapmazsaniz tum hatlar bu akisi kullanir.\n\n' +
      'Baslangic node\'u dogrudan bir sonraki adima baglanir. Genellikle bir karsilama mesaji veya intent algilama adimina baglanir.',
    scenarios:
      '- Musteri destek akisi: Baslangic → Karsilama mesaji → Intent Algilama\n' +
      '- Tek hat satis botu: Baslangic (sadece satis hatti secili) → Menu\n' +
      '- Coklu hat: Baslangic (tum hatlar) → Mesai Saati kontrolu',
    antiPatterns:
      '- Birden fazla Baslangic node\'u eklenemez (sistem otomatik engeller)\n' +
      '- Baslangic\'tan sonra dogrudan Temsilciye Aktar koymak gereksiz — musteri bot ile hic konusamaz\n' +
      '- Hat secimi yapmadan birakma: Tum hatlar ayni akisi kullanir, bu genelde istenmez',
  },
  webhook_trigger: {
    summary: 'Dis sistemlerden gelen HTTP istekleriyle akisi baslatir. CRM, e-ticaret veya baska bir yazilim entegrasyonu icin kullanilir.',
    detail:
      'Webhook tetikleyici, dis bir sistem (ornegin e-ticaret sitesi, CRM veya otomasyon araci) belirli bir URL\'ye HTTP POST istegi gonderdiginde akisi baslatir.\n\n' +
      'Gelen veri (payload) bir degiskene atanir ve akis boyunca kullanilabilir. Ornegin bir siparis tamamlandiginda e-ticaret sistemi bu webhook\'u tetikler ve siparis bilgilerini gonderir.\n\n' +
      'Secret Key (gizli anahtar) ayarlayarak sadece yetkili sistemlerin tetikleme yapmasini saglayabilirsiniz.',
    scenarios:
      '- E-ticaret siparis bildirimi: Siparis tamamlaninca webhook → Musteriye "Siparisniniz alindi" mesaji\n' +
      '- CRM entegrasyonu: Yeni musteri kaydi → Hos geldiniz mesaji\n' +
      '- Otomasyon araci (Zapier/n8n): Dis event → Akis tetikleme',
    antiPatterns:
      '- Normal musteri mesajlari icin webhook kullanmayin — bunun icin Baslangic node\'u var\n' +
      '- Secret Key\'siz birakmayin — herkes tetikleyebilir\n' +
      '- Cok fazla veri gondermekten kacinin — sadece gerekli alanlari gonderin',
  },
  outbound_trigger: {
    summary: 'Toplu mesaj kampanyalari icin tetikleyicidir. Outbound modulu bir kampanya baslattiginda bu akis devreye girer.',
    detail:
      'Outbound tetikleyici, toplu mesaj kampanyalarinda kullanilir. Siz bir kampanya olusturdugunda ve baslattiginizda, her bir musteriye bu akis uygulanir.\n\n' +
      'Kampanya bilgileri (kampanya ID, musteri verileri) otomatik olarak degiskenlere atanir. Bu sayede kisisellestirilmis mesajlar gonderebilirsiniz.\n\n' +
      'Ornegin "Indirim kampanyasi" adinda bir outbound kampanya olusturdunuz — tetiklendiginde her musteriye ozel indirim mesaji gonderilir.',
    scenarios:
      '- Toplu indirim bildirimi: Kampanya tetiklenir → Kisisel mesaj → Menu (ilgileniyor musunuz?)\n' +
      '- Hatirlatma kampanyasi: Randevu hatirlatma → Onay/iptal secenegi\n' +
      '- Yeniden pazarlama: Eski musterilere tekrar ulasma',
    antiPatterns:
      '- Tek tek musteri mesajlari icin outbound kullanmayin — Baslangic node\'u yeterli\n' +
      '- Kampanya icerigi akisa gomuluyorsa degisken kullanmayi unutmayin\n' +
      '- Cok sik kampanya gonderimi musteri sikayet riski olusturur',
  },
  schedule_trigger: {
    summary: 'Belirli zamanlarda otomatik olarak tetiklenir. Gunluk, haftalik veya ozel bir zaman diliminde akis baslatir.',
    detail:
      'Zamanlayici, cron ifadesi kullanarak belirli zamanlarda otomatik tetiklenen bir akis baslangicidir. Ornegin "Her gun sabah 9\'da" veya "Her Pazartesi saat 10\'da" gibi zamanlamalar yapabilirsiniz.\n\n' +
      'Cron ifadesi 5 alandan olusur: dakika, saat, ayin gunu, ay, haftanin gunu. En yaygin kullanimlar icin ornekler asagidadir.\n\n' +
      'Saat dilimini Turkiye olarak secmeniz onemlidir, aksi halde farkli saatlerde tetiklenebilir.',
    scenarios:
      '- Gunluk rapor: Her sabah 09:00 → Dunku ozet mesaji gonder\n' +
      '- Haftalik hatirlatma: Her Pazartesi 10:00 → Haftalik gorev listesi\n' +
      '- Aylik fatura: Her ayin 1\'inde → Fatura hatirlatma mesaji',
    antiPatterns:
      '- Musteri mesajlarina cevap vermek icin zamanlayici kullanmayin — Baslangic node\'u kullanin\n' +
      '- Cok sik tetikleme (her dakika gibi) sistem yukunu arttirir\n' +
      '- Saat dilimini UTC birakmayin — Turkiye saatini secin',
  },
  message_text: {
    summary: 'Musteriye duz metin mesaji gonderir. En temel ve en cok kullanilan adimdir.',
    detail:
      'Mesaj node\'u musteriye yaziyla bir mesaj gonderir. Mesaj icinde degiskenler kullanabilirsiniz: ornegin {{musteri_adi}} yazarsaniz musterinin adi otomatik yerlestirilir.\n\n' +
      '"Kullanici yanitini bekle" secenegi acikken, mesaj gonderildikten sonra akis durur ve musterinin cevabini bekler. Musterinin gonderdigi cevap {{user_input}} degiskenine atanir ve sonraki adimlarda kullanilabilir.\n\n' +
      'WhatsApp mesaj limiti 4096 karakterdir. Bu limiti asmamaya dikkat edin.',
    scenarios:
      '- Karsilama: "Merhaba {{musteri_adi}}, size nasil yardimci olabilirim?"\n' +
      '- Bilgilendirme: "Siparisniniz kargoya verildi. Takip no: {{kargo_no}}"\n' +
      '- Soru sorma (yanit bekle acik): "Randevu icin hangi tarih uygun?" → Cevabi al → Devam et',
    antiPatterns:
      '- Cok uzun mesajlar gondermeyin — musteriler okumaz (maks 4096 karakter)\n' +
      '- Art arda 3\'ten fazla mesaj node\'u koymayin — musteri spam gibi algilar\n' +
      '- Yanit beklemeniz gerekmiyorsa "Kullanici yanitini bekle"yi acik birakmayin — akisi gereksiz durdurur',
  },
  message_menu: {
    summary: 'Musteriye secenekli bir menu gosterir. Musteri bir secenek sectikten sonra akis o dala devam eder.',
    detail:
      'Menu node\'u musteriye bir mesaj ve altinda secenekler gonderir. Musteri numarayi veya secenegi yazdiginda, akis o secenegin dalina yonlenir.\n\n' +
      'Her secenek icin bir "anahtar" (musteri ne yazar) ve bir "etiket" (musteri ne gorur) tanimlanir. Ornegin anahtar: "1", etiket: "Satis". Musteri "1" yazdiginda Satis dalina gider.\n\n' +
      'Her secenegin kendine ait bir cikis baglantisi vardir. Boylece farkli secenekler farkli adimlara yonlendirilir.',
    scenarios:
      '- Ana menu: "1-Satis, 2-Destek, 3-Bilgi" → Her biri farkli akisa gider\n' +
      '- Onay: "1-Evet, devam et / 2-Hayir, iptal" → Iki farkli dal\n' +
      '- Urun secimi: "Hangi urunle ilgileniyorsunuz?" → Urun secenekleri',
    antiPatterns:
      '- 10\'dan fazla secenek koymayin — musteri karar veremez\n' +
      '- Her secenegin baglantisini yapin — baglantisiz secenek akisi kirar\n' +
      '- Menu metnini bos birakmayin — musteri ne secegini anlamaz',
  },
  logic_condition: {
    summary: 'Bir kosulu kontrol eder ve sonuca gore akisi iki dala ayirir: Dogru veya Yanlis.',
    detail:
      'Kosul node\'u bir degiskenin degerini kontrol eder. Ornegin "musteri_tipi esittir VIP" kosulunu tanimlarsaniz, VIP musteriler Dogru dalina, digerler Yanlis dalina yonlenir.\n\n' +
      'Kullanabileceginiz operatorler: Esittir, Icerir, Baslar, Buyuktur, Kucuktur, Bos mu ve Regex. Her biri farkli karsilastirma turu yapar.\n\n' +
      'Degisken olarak onceki adimlardan gelen herhangi bir degeri kullanabilirsiniz. Ornegin {{__last_input}} musterinin son mesajini icerir.',
    scenarios:
      '- VIP kontrolu: musteri_tipi = "VIP" → Ozel karsilama / Normal karsilama\n' +
      '- Mesaj icerik kontrolu: __last_input icerir "iptal" → Iptal akisi\n' +
      '- Bos kontrol: telefon bos mu → Telefon sor / Devam et',
    antiPatterns:
      '- Karmasik mantik icin ic ice kosul koymak yerine Switch kullanin\n' +
      '- Degisken adini yanlis yazmamaya dikkat edin — eslesme olmaz\n' +
      '- Her iki dali da (Dogru/Yanlis) baglayin — baglantisiz dal akisi kirar',
  },
  logic_switch: {
    summary: 'Bir degiskenin degerine gore akisi birden fazla dala ayirir. Coklu secenek icin idealdir.',
    detail:
      'Switch node\'u bir degiskeni birden fazla degerle karsilastirir. Hangi deger eslesirse o daldan devam eder. Hicbiri eslesmezse Varsayilan dalina gider.\n\n' +
      'Ornegin "departman" degiskeni "satis", "destek" veya "muhasebe" olabilir. Her biri farkli bir dala yonlendirilir. Tanimmadigi bir deger gelirse Varsayilan dali devreye girer.\n\n' +
      'Kosul node\'undan farki: Kosul sadece Dogru/Yanlis (2 dal) verir, Switch ise istediginiz kadar dal olusturabilir.',
    scenarios:
      '- Departman yonlendirme: satis/destek/muhasebe/varsayilan\n' +
      '- Dil secimi: tr/en/de → Farkli dilde mesajlar\n' +
      '- Siparis durumu: yeni/hazir/kargoda/teslim → Farkli bilgilendirmeler',
    antiPatterns:
      '- Sadece 2 durum varsa Switch yerine Kosul kullanin — daha basit\n' +
      '- Varsayilan dalini baglantisiz birakmayin — bilinmeyen degerler kaybolur\n' +
      '- Maks 10 durum sinirini asmayin — cok fazla dal yonetimi zorlastirir',
  },
  logic_working_hours: {
    summary: 'Mesai saati ici mi disi mi kontrol eder ve buna gore dallanir.',
    detail:
      'Mesai Saati node\'u, tenant (isletme) ayarlarinda tanimli olan calisma saatlerini kullanir. Musteri mesaji mesai saatleri icinde geldiyse "Mesai Ici" dalina, disinda geldiyse "Mesai Disi" dalina yonlenir.\n\n' +
      'Bu node\'un kendisinde ayar yoktur — calisma saatleri isletme ayarlarindan otomatik alinir. Bu sayede merkezi bir yerden yonetim saglanir.\n\n' +
      'Mesai disi dalinda genellikle "Su anda mesai saatleri disindayiz, en kisa surede donecegiz" gibi bir mesaj gonderilir.',
    scenarios:
      '- Mesai ici: Normal akis (bot + temsilci) / Mesai disi: Otomatik mesaj\n' +
      '- Acil durumlar: Mesai disi → Acil mi? → Evet → Nobetci temsilciye aktar\n' +
      '- Farkli hizmet: Mesai ici → Canli destek / Mesai disi → FAQ botu',
    antiPatterns:
      '- Isletme ayarlarinda calisma saatleri tanimlanmadan bu node\'u kullanmayin\n' +
      '- Mesai disi dalini bos birakmayin — musteri cevapsiz kalir\n' +
      '- 7/24 hizmet veren isletmelerde gereksiz — akisi karmasiklastirir',
  },
  ai_intent: {
    summary: 'Musterinin ne istedigini yapay zeka ile otomatik anlar ve dogru dala yonlendirir.',
    detail:
      'Intent Algilama, musterinin mesajini yapay zeka (Claude AI) ile analiz eder ve hangi konuyla ilgilendigini (intent) tespit eder. Ornegin musteri "fiyat ne kadar?" yazdigi zaman AI bunu "fiyat_bilgisi" intent\'i olarak algilar.\n\n' +
      'Tanimlayaciginiz her intent icin bir cikis dali olusur. AI musterinin mesajini analiz eder ve en uygun intent\'e yonlendirir. Guven esigi altinda kalan mesajlar "Diger" dalina gider.\n\n' +
      '"Musteri ismini sor" secenegi acikken, AI once musterinin adini sorar, dogrular ve sohbet boyunca ismiyle hitap eder. Bu daha samimi bir deneyim saglar.',
    scenarios:
      '- Satis botu: satin_alma / fiyat_sorgulama / iade / diger\n' +
      '- Destek botu: teknik_sorun / fatura / sikayet / diger\n' +
      '- Genel bot: randevu / bilgi / sikayet / insan_ile_gorusme / diger',
    antiPatterns:
      '- Cok fazla intent (10+) tanimlamayin — AI\'nin dogru secme orani duser\n' +
      '- Birbirine cok benzer intent\'ler tanimlamayin (ornek: "satin_alma" ve "alis") — karisiklik yaratir\n' +
      '- Guven esigini cok yuksek tutmayin (90%+) — cogu mesaj "Diger"e duser\n' +
      '- Intent isimlerini Turkce ve anlasilir yazin',
  },
  ai_faq: {
    summary: 'Bilgi bankasinda musteri sorusuna cevap arar. Eslesen cevabi otomatik gonderir.',
    detail:
      'FAQ Arama, musterinin sorusunu bilgi bankasindaki (FAQ ve dokumanlar) kayitlarla karsilastirir. Semantik arama kullanir — yani kelime kelime degil, anlam bazli eslestirir.\n\n' +
      'Ornegin musteriniz "iade suresi ne kadar?" diye sorduysa ve bilgi bankanizda "Iade Politikasi: Urunler 14 gun icinde iade edilebilir" kaydi varsa, bu eslesti olarak bulunur ve cevap gonderilir.\n\n' +
      'FAQ eslesirse cevap dogrudan gonderilir. Dokuman eslesirse AI ile ozetlenip gonderilir. Minimum guven esigi altindaki sonuclar "Eslesmedi" dalina yonlenir ve baska bir adim devreye girer.',
    scenarios:
      '- Musteri destek: Soru → FAQ\'da ara → Bulursa cevapla / Bulamazsa temsilciye aktar\n' +
      '- Bilgi botu: Soru → FAQ + Dokuman ara → Cevapla / "Bu konuda bilgim yok"\n' +
      '- Hibrit: Intent Algilama → "bilgi" intent\'i → FAQ Arama → Temsilci',
    antiPatterns:
      '- Bilgi bankasi bossa bu node ise yaramaz — once icerik ekleyin\n' +
      '- Minimum guveni cok dusuk tutmayin (20% alti) — alakasiz cevaplar gider\n' +
      '- Minimum guveni cok yuksek tutmayin (90%+) — cogu soru eslesmez\n' +
      '- FAQ yerine genel sohbet botu olarak kullanmayin — bunun icin Intent Algilama var',
  },
  ai_sentiment: {
    summary: 'Musterinin duygusal durumunu yapay zeka ile analiz eder: olumlu mu olumsuz mu?',
    detail:
      'Duygu Analizi, musterinin mesajindaki duygu tonunu AI ile tespit eder. Skor esik degerinin uzerindeyse "Pozitif" dalina, altindaysa "Negatif" dalina yonlenir.\n\n' +
      'Bu sayede kizgin veya mutsuz musterileri erken tespit edip ozel ilgi gosterebilirsiniz. Ornegin negatif duygu tespit edildiginde dogrudan temsilciye aktarma yapilabilir.\n\n' +
      'Esik degeri 0-100% arasinda ayarlanir. %50 varsayilan degerdir. Daha hassas tespit icin esigi dusuk tutabilirsiniz.',
    scenarios:
      '- Sikayet yonetimi: Negatif → Temsilciye hemen aktar / Pozitif → Normal akis\n' +
      '- Memnuniyet olcumu: Hizmet sonrasi → Duygu analizi → Negatif → Ozur + indirim teklifi\n' +
      '- Escalation: Negatif → "Sizi anliyoruz, hemen bir yetkiliye bagliyorum"',
    antiPatterns:
      '- Her mesajda duygu analizi yapmayin — gereksiz yavastir\n' +
      '- Tek kelimelik cevaplarda ("evet", "hayir") dogru sonuc vermeyebilir\n' +
      '- Sadece duygu analizine dayanarak karar vermeyin — baska verilerle birlestirin',
  },
  action_handoff: {
    summary: 'Sohbeti canli bir temsilciye yonlendirir. Akis burada sona erer.',
    detail:
      'Temsilciye Aktar, bot ile musteri arasindaki sohbeti sonlandirip canli bir temsilciye devreder. Bu bir "terminal" node\'dur — yani bu adimdan sonra akis devam etmez.\n\n' +
      'Ozet sablonu alanina, temsilcinin gorecegi ozet bilgiyi yazabilirsiniz. Degiskenler kullanarak musterinin adi, sorunu gibi bilgileri ozetleyebilirsiniz. Ornegin: "{{musteri_adi}} - {{__detected_intent}} hakkinda destek istiyor".\n\n' +
      'Temsilci sohbete baglandiginda musterinin onceki mesajlarini ve bu ozeti gorebilir.',
    scenarios:
      '- Karmasik sorunlar: Bot cevaplayamadi → Temsilciye aktar\n' +
      '- Musteri istegi: "Insanla konusmak istiyorum" → Dogrudan aktar\n' +
      '- Kizgin musteri: Duygu analizi negatif → Hemen temsilciye aktar',
    antiPatterns:
      '- Her akisin sonuna koymak zorunda degilsiniz — FAQ ile cozulen sorular icin gereksiz\n' +
      '- Bu node\'dan sonra baska bir adim baglamayin — calistirilmaz\n' +
      '- Ozet bos birakmayin — temsilci musterinin neyle ilgilendigini anlamaz',
  },
  action_assign_group: {
    summary: 'Sohbeti belirli bir temsilci grubuna yonlendirir. Departman bazli dagitim icin idealdir.',
    detail:
      'Gruba Ata, sohbeti belirli bir INMA temsilci grubuna yonlendirir. Temsilciye Aktar\'dan farki, rastgele bir temsilci yerine belirli bir gruba (ornegin "Satis Ekibi" veya "Teknik Destek") yonlendirmesidir.\n\n' +
      'Grup ID\'si INMA\'dan alinir. Grup adi sadece gorsel amaclidir — siz ve ekibiniz icin okunabilirlik saglar.\n\n' +
      'Ozet sablonu ile temsilciye aktarilacak bilgileri ozellestirebilirsiniz.',
    scenarios:
      '- Departman yonlendirme: Satis intent\'i → Satis grubuna ata\n' +
      '- VIP musteri: VIP kontrolu → Ozel destek grubuna ata\n' +
      '- Teknik sorun: Teknik intent → Teknik destek grubuna ata',
    antiPatterns:
      '- Grup ID\'sini bos birakmayin — yonlendirme calismaz\n' +
      '- Tek temsilci grubunuz varsa Temsilciye Aktar yeterli — Gruba Ata gereksiz\n' +
      '- Bu node\'dan sonra baska adim baglamayin — terminal node\'dur',
  },
  action_api_call: {
    summary: 'Dis bir web servisine HTTP istegi gonderir ve cevabini degiskene atar.',
    detail:
      'API Cagrisi, akis sirasinda dis bir web servisine (API) istek gonderir. Ornegin bir CRM\'den musteri bilgisi cekme, siparis sisteminze durum sorgulama veya baska bir servise veri gonderme icin kullanilir.\n\n' +
      'HTTP metodu (GET/POST/PUT/DELETE), URL, basliklar ve gonderilecek veri (body) tanimlanir. Cevap belirtilen degiskene atanir ve sonraki adimlarda kullanilabilir.\n\n' +
      'Zaman asimi ayari onemlidir — cok kisa tutarsaniz yavas API\'ler zaman asimina ugrar, cok uzun tutarsaniz musteri bekler.',
    scenarios:
      '- Musteri bilgisi: GET /api/customers/{{phone}} → Bilgiyi mesajda kullan\n' +
      '- Siparis olusturma: POST /api/orders → Siparis numarasini musteriye gonder\n' +
      '- Stok kontrolu: GET /api/stock/{{urun_id}} → Stok durumunu goster',
    antiPatterns:
      '- URL\'yi bos birakmayin — istek gonderilemez\n' +
      '- Zaman asimini 30 saniyeye cikartmayin — musteri bu kadar beklemez\n' +
      '- Hassas bilgileri (sifre, API key) dogrudan URL\'ye yazmayin — baslik (header) kullanin\n' +
      '- Hata dalini baglamayin birakmamaya dikkat edin — API hatasi akisi kirar',
  },
  action_delay: {
    summary: 'Akisi belirli bir sure duraklatir. Dogal bir konusma temposu icin kullanilir.',
    detail:
      'Bekle node\'u akisi tanimladiginiz sure kadar duraklatir. Bu sure boyunca musteriye bir sey gonderilmez.\n\n' +
      'En yaygin kullanim: Art arda mesaj gondermek yerine araya kucuk beklemeler koyarak daha dogal bir sohbet temposu olusturmak.\n\n' +
      'Minimum 1 saniye, maksimum 300 saniye (5 dakika) bekleyebilir. Genellikle 1-3 saniye yeterlidir.',
    scenarios:
      '- Dogal tempo: Mesaj → 2sn bekle → Mesaj (spam gibi gorunmez)\n' +
      '- Islem suresi simulasyonu: "Siparisninizi kontrol ediyorum..." → 3sn bekle → Sonuc\n' +
      '- Ardisik mesaj: Mesaj 1 → 1sn → Mesaj 2 → 1sn → Menu',
    antiPatterns:
      '- 10 saniyeden uzun beklemeler koymayin — musteri botu bozuldu sanir\n' +
      '- Her mesaj arasina bekle koymak zorunda degilsiniz\n' +
      '- Kritik islemlerde (temsilciye aktarma gibi) gecikme eklemeyin',
  },
  action_call_flow: {
    summary: 'Baska bir akisi alt program gibi cagirip sonucunu bekler. Tekrar kullanilabilir akislar icin idealdir.',
    detail:
      'Alt Flow, mevcut akisin icinden baska bir akisi cagirir. Alt akis tamamlandiginda ana akis kaldigi yerden devam eder.\n\n' +
      'Girdi esleme ile ana akistan alt akisa degisken aktarabilirsiniz. Cikti esleme ile alt akistan ana akisa sonuc alabilirsiniz. Ornegin musteri bilgilerini alt akisa gonderip, alt akisin topladigi verileri geri alabilirsiniz.\n\n' +
      'Alt akis basariyla tamamlanirsa "Tamamlandi" dalina, hata olursa "Hata" dalina yonlenir.',
    scenarios:
      '- Musteri dogrulama alt akisi: Ana akis → Dogrulama akisi cagir → Dogrulandi/Basarisiz\n' +
      '- Ortak FAQ: Birden fazla akis ayni FAQ alt akisini cagirabilir\n' +
      '- Siparis sureci: Ana akis → Siparis alt akisi → Sonuca gore devam',
    antiPatterns:
      '- Kendini cagiran dongusel akis olusturamayin — sonsuz dongu olusur\n' +
      '- Cok derin zincirleme (akis → alt akis → alt alt akis) kacinilmali — debug zorlasir\n' +
      '- Alt akisin hata dalini baglantilanmayi unutmayin\n' +
      '- Basit isler icin alt akis kullanmayin — dogrudan ana akista yapin',
  },
  utility_set_variable: {
    summary: 'Bir degiskene deger atar veya mevcut degeri degistirir. Veri tasima ve hesaplama icin kullanilir.',
    detail:
      'Degisken Ata, akis icinde kullanilacak bir degiskene deger verir. Bu deger sabit bir metin, baska bir degiskenin degeri veya bir ifade olabilir.\n\n' +
      'Ornegin musteri turunu belirleyip "musteri_tipi" degiskenine "VIP" atayabilir, sonraki adimlarda bu degiskeni kullanabilirsiniz.\n\n' +
      'Deger ifadesinde {{degisken}} yazarak mevcut degiskenlere referans verebilirsiniz. Ornegin "Merhaba {{musteri_adi}}" gibi birlesik degerler olusturabilirsiniz.',
    scenarios:
      '- Bayrak atama: musteri_tipi = "VIP" → Sonraki kosulda kullan\n' +
      '- Birlesik deger: tam_ad = "{{ad}} {{soyad}}"\n' +
      '- Sayac: deneme_sayisi = "{{deneme_sayisi + 1}}"',
    antiPatterns:
      '- Degisken adini bos birakmayin — atama yapilmaz\n' +
      '- Ayni degiskeni cok fazla yerde degistirmeyin — takip zorlasir\n' +
      '- Degisken isimlerini anlasilir ve tutarli yazin (ornek: musteri_adi, siparis_no)',
  },
  utility_note: {
    summary: 'Akisa gorsel yorum ekler. Calistirilmaz, sadece siz ve ekibiniz icin aciklama amacidir.',
    detail:
      'Not node\'u akis icinde calistirilmayan, sadece gorsel bir aciklama kutusudur. Karmasik akislarda belirli bolgelerin ne yaptigini aciklamak icin kullanilir.\n\n' +
      'Renk secenekleri ile farkli konulari (uyari, bilgi, onemli not) gorsel olarak ayirt edebilirsiniz. Sari: genel not, Kirmizi: uyari, Mavi: bilgi, Yesil: onay, Mor: ozel not.\n\n' +
      'Not node\'unu herhangi bir yere koyabilirsiniz — akisi etkilemez, baglanti yapmaniz gerekmez.',
    scenarios:
      '- Aciklama: "Bu bolum VIP musteriler icin ozel akis"\n' +
      '- Uyari (kirmizi): "DIKKAT: Bu API cagrisinda hata olursa temsilciye aktarilir"\n' +
      '- Hatirlatma (sari): "Bu kisimdaki mesaj metnini guncellemeyi unutma"',
    antiPatterns:
      '- Not node\'unu akisa baglamayin — gereksiz baglanti olusturur\n' +
      '- Her adima not eklemek akisi karmasik gosterir — sadece gerekli yerlere ekleyin\n' +
      '- Not icine teknik bilgi (degisken adlari vs.) yazmak yerine anlasilir aciklama yazin',
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
    { name: '__chat_id', description: 'Sohbet kimlik numarasi' },
    { name: '__phone', description: 'Musteri telefon numarasi' },
    { name: '__last_input', description: 'Musterinin son mesaji' },
    { name: '__instance_id', description: 'Mesajin geldigi hat ID' },
  ],
  webhook_trigger: [
    { name: 'webhook_payload', description: 'Gelen webhook verisi (JSON)' },
  ],
  outbound_trigger: [
    { name: 'campaign_id', description: 'Kampanya kimlik numarasi' },
  ],
  schedule_trigger: [
    { name: '__trigger_time', description: 'Tetiklenme zamani' },
  ],
  message_text: [
    { name: 'user_input', description: 'Musterinin cevabi (yanit bekle aciksa)' },
  ],
  message_menu: [
    { name: '__selected_option', description: 'Secilen secenek etiketi' },
    { name: '__selected_key', description: 'Secilen secenek anahtari' },
  ],
  ai_intent: [
    { name: '__detected_intent', description: 'Tespit edilen intent adi' },
    { name: '__intent_confidence', description: 'Guven skoru (0-1)' },
    { name: '__customer_name', description: 'Musteri adi (isim sorma aciksa)' },
  ],
  ai_faq: [
    { name: '__faq_answer', description: 'Bulunan cevap metni' },
    { name: '__faq_confidence', description: 'Eslesme guven skoru' },
  ],
  ai_sentiment: [
    { name: '__sentiment_score', description: 'Duygu skoru (0-1)' },
    { name: '__sentiment_label', description: 'Duygu etiketi (positive/negative)' },
  ],
  action_api_call: [
    { name: 'api_response', description: 'API cevap verisi' },
    { name: '__api_status_code', description: 'HTTP durum kodu' },
  ],
  utility_set_variable: [
    { name: '(kullanici tanimli)', description: 'Atanan degisken adi ve degeri' },
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
  { name: '__last_input', description: 'Musterinin son mesaji' },
  { name: '__chat_id', description: 'Sohbet kimlik numarasi' },
  { name: '__phone', description: 'Musteri telefon numarasi' },
  { name: '__instance_id', description: 'Mesaj gelen hat ID' },
  { name: '__customer_name', description: 'Musteri adi (varsa)' },
  { name: '__timestamp', description: 'Islem zamani' },
];

// ============================================================
// Edge Label Mapping
// ============================================================

/** Static handle → label mapping for known node types */
export const EDGE_HANDLE_LABELS: Partial<Record<FlowNodeType, Record<string, string>>> = {
  logic_condition: {
    true_handle: 'DOGRU',
    false_handle: 'YANLIS',
  },
  logic_working_hours: {
    within_hours: 'MESAI ICI',
    outside_hours: 'MESAI DISI',
  },
  ai_intent: {
    high_confidence: 'ALGILANDI',
    low_confidence: 'DIGER',
  },
  ai_faq: {
    matched: 'ESLESTI',
    no_match: 'ESLESMEDI',
  },
  ai_sentiment: {
    positive: 'POZITIF',
    negative: 'NEGATIF',
  },
  action_api_call: {
    success: 'BASARILI',
    error: 'HATA',
  },
  action_call_flow: {
    completed: 'TAMAMLANDI',
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
  if (parts.length !== 5) return 'Gecersiz cron ifadesi (5 alan olmali)';

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
    return `Her gun saat ${timeStr}`;
  }

  // Weekdays
  if (dayOfMonth === '*' && month === '*' && (dayOfWeek === '1-5' || dayOfWeek === 'MON-FRI')) {
    return `Hafta ici her gun saat ${timeStr}`;
  }

  // Specific day of week
  if (dayOfMonth === '*' && month === '*' && dayOfWeek !== '*') {
    const dayName = getDayName(dayOfWeek);
    if (dayName) return `Her ${dayName} saat ${timeStr}`;
  }

  // Specific day of month
  if (dayOfMonth !== '*' && month === '*' && dayOfWeek === '*') {
    const dom = parseInt(dayOfMonth, 10);
    if (!isNaN(dom)) return `Her ayin ${dom}. gunu saat ${timeStr}`;
  }

  return formatRawCron(parts);
}

function getDayName(day: string): string | null {
  const map: Record<string, string> = {
    '0': 'Pazar', '7': 'Pazar', '1': 'Pazartesi', '2': 'Sali',
    '3': 'Carsamba', '4': 'Persembe', '5': 'Cuma', '6': 'Cumartesi',
    'SUN': 'Pazar', 'MON': 'Pazartesi', 'TUE': 'Sali',
    'WED': 'Carsamba', 'THU': 'Persembe', 'FRI': 'Cuma', 'SAT': 'Cumartesi',
  };
  return map[day.toUpperCase()] ?? null;
}

function formatRawCron(parts: string[]): string {
  return `Cron: ${parts.join(' ')} (ozel zamanlama)`;
}
