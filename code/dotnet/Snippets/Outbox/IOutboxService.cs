namespace Snippets.Outbox;

/// <summary>
/// Service for managing outbox jobs.
/// </summary>
public interface IOutboxService
{
    // TODO: Create (or a separate interface?)

    /// <summary>
    /// Gets N jobs that match the given key.
    /// </summary>
    Task<List<IOutboxJobWrapper>> GetNext(OutboxGetParams @params, CancellationToken ct = default);

    /// <summary>
    /// Resolves the job based on the context.
    /// </summary>
    Task<IOutboxJobWrapper> Resolve(long id, OutboxResolveParams @params, CancellationToken ct = default);

    /// <summary>
    /// Activates a peeked job.
    /// </summary>
    Task<IOutboxJobWrapper> Activate(long id, OutboxActiveParams? @params = null, CancellationToken ct = default);
}

public class OutboxGetParams : OutboxTimeoutParams
{
    public required string Key { get; init; }

    public uint Count { get; init; } = 1;

    public bool ShouldPeek { get; init; }
}

public class OutboxResolveParams : OutboxReceiptParams
{
    public required OutboxResult Result { get; init; }
}

public class OutboxActiveParams : OutboxTimeoutParams;

public abstract class OutboxReceiptParams : OutboxTimeoutParams
{
    public Guid? Receipt { get; init; }
}

public abstract class OutboxTimeoutParams
{
    public TimeSpan? Timeout { get; init; }
}
