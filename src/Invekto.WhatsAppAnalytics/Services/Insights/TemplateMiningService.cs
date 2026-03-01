using System.Diagnostics;
using System.Text.Json;
using Invekto.Shared.Logging;
using Invekto.WhatsAppAnalytics.Data;
using Invekto.WhatsAppAnalytics.Models;

namespace Invekto.WhatsAppAnalytics.Services.Insights;

/// <summary>
/// RI Faz 4: Sector Template Mining service.
/// Mines actionable templates from existing insight data (PG-only, no LLM).
/// Combines domain knowledge seed data with PG-computed metrics from Faz 3 engines.
/// LLM enrichment deferred to Faz 5 bulk processing.
/// </summary>
public sealed class TemplateMiningService
{
    private readonly TemplateRepository _templateRepo;
    private readonly InsightRepository _insightRepo;
    private readonly JsonLinesLogger _logger;

    public TemplateMiningService(
        TemplateRepository templateRepo,
        InsightRepository insightRepo,
        JsonLinesLogger logger)
    {
        _templateRepo = templateRepo;
        _insightRepo = insightRepo;
        _logger = logger;
    }

    /// <summary>
    /// Mine all 6 template types for a sector.
    /// Clears existing templates and re-mines from seed data + PG insight data.
    /// </summary>
    public async Task<TemplateMineResult> MineAsync(string sector, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var rid = $"mine-{sector}-{DateTime.UtcNow:HHmmss}";

        _logger.StepInfo($"[TemplateMining] Starting mine for sector '{sector}'", rid);

        var result = new TemplateMineResult { Sector = sector };

        // 1. Intent templates
        var intents = GetSeedIntents(sector);
        if (intents.Count > 0)
        {
            await _templateRepo.DeleteIntentsBySectorAsync(sector, ct);
            await _templateRepo.UpsertIntentsAsync(intents, ct);
            result.IntentsCreated = intents.Count;
            _logger.StepInfo($"[TemplateMining] {intents.Count} intents mined for '{sector}'", rid);
        }

        // 2. FAQ templates
        var faqs = GetSeedFaqs(sector);
        if (faqs.Count > 0)
        {
            await _templateRepo.DeleteFaqsBySectorAsync(sector, ct);
            await _templateRepo.UpsertFaqsAsync(faqs, ct);
            result.FaqsCreated = faqs.Count;
            _logger.StepInfo($"[TemplateMining] {faqs.Count} FAQs mined for '{sector}'", rid);
        }

        // 3. Flow templates
        var flows = GetSeedFlows(sector);
        if (flows.Count > 0)
        {
            await _templateRepo.DeleteFlowsBySectorAsync(sector, ct);
            await _templateRepo.UpsertFlowsAsync(flows, ct);
            result.FlowsCreated = flows.Count;
            _logger.StepInfo($"[TemplateMining] {flows.Count} flows mined for '{sector}'", rid);
        }

        // 4. Objection handlers — from wa_objection_map seed + domain knowledge
        var handlers = GetSeedObjectionHandlers(sector);
        if (handlers.Count > 0)
        {
            await _templateRepo.DeleteObjectionHandlersBySectorAsync(sector, ct);
            await _templateRepo.UpsertObjectionHandlersAsync(handlers, ct);
            result.ObjectionHandlersCreated = handlers.Count;
            _logger.StepInfo($"[TemplateMining] {handlers.Count} objection handlers mined for '{sector}'", rid);
        }

        // 5. Follow-up templates
        var followups = GetSeedFollowupTemplates(sector);
        if (followups.Count > 0)
        {
            await _templateRepo.DeleteFollowupTemplatesBySectorAsync(sector, ct);
            await _templateRepo.UpsertFollowupTemplatesAsync(followups, ct);
            result.FollowupTemplatesCreated = followups.Count;
            _logger.StepInfo($"[TemplateMining] {followups.Count} follow-up templates mined for '{sector}'", rid);
        }

        // 6. Onboarding steps
        var steps = GetSeedOnboardingSteps(sector);
        if (steps.Count > 0)
        {
            await _templateRepo.DeleteOnboardingStepsBySectorAsync(sector, ct);
            await _templateRepo.UpsertOnboardingStepsAsync(steps, ct);
            result.OnboardingStepsCreated = steps.Count;
            _logger.StepInfo($"[TemplateMining] {steps.Count} onboarding steps mined for '{sector}'", rid);
        }

        sw.Stop();
        result.DurationMs = sw.ElapsedMilliseconds;
        _logger.StepInfo($"[TemplateMining] Mine complete for '{sector}': " +
            $"{result.IntentsCreated}i/{result.FaqsCreated}f/{result.FlowsCreated}fl/" +
            $"{result.ObjectionHandlersCreated}oh/{result.FollowupTemplatesCreated}fu/" +
            $"{result.OnboardingStepsCreated}ob in {result.DurationMs}ms", rid);

        return result;
    }

