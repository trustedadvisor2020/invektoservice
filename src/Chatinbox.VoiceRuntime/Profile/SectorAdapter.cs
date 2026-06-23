namespace Chatinbox.VoiceRuntime.Profile;

/// <summary>
/// FEAT-VFB F-VR-B Chunk B2 (spec §4 sector_adapter + §8 lead fields): the LANGUAGE layer for a
/// business type. Controls vocabulary (how to address the caller, sector terms), sector-inappropriate
/// banned words, and the extra lead fields to collect beyond the default name + phone. It does NOT
/// carry capability gates — those live in <see cref="CapabilityDefaults"/> (AD-51), because
/// service_business/education/real_estate reuse the generic LANGUAGE adapter while keeping their own
/// capability defaults.
///
/// Registry: 9 BusinessType keys → 6 distinct adapters. service_business / education / real_estate are
/// NOT in the registry, so <see cref="For"/> returns <see cref="GenericAdapter"/> for them (alias).
/// </summary>
public sealed record SectorAdapter(
    BusinessType BusinessType,
    string BusinessDescriptor,                  // header parens, e.g. "saglik klinigi"
    string CustomerNoun,                        // hasta / danisan / musteri / kisi
    string ActionNoun,                          // randevu / muayene randevusu / on gorusme / siparis kontrolu
    string VocabularyGuidance,                  // one sentence: tone + sector terms
    IReadOnlyList<string> BannedWords,          // sector-inappropriate words to avoid
    IReadOnlyList<string> RequiredLeadFields)   // extra lead fields beyond default name + phone
{
    private static readonly IReadOnlyList<string> ClinicBanned = new[] { "musteri", "siparis", "urun" };
    private static readonly IReadOnlyList<string> EcommerceBanned = new[] { "hasta", "randevu", "muayene", "danisan" };

    private static readonly SectorAdapter ClinicAdapter = new(
        BusinessType.Clinic, "saglik klinigi", "hasta", "randevu",
        "Saglik kurumu dili kullan; kisilere 'hasta', islemlere 'randevu' de.",
        ClinicBanned,
        new[] { "ilgilendigi islem/hizmet", "tercih edilen gun/saat araligi" });

    private static readonly SectorAdapter DentalAdapter = new(
        BusinessType.DentalClinic, "dis klinigi", "hasta", "muayene randevusu",
        "Dis sagligi dili kullan; 'hasta', 'muayene randevusu', implant/ortodonti/dolgu gibi dis tedavisi terimleri.",
        ClinicBanned,
        new[] { "ilgilendigi tedavi", "tercih edilen gun/saat", "daha once muayene oldu mu" });

    private static readonly SectorAdapter AestheticAdapter = new(
        BusinessType.AestheticClinic, "estetik klinik", "danisan", "on gorusme",
        "Estetik/bakim dili kullan; kisilere 'danisan', ilk adimda 'on gorusme' de.",
        new[] { "musteri", "siparis", "urun", "hasta" },
        new[] { "ilgilendigi islem", "tercih edilen gun/saat" });

    private static readonly SectorAdapter HairTransplantAdapter = new(
        BusinessType.HairTransplant, "sac ekim merkezi", "danisan", "on gorusme",
        "Sac ekimi dili kullan; 'danisan', sac ekimi/greft/on gorusme terimleri.",
        ClinicBanned,
        new[] { "ilgilendigi islem (sac ekimi vb.)", "tercih edilen gun/saat" });

    private static readonly SectorAdapter EcommerceAdapter = new(
        BusinessType.Ecommerce, "e-ticaret firmasi", "musteri", "siparis kontrolu",
        "E-ticaret dili kullan; kisilere 'musteri', 'siparis'; urun/kargo/iade/stok terimleri.",
        EcommerceBanned,
        new[] { "siparis numarasi (yoksa telefon)", "urun adi", "konu (iade/kargo/degisim vb.)" });

    private static readonly SectorAdapter GenericAdapter = new(
        BusinessType.Generic, "firma", "musteri", "talep",
        "Notr, sektor-bagimsiz profesyonel musteri temsilcisi dili kullan.",
        System.Array.Empty<string>(),
        System.Array.Empty<string>());

    private static readonly IReadOnlyDictionary<BusinessType, SectorAdapter> Registry =
        new Dictionary<BusinessType, SectorAdapter>
        {
            [BusinessType.Clinic] = ClinicAdapter,
            [BusinessType.DentalClinic] = DentalAdapter,
            [BusinessType.AestheticClinic] = AestheticAdapter,
            [BusinessType.HairTransplant] = HairTransplantAdapter,
            [BusinessType.Ecommerce] = EcommerceAdapter,
            [BusinessType.Generic] = GenericAdapter
        };

    /// <summary>
    /// Returns the dedicated adapter for the business type, or the Generic adapter for the three
    /// aliased types (service_business / education / real_estate) — they share generic LANGUAGE but
    /// keep their own <see cref="CapabilityDefaults"/> (AD-51).
    /// </summary>
    public static SectorAdapter For(BusinessType type) =>
        Registry.TryGetValue(type, out var adapter) ? adapter : GenericAdapter;
}
