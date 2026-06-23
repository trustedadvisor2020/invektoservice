namespace Chatinbox.Shared.Constants;

/// <summary>
/// FEAT-DMP: INMA dynamic-message reserved placeholder keys (<c>wapcrm-marketing-api.md</c> §2).
/// INMA chatoperation DynamicMessage substitutes these from the tenant Customer DB.
/// Used by <c>DynamicMessageValidator</c> allowlist + picker UI filter.
/// </summary>
public static class InmaDynamicFieldKeys
{
    public const string Name = "name";
    public const string Email = "email";
    public const string Note = "note";
    public const string PushName = "pushname";
    public const string DataListName = "datalistname";

    /// <summary>name | email | note | pushname | datalistname | cf1..cf10 — lowercase, INMA matches case-insensitively.</summary>
    public static readonly IReadOnlySet<string> Allowlist = new HashSet<string>(
        new[]
        {
            Name, Email, Note, PushName, DataListName,
            "cf1", "cf2", "cf3", "cf4", "cf5",
            "cf6", "cf7", "cf8", "cf9", "cf10"
        },
        StringComparer.OrdinalIgnoreCase);
}
