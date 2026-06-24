using System.Text.Json;
using Chatinbox.WhatsAppAnalytics.Models;

namespace Chatinbox.WhatsAppAnalytics.Services.Insights;

/// <summary>
/// RI Faz 5: Sector seed data for remaining 9 sectors.
/// Extends TemplateMiningService with domain knowledge for:
/// guzellik, dijital_pazarlama, finans, turizm, egitim, dis, lojistik, yeme_icme, diger.
/// </summary>
internal static class TemplateSeedData
{
    // ── Intent Seeds ──

    public static List<SectorIntentRecord> GetIntents(string sector) => sector switch
    {
        "guzellik" => new()
        {
            I("guzellik", "sac_bakim", "Sac bakim ve boyama", "sac", 100, ["sac", "boyama", "kesim", "bakim"]),
            I("guzellik", "cilt_bakim", "Cilt bakim ve yuz tedavisi", "cilt", 95, ["cilt", "yuz", "peeling", "hydrafacial"]),
            I("guzellik", "epilasyon", "Lazer epilasyon", "epilasyon", 90, ["lazer", "epilasyon", "tuy"]),
            I("guzellik", "manikur_pedikur", "Manikur ve pedikur", "tirnaklar", 85, ["manikur", "pedikur", "tirnak", "jeloje"]),
            I("guzellik", "kirpik_kas", "Kirpik ve kas islemleri", "kirpik", 80, ["kirpik", "kas", "lifting"]),
            I("guzellik", "fiyat_bilgi", "Fiyat sorgusu", "genel", 75, ["fiyat", "ucret", "ne kadar"]),
            I("guzellik", "randevu", "Randevu alma", "genel", 70, ["randevu", "musait", "tarih"]),
        },
        "dijital_pazarlama" => new()
        {
            I("dijital_pazarlama", "sosyal_medya", "Sosyal medya yonetimi", "hizmet", 100, ["sosyal medya", "instagram", "facebook", "tiktok"]),
            I("dijital_pazarlama", "reklam_yonetimi", "Reklam kampanya yonetimi", "hizmet", 95, ["reklam", "google ads", "meta ads", "kampanya"]),
            I("dijital_pazarlama", "seo", "SEO ve arama motoru optimizasyonu", "hizmet", 90, ["seo", "google", "arama", "optimizasyon"]),
            I("dijital_pazarlama", "web_tasarim", "Web sitesi tasarim ve gelistirme", "hizmet", 85, ["web", "site", "tasarim", "e-ticaret"]),
            I("dijital_pazarlama", "fiyat_teklif", "Fiyat teklifi talebi", "satis", 80, ["fiyat", "teklif", "butce", "paket"]),
            I("dijital_pazarlama", "mevcut_musteri", "Mevcut musteri destek", "destek", 75, ["rapor", "sonuc", "performans"]),
        },
        "finans" => new()
        {
            I("finans", "sigorta_teklif", "Sigorta teklifi talebi", "sigorta", 100, ["sigorta", "polis", "teklif"]),
            I("finans", "kasko_trafik", "Kasko ve trafik sigortasi", "sigorta", 95, ["kasko", "trafik", "arac"]),
            I("finans", "saglik_sigorta", "Saglik sigortasi", "sigorta", 90, ["saglik", "ozel", "tamamlayici"]),
            I("finans", "hasar_bildirimi", "Hasar bildirimi", "destek", 85, ["hasar", "kaza", "bildirim"]),
            I("finans", "kredi_bilgi", "Kredi ve finansman", "finans", 80, ["kredi", "faiz", "taksit"]),
            I("finans", "yatirim_bilgi", "Yatirim danismanlik", "yatirim", 75, ["yatirim", "fon", "portfoy"]),
        },
        "turizm" => new()
        {
            I("turizm", "tur_sorgulama", "Tur paketi sorgulama", "tur", 100, ["tur", "gezi", "paket"]),
            I("turizm", "otel_rezervasyon", "Otel rezervasyon", "konaklama", 95, ["otel", "rezervasyon", "konaklama"]),
            I("turizm", "ucus_bilgi", "Ucus bileti bilgi", "ulasim", 90, ["ucus", "bilet", "ucak"]),
            I("turizm", "transfer", "Havaalani transfer", "ulasim", 85, ["transfer", "havaalani", "arac"]),
            I("turizm", "vize_bilgi", "Vize islemleri bilgi", "hizmet", 80, ["vize", "pasaport", "konsolosluk"]),
            I("turizm", "fiyat_bilgi", "Fiyat sorgusu", "satis", 75, ["fiyat", "ne kadar", "kisi basi"]),
        },
        "egitim" => new()
        {
            I("egitim", "kurs_bilgi", "Kurs ve program bilgisi", "akademik", 100, ["kurs", "egitim", "program"]),
            I("egitim", "kayit_islem", "Kayit islemleri", "idari", 95, ["kayit", "basvuru", "kabul"]),
            I("egitim", "ucret_bilgi", "Ucret ve burs bilgisi", "finans", 90, ["ucret", "burs", "taksit"]),
            I("egitim", "ders_programi", "Ders programi ve takvim", "akademik", 85, ["ders", "program", "takvim"]),
            I("egitim", "sertifika", "Sertifika ve diploma", "akademik", 80, ["sertifika", "diploma", "belge"]),
        },
        "dis" => new()
        {
            I("dis", "implant", "Dis implant bilgisi ve fiyat", "tedavi", 100, ["implant", "vida", "dis eksikligi"]),
            I("dis", "zirkonyum", "Zirkonyum kaplama", "estetik", 95, ["zirkonyum", "kaplama", "porselen"]),
            I("dis", "ortodonti", "Ortodonti ve tel tedavisi", "tedavi", 90, ["tel", "ortodonti", "diş teli", "invisalign"]),
            I("dis", "beyazlatma", "Dis beyazlatma", "estetik", 85, ["beyazlatma", "bleaching", "whitening"]),
            I("dis", "genel_tedavi", "Genel dis tedavisi", "tedavi", 80, ["dis agrisi", "kanal", "dolgu", "cekim"]),
            I("dis", "fiyat_bilgi", "Fiyat sorgusu", "genel", 75, ["fiyat", "ucret", "ne kadar"]),
            I("dis", "randevu", "Randevu alma", "genel", 70, ["randevu", "musait"]),
        },
        "lojistik" => new()
        {
            I("lojistik", "kargo_takip", "Kargo takip ve durum", "kargo", 100, ["kargo", "takip", "nerede"]),
            I("lojistik", "fiyat_teklif", "Tasimacilik fiyat teklifi", "satis", 95, ["fiyat", "teklif", "tasima"]),
            I("lojistik", "gumruk", "Gumruk islemleri", "gumruk", 90, ["gumruk", "ithalat", "ihracat"]),
            I("lojistik", "depolama", "Depolama hizmeti", "depo", 85, ["depo", "depolama", "stok"]),
            I("lojistik", "uluslararasi", "Uluslararasi tasimacilik", "kargo", 80, ["uluslararasi", "yurt disi", "international"]),
        },
        "yeme_icme" => new()
        {
            I("yeme_icme", "siparis", "Online siparis", "siparis", 100, ["siparis", "order", "yemek"]),
            I("yeme_icme", "rezervasyon", "Masa rezervasyonu", "rezervasyon", 95, ["rezervasyon", "masa", "yer ayirt"]),
            I("yeme_icme", "menu_bilgi", "Menu ve fiyat bilgisi", "bilgi", 90, ["menu", "fiyat", "yemek listesi"]),
            I("yeme_icme", "catering", "Catering hizmeti", "hizmet", 85, ["catering", "organizasyon", "toplanti"]),
            I("yeme_icme", "sikayet", "Sikayet ve geri bildirim", "destek", 80, ["sikayet", "memnuniyet", "geri bildirim"]),
        },
        "diger" => new()
        {
            I("diger", "bilgi_talebi", "Genel bilgi talebi", "genel", 100, ["bilgi", "soru", "merak"]),
            I("diger", "fiyat_bilgi", "Fiyat sorgusu", "genel", 95, ["fiyat", "ucret", "ne kadar"]),
            I("diger", "randevu", "Randevu / gorusme talebi", "genel", 90, ["randevu", "gorusme", "musait"]),
            I("diger", "destek", "Destek talebi", "destek", 85, ["destek", "yardim", "sorun"]),
        },
        _ => new()
    };

