using System.Text.RegularExpressions;
using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.TenantFieldMapping;
using Invekto.Shared.Contracts.TenantFieldMapping.Dtos;

namespace Invekto.Shared.Services;

/// <summary>
/// FEAT-TFM MVP: validates tenant field-mapping payload before UPSERT.
/// Throws <see cref="TenantFieldMappingValidationException"/> with INV-BE-096..099
/// on the first failing entry (fail-fast; endpoint surfaces error code to client).
///
/// RESERVED names = <see cref="InmaDynamicFieldKeys.Allowlist"/> (name/email/note/pushname/
/// datalistname + cf1..cf10) ∪ leads core columns (id, tenant_id, full_name, phone,
/// created_at, updated_at, pipeline_status, preferred_locale). Rationale: INMA Allowlist
/// keys are already valid raw placeholders — if a tenant maps semantic 'name' → cf1, the
/// DMP validator sees '{{name}}' both as raw Allowlist match AND resolved mapping match,
/// producing ambiguous substitution. Reserved guard eliminates the contract collision.
/// </summary>
public static class TenantFieldMappingValidator
{
    // Semantic name: start with lowercase letter, then lowercase alphanum + underscore, 2-64 chars.
    private static readonly Regex SemanticNamePattern = new(
        @"^[a-z][a-z0-9_]{1,63}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // INMA source keys: cf1..cf10 only (INMA's custom field range; 11+ out of scope).
    private static readonly HashSet<string> AllowedSources = new(
        new[] { "cf1", "cf2", "cf3", "cf4", "cf5", "cf6", "cf7", "cf8", "cf9", "cf10" },
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AllowedTypes = new(
        new[] { "enum", "string", "date", "bool", "int" },
        StringComparer.OrdinalIgnoreCase);

    // Leads core columns that must not be shadowed by semantic names. Combined at runtime
    // with InmaDynamicFieldKeys.Allowlist to form the full reserved set.
    private static readonly string[] LeadsCoreColumns =
    {
        "id", "tenant_id", "full_name", "phone", "created_at", "updated_at",
        "pipeline_status", "preferred_locale"
    };

    /// <summary>
    /// Validate the full mapping dictionary. Empty map is valid (= no mapping).
    /// Throws on first failure. Caller typically wraps the call to map exceptions to 400.
    /// </summary>
    public static void Validate(IReadOnlyDictionary<string, TenantFieldMappingEntry> mapping)
    {
        if (mapping == null || mapping.Count == 0) return;

        var reserved = BuildReservedSet();
        var sourcesSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (semanticName, entry) in mapping)
        {
            ValidateSemanticName(semanticName, reserved);
            ValidateEntry(semanticName, entry);

            // Same cf slot used twice = ambiguous. Reject INV-BE-096 (structural).
            if (!sourcesSeen.Add(entry.Source))
            {
                throw new TenantFieldMappingValidationException(
                    ErrorCodes.FieldMappingInvalid,
                    semanticName,
                    $"INMA kaynak '{entry.Source}' birden fazla semantic isme atanmış — her cf1..cf10 slotu tek semantic isim taşıyabilir.");
            }
        }
    }

    private static HashSet<string> BuildReservedSet()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in InmaDynamicFieldKeys.Allowlist) set.Add(key);
        foreach (var col in LeadsCoreColumns) set.Add(col);
        return set;
    }

    private static void ValidateSemanticName(string semanticName, HashSet<string> reserved)
    {
        if (string.IsNullOrWhiteSpace(semanticName) || !SemanticNamePattern.IsMatch(semanticName))
        {
            throw new TenantFieldMappingValidationException(
                ErrorCodes.FieldMappingInvalid,
                semanticName ?? string.Empty,
                $"Semantic isim geçersiz: '{semanticName}'. Küçük harfle başlamalı, sadece [a-z0-9_] içerebilir, 2-64 karakter.");
        }

        if (reserved.Contains(semanticName))
        {
            throw new TenantFieldMappingValidationException(
                ErrorCodes.FieldMappingReservedSemanticName,
                semanticName,
                $"Bu isim sistem alanı, kullanılamaz: '{semanticName}'. Lütfen farklı bir isim seçin.");
        }
    }

    private static void ValidateEntry(string semanticName, TenantFieldMappingEntry entry)
    {
        if (entry == null)
        {
            throw new TenantFieldMappingValidationException(
                ErrorCodes.FieldMappingInvalid,
                semanticName,
                $"'{semanticName}' için alan tanımı boş.");
        }

        if (string.IsNullOrWhiteSpace(entry.Source) || !AllowedSources.Contains(entry.Source))
        {
            throw new TenantFieldMappingValidationException(
                ErrorCodes.FieldMappingSourceOutOfRange,
                semanticName,
                $"INMA kaynağı cf1..cf10 olmalı: '{entry.Source}' ('{semanticName}').");
        }

        if (string.IsNullOrWhiteSpace(entry.Type) || !AllowedTypes.Contains(entry.Type))
        {
            throw new TenantFieldMappingValidationException(
                ErrorCodes.FieldMappingInvalid,
                semanticName,
                $"Geçersiz tip: '{entry.Type}' ('{semanticName}'). İzinli: enum | string | date | bool | int.");
        }

        if (string.Equals(entry.Type, "enum", StringComparison.OrdinalIgnoreCase))
        {
            if (entry.EnumValues == null || entry.EnumValues.Count == 0)
            {
                throw new TenantFieldMappingValidationException(
                    ErrorCodes.FieldMappingEnumValueMissing,
                    semanticName,
                    $"Enum tipi için en az bir değer gerekli: '{semanticName}'.");
            }
        }
    }
}