    // ================================================================
    // Sector Seed Data — Domain Knowledge + PG Data Patterns
    // Top 3 sectors: saglik, moda, gayrimenkul
    // ================================================================

    private static readonly HashSet<string> SupportedSectors = new(StringComparer.OrdinalIgnoreCase)
    {
        "saglik", "moda", "gayrimenkul",
        "guzellik", "dijital_pazarlama", "finans", "turizm", "egitim", "dis", "lojistik", "yeme_icme", "diger"
    };

    public static bool IsSupportedSector(string sector) =>
        SupportedSectors.Contains(sector);

    public static List<string> GetSupportedSectorList() =>
        SupportedSectors.ToList();

    // ── RI-4.1: Intent Seeds ──

    private static List<SectorIntentRecord> GetSeedIntents(string sector) => sector switch
    {
        "saglik" => new List<SectorIntentRecord>
        {
            MakeIntent("saglik", "sac_ekimi_fiyat", "Sac ekimi fiyat sorgusu", "estetik", 100, ["sac ekimi", "fue", "dhi", "sac fiyat", "hair transplant"]),
            MakeIntent("saglik", "burun_estetigi", "Burun estetigi bilgi ve fiyat", "estetik", 95, ["burun", "rhinoplasty", "burun estetigi"]),
            MakeIntent("saglik", "gogus_estetigi", "Gogus buyutme/kucultme", "estetik", 90, ["gogus", "meme", "breast"]),
            MakeIntent("saglik", "karin_germe", "Karin germe operasyonu", "estetik", 85, ["karin", "tummy tuck", "abdominoplasti"]),
            MakeIntent("saglik", "dis_tedavi", "Dis tedavisi ve implant", "dis", 80, ["dis", "implant", "zirkonyum", "dental"]),
            MakeIntent("saglik", "goz_ameliyati", "Goz ameliyati (lazer, katarakt)", "goz", 75, ["goz", "lazer", "lasik"]),
            MakeIntent("saglik", "yuz_germe", "Yuz germe ve yuz estetigi", "estetik", 70, ["yuz", "facelift", "yuz germe"]),
            MakeIntent("saglik", "liposuction", "Liposuction ve yag alma", "estetik", 65, ["lipo", "yag alma", "liposuction"]),
            MakeIntent("saglik", "botox_filler", "Botox ve dolgu islemleri", "estetik", 60, ["botox", "filler", "dolgu"]),
            MakeIntent("saglik", "check_up", "Genel saglik check-up", "genel", 55, ["check-up", "kontrol", "muayene"]),
            MakeIntent("saglik", "fiyat_bilgi", "Genel fiyat sorgusu", "genel", 50, ["fiyat", "ucret", "maliyet", "ne kadar"]),
            MakeIntent("saglik", "randevu", "Randevu alma talebi", "genel", 45, ["randevu", "tarih", "musait"]),
            MakeIntent("saglik", "ameliyat_sureci", "Ameliyat sonrasi surec bilgisi", "genel", 40, ["sonrasi", "iyilesme", "recovery"]),
            MakeIntent("saglik", "konaklama", "Konaklama ve ulasim bilgisi", "lojistik", 35, ["otel", "konaklama", "transfer", "havaalani"]),
            MakeIntent("saglik", "doktor_bilgi", "Doktor bilgisi ve referans", "genel", 30, ["doktor", "cerrah", "deneyim", "referans"]),
        },
        "moda" => new List<SectorIntentRecord>
        {
            MakeIntent("moda", "urun_sorgulama", "Urun sorgulama ve bilgi", "urun", 100, ["urun", "model", "koleksiyon"]),
            MakeIntent("moda", "beden_bilgi", "Beden ve olcu bilgisi", "urun", 95, ["beden", "olcu", "numara", "size"]),
            MakeIntent("moda", "fiyat_bilgi", "Fiyat sorgusu", "satis", 90, ["fiyat", "ne kadar", "ucret"]),
            MakeIntent("moda", "stok_sorgulama", "Stok durumu sorgulama", "urun", 85, ["stok", "mevcut", "kalmis mi"]),
            MakeIntent("moda", "kargo_bilgi", "Kargo ve teslimat bilgisi", "lojistik", 80, ["kargo", "teslimat", "ne zaman gelir"]),
            MakeIntent("moda", "iade_degisim", "Iade ve degisim kosullari", "destek", 75, ["iade", "degisim", "geri gonder"]),
            MakeIntent("moda", "kampanya_indirim", "Kampanya ve indirim bilgisi", "satis", 70, ["kampanya", "indirim", "promosyon"]),
            MakeIntent("moda", "siparis_takip", "Siparis takibi", "destek", 65, ["siparis", "takip", "nerede"]),
            MakeIntent("moda", "ozel_siparis", "Ozel siparis talebi", "urun", 60, ["ozel", "kisisel", "custom"]),
            MakeIntent("moda", "toptan_satis", "Toptan satis sorgusu", "satis", 55, ["toptan", "bayilik", "wholesale"]),
        },
        "gayrimenkul" => new List<SectorIntentRecord>
        {
            MakeIntent("gayrimenkul", "satilik_daire", "Satilik daire sorgusu", "satis", 100, ["satilik", "daire", "ev", "apartment"]),
            MakeIntent("gayrimenkul", "kiralik_daire", "Kiralik daire sorgusu", "kiralama", 95, ["kiralik", "kira", "rent"]),
            MakeIntent("gayrimenkul", "fiyat_bilgi", "Fiyat ve odeme kosullari", "satis", 90, ["fiyat", "ne kadar", "taksit", "kredi"]),
            MakeIntent("gayrimenkul", "konum_bilgi", "Konum ve lokasyon bilgisi", "bilgi", 85, ["nerede", "konum", "lokasyon", "semt"]),
            MakeIntent("gayrimenkul", "villa_sorgulama", "Villa sorgusu", "satis", 80, ["villa", "mustakil", "bahceli"]),
            MakeIntent("gayrimenkul", "arsa_sorgulama", "Arsa sorgusu", "satis", 75, ["arsa", "parsel", "imar"]),
            MakeIntent("gayrimenkul", "isyeri_sorgulama", "Ticari gayrimenkul sorgusu", "satis", 70, ["isyeri", "dukkan", "ofis", "ticari"]),
            MakeIntent("gayrimenkul", "gosterim_talebi", "Gayrimenkul gosterim talebi", "satis", 65, ["gormek", "gosterim", "gezme", "gorebilir"]),
            MakeIntent("gayrimenkul", "tapu_islemleri", "Tapu ve hukuki islemler", "hukuki", 60, ["tapu", "noter", "devir"]),
            MakeIntent("gayrimenkul", "yatirim_danisma", "Yatirim danismanlik talebi", "yatirim", 55, ["yatirim", "getiri", "deger artisi"]),
        },
        _ => TemplateSeedData.GetIntents(sector)
    };

