using System.Text.Json;
using Invekto.Outbound.Data;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Outbound;
using Invekto.Shared.Logging;
using Invekto.Shared.Services;

namespace Invekto.Outbound.Services;

/// <summary>
/// Orchestrates broadcast creation: validates, checks opt-outs,
/// applies template, inserts messages as 'queued'.
/// Thread-safe, register as singleton.
/// </summary>
public sealed class BroadcastOrchestrator
{
    private readonly OutboundRepository _repository;
    private readonly TemplateEngine _templateEngine;
    private readonly OptOutManager _optOutManager;
    private readonly ConsentManager _consentManager;
    private readonly DynamicMessageValidator _dynamicValidator;
    private readonly CxapiSendOptions _cxapiOptions;
    private readonly JsonLinesLogger _logger;

    public BroadcastOrchestrator(
        OutboundRepository repository,
        TemplateEngine templateEngine,
        OptOutManager optOutManager,
        ConsentManager consentManager,
        DynamicMessageValidator dynamicValidator,
        CxapiSendOptions cxapiOptions,
        JsonLinesLogger logger)
    {
        _repository = repository;
        _templateEngine = templateEngine;
        _optOutManager = optOutManager;
        _consentManager = consentManager;
        _dynamicValidator = dynamicValidator;
        _cxapiOptions = cxapiOptions;
        _logger = logger;
    }