    // ── FAQ Seeds ──

    public static List<SectorFaqRecord> GetFaqs(string sector) => sector switch
    {
        "guzellik" => new()
        {
            F("guzellik", "Lazer epilasyon kac seans gerektirir?", "Ortalama 6-8 seans, 4-6 hafta araliklarla. Cilt ve tuy tipine gore degisir.", "epilasyon", 85),
            F("guzellik", "Sac boyama ne kadar suruyor?", "Islem turine gore 1-3 saat arasinda. Ombre ve balyaj islemleri daha uzun surer.", "sac", 80),
            F("guzellik", "Cilt bakimi icin randevu gerekli mi?", "Evet, onceden randevu almanizi oneriyoruz. Online randevu da alabilirsiniz.", "cilt", 75),
        },
        "dijital_pazarlama" => new()
        {
            F("dijital_pazarlama", "Sosyal medya yonetimi fiyatlari?", "Paket bazli calisiyoruz. Baslangic paketi aylik icerik + reklam yonetimi icin teklif hazirlarız.", "hizmet", 85),
            F("dijital_pazarlama", "Ne kadar surede sonuc alirim?", "SEO icin 3-6 ay, reklam icin 1-2 hafta icerisinde ilk sonuclar gorulur. Aylik raporlama yapariz.", "genel", 80),
            F("dijital_pazarlama", "Hangi platformlarda calisiyorsunuz?", "Google Ads, Meta, TikTok, LinkedIn, YouTube. E-ticaret icin Trendyol, Hepsiburada da destek veriyoruz.", "hizmet", 75),
        },
        "finans" => new()
        {
            F("finans", "Kasko fiyati nasil hesaplanir?", "Arac markasi, modeli, yili ve kullanim sekline gore degisir. Hemen ucretsiz teklif hazirlayabilirim.", "sigorta", 85),
            F("finans", "Hasar bildirimi nasil yapilir?", "7/24 destek hattimizdan veya WhatsApp uzerinden fotograflarla bildirim yapabilirsiniz.", "destek", 80),
        },
        "turizm" => new()
        {
            F("turizm", "Tur fiyatina neler dahil?", "Konaklama, kahvalti, rehberlik ve tur boyunca ulasim dahildir. Ucak bileti ayridir.", "tur", 85),
            F("turizm", "Iptal kosullari nelerdir?", "30 gun oncesine kadar ucretsiz iptal. 15-30 gun arasi %50 iade. 15 gunun altinda iade yapilmaz.", "genel", 80),
            F("turizm", "Vize islemleri yardim ediyor musunuz?", "Evet, vize basvuru sureci icin rehberlik ve evrak hazirlama destegi sunuyoruz.", "vize", 75),
        },
        "egitim" => new()
        {
            F("egitim", "Kayit icin gerekli belgeler neler?", "Kimlik fotokopisi, diploma, vesikalik fotograf ve basvuru formu gereklidir.", "kayit", 85),
            F("egitim", "Taksit imkani var mi?", "Evet, egitim ucretini 6-12 aya kadar taksitlendirebilirsiniz. Burs imkanlari da mevcuttur.", "finans", 80),
            F("egitim", "Online egitim secenegi var mi?", "Evet, cogu programimizda canli online veya hibrit katilim secenegi mevcuttur.", "akademik", 75),
        },
        "dis" => new()
        {
            F("dis", "Implant fiyati ne kadar?", "Implant markasina gore degisir. Muayene sonrasi detayli fiyat bilgisi verilir. Taksit imkani mevcuttur.", "implant", 90),
            F("dis", "Zirkonyum kaplama kac gun surer?", "Genellikle 5-7 gun icerisinde tamamlanir. 2 seans gerekir: hazirlama ve teslimat.", "estetik", 85),
            F("dis", "Agri oluyor mu?", "Modern anestezi ile islem sirasinda agri hissedilmez. Sonrasinda hafif hassasiyet olabilir.", "genel", 80),
        },
        "lojistik" => new()
        {
            F("lojistik", "Kargom nerede?", "Kargo takip numaranizi paylasin, aninda durum bilgisi verebilirim.", "kargo", 85),
            F("lojistik", "Uluslararasi gonderim yapiyor musunuz?", "Evet, 190+ ulkeye hava, deniz ve kara kargo hizmeti sunuyoruz.", "kargo", 80),
        },
        "yeme_icme" => new()
        {
            F("yeme_icme", "Minimum siparis tutari var mi?", "Online siparis icin minimum tutar [TUTAR] TL'dir. Ucretsiz teslimat [TUTAR2] TL uzerindedir.", "siparis", 85),
            F("yeme_icme", "Rezervasyon nasil yapilir?", "WhatsApp veya telefonla rezervasyon yapabilirsiniz. Kisi sayisi ve tarih belirtin.", "rezervasyon", 80),
        },
        "diger" => new()
        {
            F("diger", "Calisma saatleriniz nedir?", "Hafta ici 09:00-18:00, Cumartesi 10:00-14:00 arasi hizmet vermekteyiz.", "genel", 75),
            F("diger", "Iletisim bilgileriniz nedir?", "WhatsApp, telefon ve e-posta ile bize ulasabilirsiniz. 7/24 WhatsApp destegi mevcuttur.", "genel", 70),
        },
        _ => new()
    };