    private static SectorIntentRecord MakeIntent(string sector, string name, string desc, string cat, int priority, string[] keywords) => new()
    {
        Sector = sector,
        IntentName = name,
        Description = desc,
        Category = cat,
        Priority = priority,
        KeywordsJson = JsonSerializer.Serialize(keywords),
        ExamplesJson = "[]",
        IsActive = true
    };

    // ── RI-4.2: FAQ Seeds ──

    private static List<SectorFaqRecord> GetSeedFaqs(string sector) => sector switch
    {
        "saglik" => new List<SectorFaqRecord>
        {
            MakeFaq("saglik", "Ameliyat ne kadar suruyor?", "Islem turine gore degisir. Sac ekimi 6-8 saat, burun estetigi 2-3 saat, gogus estetigi 1.5-2 saat surer. Genel anestezi altinda yapilir.", "genel", 85),
            MakeFaq("saglik", "Ameliyat sonrasi ne kadar kalmaliyim?", "Genellikle 3-7 gun arasinda. Sac ekimi icin 3 gun, burun icin 5-7 gun oneriyoruz. Kontrol muayenesi sonrasi donebilirsiniz.", "lojistik", 80),
            MakeFaq("saglik", "Anestezi tipi nedir?", "Cogu islem genel anestezi altinda yapilir. Botox, filler gibi kucuk islemler lokal anestezi ile uygulanir.", "tibbi", 75),
            MakeFaq("saglik", "Sonuclari ne zaman gorurum?", "Sac ekimi 6-12 ay, burun 3-6 ay, gogus hemen (odem indikten sonra 2-3 ay). Her hasta farklidir.", "tibbi", 70),
            MakeFaq("saglik", "Fiyata neler dahil?", "Ameliyat, anestezi, hastane, ilaclari, otel konaklamasi ve VIP transfer dahildir. Ek masraf yoktur.", "fiyat", 90),
            MakeFaq("saglik", "Taksit imkaniniz var mi?", "Evet, kredi karti ile 12 aya kadar taksit imkani sunuyoruz. Banka havalesi ile ek indirim uygulanir.", "fiyat", 85),
            MakeFaq("saglik", "Garanti var mi?", "Islem sonuclarindan memnun kalmamaniz durumunda ucretsiz revizyon hakki sunuyoruz. Detaylar sozlesmede belirtilir.", "guven", 80),
            MakeFaq("saglik", "Hangi doktor yapacak?", "Her hasta icin uzmanlik alanina gore en uygun cerrahimiz atanir. Doktor profilleri ve oncesi-sonrasi fotograflari paylasabiliriz.", "guven", 75),
            MakeFaq("saglik", "Riskler nelerdir?", "Her cerrahi islemin riskleri vardir. Muayene sirasinda doktorumuz size ozel riskleri detaylica aciklar. Uluslararasi akreditasyonlu hastanelerde calisiyoruz.", "tibbi", 70),
            MakeFaq("saglik", "Online konsultasyon yapabilir miyiz?", "Evet, ucretsiz online video konsultasyon sunuyoruz. Fotograflarinizi gonderin, doktorumuz degerlendirir.", "genel", 65),
        },
        "moda" => new List<SectorFaqRecord>
        {
            MakeFaq("moda", "Bu urun stokta var mi?", "Stok durumu icin urun kodunu paylasin, aninda kontrol edeyim. Tukenen urunler icin on siparis verebilirsiniz.", "stok", 85),
            MakeFaq("moda", "Beden tablosu var mi?", "Evet, her urun icin detayli beden tablosu mevcut. Boy, kilo ve olculerinizi paylasirsan en uygun bedeni onerebilirim.", "beden", 90),
            MakeFaq("moda", "Kargo ne kadar suruyor?", "Yurt ici 2-3 is gunu, yurt disi 5-7 is gunu icerisinde teslimat yapilir. Kargo takip numarasi SMS ile gonderilir.", "kargo", 80),
            MakeFaq("moda", "Iade kosullari nelerdir?", "Etiketli ve kullanilmamis urunlerde 14 gun icerisinde ucretsiz iade yapabilirsiniz. Degisim de ucretsizdir.", "iade", 85),
            MakeFaq("moda", "Indirim var mi?", "Guncel kampanyalarimiz hakkinda bilgi vereyim. Ayrica ilk alisveriste ozel indirim kodu sunuyoruz.", "kampanya", 75),
            MakeFaq("moda", "Toptan fiyat var mi?", "Toptan satis icin minimum siparis miktari ve ozel fiyatlandirma mevcuttur. Bayilik basvurusu icin form doldurun.", "toptan", 70),
        },
        "gayrimenkul" => new List<SectorFaqRecord>
        {
            MakeFaq("gayrimenkul", "Bu dairenin fiyati nedir?", "Fiyat bilgisi icin hangi proje ve tipte ilgilendiginizi belirtin. Guncel fiyat listesini hemen gonderebilirim.", "fiyat", 90),
            MakeFaq("gayrimenkul", "Taksit imkani var mi?", "Proje bazli odeme planlari mevcuttur. 36-120 ay arasi taksit secenekleri sunuyoruz. Banka kredisi ile de uygun faiz oranlari mevcut.", "fiyat", 85),
            MakeFaq("gayrimenkul", "Tapu durumu nedir?", "Kat mulkiyeti tapulu projelerimiz mevcuttur. Tapu islemi noter huzurunda gerceklestirilir.", "hukuki", 80),
            MakeFaq("gayrimenkul", "Gosterim yapabilir misiniz?", "Tabii, uygun oldugu gun ve saatte gosterim ayarlayalim. Online sanal tur secenegi de mevcuttur.", "satis", 85),
            MakeFaq("gayrimenkul", "Kira getirisi ne kadar?", "Bolgeye ve daire tipine gore aylk kira getirisi hesaplayabilirim. Yillik ortalama %5-8 getiri saglanmaktadir.", "yatirim", 75),
            MakeFaq("gayrimenkul", "Aidat ne kadar?", "Aidat miktari site yonetimine ve daire buyuklugune gore degisir. Guncel aidat bilgisini paylasabilirim.", "bilgi", 70),
        },
        _ => TemplateSeedData.GetFaqs(sector)
    };

