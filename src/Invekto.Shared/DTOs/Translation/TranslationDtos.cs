using System.Text.Json.Serialization;

namespace Invekto.Shared.DTOs.Translation;

/// <summary>
/// Tek mesaj çeviri isteği + yanıtı (INMA echo-back contract).
/// İstek olarak gelir, translatedMessage doldurularak geri döner.
/// </summary>
public sealed class TranslateRequest
{
    [JsonPropertyName("userID")]
    public int UserID { get; set; }

    [JsonPropertyName("channelID")]
    public int ChannelID { get; set; }

    [JsonPropertyName("chatID")]
    public int ChatID { get; set; }

    [JsonPropertyName("messageID")]
    public int MessageID { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("translatedMessage")]
    public string TranslatedMessage { get; set; } = "";

    [JsonPropertyName("targetLanguage")]
    public string TargetLanguage { get; set; } = "";
}

/// <summary>
/// Toplu çeviri isteği (max 50 mesaj).
/// </summary>
public sealed class BatchTranslateRequest
{
    [JsonPropertyName("messages")]
    public List<BatchTranslateItem> Messages { get; set; } = new();

    [JsonPropertyName("target_language")]
    public string TargetLanguage { get; set; } = "";
}

/// <summary>
/// Toplu çevirideki tek mesaj.
/// </summary>
public sealed class BatchTranslateItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("source_language")]
    public string? SourceLanguage { get; set; }
}

/// <summary>
/// Toplu çeviri yanıtı.
/// </summary>
public sealed class BatchTranslateResponse
{
    [JsonPropertyName("translations")]
    public List<BatchTranslateResultItem> Translations { get; set; } = new();

    [JsonPropertyName("target_language")]
    public string TargetLanguage { get; set; } = "";

    [JsonPropertyName("cache_hits")]
    public int CacheHits { get; set; }

    [JsonPropertyName("api_calls")]
    public int ApiCalls { get; set; }
}

/// <summary>
/// Toplu çeviri sonuç öğesi.
/// </summary>
public sealed class BatchTranslateResultItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("translated_text")]
    public string TranslatedText { get; set; } = "";

    [JsonPropertyName("source_language")]
    public string SourceLanguage { get; set; } = "";

    [JsonPropertyName("from_cache")]
    public bool FromCache { get; set; }
}

/// <summary>
/// Dil algılama isteği.
/// </summary>
public sealed class DetectLanguageRequest
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = "";
}

/// <summary>
/// Dil algılama yanıtı.
/// </summary>
public sealed class DetectLanguageResponse
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "";

    [JsonPropertyName("language_name")]
    public string LanguageName { get; set; } = "";

    [JsonPropertyName("confidence")]
    public string Confidence { get; set; } = ""; // "high", "medium", "low"
}

/// <summary>
/// Desteklenen dil bilgisi.
/// </summary>
public sealed class SupportedLanguageInfo
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("native_name")]
    public string NativeName { get; set; } = "";
}
