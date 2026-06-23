using Chatinbox.Shared.Constants;

namespace Chatinbox.Shared.Contracts.Kanban;

/// <summary>
/// FEAT-PILOT-KANBAN status enum. Migration 035 chk_kanban_cards_status
/// CHECK constraint ile birebir eslesir; yeni deger eklenirse migration
/// CHECK clause ve C# enum es zamanli guncellenmelidir.
/// </summary>
public enum KanbanStatus
{
    Blocked,
    Todo,
    InProgress,
    Backlog,
    Done
}

public static class KanbanStatusExtensions
{
    public const string DbValueBlocked    = "BLOCKED";
    public const string DbValueTodo       = "TODO";
    public const string DbValueInProgress = "IN_PROGRESS";
    public const string DbValueBacklog    = "BACKLOG";
    public const string DbValueDone       = "DONE";

    public static string ToDbValue(this KanbanStatus status) => status switch
    {
        KanbanStatus.Blocked    => DbValueBlocked,
        KanbanStatus.Todo       => DbValueTodo,
        KanbanStatus.InProgress => DbValueInProgress,
        KanbanStatus.Backlog    => DbValueBacklog,
        KanbanStatus.Done       => DbValueDone,
        _ => throw new System.ArgumentOutOfRangeException(nameof(status), status,
                $"[{ErrorCodes.KanbanStatusUnknown}] Tanımlı KanbanStatus enum aralığı dışında değer; " +
                "beklenen: Blocked|Todo|InProgress|Backlog|Done. Bu noktaya ulaşmak internal bug işaretidir " +
                "(PATCH endpoint TryParse zaten geçersiz string'leri INV-KB-003 ile reddeder).")
    };

    public static bool TryParse(string? raw, out KanbanStatus status)
    {
        switch (raw)
        {
            case DbValueBlocked:    status = KanbanStatus.Blocked;    return true;
            case DbValueTodo:       status = KanbanStatus.Todo;       return true;
            case DbValueInProgress: status = KanbanStatus.InProgress; return true;
            case DbValueBacklog:    status = KanbanStatus.Backlog;    return true;
            case DbValueDone:       status = KanbanStatus.Done;       return true;
            default:                status = KanbanStatus.Todo;       return false;
        }
    }
}
