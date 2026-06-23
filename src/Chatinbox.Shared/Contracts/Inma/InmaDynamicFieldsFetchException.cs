namespace Chatinbox.Shared.Contracts.Inma;

/// <summary>
/// FEAT-DMP: thrown by <see cref="HttpInmaDynamicFieldsClient"/> when the INMA
/// <c>/api/dynamicfields</c> round-trip cannot return an authoritative list
/// (timeout / network / malformed envelope / non-success status). Callers distinguish
/// this from an empty-but-successful response so the UI can render "upstream unreachable"
/// separately from "tenant has no active placeholders".
/// </summary>
public sealed class InmaDynamicFieldsFetchException : Exception
{
    public int TenantId { get; }
    public string? InmaStatusCode { get; }
    public int? HttpStatusCode { get; }

    public InmaDynamicFieldsFetchException(
        int tenantId,
        string message,
        Exception? innerException = null,
        string? inmaStatusCode = null,
        int? httpStatusCode = null)
        : base(message, innerException)
    {
        TenantId = tenantId;
        InmaStatusCode = inmaStatusCode;
        HttpStatusCode = httpStatusCode;
    }
}
