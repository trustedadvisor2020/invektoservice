namespace Invekto.Contracts.Constants;

/// <summary>
/// RabbitMQ exchange and queue names for Invekto services.
/// </summary>
public static class QueueNames
{
    #region Exchanges

    /// <summary>Direct exchange for routing by key</summary>
    public const string DirectExchange = "invekto.direct";

    /// <summary>Fanout exchange for broadcast</summary>
    public const string FanoutExchange = "invekto.fanout";

    /// <summary>Dead letter exchange</summary>
    public const string DeadLetterExchange = "invekto.dlx";

    /// <summary>Retry exchange with TTL queues</summary>
    public const string RetryExchange = "invekto.retry";

    #endregion

    #region Main Queues

    /// <summary>Chat analysis job queue</summary>
    public const string ChatAnalysisJobs = "invekto.jobs.chat-analysis";

    #endregion

    #region Retry Queues

    /// <summary>2 second retry queue</summary>
    public const string Retry2s = "invekto.retry.2s";

    /// <summary>10 second retry queue</summary>
    public const string Retry10s = "invekto.retry.10s";

    /// <summary>30 second retry queue</summary>
    public const string Retry30s = "invekto.retry.30s";

    /// <summary>2 minute retry queue</summary>
    public const string Retry2m = "invekto.retry.2m";

    /// <summary>10 minute retry queue</summary>
    public const string Retry10m = "invekto.retry.10m";

    #endregion

    #region Dead Letter Queue

    /// <summary>Dead letter queue for failed messages</summary>
    public const string DeadLetterQueue = "invekto.dlq";

    #endregion

    #region Routing Keys

    /// <summary>Chat analysis routing key</summary>
    public const string ChatAnalysisRoutingKey = "chat-analysis";

    #endregion
}