    // ── Flow Seeds ──

    public static List<SectorFlowRecord> GetFlows(string sector) => sector switch
    {
        "guzellik" => new()
        {
            Fl("guzellik", "randevu_akisi", "ilk_temas", "Randevu alma ve hizmet akisi",
                """[{"name":"hizmet_sorgu","description":"Musteri istenen hizmeti sorar"},{"name":"uygunluk","description":"Musait tarih ve saat belirlenir"},{"name":"randevu","description":"Randevu onaylanir"},{"name":"hatirlatma","description":"1 gun once hatirlatma mesaji"},{"name":"sonrasi","description":"Memnuniyet sorgusu ve sonraki randevu"}]"""),
        },
        "dijital_pazarlama" => new()
        {
            Fl("dijital_pazarlama", "teklif_akisi", "ilk_temas", "Teklif hazirlama akisi",
                """[{"name":"ihtiyac_analiz","description":"Musteri ihtiyaclari dinlenir"},{"name":"mevcut_durum","description":"Mevcut dijital varliklar incelenir"},{"name":"teklif","description":"Ozel teklif hazirlanir ve sunulur"},{"name":"muzakere","description":"Kapsam ve butce belirlenir"},{"name":"sozlesme","description":"Sozlesme imzalanir ve baslanir"}]"""),
        },
        "finans" => new()
        {
            Fl("finans", "sigorta_akisi", "ilk_temas", "Sigorta teklif ve polis akisi",
                """[{"name":"bilgi_toplama","description":"Arac/saglik/konut bilgisi alinir"},{"name":"teklif","description":"En uygun teklif hazirlanir"},{"name":"karsilastirma","description":"Alternatif teklifler sunulur"},{"name":"onay","description":"Polis duzenlenir"},{"name":"odeme","description":"Odeme alinir, polis gonderilir"}]"""),
        },
        "turizm" => new()
        {
            Fl("turizm", "tur_akisi", "ilk_temas", "Tur paketi satis akisi",
                """[{"name":"destinasyon_sorgu","description":"Musteri gidecegi yer ve tarih belirtir"},{"name":"secenek_sun","description":"Uygun turlar/oteller listelenir"},{"name":"detay","description":"Detayli program ve fiyat gonderilir"},{"name":"rezervasyon","description":"Kapora alinir, rezervasyon yapilir"},{"name":"bilgilendirme","description":"Seyahat oncesi bilgilendirme yapilir"}]"""),
        },
        "egitim" => new()
        {
            Fl("egitim", "kayit_akisi", "ilk_temas", "Ogrenci kayit akisi",
                """[{"name":"bilgi_sorgu","description":"Ogrenci program ve kosullari sorar"},{"name":"danismanlik","description":"Uygun program onerilir"},{"name":"basvuru","description":"Belgeler toplanir, basvuru yapilir"},{"name":"kabul","description":"Kabul sonucu bildirilir"},{"name":"kayit","description":"Odeme ve kayit tamamlanir"}]"""),
        },
        "dis" => new()
        {
            Fl("dis", "tedavi_akisi", "ilk_temas", "Dis tedavisi satis akisi",
                """[{"name":"sikayet","description":"Hasta sikayetini belirtir"},{"name":"muayene","description":"Muayene randevusu ayarlanir"},{"name":"tedavi_plani","description":"Doktor tedavi planini aciklar"},{"name":"fiyat","description":"Tedavi fiyati ve taksit secenekleri sunulur"},{"name":"randevu","description":"Tedavi randevusu belirlenir"}]"""),
        },
        "lojistik" => new()
        {
            Fl("lojistik", "teklif_akisi", "ilk_temas", "Lojistik teklif akisi",
                """[{"name":"detay_alma","description":"Gonderim detaylari alinir (nereden, nereye, ne)"},{"name":"fiyat","description":"Fiyat teklifi hazirlanir"},{"name":"onay","description":"Musteri onayi alinir"},{"name":"toplama","description":"Kargo toplanir"},{"name":"teslimat","description":"Teslimat ve takip"}]"""),
        },
        "yeme_icme" => new()
        {
            Fl("yeme_icme", "siparis_akisi", "ilk_temas", "Online siparis akisi",
                """[{"name":"menu_inceleme","description":"Musteri menu inceler"},{"name":"siparis","description":"Siparis verilir"},{"name":"onay","description":"Siparis onaylanir ve hazirlama baslar"},{"name":"teslimat","description":"Siparis teslim edilir"},{"name":"geri_bildirim","description":"Memnuniyet sorulur"}]"""),
        },
        "diger" => new()
        {
            Fl("diger", "genel_satis_akisi", "ilk_temas", "Genel satis akisi",
                """[{"name":"ihtiyac","description":"Musteri ihtiyacini belirtir"},{"name":"bilgi","description":"Urun/hizmet bilgisi verilir"},{"name":"teklif","description":"Fiyat teklifi sunulur"},{"name":"karar","description":"Musteri karar verir"}]"""),
        },
        _ => new()
    };

