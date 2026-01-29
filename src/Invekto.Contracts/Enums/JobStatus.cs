namespace Invekto.Contracts.Enums;

/// <summary>
/// Status of an async job.
/// </summary>
public enum JobStatus
{
    /// <summary>Job is queued and waiting to be processed.</summary>
    Pending = 0,

    /// <summary>Job is currently being processed.</summary>
    Processing = 1,

    /// <summary>Job completed successfully.</summary>
    Completed = 2,

    /// <summary>Job failed and will be retried.</summary>
    Retrying = 3,

    /// <summary>Job failed permanently (moved to DLQ).</summary>
    Failed = 4,

    /// <summary>Job was cancelled.</summary>
    Cancelled = 5
}
