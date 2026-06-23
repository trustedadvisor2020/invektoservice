using System.Text;
using System.Text.Json;
using System.Web;
using Chatinbox.Shared.Auth;
using Chatinbox.Shared.Constants;

namespace Chatinbox.Automation.Services.NodeHandlers;

/// <summary>
/// E-commerce action node. Calls Integrations service for order/product/customer operations.
/// NodeType: "action_ecommerce". Auto-chain (2 output handles: success, error).
///
/// Node data (flow_config JSON):
///   provider         - e-commerce provider name (e.g. "ikas")
///   operation        - one of: list_orders, get_order, list_products, get_product,
///                      list_customers, fulfill_order, update_order_status, refund_order_line
///   filter_phone     - phone filter (for orders/customers)
///   filter_email     - email filter (for customers)
///   filter_search    - search term
///   filter_status    - status filter
///   order_id         - order ID (for get_order, fulfill, status, refund)
///   product_id       - product ID (for get_product)
///   tracking_code    - tracking code (for fulfill_order)
///   cargo_provider   - cargo company (for fulfill_order)
///   new_status       - new status value (for update_order_status)
///   line_item_id     - line item ID (for refund)
///   refund_quantity   - refund quantity (for refund)
///   refund_reason    - refund reason (for refund)
///   response_variable - variable name to store response JSON (default: ecom_result)
///
/// All string values support {{variable}} substitution via ExpressionEvaluator.
/// Simulation: returns mock JSON without real HTTP call.
/// Production: calls Integrations service via IntegrationsClient with service JWT.
/// </summary>
public sealed class EcommerceHandler : INodeHandler
{
    private readonly IntegrationsClient _integrationsClient;
    private readonly JwtGenerator _jwtGenerator;

    public string NodeType => "action_ecommerce";

    public EcommerceHandler(IntegrationsClient integrationsClient, JwtGenerator jwtGenerator)
    {
        _integrationsClient = integrationsClient;
        _jwtGenerator = jwtGenerator;
    }

    public async Task<NodeResult> ExecuteAsync(FlowNodeV2 node, ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var provider = Sub(node, "provider", "ikas", ctx);
        var operation = node.GetData("operation", "list_orders");
        var responseVariable = node.GetData("response_variable", "ecom_result");

        // Simulation mode: return mock data
        if (ctx.IsSimulation)
        {
            ctx.Logger.StepInfo(
                $"Ecommerce '{operation}': simulation mock (no real call). provider={provider}",
                ctx.RequestId);

            var mockJson = BuildSimulationResponse(operation);
            return new NodeResult
            {
                MessageText = null,
                Action = NodeAction.Continue,
                OutputHandle = "success",
                VariableUpdates = new Dictionary<string, string>
                {
                    [responseVariable] = mockJson,
                    ["ecom_status_code"] = "200",
                    ["ecom_error_message"] = ""
                }
            };
        }

        // Build HTTP request from node config
        var (method, path) = BuildRequest(node, provider, operation, ctx);
        var bodyJson = BuildRequestBody(node, operation, ctx);

        // Generate service JWT for Integrations service
        var jwt = _jwtGenerator.GenerateServiceToken(ctx.TenantId);

        var result = await _integrationsClient.ExecuteAsync(method, path, bodyJson, jwt, ct);

        if (result.Success)
        {
            ctx.Logger.StepInfo(
                $"Ecommerce '{operation}': success. provider={provider}, status={result.StatusCode}",
                ctx.RequestId);

            return new NodeResult
            {
                MessageText = null,
                Action = NodeAction.Continue,
                OutputHandle = "success",
                VariableUpdates = new Dictionary<string, string>
                {
                    [responseVariable] = result.ResponseJson,
                    ["ecom_status_code"] = result.StatusCode.ToString(),
                    ["ecom_error_message"] = ""
                }
            };
        }

        // Error path
        ctx.Logger.SystemWarn(
            $"[{ErrorCodes.AutomationEcommerceCallFailed}] Ecommerce {operation} failed: " +
            $"provider={provider}, status={result.StatusCode}, reason={result.ErrorReason}");

        return new NodeResult
        {
            MessageText = null,
            Action = NodeAction.Continue,
            OutputHandle = "error",
            VariableUpdates = new Dictionary<string, string>
            {
                [responseVariable] = result.ResponseJson,
                ["ecom_status_code"] = result.StatusCode.ToString(),
                ["ecom_error_message"] = $"[{ErrorCodes.AutomationEcommerceCallFailed}] {result.ErrorReason ?? $"HTTP {result.StatusCode}"}"
            }
        };
    }