    // ── Objection Handler Seeds ──

    public static List<SectorObjectionHandlerRecord> GetObjHandlers(string sector) => sector switch
    {
        "guzellik" => new()
        {
            OH("guzellik", "price_high", "Fiyat Yuksek",
                """[{"text":"Paket kampanyamiz var, birden fazla islem alirsan indirim uygulanir.","effectiveness_score":75},{"text":"Ilk ziyarete ozel %20 indirim sunuyoruz.","effectiveness_score":70}]"""),
            OH("guzellik", "timing", "Zamanlama",
                """[{"text":"Aksam ve hafta sonu randevularimiz da mevcut.","effectiveness_score":70}]"""),
        },
        "dijital_pazarlama" => new()
        {
            OH("dijital_pazarlama", "price_high", "Butce Yetersiz",
                """[{"text":"Butcenize uygun baslangic paketi hazirlayabiliriz. Sonuclara gore buyuturuz.","effectiveness_score":80},{"text":"ROI odakli calisiyoruz, harcanan her lira geri donuyor.","effectiveness_score":70}]"""),
            OH("dijital_pazarlama", "not_ready", "Henuz Hazir Degil",
                """[{"text":"Ucretsiz dijital analiz raporu hazirlayalim, ne zaman baslamak isterseniz hazir olsun.","effectiveness_score":75}]"""),
        },
        "finans" => new()
        {
            OH("finans", "price_high", "Prim Yuksek",
                """[{"text":"Farkli teminat seviyeleriyle daha uygun teklifler hazirlayabilirim.","effectiveness_score":75},{"text":"Yillik yerine aylik odeme secenegi mevcuttur.","effectiveness_score":70}]"""),
            OH("finans", "chose_competitor", "Baska Sirket Tercih",
                """[{"text":"Teminat karsilastirmasi yapalim, ayni teminat icin daha uygun fiyat sunabilirim.","effectiveness_score":70}]"""),
        },
        "turizm" => new()
        {
            OH("turizm", "price_high", "Fiyat Yuksek",
                """[{"text":"Erken rezervasyon indirimi %15'e kadar. Grup indirimi de mevcuttur.","effectiveness_score":75},{"text":"Ekonomik otel secenegiyle fiyati dusurubiliriz, tur programi ayni kalir.","effectiveness_score":70}]"""),
            OH("turizm", "timing", "Tarih Uymuyor",
                """[{"text":"Farkli tarih secenekleri mevcut, hangi donem uygun?","effectiveness_score":70}]"""),
        },
        "egitim" => new()
        {
            OH("egitim", "price_high", "Ucret Yuksek",
                """[{"text":"Burs ve taksit imkanlari mevcuttur. Erken kayit indirimi de var.","effectiveness_score":80},{"text":"Egitim masrafi yatirimdir, sertifika ile kariyer firsatlari artar.","effectiveness_score":65}]"""),
        },
        "dis" => new()
        {
            OH("dis", "price_high", "Tedavi Pahali",
                """[{"text":"12 aya kadar faizsiz taksit imkani var. Tedavi planini asama asama da yapabiliriz.","effectiveness_score":80},{"text":"Kampanya donemlerinde ozel fiyatlarimiz oluyor, bilgi vereyim.","effectiveness_score":70}]"""),
            OH("dis", "medical_concern", "Agri/Korku",
                """[{"text":"Sedasyon altinda tamamen uyuyarak tedavi olabilirsiniz. Agri hissetmezsiniz.","effectiveness_score":80},{"text":"Modern anestezi yontemleriyle islem sirasinda agri sifir. Binlerce hasta memnun.","effectiveness_score":75}]"""),
        },
        "lojistik" => new()
        {
            OH("lojistik", "price_high", "Fiyat Yuksek",
                """[{"text":"Hacme gore ozel fiyat sunabiliriz. Duzenli gonderimde indirimli anlaşma yapabiliriz.","effectiveness_score":75}]"""),
        },
        "yeme_icme" => new()
        {
            OH("yeme_icme", "timing", "Teslimat Suresi",
                """[{"text":"Ortalama teslimat suremiz 30-45 dakikadir. Yogun saatlerde biraz uzayabilir.","effectiveness_score":70}]"""),
        },
        "diger" => new()
        {
            OH("diger", "price_high", "Fiyat Yuksek",
                """[{"text":"Butcenize uygun alternatif secenek sunabiliriz.","effectiveness_score":70}]"""),
            OH("diger", "not_ready", "Hazir Degil",
                """[{"text":"Bilgi dosyasi gondereyim, hazir oldugunuzda iletisime gecin.","effectiveness_score":70}]"""),
        },
        _ => new()
    };

