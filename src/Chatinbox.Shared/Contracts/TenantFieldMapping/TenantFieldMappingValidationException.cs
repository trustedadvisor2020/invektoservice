namespace Chatinbox.Shared.Contracts.TenantFieldMapping;

/// <summary>
/// FEAT-TFM MVP: thrown by <see cref="Services.TenantFieldMappingValidator"/> when a tenant
/// field-mapping payload fails validation. <see cref="ErrorCode"/> is one of INV-BE-096..099.
/// Endpoint handler maps to 400 with the error code surfaced to the client.
/// </summary>
public sealed class TenantFieldMappingValidationException : Exception
{
    public string ErrorCode { get; }
    public string SemanticName { get; }

    public TenantFieldMappingValidationException(string errorCode, string semanticName, string message)
        : base(message)
    {
        ErrorCode = errorCode;
        SemanticName = semanticName;
    }
}
