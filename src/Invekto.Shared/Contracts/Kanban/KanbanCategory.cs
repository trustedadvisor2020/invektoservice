using Invekto.Shared.Constants;

namespace Invekto.Shared.Contracts.Kanban;

/// <summary>
/// FEAT-PILOT-KANBAN category enum. Migration 035 chk_kanban_cards_category
/// CHECK constraint ile birebir eslesir.
/// </summary>
public enum KanbanCategory
{
    Customer,
    Ops,
    Dev,
    Decision,
    Ui,
    Doc
}

public static class KanbanCategoryExtensions
{
    public const string DbValueCustomer = "CUSTOMER";
    public const string DbValueOps      = "OPS";
    public const string DbValueDev      = "DEV";
    public const string DbValueDecision = "DECISION";
    public const string DbValueUi       = "UI";
    public const string DbValueDoc      = "DOC";

    public static string ToDbValue(this KanbanCategory cat) => cat switch
    {
        KanbanCategory.Customer => DbValueCustomer,
        KanbanCategory.Ops      => DbValueOps,
        KanbanCategory.Dev      => DbValueDev,
        KanbanCategory.Decision => DbValueDecision,
        KanbanCategory.Ui       => DbValueUi,
        KanbanCategory.Doc      => DbValueDoc,
        _ => throw new System.ArgumentOutOfRangeException(nameof(cat), cat,
                $"[{ErrorCodes.KanbanCategoryUnknown}] Tanımlı KanbanCategory enum aralığı dışında değer; " +
                "beklenen: Customer|Ops|Dev|Decision|Ui|Doc. Bu yola yalnızca migration seed üzerinden " +
                "ulaşılır (kategori user input ile mutate edilmez); ulaşıldıysa internal bug işaretidir.")
    };

    public static bool TryParse(string? raw, out KanbanCategory cat)
    {
        switch (raw)
        {
            case DbValueCustomer: cat = KanbanCategory.Customer; return true;
            case DbValueOps:      cat = KanbanCategory.Ops;      return true;
            case DbValueDev:      cat = KanbanCategory.Dev;      return true;
            case DbValueDecision: cat = KanbanCategory.Decision; return true;
            case DbValueUi:       cat = KanbanCategory.Ui;       return true;
            case DbValueDoc:      cat = KanbanCategory.Doc;      return true;
            default:              cat = KanbanCategory.Ops;      return false;
        }
    }
}
