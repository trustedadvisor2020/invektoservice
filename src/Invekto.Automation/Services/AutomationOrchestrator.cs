using System.Diagnostics;
using Invekto.Automation.Data;
using Invekto.Shared.Auth;
using Invekto.Shared.Constants;
using Invekto.Shared.DTOs.Integration;
using Invekto.Shared.Integration;
using Invekto.Shared.Logging;

namespace Invekto.Automation.Services;

/// <summary>
/// Orchestrates the full message processing pipeline:
/// Working hours check -> Flow engine -> FAQ match -> Intent detection -> Callback.
/// Thread-safe, register as singleton.
/// </summary>
public sealed class AutomationOrchestrator
{
    private readonly AutomationRepository _repo;
    private readonly FlowEngine _flowEngine;
    private readonly FaqMatcher _faqMatcher;
    private readonly IntentDetector _intentDetector;
    private readonly WorkingHoursChecker _workingHours;
    private readonly MainAppCallbackClient _callbackClient;
    private readonly JsonLinesLogger _logger;

    public AutomationOrchestrator(
        AutomationRepository repo,
        FlowEngine flowEngine,
        FaqMatcher faqMatcher,
        IntentDetector intentDetector,
        WorkingHoursChecker workingHours,
        MainAppCallbackClient callbackClient,
        JsonLinesLogger logger)
    {
        _repo = repo;
        _flowEngine = flowEngine;
        _faqMatcher = faqMatcher;
        _intentDetector = intentDetector;
        _workingHours = workingHours;
        _callbackClient = callbackClient;
        _logger = logger;
    }

    /// <summary>
    /// Process an incoming message through the full automation pipeline.
    /// Returns true if processing completed (success or graceful failure).
    /// </summary>
    public async Task<bool> ProcessMessageAsync(
        TenantContext tenant,
        IncomingWebhookEvent webhook,
        string requestId,
        string? callbackUrl,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var tenantId = tenant.TenantId;
        var chatId = webhook.ChatId;
        var phone = webhook.Data?.Phone;
        var messageText = webhook.Data?.MessageText ?? "";

        try
        {
            // 1. Get active flow config
            var flow = await _flowEngine.GetActiveFlowAsync(tenantId, ct);
            if (flow == null)
            {
                _logger.StepWarn($"No active flow for tenant {tenantId}, handing off to human", requestId);
                await SendHandoffAsync(requestId, tenantId, chatId, webhook.SequenceId,
                    "Chatbot akisi tanimlanmamis, mesaj temsilciye yonlendiriliyor", 0, callbackUrl, ct);
                return true;
            }

            // 2. Check working hours
            var (isWithinHours, offHoursMsg) = await _workingHours.CheckAsync(tenantId, ct);
            if (!isWithinHours)
            {
                var offReply = offHoursMsg ?? flow.OffHoursMessage ?? "Su anda mesai saatleri disindayiz. En kisa surede size donus yapacagiz.";
                sw.Stop();

                await SendCallbackAsync(requestId, tenantId, chatId, webhook.SequenceId,
                    CallbackActions.SendMessage, offReply, null, null, sw.ElapsedMilliseconds, callbackUrl, ct);

                await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, offReply,
                    "off_hours", null, null, (int)sw.ElapsedMilliseconds, ct);

                _logger.StepInfo("Off-hours auto reply sent", requestId, sw.ElapsedMilliseconds);
                return true;
            }

            // 3. Get or create chat session
            var session = await _repo.GetActiveSessionAsync(tenantId, chatId, ct);
            if (session == null)
            {
                await _repo.CreateSessionAsync(tenantId, chatId, phone, "welcome", ct);
                session = await _repo.GetActiveSessionAsync(tenantId, chatId, ct);
            }

            // 4. Process through flow engine
            var action = _flowEngine.ProcessInput(flow, session, messageText);

            switch (action.Type)
            {
                case FlowActionType.ShowWelcome:
                case FlowActionType.ShowMenu:
                case FlowActionType.StaticReply:
                case FlowActionType.UnknownInput:
                    sw.Stop();
                    await SendCallbackAsync(requestId, tenantId, chatId, webhook.SequenceId,
                        CallbackActions.SendMessage, action.ReplyText!, null, null, sw.ElapsedMilliseconds, callbackUrl, ct);

                    var replyType = action.Type switch
                    {
                        FlowActionType.ShowWelcome => "welcome",
                        FlowActionType.ShowMenu => "menu",
                        FlowActionType.StaticReply => "menu",
                        _ => "menu"
                    };
                    await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, action.ReplyText,
                        replyType, null, null, (int)sw.ElapsedMilliseconds, ct);

                    if (session != null)
                        await _repo.UpdateSessionAsync(session.Id, action.NextNode, null, ct);

                    return true;

                case FlowActionType.FaqSearch:
                    return await HandleFaqSearchAsync(requestId, tenantId, chatId, webhook.SequenceId,
                        phone, messageText, flow, session, callbackUrl, sw, ct);

                case FlowActionType.IntentDetection:
                    return await HandleIntentDetectionAsync(requestId, tenantId, chatId, webhook.SequenceId,
                        phone, messageText, flow, session, callbackUrl, sw, ct);

                case FlowActionType.Handoff:
                    sw.Stop();
                    await SendHandoffAsync(requestId, tenantId, chatId, webhook.SequenceId,
                        "Musteri temsilci ile gorusme talep etti", sw.ElapsedMilliseconds, callbackUrl, ct);

                    await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, null,
                        "handoff", null, null, (int)sw.ElapsedMilliseconds, ct);