    // ── Follow-up Template Seeds ──

    public static List<SectorFollowupTemplateRecord> GetFollowups(string sector) => sector switch
    {
        "guzellik" => new()
        {
            FU("guzellik", "ilk_hatirlatma", "Randevu Hatirlatma", "Merhaba, bizimle ilgilendiginiz islem icin randevu olusturmak ister misiniz? Bu hafta musait saatlerimiz mevcut.", 24),
            FU("guzellik", "ikinci_hatirlatma", "Kampanya Bilgisi", "Merhaba, bu hafta ozel kampanyamiz var! Ilgilendiginiz islem icin indirimli fiyat sunuyoruz.", 72),
        },
        "dijital_pazarlama" => new()
        {
            FU("dijital_pazarlama", "ilk_hatirlatma", "Teklif Hatirlatma", "Merhaba, hazirlanan teklif hakkinda sorulariniz oldu mu? Ucretsiz dijital analiz raporunu da paylasabilirim.", 48),
            FU("dijital_pazarlama", "ikinci_hatirlatma", "Basari Hikayesi", "Merhaba, sektorunuzden bir basari hikayesi paylasiyim — benzer butceyle %200 ROI saglandi.", 120),
        },
        "finans" => new()
        {
            FU("finans", "ilk_hatirlatma", "Teklif Gecerliligi", "Merhaba, hazirlanan sigorta teklifinin gecerlilik suresi doluyor. Onaylamak ister misiniz?", 72),
            FU("finans", "ikinci_hatirlatma", "Yeni Teklif", "Merhaba, sizin icin daha uygun bir teklif hazirladi. Incelemek ister misiniz?", 168),
        },
        "turizm" => new()
        {
            FU("turizm", "ilk_hatirlatma", "Yer Hatirlatma", "Merhaba, ilgilendiginiz turda son birkaç yer kaldi. Rezervasyon yapmak ister misiniz?", 48),
            FU("turizm", "ikinci_hatirlatma", "Erken Rezervasyon", "Merhaba, erken rezervasyon indirimi bu hafta sona eriyor. Son firsat!", 120),
        },
        "egitim" => new()
        {
            FU("egitim", "ilk_hatirlatma", "Kayit Hatirlatma", "Merhaba, kayit donemimiz devam ediyor. Sorulariniz varsa yardimci olabilirim.", 72),
            FU("egitim", "ikinci_hatirlatma", "Burs Imkani", "Merhaba, burs basvuru suresi doluyor. Basvuru yapmak ister misiniz?", 168),
        },
        "dis" => new()
        {
            FU("dis", "ilk_hatirlatma", "Muayene Daveti", "Merhaba, ucretsiz muayene ve tedavi planlama icin randevu olusturmak ister misiniz?", 48),
            FU("dis", "ikinci_hatirlatma", "Kampanya Bilgisi", "Merhaba, bu ay implant kampanyamiz var. Detaylar icin bilgi gondereyim mi?", 168),
        },
        "lojistik" => new()
        {
            FU("lojistik", "ilk_hatirlatma", "Teklif Takip", "Merhaba, gondermis oldugum lojistik teklifini inceleme firsatiniz oldu mu?", 48),
        },
        "yeme_icme" => new()
        {
            FU("yeme_icme", "ilk_hatirlatma", "Siparis Daveti", "Merhaba, yeni menumuz hazir! Siparis vermek ister misiniz?", 72),
        },
        "diger" => new()
        {
            FU("diger", "ilk_hatirlatma", "Genel Hatirlatma", "Merhaba, goresmemiz hakkinda bir gelisme var mi? Yardimci olabilecegim bir konu varsa buradayim.", 48),
        },
        _ => new()
    };