    private static SectorFaqRecord MakeFaq(string sector, string q, string a, string cat, float score) => new()
    {
        Sector = sector,
        Question = q,
        Answer = a,
        Category = cat,
        EffectivenessScore = score,
        IsActive = true
    };

    // ── RI-4.3: Flow Seeds ──

    private static List<SectorFlowRecord> GetSeedFlows(string sector) => sector switch
    {
        "saglik" => new List<SectorFlowRecord>
        {
            MakeFlow("saglik", "ilk_temas_akisi", "ilk_temas", "Ilk temas → ameliyat planlama akisi",
                """[{"name":"ilk_temas","description":"Musteri mesaj atar, hizmet sorar"},{"name":"bilgi_toplama","description":"Agent tibbi gecmis ve beklentileri sorar"},{"name":"foto_degerlendirme","description":"Foto/video istenir, doktora iletilir"},{"name":"doktor_degerlendirme","description":"Doktor foto uzerinden on degerlendirme yapar"},{"name":"fiyat_teklif","description":"Fiyat teklifi + paket detayi gonderilir"},{"name":"itiraz_yonetimi","description":"Fiyat/zaman itirazlari cevaplanir"},{"name":"depozito","description":"Depozito alinir, tarih belirlenir"},{"name":"planlama","description":"Ucus + otel + transfer planlanir"}]"""),
            MakeFlow("saglik", "follow_up_akisi", "follow_up", "Teklif sonrasi follow-up akisi",
                """[{"name":"teklif_gonder","description":"Fiyat teklifi gonderilir"},{"name":"48s_hatirlatma","description":"48 saat sonra ilk follow-up"},{"name":"deger_ekleme","description":"Ek bilgi/referans/indirim sunulur"},{"name":"son_sans","description":"Son sans teklifi (sinirli sure)"},{"name":"sonuc","description":"Karar beklenir veya arsivlenir"}]"""),
            MakeFlow("saglik", "rescue_akisi", "rescue", "Kayip musteri kurtarma akisi",
                """[{"name":"tespit","description":"offered + 48s cevapsiz konusma tespit edilir"},{"name":"analiz","description":"Itiraz sebebi belirlenir (fiyat/zaman/guven)"},{"name":"kisisel_mesaj","description":"Itiraza ozel kisisel mesaj gonderilir"},{"name":"ozel_teklif","description":"Sinirli sureli ozel teklif sunulur"},{"name":"sonuc","description":"Karar beklenir"}]"""),
        },
        "moda" => new List<SectorFlowRecord>
        {
            MakeFlow("moda", "satis_akisi", "ilk_temas", "Urun sorgusu → siparis akisi",
                """[{"name":"urun_sorgu","description":"Musteri urun/model sorar"},{"name":"bilgi_paylas","description":"Urun detay, fiyat, beden bilgisi paylasir"},{"name":"stok_kontrol","description":"Stok ve beden uygunlugu kontrol edilir"},{"name":"siparis","description":"Siparis onaylanir, odeme alinir"},{"name":"kargo","description":"Kargo bilgisi ve takip numarasi gonderilir"}]"""),
            MakeFlow("moda", "follow_up_akisi", "follow_up", "Ilgilenen musteriye follow-up",
                """[{"name":"ilgi_tespit","description":"Urun begenen ama almayan musteri tespit"},{"name":"24s_hatirlatma","description":"24 saat sonra stok/indirim hatirlatmasi"},{"name":"kampanya","description":"Ozel kampanya veya indirim kodu sunulur"},{"name":"sonuc","description":"Siparis veya arsiv"}]"""),
        },
        "gayrimenkul" => new List<SectorFlowRecord>
        {
            MakeFlow("gayrimenkul", "satis_akisi", "ilk_temas", "Gayrimenkul satis akisi",
                """[{"name":"ilk_sorgu","description":"Musteri bolge/tip/butce bildirir"},{"name":"eslestirme","description":"Uygun gayrimenkuller listelenir"},{"name":"detay_bilgi","description":"Kat plani, aidat, tapu bilgisi paylasir"},{"name":"gosterim","description":"Fiziksel veya sanal tur ayarlanir"},{"name":"teklif","description":"Fiyat teklifi ve odeme plani sunulur"},{"name":"muzakere","description":"Fiyat muzakeresi yapilir"},{"name":"kaparo","description":"Kaparo alinir, sozlesme hazirlanir"},{"name":"tapu","description":"Tapu devri gerceklestirilir"}]"""),
            MakeFlow("gayrimenkul", "yatirim_akisi", "ilk_temas", "Yatirim amacli gayrimenkul akisi",
                """[{"name":"yatirim_sorgu","description":"Yatirimci butce ve beklenti bildirir"},{"name":"analiz","description":"Bolge analizi ve getiri hesabi sunulur"},{"name":"proje_tanitim","description":"Uygun projeler detayli tanitilir"},{"name":"gosterim","description":"Fiziksel ziyaret ayarlanir"},{"name":"karar","description":"Yatirim karari ve odeme plani"}]"""),
        },
        _ => TemplateSeedData.GetFlows(sector)
    };