    /// <summary>
    /// Create a broadcast: validate template, filter opt-outs, insert messages.
    /// Returns the broadcast response or an error tuple.
    /// <para><paramref name="overrideInstanceId"/> (FEAT-PROJELER SS-C): a project run's own Cloud API
    /// instance. When supplied (and the cxapi route is active) the broadcast sends from THAT instance
    /// instead of the tenant default; CSV/list/single broadcasts pass null and keep the tenant default
    /// (byte-identical). Consumed ONLY on the cxapi route — the route gate is unchanged.</para>
    /// </summary>
    public async Task<(BroadcastSendResponse? response, string? errorCode, string? errorMessage)>
        CreateBroadcastAsync(
            int tenantId, BroadcastSendRequest request, CancellationToken ct = default, int? overrideInstanceId = null)
    {
        // Validate recipients count
        if (request.Recipients == null || request.Recipients.Count == 0)
            return (null, ErrorCodes.OutboundInvalidBroadcastPayload, "recipients is required and cannot be empty");

        if (request.Recipients.Count > 1000)
            return (null, ErrorCodes.OutboundTooManyRecipients, $"Max 1000 recipients per broadcast, got {request.Recipients.Count}");

        // Content source — EXACTLY ONE of three (reject any mix so the caller's intent is never
        // silently coerced):
        //   template_id   -> INSE template (legacy broadcast/bulk + a project's gallery_template)
        //   message_text  -> inline free text (SS-A; literal body, no substitution)
        //   wa_template_id (PR-4) -> approved-template (HSM) over cxapi; per-recipient PARAM VALUES
        //                            arrive RESOLVED in each recipient's Variables (preview snapshot).
        // inlineText/hsmTemplateId are non-null locals so no render path needs a null-forgiving (!).
        var inlineText = request.MessageText?.Trim() ?? string.Empty;
        var useInline = inlineText.Length > 0;
        var hsmTemplateId = request.WaTemplateId?.Trim() ?? string.Empty;
        var useHsm = hsmTemplateId.Length > 0;
        var sourceCount = (useInline ? 1 : 0) + (request.TemplateId > 0 ? 1 : 0) + (useHsm ? 1 : 0);
        if (sourceCount > 1)
            return (null, ErrorCodes.OutboundInvalidBroadcastPayload, "Provide exactly one of template_id / message_text / wa_template_id");
        if (sourceCount == 0)
            return (null, ErrorCodes.OutboundInvalidBroadcastPayload, "template_id, message_text or wa_template_id is required");

        // Template mode loads + validates the INSE template and runs the DMP gate; inline mode skips ALL of
        // that (literal text, no placeholders). lang: request override > template lang (template mode) / the
        // request value only (inline mode).
        TemplateDto? template = null;
        string? lang = request.Lang;
        var useDynamic = false;
        string[]? broadcastDynamicFields = null;

        if (!useInline && !useHsm)
        {
            // Validate template exists
            template = await _repository.GetTemplateByIdAsync(tenantId, request.TemplateId, ct);
            if (template == null)
                return (null, ErrorCodes.OutboundTemplateNotFound, $"Template {request.TemplateId} not found or inactive");

            // GR-2.3: Resolve broadcast language (request override > template lang)
            lang = request.Lang ?? template.Lang;

            // FEAT-DMP: per-broadcast DynamicMessage activation gate (interview Q5).
            // enable_dynamic_message=FALSE forces legacy INSE substitution regardless of placeholder shape.
            // Placeholder scan runs once per broadcast (template is shared across recipients).
            var dynamicEnabled = await _repository.GetEnableDynamicMessageAsync(tenantId, ct);
            var validation = await _dynamicValidator.ValidateAsync(tenantId, template.MessageTemplate, ct);
            useDynamic = dynamicEnabled && validation.HasPlaceholders && validation.IsValid;
            broadcastDynamicFields = useDynamic ? validation.InmaFieldKeys.ToArray() : null;

            if (dynamicEnabled && validation.HasPlaceholders && !validation.IsValid)
            {
                // Unknown placeholders + flag TRUE = template references a key outside the INMA
                // allowlist AND TFM doesn't map it. Fall through to legacy TemplateEngine.Substitute
                // which will consume recipient.Variables — if the token is a user-variable the
                // broadcast succeeds; if not, recipient is skipped per its own missingVars path.
                // Logged once per broadcast so the tenant can spot stale placeholders in their template.
                _logger.SystemWarn(
                    $"[{ErrorCodes.DynamicFieldValidationFailed}] Broadcast template has non-INMA placeholders: " +
                    $"tenant={tenantId}, template={request.TemplateId}, unknown=[{string.Join(",", validation.UnknownPlaceholders)}]");
            }
        }

        // FEAT-PROJELER / cxapi (PR-3a): immutable route decision at create. A cxapi-allowlisted tenant
        // routes this PLAIN-TEXT broadcast to WapCRM cxapi instead of the Main App bridge. The instance is
        // the tenant default (settings_json->'wapcrm'.instance_id); secret/userId are loaded per-batch at
        // send time (never persisted). Decided once per broadcast (homogeneous route, Codex P0 #5).
        var cxapiRoute = _cxapiOptions.IsTenantAllowed(tenantId);

        // PR-4: an HSM broadcast can ONLY ride cxapi — approved templates cannot go through the
        // INMA Main App bridge. NO fallback (roadmap "sessiz fallback yok"): a non-allowlisted
        // tenant is rejected with the same typed code the upstream gates use. With the allowlist
        // empty (P0-3) this keeps the whole HSM path inert at every layer.
        if (useHsm && !cxapiRoute)
            return (null, ErrorCodes.HsmSendNotAllowlisted, "Onaylı şablon gönderimi bu hesapta henüz açık değil.");

        var sendRoute = "mainapp_bridge";
        int? instanceId = null;
        if (cxapiRoute)
        {
            // DMP HARD FAIL (Codex P0 #3): cxapi plain-text has no DynamicMessage mechanism — reject,
            // never silently fall back to the bridge.
            if (useDynamic)
                return (null, ErrorCodes.CxapiDynamicNotSupported,
                    "Dynamic (DMP) content is not supported on the WapCRM cxapi route; use a static template or disable dynamic message for this tenant");

            var wap = await _repository.GetWapCrmSettingsAsync(tenantId, ct);
            // FEAT-PROJELER SS-C: a project run carries its OWN Cloud API instance (overrideInstanceId,
            // validated owned + Cloud API at project save in SS-B). Use it when supplied; otherwise the
            // tenant-default instance (settings_json->'wapcrm'.instance_id). The tenant secret/userId are
            // always required. csv/list/single broadcasts pass null -> tenant-default (byte-identical).
            var resolvedInstance = (overrideInstanceId is int oi && oi > 0)
                ? oi
                : (wap?.InstanceId is int di && di > 0 ? di : 0);
            if (wap == null || string.IsNullOrWhiteSpace(wap.SecretKey) || wap.UserId <= 0 || resolvedInstance <= 0)
                return (null, ErrorCodes.CxapiRouteMisconfigured,
                    "WapCRM cxapi is enabled for this tenant but instance_id/secret_key/user_id are not configured in tenant settings");

            sendRoute = "wapcrm_cxapi";
            instanceId = resolvedInstance;
        }

        // Collect valid phones for batch opt-out check
        var validRecipients = request.Recipients
            .Where(r => !string.IsNullOrWhiteSpace(r.Phone))
            .ToList();

        // Batch opt-out check (single query instead of N queries)
        var phones = validRecipients.Select(r => r.Phone).ToList();
        var optedOutPhones = await _repository.BatchCheckOptOutsAsync(tenantId, phones, ct);

        // GR-2.6: Check if health tenant (once per broadcast, not per message)
        var (healthSettingsJson, healthSector) = await _repository.GetTenantHealthInfoAsync(tenantId, ct);
        var isHealthTenant = KvkkHelper.IsHealthTenant(healthSettingsJson, healthSector);

        // GR-3.26: Batch marketing consent check (broadcasts are marketing)
        var nonOptoutPhones = validRecipients
            .Where(r => !optedOutPhones.Contains(r.Phone))
            .Select(r => r.Phone).ToList();
        var noConsentPhones = await _consentManager.GetPhonesWithoutMarketingConsentAsync(
            tenantId, nonOptoutPhones, ct);

        // Filter and prepare messages
        var skippedOptout = 0;
        var skippedConsent = 0;
        var messagesToInsert = new List<(string phone, string text, string[]? dynamicFields)>();
        // PR-4 (HSM): index-aligned with messagesToInsert (single Add point per recipient below).
        // Each entry is the recipient's RESOLVED param dict serialized verbatim from Variables —
        // the preview snapshot already resolved columns -> values; this loop copies, never re-derives.
        var hsmParamsJson = new List<string>();

        foreach (var recipient in validRecipients)
        {
            if (optedOutPhones.Contains(recipient.Phone))
            {
                skippedOptout++;
                continue;
            }

            // GR-3.26: Skip if no marketing consent
            if (noConsentPhones.Contains(recipient.Phone))
            {
                skippedConsent++;
                continue;
            }

            string messageText;
            string[]? recipientDynamicFields = null;
            if (useHsm)
            {
                // PR-4 approved-template: WhatsApp renders the REAL text provider-side from the
                // pre-approved template + per-recipient params. message_text stores a machine-
                // distinguishable DISPLAY placeholder only — INVARIANT: for message_kind=
                // 'wapcrm_template' it is NEVER a send-payload source (UI shows template_ref +
                // template_params instead). KVKK disclaimer is intentionally NOT appended: the
                // wire carries no free text to append to — a health tenant's disclaimer must be
                // part of the Meta-approved template content itself.
                messageText = $"[WAPCRM_TEMPLATE] {hsmTemplateId}";
                hsmParamsJson.Add(recipient.Variables is { Count: > 0 }
                    ? JsonSerializer.Serialize(recipient.Variables)
                    : "{}");
                messagesToInsert.Add((recipient.Phone, messageText, null));
                continue;
            }
            if (useInline)
            {
                // SS-A inline free text: the SAME literal body for every recipient. No INSE substitution
                // (recipient.Variables is ignored) and no DMP — the operator typed a finished message.
                messageText = inlineText;
            }
            // Pattern-bind the (guaranteed non-null when !useInline) template to a non-nullable local so the
            // render branches need no null-forgiving operator. The final else is unreachable (a non-inline
            // broadcast always validated a template above) — kept as a defensive skip, never an NRE.
            else if (template is { } tmpl)
            {
                if (useDynamic)
                {
                    // FEAT-DMP: raw template text ships to INMA which resolves placeholders
                    // from Customer DB. TemplateEngine.Substitute is bypassed entirely —
                    // recipient.Variables is ignored in dynamic mode (INMA doesn't use it).
                    messageText = tmpl.MessageTemplate;
                    recipientDynamicFields = broadcastDynamicFields;
                }
                else
                {
                    var (substituted, missingVars) = _templateEngine.Substitute(
                        tmpl.MessageTemplate, recipient.Variables);

                    if (missingVars.Count > 0)
                    {
                        _logger.SystemWarn(
                            $"Broadcast skipping {recipient.Phone}: missing variables [{string.Join(", ", missingVars)}]");
                        continue;
                    }
                    messageText = substituted;
                }
            }
            else
            {
                continue; // defensive: no content source resolved (unreachable after the exactly-one guard)
            }

            // GR-2.6.1: Append KVKK health disclaimer if applicable
            var finalText = KvkkHelper.AppendDisclaimerIfHealth(messageText, isHealthTenant);

            // cxapi defensive guard (Codex P0 #3): a plain-text cxapi message must not carry unresolved
            // placeholders onto the wire. INSE substitution already resolved {{vars}}; a leftover '{{'
            // means a DMP-style token slipped through — skip this recipient rather than send a broken message.
            if (cxapiRoute && finalText.Contains("{{"))
            {
                _logger.SystemWarn(
                    $"[{ErrorCodes.CxapiDynamicNotSupported}] cxapi skip {recipient.Phone}: unresolved placeholder in rendered text");
                continue;
            }

            messagesToInsert.Add((recipient.Phone, finalText, recipientDynamicFields));
        }

        if (messagesToInsert.Count == 0)
        {
            return (null, ErrorCodes.OutboundInvalidBroadcastPayload,
                "No valid recipients after opt-out filtering and variable validation");
        }

        // SS-A/PR-4: inline + HSM broadcasts carry NO INSE template_id (nullable columns); template
        // broadcasts keep their id. Used for the broadcast, messages AND audit rows.
        int? broadcastTemplateId = (useInline || useHsm) ? null : request.TemplateId;

        // PR-4 (HSM): the single dynamic HEADER media for the whole broadcast, persisted per message
        // as template_header_media JSONB ({"url": ...}); fileName derivation happens in the send client.
        var hsmHeaderMediaJson = useHsm && !string.IsNullOrWhiteSpace(request.TemplateHeaderMediaUrl)
            ? JsonSerializer.Serialize(new { url = request.TemplateHeaderMediaUrl.Trim() })
            : null;

        // Create broadcast record (GR-2.3: with language; PR-3a: immutable route + instance;
        // PR-4: HSM broadcasts also stamp template_kind + wa_template_id for run reporting)
        var broadcastId = await _repository.CreateBroadcastAsync(
            tenantId, broadcastTemplateId, request.Recipients.Count,
            messagesToInsert.Count, request.ScheduledAt, lang, ct, sendRoute, instanceId,
            templateKind: useHsm ? "wapcrm_template" : null,
            waTemplateId: useHsm ? hsmTemplateId : null);

        // Batch insert all messages (single multi-row INSERT). HSM rows additionally carry
        // message_kind='wapcrm_template' + template_ref + per-recipient template_params +
        // template_header_media (PR-1 reserved columns, written for the first time here).
        if (useHsm)
        {
            await _repository.BatchInsertHsmMessagesAsync(
                tenantId, broadcastId, hsmTemplateId,
                messagesToInsert.Select((m, i) => (m.phone, m.text, hsmParamsJson[i])).ToList(),
                request.TemplateLanguage, hsmHeaderMediaJson, lang, ct, sendRoute, instanceId);
        }
        else
        {
            await _repository.BatchInsertMessagesAsync(
                tenantId, broadcastId, broadcastTemplateId, messagesToInsert, lang, ct, sendRoute, instanceId);
        }
        var queuedCount = messagesToInsert.Count;

        // GR-3.29: Audit trail - batch insert for compliance (single multi-row INSERT).
        // AuditTrail records the rendered MessageText only (DynamicFields is per-message
        // metadata, not customer-visible content) — project to the legacy (phone, content) shape.
        // PR-4 (HSM): the real text renders provider-side, so the compliance row records the
        // template slug + the exact per-recipient param values that were filled.
        var auditRecords = useHsm
            ? messagesToInsert
                .Select((m, i) => (phone: m.phone, content: $"{m.text} params={hsmParamsJson[i]}"))
                .ToList()
            : messagesToInsert
                .Select(m => (phone: m.phone, content: m.text))
                .ToList();
        await _repository.BatchInsertAuditTrailAsync(
            tenantId, broadcastTemplateId, null, auditRecords, ct);

        _logger.SystemInfo(
            $"Broadcast created: id={broadcastId}, tenant={tenantId}, " +
            $"total={request.Recipients.Count}, queued={queuedCount}, " +
            $"skipped_optout={skippedOptout}, skipped_consent={skippedConsent}");

        return (new BroadcastSendResponse
        {
            BroadcastId = broadcastId,
            TotalRecipients = request.Recipients.Count,
            Queued = queuedCount,
            SkippedOptout = skippedOptout,
            SkippedConsent = skippedConsent
        }, null, null);
    }
}