    // ── Onboarding Step Seeds ──

    public static List<SectorOnboardingStepRecord> GetOnboardingSteps(string sector) => sector switch
    {
        "guzellik" => new()
        {
            S("guzellik", 1, "Agent'lari sisteme ekle", "Tum calisanlari Chatinbox'a ekleyin.", "Agent bazli takip baslar", "1-3"),
            S("guzellik", 2, "Hizmet listesi FAQ'larini aktif et", "Fiyat, sure, seans bilgileri icin hazir cevaplar.", "Cevap suresi %40 azalir", "1-7"),
            S("guzellik", 3, "Randevu flow'unu kur", "Hizmet sorgusu → musaitlik → randevu akisi.", "Randevu donusum orani artar", "3-7"),
            S("guzellik", 4, "Follow-up otomasyonunu ac", "Gelmeyenlere hatirlatma mesaji.", "Randevu iptali %20 azalir", "7-14"),
            S("guzellik", 5, "Performans raporunu incele", "Haftalik hizmet bazli rapor.", "Veri odakli karar alma", "14-30"),
        },
        "dijital_pazarlama" => new()
        {
            S("dijital_pazarlama", 1, "Ekibi sisteme ekle", "Satis ve hesap yoneticilerini ekleyin.", "Ekip takibi baslar", "1-3"),
            S("dijital_pazarlama", 2, "Teklif sablonlarini hazirla", "Paket bazli teklif sablonlari.", "Teklif hazirlama hizlanir", "1-7"),
            S("dijital_pazarlama", 3, "Satis flow'unu kur", "Ihtiyac analizi → teklif → muzakere akisi.", "Satıs sureci standartlasir", "3-7"),
            S("dijital_pazarlama", 4, "Follow-up otomasyonunu ac", "Teklif sonrasi takip mesajlari.", "Teklif donusumu artar", "7-14"),
        },
        "finans" => new()
        {
            S("finans", 1, "Agent'lari sisteme ekle", "Sigorta danismanlarini ekleyin.", "Agent bazli takip", "1-3"),
            S("finans", 2, "Urun FAQ'larini aktif et", "Sigorta turlerine gore hazir cevaplar.", "Musteri bilgilendirme hizlanir", "1-7"),
            S("finans", 3, "Polis satis flow'unu kur", "Bilgi toplama → teklif → onay akisi.", "Satis sureci standartlasir", "3-7"),
            S("finans", 4, "Yenileme hatirlatmalarini ac", "Polis bitis tarihine gore otomatik mesaj.", "Yenileme orani artar", "7-14"),
        },
        "turizm" => new()
        {
            S("turizm", 1, "Agent'lari ekle", "Tur operatorlerini ekleyin.", "Agent takibi baslar", "1-3"),
            S("turizm", 2, "Destinasyon FAQ'larini aktif et", "Popüler destinasyonlar icin hazir bilgiler.", "Cevap suresi azalir", "1-7"),
            S("turizm", 3, "Tur satis flow'unu kur", "Sorgu → secenek → rezervasyon akisi.", "Rezervasyon orani artar", "3-7"),
            S("turizm", 4, "Erken rezervasyon follow-up ac", "Ilgilenen musterilere erken rezervasyon firsati.", "Erken satis artar", "7-14"),
        },
        "egitim" => new()
        {
            S("egitim", 1, "Danismanlari ekle", "Kayit danismanlarini sisteme ekleyin.", "Danisman takibi baslar", "1-3"),
            S("egitim", 2, "Program FAQ'larini aktif et", "Kayit, ucret, sertifika bilgileri.", "Kayit sureci hizlanir", "1-7"),
            S("egitim", 3, "Kayit flow'unu kur", "Bilgi → basvuru → kabul → kayit akisi.", "Kayit donusumu artar", "3-7"),
            S("egitim", 4, "Donem basi hatirlatmasi ac", "Kayit donemlerinde otomatik bilgilendirme.", "Kayit sayisi artar", "7-14"),
        },
        "dis" => new()
        {
            S("dis", 1, "Doktor ve personeli ekle", "Doktorlari ve asistanlari sisteme ekleyin.", "Doktor bazli takip baslar", "1-3"),
            S("dis", 2, "Tedavi FAQ'larini aktif et", "Implant, ortodonti, beyazlatma bilgileri.", "Hasta bilgilendirme hizlanir", "1-7"),
            S("dis", 3, "Tedavi flow'unu kur", "Sikayet → muayene → plan → tedavi akisi.", "Tedavi donusumu artar", "3-7"),
            S("dis", 4, "Kontrol hatirlatmasi ac", "6 aylik kontrol icin otomatik mesaj.", "Hasta sadakati artar", "7-14"),
            S("dis", 5, "Performans raporunu incele", "Tedavi bazli ve doktor bazli raporlar.", "Veri odakli karar alma", "14-30"),
        },
        "lojistik" => new()
        {
            S("lojistik", 1, "Ekibi sisteme ekle", "Satis ve operasyon ekibini ekleyin.", "Ekip takibi baslar", "1-3"),
            S("lojistik", 2, "Hizmet FAQ'larini aktif et", "Kargo suresi, gumruk, fiyatlandirma bilgileri.", "Musteri bilgilendirme hizlanir", "1-7"),
            S("lojistik", 3, "Teklif flow'unu kur", "Detay alma → fiyat → onay akisi.", "Teklif sureci standartlasir", "3-7"),
        },
        "yeme_icme" => new()
        {
            S("yeme_icme", 1, "Ekibi sisteme ekle", "Sipa ris ve rezervasyon ekibini ekleyin.", "Ekip takibi baslar", "1-3"),
            S("yeme_icme", 2, "Menu ve siparis FAQ'larini aktif et", "Minimum siparis, teslimat, odeme bilgileri.", "Siparis sureci hizlanir", "1-7"),
            S("yeme_icme", 3, "Siparis flow'unu kur", "Menu → siparis → hazirlama → teslimat akisi.", "Siparis donusumu artar", "3-7"),
        },
        "diger" => new()
        {
            S("diger", 1, "Ekibi sisteme ekle", "Calisanlarınizi Chatinbox'a ekleyin.", "Ekip takibi baslar", "1-3"),
            S("diger", 2, "Genel FAQ'lari aktif et", "Sik sorulan sorulara hazir cevaplar.", "Cevap suresi azalir", "1-7"),
            S("diger", 3, "Satis flow'unu kur", "Temel satis akisinizi olusturun.", "Satis sureci standartlasir", "3-7"),
            S("diger", 4, "Follow-up otomasyonunu ac", "Ilgilenen musterilere otomatik takip.", "Donusum orani artar", "7-14"),
        },
        _ => new()
    };

