namespace Invekto.Appointments.Services;

/// <summary>
/// Static step definitions for each treatment lifecycle type.
/// Steps are created in bulk when a lifecycle is started.
/// offset_hours: positive = after reference_date, negative = before reference_date.
/// </summary>
public static class LifecycleStepDefinitions
{
    public static readonly IReadOnlyList<string> ValidLifecycleTypes = new[]
    {
        "post_treatment", "plan_approval", "pre_op"
    };

    /// <summary>
    /// GR-3.20: Post-treatment follow-up chain.
    /// T+1 day: "Nasil hissediyorsunuz?" check-in.
    /// T+7 days: Control questions (agri, sislik).
    /// T+30 days: "Kontrol randevusu alalim mi?" booking offer.
    /// </summary>
    public static readonly IReadOnlyList<StepDefinition> PostTreatment = new[]
    {
        new StepDefinition(
            stepOrder: 1,
            stepKey: "check_in",
            offsetHours: 24,
            messageTemplate: "Merhaba {{patient_name}}, {{treatment_type}} tedaviniz tamamlandi. Nasilsiniz? Herhangi bir sikayetiniz varsa bize yazabilirsiniz.",
            escalationTarget: null),
        new StepDefinition(
            stepOrder: 2,
            stepKey: "control_questions",
            offsetHours: 168, // 7 days
            messageTemplate: "Merhaba {{patient_name}}, tedavinizin uzerinden 1 hafta gecti. Agri, sislik veya rahatsizlik hissediyor musunuz? Durumunuzu bize bildirin.",
            escalationTarget: "doctor"),
        new StepDefinition(
            stepOrder: 3,
            stepKey: "booking_offer",
            offsetHours: 720, // 30 days
            messageTemplate: "Merhaba {{patient_name}}, tedavinizin uzerinden 1 ay gecti. Kontrol randevusu almak ister misiniz? Size uygun bir zaman ayarlayalim.",
            escalationTarget: null),
    };

    /// <summary>
    /// GR-3.41: Treatment plan approval follow-up chain.
    /// T+1 day: "Tedavi planinizi incelediniz mi?"
    /// T+3 days: "Sorulariniz varsa yardimci olabiliriz"
    /// T+7 days: Final reminder + special offer option.
    /// </summary>
    public static readonly IReadOnlyList<StepDefinition> PlanApproval = new[]
    {
        new StepDefinition(
            stepOrder: 1,
            stepKey: "plan_review",
            offsetHours: 24,
            messageTemplate: "Merhaba {{patient_name}}, size gonderdigimiz {{treatment_type}} tedavi planini incelediniz mi? Sorulariniz icin buradayiz.",
            escalationTarget: null),
        new StepDefinition(
            stepOrder: 2,
            stepKey: "plan_questions",
            offsetHours: 72, // 3 days
            messageTemplate: "Merhaba {{patient_name}}, tedavi planinizla ilgili merak ettiginiz bir sey var mi? Size yardimci olmaktan memnuniyet duyariz.",
            escalationTarget: null),
        new StepDefinition(
            stepOrder: 3,
            stepKey: "plan_final_reminder",
            offsetHours: 168, // 7 days
            messageTemplate: "Merhaba {{patient_name}}, tedavi planinizi degerlendirebilmemiz icin son hatirlatmamizi yapmak istiyoruz. Randevu almak icin bize yazmaniz yeterli.",
            escalationTarget: "supervisor"),
    };

    /// <summary>
    /// GR-3.43: Pre-op preparation instructions chain.
    /// T-3 days: Preparation instructions (treatment-type specific).
    /// T-1 day: Reminder.
    /// T-3 hours: Morning of procedure check.
    /// Offset is negative (before appointment date).
    /// </summary>
    public static readonly IReadOnlyList<StepDefinition> PreOp = new[]
    {
        new StepDefinition(
            stepOrder: 1,
            stepKey: "prep_instructions",
            offsetHours: -72, // 3 days before
            messageTemplate: "Merhaba {{patient_name}}, {{treatment_type}} isleminden 3 gun once hatirlatma: Lutfen hazirlik talimatlarini dikkatle okuyun. Sorulariniz icin bize yazin.",
            escalationTarget: null),
        new StepDefinition(
            stepOrder: 2,
            stepKey: "prep_reminder",
            offsetHours: -24, // 1 day before
            messageTemplate: "Merhaba {{patient_name}}, yarin {{treatment_type}} isleminiz var. Hazirliklarinizi tamamladiniz mi? 'Evet' yazarak onaylayabilirsiniz.",
            escalationTarget: null),
        new StepDefinition(
            stepOrder: 3,
            stepKey: "prep_morning",
            offsetHours: -3, // Morning of (3 hours before)
            messageTemplate: "Merhaba {{patient_name}}, bugun {{treatment_type}} isleminiz var. Hazirliklari tamamladigini onaylar misiniz? Klinikte sizi bekliyoruz.",
            escalationTarget: "doctor"),
    };

    /// <summary>
    /// Get step definitions for a lifecycle type.
    /// Returns null for invalid types.
    /// </summary>
    public static IReadOnlyList<StepDefinition>? GetSteps(string lifecycleType)
    {
        return lifecycleType switch
        {
            "post_treatment" => PostTreatment,
            "plan_approval" => PlanApproval,
            "pre_op" => PreOp,
            _ => null
        };
    }
}

/// <summary>
/// Immutable step definition (template for creating DB rows).
/// </summary>
public sealed class StepDefinition
{
    public int StepOrder { get; }
    public string StepKey { get; }

    /// <summary>
    /// Hours relative to reference_date. Positive = after, negative = before.
    /// </summary>
    public int OffsetHours { get; }

    public string MessageTemplate { get; }

    /// <summary>
    /// Escalation target if no response after this step. Null = no escalation.
    /// </summary>
    public string? EscalationTarget { get; }

    public StepDefinition(int stepOrder, string stepKey, int offsetHours, string messageTemplate, string? escalationTarget)
    {
        StepOrder = stepOrder;
        StepKey = stepKey;
        OffsetHours = offsetHours;
        MessageTemplate = messageTemplate;
        EscalationTarget = escalationTarget;
    }
}
