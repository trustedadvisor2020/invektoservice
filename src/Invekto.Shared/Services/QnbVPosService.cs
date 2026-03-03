using System.Security.Cryptography;
using System.Text;
using System.Web;
using Invekto.Shared.DTOs.Payment;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Invekto.Shared.Services;

/// <summary>
/// QNB Finansbank 3DPay sanal pos entegrasyonu.
/// Ref: arch/docs/qnb-vpos-integration.md
/// </summary>
public sealed class QnbVPosService
{
    private readonly QnbVPosSettings _settings;
    private readonly ILogger<QnbVPosService> _logger;

    public QnbVPosService(IOptions<QnbVPosSettings> settings, ILogger<QnbVPosService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// 3DPay ödeme başlatır. Banka gateway'ine auto-submit HTML form döner.
    /// Caller bu HTML'i response olarak client'a gönderir.
    /// </summary>
    public PaymentInitResult InitiatePayment(PaymentInitRequest request, string callbackUrl, string tenantId)
    {
        var orderId = GenerateOrderId(tenantId);
        var rnd = DateTime.UtcNow.ToString("yyyyMMddHHmmssffff");
        var amount = request.Amount.ToString("F2");
        var installmentCount = "0";
        var txnType = "Auth";
        var secureType = "3DPay";

        var hash = ComputeHash(
            _settings.MbrId, orderId, amount,
            callbackUrl, callbackUrl,
            txnType, installmentCount, rnd,
            _settings.MerchantPass
        );

        var pan = request.CardNumber.Replace(" ", "");
        var expiry = request.CardExpireMonth + request.CardExpireYear;

        var parameters = new Dictionary<string, string>
        {
            ["MbrId"] = _settings.MbrId,
            ["MerchantId"] = _settings.MerchantId,
            ["UserCode"] = _settings.UserCode,
            ["UserPass"] = _settings.UserPass,
            ["SecureType"] = secureType,
            ["TxnType"] = txnType,
            ["InstallmentCount"] = installmentCount,
            ["Currency"] = "949", // TRY
            ["OkUrl"] = callbackUrl,
            ["FailUrl"] = callbackUrl,
            ["OrderId"] = orderId,
            ["PurchAmount"] = amount,
            ["Lang"] = "TR",
            ["Rnd"] = rnd,
            ["Hash"] = hash,
            ["Pan"] = pan,
            ["Expiry"] = expiry,
            ["Cvv2"] = request.Cvv
        };

        var html = BuildAutoSubmitForm(_settings.GatewayUrl, parameters);

        _logger.LogInformation("Payment initiated: OrderId={OrderId}, Tenant={TenantId}, Amount={Amount}",
            orderId, tenantId, amount);

        return new PaymentInitResult
        {
            Success = true,
            RedirectHtml = html,
            OrderId = orderId
        };
    }

    /// <summary>
    /// Banka callback'inden gelen form verilerini parse eder.
    /// </summary>
    public PaymentCallbackData ParseCallback(IDictionary<string, string> formData)
    {
        var result = new PaymentCallbackData
        {
            MerchantId = GetFormValue(formData, "MerchantID"),
            AuthCode = GetFormValue(formData, "AuthCode"),
            ProcReturnCode = GetFormValue(formData, "ProcReturnCode"),
            ThreeDStatus = GetFormValue(formData, "3DStatus"),
            ResponseRnd = GetFormValue(formData, "ResponseRnd"),
            ResponseHash = GetFormValue(formData, "ResponseHash"),
            UserCode = GetFormValue(formData, "UserCode"),
            TransId = GetFormValue(formData, "TransId"),
            ErrMsg = GetFormValue(formData, "ErrMsg"),
            HostRefNum = GetFormValue(formData, "HostRefNum"),
            OrderId = GetFormValue(formData, "OrderId"),
            TxnResult = GetFormValue(formData, "TxnResult"),
            PurchAmount = GetFormValue(formData, "PurchAmount"),
            TxnType = GetFormValue(formData, "TxnType"),
            Rnd = GetFormValue(formData, "Rnd"),
            TxnStatus = GetFormValue(formData, "TxnStatus")
        };

        if (result.IsSuccessful)
        {
            _logger.LogInformation("Payment successful: OrderId={OrderId}, HostRef={HostRefNum}, AuthCode={AuthCode}",
                result.OrderId, result.HostRefNum, result.AuthCode);
        }
        else
        {
            _logger.LogWarning("Payment failed: OrderId={OrderId}, 3DStatus={ThreeDStatus}, ProcReturn={ProcReturn}, Err={ErrMsg}",
                result.OrderId, result.ThreeDStatus, result.ProcReturnCode, result.ErrMsg);
        }

        return result;
    }

    /// <summary>
    /// SHA1 hash hesaplar (banka gereksinimine göre).
    /// hashstr = MbrId + OrderId + Amount + OkUrl + FailUrl + TxnType + InstallmentCount + Rnd + MerchantPass
    /// </summary>
    private static string ComputeHash(
        string mbrId, string orderId, string amount,
        string okUrl, string failUrl,
        string txnType, string installmentCount, string rnd,
        string merchantPass)
    {
        var hashStr = mbrId + orderId + amount + okUrl + failUrl + txnType + installmentCount + rnd + merchantPass;
        var hashBytes = Encoding.UTF8.GetBytes(hashStr);
#pragma warning disable CA5350 // Bank requires SHA1
        var computed = SHA1.HashData(hashBytes);
#pragma warning restore CA5350
        return Convert.ToBase64String(computed);
    }

    private static string GenerateOrderId(string tenantId)
    {
        var prefix = tenantId.Length > 8 ? tenantId[..8] : tenantId;
        return $"INV-{prefix}-{DateTime.UtcNow:yyyyMMddHHmmssffff}";
    }

    private static string BuildAutoSubmitForm(string gatewayUrl, Dictionary<string, string> parameters)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'/></head>");
        sb.Append("<body onload='document.forms[0].submit()'>");
        sb.AppendFormat("<form action='{0}' method='post'>", HttpUtility.HtmlAttributeEncode(gatewayUrl));
        foreach (var (key, value) in parameters)
        {
            sb.AppendFormat("<input type='hidden' name='{0}' value='{1}'/>",
                HttpUtility.HtmlAttributeEncode(key),
                HttpUtility.HtmlAttributeEncode(value));
        }
        sb.Append("</form></body></html>");
        return sb.ToString();
    }

    private static string? GetFormValue(IDictionary<string, string> form, string key)
    {
        return form.TryGetValue(key, out var value) ? value?.Split(',')[0] : null;
    }
}

/// <summary>
/// QNB vPOS konfigürasyon ayarları — appsettings.json "QnbVPos" section.
/// </summary>
public sealed class QnbVPosSettings
{
    public string MbrId { get; set; } = "5";
    public string MerchantId { get; set; } = "";
    public string UserCode { get; set; } = "";
    public string UserPass { get; set; } = "";
    public string MerchantPass { get; set; } = "";
    public string GatewayUrl { get; set; } = "https://vpos.qnbfinansbank.com/Gateway/Default.aspx";
}