    private (HttpMethod Method, string Path) BuildRequest(
        FlowNodeV2 node, string provider, string operation, ExecutionContext ctx)
    {
        return operation switch
        {
            "list_orders" => (HttpMethod.Get,
                $"/api/v1/orders?provider={Enc(provider)}{BuildOrderQueryString(node, ctx)}"),

            "get_order" => (HttpMethod.Get,
                $"/api/v1/orders/{Enc(provider)}/{Enc(Sub(node, "order_id", "", ctx))}"),

            "list_products" => (HttpMethod.Get,
                $"/api/v1/ecommerce/{Enc(provider)}/products{BuildProductQueryString(node, ctx)}"),

            "get_product" => (HttpMethod.Get,
                $"/api/v1/ecommerce/{Enc(provider)}/products/{Enc(Sub(node, "product_id", "", ctx))}"),

            "list_customers" => (HttpMethod.Get,
                $"/api/v1/ecommerce/{Enc(provider)}/customers{BuildCustomerQueryString(node, ctx)}"),

            "fulfill_order" => (HttpMethod.Post,
                $"/api/v1/ecommerce/{Enc(provider)}/orders/{Enc(Sub(node, "order_id", "", ctx))}/fulfill"),

            "update_order_status" => (HttpMethod.Post,
                $"/api/v1/ecommerce/{Enc(provider)}/orders/{Enc(Sub(node, "order_id", "", ctx))}/status"),

            "refund_order_line" => (HttpMethod.Post,
                $"/api/v1/ecommerce/{Enc(provider)}/orders/{Enc(Sub(node, "order_id", "", ctx))}/refund-line"),

            _ => (HttpMethod.Get, $"/api/v1/orders?provider={Enc(provider)}")
        };
    }

    private string? BuildRequestBody(FlowNodeV2 node, string operation, ExecutionContext ctx)
    {
        return operation switch
        {
            "fulfill_order" => JsonSerializer.Serialize(new
            {
                tracking_code = Sub(node, "tracking_code", "", ctx),
                cargo_provider = Sub(node, "cargo_provider", "", ctx)
            }),

            "update_order_status" => JsonSerializer.Serialize(new
            {
                status = Sub(node, "new_status", "", ctx)
            }),

            "refund_order_line" => JsonSerializer.Serialize(new
            {
                line_item_id = Sub(node, "line_item_id", "", ctx),
                quantity = ParseInt(Sub(node, "refund_quantity", "1", ctx), 1),
                reason = Sub(node, "refund_reason", "", ctx)
            }),

            _ => null // GET requests have no body
        };
    }

    private string BuildOrderQueryString(FlowNodeV2 node, ExecutionContext ctx)
    {
        var sb = new StringBuilder();
        AppendQueryParam(sb, "phone", Sub(node, "filter_phone", "", ctx));
        AppendQueryParam(sb, "status", Sub(node, "filter_status", "", ctx));
        return sb.ToString();
    }

    private string BuildProductQueryString(FlowNodeV2 node, ExecutionContext ctx)
    {
        var sb = new StringBuilder();
        var hasParams = false;
        hasParams = AppendQueryParam(sb, "search", Sub(node, "filter_search", "", ctx), hasParams ? '&' : '?');
        hasParams = AppendQueryParam(sb, "status", Sub(node, "filter_status", "", ctx), hasParams ? '&' : '?');
        return sb.ToString();
    }

    private string BuildCustomerQueryString(FlowNodeV2 node, ExecutionContext ctx)
    {
        var sb = new StringBuilder();
        var hasParams = false;
        hasParams = AppendQueryParam(sb, "phone", Sub(node, "filter_phone", "", ctx), hasParams ? '&' : '?');
        hasParams = AppendQueryParam(sb, "email", Sub(node, "filter_email", "", ctx), hasParams ? '&' : '?');
        hasParams = AppendQueryParam(sb, "search", Sub(node, "filter_search", "", ctx), hasParams ? '&' : '?');
        return sb.ToString();
    }

    private static bool AppendQueryParam(StringBuilder sb, string key, string value, char prefix = '&')
    {
        if (string.IsNullOrEmpty(value)) return sb.Length > 0;
        sb.Append(sb.Length == 0 && prefix == '&' ? '&' : prefix);
        sb.Append(key).Append('=').Append(HttpUtility.UrlEncode(value));
        return true;
    }

    private static string BuildSimulationResponse(string operation)
    {
        return operation switch
        {
            "list_orders" or "get_order" => JsonSerializer.Serialize(new[]
            {
                new
                {
                    id = 1, provider = "ikas", external_order_id = "SIM-001",
                    customer_phone = "+905001234567", customer_name = "Simülasyon Müşteri",
                    order_status = "shipped", total_amount = 299.90m, currency = "TRY"
                }
            }),

            "list_products" or "get_product" => JsonSerializer.Serialize(new
            {
                products = new[]
                {
                    new
                    {
                        id = "sim-prod-1", name = "Simülasyon Ürün",
                        price = 149.90m, currency = "TRY",
                        stock_count = 25, status = "active"
                    }
                },
                total_count = 1
            }),

            "list_customers" => JsonSerializer.Serialize(new
            {
                customers = new[]
                {
                    new
                    {
                        id = "sim-cust-1", full_name = "Simülasyon Müşteri",
                        email = "sim@test.com", phone = "+905001234567",
                        order_count = 3, total_spent = 899.70m
                    }
                },
                total_count = 1
            }),

            "fulfill_order" or "update_order_status" or "refund_order_line" =>
                JsonSerializer.Serialize(new { success = true, result = "Simülasyon: İşlem başarılı" }),

            _ => "{\"mock\":true}"
        };
    }

    /// <summary>Substitute variables in node data value.</summary>
    private static string Sub(FlowNodeV2 node, string key, string fallback, ExecutionContext ctx)
    {
        var raw = node.GetData(key, fallback);
        return ctx.Evaluator.Substitute(raw, ctx.State.Variables);
    }

    private static string Enc(string value) => HttpUtility.UrlEncode(value);

    private static int ParseInt(string? raw, int fallback)
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        return int.TryParse(raw, out var v) ? v : fallback;
    }
}