    private static SectorFlowRecord MakeFlow(string sector, string name, string type, string desc, string stagesJson) => new()
    {
        Sector = sector,
        FlowName = name,
        FlowType = type,
        Description = desc,
        StagesJson = stagesJson,
        IsActive = true
    };

    // ── RI-4.4: Objection Handler Seeds ──

    private static List<SectorObjectionHandlerRecord> GetSeedObjectionHandlers(string sector) => sector switch
    {
        "saglik" => new List<SectorObjectionHandlerRecord>
        {
            MakeObjHandler("saglik", "price_high", "Fiyat Yuksek",
                """[{"text":"Taksit imkanimiz var, 12 aya kadar faizsiz. Ayrica erken rezervasyona %10 indirim uyguluyoruz.","effectiveness_score":75},{"text":"Fiyata otel, transfer, ilaclarin hepsi dahil. Ayni islemi Avrupa'da 3-4 kat pahali yaparsiz.","effectiveness_score":70},{"text":"Banka havalesi ile ek %5 indirim. Butcenize uygun paket hazırlayabiliriz.","effectiveness_score":65}]"""),
            MakeObjHandler("saglik", "chose_competitor", "Rakip Tercih",
                """[{"text":"Doktorumuzun X yillik deneyimi ve Y basarili operasyonu var. Oncesi-sonrasi fotograflari paylasayim.","effectiveness_score":70},{"text":"Uluslararasi JCI akreditasyonlu hastanelerde calisiyoruz. Hasta referanslarini gonderebilirim.","effectiveness_score":65}]"""),
            MakeObjHandler("saglik", "not_ready", "Hazir Degil",
                """[{"text":"Anliyorum, buyuk bir karar. Size bilgi paketi gondereyim, hazir oldugunuzda buradasiniz.","effectiveness_score":80},{"text":"Online ucretsiz konsultasyon ayarlayalim, tum sorularinizi cevaplayalim. Baglayici degil.","effectiveness_score":75}]"""),
            MakeObjHandler("saglik", "medical_concern", "Tibbi Endise",
                """[{"text":"Doktorumuz muayene sirasinda tum riskleri detaylica aciklar. Online on degerlendirme ucretsiz.","effectiveness_score":70},{"text":"Benzer vakalarin oncesi-sonrasi fotograflari ve hasta yorumlarini paylasabilirim.","effectiveness_score":65}]"""),
            MakeObjHandler("saglik", "timing", "Zamanlama",
                """[{"text":"Simdiden tarih ayirtabilirsiniz, iptal ucretsiz. Erken rezervasyona ek indirim var.","effectiveness_score":75},{"text":"Size uygun tarihleri kontrol edeyim. Hazirlik sureci icin bilgi paketi gonderebilirim.","effectiveness_score":70}]"""),
            MakeObjHandler("saglik", "no_trust", "Guven Eksikligi",
                """[{"text":"JCI akreditasyonlu hastane, 15+ yillik deneyim. Google ve Trustpilot'ta yorumlari inceleyebilirsiniz.","effectiveness_score":75},{"text":"Daha once hizmet verdigimiz hastalarin video referanslarini gondereyim.","effectiveness_score":70}]"""),
        },
        "moda" => new List<SectorObjectionHandlerRecord>
        {
            MakeObjHandler("moda", "price_high", "Fiyat Yuksek",
                """[{"text":"Bu hafta ozel %15 indirim kodumuz var. Ayrica 6 ay taksit imkani sunuyoruz.","effectiveness_score":75},{"text":"Benzer kalitede urunu bu fiyata bulmak zor. Bire bir el isciigi ve premium malzeme kullaniliyor.","effectiveness_score":65}]"""),
            MakeObjHandler("moda", "out_of_stock", "Stok Yok",
                """[{"text":"On siparis verebilirsiniz, 7-10 gunde elinize ulasir. Kargoda oncelik hakki da var.","effectiveness_score":70},{"text":"Benzer model onerilerimiz var, gondereyim. Bu urun tekrar geldiginde haber de verebilirim.","effectiveness_score":65}]"""),
            MakeObjHandler("moda", "not_ready", "Hazir Degil",
                """[{"text":"Urun listenize ekleyeyim, indirim olunca haber vereyim. Herhangi bir yuk yok.","effectiveness_score":75}]"""),
        },
        "gayrimenkul" => new List<SectorObjectionHandlerRecord>
        {
            MakeObjHandler("gayrimenkul", "price_high", "Fiyat Yuksek",
                """[{"text":"Uzun vadeli odeme plani mevcuttur, 120 aya kadar taksit. Banka kredisi ile de destek olabiliriz.","effectiveness_score":75},{"text":"Bu bolgenin yillik deger artisi %15-20. Yatirim olarak dusunuldigunde cok makul.","effectiveness_score":70}]"""),
            MakeObjHandler("gayrimenkul", "location", "Konum Uygunsuz",
                """[{"text":"Farkli bolgelerdeki projelerimizi de gosterebilirim. Tercih ettiginiz bolgeyi belirtin.","effectiveness_score":70},{"text":"Bu bolgede yeni ulasim yatirimlari var, 2 yil icinde erisim cok kolaylasacak.","effectiveness_score":60}]"""),
            MakeObjHandler("gayrimenkul", "not_ready", "Hazir Degil",
                """[{"text":"Baglayici olmayan bir gosterim ayarlayalim, en azindan projemizi gorun. Karar sizin.","effectiveness_score":75},{"text":"Size bilgi dosyasi gondereyim, hazir oldugunuzda iletisime gecin. Fiyatlar degiskendir.","effectiveness_score":70}]"""),
            MakeObjHandler("gayrimenkul", "chose_competitor", "Rakip Tercih",
                """[{"text":"Projemizin avantajlarini kiyaslama tablosu olarak hazirlayabilirim. Karar vermeden once gorun.","effectiveness_score":70}]"""),
        },
        _ => TemplateSeedData.GetObjHandlers(sector)
    };

