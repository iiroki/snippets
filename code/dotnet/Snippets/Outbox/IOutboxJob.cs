using System.Text.Json;

namespace Snippets.Outbox;

public interface IOutboxJob : IOutboxJob<JsonElement, JsonElement>;

public interface IOutboxJob<out TData, out TMetadata>
{
    /// <summary>
    /// Outbox job key.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Outbox job data.
    /// </summary>
    TData Data { get; }

    /// <summary>
    /// Outbox job metadata that contains additional information for the job handler.
    /// </summary>
    TMetadata Metadata { get; }
}

public interface IOutboxJobProperties
{
    /// <summary>
    /// Unique outbox job ID.
    /// </summary>
    long Id { get; }

    /// <summary>
    /// How many times the outbox job has been attempted.
    /// </summary>
    uint AttemptCount { get; }

    /// <summary>
    /// Receipt for managing the outbox job.
    /// </summary>
    Guid? Receipt { get; }
}

/// <summary>
/// Wrapper for binding an outbox job and its properties together.
/// </summary>
public interface IOutboxJobWrapper : IOutboxJob, IOutboxJobProperties;
