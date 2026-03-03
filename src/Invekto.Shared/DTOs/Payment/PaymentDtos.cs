using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Payment;

/// <summary>
/// QNB Finansbank 3DPay ödeme başlatma isteği.
/// </summary>
public sealed class PaymentInitRequest
{
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("card_number")]
    public string CardNumber { get; set; } = "";

    [JsonPropertyName("card_expire_month")]
    public string CardExpireMonth { get; set; } = "";

    [JsonPropertyName("card_expire_year")]
    public string CardExpireYear { get; set; } = "";

    [JsonPropertyName("cvv")]
    public string Cvv { get; set; } = "";

    [JsonPropertyName("card_holder_name")]
    public string CardHolderName { get; set; } = "";
}

/// <summary>
/// Ödeme başlatma sonucu — 3D yönlendirme HTML'i veya hata.
/// </summary>
public sealed class PaymentInitResult
{
    public bool Success { get; set; }
    public string? RedirectHtml { get; set; }
    public string? OrderId { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Banka callback'inden parse edilen parametreler.
/// </summary>
public sealed class PaymentCallbackData
{
    public string? MerchantId { get; set; }
    public string? AuthCode { get; set; }
    public string? ProcReturnCode { get; set; }
    public string? ThreeDStatus { get; set; }
    public string? ResponseRnd { get; set; }
    public string? ResponseHash { get; set; }
    public string? UserCode { get; set; }
    public string? TransId { get; set; }
    public string? ErrMsg { get; set; }
    public string? HostRefNum { get; set; }
    public string? OrderId { get; set; }
    public string? TxnResult { get; set; }
    public string? PurchAmount { get; set; }
    public string? TxnType { get; set; }
    public string? Rnd { get; set; }
    public string? TxnStatus { get; set; }

    /// <summary>
    /// 3D doğrulama başarılı VE banka işlemi onaylandı mı?
    /// </summary>
    public bool IsSuccessful => ThreeDStatus == "1" && ProcReturnCode == "00";
}

/// <summary>
/// Ödeme sonuç yanıtı (API response olarak döner).
/// </summary>
public sealed class PaymentResultResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("order_id")]
    public string? OrderId { get; set; }

    [JsonPropertyName("host_ref_num")]
    public string? HostRefNum { get; set; }

    [JsonPropertyName("auth_code")]
    public string? AuthCode { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}