                    if (session != null)
                        await _repo.EndSessionAsync(session.Id, "handed_off", ct);

                    return true;

                default:
                    _logger.SystemWarn($"Unknown flow action type: {action.Type}");
                    return false;
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.StepError($"Message processing failed: {ex.Message}", requestId, sw.ElapsedMilliseconds);
            return false;
        }
    }

    private async Task<bool> HandleFaqSearchAsync(
        string requestId, int tenantId, int chatId, long sequenceId,
        string? phone, string messageText, FlowConfig flow, ChatSession? session,
        string? callbackUrl, Stopwatch sw, CancellationToken ct)
    {
        // If this is the first entry to FAQ mode, prompt user to ask their question
        if (session?.CurrentNode != "faq")
        {
            sw.Stop();
            var promptMsg = "Sorunuzu yazin, size en uygun cevabi bulayim. Ana menuye donmek icin '0' yazin.";
            await SendCallbackAsync(requestId, tenantId, chatId, sequenceId,
                CallbackActions.SendMessage, promptMsg, null, null, sw.ElapsedMilliseconds, callbackUrl, ct);

            if (session != null)
                await _repo.UpdateSessionAsync(session.Id, "faq", null, ct);

            return true;
        }

        // Search FAQs
        var faqMatch = await _faqMatcher.FindMatchAsync(tenantId, messageText, ct);
        if (faqMatch != null && faqMatch.Confidence >= 0.3)
        {
            sw.Stop();
            var replyText = faqMatch.Answer + "\n\nBaska bir sorunuz var mi? Ana menu icin '0' yazin.";
            await SendCallbackAsync(requestId, tenantId, chatId, sequenceId,
                CallbackActions.SendMessage, replyText, "faq_match", faqMatch.Confidence, sw.ElapsedMilliseconds, callbackUrl, ct);

            await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, faqMatch.Answer,
                "faq", "faq_match", faqMatch.Confidence, (int)sw.ElapsedMilliseconds, ct);

            return true;
        }

        // No FAQ match -> fallback to intent detection
        return await HandleIntentDetectionAsync(requestId, tenantId, chatId, sequenceId,
            phone, messageText, flow, session, callbackUrl, sw, ct);
    }

    private async Task<bool> HandleIntentDetectionAsync(
        string requestId, int tenantId, int chatId, long sequenceId,
        string? phone, string messageText, FlowConfig flow, ChatSession? session,
        string? callbackUrl, Stopwatch sw, CancellationToken ct)
    {
        // If first entry to intent mode, prompt user
        if (session?.CurrentNode != "intent" && session?.CurrentNode != "faq")
        {
            sw.Stop();
            var promptMsg = "Sorunuzu veya talebinizi yazin. Ana menuye donmek icin '0' yazin.";
            await SendCallbackAsync(requestId, tenantId, chatId, sequenceId,
                CallbackActions.SendMessage, promptMsg, null, null, sw.ElapsedMilliseconds, callbackUrl, ct);

            if (session != null)
                await _repo.UpdateSessionAsync(session.Id, "intent", null, ct);

            return true;
        }

        // Run Claude intent detection
        var intentResult = await _intentDetector.DetectAsync(messageText, ct);

        if (intentResult == null)
        {
            // AI failed -> handoff (graceful degradation)
            sw.Stop();
            _logger.StepWarn("Intent detection returned null, falling back to handoff", requestId, sw.ElapsedMilliseconds);

            await SendHandoffAsync(requestId, tenantId, chatId, sequenceId,
                "Niyet algilama basarisiz, temsilciye yonlendiriliyor", sw.ElapsedMilliseconds, callbackUrl, ct);

            await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, null,
                "handoff", null, null, (int)sw.ElapsedMilliseconds, ct);

            if (session != null)
                await _repo.EndSessionAsync(session.Id, "handed_off", ct);

            return true;
        }

        // Check confidence threshold
        if (intentResult.Confidence < flow.HandoffConfidenceThreshold)
        {
            sw.Stop();
            await SendHandoffAsync(requestId, tenantId, chatId, sequenceId,
                $"Dusuk guven ({intentResult.Confidence:F2}): {intentResult.Summary}",
                sw.ElapsedMilliseconds, callbackUrl, ct);

            await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, null,
                "handoff", intentResult.Intent, intentResult.Confidence, (int)sw.ElapsedMilliseconds, ct);

            if (session != null)
                await _repo.EndSessionAsync(session.Id, "handed_off", ct);

            return true;
        }

        // High confidence -> send auto-reply with suggest_reply (let agent review)
        sw.Stop();
        var suggestionText = $"[AI {intentResult.Intent} ({intentResult.Confidence:F2})]: {intentResult.Summary}";

        await SendCallbackAsync(requestId, tenantId, chatId, sequenceId,
            CallbackActions.SuggestReply, suggestionText, intentResult.Intent, intentResult.Confidence,
            sw.ElapsedMilliseconds, callbackUrl, ct);

        await _repo.LogAutoReplyAsync(tenantId, chatId, phone, messageText, suggestionText,
            "intent", intentResult.Intent, intentResult.Confidence, (int)sw.ElapsedMilliseconds, ct);

        if (session != null)
            await _repo.UpdateSessionAsync(session.Id, "menu", null, ct);

        return true;
    }

    private async Task<bool> SendCallbackAsync(
        string requestId, int tenantId, int chatId, long sequenceId,
        string action, string messageText, string? intent, double? confidence,
        long processingTimeMs, string? callbackUrl, CancellationToken ct)
    {
        var callback = new OutgoingCallback
        {
            RequestId = requestId,
            Action = action,
            TenantId = tenantId,
            ChatId = chatId,
            SequenceId = sequenceId,
            Data = new CallbackData
            {
                MessageText = action == CallbackActions.SendMessage ? messageText : null,
                SuggestedReply = action == CallbackActions.SuggestReply ? messageText : null,
                Intent = intent,
                Confidence = confidence
            },
            ProcessingTimeMs = processingTimeMs
        };

        var delivered = await _callbackClient.SendCallbackAsync(callback, callbackUrl, ct);
        if (!delivered)
            _logger.StepError($"[{ErrorCodes.IntegrationCallbackFailed}] Callback delivery failed: action={action}, tenant={tenantId}, chat={chatId}", requestId, processingTimeMs);
        return delivered;
    }

    private async Task<bool> SendHandoffAsync(
        string requestId, int tenantId, int chatId, long sequenceId,
        string aiSummary, long processingTimeMs, string? callbackUrl, CancellationToken ct)
    {
        var callback = new OutgoingCallback
        {
            RequestId = requestId,
            Action = CallbackActions.HandoffToHuman,
            TenantId = tenantId,
            ChatId = chatId,
            SequenceId = sequenceId,
            Data = new CallbackData
            {
                HandoffToHuman = true,
                AiSummary = aiSummary
            },
            ProcessingTimeMs = processingTimeMs
        };

        var delivered = await _callbackClient.SendCallbackAsync(callback, callbackUrl, ct);
        if (!delivered)
            _logger.StepError($"[{ErrorCodes.IntegrationCallbackFailed}] Handoff callback delivery failed: tenant={tenantId}, chat={chatId}", requestId, processingTimeMs);
        return delivered;
    }
}