    // ── Helpers (short aliases) ──

    private static SectorIntentRecord I(string sector, string name, string desc, string cat, int priority, string[] keywords) => new()
    {
        Sector = sector, IntentName = name, Description = desc, Category = cat,
        Priority = priority, KeywordsJson = JsonSerializer.Serialize(keywords), ExamplesJson = "[]", IsActive = true
    };

    private static SectorFaqRecord F(string sector, string q, string a, string cat, float score) => new()
    {
        Sector = sector, Question = q, Answer = a, Category = cat, EffectivenessScore = score, IsActive = true
    };

    private static SectorFlowRecord Fl(string sector, string name, string type, string desc, string stagesJson) => new()
    {
        Sector = sector, FlowName = name, FlowType = type, Description = desc, StagesJson = stagesJson, IsActive = true
    };

    private static SectorObjectionHandlerRecord OH(string sector, string type, string label, string responsesJson) => new()
    {
        Sector = sector, ObjectionType = type, ObjectionLabel = label, ResponseTemplatesJson = responsesJson, IsActive = true
    };

    private static SectorFollowupTemplateRecord FU(string sector, string type, string label, string msg, int delayHours) => new()
    {
        Sector = sector, TemplateType = type, TemplateLabel = label, MessageTemplate = msg, OptimalDelayHours = delayHours, IsActive = true
    };

    private static SectorOnboardingStepRecord S(string sector, int num, string action, string desc, string impact, string dayRange) => new()
    {
        Sector = sector, StepNumber = num, Action = action, Description = desc, ExpectedImpact = impact, DayRange = dayRange, IsActive = true
    };
}
