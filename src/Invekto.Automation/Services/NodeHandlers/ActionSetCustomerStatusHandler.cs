using Invekto.Shared.Constants;
using Invekto.Shared.Contracts.Inma.Dtos;
using Invekto.Shared.Contracts.Inma.Webhooks;

namespace Invekto.Automation.Services.NodeHandlers;

/// <summary>
/// FEAT-INMA-PIPELINE-V2 C4 — 'Set Customer Status' flow ACTION node. Writes a lead's INMA feature-group
/// selection back via the Backend proxy (-> cxapi <c>customer-feature-groups/update</c>). FULL-LIST
/// semantics: <c>feature_ids</c> is the COMPLETE new selection for the group (empty = clear it). Single +
/// Multi only (text-mode groups are disabled in the picker — the vendor /update has no text-write payload).
///
/// Mirrors <see cref="ApiCallHandler"/> / <see cref="EcommerceHandler"/>: Action=Continue with two output
/// handles (<c>success</c> / <c>error</c>); <c>ctx.IsSimulation</c> short-circuits to a mock success BEFORE
/// any identity resolution or real call. Identity: <see cref="ExecutionContext.CustomerId"/> (preferred) then
/// <see cref="ExecutionContext.Phone"/>; neither present -> INV-AT-090, error handle, no call. ClientRequestID
/// is STABLE (<c>invekto-{tenant}-{fg}-{node}-{identity}</c>, reusing
/// <see cref="CustomerStatusFlowSuppression.OriginRequestIdPrefix"/>) so a deliberate retry of the same write
/// reuses one id and the C3a fail-closed loop guard (actor=='api' -> suppress) keeps the C2->C3->C4->C2 cycle
/// dead. The action NEVER faults the flow run: any failure routes to the <c>error</c> handle + INV-AT-089.
/// </summary>
public sealed class ActionSetCustomerStatusHandler : INodeHandler
{
    public string NodeType => "action_set_customer_status";

    private const int ClientRequestIdMaxLength = 128;
    private const string SuccessHandle = "success";
    private const string ErrorHandle = "error";

    private readonly BackendCustomerStatusClient _client;

    public ActionSetCustomerStatusHandler(BackendCustomerStatusClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var label = node.GetData("label", node.Id);

        // feature_group_id (required, numeric). Empty / non-numeric = misconfiguration -> error handle.
        var fgRaw = node.GetData("feature_group_id", "").Trim();
        if (!int.TryParse(fgRaw, out var featureGroupId) || featureGroupId <= 0)
        {
            ctx.Logger.SystemWarn(
                $"[{ErrorCodes.SetCustomerStatusActionFailed}] ActionSetCustomerStatus '{label}': invalid feature_group_id='{fgRaw}'");
            return Error("Geçersiz durum grubu (feature_group_id).", ErrorCodes.SetCustomerStatusActionFailed);
        }

        // FULL new selection for the group. Empty list = clear the group's selection (intentional).
        var featureIds = ParseFeatureIds(node.GetData("feature_ids", ""));

        // Simulation: never make a real CRM write.
        if (ctx.IsSimulation)
        {
            ctx.Logger.StepInfo(
                $"ActionSetCustomerStatus '{label}': simulation (no real write). fg={featureGroupId} ids=[{string.Join(",", featureIds)}]",
                ctx.RequestId);
            return Success("200");
        }

        // Identity: customerId preferred, phone fallback. Pattern-bind to avoid the null-forgiving operator.
        var customerId = ctx.CustomerId is > 0 ? ctx.CustomerId : null;
        var phone = ctx.Phone is { } rawPhone && !string.IsNullOrWhiteSpace(rawPhone) ? rawPhone.Trim() : null;
        if (customerId is null && phone is null)
        {
            ctx.Logger.SystemWarn(
                $"[{ErrorCodes.SetCustomerStatusNoIdentity}] ActionSetCustomerStatus '{label}': no customerId/phone in flow context (fg={featureGroupId})");
            return Error("Müşteri kimliği yok (bu akış bağlamı telefon/müşteri taşımıyor).", ErrorCodes.SetCustomerStatusNoIdentity);
        }

        var clientRequestId = BuildClientRequestId(ctx.TenantId, featureGroupId, node.Id, customerId, phone);

        var request = new SetCustomerStatusProxyRequest
        {
            TenantId = ctx.TenantId,
            CustomerId = customerId,
            Phone = phone,
            FeatureGroupId = featureGroupId,
            FeatureIds = featureIds,
            ClientRequestId = clientRequestId
        };

        var outcome = await _client.UpdateAsync(request, ctx.RequestId, ct);
        if (outcome.Success)
        {
            ctx.Logger.StepInfo(
                $"ActionSetCustomerStatus '{label}': applied fg={featureGroupId} ids=[{string.Join(",", featureIds)}] " +
                $"target={(customerId.HasValue ? "customerId" : "phone")} crid={clientRequestId}",
                ctx.RequestId);
            return Success("200");
        }

        ctx.Logger.SystemWarn(
            $"[{ErrorCodes.SetCustomerStatusActionFailed}] ActionSetCustomerStatus '{label}': write failed " +
            $"fg={featureGroupId} code={outcome.Code ?? "-"}");
        return Error(outcome.Message ?? "Durum güncellenemedi.", outcome.Code ?? ErrorCodes.SetCustomerStatusActionFailed);
    }

    /// <summary>
    /// Parse the node's <c>feature_ids</c> CSV into the COMPLETE new selection (FULL-LIST semantics):
    /// empty/blank => empty list = CLEAR the group; duplicates and non-positive/non-numeric ids dropped.
    /// PUBLIC static so the pure parsing semantic is unit-pinned without a FlowEngine/HTTP host (mirrors
    /// <see cref="Invekto.Automation.Services.Jobs.TriggerWelcomeFlowJob.ShouldDispatchWelcomeSent"/>).
    /// </summary>
    public static List<int> ParseFeatureIds(string raw)
    {
        var list = new List<int>();
        if (string.IsNullOrWhiteSpace(raw)) return list; // empty = clear the group's selection
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(part, out var id) && id > 0 && !list.Contains(id))
                list.Add(id);
        }
        return list;
    }

    /// <summary>
    /// Stable, GUID-free correlation id: deterministic for the same (tenant, group, node, target) so a
    /// deliberate retry reuses one id. The 'invekto-' prefix (shared with C3a) is the redundant loop kill;
    /// the real loop kill is C3a's fail-closed actor guard. PUBLIC static for unit pinning.
    /// </summary>
    public static string BuildClientRequestId(int tenantId, int featureGroupId, string nodeId, int? customerId, string? phone)
    {
        var identity = customerId.HasValue ? $"c{customerId.Value}" : $"p{phone}";
        var id = $"{CustomerStatusFlowSuppression.OriginRequestIdPrefix}{tenantId}-{featureGroupId}-{nodeId}-{identity}";
        return id.Length <= ClientRequestIdMaxLength ? id : id[..ClientRequestIdMaxLength];
    }

    private static NodeResult Success(string statusCode) => new()
    {
        MessageText = null,
        Action = NodeAction.Continue,
        OutputHandle = SuccessHandle,
        VariableUpdates = new Dictionary<string, string>
        {
            ["set_status_code"] = statusCode,
            ["set_status_error"] = ""
        }
    };

    private static NodeResult Error(string message, string code) => new()
    {
        MessageText = null,
        Action = NodeAction.Continue,
        OutputHandle = ErrorHandle,
        VariableUpdates = new Dictionary<string, string>
        {
            ["set_status_code"] = "0",
            ["set_status_error"] = $"[{code}] {message}"
        }
    };
}