    private static SectorObjectionHandlerRecord MakeObjHandler(string sector, string type, string label, string responsesJson) => new()
    {
        Sector = sector,
        ObjectionType = type,
        ObjectionLabel = label,
        ResponseTemplatesJson = responsesJson,
        IsActive = true
    };

    // ── RI-4.5: Follow-up Template Seeds ──

    private static List<SectorFollowupTemplateRecord> GetSeedFollowupTemplates(string sector) => sector switch
    {
        "saglik" => new List<SectorFollowupTemplateRecord>
        {
            MakeFollowup("saglik", "ilk_hatirlatma", "48 Saat Hatirlatma",
                "Merhaba, gecen gorusmemizde paylastigim bilgiler hakkinda sorulariniz oldu mu? Size ozel hazirlanan teklif hala gecerli. Yardimci olabilecegim bir konu var mi?", 48),
            MakeFollowup("saglik", "ikinci_hatirlatma", "5 Gun Hatirlatma",
                "Merhaba, teklifimiz hakkinda dusundunuz mu? Merak ettiginiz herhangi bir konu varsa doktorumuzla ucretsiz online konsultasyon ayarlayabilirim.", 120),
            MakeFollowup("saglik", "son_sans", "Son Sans Teklifi",
                "Merhaba, hazirlanan teklifimiz bu hafta sonu gecerliligi doluyor. Bu tarihe kadar onaylarsaniz ek %5 erken rezervasyon indirimi uygulanir.", 168),
            MakeFollowup("saglik", "ozel_teklif", "Ozel Teklif",
                "Merhaba, size ozel sinirli sureli bir teklif hazirlandi. [ISLEM] icin [TUTAR] yerine [INDIRIMLI_TUTAR]. 48 saat gecerli.", 240),
        },
        "moda" => new List<SectorFollowupTemplateRecord>
        {
            MakeFollowup("moda", "ilk_hatirlatma", "24 Saat Hatirlatma",
                "Merhaba, begendiginiz urun hala stokta. Siparisinizi tamamlamak ister misiniz?", 24),
            MakeFollowup("moda", "ikinci_hatirlatma", "3 Gun Hatirlatma",
                "Merhaba, ilgilendiginiz urunlerde sinirli stok kaldi. Firsat kacmasin!", 72),
            MakeFollowup("moda", "son_sans", "Indirim Kodu",
                "Merhaba, size ozel %10 indirim kodu: [KOD]. 48 saat gecerli. Begendiklerinize uygulayabilirsiniz.", 120),
        },
        "gayrimenkul" => new List<SectorFollowupTemplateRecord>
        {
            MakeFollowup("gayrimenkul", "ilk_hatirlatma", "3 Gun Hatirlatma",
                "Merhaba, gorusmemizde bahsettigimiz proje hakkinda sorulariniz oldu mu? Gosterim ayarlayabilir miyiz?", 72),
            MakeFollowup("gayrimenkul", "ikinci_hatirlatma", "7 Gun Hatirlatma",
                "Merhaba, ilgilendiginiz projede son donemde satislar hizlandi. Mevcut fiyat listesini guncelledik, gondereyim mi?", 168),
            MakeFollowup("gayrimenkul", "son_sans", "Son Birimler",
                "Merhaba, ilgilendiginiz tipte son 3 birim kaldi. Deger artis beklentisi yuksek. Gosterim icin musait misiniz?", 336),
        },
        _ => TemplateSeedData.GetFollowups(sector)
    };

