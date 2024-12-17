using System.Text.Json;

namespace Snippets.Outbox;

/// <summary>
/// Outbox job handler.
/// </summary>
public interface IOutboxHandler
{
    /// <summary>
    /// Outbox job key.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// How many times the handler should attempt to handle an outbox job.
    /// </summary>
    uint? MaxAttemptCount { get; }

    /// <summary>
    /// Defines how long the handler should wait before attempting an outbox job again.
    /// </summary>
    TimeSpan? Timeout { get; }

    /// <summary>
    /// How many concurrent invocations the handler can handle at the same time.
    /// </summary>
    uint? Concurrency { get; }

    /// <summary>
    /// Whether an outbox job should only be peeked before handling it.
    /// </summary>
    bool ShouldPeek { get; }

    /// <summary>
    /// Handles an outbox job and provides a result what should be done with the job.
    /// </summary>
    Task<OutboxResult> Handle(IOutboxContext ctx, CancellationToken ct = default);
}

public class OutboxResult
{
    /// <summary>
    /// What to do with the handled outbox job.
    /// </summary>
    public required OutboxAction Action { get; init; }

    /// <summary>
    /// New metadata that should be updated.
    /// </summary>
    public JsonElement? Metadata { get; init; }
}

public enum OutboxAction
{
    Unknown,
    Complete,
    Update,
    Error,
}