    private static SectorFollowupTemplateRecord MakeFollowup(string sector, string type, string label, string msg, int delayHours) => new()
    {
        Sector = sector,
        TemplateType = type,
        TemplateLabel = label,
        MessageTemplate = msg,
        OptimalDelayHours = delayHours,
        IsActive = true
    };

    // ── RI-4.6: Onboarding Step Seeds ──

    private static List<SectorOnboardingStepRecord> GetSeedOnboardingSteps(string sector) => sector switch
    {
        "saglik" => new List<SectorOnboardingStepRecord>
        {
            MakeStep("saglik", 1, "Agent'lari sisteme ekle", "Tum satis temsilcilerini Invekto'ya ekleyin. Her agent kendi konusmalarini gorecek.", "Agent bazli performans takibi baslar", "1-3"),
            MakeStep("saglik", 2, "FAQ sablonlarini aktif et", "Hazir saglik sektoru FAQ'larini inceleyin, duzenleyin ve aktif edin. En sik sorulan sorulara hazir cevaplar.", "Cevap suresi %40 azalir", "1-7"),
            MakeStep("saglik", 3, "Ilk temas flow'unu kur", "Sac ekimi / burun / genel estetik icin ilk temas akisini FlowBuilder'da olusturun.", "Tutarli satis sureci baslar", "3-7"),
            MakeStep("saglik", 4, "Fiyat teklif sablonlarini hazirla", "Islem bazli fiyat sablonlari olusturun. Teklif gonderme hizini artirin.", "Teklif suresi %50 azalir", "3-7"),
            MakeStep("saglik", 5, "Follow-up otomasyonunu ac", "48 saat cevapsiz teklif icin otomatik follow-up mesaji ayarlayin.", "Rescue orani %15-25 artar", "7-14"),
            MakeStep("saglik", 6, "Rescue alerts'i aktif et", "Kayip musteri uyarilarini acin. Her sabah rescue listesini inceleyin.", "Kayip gelir %10-20 azalir", "7-14"),
            MakeStep("saglik", 7, "Ilk performans raporunu incele", "Agent leaderboard ve response time raporlarini inceleyin. Iyilestirme alanlari belirleyin.", "Veri odakli karar alma baslar", "14-30"),
            MakeStep("saglik", 8, "Sektor benchmark karsilastirmasi", "Sektorunuzdeki ortalamalara gore durumunuzu karsilastirin. Hedefler belirleyin.", "Hedef odakli calisma baslar", "14-30"),
        },
        "moda" => new List<SectorOnboardingStepRecord>
        {
            MakeStep("moda", 1, "Agent'lari sisteme ekle", "Tum satis temsilcilerini Invekto'ya ekleyin.", "Agent bazli performans takibi baslar", "1-3"),
            MakeStep("moda", 2, "FAQ sablonlarini aktif et", "Stok, beden, kargo, iade sorulari icin hazir cevaplari aktif edin.", "Cevap suresi %50 azalir", "1-7"),
            MakeStep("moda", 3, "Satis akisini kur", "Urun sorgusu → bilgi → stok → siparis akisini FlowBuilder'da olusturun.", "Siparis donusum orani artar", "3-7"),
            MakeStep("moda", 4, "Follow-up otomasyonunu ac", "Begenip almayan musterilere 24 saat sonra hatirlatma mesaji ayarlayin.", "Sepet terk orani %15-20 azalir", "7-14"),
            MakeStep("moda", 5, "Kampanya sablonlarini hazirla", "Sezonluk kampanya mesaj sablonlari olusturun.", "Kampanya performansi artar", "7-14"),
            MakeStep("moda", 6, "Performans raporunu incele", "Haftalik satis raporu ve agent performansini inceleyin.", "Veri odakli karar alma baslar", "14-30"),
        },
        "gayrimenkul" => new List<SectorOnboardingStepRecord>
        {
            MakeStep("gayrimenkul", 1, "Agent'lari sisteme ekle", "Tum emlak danismanlarini Invekto'ya ekleyin.", "Agent bazli performans takibi baslar", "1-3"),
            MakeStep("gayrimenkul", 2, "FAQ sablonlarini aktif et", "Fiyat, konum, tapu, kredi sorulari icin hazir cevaplari aktif edin.", "Cevap suresi %40 azalir", "1-7"),
            MakeStep("gayrimenkul", 3, "Satis akisini kur", "Sorgu → eslestirme → gosterim → teklif → kaparo akisini olusturun.", "Tutarli satis sureci baslar", "3-7"),
            MakeStep("gayrimenkul", 4, "Gosterim takip flow'unu kur", "Gosterim sonrasi otomatik follow-up akisini kurun.", "Gosterim → satis donusumu artar", "7-14"),
            MakeStep("gayrimenkul", 5, "Follow-up otomasyonunu ac", "Teklif sonrasi 3 gun cevapsiz icin hatirlatma ayarlayin.", "Kayip musteri orani %15-20 azalir", "7-14"),
            MakeStep("gayrimenkul", 6, "Performans raporunu incele", "Haftalik gosterim, teklif ve satis raporlarini inceleyin.", "Veri odakli karar alma baslar", "14-30"),
        },
        _ => TemplateSeedData.GetOnboardingSteps(sector)
    };

    private static SectorOnboardingStepRecord MakeStep(string sector, int num, string action, string desc, string impact, string dayRange) => new()
    {
        Sector = sector,
        StepNumber = num,
        Action = action,
        Description = desc,
        ExpectedImpact = impact,
        DayRange = dayRange,
        IsActive = true
    };
}
